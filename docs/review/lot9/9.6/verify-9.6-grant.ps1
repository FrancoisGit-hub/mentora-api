<#
.SYNOPSIS
    Lot 9.6 - finding #2 runtime proof. As the demo coach:
      1. picks the coach's first active offer program,
      2. creates one DRAFT product (fixture; the local DB had none),
      3. grants 1 voucher on it to member@mentora.fr,
      4. re-reads GET /api/v1/coach/me.
    Writes fixture-ids.json (product id, order id) for cleanup.sql. Prints no token.
#>
[CmdletBinding()]
param(
    [string]$MemberId = "38b05ed2-e577-4cf7-81e9-365eaafab626",   # member@mentora.fr, attached to coach@mentora.fr
    [string]$Dir = $PSScriptRoot
)
$ErrorActionPreference = "Stop"

function Api { param([string]$Path, [string]$Method = "GET", $Body = $null)
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 10 -Compress } else { $null }
    $r = & "$Dir\call.ps1" -Path $Path -Method $Method -Body $json
    $r[0] | Out-Host
    return (($r | Select-Object -Skip 1) -join "") | ConvertFrom-Json
}

"## 1. Offer program"
$programs = Api "/api/v1/coach/product-programs"
$program  = @($programs.data | Where-Object { $_.isActive })[0]
"  programId: $($program.id)  name: $($program.name)"

"## 2. Fixture product (DRAFT)"
$product = Api "/api/v1/coach/products" POST @{
    name = "Lot96 grant fixture"; description = $null; offerType = "VISIO"; offerNature = $null
    durationMinutes = 60; priceEuros = 50; sport = "TRAINING"; location = $null; tags = @()
    offerProgramId = $program.id
}
$productId = $product.data.productId
"  productId: $productId  status: $($product.data.status)"

"## 3. Grant 1 voucher to member $MemberId"
$grant = Api "/api/v1/coach/members/$MemberId/voucher-grants" POST @{ productId = $productId; quantity = 1; note = "Lot 9.6 runtime proof" }
$grant.data | ConvertTo-Json -Depth 10
$orderId = $grant.data.purchaseId
"  orderId (purchaseId): $orderId"

[PSCustomObject]@{ productId = $productId; orderId = $orderId; memberId = $MemberId } |
    ConvertTo-Json | Set-Content -Path "$Dir\fixture-ids.json" -Encoding utf8

"## 4. GET /api/v1/coach/me after the grant"
$me = Api "/api/v1/coach/me"
$me.data.stats | Format-List | Out-String
