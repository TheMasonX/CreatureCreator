<#
.SYNOPSIS
Publishes the MemorySmith.App engine to artifacts/MemorySmith.App.

.DESCRIPTION
Standalone publish helper adapted from
CMST-341\Scripts\Bootstrap-CourseWikiEngine.ps1. Deploy-CreatureWiki.ps1
publishes inline; this script is for manual rebuilds without reinstalling the
service (combined with Deploy-CreatureWiki.ps1 -NoBuild).

.PARAMETER MemorySmithRepoPath
Path to the MemorySmith engine repository. Default: "D:\@Repos\MemorySmith"

.PARAMETER Configuration
dotnet publish configuration. Default: "Release"

.EXAMPLE
.\Scripts\Bootstrap-CreatureWikiEngine.ps1
#>
[CmdletBinding()]
param(
  [string]$MemorySmithRepoPath = "D:\@Repos\MemorySmith",
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot    = Split-Path -Parent $PSScriptRoot
$publishDir  = Join-Path $repoRoot "artifacts/MemorySmith.App"
$appProject  = Join-Path $MemorySmithRepoPath "MemorySmith.App/MemorySmith.App.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw "dotnet SDK is required but was not found on PATH."
}

if (-not (Test-Path $MemorySmithRepoPath)) {
  throw "MemorySmith repo path not found: $MemorySmithRepoPath"
}

if (-not (Test-Path $appProject)) {
  throw "MemorySmith.App project not found at: $appProject"
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

dotnet publish $appProject -c $Configuration -o $publishDir -nologo | Out-Host
if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "Publish complete. Output: $publishDir"
