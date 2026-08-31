<#
taskctl.ps1 - Dispatch the CreatureCreator task tools.

Run from any directory; the script resolves the tools folder from its own
location. PowerShell must be able to find `python` on PATH.

Usage:
    ./taskctl.ps1 search --status "In Progress"
    ./taskctl.ps1 search --include-archive --status Done
    ./taskctl.ps1 validate
    ./taskctl.ps1 new --title "..." --priority P2
    ./taskctl.ps1 archive CC-091 --status Done --reason "Unity tests passed"
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Command,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ToolArgs
)

$toolDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$script = Join-Path $toolDir "task_$($Command.ToLower()).py"

if (-not (Test-Path $script)) {
    Write-Error "Unknown task command '$Command'. Expected one of: search, validate, archive, new."
    exit 1
}

& python $script @ToolArgs
exit $LASTEXITCODE
