<#
.SYNOPSIS
    Downloads a plugin from a GitHub release into the geopilot Plugins directory.
.PARAMETER ReleaseUrl
    GitHub releases URL, e.g. "https://github.com/owner/repo/releases/"
.PARAMETER PluginName
    Name of the subdirectory under Plugins/ where the plugin will be placed.
.PARAMETER Tag
    Specific release tag to download. Defaults to the latest release.
.PARAMETER Token
    GitHub personal access token for private repos. Falls back to GITHUB_TOKEN env var.
.PARAMETER AssetPattern
    Wildcard pattern to match the ZIP asset name. Defaults to "*.zip".
.EXAMPLE
    .\Download-Plugin.ps1 -ReleaseUrl "https://github.com/geowerkstatt/vsa-checker-analytics/releases/" -PluginName "VsaCheckerAnalytics"
.EXAMPLE
    .\Download-Plugin.ps1 -ReleaseUrl "https://github.com/geowerkstatt/vsa-checker-analytics/releases/" -PluginName "VsaCheckerAnalytics" -Token "ghp_..."
#>
param(
    [Parameter(Mandatory)]
    [string]$ReleaseUrl,

    [Parameter(Mandatory)]
    [string]$PluginName,

    [string]$Tag,

    [string]$Token,

    [string]$AssetPattern = "*.zip"
)

$ErrorActionPreference = "Stop"

$uri = [System.Uri]$ReleaseUrl
$segments = $uri.AbsolutePath.Trim('/').Split('/')
if ($segments.Length -lt 2) {
    Write-Error "Cannot parse owner/repo from URL: $ReleaseUrl"
    exit 1
}
$owner = $segments[0]
$repo = $segments[1]

if (-not $Token) {
    $Token = $env:GITHUB_TOKEN
}

$headers = @{ "Accept" = "application/vnd.github+json"; "User-Agent" = "geopilot-plugin-downloader" }
if ($Token) {
    $headers["Authorization"] = "Bearer $Token"
}

if ($Tag) {
    $apiUrl = "https://api.github.com/repos/$owner/$repo/releases/tags/$Tag"
} else {
    $apiUrl = "https://api.github.com/repos/$owner/$repo/releases/latest"
}

Write-Host "Fetching release from $apiUrl ..."
$release = Invoke-RestMethod -Uri $apiUrl -Headers $headers

$asset = $release.assets | Where-Object { $_.name -like $AssetPattern } | Select-Object -First 1
if (-not $asset) {
    Write-Error "No asset matching '$AssetPattern' found in release '$($release.tag_name)'. Available: $($release.assets.name -join ', ')"
    exit 1
}

$targetDir = Join-Path $PSScriptRoot "..\tests\Geopilot.Api.Test\Plugins\$PluginName"
$tempZip = Join-Path ([System.IO.Path]::GetTempPath()) $asset.name

try {
    Write-Host "Downloading $($asset.name) ($([math]::Round($asset.size / 1KB)) KB) ..."
    $downloadHeaders = @{ "Accept" = "application/octet-stream"; "User-Agent" = "geopilot-plugin-downloader" }
    if ($Token) {
        $downloadHeaders["Authorization"] = "Bearer $Token"
    }
    Invoke-WebRequest -Uri $asset.url -OutFile $tempZip -Headers $downloadHeaders

    if (Test-Path $targetDir) {
        Remove-Item $targetDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

    Expand-Archive $tempZip -DestinationPath $targetDir -Force

    $fileCount = (Get-ChildItem $targetDir -File -Recurse).Count
    Write-Host "Extracted $fileCount file(s) to '$targetDir' (release $($release.tag_name))"
} finally {
    if (Test-Path $tempZip) {
        Remove-Item $tempZip -Force
    }
}
