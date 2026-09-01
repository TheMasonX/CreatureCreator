#!/usr/bin/env pwsh
# Normalize-TaskRecords.ps1 - Bulk normalize CreatureCreator task records.
# Adapted from MemorySmith.Agent's bulk-normalize script. Fixes id/format
# drift, derives missing keys, and strips priority labels.

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
        try { Write-Host "SKIP: $($_.Name) - not valid JSON: $($_.Exception.Message)" -ForegroundColor Yellow } catch {}
        $skipped++
        return
    }

    $id = [string]$task.id
    $key = [string]$task.key
    $expectedId = $_.BaseName  # The full slug from filename

    # Fix 1: If id = "TSK-XXXX" but filename = "tsk-XXXX-slug.json", set id = filename
    if ($id -match '^TSK-\d{4,}$' -and $expectedId -match '^tsk-\d{4,}-') {
        $task.id = $expectedId
        Write-Host "$($_.Name): fixed id '$id' -> '$expectedId'"
        $needSave = $true
    }

    # Fix 2: If key is missing, derive from filename
    if ([string]::IsNullOrWhiteSpace($key)) {
        $idMatch = [regex]::Match($expectedId, '^tsk-(\d{4,})')
        if ($idMatch.Success) {
            $task | Add-Member -NotePropertyName 'key' -NotePropertyValue "TSK-$($idMatch.Groups[1].Value)" -Force
            Write-Host "$($_.Name): added key 'TSK-$($idMatch.Groups[1].Value)'"
            $needSave = $true
        }
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
