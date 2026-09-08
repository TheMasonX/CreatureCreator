#!/usr/bin/env pwsh
# Normalize-TaskRecords.ps1 - Bulk normalize CreatureCreator task records.
# Adapted from MemorySmith.Agent's bulk-normalize script. Fixes id/format
# drift, derives missing keys, repairs filename/key drift, and strips priority labels.

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

$fixed = 0
$skipped = 0

Get-ChildItem "$taskDir\*.json" | Sort-Object Name | ForEach-Object {
    $path = $_.FullName
    $needSave = $false

    try {
        $task = Get-Content $path -Raw | ConvertFrom-Json
    } catch {
        Write-Warning "SKIP: $($_.Name) - not valid JSON: $($_.Exception.Message)"
        $skipped++
        return
    }

    $id = [string]$task.id
    $key = [string]$task.key
    $expectedId = $_.BaseName
    $expectedKeyMatch = [regex]::Match($expectedId, '^tsk-(\d{4,})(?:-|$)')
    $expectedKey = if ($expectedKeyMatch.Success) { "TSK-$($expectedKeyMatch.Groups[1].Value)" } else { $null }

    # Fix 1: If id = "TSK-XXXX" but filename = "tsk-XXXX-slug.json", set id = filename
    if ($id -match '^TSK-\d{4,}$' -and $expectedId -match '^tsk-\d{4,}-') {
        $task.id = $expectedId
        Write-Host "$($_.Name): fixed id '$id' -> '$expectedId'"
        $needSave = $true
    }

    # Fix 2: Derive a missing key from the filename.
    if ([string]::IsNullOrWhiteSpace($key)) {
        if ($expectedKey) {
            $task.key = $expectedKey
            Write-Host "$($_.Name): added key '$expectedKey'"
            $needSave = $true
        }
    }
    # Fix 2b: A present key must remain consistent with the filename-derived key.
    # This is intentionally deterministic: task identity is keyed by the canonical
    # filename, so stale keys are worse than a loud, reproducible normalization.
    elseif ($expectedKey -and $key -ne $expectedKey) {
        $task.key = $expectedKey
        Write-Host "$($_.Name): fixed key '$key' -> '$expectedKey'"
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
            Write-Host "$($_.Name): removed $removedCount priority label(s)"
            $needSave = $true
        }
    }

    if ($needSave) {
        $task | ConvertTo-Json -Depth 10 | Set-Content $path -NoNewline
        $fixed++
    }
}

Write-Host "`nFixed: $fixed  Skipped: $skipped"