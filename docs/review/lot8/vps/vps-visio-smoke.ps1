<#
.SYNOPSIS
    Lot 8.3 - VPS visio-token smoke test against the demo environment.
    Real OTP login (OTP read from VPS container logs via SSH, pasted in by the operator),
    real HTTPS calls to the API and to the real LiveKit server. Tokens are kept in memory
    only - never printed beyond a 12-char preview, never written to any file.
.NOTES
    Windows PowerShell 5.1 compatible. Always uses -UseBasicParsing.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "https://api-demo.mentorapp.fr",
    [string]$LiveKitHttpsUrl = "https://livekit.mentorapp.fr",
    [Parameter(Mandatory = $true)][string]$SessionId,
    [string]$CoachAEmail = "admin@web2success.fr",
    [string]$CoachBEmail = "coach2@mentorapp.fr",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = $PSScriptRoot }
$script:Results = New-Object System.Collections.Generic.List[object]

function Invoke-ApiCall {
    param([string]$Method, [string]$Path, $Body = $null, [string]$Token = $null, [switch]$NoAuthHeader)
    $uri = "$BaseUrl$Path"
    $headers = @{}
    if (-not $NoAuthHeader -and $Token) { $headers["Authorization"] = "Bearer $Token" }
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

function Invoke-RawGet {
    param([string]$Uri)
    try {
        $resp = Invoke-WebRequest -Uri $Uri -Method GET -UseBasicParsing
        return [PSCustomObject]@{ StatusCode = [int]$resp.StatusCode; Content = $resp.Content }
    } catch {
        $webResp = $_.Exception.Response
        if ($webResp) {
            $stream = $webResp.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $rawBody = $reader.ReadToEnd()
            return [PSCustomObject]@{ StatusCode = [int]$webResp.StatusCode; Content = $rawBody }
        }
        throw
    }
}

function Get-TamperedToken {
    param([string]$Token)
    $parts = $Token.Split('.')
    if ($parts.Length -ne 3) { throw "Not a JWT" }
    $sig = $parts[2]
    $firstChar = $sig.Substring(0, 1)
    $newChar = if ($firstChar -eq 'A') { 'B' } else { 'A' }
    $newSig = $newChar + $sig.Substring(1)
    return "$($parts[0]).$($parts[1]).$newSig"
}

function New-VpsSession {
    param([string]$Email)
    Write-Host "Requesting OTP for $Email ..." -ForegroundColor Cyan
    $r1 = Invoke-ApiCall -Method POST -Path "/api/v1/auth/otp/request" -Body @{ email = $Email; isCoach = $true }
    if ($r1.StatusCode -ne 200) { throw "OTP request failed for $Email : $($r1.StatusCode)" }

    $code = Read-Host "Read the OTP for $Email from the VPS container logs (SSH) and paste it here"
    $r2 = Invoke-ApiCall -Method POST -Path "/api/v1/auth/otp/verify" -Body @{ email = $Email; code = $code; isCoach = $true }
    if ($r2.StatusCode -ne 200) { throw "OTP verify failed for $Email : $($r2.StatusCode)" }

    $token = $r2.Content.data.accessToken
    Write-Host "  OK - token (first 12): $($token.Substring(0,12))..." -ForegroundColor Green
    return $token
}

function Add-Result {
    param([string]$Num, [string]$Scenario, $Expected, $Actual, [string]$Details = "")
    $pass = ($Actual -eq $Expected)
    $status = if ($pass) { "PASS" } else { "FAIL" }
    $script:Results.Add([PSCustomObject]@{ Num = $Num; Scenario = $Scenario; Expected = $Expected; Actual = $Actual; Status = $status; Details = $Details })
    Write-Host "[$status] #$Num $Scenario -> expected $Expected, got $Actual" -ForegroundColor $(if ($pass) { "Green" } else { "Red" })
    if ($Details) { Write-Host "         $Details" -ForegroundColor DarkGray }
}

# ---------------------------------------------------------------------------
# 1. Real OTP login for coach A and coach B - tokens kept in memory only
# ---------------------------------------------------------------------------

$coachAToken = New-VpsSession -Email $CoachAEmail
$coachBToken = New-VpsSession -Email $CoachBEmail

# ---------------------------------------------------------------------------
# S1: coach A issues a visio token for its own session -> 200
# ---------------------------------------------------------------------------

$s1 = Invoke-ApiCall -Method POST -Path "/api/v1/coach/sessions/$SessionId/visio-token" -Token $coachAToken
$s1Details = if ($s1.Content -and $s1.Content.data) {
    "serverUrl=$($s1.Content.data.serverUrl) roomName=$($s1.Content.data.roomName) identity=$($s1.Content.data.identity) expiresAt=$($s1.Content.data.expiresAt)"
} else { "(no data)" }
Add-Result -Num "S1" -Scenario "Coach A issues visio token for its own session" -Expected 200 -Actual $s1.StatusCode -Details $s1Details

$s1Token = $null
if ($s1.StatusCode -eq 200 -and $s1.Content -and $s1.Content.data) { $s1Token = $s1.Content.data.token }

# ---------------------------------------------------------------------------
# S2: valid S1 token against real LiveKit /rtc/validate -> 200
# ---------------------------------------------------------------------------

if ($s1Token) {
    $s2 = Invoke-RawGet -Uri "$LiveKitHttpsUrl/rtc/validate?access_token=$s1Token"
    Add-Result -Num "S2" -Scenario "Valid S1 token against real LiveKit /rtc/validate" -Expected 200 -Actual $s2.StatusCode
} else {
    Add-Result -Num "S2" -Scenario "Valid S1 token against real LiveKit /rtc/validate" -Expected 200 -Actual "SKIPPED (no S1 token)"
}

# ---------------------------------------------------------------------------
# S3: tampered S1 token against real LiveKit /rtc/validate -> 401
# ---------------------------------------------------------------------------

if ($s1Token) {
    $tampered = Get-TamperedToken -Token $s1Token
    $s3 = Invoke-RawGet -Uri "$LiveKitHttpsUrl/rtc/validate?access_token=$tampered"
    Add-Result -Num "S3" -Scenario "Tampered S1 token against real LiveKit /rtc/validate" -Expected 401 -Actual $s3.StatusCode
} else {
    Add-Result -Num "S3" -Scenario "Tampered S1 token against real LiveKit /rtc/validate" -Expected 401 -Actual "SKIPPED (no S1 token)"
}

# ---------------------------------------------------------------------------
# S4: coach B requests a token for coach A's session -> 404 (scope mismatch)
# ---------------------------------------------------------------------------

$s4 = Invoke-ApiCall -Method POST -Path "/api/v1/coach/sessions/$SessionId/visio-token" -Token $coachBToken
Add-Result -Num "S4" -Scenario "Coach B requests a token for coach A's session (scope mismatch)" -Expected 404 -Actual $s4.StatusCode

# ---------------------------------------------------------------------------
# S5: no Authorization header -> 401
# ---------------------------------------------------------------------------

$s5 = Invoke-ApiCall -Method POST -Path "/api/v1/coach/sessions/$SessionId/visio-token" -NoAuthHeader
Add-Result -Num "S5" -Scenario "No Authorization header" -Expected 401 -Actual $s5.StatusCode

# ---------------------------------------------------------------------------
# Report - never contains a token
# ---------------------------------------------------------------------------

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $OutDir "vps-smoke-$timestamp.md"

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Lot 8.3 VPS visio-token smoke test - $timestamp")
[void]$sb.AppendLine()
[void]$sb.AppendLine("Base URL: $BaseUrl | LiveKit: $LiveKitHttpsUrl | SessionId: $SessionId")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| # | Scenario | Expected | Actual | Status | Details |")
[void]$sb.AppendLine("|---|---|---|---|---|---|")
foreach ($row in $script:Results) {
    [void]$sb.AppendLine("| $($row.Num) | $($row.Scenario) | $($row.Expected) | $($row.Actual) | $($row.Status) | $($row.Details) |")
}

Set-Content -Path $reportPath -Value $sb.ToString() -Encoding utf8
Write-Host ""
Write-Host "Report written to $reportPath (no token written)" -ForegroundColor Cyan

$failCount = @($script:Results | Where-Object { $_.Status -eq "FAIL" }).Count
if ($failCount -gt 0) {
    Write-Host "$failCount scenario(s) FAILED." -ForegroundColor Red
    exit 1
} else {
    Write-Host "All scenarios PASSED." -ForegroundColor Green
    exit 0
}
