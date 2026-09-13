#!/usr/bin/env pwsh
# Normalize-TaskRecords.ps1 - Bulk normalize CreatureCreator task records.
# Adapted from MemorySmith.Agent's bulk-normalize script. Fixes id/format
# drift, derives missing keys, repairs filename/key drift, and strips priority labels.
#
# Safety contract: normalization is transactional with respect to task identity.
# Before any file is written, the script computes the post-normalization id/key for
# every record and fails closed if those identities collide. A conflicting repair
# must be reconciled explicitly rather than silently creating duplicate ownership.
#
# Task identity is immutable through the MemorySmith task API, so a collision
# backlog is reconciled through a reviewed -RenumberMap file (source file, new id,
# new key, note). The map is validated in full before any file is touched and is
# idempotent once applied. Normalization also backfills required schema fields
# (type, createdAtUtc, updatedAtUtc, revision) that the record contract requires,
# and coerces string external links into the object shape the strict reader expects.

param(
    [string]$TasksRoot = $null,
    [string]$RenumberMap = $null
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
if (-not $TasksRoot) {
    $taskDir = Join-Path $repoRoot 'Data/Tasks'
} else {
    $taskDir = $TasksRoot
}

if (-not (Test-Path -LiteralPath $taskDir)) {
    throw "Task records directory not found: $taskDir"
}

function Get-TaskKeyFromId {
    param([string]$TaskId)
    $match = [regex]::Match($TaskId, '^tsk-(\d{4,})(?:-|$)')
    if ($match.Success) { return "TSK-$($match.Groups[1].Value)" }
    return $null
}

# Missing timestamps use the file's git addition date so the value is stable
# across clones; the file-system write time is a deterministic fallback.
function Get-TaskRecordTimestamp {
    param([System.IO.FileInfo]$File)

    $relative = $null
    if ($File.FullName.StartsWith($repoRoot.Path, [System.StringComparison]::OrdinalIgnoreCase)) {
        $relative = $File.FullName.Substring($repoRoot.Path.Length).TrimStart('\', '/')
    }

    if ($relative) {
        try {
            $added = (& git -C $repoRoot.Path log --diff-filter=A --follow --format=%aI -- $relative 2>$null | Select-Object -Last 1)
            if ($added) {
                return ([datetimeoffset]::Parse($added.Trim())).UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
            }
        } catch { }
    }

    return $File.LastWriteTimeUtc.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
}

# Re-emit any timestamp value as ISO 8601 UTC. ConvertFrom-Json can hand back a
# DateTime, and the strict MemorySmith reader rejects a culture-formatted string.
function ConvertTo-IsoUtc {
    param($Value)
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return $null }
    if ($Value -is [datetime]) { return $Value.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ') }
    if ($Value -is [datetimeoffset]) { return $Value.UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ') }
    try { return ([datetimeoffset]::Parse([string]$Value)).UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ') } catch { return $null }
}

# A migrated string external link has no label; derive one from the URL path when
# it is meaningful, otherwise keep the URL itself so nothing is invented.
function Get-ExternalLinkLabel {
    param([string]$Url)
    try {
        $path = ([System.Uri]$Url).AbsolutePath.TrimEnd('/')
        if ($path) {
            $segment = $path.Substring($path.LastIndexOf('/') + 1)
            if ($segment -match '[A-Za-z]') { return [System.IO.Path]::GetFileNameWithoutExtension($segment) }
        }
    } catch { }
    return $Url
}

# The MemorySmith reader uses strict System.Text.Json. ConvertFrom-Json tolerates a
# raw, unescaped control character inside a string that the reader rejects.
function Test-StrictJson {
    param([string]$Raw)
    try {
        [System.Text.Json.JsonDocument]::Parse($Raw).Dispose()
        return $true
    } catch {
        return $false
    }
}

# Task identity (id/key) is immutable through the MemorySmith task API, so a
# task-key collision is reconciled through a reviewed, explicit map file. The
# whole map is validated before any file is touched, and the pass is idempotent
# because a reopened map finds its sources already renamed.
function Invoke-TaskRenumberMap {
    param([string]$MapPath, [string]$TaskDir)

    if (-not (Test-Path -LiteralPath $MapPath)) {
        throw "Renumber map not found: $MapPath"
    }

    $entries = @(Get-Content -LiteralPath $MapPath -Raw | ConvertFrom-Json)
    if ($entries.Count -eq 0) { return }

    $existing = @{}
    foreach ($file in (Get-ChildItem "$TaskDir\*.json" -File)) {
        $existing[$file.Name.ToLowerInvariant()] = $file
    }

    # Exclude this map's own targets from the ceiling so a fully applied map can
    # be re-run without tripping its own key-allocation check.
    $mapTargetKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $entries) { [void]$mapTargetKeys.Add([string]$entry.newKey) }

    $maxKey = 0
    foreach ($file in $existing.Values) {
        $key = Get-TaskKeyFromId $file.BaseName
        if ($key -and $key -match '^TSK-(\d{4,})$' -and -not $mapTargetKeys.Contains($key)) {
            $maxKey = [Math]::Max($maxKey, [int]$Matches[1])
        }
    }

    $targets = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $pending = New-Object 'System.Collections.Generic.List[object]'

    foreach ($entry in $entries) {
        foreach ($field in @('file', 'newId', 'newKey', 'note')) {
            if ([string]::IsNullOrWhiteSpace([string]$entry.$field)) {
                throw "Renumber map entry is missing '$field': $($entry | ConvertTo-Json -Compress)"
            }
        }

        $sourceName = [string]$entry.file
        $newId = [string]$entry.newId
        $newKey = [string]$entry.newKey

        if (-not $existing.ContainsKey($sourceName.ToLowerInvariant())) {
            if ($existing.ContainsKey("$newId.json".ToLowerInvariant())) {
                Write-Host "$sourceName already applied as $newId; skipping."
                continue
            }
            throw "Renumber map source file not found: $sourceName"
        }
        if ($newId -notmatch '^tsk-\d{4,}-[a-z0-9-]+$') {
            throw "Renumber map newId '$newId' does not match tsk-0000-slug format."
        }
        if ($newKey -notmatch '^TSK-\d{4,}$' -or -not $newId.StartsWith($newKey.ToLowerInvariant() + '-', [System.StringComparison]::Ordinal)) {
            throw "Renumber map newKey '$newKey' is not the numeric prefix of newId '$newId'."
        }
        if ([int]$newKey.Substring(4) -le $maxKey) {
            throw "Renumber map newKey '$newKey' must be greater than the current maximum key TSK-$maxKey."
        }
        if ($existing.ContainsKey("$newId.json".ToLowerInvariant())) {
            throw "Renumber map target '$newId' already exists."
        }
        if (-not $targets.Add($newKey) -or -not $targets.Add($newId)) {
            throw "Renumber map target '$newId' or '$newKey' is used more than once."
        }

        [void]$pending.Add([pscustomobject]@{
                Entry  = $entry
                Source = $existing[$sourceName.ToLowerInvariant()]
                NewId  = $newId
                NewKey = $newKey
            })
    }

    foreach ($item in $pending) {
        $entry = $item.Entry
        $source = $item.Source
        $newId = $item.NewId
        $newKey = $item.NewKey
        $targetPath = Join-Path $TaskDir "$newId.json"

        $task = Get-Content -LiteralPath $source.FullName -Raw | ConvertFrom-Json
        $oldKey = [string]$task.key

        $task.id = $newId
        $task.key = $newKey
        if ($null -ne $task.PSObject.Properties['sourceFilePath'] -and -not [string]::IsNullOrWhiteSpace([string]$task.sourceFilePath)) {
            $task.sourceFilePath = "$newId.json"
        }
        $task.description = ([string]$task.description).TrimEnd() + "`n`n## Reconciliation`n$([string]$entry.note)"
        $now = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
        if ($null -ne $task.PSObject.Properties['updatedAtUtc']) {
            $task.updatedAtUtc = $now
        } else {
            $task | Add-Member -NotePropertyName updatedAtUtc -NotePropertyValue $now -Force
        }
        if ($null -ne $task.PSObject.Properties['revision'] -and -not [string]::IsNullOrWhiteSpace([string]$task.revision)) {
            $task.revision = [int]$task.revision + 1
        } else {
            $task | Add-Member -NotePropertyName revision -NotePropertyValue 1 -Force
        }

        $task | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $targetPath -NoNewline
        Remove-Item -LiteralPath $source.FullName -Force
        Write-Host "$($source.Name) -> $newId (key $oldKey -> $newKey)"
    }
}

if ($RenumberMap) {
    Invoke-TaskRenumberMap -MapPath $RenumberMap -TaskDir $taskDir
}

$files = @(Get-ChildItem "$taskDir\*.json" -File | Sort-Object Name)
$parsed = New-Object 'System.Collections.Generic.List[object]'
$fixed = 0
$skipped = 0

# Preflight: parse every record and compute the identity it would have after
# normalization. No writes occur before this pass succeeds.
foreach ($file in $files) {
    $rawText = Get-Content -LiteralPath $file.FullName -Raw
    try {
        $task = $rawText | ConvertFrom-Json
    } catch {
        Write-Warning "INVALID: $($file.Name) - not valid JSON: $($_.Exception.Message)"
        $skipped++
        continue
    }

    $id = [string]$task.id
    $key = [string]$task.key
    $expectedId = $file.BaseName
    $expectedKeyMatch = [regex]::Match($expectedId, '^tsk-(\d{4,})(?:-|$)')
    $expectedKey = if ($expectedKeyMatch.Success) { "TSK-$($expectedKeyMatch.Groups[1].Value)" } else { $null }

    $normalizedId = $id
    if ($id -match '^TSK-\d{4,}$' -and $expectedId -match '^tsk-\d{4,}-') {
        $normalizedId = $expectedId
    }

    $normalizedKey = $key
    if ([string]::IsNullOrWhiteSpace($key)) {
        $normalizedKey = $expectedKey
    }
    elseif ($expectedKey -and $key -ne $expectedKey) {
        $normalizedKey = $expectedKey
    }

    [void]$parsed.Add([pscustomobject]@{
            File          = $file
            Task          = $task
            Raw           = $rawText
            Id            = $id
            Key           = $key
            NormalizedId  = $normalizedId
            NormalizedKey = $normalizedKey
            ExpectedId    = $expectedId
            ExpectedKey   = $expectedKey
        })
}

# A skipped record means preflight did not inspect the complete task set. Do not
# normalize any subset because doing so would violate the all-records safety contract.
if ($skipped -gt 0) {
    Write-Host "FAIL: Normalization preflight skipped $skipped invalid task record(s). No files were modified." -ForegroundColor Red
    throw 'Task record normalization aborted because every task record must be parseable before normalization.'
}

$identityErrors = New-Object 'System.Collections.Generic.List[string]'

$parsed | Where-Object { -not [string]::IsNullOrWhiteSpace($_.NormalizedId) } |
    Group-Object { $_.NormalizedId.ToLowerInvariant() } |
    Where-Object Count -gt 1 |
    ForEach-Object {
        $filesForIdentity = ($_.Group | Sort-Object { $_.File.Name } | ForEach-Object { $_.File.Name }) -join ', '
        [void]$identityErrors.Add("Duplicate normalized task id '$($_.Name)' in $filesForIdentity")
    }

$parsed | Where-Object { -not [string]::IsNullOrWhiteSpace($_.NormalizedKey) } |
    Group-Object { $_.NormalizedKey.ToUpperInvariant() } |
    Where-Object Count -gt 1 |
    ForEach-Object {
        $filesForIdentity = ($_.Group | Sort-Object { $_.File.Name } | ForEach-Object { $_.File.Name }) -join ', '
        [void]$identityErrors.Add("Duplicate normalized task key '$($_.Name)' in $filesForIdentity")
    }

if ($identityErrors.Count -gt 0) {
    Write-Host "FAIL: Normalization preflight found $($identityErrors.Count) task-identity collision(s). No files were modified." -ForegroundColor Red
    $identityErrors | ForEach-Object { Write-Host (" - " + $_) -ForegroundColor Red }
    throw 'Task record normalization aborted because identity collisions must be reconciled explicitly.'
}

foreach ($record in $parsed) {
    $file = $record.File
    $task = $record.Task
    $needSave = $false
    $expectedId = $record.ExpectedId
    $expectedKey = $record.ExpectedKey

    $id = [string]$task.id
    $key = [string]$task.key

    # Fix 1: If id = "TSK-XXXX" but filename = "tsk-XXXX-slug.json", set id = filename
    if ($id -match '^TSK-\d{4,}$' -and $expectedId -match '^tsk-\d{4,}-') {
        $task.id = $expectedId
        Write-Host "$($file.Name): fixed id '$id' -> '$expectedId'"
        $needSave = $true
    }

    # Fix 2: Derive a missing key from the filename.
    if ([string]::IsNullOrWhiteSpace($key)) {
        if ($expectedKey) {
            $task.key = $expectedKey
            Write-Host "$($file.Name): added key '$expectedKey'"
            $needSave = $true
        }
    }
    # Fix 2b: A present key must remain consistent with the filename-derived key.
    # The preflight above guarantees that this repair cannot collide with another
    # record's post-normalization identity.
    elseif ($expectedKey -and $key -ne $expectedKey) {
        $task.key = $expectedKey
        Write-Host "$($file.Name): fixed key '$key' -> '$expectedKey'"
        $needSave = $true
    }

    # Fix 3: Remove priority labels (P0, P1, etc.) from labels array
    if ($task.labels) {
        $cleaned = [System.Collections.Generic.List[object]]::new()
        $removedCount = 0
        foreach ($label in $task.labels) {
            $ls = [string]$label
            if ($ls -match '^[Pp][0-9]$') {
                $removedCount++
            } else {
                [void]$cleaned.Add($ls)
            }
        }
        if ($removedCount -gt 0) {
            $task.labels = $cleaned.ToArray()
            Write-Host "$($file.Name): removed $removedCount priority label(s)"
            $needSave = $true
        }
    }

    # Fix 4: Backfill required schema fields that the record contract and
    # Test-TaskRecords.ps1 require.
    if ($null -eq $task.PSObject.Properties['type'] -or [string]::IsNullOrWhiteSpace([string]$task.type)) {
        $task | Add-Member -NotePropertyName type -NotePropertyValue 'Task' -Force
        Write-Host "$($file.Name): added missing type 'Task'"
        $needSave = $true
    }

    $createdMissing = $null -eq $task.PSObject.Properties['createdAtUtc'] -or [string]::IsNullOrWhiteSpace([string]$task.createdAtUtc)
    $updatedMissing = $null -eq $task.PSObject.Properties['updatedAtUtc'] -or [string]::IsNullOrWhiteSpace([string]$task.updatedAtUtc)
    if ($createdMissing -or $updatedMissing) {
        $timestamp = if ($createdMissing) { $null } else { ConvertTo-IsoUtc $task.createdAtUtc }
        if (-not $timestamp) { $timestamp = Get-TaskRecordTimestamp -File $file }
        if ($createdMissing) {
            $task | Add-Member -NotePropertyName createdAtUtc -NotePropertyValue $timestamp -Force
            Write-Host "$($file.Name): added missing createdAtUtc '$timestamp'"
        }
        if ($updatedMissing) {
            $task | Add-Member -NotePropertyName updatedAtUtc -NotePropertyValue $timestamp -Force
            Write-Host "$($file.Name): added missing updatedAtUtc '$timestamp'"
        }
        $needSave = $true
    }

    if ($null -eq $task.PSObject.Properties['revision'] -or [string]::IsNullOrWhiteSpace([string]$task.revision)) {
        $task | Add-Member -NotePropertyName revision -NotePropertyValue 1 -Force
        Write-Host "$($file.Name): added missing revision 1"
        $needSave = $true
    }

    # Fix 5: Normalize external links to the TaskExternalLink object shape the
    # strict MemorySmith reader requires ({ id, label, url, addedAtUtc }). A
    # string entry, or a non-ISO addedAtUtc, makes the reader reject the record.
    if ($task.externalLinks) {
        $normalizedLinks = [System.Collections.Generic.List[object]]::new()
        $changedLinks = 0
        $fallbackAddedAt = ConvertTo-IsoUtc $task.createdAtUtc
        if (-not $fallbackAddedAt) { $fallbackAddedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ') }
        foreach ($link in $task.externalLinks) {
            if ($link -is [string]) {
                $url = [string]$link
                $sha = [System.Security.Cryptography.SHA256]::Create()
                $hash = ([System.BitConverter]::ToString($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($url)))).Replace('-', '').ToLowerInvariant().Substring(0, 32)
                [void]$normalizedLinks.Add([pscustomobject]@{
                        id         = "l-$hash"
                        label      = Get-ExternalLinkLabel $url
                        url        = $url
                        addedAtUtc = $fallbackAddedAt
                    })
                $changedLinks++
            } else {
                $linkChanged = $false
                $currentLabel = [string]$link.label
                if ([string]::IsNullOrWhiteSpace($currentLabel) -or $currentLabel -match '^\d+$') {
                    $link | Add-Member -NotePropertyName label -NotePropertyValue (Get-ExternalLinkLabel ([string]$link.url)) -Force
                    $linkChanged = $true
                }
                # A DateTime here means the file text was already ISO; only a
                # non-ISO string (or a missing value) needs repair.
                $rawAdded = $link.addedAtUtc
                $addedIsIso = ($rawAdded -is [datetime]) -or ($rawAdded -is [datetimeoffset]) -or ([string]$rawAdded -match '^\d{4}-\d{2}-\d{2}T')
                if (-not $addedIsIso) {
                    $link | Add-Member -NotePropertyName addedAtUtc -NotePropertyValue $fallbackAddedAt -Force
                    $linkChanged = $true
                }
                if ($linkChanged) { $changedLinks++ }
                [void]$normalizedLinks.Add($link)
            }
        }
        if ($changedLinks -gt 0) {
            $task.externalLinks = $normalizedLinks.ToArray()
            Write-Host "$($file.Name): normalized $changedLinks external link(s)"
            $needSave = $true
        }
    }

    # Fix 6: Re-serialize when the raw text is not strictly valid JSON. A raw,
    # unescaped control character inside a string is accepted by ConvertFrom-Json
    # but rejected by the MemorySmith reader; writing the parsed record back
    # escapes it.
    if (-not (Test-StrictJson $record.Raw)) {
        Write-Host "$($file.Name): re-serializing to escape invalid raw control character(s)"
        $needSave = $true
    }

    if ($needSave) {
        $task | ConvertTo-Json -Depth 10 | Set-Content $file.FullName -NoNewline
        $fixed++
    }
}

Write-Host "`nFixed: $fixed  Skipped: $skipped"