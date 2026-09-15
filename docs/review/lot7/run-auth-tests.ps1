<#
.SYNOPSIS
    Lot 7 / E1.1 - auth runtime regression matrix.
    Reproduces the 9 scenarios in docs/review/lot7/auth-baseline-before.md plus the
    6 ClaimTypes.NameIdentifier call sites (auth/logout, UserDevicesController x2,
    AccountController x3), each against a fresh OTP-verified session unless the
    scenario is deliberately chained (token reuse / revocation checks).

.NOTES
    Windows PowerShell 5.1 compatible. Always uses -UseBasicParsing.
    Requires: API running locally (ASPNETCORE_ENVIRONMENT=Development), its console
    output redirected to -LogFile (Development logs the plaintext OTP via
    Console.WriteLine("[OTP] {email} -> {code}")), and mentora-postgres-local
    reachable via `docker exec` for row-accounting queries.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5243",
    [string]$CoachEmail = "coach@mentora.fr",
    [string]$MemberEmail = "member@mentora.fr",
    [Parameter(Mandatory = $true)]
    [string]$LogFile,
    [string]$PostgresContainer = "mentora-postgres-local",
    [string]$PgUser = "mentora_user",
    [string]$PgDatabase = "mentora_db",
    [string]$OutDir = (Join-Path $PSScriptRoot ".")
)

$ErrorActionPreference = "Stop"
$script:Results = New-Object System.Collections.Generic.List[object]

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Add-Result {
    param([string]$Num, [string]$Scenario, $Expected, $Actual, [string]$Notes = "")
    $pass = ($Actual -eq $Expected)
    $status = if ($pass) { "PASS" } else { "FAIL" }
    $script:Results.Add([PSCustomObject]@{
        Num      = $Num
        Scenario = $Scenario
        Expected = $Expected
        Actual   = $Actual
        Status   = $status
        Notes    = $Notes
    })
    Write-Host "[$status] #$Num $Scenario -> expected $Expected, got $Actual" -ForegroundColor $(if ($pass) { "Green" } else { "Red" })
    if ($Notes) { Write-Host "         $Notes" -ForegroundColor DarkGray }
}

function Invoke-ApiCall {
    param(
        [string]$Method,
        [string]$Path,
        $Body = $null,
        [string]$Token = $null
    )
    $uri = "$BaseUrl$Path"
    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    $params = @{
        Method          = $Method
        Uri             = $uri
        UseBasicParsing = $true
        Headers         = $headers
    }
    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 10)
    }
    try {
        $resp = Invoke-WebRequest @params
        $statusCode = [int]$resp.StatusCode
        $content = $null
        if ($resp.Content) {
            try { $content = $resp.Content | ConvertFrom-Json } catch { $content = $resp.Content }
        }
        return [PSCustomObject]@{ StatusCode = $statusCode; Content = $content }
    }
    catch {
        $webResp = $_.Exception.Response
        if ($webResp) {
            $statusCode = [int]$webResp.StatusCode
            $stream = $webResp.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $rawBody = $reader.ReadToEnd()
            $content = $null
            try { $content = $rawBody | ConvertFrom-Json } catch { $content = $rawBody }
            return [PSCustomObject]@{ StatusCode = $statusCode; Content = $content }
        }
        throw
    }
}

function Get-LatestOtp {
    param([string]$Email)
    if (-not (Test-Path $LogFile)) { throw "Log file not found: $LogFile" }
    $needle = "[OTP] $Email"
    $lines = Get-Content -Path $LogFile | Where-Object { $_.Contains($needle) }
    if (-not $lines -or $lines.Count -eq 0) { throw "No OTP log line found for '$Email' in $LogFile" }
    $lastLine = $lines[-1]
    if ($lastLine -match '(\d{6})\s*$') {
        return $Matches[1]
    }
    throw "Could not parse a 6-digit OTP from log line: $lastLine"
}

function New-Session {
    param([string]$Email, [Nullable[bool]]$IsCoach)
    $body = @{ email = $Email }
    if ($null -ne $IsCoach) { $body.isCoach = $IsCoach }
    $r1 = Invoke-ApiCall -Method POST -Path "/api/v1/auth/otp/request" -Body $body
    if ($r1.StatusCode -ne 200) { throw "OTP request failed for $Email : $($r1.StatusCode) $($r1.Content | ConvertTo-Json -Compress)" }
    Start-Sleep -Milliseconds 300
    $code = Get-LatestOtp -Email $Email
    $body2 = @{ email = $Email; code = $code }
    if ($null -ne $IsCoach) { $body2.isCoach = $IsCoach }
    $r2 = Invoke-ApiCall -Method POST -Path "/api/v1/auth/otp/verify" -Body $body2
    if ($r2.StatusCode -ne 200) { throw "OTP verify failed for $Email : $($r2.StatusCode) $($r2.Content | ConvertTo-Json -Compress)" }
    return $r2.Content.data
}

function Get-TamperedToken {
    # Mutates the FIRST character of the signature, not the last: base64url's final
    # character can carry unused padding bits that some decoders silently ignore, so a
    # last-character flip can occasionally decode to the same bytes and leave the
    # signature validly unchanged. The first character always encodes real data bits.
    param([string]$Token)
    $parts = $Token.Split('.')
    if ($parts.Length -ne 3) { throw "Not a JWT: $Token" }
    $sig = $parts[2]
    $firstChar = $sig.Substring(0, 1)
    $newChar = if ($firstChar -eq 'A') { 'B' } else { 'A' }
    $newSig = $newChar + $sig.Substring(1)
    return "$($parts[0]).$($parts[1]).$newSig"
}

function Invoke-Sql {
    param([string]$Sql)
    $result = $Sql | docker exec -i $PostgresContainer psql -U $PgUser -d $PgDatabase -t -A
    return ($result -join "`n").Trim()
}

function Get-RowCounts {
    param([string]$UserId)
    [PSCustomObject]@{
        Otps          = [int](Invoke-Sql "SELECT COUNT(*) FROM ""AUTH_OTPS"" WHERE ""USER_ID"" = '$UserId';")
        RefreshTokens = [int](Invoke-Sql "SELECT COUNT(*) FROM ""AUTH_REFRESH_TOKENS"" WHERE ""USER_ID"" = '$UserId';")
        Devices       = [int](Invoke-Sql "SELECT COUNT(*) FROM ""USER_DEVICES"" WHERE ""USER_ID"" = '$UserId';")
    }
}

# ---------------------------------------------------------------------------
# Resolve test users + capture BEFORE row counts
# ---------------------------------------------------------------------------

Write-Host "Resolving test user ids..." -ForegroundColor Cyan
$coachUserId  = Invoke-Sql "SELECT ""USER_ID"" FROM ""USERS"" WHERE ""USER_EMAIL"" = '$CoachEmail';"
$memberUserId = Invoke-Sql "SELECT ""USER_ID"" FROM ""USERS"" WHERE ""USER_EMAIL"" = '$MemberEmail';"
if (-not $coachUserId)  { throw "Could not resolve UserId for coach email $CoachEmail" }
if (-not $memberUserId) { throw "Could not resolve UserId for member email $MemberEmail" }
Write-Host "  coach  UserId = $coachUserId"
Write-Host "  member UserId = $memberUserId"

$beforeCoach  = Get-RowCounts -UserId $coachUserId
$beforeMember = Get-RowCounts -UserId $memberUserId
$beforeDeletionMarker = Invoke-Sql "SELECT COALESCE(""USER_DELETION_REASON"",'') || '|' || COALESCE(""USER_DELETION_REQUESTED_DATE""::text,'') FROM ""USERS"" WHERE ""USER_ID"" = '$coachUserId';"

# ---------------------------------------------------------------------------
# Scenarios 1-3
# ---------------------------------------------------------------------------

try {
    $s1 = New-Session -Email $CoachEmail -IsCoach $true
    $r = Invoke-ApiCall -Method GET -Path "/api/v1/coach/me" -Token $s1.accessToken
    Add-Result -Num "1" -Scenario "Valid access token on protected coach endpoint (GET /api/v1/coach/me)" -Expected 200 -Actual $r.StatusCode
}
catch { Add-Result -Num "1" -Scenario "Valid access token on protected coach endpoint (GET /api/v1/coach/me)" -Expected 200 -Actual "ERROR" -Notes $_.Exception.Message }

try {
    $s2 = New-Session -Email $MemberEmail -IsCoach $false
    $r = Invoke-ApiCall -Method GET -Path "/api/v1/member/me" -Token $s2.accessToken
    Add-Result -Num "2" -Scenario "Valid access token on protected member endpoint (GET /api/v1/member/me)" -Expected 200 -Actual $r.StatusCode
}
catch { Add-Result -Num "2" -Scenario "Valid access token on protected member endpoint (GET /api/v1/member/me)" -Expected 200 -Actual "ERROR" -Notes $_.Exception.Message }

try {
    $s3 = New-Session -Email $CoachEmail -IsCoach $true
    $tampered = Get-TamperedToken -Token $s3.accessToken
    $r = Invoke-ApiCall -Method GET -Path "/api/v1/coach/me" -Token $tampered
    Add-Result -Num "3" -Scenario "Tampered access token (signature corrupted) on GET /api/v1/coach/me" -Expected 401 -Actual $r.StatusCode
}
catch { Add-Result -Num "3" -Scenario "Tampered access token (signature corrupted) on GET /api/v1/coach/me" -Expected 401 -Actual "ERROR" -Notes $_.Exception.Message }

# ---------------------------------------------------------------------------
# Scenarios 4-5 (chained: reuse the same original refresh token)
# ---------------------------------------------------------------------------

$s45Token = $null
try {
    $s45 = New-Session -Email $CoachEmail -IsCoach $true
    $s45Token = $s45.refreshToken
    $r = Invoke-ApiCall -Method POST -Path "/api/v1/auth/token/refresh" -Body @{ refreshToken = $s45Token }
    Add-Result -Num "4" -Scenario "Refresh with a valid, unused refresh token" -Expected 200 -Actual $r.StatusCode
}
catch { Add-Result -Num "4" -Scenario "Refresh with a valid, unused refresh token" -Expected 200 -Actual "ERROR" -Notes $_.Exception.Message }

try {
    if (-not $s45Token) { throw "Prerequisite scenario 4 did not produce a refresh token" }
    $r = Invoke-ApiCall -Method POST -Path "/api/v1/auth/token/refresh" -Body @{ refreshToken = $s45Token }
    Add-Result -Num "5" -Scenario "Refresh reusing the same token immediately after (now revoked)" -Expected 400 -Actual $r.StatusCode -Notes "Known preexisting gap: should be 401, not fixed in E1.1"
}
catch { Add-Result -Num "5" -Scenario "Refresh reusing the same token immediately after (now revoked)" -Expected 400 -Actual "ERROR" -Notes $_.Exception.Message }

# ---------------------------------------------------------------------------
# Scenarios 6a-6b / 10 (chained: logout revokes, then refresh with that token)
# ---------------------------------------------------------------------------

$s6Token = $null
try {
    $s6 = New-Session -Email $CoachEmail -IsCoach $true
    $s6Token = $s6.refreshToken
    $r = Invoke-ApiCall -Method POST -Path "/api/v1/auth/logout" -Body @{ refreshToken = $s6Token; deviceToken = $null } -Token $s6.accessToken
    Add-Result -Num "6a" -Scenario "POST /api/v1/auth/logout with a valid access token" -Expected 200 -Actual $r.StatusCode
    Add-Result -Num "10" -Scenario "POST /api/v1/auth/logout valid token (ClaimTypes.NameIdentifier site)" -Expected 200 -Actual $r.StatusCode -Notes "Same call as #6a"
}
catch {
    Add-Result -Num "6a" -Scenario "POST /api/v1/auth/logout with a valid access token" -Expected 200 -Actual "ERROR" -Notes $_.Exception.Message
    Add-Result -Num "10" -Scenario "POST /api/v1/auth/logout valid token (ClaimTypes.NameIdentifier site)" -Expected 200 -Actual "ERROR" -Notes $_.Exception.Message
}

try {
    if (-not $s6Token) { throw "Prerequisite scenario 6a did not produce a refresh token" }
    $r = Invoke-ApiCall -Method POST -Path "/api/v1/auth/token/refresh" -Body @{ refreshToken = $s6Token }
    Add-Result -Num "6b" -Scenario "Refresh attempted with the token 6a just revoked via logout" -Expected 400 -Actual $r.StatusCode -Notes "Same preexisting gap as #5"
}
catch { Add-Result -Num "6b" -Scenario "Refresh attempted with the token 6a just revoked via logout" -Expected 400 -Actual "ERROR" -Notes $_.Exception.Message }

# ---------------------------------------------------------------------------
# Scenarios 7-9
# ---------------------------------------------------------------------------

try {
    $s7 = New-Session -Email $MemberEmail -IsCoach $false
    $r = Invoke-ApiCall -Method GET -Path "/api/v1/coach/me" -Token $s7.accessToken
    Add-Result -Num "7" -Scenario "CoachOnly policy hit with a member token (GET /api/v1/coach/me)" -Expected 403 -Actual $r.StatusCode
}
catch { Add-Result -Num "7" -Scenario "CoachOnly policy hit with a member token (GET /api/v1/coach/me)" -Expected 403 -Actual "ERROR" -Notes $_.Exception.Message }

try {
    $s8 = New-Session -Email $CoachEmail -IsCoach $true
    $r = Invoke-ApiCall -Method GET -Path "/api/v1/member/me" -Token $s8.accessToken
    Add-Result -Num "8" -Scenario "MemberOnly policy hit with a coach token (GET /api/v1/member/me)" -Expected 403 -Actual $r.StatusCode
}
catch { Add-Result -Num "8" -Scenario "MemberOnly policy hit with a coach token (GET /api/v1/member/me)" -Expected 403 -Actual "ERROR" -Notes $_.Exception.Message }

try {
    $s9 = New-Session -Email $CoachEmail -IsCoach $true
    $randomMemberId = [guid]::NewGuid().ToString()
    $r = Invoke-ApiCall -Method GET -Path "/api/v1/coach/members/$randomMemberId/parameters" -Token $s9.accessToken
    Add-Result -Num "9" -Scenario "Scoped coach endpoint with a memberId not attached to the calling coach" -Expected 404 -Actual $r.StatusCode
}
catch { Add-Result -Num "9" -Scenario "Scoped coach endpoint with a memberId not attached to the calling coach" -Expected 404 -Actual "ERROR" -Notes $_.Exception.Message }

# ---------------------------------------------------------------------------
# Scenarios 11a-11b - UserDevicesController (ClaimTypes.NameIdentifier)
# ---------------------------------------------------------------------------

$deviceToken = "lot7-e1.1-test-device-$([guid]::NewGuid())"
try {
    $s11 = New-Session -Email $CoachEmail -IsCoach $true
    $r = Invoke-ApiCall -Method POST -Path "/api/v1/devices" -Body @{ token = $deviceToken; platform = "IOS" } -Token $s11.accessToken
    $rowCount = [int](Invoke-Sql "SELECT COUNT(*) FROM ""USER_DEVICES"" WHERE ""USER_ID"" = '$coachUserId' AND ""USER_DEVICE_TOKEN"" = '$deviceToken';")
    Add-Result -Num "11a" -Scenario "POST /api/v1/devices (Register), valid coach token" -Expected 200 -Actual $r.StatusCode -Notes "Row present for USER_ID=$coachUserId : $rowCount"

    $r2 = Invoke-ApiCall -Method DELETE -Path "/api/v1/devices" -Body @{ token = $deviceToken } -Token $s11.accessToken
    $rowCountAfter = [int](Invoke-Sql "SELECT COUNT(*) FROM ""USER_DEVICES"" WHERE ""USER_ID"" = '$coachUserId' AND ""USER_DEVICE_TOKEN"" = '$deviceToken';")
    Add-Result -Num "11b" -Scenario "DELETE /api/v1/devices (Delete), same token" -Expected 200 -Actual $r2.StatusCode -Notes "Row removed, count now $rowCountAfter"
}
catch {
    Add-Result -Num "11a" -Scenario "POST /api/v1/devices (Register), valid coach token" -Expected 200 -Actual "ERROR" -Notes $_.Exception.Message
    Add-Result -Num "11b" -Scenario "DELETE /api/v1/devices (Delete), same token" -Expected 200 -Actual "ERROR" -Notes $_.Exception.Message
}

# ---------------------------------------------------------------------------
# Scenarios 12a-12c - AccountController deletion request (ClaimTypes.NameIdentifier)
# ---------------------------------------------------------------------------

$deletionReason = "lot7 e1.1 run-auth-tests.ps1 check"
try {
    $s12 = New-Session -Email $CoachEmail -IsCoach $true
    $r = Invoke-ApiCall -Method POST -Path "/api/v1/account/deletion-request" -Body @{ reason = $deletionReason } -Token $s12.accessToken
    Add-Result -Num "12a" -Scenario "POST /api/v1/account/deletion-request (RequestDeletion)" -Expected 200 -Actual $r.StatusCode

    $r2 = Invoke-ApiCall -Method GET -Path "/api/v1/account/deletion-request" -Token $s12.accessToken
    $pendingOk = ($r2.Content.data.isPending -eq $true) -and ($r2.Content.data.reason -eq $deletionReason)
    Add-Result -Num "12b" -Scenario "GET /api/v1/account/deletion-request (GetDeletionStatus)" -Expected 200 -Actual $r2.StatusCode -Notes "isPending/reason match written value: $pendingOk"

    $r3 = Invoke-ApiCall -Method DELETE -Path "/api/v1/account/deletion-request" -Token $s12.accessToken
    $markerAfter = Invoke-Sql "SELECT COALESCE(""USER_DELETION_REASON"",'') || '|' || COALESCE(""USER_DELETION_REQUESTED_DATE""::text,'') FROM ""USERS"" WHERE ""USER_ID"" = '$coachUserId';"
    Add-Result -Num "12c" -Scenario "DELETE /api/v1/account/deletion-request (CancelDeletion)" -Expected 200 -Actual $r3.StatusCode -Notes "Deletion markers reset to null: $($markerAfter -eq '|')"
}
catch {
    Add-Result -Num "12a" -Scenario "POST /api/v1/account/deletion-request (RequestDeletion)" -Expected 200 -Actual "ERROR" -Notes $_.Exception.Message
    Add-Result -Num "12b" -Scenario "GET /api/v1/account/deletion-request (GetDeletionStatus)" -Expected 200 -Actual "ERROR" -Notes $_.Exception.Message
    Add-Result -Num "12c" -Scenario "DELETE /api/v1/account/deletion-request (CancelDeletion)" -Expected 200 -Actual "ERROR" -Notes $_.Exception.Message
}

# ---------------------------------------------------------------------------
# AFTER row counts + report
# ---------------------------------------------------------------------------

$afterCoach  = Get-RowCounts -UserId $coachUserId
$afterMember = Get-RowCounts -UserId $memberUserId
$afterDeletionMarker = Invoke-Sql "SELECT COALESCE(""USER_DELETION_REASON"",'') || '|' || COALESCE(""USER_DELETION_REQUESTED_DATE""::text,'') FROM ""USERS"" WHERE ""USER_ID"" = '$coachUserId';"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $OutDir "auth-test-run-$timestamp.md"

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Auth test run - $timestamp")
[void]$sb.AppendLine()
[void]$sb.AppendLine("Base URL: $BaseUrl | Coach: $CoachEmail ($coachUserId) | Member: $MemberEmail ($memberUserId)")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| # | Scenario | Expected | Actual | Status | Notes |")
[void]$sb.AppendLine("|---|---|---|---|---|---|")
foreach ($row in $script:Results) {
    [void]$sb.AppendLine("| $($row.Num) | $($row.Scenario) | $($row.Expected) | $($row.Actual) | $($row.Status) | $($row.Notes) |")
}
[void]$sb.AppendLine()
[void]$sb.AppendLine("## Row accounting (for exact cleanup)")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| User | Table | Before | After | Delta |")
[void]$sb.AppendLine("|---|---|---|---|---|")
[void]$sb.AppendLine("| coach | AUTH_OTPS | $($beforeCoach.Otps) | $($afterCoach.Otps) | $($afterCoach.Otps - $beforeCoach.Otps) |")
[void]$sb.AppendLine("| coach | AUTH_REFRESH_TOKENS | $($beforeCoach.RefreshTokens) | $($afterCoach.RefreshTokens) | $($afterCoach.RefreshTokens - $beforeCoach.RefreshTokens) |")
[void]$sb.AppendLine("| coach | USER_DEVICES | $($beforeCoach.Devices) | $($afterCoach.Devices) | $($afterCoach.Devices - $beforeCoach.Devices) |")
[void]$sb.AppendLine("| member | AUTH_OTPS | $($beforeMember.Otps) | $($afterMember.Otps) | $($afterMember.Otps - $beforeMember.Otps) |")
[void]$sb.AppendLine("| member | AUTH_REFRESH_TOKENS | $($beforeMember.RefreshTokens) | $($afterMember.RefreshTokens) | $($afterMember.RefreshTokens - $beforeMember.RefreshTokens) |")
[void]$sb.AppendLine("| member | USER_DEVICES | $($beforeMember.Devices) | $($afterMember.Devices) | $($afterMember.Devices - $beforeMember.Devices) |")
[void]$sb.AppendLine()
[void]$sb.AppendLine("USERS deletion marker (coach), format 'reason|requestedDate' (empty = null/null):")
[void]$sb.AppendLine()
[void]$sb.AppendLine("Before: ``$beforeDeletionMarker``")
[void]$sb.AppendLine()
[void]$sb.AppendLine("After: ``$afterDeletionMarker``")

Set-Content -Path $reportPath -Value $sb.ToString() -Encoding utf8

Write-Host ""
Write-Host "Report written to $reportPath" -ForegroundColor Cyan

$failCount = @($script:Results | Where-Object { $_.Status -eq "FAIL" }).Count
if ($failCount -gt 0) {
    Write-Host "$failCount scenario(s) FAILED." -ForegroundColor Red
    exit 1
}
else {
    Write-Host "All scenarios PASSED." -ForegroundColor Green
    exit 0
}
