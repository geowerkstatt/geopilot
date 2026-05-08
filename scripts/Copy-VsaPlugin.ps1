<#
.SYNOPSIS
    Copies the locally built VSA-Checker-Analytics plugin into geopilot.
.PARAMETER RepoPath
    Path to the vsa-checker-analytics repository. Defaults to a sibling directory.
#>
param(
    [string]$RepoPath = (Join-Path $PSScriptRoot "..\..\vsa-checker-analytics"),
    [string]$Configuration = "Debug"
)

$sourcePath = Join-Path $RepoPath "src\VSACheckerAnalytics\bin\$Configuration\net10.0"

& "$PSScriptRoot\Copy-Plugin.ps1" -SourcePath $sourcePath -PluginName "VsaCheckerAnalytics"
