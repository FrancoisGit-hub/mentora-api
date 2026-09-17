<#
.SYNOPSIS
    Lot 8.2 - visio-token endpoint HTTP matrix against real fixtures and real JWTs.
.NOTES
    Windows PowerShell 5.1 compatible. Always uses -UseBasicParsing.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5243",
    [Parameter(Mandatory = $true)][string]$CoachToken,
    [Parameter(Mandatory = $true)][string]$MemberToken,
    [Parameter(Mandatory = $true)][string]$F1,
    [Parameter(Mandatory = $true)][string]$F2,
    [Parameter(Mandatory = $true)][string]$F3,
    [Parameter(Mandatory = $true)][string]$F4,
    [Parameter(Mandatory = $true)][string]$F5,
    [Parameter(Mandatory = $true)][string]$F6,
    [Parameter(Mandatory = $true)][string]$F7,
    [string]$F8 = "",
    [string]$OutDir = (Join-Path $PSScriptRoot ".")
)

$ErrorActionPreference = "Stop"
$script:Results = New-Object System.Collections.Generic.List[object]

function Invoke-VisioCall {
    param([string]$Path, [string]$Token = $null, [switch]$NoAuthHeader)
    $uri = "$BaseUrl$Path"
    $headers = @{}
    if (-not $NoAuthHeader -and $Token) { $headers["Authorization"] = "Bearer $Token" }
    $params = @{ Method = "POST"; Uri = $uri; UseBasicParsing = $true; Headers = $headers; ContentType = "application/json"; Body = "" }
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

function Add-Result {
    param([string]$Num, [string]$Scenario, [string]$Path, $Expected, $Actual, [string]$Details = "", [string]$Notes = "")
    $pass = ($Actual -eq $Expected)
    $status = if ($Notes -eq "SKIPPED") { "SKIP" } elseif ($pass) { "PASS" } else { "FAIL" }
    $script:Results.Add([PSCustomObject]@{
        Num = $Num; Scenario = $Scenario; Path = $Path; Expected = $Expected; Actual = $Actual
        Status = $status; Details = $Details; Notes = $Notes
    })
    $color = switch ($status) { "PASS" { "Green" }; "SKIP" { "Yellow" }; default { "Red" } }
    Write-Host "[$status] #$Num $Scenario -> expected $Expected, got $Actual" -ForegroundColor $color
    if ($Details) { Write-Host "         $Details" -ForegroundColor DarkGray }
}

function Format-ConflictDetails {
    param($Content)
    if ($null -eq $Content -or $null -eq $Content.details) { return "(no details)" }
    $d = $Content.details
    return "reason=$($d.reason) opensAt=$($d.opensAt) closesAt=$($d.closesAt)"
}

function Format-SuccessDetails {
    param($Content)
    if ($null -eq $Content -or $null -eq $Content.data) { return "(no data)" }
    $d = $Content.data
    return "serverUrl=$($d.serverUrl) roomName=$($d.roomName) identity=$($d.identity) expiresAt=$($d.expiresAt)"
}

# ---------------------------------------------------------------------------
# Coach endpoint
# ---------------------------------------------------------------------------

$randomGuid = [guid]::NewGuid().ToString()

$r = Invoke-VisioCall -Path "/api/v1/coach/sessions/$F1/visio-token" -Token $CoachToken
Add-Result -Num "C1" -Scenario "Coach, F1 (VISIO, in window)" -Path "coach/$F1" -Expected 200 -Actual $r.StatusCode -Details (Format-SuccessDetails $r.Content)

$r = Invoke-VisioCall -Path "/api/v1/coach/sessions/$randomGuid/visio-token" -Token $CoachToken
Add-Result -Num "C2" -Scenario "Coach, random GUID (unknown session)" -Path "coach/$randomGuid" -Expected 404 -Actual $r.StatusCode

if ([string]::IsNullOrWhiteSpace($F8)) {
    Add-Result -Num "C3" -Scenario "Coach, F8 (session owned by coach B)" -Path "coach/(skipped)" -Expected 404 -Actual "N/A" -Notes "SKIPPED" -Details "No second coach (coach B) exists in the local DB - see fixtures-manifest.txt"
} else {
    $r = Invoke-VisioCall -Path "/api/v1/coach/sessions/$F8/visio-token" -Token $CoachToken
    Add-Result -Num "C3" -Scenario "Coach, F8 (session owned by coach B)" -Path "coach/$F8" -Expected 404 -Actual $r.StatusCode
}

$r = Invoke-VisioCall -Path "/api/v1/coach/sessions/$F2/visio-token" -Token $CoachToken
Add-Result -Num "C4" -Scenario "Coach, F2 (PRESENTIEL_SOLO)" -Path "coach/$F2" -Expected 409 -Actual $r.StatusCode -Details (Format-ConflictDetails $r.Content)

$r = Invoke-VisioCall -Path "/api/v1/coach/sessions/$F3/visio-token" -Token $CoachToken
Add-Result -Num "C5" -Scenario "Coach, F3 (group session)" -Path "coach/$F3" -Expected 409 -Actual $r.StatusCode -Details (Format-ConflictDetails $r.Content)

$r = Invoke-VisioCall -Path "/api/v1/coach/sessions/$F4/visio-token" -Token $CoachToken
Add-Result -Num "C6" -Scenario "Coach, F4 (cancelled)" -Path "coach/$F4" -Expected 409 -Actual $r.StatusCode -Details (Format-ConflictDetails $r.Content)

$r = Invoke-VisioCall -Path "/api/v1/coach/sessions/$F5/visio-token" -Token $CoachToken
Add-Result -Num "C7" -Scenario "Coach, F5 (window not open, +3h)" -Path "coach/$F5" -Expected 409 -Actual $r.StatusCode -Details (Format-ConflictDetails $r.Content)

$r = Invoke-VisioCall -Path "/api/v1/coach/sessions/$F6/visio-token" -Token $CoachToken
Add-Result -Num "C8" -Scenario "Coach, F6 (window closed, -3h)" -Path "coach/$F6" -Expected 409 -Actual $r.StatusCode -Details (Format-ConflictDetails $r.Content)

$r = Invoke-VisioCall -Path "/api/v1/coach/sessions/$F1/visio-token" -NoAuthHeader
Add-Result -Num "C9" -Scenario "Coach endpoint, no Authorization header" -Path "coach/$F1" -Expected 401 -Actual $r.StatusCode

$r = Invoke-VisioCall -Path "/api/v1/coach/sessions/$F1/visio-token" -Token $MemberToken
Add-Result -Num "C10" -Scenario "Coach endpoint, member token (CoachOnly policy)" -Path "coach/$F1" -Expected 403 -Actual $r.StatusCode

# ---------------------------------------------------------------------------
# Member endpoint
# ---------------------------------------------------------------------------

$r = Invoke-VisioCall -Path "/api/v1/member/sessions/$F1/visio-token" -Token $MemberToken
Add-Result -Num "MT1" -Scenario "Member, F1 (VISIO, in window)" -Path "member/$F1" -Expected 200 -Actual $r.StatusCode -Details (Format-SuccessDetails $r.Content)

$r = Invoke-VisioCall -Path "/api/v1/member/sessions/$F7/visio-token" -Token $MemberToken
Add-Result -Num "MT2" -Scenario "Member, F7 (other member's session)" -Path "member/$F7" -Expected 404 -Actual $r.StatusCode

$r = Invoke-VisioCall -Path "/api/v1/member/sessions/$F3/visio-token" -Token $MemberToken
Add-Result -Num "MT3" -Scenario "Member, F3 (group session, M1 is a participant)" -Path "member/$F3" -Expected 404 -Actual $r.StatusCode

$r = Invoke-VisioCall -Path "/api/v1/member/sessions/$F5/visio-token" -Token $MemberToken
Add-Result -Num "MT4" -Scenario "Member, F5 (window not open, +3h)" -Path "member/$F5" -Expected 409 -Actual $r.StatusCode -Details (Format-ConflictDetails $r.Content)

$r = Invoke-VisioCall -Path "/api/v1/member/sessions/$F1/visio-token" -Token $CoachToken
Add-Result -Num "MT5" -Scenario "Member endpoint, coach token (MemberOnly policy)" -Path "member/$F1" -Expected 403 -Actual $r.StatusCode

# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $OutDir "run-visio-tests-$timestamp.md"

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Lot 8.2 visio-token HTTP matrix - $timestamp")
[void]$sb.AppendLine()
[void]$sb.AppendLine("Base URL: $BaseUrl")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| # | Scenario | Path | Expected | Actual | Status | Details |")
[void]$sb.AppendLine("|---|---|---|---|---|---|---|")
foreach ($row in $script:Results) {
    [void]$sb.AppendLine("| $($row.Num) | $($row.Scenario) | $($row.Path) | $($row.Expected) | $($row.Actual) | $($row.Status) | $($row.Details) $($row.Notes) |")
}

Set-Content -Path $reportPath -Value $sb.ToString() -Encoding utf8
Write-Host ""
Write-Host "Report written to $reportPath" -ForegroundColor Cyan

$failCount = @($script:Results | Where-Object { $_.Status -eq "FAIL" }).Count
if ($failCount -gt 0) {
    Write-Host "$failCount scenario(s) FAILED." -ForegroundColor Red
    exit 1
} else {
    Write-Host "All scenarios PASSED (or explicitly skipped)." -ForegroundColor Green
    exit 0
}
