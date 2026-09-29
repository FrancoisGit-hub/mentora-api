<#
.SYNOPSIS
    Lot 9.4 - V6: number of SQL commands (EF "Executed DbCommand" log lines) issued by each read call.
    Reads the API log (EF command logging at Information) before and after each call.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$TokensFile,
    [Parameter(Mandatory = $true)][string]$LogFile,
    [Parameter(Mandatory = $true)][string]$SliceDir,
    [string]$BaseUrl = "http://localhost:5243"
)
$ErrorActionPreference = "Stop"
$tok = Get-Content $TokensFile -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $SliceDir | Out-Null

function Read-Log {
    $fs = [System.IO.File]::Open($LogFile, 'Open', 'Read', 'ReadWrite')
    try { $sr = New-Object System.IO.StreamReader($fs, [System.Text.Encoding]::UTF8); return @($sr.ReadToEnd() -split "`r?`n") } finally { $fs.Dispose() }
}
function Stable-Log {
    # The redirected console log lags: wait until its length stops changing.
    $prev = -1
    for ($i = 0; $i -lt 30; $i++) { $n = (Read-Log).Count; if ($n -eq $prev) { return $n }; $prev = $n; Start-Sleep -Seconds 2 }
    return $prev
}
function Invoke-Counted {
    param([string]$Label, [string]$Path, [string]$Who)
    $before = Stable-Log
    $headers = @{}
    if ($Who -eq "member") { $headers["Authorization"] = "Bearer $($tok.memberAccessToken)" }
    if ($Who -eq "coach")  { $headers["Authorization"] = "Bearer $($tok.coachAAccessToken)" }
    $status = 0
    try { $status = [int](Invoke-WebRequest -Uri "$BaseUrl$Path" -Headers $headers -UseBasicParsing).StatusCode }
    catch { $status = [int]$_.Exception.Response.StatusCode }
    Start-Sleep -Seconds 3
    $after = Stable-Log
    $slice = (Read-Log)[$before..($after - 1)]
    $cmds = @($slice | Where-Object { $_ -match 'Executed DbCommand' }).Count
    $file = Join-Path $SliceDir (($Label -replace '[^A-Za-z0-9]+', '_') + ".log")
    $slice | Set-Content -Path $file -Encoding utf8
    "{0,-58} HTTP {1}  SQL commands = {2}   ({3})" -f $Label, $status, $cmds, (Split-Path $file -Leaf)
}

$pectoraux = "d6660410-7110-4ac5-8f08-c90ac7f1288d"; $haltere = "63b68ad4-a46f-44c0-89ba-23c56be801e5"
Invoke-Counted "member list no filter"                    "/api/v1/member/exercises" member
Invoke-Counted "member list 2 filters (muscleGroup+equipment)" "/api/v1/member/exercises?muscleGroupId=$pectoraux&equipmentId=$haltere" member
Invoke-Counted "member list all 5 filters + search"       "/api/v1/member/exercises?muscleGroupId=$pectoraux&equipmentId=$haltere&objectiveId=88e02f18-6783-4826-9056-24c4e3d4914b&level=INTERMEDIATE&search=couch" member
Invoke-Counted "member list page 2 (cursor)"              ("/api/v1/member/exercises?limit=10&cursor=" + [uri]::EscapeDataString([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('{"Name":"Curl barre","ExerciseId":"c2ff6721-89b6-4711-973f-937c17f4a6e1"}')))) member
Invoke-Counted "member detail EX001"                      "/api/v1/member/exercises/1194fd80-be00-4630-907e-80609ef0f331" member
Invoke-Counted "member detail 404"                        "/api/v1/member/exercises/5b4e9c1a-0c1d-4a55-9d3e-940400000003" member
Invoke-Counted "member exercise-filters"                  "/api/v1/member/exercise-filters" member
Invoke-Counted "coach exercise-filters"                   "/api/v1/coach/exercise-filters" coach
Invoke-Counted "coach list"                               "/api/v1/coach/exercises" coach
Invoke-Counted "coach detail EX001"                       "/api/v1/coach/exercises/1194fd80-be00-4630-907e-80609ef0f331" coach
