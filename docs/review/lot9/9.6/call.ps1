<#
.SYNOPSIS
    Lot 9.6 - one authenticated call as the demo coach. Prints status + raw body (no token).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [string]$Method = "GET",
    [string]$Body = $null,
    [string]$TokensFile = "$PSScriptRoot\tokens.json",
    [string]$BaseUrl = "http://localhost:5243"
)
$ErrorActionPreference = "Stop"
$tok = Get-Content $TokensFile -Raw | ConvertFrom-Json
$params = @{
    Method = $Method; Uri = "$BaseUrl$Path"; UseBasicParsing = $true
    Headers = @{ Authorization = "Bearer $($tok.coachAccessToken)" }
}
if ($Body) { $params.ContentType = "application/json"; $params.Body = $Body }
try {
    $r = Invoke-WebRequest @params
    $raw = [System.Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
    "$Method $Path -> $([int]$r.StatusCode)"
} catch {
    $resp = $_.Exception.Response
    if (-not $resp) { throw }
    $raw = (New-Object System.IO.StreamReader($resp.GetResponseStream(), [System.Text.Encoding]::UTF8)).ReadToEnd()
    "$Method $Path -> $([int]$resp.StatusCode)"
}
$raw
