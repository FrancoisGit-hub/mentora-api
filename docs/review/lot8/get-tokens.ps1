<#
.SYNOPSIS
    Lot 8.2 - obtains real access tokens for coach A and member M1 via the real OTP
    flow (same pattern as docs/review/lot7/run-auth-tests.ps1), and saves them to
    tokens.json (full tokens - never printed to the console beyond a 12-char preview).
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5243",
    [string]$CoachEmail = "coach@mentora.fr",
    [string]$MemberEmail = "member@mentora.fr",
    [Parameter(Mandatory = $true)]
    [string]$LogFile,
    [string]$OutFile = (Join-Path $PSScriptRoot "tokens.json")
)

$ErrorActionPreference = "Stop"

function Invoke-ApiCall {
    param([string]$Method, [string]$Path, $Body = $null, [string]$Token = $null)
    $uri = "$BaseUrl$Path"
    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    $params = @{ Method = $Method; Uri = $uri; UseBasicParsing = $true; Headers = $headers }
    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 10)
    }
    try {
        $resp = Invoke-WebRequest @params
        $content = $null
        if ($resp.Content) { try { $content = $resp.Content | ConvertFrom-Json } catch { $content = $resp.Content } }
        return [PSCustomObject]@{ StatusCode = [int]$resp.StatusCode; Content = $content }
    } catch {
        $webResp = $_.Exception.Response
        if ($webResp) {
            $stream = $webResp.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $rawBody = $reader.ReadToEnd()
            $content = $null
            try { $content = $rawBody | ConvertFrom-Json } catch { $content = $rawBody }
            return [PSCustomObject]@{ StatusCode = [int]$webResp.StatusCode; Content = $content }
        }
        throw
    }
}

function Get-OtpLineCount {
    param([string]$Email)
    if (-not (Test-Path $LogFile)) { return 0 }
    $needle = "[OTP] $Email"
    @(Get-Content -Path $LogFile | Where-Object { $_.Contains($needle) }).Count
}

function Get-LatestOtp {
    param([string]$Email, [int]$MinLineCount)
    if (-not (Test-Path $LogFile)) { return $null }
    $needle = "[OTP] $Email"
    $lines = @(Get-Content -Path $LogFile | Where-Object { $_.Contains($needle) })
    if ($lines.Count -lt $MinLineCount) { return $null }
    $lastLine = $lines[-1]
    if ($lastLine -match '(\d{6})\s*$') { return $Matches[1] }
    return $null
}

function New-Session {
    param([string]$Email, [Nullable[bool]]$IsCoach)
    $priorCount = Get-OtpLineCount -Email $Email
    $body = @{ email = $Email }
    if ($null -ne $IsCoach) { $body.isCoach = $IsCoach }
    $r1 = Invoke-ApiCall -Method POST -Path "/api/v1/auth/otp/request" -Body $body
    if ($r1.StatusCode -ne 200) { throw "OTP request failed for $Email : $($r1.StatusCode)" }
    $code = $null
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Milliseconds 200
        $code = Get-LatestOtp -Email $Email -MinLineCount ($priorCount + 1)
        if ($code) { break }
    }
    if (-not $code) { throw "No new OTP log line found for '$Email' in $LogFile" }
    $body2 = @{ email = $Email; code = $code }
    if ($null -ne $IsCoach) { $body2.isCoach = $IsCoach }
    $r2 = Invoke-ApiCall -Method POST -Path "/api/v1/auth/otp/verify" -Body $body2
    if ($r2.StatusCode -ne 200) { throw "OTP verify failed for $Email : $($r2.StatusCode)" }
    return $r2.Content.data
}

$coachSession  = New-Session -Email $CoachEmail -IsCoach $true
$memberSession = New-Session -Email $MemberEmail -IsCoach $false

$out = [PSCustomObject]@{
    coachAccessToken  = $coachSession.accessToken
    memberAccessToken = $memberSession.accessToken
}
$out | ConvertTo-Json | Set-Content -Path $OutFile -Encoding utf8

Write-Host "Coach token  (first 12): $($coachSession.accessToken.Substring(0,12))..."
Write-Host "Member token (first 12): $($memberSession.accessToken.Substring(0,12))..."
Write-Host "Full tokens written to $OutFile"
