<#
.SYNOPSIS
    Lot 9.1 - coach voucher grants + coach/programs -> product-programs alias HTTP matrix
    against real fixtures and real JWTs (V1, V3-V8, V10-V11). V2, V9, V12, V13 are run
    separately (SQL / log analysis / a second HTTP call chained on V1's voucherIds).
.NOTES
    Windows PowerShell 5.1 compatible. Always uses -UseBasicParsing.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5243",
    [Parameter(Mandatory = $true)][string]$TokensFile,
    [Parameter(Mandatory = $true)][string]$CoachAId,
    [Parameter(Mandatory = $true)][string]$CoachBId,
    [Parameter(Mandatory = $true)][string]$MemberM1Id,
    [Parameter(Mandatory = $true)][string]$MemberM2Id,
    [Parameter(Mandatory = $true)][string]$ProductPA1Id,
    [Parameter(Mandatory = $true)][string]$ProductPB1Id,
    [Parameter(Mandatory = $true)][string]$ProductIneligibleId,
    [Parameter(Mandatory = $true)][string]$ProductPublishedId,
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = $PSScriptRoot }

$tokens = Get-Content -Path $TokensFile -Raw | ConvertFrom-Json
$CoachAToken  = $tokens.coachAAccessToken
$CoachBToken  = $tokens.coachBAccessToken
$MemberM1Token = $tokens.memberM1AccessToken
$MemberM2Token = $tokens.memberM2AccessToken

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
    } elseif ($Method -eq "POST") {
        $params.ContentType = "application/json"
        $params.Body = ""
    }
    try {
        $resp = Invoke-WebRequest @params
        $content = $null
        if ($resp.Content) { try { $content = $resp.Content | ConvertFrom-Json } catch { $content = $resp.Content } }
        return [PSCustomObject]@{ StatusCode = [int]$resp.StatusCode; Content = $content; Headers = $resp.Headers }
    } catch {
        $webResp = $_.Exception.Response
        if ($webResp) {
            $stream = $webResp.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $rawBody = $reader.ReadToEnd()
            $content = $null
            try { $content = $rawBody | ConvertFrom-Json } catch { $content = $rawBody }
            return [PSCustomObject]@{ StatusCode = [int]$webResp.StatusCode; Content = $content; Headers = $null }
        }
        throw
    }
}

function Add-Result {
    param([string]$Num, [string]$Scenario, [string]$Method, [string]$Path, $Expected, $Actual, [string]$Details = "")
    $pass = ($Actual -eq $Expected)
    $status = if ($pass) { "PASS" } else { "FAIL" }
    $script:Results.Add([PSCustomObject]@{
        Num = $Num; Scenario = $Scenario; Method = $Method; Path = $Path
        Expected = $Expected; Actual = $Actual; Status = $status; Details = $Details
    })
    $color = if ($pass) { "Green" } else { "Red" }
    Write-Host "[$status] #$Num $Method $Path -> expected $Expected, got $Actual" -ForegroundColor $color
    if ($Details) { Write-Host "         $Details" -ForegroundColor DarkGray }
}

# ---------------------------------------------------------------------------
# V1 - coach A grants quantity 3 on PA1 to M1
# ---------------------------------------------------------------------------

$grantPath = "/api/v1/coach/members/$MemberM1Id/voucher-grants"
$v1Body = @{ productId = $ProductPA1Id; quantity = 3; note = "Lot9.1 verification grant" }
$r = Invoke-ApiCall -Method POST -Path $grantPath -Body $v1Body -Token $CoachAToken
Add-Result -Num "V1" -Scenario "Coach A grants qty 3 on PA1 to M1" -Method POST -Path $grantPath -Expected 201 -Actual $r.StatusCode `
    -Details "voucherCount=$($r.Content.data.voucherCount) purchaseId=$($r.Content.data.purchaseId) origin=$($r.Content.data.origin)"

$v1Response = $r.Content
$v1Response | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $OutDir "v1-response.json") -Encoding utf8

# ---------------------------------------------------------------------------
# V3 - coach B grants to M1 (not attached to B) -> 404
# ---------------------------------------------------------------------------

$v3Body = @{ productId = $ProductPB1Id; quantity = 1; note = $null }
$r = Invoke-ApiCall -Method POST -Path $grantPath -Body $v3Body -Token $CoachBToken
Add-Result -Num "V3" -Scenario "Coach B grants to M1 (not attached to B)" -Method POST -Path $grantPath -Expected 404 -Actual $r.StatusCode -Details $r.Content.error

# ---------------------------------------------------------------------------
# V4 - coach A grants on a product of coach B -> 404
# ---------------------------------------------------------------------------

$v4Body = @{ productId = $ProductPB1Id; quantity = 1; note = $null }
$r = Invoke-ApiCall -Method POST -Path $grantPath -Body $v4Body -Token $CoachAToken
Add-Result -Num "V4" -Scenario "Coach A grants on coach B's product" -Method POST -Path $grantPath -Expected 404 -Actual $r.StatusCode -Details $r.Content.error

# ---------------------------------------------------------------------------
# V5 - coach A grants to M2 (not attached to A) -> 404
# ---------------------------------------------------------------------------

$grantPathM2 = "/api/v1/coach/members/$MemberM2Id/voucher-grants"
$v5Body = @{ productId = $ProductPA1Id; quantity = 1; note = $null }
$r = Invoke-ApiCall -Method POST -Path $grantPathM2 -Body $v5Body -Token $CoachAToken
Add-Result -Num "V5" -Scenario "Coach A grants to M2 (not attached to A)" -Method POST -Path $grantPathM2 -Expected 404 -Actual $r.StatusCode -Details $r.Content.error

# ---------------------------------------------------------------------------
# V6 - quantity 0 and 51 -> 422 with "errors"
# ---------------------------------------------------------------------------

$v6aBody = @{ productId = $ProductPA1Id; quantity = 0; note = $null }
$r = Invoke-ApiCall -Method POST -Path $grantPath -Body $v6aBody -Token $CoachAToken
$hasErrors = ($null -ne $r.Content.errors)
Add-Result -Num "V6a" -Scenario "quantity=0 -> 422 with errors" -Method POST -Path $grantPath -Expected 422 -Actual $r.StatusCode -Details "errors present=$hasErrors : $($r.Content.errors | ConvertTo-Json -Compress)"

$v6bBody = @{ productId = $ProductPA1Id; quantity = 51; note = $null }
$r = Invoke-ApiCall -Method POST -Path $grantPath -Body $v6bBody -Token $CoachAToken
$hasErrors = ($null -ne $r.Content.errors)
Add-Result -Num "V6b" -Scenario "quantity=51 -> 422 with errors" -Method POST -Path $grantPath -Expected 422 -Actual $r.StatusCode -Details "errors present=$hasErrors : $($r.Content.errors | ConvertTo-Json -Compress)"

# ---------------------------------------------------------------------------
# V7 - ineligible product type (VISIO_GROUPE fixture) -> 409 PRODUCT_NOT_ELIGIBLE
# ---------------------------------------------------------------------------

$v7Body = @{ productId = $ProductIneligibleId; quantity = 1; note = $null }
$r = Invoke-ApiCall -Method POST -Path $grantPath -Body $v7Body -Token $CoachAToken
$reason = $r.Content.details.reason
Add-Result -Num "V7" -Scenario "Ineligible product type (VISIO_GROUPE fixture)" -Method POST -Path $grantPath -Expected 409 -Actual $r.StatusCode -Details "details.reason=$reason"

# ---------------------------------------------------------------------------
# V8 - M1 token on grant route -> 403; no token -> 401
# ---------------------------------------------------------------------------

$v8Body = @{ productId = $ProductPA1Id; quantity = 1; note = $null }
$r = Invoke-ApiCall -Method POST -Path $grantPath -Body $v8Body -Token $MemberM1Token
Add-Result -Num "V8a" -Scenario "Member token on grant route (CoachOnly policy)" -Method POST -Path $grantPath -Expected 403 -Actual $r.StatusCode

$r = Invoke-ApiCall -Method POST -Path $grantPath -Body $v8Body -NoAuthHeader
Add-Result -Num "V8b" -Scenario "No Authorization header on grant route" -Method POST -Path $grantPath -Expected 401 -Actual $r.StatusCode

# ---------------------------------------------------------------------------
# V10 - existing member purchase still creates vouchers as before
# ---------------------------------------------------------------------------

$addItemPath = "/api/v1/member/coaches/$CoachAId/cart/items"
$r = Invoke-ApiCall -Method POST -Path $addItemPath -Body @{ productId = $ProductPublishedId; packId = $null; quantity = 2 } -Token $MemberM1Token
Add-Result -Num "V10a" -Scenario "M1 adds PA3 (qty 2) to cart" -Method POST -Path $addItemPath -Expected 200 -Actual $r.StatusCode

$checkoutPath = "/api/v1/member/coaches/$CoachAId/cart/checkout"
$r = Invoke-ApiCall -Method POST -Path $checkoutPath -Token $MemberM1Token
Add-Result -Num "V10b" -Scenario "M1 checks out cart" -Method POST -Path $checkoutPath -Expected 200 -Actual $r.StatusCode -Details "orderId=$($r.Content.data.orderId)"
$v10OrderId = $r.Content.data.orderId
$v10CheckoutUrl = $r.Content.data.checkoutUrl
# session id is embedded in the fake checkout URL: .../fake-checkout/{sessionId}
$v10SessionId = ($v10CheckoutUrl -split "/")[-1]

$simulatePath = "/api/v1/internal/stripe/simulate-webhook"
$r = Invoke-ApiCall -Method POST -Path $simulatePath -Body @{ eventType = "checkout.session.completed"; sessionId = $v10SessionId }
Add-Result -Num "V10c" -Scenario "Simulate checkout.session.completed for M1's order" -Method POST -Path $simulatePath -Expected 200 -Actual $r.StatusCode -Details "status=$($r.Content.status)"

[PSCustomObject]@{ orderId = $v10OrderId; sessionId = $v10SessionId } | ConvertTo-Json | Set-Content -Path (Join-Path $OutDir "v10-response.json") -Encoding utf8

# ---------------------------------------------------------------------------
# V11 - GET coach/programs vs coach/product-programs: same status and payload
# ---------------------------------------------------------------------------

$rOld = Invoke-ApiCall -Method GET -Path "/api/v1/coach/programs" -Token $CoachAToken
$rNew = Invoke-ApiCall -Method GET -Path "/api/v1/coach/product-programs" -Token $CoachAToken
$samePayload = (($rOld.Content | ConvertTo-Json -Depth 10 -Compress) -eq ($rNew.Content | ConvertTo-Json -Depth 10 -Compress))
Add-Result -Num "V11a" -Scenario "GET coach/programs status" -Method GET -Path "/api/v1/coach/programs" -Expected 200 -Actual $rOld.StatusCode
Add-Result -Num "V11b" -Scenario "GET coach/product-programs status" -Method GET -Path "/api/v1/coach/product-programs" -Expected 200 -Actual $rNew.StatusCode
Add-Result -Num "V11c" -Scenario "Same status code, both routes" -Method GET -Path "(compare)" -Expected $rOld.StatusCode -Actual $rNew.StatusCode
Add-Result -Num "V11d" -Scenario "Same payload, both routes" -Method GET -Path "(compare)" -Expected $true -Actual $samePayload

# Create on product-programs -> check Location header (endpoint sets none today)
$createPath = "/api/v1/coach/product-programs"
$r = Invoke-ApiCall -Method POST -Path $createPath -Body @{ name = "Lot9.1 V11 create check" } -Token $CoachAToken
$locationHeader = $null
if ($r.Headers -and $r.Headers["Location"]) { $locationHeader = $r.Headers["Location"] }
Add-Result -Num "V11e" -Scenario "POST product-programs status" -Method POST -Path $createPath -Expected 201 -Actual $r.StatusCode -Details "Location header=$locationHeader (endpoint sets none - StatusCode(201,...) with no CreatedAtAction, confirmed in inventory 2.2)"

# Coach swagger JSON: old path deprecated:true, new path not deprecated
$rSwagger = Invoke-WebRequest -Uri "$BaseUrl/swagger/coach/swagger.json" -UseBasicParsing
$swaggerJson = $rSwagger.Content | ConvertFrom-Json
$oldPathObj = $swaggerJson.paths.'/api/v1/coach/programs'
$newPathObj = $swaggerJson.paths.'/api/v1/coach/product-programs'
$oldDeprecated = $oldPathObj.get.deprecated
$newDeprecated = $newPathObj.get.deprecated
Add-Result -Num "V11f" -Scenario "Swagger: /api/v1/coach/programs GET deprecated=true" -Method GET -Path "/swagger/coach/swagger.json" -Expected $true -Actual ($oldDeprecated -eq $true)
Add-Result -Num "V11g" -Scenario "Swagger: /api/v1/coach/product-programs GET deprecated absent/false" -Method GET -Path "/swagger/coach/swagger.json" -Expected $true -Actual ($newDeprecated -ne $true)

# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $OutDir "verify-9.1-$timestamp.md"

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Lot 9.1 verification - $timestamp")
[void]$sb.AppendLine()
[void]$sb.AppendLine("Base URL: $BaseUrl")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| # | Scenario | Method | Path | Expected | Actual | Status | Details |")
[void]$sb.AppendLine("|---|---|---|---|---|---|---|---|")
foreach ($row in $script:Results) {
    [void]$sb.AppendLine("| $($row.Num) | $($row.Scenario) | $($row.Method) | $($row.Path) | $($row.Expected) | $($row.Actual) | $($row.Status) | $($row.Details) |")
}

Set-Content -Path $reportPath -Value $sb.ToString() -Encoding utf8
Write-Host ""
Write-Host "Report written to $reportPath" -ForegroundColor Cyan

$failCount = @($script:Results | Where-Object { $_.Status -eq "FAIL" }).Count
if ($failCount -gt 0) {
    Write-Host "$failCount scenario(s) FAILED." -ForegroundColor Red
    exit 1
} else {
    Write-Host "All scenarios PASSED." -ForegroundColor Green
    exit 0
}
