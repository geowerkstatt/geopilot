#Requires -Version 5.1
<#
.SYNOPSIS
    Links a plugin's dev appsettings into geopilot for local development.

.DESCRIPTION
    Creates a git-ignored symbolic link under src/Geopilot.Api that points at a plugin's
    appsettings.<Profile>.json. geopilot loads every appsettings.Local*.json as a config overlay,
    so the plugin's settings take effect without copying anything by hand, and several plugins can
    be linked at once under different names. The plugin repo stays the single source of truth.

    A symlink resolves by path on every access, so it survives editor saves and git operations
    (pull, checkout, branch switch) that replace the target file.

    Requirement on Windows: creating a symlink needs Administrator rights or enabled Developer Mode
    (Settings > For developers > Developer Mode). Without it, mklink fails with a privilege error.

.PARAMETER Plugin
    Folder name of the plugin repository next to geopilot (for example: my-plugin).

.PARAMETER Profile
    Appsettings profile to link, i.e. appsettings.<Profile>.json in the plugin (for example: Development).

.PARAMETER Name
    Optional overlay name. Given "name" the link becomes appsettings.Local.name.json; omitted it is
    plain appsettings.Local.json. geopilot loads any appsettings.Local*.json, so no Program.cs change
    is needed per name.

.EXAMPLE
    ./scripts/link-plugin-config.ps1 my-plugin Development myplugin
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Plugin,
    [Parameter(Mandatory, Position = 1)][string]$Profile,
    [Parameter(Position = 2)][string]$Name
)

$ErrorActionPreference = 'Stop'

# geopilot repository root (this script lives inside it)
$geopilotRoot = (Resolve-Path (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()).Path

# Local overlay file name: appsettings.Local.json, or appsettings.Local.<Name>.json when a name is given
$linkFileName = if ([string]::IsNullOrWhiteSpace($Name)) { 'appsettings.Local.json' } else { "appsettings.Local.$Name.json" }
$linkPath = Join-Path $geopilotRoot "src\Geopilot.Api\$linkFileName"

# The plugin repository is expected next to geopilot
$pluginRoot = Join-Path (Split-Path -Parent $geopilotRoot) $Plugin
if (-not (Test-Path $pluginRoot)) { throw "Plugin repository not found: $pluginRoot" }

# Find the requested profile config in the plugin, ignoring build output
$target = Get-ChildItem -Path $pluginRoot -Recurse -File -Filter "appsettings.$Profile.json" |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $target) { throw "No appsettings.$Profile.json found under $pluginRoot" }

# (Re)create the symbolic link
if (Test-Path $linkPath) { Remove-Item -Force $linkPath }
& cmd.exe /c "mklink `"$linkPath`" `"$target`"" | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "mklink failed (exit code $LASTEXITCODE). Symlinks need Administrator rights or enabled Developer Mode (Settings > For developers > Developer Mode)."
}
Write-Host "Linked $linkPath -> $target"

# Make sure the link is git-ignored. If nothing covers it yet, add the shared glob so every
# appsettings.Local*.json is ignored with a single entry (idempotent across runs and names).
git -C $geopilotRoot check-ignore -q -- $linkPath
if ($LASTEXITCODE -ne 0) {
    Add-Content -Path (Join-Path $geopilotRoot '.gitignore') -Value "`r`n# Local developer overlays (symlinks created by scripts/link-plugin-config.ps1)`r`n**/appsettings.Local*.json"
    Write-Host "Added **/appsettings.Local*.json to .gitignore"
}
else {
    Write-Host "Already git-ignored"
}
