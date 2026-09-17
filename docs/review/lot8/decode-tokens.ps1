<#
.SYNOPSIS
    Lot 8.2 - fetches fresh visio tokens for F1 (coach + member) and decodes their
    JWT header/payload manually (base64url, no library) to prove claim shapes.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5243",
    [Parameter(Mandatory = $true)][string]$CoachToken,
    [Parameter(Mandatory = $true)][string]$MemberToken,
    [Parameter(Mandatory = $true)][string]$F1,
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = $PSScriptRoot }

function Invoke-VisioCall {
    param([string]$Path, [string]$Token)
    $uri = "$BaseUrl$Path"
    $headers = @{ Authorization = "Bearer $Token" }
    $resp = Invoke-WebRequest -Method POST -Uri $uri -UseBasicParsing -Headers $headers -ContentType "application/json" -Body ""
    return $resp.Content | ConvertFrom-Json
}

function ConvertFrom-Base64Url {
    param([string]$Value)
    $s = $Value.Replace('-', '+').Replace('_', '/')
    switch ($s.Length % 4) {
        2 { $s += "==" }
        3 { $s += "=" }
    }
    $bytes = [Convert]::FromBase64String($s)
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

function Decode-Jwt {
    param([string]$Token)
    $parts = $Token.Split('.')
    if ($parts.Length -ne 3) { throw "Not a JWT" }
    $headerJson  = ConvertFrom-Base64Url -Value $parts[0]
    $payloadJson = ConvertFrom-Base64Url -Value $parts[1]
    [PSCustomObject]@{
        HeaderRaw  = $headerJson
        PayloadRaw = $payloadJson
        Header     = $headerJson  | ConvertFrom-Json
        Payload    = $payloadJson | ConvertFrom-Json
    }
}

$coachResp  = Invoke-VisioCall -Path "/api/v1/coach/sessions/$F1/visio-token" -Token $CoachToken
$memberResp = Invoke-VisioCall -Path "/api/v1/member/sessions/$F1/visio-token" -Token $MemberToken

$coachResp  | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $OutDir "c1-response.json") -Encoding utf8
$memberResp | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $OutDir "mt1-response.json") -Encoding utf8

$c1Token  = $coachResp.data.token
$mt1Token = $memberResp.data.token

$c1Decoded  = Decode-Jwt -Token $c1Token
$mt1Decoded = Decode-Jwt -Token $mt1Token

# Save raw decoded JSON for the record (never the compact token itself)
$c1Decoded.HeaderRaw   | Set-Content -Path (Join-Path $OutDir "c1-header.json") -Encoding utf8
$c1Decoded.PayloadRaw  | Set-Content -Path (Join-Path $OutDir "c1-payload.json") -Encoding utf8
$mt1Decoded.HeaderRaw  | Set-Content -Path (Join-Path $OutDir "mt1-header.json") -Encoding utf8
$mt1Decoded.PayloadRaw | Set-Content -Path (Join-Path $OutDir "mt1-payload.json") -Encoding utf8

function Show-Decoded {
    param([string]$Label, $Decoded, $RespData)
    Write-Host "=== $Label ===" -ForegroundColor Cyan
    Write-Host "alg: $($Decoded.Header.alg)"
    Write-Host "iss: $($Decoded.Header.typ) / payload.iss: $($Decoded.Payload.iss)"
    Write-Host "sub: $($Decoded.Payload.sub)"
    Write-Host "name: $($Decoded.Payload.name)"
    $nbfDt = [DateTimeOffset]::FromUnixTimeSeconds([long]$Decoded.Payload.nbf).UtcDateTime.ToString("o")
    $expDt = [DateTimeOffset]::FromUnixTimeSeconds([long]$Decoded.Payload.exp).UtcDateTime.ToString("o")
    Write-Host "nbf: $($Decoded.Payload.nbf) ($nbfDt)"
    Write-Host "exp: $($Decoded.Payload.exp) ($expDt)"
    Write-Host "video claim .NET type: $($Decoded.Payload.video.GetType().FullName)"
    Write-Host "video.room: $($Decoded.Payload.video.room)"
    Write-Host "video.roomJoin: $($Decoded.Payload.video.roomJoin)"
    Write-Host "video.canPublish: $($Decoded.Payload.video.canPublish)"
    Write-Host "video.canSubscribe: $($Decoded.Payload.video.canSubscribe)"
    Write-Host "video.canPublishData: $($Decoded.Payload.video.canPublishData)"
    Write-Host "response.data.expiresAt: $($RespData.expiresAt)"
    Write-Host "response.data.roomName: $($RespData.roomName)"
    Write-Host "response.data.identity: $($RespData.identity)"
    Write-Host "exp (epoch->UTC) == response.expiresAt : $($expDt -eq ([DateTime]$RespData.expiresAt).ToUniversalTime().ToString('o'))"
    Write-Host ""
}

Show-Decoded -Label "C1 (coach token)"  -Decoded $c1Decoded  -RespData $coachResp.data
Show-Decoded -Label "MT1 (member token)" -Decoded $mt1Decoded -RespData $memberResp.data

Write-Host "Same roomName: $($coachResp.data.roomName -eq $memberResp.data.roomName) ($($coachResp.data.roomName))"
Write-Host "Coach identity: $($coachResp.data.identity)"
Write-Host "Member identity: $($memberResp.data.identity)"
