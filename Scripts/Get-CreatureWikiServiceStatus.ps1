<#
.SYNOPSIS
Displays the status of the MemorySmith CreatureCreator Wiki service.

.DESCRIPTION
Adapted from MemorySmith.Agent\Scripts\Get-CodebaseWikiStatus.ps1.
#>
[CmdletBinding()]
param(
  [string]$ServiceName = "MemorySmith - CreatureCreator Wiki"
)

$repoRoot   = Split-Path -Parent $PSScriptRoot
$serviceDir = Join-Path $repoRoot ".service"
$portFile   = Join-Path $serviceDir "creature-wiki.port"
$logFile    = Join-Path $serviceDir "creature-wiki.log"
$errFile    = Join-Path $serviceDir "creature-wiki.err.log"

$svc  = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
$port = if (Test-Path $portFile) { (Get-Content $portFile -Raw).Trim() } else { "unknown" }

if ($null -ne $svc) {
  Write-Host "Mode          : windows-service"
  Write-Host "ServiceName   : $ServiceName"
  Write-Host "ServiceStatus : $($svc.Status)"
  Write-Host "Port          : $port"
  Write-Host "URL           : http://127.0.0.1:$port"
  if (Test-Path $logFile) { Write-Host "OutLog        : $logFile" }
  if (Test-Path $errFile) { Write-Host "ErrLog        : $errFile" }
  return
}

Write-Host "Status: stopped or not installed"
