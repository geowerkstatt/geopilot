<#
.SYNOPSIS
    Copies a locally built plugin into the geopilot Plugins directory.
.PARAMETER SourcePath
    Path to the directory containing the built plugin DLLs.
.PARAMETER PluginName
    Name of the subdirectory under Plugins/ where the plugin will be placed.
.EXAMPLE
    .\Copy-Plugin.ps1 -SourcePath "C:\Projects\my-plugin\bin\Debug\net10.0" -PluginName "MyPlugin"
#>
param(
    [Parameter(Mandatory)]
    [string]$SourcePath,

    [Parameter(Mandatory)]
    [string]$PluginName
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SourcePath)) {W
    Write-Error "Source path not found: $SourcePath"
    exit 1
}

$targetDir = Join-Path $PSScriptRoot "..\tests\Geopilot.Api.Test\Plugins\$PluginName"

if (Test-Path $targetDir) {
    Remove-Item $targetDir -Recurse -Force
}

New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
Copy-Item (Join-Path $SourcePath "*") $targetDir -Recurse

$fileCount = (Get-ChildItem $targetDir -File).Count
Write-Host "Copied $fileCount file(s) from '$SourcePath' to '$targetDir'"
