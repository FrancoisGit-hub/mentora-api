<#
.SYNOPSIS
    Lot 8.2 - real LiveKit acceptance test: starts a throwaway local livekit-server,
    validates the C1 token against it, and a tampered negative control.
    The dev ApiSecret is read from appsettings.Development.json and never echoed.
#>
[CmdletBinding()]
param(
    [string]$AppSettingsDevPath = "C:\Users\gitte\OneDrive\Documents\Web2success\Mentora\tech\repo-mentora\Mentora.API\appsettings.Development.json",
    [string]$C1ResponsePath = (Join-Path $PSScriptRoot "c1-response.json"),
    [string]$LiveKitImage = "livekit/livekit-server:v1.13.7"
)

$ErrorActionPreference = "Stop"

# --- Read secret, never echoed ---
$devSettings = Get-Content $AppSettingsDevPath -Raw | ConvertFrom-Json
$apiSecret = $devSettings.LiveKit.ApiSecret
if ([string]::IsNullOrWhiteSpace($apiSecret)) { throw "LiveKit:ApiSecret not found in $AppSettingsDevPath" }
Write-Host "Secret read from file (length only, never the value): $($apiSecret.Length) chars"

$keysArg = "devkey: $apiSecret"

Write-Host "Starting throwaway lk-test container ($LiveKitImage)..."
docker run --rm -d --name lk-test -p 7880:7880 $LiveKitImage --dev --bind 0.0.0.0 --keys $keysArg | Out-Null

Start-Sleep -Seconds 4
docker ps --filter "name=lk-test" --format "{{.Names}}`t{{.Status}}`t{{.Ports}}"

# Give it a bit more time and check logs for readiness
Start-Sleep -Seconds 3
Write-Host ""
Write-Host "--- lk-test logs (tail) ---"
docker logs lk-test --tail 20

$c1 = Get-Content $C1ResponsePath -Raw | ConvertFrom-Json
$c1Token = $c1.data.token

function Get-TamperedToken {
    param([string]$Token)
    $parts = $Token.Split('.')
    $sig = $parts[2]
    $firstChar = $sig.Substring(0, 1)
    $newChar = if ($firstChar -eq 'A') { 'B' } else { 'A' }
    $newSig = $newChar + $sig.Substring(1)
    return "$($parts[0]).$($parts[1]).$newSig"
}
$tamperedToken = Get-TamperedToken -Token $c1Token

Write-Host ""
Write-Host "=== Positive: valid C1 token against /rtc/validate ==="
try {
    $r1 = Invoke-WebRequest -Uri "http://localhost:7880/rtc/validate?access_token=$c1Token" -UseBasicParsing -Method GET
    Write-Host "StatusCode: $($r1.StatusCode)"
    Write-Host "Body: $($r1.Content)"
} catch {
    $resp = $_.Exception.Response
    if ($resp) {
        Write-Host "StatusCode: $([int]$resp.StatusCode)"
        $stream = $resp.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        Write-Host "Body: $($reader.ReadToEnd())"
    } else {
        Write-Host "ERROR (no HTTP response): $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "=== Negative control: tampered signature against /rtc/validate ==="
try {
    $r2 = Invoke-WebRequest -Uri "http://localhost:7880/rtc/validate?access_token=$tamperedToken" -UseBasicParsing -Method GET
    Write-Host "StatusCode: $($r2.StatusCode)"
    Write-Host "Body: $($r2.Content)"
} catch {
    $resp = $_.Exception.Response
    if ($resp) {
        Write-Host "StatusCode: $([int]$resp.StatusCode)"
        $stream = $resp.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        Write-Host "Body: $($reader.ReadToEnd())"
    } else {
        Write-Host "ERROR (no HTTP response): $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "Stopping lk-test..."
docker stop lk-test | Out-Null
Start-Sleep -Seconds 1
Write-Host "--- docker ps -a filter lk-test (should be empty, --rm auto-removes) ---"
docker ps -a --filter "name=lk-test"
