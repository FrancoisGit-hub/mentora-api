<#
.SYNOPSIS
    Lot 9.4 - API checks V2 to V5, V7 and V8 against a running local API.
    Tokens come from get-tokens.ps1 (real OTP flow). SQL expectations come from expected-counts.sql.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$TokensFile,
    [Parameter(Mandatory = $true)][string]$ExpectedFile,   # lines "label|n" from expected-counts.sql (psql -At)
    [Parameter(Mandatory = $true)][string]$FixtureIdsFile,  # {activeId, inactiveId} of coach A's test exercises
    [Parameter(Mandatory = $true)][string]$ExercisesCsv,
    [string]$BaseUrl = "http://localhost:5243"
)
$ErrorActionPreference = "Stop"
$tok = Get-Content $TokensFile -Raw | ConvertFrom-Json
$fx  = Get-Content $FixtureIdsFile -Raw | ConvertFrom-Json
$coachBExerciseId = "5b4e9c1a-0c1d-4a55-9d3e-940400000003"
$ex001Id          = "1194fd80-be00-4630-907e-80609ef0f331"

$expected = @{}
foreach ($line in Get-Content $ExpectedFile -Encoding utf8) {
    if ($line -match '^(V\d[^|]*)\|(\d+)$') { $expected[$Matches[1]] = [int]$Matches[2] }
}

function Call {
    param([string]$Path, [string]$Who = "", [string]$Method = "GET")
    $headers = @{}
    if ($Who -eq "member") { $headers["Authorization"] = "Bearer $($tok.memberAccessToken)" }
    if ($Who -eq "coach")  { $headers["Authorization"] = "Bearer $($tok.coachAAccessToken)" }
    try {
        $r = Invoke-WebRequest -Method $Method -Uri "$BaseUrl$Path" -Headers $headers -UseBasicParsing
        $raw = [System.Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
        return [PSCustomObject]@{ Status = [int]$r.StatusCode; Raw = $raw; Json = $(if ($raw) { $raw | ConvertFrom-Json } else { $null }) }
    } catch {
        $resp = $_.Exception.Response
        if (-not $resp) { throw }
        $sr = New-Object System.IO.StreamReader($resp.GetResponseStream(), [System.Text.Encoding]::UTF8)
        $raw = $sr.ReadToEnd(); $j = $null; try { $j = $raw | ConvertFrom-Json } catch {}
        return [PSCustomObject]@{ Status = [int]$resp.StatusCode; Raw = $raw; Json = $j }
    }
}

function Walk {
    param([string]$Query)
    $items = New-Object System.Collections.Generic.List[object]; $cursor = $null; $pages = 0
    do {
        $q = "limit=100"; if ($Query) { $q += "&$Query" }; if ($cursor) { $q += "&cursor=$([uri]::EscapeDataString($cursor))" }
        $r = Call -Path "/api/v1/member/exercises?$q" -Who member
        if ($r.Status -ne 200) { throw "Walk '$Query' -> $($r.Status): $($r.Raw)" }
        foreach ($i in $r.Json.data.items) { $items.Add($i) }
        $cursor = $r.Json.data.nextCursor; $pages++
    } while ($cursor)
    return [PSCustomObject]@{ Items = $items; Pages = $pages }
}

function Check { param([string]$Label, [int]$Api, [string]$Key)
    $exp = $expected[$Key]
    $ok = ($exp -eq $Api)
    "{0,-58} API = {1,4}  SQL = {2,4}  {3}" -f $Label, $Api, $exp, $(if ($ok) { "OK" } else { "MISMATCH" })
}

$pectoraux = "d6660410-7110-4ac5-8f08-c90ac7f1288d"; $haltere = "63b68ad4-a46f-44c0-89ba-23c56be801e5"
$sansMat = "944fec76-b2b3-4c93-9e7e-1506666fe773"; $banc = "74895d1d-ccb9-4458-b37e-fd899f146dce"; $force = "88e02f18-6783-4826-9056-24c4e3d4914b"

"## V2 - filters (member list, walked with limit=100)"
Check "Pectoraux (muscleGroupId)"               (Walk "muscleGroupId=$pectoraux").Items.Count "V2 Pectoraux (primary group)"
Check "Haltères (equipmentId, primary OR secondary)" (Walk "equipmentId=$haltere").Items.Count "V2 Haltères (primary OR secondary)"
$sm = Walk "equipmentId=$sansMat"
Check "Sans matériel (equipmentId)"             $sm.Items.Count "V2 Sans matériel (primary OR secondary)"
Check "Banc (equipmentId)"                      (Walk "equipmentId=$banc").Items.Count "V2 Banc (primary OR secondary)"
Check "objective Force (objectiveId)"           (Walk "objectiveId=$force").Items.Count "V2 objective Force"
Check "level=BEGINNER"                          (Walk "level=BEGINNER").Items.Count "V2 level BEGINNER"
Check "level=INTERMEDIATE (>= BEGINNER)"        (Walk "level=INTERMEDIATE").Items.Count "V2 level INTERMEDIATE (<=)"
Check "search=squat"                            (Walk "search=squat").Items.Count "V2 search squat"
Check "combined Pectoraux + Haltères"          (Walk "muscleGroupId=$pectoraux&equipmentId=$haltere").Items.Count "V2 combined Pectoraux + Haltères"
Check "combined Force + level=INTERMEDIATE"    (Walk "objectiveId=$force&level=INTERMEDIATE").Items.Count "V2 combined Force + level INTERMEDIATE"

""
"## V2 - 'Sans matériel': exact count $($sm.Items.Count); 10 first names:"
$sm.Items | Select-Object -First 10 | ForEach-Object { "  - $($_.name)  [primary: $($_.primaryEquipment.name); secondary: $($_.secondaryEquipment.name)]" }
$csv = Import-Csv -Path $ExercisesCsv -Encoding UTF8
$bad = @("Banc", "Barre", "Corde", "Poignée simple")
$inSheet = 0; $notInSheet = @(); $violations = @()
foreach ($item in $sm.Items) {
    $row = @($csv | Where-Object { $_.nom_exercice_fr -eq $item.name -and $_.actif -eq "Oui" })
    if ($row.Count -eq 0) { $notInSheet += $item.name; continue }
    $inSheet++
    foreach ($r in $row) { if ($bad -contains $r.materiel_principal.Trim()) { $violations += "$($item.name) -> primary '$($r.materiel_principal)'" } }
}
"  matched to a sheet row by name: $inSheet; not in the sheet (historical): $($notInSheet.Count) -> $($notInSheet -join ' | ')"
"  sheet exercises whose materiel_principal is Banc / Barre / Corde / Poignée simple: $($violations.Count)"
$violations | ForEach-Object { "    $_" }
"  secondary equipment shown for the 'Sans matériel' results (distinct): " + ((@($sm.Items | ForEach-Object { $_.secondaryEquipment.name } | Where-Object { $_ } | Sort-Object -Unique)) -join ", ")

""
"## V3 - visibility"
$all = Walk ""
"member list has coach A's active exercise ($($fx.activeId)): $([bool]($all.Items | Where-Object { $_.exerciseId -eq $fx.activeId }))"
"member list has coach B's exercise ($coachBExerciseId): $([bool]($all.Items | Where-Object { $_.exerciseId -eq $coachBExerciseId }))"
"member list has coach A's inactive exercise ($($fx.inactiveId)): $([bool]($all.Items | Where-Object { $_.exerciseId -eq $fx.inactiveId }))"
foreach ($t in @(@("coach A active", $fx.activeId), @("coach B", $coachBExerciseId), @("coach A inactive", $fx.inactiveId), @("unknown guid", [guid]::NewGuid().ToString()))) {
    $r = Call -Path "/api/v1/member/exercises/$($t[1])" -Who member
    "GET member/exercises/{0} [{1}] -> {2}" -f $t[1], $t[0], $r.Status
}

""
"## V4 - errors"
foreach ($q in @("level=EXPERT", "limit=0", "limit=101", "cursor=garbage", "search=a", ("search=" + ("x" * 61)), "search=%20%20", ("cursor=" + [uri]::EscapeDataString([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('{"foo":1}'))))) ) {
    $r = Call -Path "/api/v1/member/exercises?$q" -Who member
    $shown = if ($q.Length -gt 40) { $q.Substring(0, 40) + "..." } else { $q }
    "GET member/exercises?{0} -> {1}  {2}" -f $shown, $r.Status, $r.Raw
}
$u = Call -Path "/api/v1/member/exercises?muscleGroupId=$([guid]::NewGuid())" -Who member
"unknown muscleGroupId -> $($u.Status), items $($u.Json.data.items.Count), nextCursor = $($u.Json.data.nextCursor)"
$w = Call -Path "/api/v1/member/exercises?search=__" -Who member
"search='__' (LIKE wildcards escaped, must not match everything) -> $($w.Status), items $($w.Json.data.items.Count)"
$w = Call -Path "/api/v1/member/exercises?search=%25%25" -Who member
"search='%%' -> $($w.Status), items $($w.Json.data.items.Count)"
$w = Call -Path "/api/v1/member/exercises?search=%20SQUAT%20" -Who member
"search=' SQUAT ' (trimmed, case-insensitive) -> $($w.Status), items $($w.Json.data.items.Count)"

""
"## V5 - detail of EX001 (member)"
$d = Call -Path "/api/v1/member/exercises/$ex001Id" -Who member
"status $($d.Status); video is null: $($null -eq $d.Json.data.video)"
$d.Json.data | ConvertTo-Json -Depth 8

""
"## V7 - coach"
$legacy = "exerciseId,coachId,name,description,instructions,videoUrl,imageUrl,muscleGroup,equipment,isPolyarticular,isActive,createdDate,updatedDate".Split(",")
$new = "sourceCode,nameEn,category,movementPattern,exerciseType,laterality,startingPosition,gripType,primaryMuscleGroup,secondaryMuscles,primaryEquipment,secondaryEquipment,level,breathing,commonMistakes,precautions,coachTip,objectives,video".Split(",")
$cl = Call -Path "/api/v1/coach/exercises" -Who coach
$keys = @($cl.Json.data[0].PSObject.Properties.Name)
"GET coach/exercises -> $($cl.Status); data is an array: $($cl.Json.data -is [array]); items: $($cl.Json.data.Count)"
"legacy keys present and unchanged in order: $(($keys[0..12] -join ',') -eq ($legacy -join ','))"
"new keys present: $(-not @($new | Where-Object { $keys -notcontains $_ }).Count); total keys: $($keys.Count)"
"every item has the same $($keys.Count) keys: $(-not @($cl.Json.data | Where-Object { ($_.PSObject.Properties.Name -join ',') -ne ($keys -join ',') }).Count)"
$chest = Call -Path "/api/v1/coach/exercises?muscleGroup=CHEST" -Who coach
"muscleGroup=CHEST -> $($chest.Status), items $($chest.Json.data.Count), all legacy muscleGroup CHEST: $(-not @($chest.Json.data | Where-Object { $_.muscleGroup -ne 'CHEST' }).Count)"
$mg = Call -Path "/api/v1/coach/exercises?muscleGroupId=$pectoraux" -Who coach
"muscleGroupId=Pectoraux -> $($mg.Status), items $($mg.Json.data.Count), all primaryMuscleGroup Pectoraux: $(-not @($mg.Json.data | Where-Object { $_.primaryMuscleGroup.name -ne 'Pectoraux' }).Count)"
$eqc = Call -Path "/api/v1/coach/exercises?equipmentId=$haltere" -Who coach
"equipmentId=Haltères -> $($eqc.Status), items $($eqc.Json.data.Count)"
$eqb = Call -Path "/api/v1/coach/exercises?equipment=DUMBBELL" -Who coach
"equipment=DUMBBELL (legacy) -> $($eqb.Status), items $($eqb.Json.data.Count)"
$ob = Call -Path "/api/v1/coach/exercises?objectiveId=$force" -Who coach
"objectiveId=Force -> $($ob.Status), items $($ob.Json.data.Count)"
$lv = Call -Path "/api/v1/coach/exercises?level=BEGINNER" -Who coach
"level=BEGINNER -> $($lv.Status), items $($lv.Json.data.Count)"
$bad = Call -Path "/api/v1/coach/exercises?level=EXPERT" -Who coach
"level=EXPERT -> $($bad.Status) $($bad.Raw)"
$cd = Call -Path "/api/v1/coach/exercises/$($fx.activeId)" -Who coach
"GET coach/exercises/{coach A exercise} -> $($cd.Status); keys: $(@($cd.Json.data.PSObject.Properties.Name).Count)"
$ce = Call -Path "/api/v1/coach/exercises/$ex001Id" -Who coach
"GET coach/exercises/EX001 -> $($ce.Status); level $($ce.Json.data.level); primaryEquipment $($ce.Json.data.primaryEquipment.name); objectives $((($ce.Json.data.objectives | ForEach-Object { $_.name }) -join ' ; '))"
$cb = Call -Path "/api/v1/coach/exercises/$coachBExerciseId" -Who coach
"GET coach/exercises/{coach B exercise} -> $($cb.Status)"

""
"## V8 - filter endpoints"
foreach ($t in @(@("member", "/api/v1/member/exercise-filters", "member"), @("coach", "/api/v1/coach/exercise-filters", "coach"))) {
    $f = Call -Path $t[1] -Who $t[2]
    "GET $($t[1]) -> $($f.Status): muscleGroups $($f.Json.data.muscleGroups.Count), equipment $($f.Json.data.equipment.Count), objectives $($f.Json.data.objectives.Count), levels $($f.Json.data.levels -join ',')"
    "  equipment first 4: " + (($f.Json.data.equipment | Select-Object -First 4 | ForEach-Object { $_.name }) -join " | ")
    "  muscleGroups (sortOrder): " + (($f.Json.data.muscleGroups | ForEach-Object { "$($_.name)($($_.sortOrder))" }) -join ", ")
    "  objectives first 5: " + (($f.Json.data.objectives | Select-Object -First 5 | ForEach-Object { $_.name }) -join " | ")
    "  sample equipment item: " + ($f.Json.data.equipment[0] | ConvertTo-Json -Compress)
}
$x = Call -Path "/api/v1/coach/exercise-filters" -Who member
"member token on coach/exercise-filters -> $($x.Status)"
$x = Call -Path "/api/v1/member/exercise-filters" -Who ""
"no token on member/exercise-filters -> $($x.Status)"
$x = Call -Path "/api/v1/coach/exercise-filters" -Who ""
"no token on coach/exercise-filters -> $($x.Status)"
$x = Call -Path "/api/v1/member/exercises/$ex001Id" -Who ""
"no token on member/exercises/{id} -> $($x.Status)"
$x = Call -Path "/api/v1/member/exercise-filters" -Who coach
"coach token (COACH, has member profile) on member/exercise-filters -> $($x.Status)"
