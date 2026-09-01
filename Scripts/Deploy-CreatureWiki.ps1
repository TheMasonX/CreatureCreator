<#
.SYNOPSIS
Builds, deploys, and starts a MemorySmith wiki service for the CreatureCreator
codebase.

.DESCRIPTION
Publishes MemorySmith.App from the MemorySmith engine repo and registers a
Windows service that serves this repo's Data/ as a live wiki: task records,
memories, pages, and architecture docs.

This is adapted from MemorySmith.Agent\Scripts\Deploy-CodebaseWiki.ps1.

.PARAMETER MemorySmithRepoPath
Path to the MemorySmith engine repository (where MemorySmith.App.csproj
lives). Default: "D:\@Repos\MemorySmith"

.PARAMETER PreferredPort
Primary HTTP port for the wiki service. Default: 7916

.PARAMETER FallbackPort
Fallback HTTP port if the primary is in use. Default: 4279

.PARAMETER Configuration
dotnet build/publish configuration (Release or Debug). Default: "Release"

.PARAMETER ServiceName
Windows Service name for the CreatureCreator wiki.
Default: "MemorySmith - CreatureCreator Wiki"

.PARAMETER ServiceDisplayName
Display name shown in Windows Service Manager.
Default: "MemorySmith - CreatureCreator Wiki"

.PARAMETER NoBuild
Skip the dotnet publish step. Uses the existing publish output from
artifacts/MemorySmith.App. Useful when only reinstalling or restarting.

.EXAMPLE
# Full deploy with defaults
.\Scripts\Deploy-CreatureWiki.ps1

.EXAMPLE
# Custom ports
.\Scripts\Deploy-CreatureWiki.ps1 -PreferredPort 5050 -FallbackPort 6060

.EXAMPLE
# Refresh service without rebuilding
.\Scripts\Deploy-CreatureWiki.ps1 -NoBuild
#>
[CmdletBinding()]
param(
  [string]$MemorySmithRepoPath = "D:\@Repos\MemorySmith",
  [int]$PreferredPort = 7916,
  [int]$FallbackPort = 4279,
  [string]$Configuration = "Release",
  [string]$ServiceName = "MemorySmith - CreatureCreator Wiki",
  [string]$ServiceDisplayName = "MemorySmith - CreatureCreator Wiki",
  [string]$InstanceName = "CreatureCreator Wiki",
  [string]$ShortLabel = "Creature Wiki",
  [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Resolve paths ──────────────────────────────────────────────────────────
$repoRoot      = Split-Path -Path $PSScriptRoot -Parent
$serviceDir    = Join-Path $repoRoot ".service"
$publishDir    = Join-Path $repoRoot "artifacts/MemorySmith.App"
$publishExe    = Join-Path $publishDir "MemorySmith.App.exe"
$publishDll    = Join-Path $publishDir "MemorySmith.App.dll"

$appProject    = Join-Path $MemorySmithRepoPath "MemorySmith.App/MemorySmith.App.csproj"
$sourceData    = Join-Path $repoRoot "Data"
$memoryDir     = Join-Path $sourceData "Memories"
$pagesPath     = Join-Path $sourceData "Pages"
$varsPath      = Join-Path $sourceData "vars.json"
$eventLogPath  = Join-Path $sourceData "Events/audit.log"
$keysPath      = Join-Path $sourceData "Keys"
$modelsPath    = Join-Path $sourceData "Models"

$logFile       = Join-Path $serviceDir "creature-wiki.log"
$errFile       = Join-Path $serviceDir "creature-wiki.err.log"
$portFile      = Join-Path $serviceDir "creature-wiki.port"

# ── Prerequisites ──────────────────────────────────────────────────────────
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw "dotnet SDK is required but was not found on PATH."
}

if (-not (Test-Path $MemorySmithRepoPath)) {
  throw "MemorySmith engine repo not found: $MemorySmithRepoPath"
}

if (-not (Test-Path $sourceData)) {
  throw "Data directory not found at: $sourceData"
}

# ── Helper functions ───────────────────────────────────────────────────────
function Test-PortAvailable([int]$Port) {
  return -not (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
}

function Test-IsAdministrator {
  $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = [Security.Principal.WindowsPrincipal]::new($identity)
  return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Stop-CreatureService {
  $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
  if ($null -ne $svc -and $svc.Status -ne 'Stopped') {
    Write-Host "  Stopping Windows service '$ServiceName'..."
    Stop-Service -Name $ServiceName -Force
    $svc.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(30))
    Write-Host "  Service stopped."
  }
  elseif ($null -ne $svc) {
    Write-Host "  Service '$ServiceName' is already stopped."
  }
  else {
    Write-Host "  Service '$ServiceName' is not installed."
  }
}

function Unregister-CreatureService {
  if (-not (Test-Path $appArtifact)) {
    Write-Host "  Publish artifact not found at $appArtifact — skipping unregister."
    return
  }

  $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
  if ($null -ne $svc) {
    Write-Host "  Unregistering Windows service '$ServiceName'..."
    & dotnet $appArtifact uninstall --service-name $ServiceName | Out-Host
    if ($LASTEXITCODE -ne 0) {
      Write-Warning "  Uninstall returned exit code $LASTEXITCODE. Continuing..."
    }
  }
}

# ── Admin check ────────────────────────────────────────────────────────────
if (-not (Test-IsAdministrator)) {
  throw "This script must be run from an elevated PowerShell session (Run as Administrator)."
}

# ── Header ─────────────────────────────────────────────────────────────────
Write-Host "── Deploy-CreatureWiki ──────────────────────────────────"
Write-Host "MemorySmith engine repo : $MemorySmithRepoPath"
Write-Host "CreatureCreator (data)  : $repoRoot"
Write-Host "Data directory          : $sourceData"
Write-Host "Service name            : $ServiceName"
Write-Host ""

# ── Stop any existing service ──────────────────────────────────────────────
Stop-CreatureService

# ── Publish MemorySmith.App ────────────────────────────────────────────────
if (-not $NoBuild) {
  if (-not (Test-Path $appProject)) {
    throw "MemorySmith.App project not found at: $appProject"
  }

  Write-Host "Building MemorySmith.App ($Configuration)..."
  New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

  & dotnet publish $appProject -c $Configuration -o $publishDir -nologo | Out-Host
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
  }
  Write-Host "  Published to: $publishDir"
}
else {
  if (-not (Test-Path $publishExe) -and -not (Test-Path $publishDll)) {
    throw "No publish output found at $publishDir. Run without -NoBuild first."
  }
  Write-Host "Skipping build (-NoBuild). Using existing publish: $publishDir"
}

# Use the .dll with "dotnet" — the .exe is a native host that dotnet CLI cannot load as a managed assembly.
$appArtifact = if (Test-Path $publishDll) { $publishDll } else { $publishExe }
Write-Host "  App artifact: $appArtifact"

# ── Prepare data directories ──────────────────────────────────────────────
New-Item -ItemType Directory -Path $serviceDir -Force | Out-Null
New-Item -ItemType Directory -Path $keysPath -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path $eventLogPath -Parent) -Force | Out-Null
New-Item -ItemType Directory -Path $modelsPath -Force | Out-Null

if (-not (Test-Path $varsPath)) {
  Set-Content -Path $varsPath -Value "{}" -Encoding utf8
  Write-Host "  Created default vars.json at $varsPath"
}
else {
  Write-Host "  vars.json: $((Get-Item $varsPath).Length) bytes"
}

# ── Select port ────────────────────────────────────────────────────────────
$chosenPort = if (Test-PortAvailable -Port $PreferredPort) {
  $PreferredPort
}
elseif (Test-PortAvailable -Port $FallbackPort) {
  Write-Warning "Preferred port $PreferredPort is in use. Falling back to port $FallbackPort."
  $FallbackPort
}
else {
  throw "Neither preferred port $PreferredPort nor fallback port $FallbackPort is available."
}

# ── Unregister stale service ──────────────────────────────────────────────
Unregister-CreatureService

# ── Install service ────────────────────────────────────────────────────────
Write-Host "Installing Windows service '$ServiceName' on port $chosenPort..."

& dotnet $appArtifact install `
  --service-name $ServiceName `
  --service-display-name $ServiceDisplayName `
  --service-description "MemorySmith wiki service for CreatureCreator — internal project documentation and task tracking" `
  --service-start-type auto `
  --memory-directory $memoryDir `
  --port $chosenPort `
  -- `
  --MemorySmith:InstanceName $InstanceName `
  --MemorySmith:Branding:ShortLabel $ShortLabel `
  --MemorySmith:DataProtectionKeysPath $keysPath `
  --MemorySmith:AllowedFileRoots:0 $repoRoot `
  --MemorySmith:AllowedFileRoots:1 $pagesPath `
  --MemorySmith:AllowedFileRoots:2 (Join-Path $pagesPath "Sources") `
  --MemorySmith:SourceLinks:AllowedFileRoots:0 $repoRoot `
  --MemorySmith:SourceLinks:AllowedFileRoots:1 $pagesPath `
  --MemorySmith:SourceLinks:AllowedFileRoots:2 (Join-Path $pagesPath "Sources") | Out-Host

if ($LASTEXITCODE -ne 0) {
  throw "Service installation failed with exit code $LASTEXITCODE."
}

# ── Start service ──────────────────────────────────────────────────────────
Write-Host "Starting service '$ServiceName'..."
Start-Service -Name $ServiceName
$svc = Get-Service -Name $ServiceName
$svc.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(30))
Write-Host "  Service started."

# Confirm the application is accepting requests before reporting deployment success.
$readyUri = "http://127.0.0.1:$chosenPort/api/health/ready"
$ready = $false
for ($attempt = 1; $attempt -le 30; $attempt++) {
  try {
    $response = Invoke-WebRequest -Uri $readyUri -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
    if ($response.StatusCode -eq 200 -and $response.Content -match '"status"\s*:\s*"Ready"') {
      $ready = $true
      break
    }
  }
  catch {
    # The service can be running before Kestrel and migrations are ready.
  }
}

if (-not $ready) {
  throw "Service started but readiness check failed: $readyUri"
}
Write-Host "  Readiness check passed: $readyUri"

# ── Write port file ────────────────────────────────────────────────────────
Set-Content -Path $portFile -Value $chosenPort

# ── Summary ────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "── Deployment Complete ───────────────────────────────────"
Write-Host "ServiceName    : $ServiceName"
Write-Host "URL            : http://127.0.0.1:$chosenPort"
Write-Host "Data directory : $sourceData"
Write-Host "  Memories     : $memoryDir"
Write-Host "  Pages        : $pagesPath"
Write-Host "Publish Dir    : $publishDir"
Write-Host "Out Log        : $logFile"
Write-Host "Err Log        : $errFile"
Write-Host ""
Write-Host "Management:"
Write-Host "  Stop    : .\Scripts\Stop-CreatureWikiService.ps1"
Write-Host "  Status  : .\Scripts\Get-CreatureWikiServiceStatus.ps1"
Write-Host "  Uninstall: .\Scripts\Uninstall-CreatureWikiService.ps1"
Write-Host ""
Write-Host "The task/memory import scripts should point to: http://127.0.0.1:$chosenPort"
Write-Host ""
