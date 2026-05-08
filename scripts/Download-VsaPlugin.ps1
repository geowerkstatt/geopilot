<#
.SYNOPSIS
    Downloads the latest VSA-Checker-Analytics plugin release into geopilot.
.PARAMETER Tag
    Specific release tag to download. Defaults to the latest release.
.PARAMETER Token
    GitHub personal access token. Falls back to GITHUB_TOKEN env var.
#>
param(
    [string]$Tag,
    [string]$Token
)

$params = @{
    ReleaseUrl = "https://github.com/geowerkstatt/vsa-checker-analytics/releases/"
    PluginName = "VsaCheckerAnalytics"
}
if ($Tag) {
    $params["Tag"] = $Tag
}
if ($Token) {
    $params["Token"] = $Token
}

& "$PSScriptRoot\Download-Plugin.ps1" @params
