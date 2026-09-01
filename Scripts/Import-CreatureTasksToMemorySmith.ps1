#!/usr/bin/env pwsh
# Import-CreatureTasksToMemorySmith.ps1
# Imports CC Markdown tickets through the local MemorySmith MCP task tools.

param(
    [string]$Endpoint = 'http://127.0.0.1:7916/mcp',
    [string]$StatePath = $null,
    [string]$CookieHeader = $env:MEMORYSMITH_AUTH_COOKIE,
    [switch]$IncludeArchive = $true,
    [switch]$DryRun = $false,
    [switch]$ResetState = $false
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

if (-not $StatePath) {
    $StatePath = Join-Path $repoRoot '.service\memorysmith-import-state.json'
}

function Get-FrontMatterValue([string]$FrontMatter, [string]$Name) {
    $match = [regex]::Match($FrontMatter, "(?m)^$([regex]::Escape($Name)):\s*(.+)$")
    if (-not $match.Success) { return $null }
    return $match.Groups[1].Value.Trim().Trim('''').Trim('"')
}

function Get-FrontMatterList([string]$FrontMatter, [string]$Name) {
    $value = Get-FrontMatterValue $FrontMatter $Name
    if ([string]::IsNullOrWhiteSpace($value)) { return @() }
    if ($value.StartsWith('[') -and $value.EndsWith(']')) {
        return @($value.Trim('[', ']') -split ',' | ForEach-Object { $_.Trim().Trim('''').Trim('"') } | Where-Object { $_ })
    }
    return @($value)
}

function Convert-Priority([string]$Priority) {
    switch ($Priority.ToUpperInvariant()) {
        'P0' { return 'Critical' }
        'P1' { return 'High' }
        'P2' { return 'Medium' }
        'P3' { return 'Low' }
        default { throw "Unsupported priority '$Priority'." }
    }
}

function Convert-Status([string]$Status, [bool]$Archived) {
    if ($Archived -and $Status -eq 'Superseded') { return 'Archived' }
    switch ($Status) {
        'Backlog' { return 'Backlog' }
        'In Progress' { return 'InProgress' }
        'Review' { return 'Ready' }
        'Done' { return 'Done' }
        'Superseded' { return 'Archived' }
        default { throw "Unsupported status '$Status'." }
    }
}

function Read-Ticket([System.IO.FileInfo]$File) {
    $content = (Get-Content -LiteralPath $File.FullName -Raw).TrimStart([char]0xFEFF)
    $match = [regex]::Match($content, '\A---\r?\n(?<front>.*?)\r?\n---\r?\n(?<body>.*)\z', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) {
        $fallback = [regex]::Match($content, '\A---\r?\n(?<front>.*?)\r?\n(?<body>##\s+Summary\b.*)\z', [System.Text.RegularExpressions.RegexOptions]::Singleline)
        if (-not $fallback.Success) { throw "$($File.Name): missing YAML frontmatter boundary." }
        $match = $fallback
    }

    $key = Get-FrontMatterValue $match.Groups['front'].Value 'key'
    if ($key -notmatch '^CC-\d+[A-Z]?$') { throw "$($File.Name): invalid CC key '$key'." }
    $status = Get-FrontMatterValue $match.Groups['front'].Value 'status'
    $priority = Get-FrontMatterValue $match.Groups['front'].Value 'priority'
    $tags = @(Get-FrontMatterList $match.Groups['front'].Value 'tags')
    $archived = $File.Directory.Name -eq 'archive'
    $relative = $File.FullName.Substring($repoRoot.Path.Length + 1).Replace('\', '/')
    $body = $match.Groups['body'].Value.Trim()
    $description = "Source: $relative`nSource key: $key`nSource status: $status`nSource priority: $priority`n`n$body"
    $labels = @('source-cc', $key.ToLowerInvariant()) + @($tags | Where-Object { $_ -and $_ -notmatch '^P\d$' })
    if ($archived) { $labels += 'source-archived' }
    if ($status -eq 'Superseded') { $labels += 'source-superseded' }

    return [pscustomobject]@{
        Source = $relative
        Key = $key
        Title = Get-FrontMatterValue $match.Groups['front'].Value 'title'
        Description = $description
        Type = Get-FrontMatterValue $match.Groups['front'].Value 'type'
        Priority = Convert-Priority $priority
        Status = Convert-Status $status $archived
        Labels = @($labels | Select-Object -Unique)
    }
}

function Invoke-MemorySmith([string]$Method, [hashtable]$Arguments, [int]$RequestId) {
    $payload = @{ jsonrpc = '2.0'; id = $RequestId; method = 'tools/call'; params = @{ name = $Method; arguments = $Arguments } } | ConvertTo-Json -Depth 12
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($CookieHeader)) { $headers['Cookie'] = $CookieHeader }
    $response = Invoke-RestMethod -Uri $Endpoint -Method Post -ContentType 'application/json' -Headers $headers -Body $payload
    if ($response.error) { throw "MCP $Method failed: $($response.error.message)" }
    $text = @($response.result.content | Where-Object { $_.type -eq 'text' } | Select-Object -ExpandProperty text) -join "`n"
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    for ($attempt = 0; $attempt -lt 2; $attempt++) {
        try {
            $parsed = $text | ConvertFrom-Json
            if ($parsed -is [string]) { $text = $parsed; continue }
            return $parsed
        } catch { return $text }
    }
    return $text
}

if ($ResetState -and (Test-Path -LiteralPath $StatePath)) { Remove-Item -LiteralPath $StatePath -Force }
$state = @{ sources = @{} }
if (Test-Path -LiteralPath $StatePath) {
    $loaded = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
    foreach ($property in $loaded.sources.PSObject.Properties) { $state.sources[$property.Name] = $property.Value }
}

$files = @(Get-ChildItem (Join-Path $repoRoot 'docs/tasks/tickets') -Filter 'CC-*.md' -File)
if ($IncludeArchive) { $files += @(Get-ChildItem (Join-Path $repoRoot 'docs/tasks/archive') -Filter 'CC-*.md' -File) }
$tickets = @($files | Sort-Object Name | ForEach-Object { Read-Ticket $_ })

if (-not $DryRun) {
    foreach ($ticket in $tickets) {
        if ($state.sources.ContainsKey($ticket.Source)) { continue }
        $existing = Invoke-MemorySmith 'memorysmith_task_list' @{ query = $ticket.Key; limit = 10 } 999
        $match = @($existing.tasks | Where-Object { $_.title -eq $ticket.Title }) |
            Sort-Object { [int]($_.key -replace '^TSK-', '') }
        if ($match.Count -ge 1) {
            $state.sources[$ticket.Source] = [string]$match[0].id
            Write-Output ("RECONCILED {0} => {1}" -f $ticket.Key, $match[0].id)
        }
    }
    $state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $StatePath -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($CookieHeader)) {
        throw 'MemorySmith write access requires an authenticated session. Set MEMORYSMITH_AUTH_COOKIE in this PowerShell session before running the importer.'
    }
}

$requestId = 1000
$created = 0
$skipped = 0

foreach ($ticket in $tickets) {
    if ($state.sources.ContainsKey($ticket.Source)) { $skipped++; continue }
    if ($DryRun) {
        Write-Output ("DRY_RUN {0} => {1} [{2}, {3}]" -f $ticket.Key, $ticket.Title, $ticket.Status, $ticket.Priority)
        continue
    }

    $result = Invoke-MemorySmith 'memorysmith_task_create' @{
        title = $ticket.Title
        description = $ticket.Description
        type = $ticket.Type
        status = $ticket.Status
        priority = $ticket.Priority
        assigneeMode = 'Custom'
        assigneeCustomText = 'Unassigned'
        reporter = 'cc-markdown-import'
        labels = $ticket.Labels
    } $requestId
    $requestId++
    $taskId = [string]$result.Task.id
    if ([string]::IsNullOrWhiteSpace($taskId)) { $taskId = [string]$result.id }
    if ([string]::IsNullOrWhiteSpace($taskId)) { throw "Create returned no task id for $($ticket.Key): $($result | ConvertTo-Json -Depth 8 -Compress)" }
    $state.sources[$ticket.Source] = $taskId
    $created++
    Write-Output ("IMPORTED {0} => {1}" -f $ticket.Key, $taskId)
    $state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $StatePath -Encoding UTF8
}

if (-not $DryRun) {
    $state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $StatePath -Encoding UTF8
}
Write-Output ("IMPORT_SUMMARY total={0} created={1} skipped={2} dryRun={3}" -f $tickets.Count, $created, $skipped, $DryRun.IsPresent)