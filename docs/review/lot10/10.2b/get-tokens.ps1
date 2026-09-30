<#
.SYNOPSIS
    Lot 10.2b - obtains real access tokens for coach A (coach@mentora.fr) and the member
    (member@mentora.fr) through the real OTP flow (the OTP is read from the API log),
    saves them to a JSON file that is never committed and never printed in full.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5243",
    [string]$CoachAEmail = "coach@mentora.fr",
    [string]$MemberEmail = "member@mentora.fr",
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
    $needle = "[" + "OTP" + "] $Email"  # split so this file does not itself contain the log marker
    # The API holds the log file open: read it with shared access.
    $fs = [System.IO.File]::Open($LogFile, 'Open', 'Read', 'ReadWrite')
    try {
        $reader = New-Object System.IO.StreamReader($fs)
        $text = $reader.ReadToEnd()
    } finally { $fs.Dispose() }
    return @($text -split "`r?`n" | Where-Object { $_.Contains($needle) })
}

function New-Session {
    param([string]$Email, [bool]$IsCoach)
    $prior = @(Get-OtpLines -Email $Email).Count
    $r1 = Invoke-ApiCall -Method POST -Path "/api/v1/auth/otp/request" -Body @{ email = $Email; isCoach = $IsCoach }
    Write-Host "otp/request $Email -> $($r1.StatusCode)"
    $code = $null
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 250
        $lines = @(Get-OtpLines -Email $Email)
        if ($lines.Count -gt $prior -and $lines[-1] -match '(\d{6})\s*$') { $code = $Matches[1]; break }
    }
    if (-not $code) { throw "No new OTP log line found for '$Email' in $LogFile" }
    $r2 = Invoke-ApiCall -Method POST -Path "/api/v1/auth/otp/verify" -Body @{ email = $Email; code = $code; isCoach = $IsCoach }
    Write-Host "otp/verify  $Email -> $($r2.StatusCode)"
    return $r2.Content.data
}

$coach  = New-Session -Email $CoachAEmail -IsCoach $true
$member = New-Session -Email $MemberEmail -IsCoach $false

[PSCustomObject]@{
    coachAAccessToken = $coach.accessToken
    memberAccessToken = $member.accessToken
} | ConvertTo-Json | Set-Content -Path $OutFile -Encoding utf8

Write-Host "Coach A token (first 12): $($coach.accessToken.Substring(0,12))..."
Write-Host "Member token  (first 12): $($member.accessToken.Substring(0,12))..."
Write-Host "Full tokens written to $OutFile"
