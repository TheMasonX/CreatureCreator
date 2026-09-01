<#
.SYNOPSIS
Stops and uninstalls the MemorySmith CreatureCreator Wiki Windows service.

.DESCRIPTION
Adapted from MemorySmith.Agent\Scripts\Uninstall-CodebaseWikiService.ps1.
#>
[CmdletBinding()]
param(
  [string]$ServiceName = "MemorySmith - CreatureCreator Wiki"
)

$ErrorActionPreference = "Stop"
$repoRoot   = Split-Path -Parent $PSScriptRoot
$appDll     = Join-Path $repoRoot "artifacts/MemorySmith.App/MemorySmith.App.dll"

# Stop first
& (Join-Path $PSScriptRoot "Stop-CreatureWikiService.ps1") -Quiet -ServiceName $ServiceName

if (-not (Test-Path $appDll)) {
  Write-Host "Published MemorySmith.App.dll not found in artifacts/MemorySmith.App."
  Write-Host "Service may still be registered. Use sc.exe or Service Manager to remove it manually."
  return
}

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $svc) {
  Write-Host "Service '$ServiceName' is not installed."
  return
}

& dotnet $appDll uninstall --service-name $ServiceName | Out-Host
Write-Host "Uninstalled Windows service '$ServiceName'."
