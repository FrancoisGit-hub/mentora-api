<#
.SYNOPSIS
    Lot 9.5 V3 - assigns every active Mentora template to fixture member M3 with a real coach A JWT,
    then reads the program back and compares its tree with the template body.
    Expectations come from expected-templates.sql (psql -At output, lines
    template_id|template_name|exercise_id|blocks|sessions|circuits).
    Writes the created program ids to -CreatedIdsFile (used by the cleanup).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$TokensFile,
    [Parameter(Mandatory = $true)][string]$ExpectedFile,
    [Parameter(Mandatory = $true)][string]$CreatedIdsFile,
    [string]$MemberId = "5b4e9c1a-0c1d-4a55-9d3e-950500000002",
    [string]$StartDate = "2026-10-05",
    [string]$BaseUrl = "http://localhost:5243"
)
$ErrorActionPreference = "Stop"
$tok = Get-Content $TokensFile -Raw | ConvertFrom-Json
$headers = @{ Authorization = "Bearer $($tok.coachAAccessToken)" }

function Call {
    param([string]$Method, [string]$Path, $Body = $null)
    $p = @{ Method = $Method; Uri = "$BaseUrl$Path"; Headers = $headers; UseBasicParsing = $true }
    if ($null -ne $Body) { $p.ContentType = "application/json"; $p.Body = ($Body | ConvertTo-Json -Depth 10) }
    try {
        $r = Invoke-WebRequest @p
        $raw = [System.Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
        return [PSCustomObject]@{ Status = [int]$r.StatusCode; Location = $r.Headers["Location"]; Raw = $raw; Json = $(if ($raw) { $raw | ConvertFrom-Json } else { $null }) }
    } catch {
        $resp = $_.Exception.Response
        if (-not $resp) { throw }
        $sr = New-Object System.IO.StreamReader($resp.GetResponseStream(), [System.Text.Encoding]::UTF8)
        $raw = $sr.ReadToEnd()
        return [PSCustomObject]@{ Status = [int]$resp.StatusCode; Location = $null; Raw = $raw; Json = $null }
    }
}

function Count-Tree {
    param($Blocks)
    $c = @{ blocks = 0; sessions = 0; circuits = 0; exercises = @() }
    foreach ($b in $Blocks) {
        $c.blocks++
        foreach ($s in $b.sessions) {
            $c.sessions++
            foreach ($ci in $s.circuits) {
                $c.circuits++
                foreach ($e in $ci.exercises) { $c.exercises += $e }
            }
        }
        $sub = Count-Tree -Blocks $b.blocks
        $c.blocks += $sub.blocks; $c.sessions += $sub.sessions; $c.circuits += $sub.circuits; $c.exercises += $sub.exercises
    }
    return $c
}

# Group expectation lines per template.
$templates = [ordered]@{}
foreach ($line in Get-Content $ExpectedFile -Encoding utf8) {
    if (-not $line.Trim()) { continue }
    $f = $line.Split('|')
    if (-not $templates.Contains($f[0])) {
        $templates[$f[0]] = [PSCustomObject]@{ Name = $f[1]; Blocks = [int]$f[3]; Sessions = [int]$f[4]; Circuits = [int]$f[5]; ExerciseIds = @() }
    }
    $templates[$f[0]].ExerciseIds += $f[2]
}

$created = @()
$allOk = $true
foreach ($tid in $templates.Keys) {
    $t = $templates[$tid]
    "=== $($t.Name) ($tid)"
    $post = Call -Method POST -Path "/api/v1/coach/members/$MemberId/training-programs" -Body @{ templateId = $tid; startDate = $StartDate }
    "POST -> $($post.Status)  Location: $($post.Location)"
    if ($post.Status -ne 201) { "  FAIL body: $($post.Raw)"; $allOk = $false; continue }
    $programId = $post.Json.data.program.programId
    $archived  = $post.Json.data.archivedProgramId
    $created += $programId
    "  programId=$programId archivedProgramId=$archived"

    $get = Call -Method GET -Path "/api/v1/coach/training-programs/$programId"
    "GET  -> $($get.Status)"
    if ($get.Status -ne 200) { "  FAIL body: $($get.Raw)"; $allOk = $false; continue }
    $p = $get.Json.data
    "  name='$($p.name)' goal=$($p.goal) status=$($p.status) durationWeeks=$($p.durationWeeks) templateId=$($p.templateId)"

    $tree = Count-Tree -Blocks $p.blocks
    $exIds = @($tree.exercises | ForEach-Object { $_.exerciseId })
    $unresolved = @($tree.exercises | Where-Object { -not $_.exerciseName -or -not $_.exerciseId })
    $sortedExp = @($t.ExerciseIds | Sort-Object)
    $sortedGot = @($exIds | Sort-Object)
    $sameIds = (($sortedExp -join ",") -eq ($sortedGot -join ","))

    $checks = [ordered]@{
        "status ACTIVE"              = ($p.status -eq "ACTIVE")
        "templateId kept"            = ($p.templateId -eq $tid)
        "blocks $($tree.blocks) = $($t.Blocks)"       = ($tree.blocks -eq $t.Blocks)
        "sessions $($tree.sessions) = $($t.Sessions)" = ($tree.sessions -eq $t.Sessions)
        "circuits $($tree.circuits) = $($t.Circuits)" = ($tree.circuits -eq $t.Circuits)
        "exercises $($tree.exercises.Count) = $($t.ExerciseIds.Count)" = ($tree.exercises.Count -eq $t.ExerciseIds.Count)
        "exercise ids identical to template body (multiset)" = $sameIds
        "every exercise resolves (name present), unresolved=$($unresolved.Count)" = ($unresolved.Count -eq 0)
    }
    foreach ($k in $checks.Keys) {
        "  [{0}] {1}" -f $(if ($checks[$k]) { "OK" } else { "FAIL" }), $k
        if (-not $checks[$k]) { $allOk = $false }
    }
    "  distinct exercises: $(@($exIds | Sort-Object -Unique).Count); first names: $((@($tree.exercises | Select-Object -First 4 | ForEach-Object { $_.exerciseName })) -join ' / ')"
}

$created | Set-Content -Path $CreatedIdsFile -Encoding utf8
""
"Created program ids ($($created.Count)) saved to $CreatedIdsFile"
"V3 RESULT: $(if ($allOk) { 'ALL OK' } else { 'FAILURES' })"
