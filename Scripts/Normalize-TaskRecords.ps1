#!/usr/bin/env pwsh
# Normalize-TaskRecords.ps1 - Bulk normalize CreatureCreator task records.
# Adapted from MemorySmith.Agent's bulk-normalize script. Fixes id/format
# drift, derives missing keys, repairs filename/key drift, and strips priority labels.
#
# Safety contract: normalization is transactional with respect to task identity.
# Before any file is written, the script computes the post-normalization id/key for
# every record and fails closed if those identities collide. A conflicting repair
# must be reconciled explicitly rather than silently creating duplicate ownership.

param(
    [string]$TasksRoot = $null
)

$ErrorActionPreference = 'Stop'

if (-not $TasksRoot) {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
    $taskDir = Join-Path $repoRoot 'Data/Tasks'
} else {
    $taskDir = $TasksRoot
}

if (-not (Test-Path -LiteralPath $taskDir)) {
    throw "Task records directory not found: $taskDir"
}

$files = @(Get-ChildItem "$taskDir\*.json" -File | Sort-Object Name)
$parsed = New-Object 'System.Collections.Generic.List[object]'
$fixed = 0
$skipped = 0

# Preflight: parse every record and compute the identity it would have after
# normalization. No writes occur before this pass succeeds.
foreach ($file in $files) {
    try {
        $task = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
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
        File = $file
        Task = $task
        Id = $id
        Key = $key
        NormalizedId = $normalizedId
        NormalizedKey = $normalizedKey
        ExpectedId = $expectedId
        ExpectedKey = $expectedKey
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

    if ($needSave) {
        $task | ConvertTo-Json -Depth 10 | Set-Content $file.FullName -NoNewline
        $fixed++
    }
}

Write-Host "`nFixed: $fixed  Skipped: $skipped"