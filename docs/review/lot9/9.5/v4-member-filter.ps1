<#
.SYNOPSIS
    Lot 9.5 V4 - walks GET api/v1/member/exercises?muscleGroupId=<id> (all pages) with the member
    token and writes the sorted exercise names to a file, printing the count.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$TokensFile,
    [Parameter(Mandatory = $true)][string]$MuscleGroupId,
    [Parameter(Mandatory = $true)][string]$OutFile,
    [string]$BaseUrl = "http://localhost:5243"
)
$ErrorActionPreference = "Stop"
$tok = Get-Content $TokensFile -Raw | ConvertFrom-Json
$headers = @{ Authorization = "Bearer $($tok.memberAccessToken)" }

$names = New-Object System.Collections.Generic.List[string]
$cursor = $null; $pages = 0
do {
    $q = "limit=100&muscleGroupId=$MuscleGroupId"
    if ($cursor) { $q += "&cursor=$([uri]::EscapeDataString($cursor))" }
    $r = Invoke-WebRequest -Method GET -Uri "$BaseUrl/api/v1/member/exercises?$q" -Headers $headers -UseBasicParsing
    $raw = [System.Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
    $j = $raw | ConvertFrom-Json
    if (-not $j.success) { throw "Unexpected response: $raw" }
    foreach ($i in $j.data.items) { $names.Add($i.name) }
    $cursor = $j.data.nextCursor; $pages++
} while ($cursor)

$names | Sort-Object | Set-Content -Path $OutFile -Encoding utf8
"muscleGroupId=$MuscleGroupId -> count=$($names.Count) pages=$pages (names in $OutFile)"
