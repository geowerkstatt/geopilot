#Requires -Version 7
<#
.SYNOPSIS
    Delivers files to geopilot as a machine client, on an installation that writes its uploads through the API.

.DESCRIPTION
    Runs the machine delivery end to end, the way a client of such an installation has to:

      1. POST {TokenUrl}                          client credentials, answers the access token
      2. POST {ApiUrl}/api/v1/submission/files    one multipart/form-data request: the fields, then the files
      3. GET  {Location of step 2}                polls the attempt until it is delivered, rejected or failed

    The fields have to precede the files: the server checks the mandate and the delivery details before
    it reads the first byte of file content, so a wrong key is refused before a large file is transferred.

    An installation that keeps its uploads in an object storage refuses step 2 with 400 and asks for the
    upload first; use submit-delivery-upload.ps1 there. Which of the two applies is a property of the
    installation, not of the mandate, so a client is set up once with the matching script.

    Exit code 0 means the delivery was made, 2 the pipeline rejected the data, 3 the attempt failed
    before the data could be judged, 4 the status was still processing when the wait ran out, and 1
    that a request was refused; the server's message is in the output.

.PARAMETER MandateKey
    The key of the mandate to deliver to, as set in the administration. Compared exactly, including case.

.PARAMETER File
    One or more files to deliver.

.PARAMETER ClientSecret
    The secret of the machine client at the identity provider. Taken from the environment variable
    GEOPILOT_CLIENT_SECRET when omitted, so it does not have to appear on a command line. In the
    development stack the client is geopilot-api and its secret stands in config/realms/keycloak-geopilot.json.

.PARAMETER ClientId
    The client id at the identity provider. Defaults to the client of the development stack.

.PARAMETER TokenUrl
    The token endpoint of the identity provider. Defaults to the Keycloak of the development stack.

.PARAMETER Scope
    An optional scope for the token request, for an identity provider that puts the audience of the
    API into the token only when asked for it. The Keycloak of the development stack needs none.

.PARAMETER ApiUrl
    The base URL of the geopilot API. Defaults to the API of the development stack.

.PARAMETER Comment
    The comment accompanying the delivery. The mandate decides whether it is forbidden, optional or required.

.PARAMETER PartialDelivery
    Whether the delivery covers only part of the mandate. Left out when not given; the mandate decides
    whether it has to be given.

.PARAMETER PrecursorDeliveryId
    The delivery this one supersedes. Left out when not given; the mandate decides whether it has to be given.

.PARAMETER PollSeconds
    Seconds between two status requests.

.PARAMETER TimeoutMinutes
    How long to poll before giving up. The attempt itself keeps running on the server.

.PARAMETER SkipCertificateCheck
    Accept the self-signed certificate of a development stack.

.EXAMPLE
    $env:GEOPILOT_CLIENT_SECRET = '...'
    ./scripts/submit-delivery-files.ps1 -MandateKey av-2026 -File .\lieferung.xtf -SkipCertificateCheck

.EXAMPLE
    ./scripts/submit-delivery-files.ps1 -ApiUrl https://geopilot.example.ch -TokenUrl https://idp.example.ch/oauth2/token -ClientId my-client -ClientSecret '...' -MandateKey av-2026 -File .\a.xtf, .\b.xtf -Comment 'Nachlieferung'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$MandateKey,
    [Parameter(Mandatory)][string[]]$File,
    [string]$ClientSecret = $env:GEOPILOT_CLIENT_SECRET,
    [string]$ClientId = 'geopilot-api',
    [string]$TokenUrl = 'http://localhost:4011/realms/geopilot/protocol/openid-connect/token',
    [string]$Scope,
    [string]$ApiUrl = 'https://localhost:7443',
    [string]$Comment,
    [nullable[bool]]$PartialDelivery,
    [nullable[int]]$PrecursorDeliveryId,
    [int]$PollSeconds = 5,
    [int]$TimeoutMinutes = 30,
    [switch]$SkipCertificateCheck
)

$ErrorActionPreference = 'Stop'
$PSDefaultParameterValues = @{ 'Invoke-RestMethod:SkipCertificateCheck' = [bool]$SkipCertificateCheck }

if ([string]::IsNullOrWhiteSpace($ClientSecret)) {
    throw 'No client secret: pass -ClientSecret or set the environment variable GEOPILOT_CLIENT_SECRET.'
}

$ApiUrl = $ApiUrl.TrimEnd('/')
$files = @(foreach ($path in $File) { Get-Item -LiteralPath $path })

$script:token = $null
$script:tokenExpiresAt = [datetime]::MinValue

function Get-AuthorizationHeader {
    # Client credentials (RFC 6749, section 4.4). Renewed shortly before it expires, because a long run
    # can outlive a short-lived token while the status is polled.
    if ((Get-Date) -ge $script:tokenExpiresAt) {
        $form = @{ grant_type = 'client_credentials'; client_id = $ClientId; client_secret = $ClientSecret }
        if ($Scope) { $form.scope = $Scope }

        try {
            $response = Invoke-RestMethod -Method Post -Uri $TokenUrl -Body $form
        }
        catch {
            if ($null -eq $_.Exception.Response) { throw }
            throw "The identity provider refused the token request for client <$ClientId> with HTTP $([int]$_.Exception.Response.StatusCode): $($_.ErrorDetails.Message)"
        }

        $lifetime = if ($response.expires_in) { [int]$response.expires_in } else { 300 }
        $script:token = $response.access_token
        $script:tokenExpiresAt = (Get-Date).AddSeconds($lifetime - 30)
    }

    return @{ Authorization = "Bearer $script:token" }
}

function Invoke-Submission([System.Collections.IDictionary]$Form) {
    # 503 means the installation is at its upload capacity. The request is fine and is repeated unchanged
    # after the time Retry-After names, a handful of times.
    for ($attempt = 1; ; $attempt++) {
        try {
            $headers = $null
            $response = Invoke-RestMethod -Method Post -Uri "$ApiUrl/api/v1/submission/files" -Headers (Get-AuthorizationHeader) -Form $Form -ResponseHeadersVariable headers
            $statusUrl = if ($headers.Location) { $headers.Location[0] } else { "$ApiUrl/api/v1/submission/$($response.id)" }
            return [pscustomobject]@{ Response = $response; StatusUrl = $statusUrl }
        }
        catch {
            $httpResponse = $_.Exception.Response
            if ($null -eq $httpResponse) { throw }

            if ($httpResponse.StatusCode -eq [System.Net.HttpStatusCode]::ServiceUnavailable -and $attempt -lt 5) {
                $retryAfter = $httpResponse.Headers.RetryAfter
                $seconds = if ($retryAfter -and $null -ne $retryAfter.Delta) { [int]$retryAfter.Delta.TotalSeconds } else { 30 }
                Write-Host "The installation is at capacity, retrying in $seconds s (attempt $attempt of 5)."
                Start-Sleep -Seconds $seconds
                continue
            }

            throw "The submission was refused with HTTP $([int]$httpResponse.StatusCode): $($_.ErrorDetails.Message)"
        }
    }
}

function Wait-ForSubmission([string]$StatusUrl) {
    # The data is accepted and the pipeline is running by now, so a hiccup on the way to the status must not
    # look like a failed delivery: a network error or a 5xx from a proxy is repeated a handful of times.
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    $failures = 0
    while ($true) {
        try {
            $status = Invoke-RestMethod -Method Get -Uri $StatusUrl -Headers (Get-AuthorizationHeader) -TimeoutSec 60
            $failures = 0
        }
        catch {
            $httpResponse = $_.Exception.Response
            if ($null -ne $httpResponse -and $httpResponse.StatusCode -eq [System.Net.HttpStatusCode]::NotFound) {
                throw "The attempt is gone from $StatusUrl. An attempt lives no longer than its processing job and does not survive a restart of the instance; where it created a delivery, that delivery is the durable record."
            }

            if ($null -ne $httpResponse -and [int]$httpResponse.StatusCode -lt 500) { throw }
            if (++$failures -ge 5) { throw }

            Write-Host "The status could not be read ($($_.Exception.Message)), trying again in $PollSeconds s."
            Start-Sleep -Seconds $PollSeconds
            continue
        }

        if ($status.state -ne 'processing' -or (Get-Date) -ge $deadline) {
            return $status
        }

        Write-Host "Attempt $($status.id) is processing, next status in $PollSeconds s."
        Start-Sleep -Seconds $PollSeconds
    }
}

function Get-Text($localizedText) {
    # A message text carries one property per language code.
    foreach ($language in 'de', 'en', 'fr', 'it') {
        if ($localizedText.$language) { return $localizedText.$language }
    }

    return ($localizedText.PSObject.Properties | Select-Object -First 1).Value
}

function Show-Submission($status) {
    Write-Host ''
    Write-Host "Attempt $($status.id) for mandate <$($status.mandateKey)>: $($status.state)"
    if ($status.deliveryId) { Write-Host "Delivery: $($status.deliveryId)" }
    foreach ($message in $status.messages) {
        Write-Host ("  {0,-7} {1}: {2}" -f $message.severity, $message.step, (Get-Text $message.text))
    }

    foreach ($download in $status.downloads) {
        Write-Host ("  download {0}/{1}: {2}" -f $download.step, $download.name, $download.url)
    }
}

# 2. One request: the fields first, the files last. An ordered dictionary keeps that order in the form.
#    Only the fields that were given go into the request; the mandate decides which of the optional ones
#    are forbidden, allowed or required. The field name of the files does not matter to the server.
$form = [ordered]@{ mandateKey = $MandateKey }
if ($Comment) { $form.comment = $Comment }
if ($null -ne $PartialDelivery) { $form.partialDelivery = $PartialDelivery.ToString().ToLowerInvariant() }
if ($null -ne $PrecursorDeliveryId) { $form.precursorDeliveryId = $PrecursorDeliveryId.ToString() }
$form.file = $files

# 1. The token, fetched once up front, so wrong credentials fail before anything else is attempted.
Write-Host "Requesting a token for client <$ClientId> from $TokenUrl ..."
Get-AuthorizationHeader | Out-Null

Write-Host "Sending $($files.Count) file(s) to $ApiUrl ..."
$submission = Invoke-Submission $form
Write-Host "Accepted as attempt $($submission.Response.id); status at $($submission.StatusUrl)"

# 3. Poll until the attempt has ended.
$status = Wait-ForSubmission $submission.StatusUrl
Show-Submission $status

switch ($status.state) {
    'delivered' { exit 0 }
    'rejected' { exit 2 }
    'failed' { exit 3 }
    default {
        Write-Host "Still processing after $TimeoutMinutes minutes; the attempt keeps running, its status stays at $($submission.StatusUrl)."
        exit 4
    }
}
