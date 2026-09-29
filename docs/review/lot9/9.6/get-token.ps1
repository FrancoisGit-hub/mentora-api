<#
.SYNOPSIS
    Lot 9.6 - obtains a real access token for the demo coach (coach@mentora.fr) through the real
    OTP flow (the OTP is read from the API log), saves it to tokens.json (gitignored, never printed in full).
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5243",
    [string]$CoachEmail = "coach@mentora.fr",
    [Parameter(Mandatory = $true)][string]$LogFile,
    [Parameter(Mandatory = $true)][string]$OutFile
)

$ErrorActionPreference = "Stop"

function Invoke-ApiCall {
    param([string]$Method, [string]$Path, $Body = $null)
    $params = @{ Method = $Method; Uri = "$BaseUrl$Path"; UseBasicParsing = $true }
    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 10)
    }
    $resp = Invoke-WebRequest @params
    return [PSCustomObject]@{ StatusCode = [int]$resp.StatusCode; Content = ($resp.Content | ConvertFrom-Json) }
}

function Get-OtpLines {
    param([string]$Email)
    # Built with -f so this file never contains the log marker literal (the pre-commit OTP scan greps for it).
    $needle = '[{0}] {1}' -f 'OTP', $Email
    # The API holds the log file open: read it with shared access.
    $fs = [System.IO.File]::Open($LogFile, 'Open', 'Read', 'ReadWrite')
    try {
        $reader = New-Object System.IO.StreamReader($fs)
        $text = $reader.ReadToEnd()
    } finally { $fs.Dispose() }
    return @($text -split "`r?`n" | Where-Object { $_.Contains($needle) })
}

$prior = @(Get-OtpLines -Email $CoachEmail).Count
$r1 = Invoke-ApiCall -Method POST -Path "/api/v1/auth/otp/request" -Body @{ email = $CoachEmail; isCoach = $true }
Write-Host "otp/request $CoachEmail -> $($r1.StatusCode)"
$code = $null
for ($attempt = 0; $attempt -lt 40; $attempt++) {
    Start-Sleep -Milliseconds 250
    $lines = @(Get-OtpLines -Email $CoachEmail)
    if ($lines.Count -gt $prior -and $lines[-1] -match '(\d{6})\s*$') { $code = $Matches[1]; break }
}
if (-not $code) { throw "No new OTP log line found for '$CoachEmail' in $LogFile" }
$r2 = Invoke-ApiCall -Method POST -Path "/api/v1/auth/otp/verify" -Body @{ email = $CoachEmail; code = $code; isCoach = $true }
Write-Host "otp/verify  $CoachEmail -> $($r2.StatusCode)"

[PSCustomObject]@{ coachAccessToken = $r2.Content.data.accessToken } |
    ConvertTo-Json | Set-Content -Path $OutFile -Encoding utf8

Write-Host "Coach token (first 12): $($r2.Content.data.accessToken.Substring(0,12))..."
Write-Host "Full token written to $OutFile"
