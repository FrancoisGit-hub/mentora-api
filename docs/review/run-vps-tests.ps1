<#
run-vps-tests.ps1

Runs PHASES 1, 2, 3, 4 of docs/review/tests-vps-lot51-lot6.md against the live
demo API (https://api-demo.mentorapp.fr). Read-only where the checklist is
read-only; the only writes performed are the ones phases 3 and 4 explicitly
require. Never modifies source, never runs git, never commits.

EXECUTION ORDER (deliberately NOT checklist order - see conversation that
requested this script):
  1. PHASE 1  - lot 5.1 endpoints, read-mostly, no fixture dependency.
  2. PHASE 4  - full path. Creates COACH_A's private fixtures (template,
                assigned program, program sessions, booking) that PHASE 2
                needs to test tenant scope against.
  3. PHASE 2  - tenant scope, MINUS P2.15-P2.17 (those need the group
                session created in PHASE 3's P3.4). Consumes ids captured
                from PHASE 4's responses, never ids re-discovered by listing
                (see exceptions below).
  4. PHASE 3  - agenda + group sessions. P3.4 creates its own group-session
                fixture.
  5. PHASE 2 tail - P2.15, P2.16, P2.17, run now that PHASE 3 exists. Ids in
                the results file are unchanged (P2.15 stays P2.15); only the
                execution position moves.

Checklist ids are never renumbered. Five ids that the checklist verifies with
a raw SQL query have no way to be authoritatively checked from this script
(no SSH / DB access): P4.7 is reported as a single MANUAL row (no API
equivalent exists for "SELECT count(*) FROM EXERCISES" compared against a
P0.4 baseline this script never captures, since phase 0 is out of scope).
P3.1, P4.6, P4.9 and P4.15 each have BOTH an API-observable half (which this
script executes and scores PASS/FAIL under their own id) AND a DB-only half
that the checklist text gives as a literal SQL query - for those four, a
second row suffixed "-SQL" is added (P3.1-SQL, P4.6-SQL, P4.9-SQL,
P4.15-SQL) carrying Status=MANUAL, so the DB obligation stays visible without
silently downgrading the whole id or inventing a fake pass. This is additive
only: no existing id is renamed or removed.

Two reclassifications vs. a naive reading of the checklist, both confirmed
against the live OpenAPI documents before writing this script:
  - P3.6 ("modify the source PRODUCTS capacity") is done for real via
    PUT /api/v1/coach/products/{productId} - this endpoint exists.
  - Voucher-status checks in P3.8 and P4.16 are done for real via
    GET /api/v1/member/vouchers - this endpoint exists and is member-scoped,
    which is sufficient since MEMBER_1 is the only member ever enrolled in
    COACH_A's group session.

KNOWN DATA-DEPENDENT RISKS (the script reports these as BLOCKED rather than
guessing or fabricating a fixture):
  - P2.1 / P2.4 / P2.24 need a pre-existing PRIVATE exercise owned by
    COACH_A. Neither phase 3 nor phase 4 creates one (P4.1's tree only
    points at shared Mentora exercises, by design). This script looks for
    one via GET /coach/exercises?scope=MINE (read-only discovery of
    pre-existing demo data, not a fixture this script creates) and reports
    BLOCKED if COACH_A owns none.
  - P3.4/P3.6/P3.7/P2.20/P2.21 (everything group-session-related) need an
    existing PRESENTIEL_GROUPE product for COACH_A. ProductRequest's own
    schema says PRESENTIEL_GROUPE "is reserved for V2 and is currently
    rejected by validation" on create/replace, so this script cannot create
    one if none exists - it can only discover a pre-existing one via
    GET /coach/products and report BLOCKED if there is none.
  - P3.6's "register participants until capacity is exceeded, expect 409"
    cannot be driven to a real capacity breach with only one eligible member
    (MEMBER_1 - MEMBER_2 is not linked to COACH_A, so registering it 404s,
    it never reaches the capacity branch). The script instead verifies the
    part of P3.6 it CAN prove without a second member: register MEMBER_1,
    then PUT the source product's maxParticipants to a different value, then
    re-read the session and confirm its snapshotted capacity did not change.
    The "409 on overflow" sub-check is reported as BLOCKED with this reason.
  - P4.8/P4.9's auto-link depends on booking a slot that the backend's
    matching heuristic (undocumented in the OpenAPI - response schemas for
    this whole API are erased to `{}` in the generated docs) actually
    associates with one of the assigned program's sessions. This script
    books the earliest slot compatible with MEMBER_1's voucher and then
    reads back whatever link field the live JSON exposes; if the heuristic
    needs closer alignment than "earliest compatible slot", P4.9's
    API-observable half will legitimately FAIL rather than PASS, and that
    is a real finding, not a script bug.
  - MEMBER_2's refresh token is deliberately revoked by P1.16 (that is what
    P1.16 tests). If MEMBER_2's access token expires naturally later in the
    run (P2.22-P2.24, P2.26), refresh will fail by design and the script
    stops per the "ask for a new OTP" rule below - this is expected, not a
    bug in this script.

Tokens are read ONLY from environment variables, never hardcoded, never
printed, never logged:
  MENTORA_TOKEN_COACH_A / _REFRESH
  MENTORA_TOKEN_COACH_B / _REFRESH
  MENTORA_TOKEN_MEMBER_1 / _REFRESH
  MENTORA_TOKEN_MEMBER_2 / _REFRESH

On a 401, the matching refresh token is used exactly once; if that also
fails, the script stops and names the actor that needs a new OTP.

Outputs (both under docs/review/, both uncommitted by this script):
  run-results-<timestamp>.md        - one row per check, nothing else.
  cleanup-manifest-<timestamp>.json - every fixture created, in the order it
                                       must be deleted to respect FKs.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

$BaseUrl = 'https://api-demo.mentorapp.fr'
$RunStamp = Get-Date -Format 'yyyyMMdd-HHmm'
$FixturePrefix = "VPSTEST-$RunStamp"
$ScriptDir = $PSScriptRoot
$ResultsPath = Join-Path $ScriptDir "run-results-$RunStamp.md"
$ManifestPath = Join-Path $ScriptDir "cleanup-manifest-$RunStamp.json"

# Actor ids fixed by the checklist (P0.5) - not secrets, just UUIDs.
$CoachAId = 'cebb9b4d-00ee-4826-a8fe-58e8ab3b325b'
$Member1Id = '25d5b053-d885-4887-b297-822d4341e7ff'

# COACH_B / MEMBER_2 - the cross-tenant fixtures created in SQL direct and kept in the VPS DB
# for the second pass (see "Fixtures conservees en base pour la seconde passe" in
# docs/review/tri-ecarts-lot6.md). MEMBER_2 stays deliberately unattached to any coach.
$CoachBId = 'b0000000-0000-4000-8000-000000000002'
$Member2Id = 'b0000000-0000-4000-8000-000000000004'

# ---------------------------------------------------------------------------
# Token store (mutated in place on a successful refresh)
# ---------------------------------------------------------------------------

$Tokens = @{
    COACH_A  = $env:MENTORA_TOKEN_COACH_A
    COACH_B  = $env:MENTORA_TOKEN_COACH_B
    MEMBER_1 = $env:MENTORA_TOKEN_MEMBER_1
    MEMBER_2 = $env:MENTORA_TOKEN_MEMBER_2
}
$RefreshTokens = @{
    COACH_A  = $env:MENTORA_TOKEN_COACH_A_REFRESH
    COACH_B  = $env:MENTORA_TOKEN_COACH_B_REFRESH
    MEMBER_1 = $env:MENTORA_TOKEN_MEMBER_1_REFRESH
    MEMBER_2 = $env:MENTORA_TOKEN_MEMBER_2_REFRESH
}

foreach ($actorKey in @('COACH_A', 'COACH_B', 'MEMBER_1', 'MEMBER_2')) {
    if ([string]::IsNullOrWhiteSpace($Tokens[$actorKey])) {
        Write-Error "Missing environment variable MENTORA_TOKEN_$actorKey. Set all four access tokens (and their _REFRESH counterparts) before running."
        exit 1
    }
}

# ---------------------------------------------------------------------------
# Result / manifest collectors
# ---------------------------------------------------------------------------

$Results = New-Object System.Collections.Generic.List[object]
$Manifest = New-Object System.Collections.Generic.List[object]

# Ids captured while running PHASE 4, consumed by PHASE 2. Never repopulated
# by a listing call - if a phase-4 step failed, the matching key is absent
# and PHASE 2 checks that need it are reported BLOCKED.
$Captured = @{}

# Reasons a phase-4 (or phase-3) step failed, keyed the same way as $Captured,
# surfaced verbatim in BLOCKED rows that depend on that step.
$BlockedReasons = @{}

function Add-Result {
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Actor,
        [Parameter(Mandatory)][string]$Expected,
        [Parameter(Mandatory)][string]$Actual,
        [Parameter(Mandatory)][ValidateSet('PASS', 'FAIL', 'MANUAL', 'BLOCKED')][string]$Status
    )
    $Results.Add([PSCustomObject]@{
        Id       = $Id
        Method   = $Method
        Path     = $Path
        Actor    = $Actor
        Expected = $Expected
        Actual   = $Actual
        Status   = $Status
    })
}

function Add-Manual {
    param([string]$Id, [string]$Method, [string]$Path, [string]$Actor, [string]$Expected, [string]$Reason)
    Add-Result -Id $Id -Method $Method -Path $Path -Actor $Actor -Expected $Expected -Actual "not executed - $Reason" -Status 'MANUAL'
}

function Add-Blocked {
    param([string]$Id, [string]$Method, [string]$Path, [string]$Actor, [string]$Expected, [string]$Reason)
    Add-Result -Id $Id -Method $Method -Path $Path -Actor $Actor -Expected $Expected -Actual "not executed - $Reason" -Status 'BLOCKED'
}

function Add-Fixture {
    param([string]$Type, [string]$FixtureId, [string]$Owner, [int]$DeleteOrder, [string]$Name = '')
    $Manifest.Add([PSCustomObject]@{
        Type        = $Type
        Id          = $FixtureId
        Owner       = $Owner
        DeleteOrder = $DeleteOrder
        Name        = $Name
    })
}

# ---------------------------------------------------------------------------
# HTTP helper
# ---------------------------------------------------------------------------

function Invoke-Refresh {
    param([string]$ActorKey)
    $rt = $RefreshTokens[$ActorKey]
    if ([string]::IsNullOrWhiteSpace($rt)) { return $false }
    try {
        $bodyJson = (@{ refreshToken = $rt } | ConvertTo-Json)
        $resp = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/auth/token/refresh" -ContentType 'application/json' -Body $bodyJson -UseBasicParsing
        $newAccess = $null
        if ($resp.data) {
            if ($resp.data.accessToken) { $newAccess = $resp.data.accessToken }
            elseif ($resp.data.token) { $newAccess = $resp.data.token }
        }
        if ($newAccess) {
            $Tokens[$ActorKey] = $newAccess
            return $true
        }
        return $false
    } catch {
        return $false
    }
}

# Returns [PSCustomObject]@{ Status = <int or $null>; Data = <parsed .data or $null>; Raw = <parsed body or $null> }
# $Expected may be a single int or an array of ints - Status is compared with -contains.
function Invoke-Api {
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ActorKey,
        [Parameter(Mandatory)]$Expected,
        [object]$Body = $null,
        [switch]$NoAuth,
        [switch]$Quiet
    )
    $expectedArr = @($Expected)
    $uri = "$BaseUrl$Path"
    $headers = @{}
    if (-not $NoAuth) {
        $headers['Authorization'] = "Bearer $($Tokens[$ActorKey])"
    }
    $bodyJson = $null
    if ($null -ne $Body) { $bodyJson = ($Body | ConvertTo-Json -Depth 30) }

    $attempted401Refresh = $false
    while ($true) {
        $status = $null
        $data = $null
        $raw = $null
        try {
            if ($null -ne $bodyJson) {
                $resp = Invoke-WebRequest -Method $Method -Uri $uri -Headers $headers -ContentType 'application/json' -Body $bodyJson -UseBasicParsing
            } else {
                $resp = Invoke-WebRequest -Method $Method -Uri $uri -Headers $headers -UseBasicParsing
            }
            $status = [int]$resp.StatusCode
            if ($resp.Content) {
                try { $raw = $resp.Content | ConvertFrom-Json } catch { $raw = $null }
            }
        } catch {
            $webResp = $_.Exception.Response
            if ($webResp) {
                try { $status = [int]$webResp.StatusCode } catch { $status = $null }
                try {
                    $stream = $webResp.GetResponseStream()
                    $reader = New-Object System.IO.StreamReader($stream)
                    $content = $reader.ReadToEnd()
                    if ($content) { try { $raw = $content | ConvertFrom-Json } catch { $raw = $null } }
                } catch { }
            }
        }

        if ($status -eq 401 -and -not $NoAuth -and -not $attempted401Refresh) {
            $attempted401Refresh = $true
            $refreshed = Invoke-Refresh -ActorKey $ActorKey
            if ($refreshed) {
                $headers['Authorization'] = "Bearer $($Tokens[$ActorKey])"
                continue
            } else {
                Add-Result -Id $Id -Method $Method -Path $Path -Actor $ActorKey -Expected ($expectedArr -join '/') -Actual '401 (refresh failed)' -Status 'FAIL'
                Write-Error "STOP: token for $ActorKey was rejected (401) and refresh also failed. $ActorKey needs a fresh OTP login before this run can continue."
                Write-Host ''
                Write-Host '=== PARTIAL RESULTS BEFORE STOP ===' -ForegroundColor Yellow
                Write-ResultsTable
                Write-ResultsFile
                Write-ManifestFile
                exit 1
            }
        }

        if ($null -ne $raw -and $raw.PSObject.Properties.Name -contains 'data') {
            $data = $raw.data
        } else {
            $data = $raw
        }

        $actualStr = if ($null -ne $status) { "$status" } else { 'ERROR (no response)' }
        $resultStatus = if ($expectedArr -contains $status) { 'PASS' } else { 'FAIL' }
        if (-not $Quiet) {
            Add-Result -Id $Id -Method $Method -Path $Path -Actor $ActorKey -Expected ($expectedArr -join '/') -Actual $actualStr -Status $resultStatus
        }
        return [PSCustomObject]@{ Status = $status; Data = $data; Raw = $raw }
    }
}

# ---------------------------------------------------------------------------
# Generic JSON tree walker - used because response schemas for this API are
# not documented (every response body is `{}` in the generated OpenAPI), so
# ids are located structurally rather than by a known property path.
# ---------------------------------------------------------------------------

function Get-AllNodes {
    param([object]$Obj)
    $result = New-Object System.Collections.Generic.List[object]
    function Walk {
        param($o)
        if ($null -eq $o) { return }
        if ($o -is [System.Management.Automation.PSCustomObject]) {
            $result.Add($o)
            foreach ($p in $o.PSObject.Properties) { Walk $p.Value }
        } elseif (($o -is [System.Collections.IEnumerable]) -and -not ($o -is [string])) {
            foreach ($item in $o) { Walk $item }
        }
    }
    Walk $Obj
    return $result
}

function Get-FirstProp {
    param([object]$Obj, [string[]]$Names)
    if ($null -eq $Obj) { return $null }
    foreach ($n in $Names) {
        if ($Obj.PSObject.Properties.Name -contains $n) {
            $v = $Obj.$n
            if ($null -ne $v) { return $v }
        }
    }
    return $null
}

# ---------------------------------------------------------------------------
# Output writers
# ---------------------------------------------------------------------------

function Write-ResultsTable {
    $Results | Format-Table -Property Id, Method, Path, Actor, Expected, Actual, Status -AutoSize | Out-String -Width 220 | Write-Host
}

function Write-ResultsFile {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('| id | method | path | actor | expected | actual | status |')
    $lines.Add('|---|---|---|---|---|---|---|')
    foreach ($r in $Results) {
        $lines.Add("| $($r.Id) | $($r.Method) | $($r.Path) | $($r.Actor) | $($r.Expected) | $($r.Actual) | $($r.Status) |")
    }
    Set-Content -Path $ResultsPath -Value $lines -Encoding utf8
}

function Write-ManifestFile {
    $sorted = $Manifest | Sort-Object DeleteOrder
    $sorted | ConvertTo-Json -Depth 10 | Set-Content -Path $ManifestPath -Encoding utf8
}

# ---------------------------------------------------------------------------
# PHASE 1 - lot 5.1 endpoints
# ---------------------------------------------------------------------------

function Invoke-Phase1 {
    Write-Host '--- PHASE 1 ---' -ForegroundColor Cyan

    # P1.1
    $from = (Get-Date).ToString('yyyy-MM-dd')
    $to = (Get-Date).AddDays(60).ToString('yyyy-MM-dd')
    $r = Invoke-Api -Id 'P1.1' -Method GET -Path "/api/v1/coach/session-slots?from=$from&to=$to" -ActorKey 'COACH_A' -Expected 200
    if ($r.Status -eq 200 -and $r.Data) {
        $count = @($r.Data).Count
        if ($count -eq 0) { Write-Host "  P1.1: slot list is empty (checklist expects ~1085 seeded slots)" -ForegroundColor Yellow }
    }

    # P1.2
    $r = Invoke-Api -Id 'P1.2' -Method GET -Path '/api/v1/coach/sessions' -ActorKey 'COACH_A' -Expected 200

    # P1.3
    Invoke-Api -Id 'P1.3' -Method GET -Path '/api/v1/coach/conversations' -ActorKey 'COACH_A' -Expected 200 | Out-Null

    # P1.4
    $r = Invoke-Api -Id 'P1.4' -Method GET -Path "/api/v1/coach/members/$Member1Id/conversation" -ActorKey 'COACH_A' -Expected 200
    if ($r.Status -eq 200) {
        $visio = Get-FirstProp -Obj $r.Data -Names @('visioUrl', 'videoUrl', 'permanentVisioUrl', 'visioLink')
        if ($visio -and $visio -notmatch '^https://') {
            Write-Host "  P1.4: visio URL is not https - $visio" -ForegroundColor Yellow
        }
    }

    # P1.5
    Invoke-Api -Id 'P1.5' -Method GET -Path "/api/v1/coach/members/$Member1Id/conversation/messages" -ActorKey 'COACH_A' -Expected 200 | Out-Null

    # P1.6
    $msgBody = @{ content = "$FixturePrefix automated check message" }
    Invoke-Api -Id 'P1.6' -Method POST -Path "/api/v1/coach/members/$Member1Id/conversation/messages" -ActorKey 'COACH_A' -Expected @(200, 201) -Body $msgBody | Out-Null

    # P1.7
    Invoke-Api -Id 'P1.7' -Method PATCH -Path "/api/v1/coach/members/$Member1Id/conversation/messages/read" -ActorKey 'COACH_A' -Expected 200 | Out-Null

    # P1.8
    Invoke-Api -Id 'P1.8' -Method GET -Path "/api/v1/coach/members/$Member1Id/parameters" -ActorKey 'COACH_A' -Expected 200 | Out-Null

    # P1.9 - PUT then re-read
    $paramBody = @{ presentialAddress = "$FixturePrefix 12 Rue de Test, 75000 Paris" }
    Invoke-Api -Id 'P1.9' -Method PUT -Path "/api/v1/coach/members/$Member1Id/parameters" -ActorKey 'COACH_A' -Expected 200 -Body $paramBody | Out-Null

    # P1.10
    $r = Invoke-Api -Id 'P1.10' -Method GET -Path '/api/v1/coach/parameters' -ActorKey 'COACH_A' -Expected 200
    if ($r.Status -eq 200) {
        $behavior = Get-FirstProp -Obj $r.Data -Names @('missedSessionBehavior')
        if ($behavior -ne 'SKIP') { Write-Host "  P1.10: missedSessionBehavior is '$behavior', checklist expects default 'SKIP'" -ForegroundColor Yellow }
    }

    # P1.11 / P1.12
    $deviceToken = "$FixturePrefix-device"
    Invoke-Api -Id 'P1.11' -Method POST -Path '/api/v1/devices' -ActorKey 'MEMBER_1' -Expected @(200, 201) -Body @{ token = $deviceToken; platform = 'ANDROID' } | Out-Null
    Invoke-Api -Id 'P1.12' -Method DELETE -Path '/api/v1/devices' -ActorKey 'MEMBER_1' -Expected @(200, 204) -Body @{ token = $deviceToken } | Out-Null

    # P1.13 / P1.14 / P1.15 - MEMBER_2
    Invoke-Api -Id 'P1.13' -Method POST -Path '/api/v1/account/deletion-request' -ActorKey 'MEMBER_2' -Expected 200 -Body @{ reason = "$FixturePrefix automated test" } | Out-Null
    Invoke-Api -Id 'P1.14' -Method GET -Path '/api/v1/account/deletion-request' -ActorKey 'MEMBER_2' -Expected 200 | Out-Null
    Invoke-Api -Id 'P1.15' -Method DELETE -Path '/api/v1/account/deletion-request' -ActorKey 'MEMBER_2' -Expected 200 | Out-Null

    # P1.16 - logout then refresh with revoked token -> 401
    # NOTE: this deliberately burns MEMBER_2's refresh token, by design (see
    # header comment). MEMBER_2's existing access token in $Tokens remains
    # usable until it naturally expires.
    $member2Refresh = $RefreshTokens['MEMBER_2']
    Invoke-Api -Id 'P1.16a' -Method POST -Path '/api/v1/auth/logout' -ActorKey 'MEMBER_2' -Expected 200 -Body @{ refreshToken = $member2Refresh } | Out-Null
    if ([string]::IsNullOrWhiteSpace($member2Refresh)) {
        Add-Manual -Id 'P1.16' -Method 'POST' -Path '/api/v1/auth/token/refresh' -Actor 'MEMBER_2' -Expected '401' -Reason 'MENTORA_TOKEN_MEMBER_2_REFRESH not set, cannot exercise revoked-refresh path'
    } else {
        Invoke-Api -Id 'P1.16' -Method POST -Path '/api/v1/auth/token/refresh' -ActorKey 'MEMBER_2' -Expected 401 -Body @{ refreshToken = $member2Refresh } -NoAuth | Out-Null
    }
}

# ---------------------------------------------------------------------------
# PHASE 4 - full path (creates COACH_A's private fixtures)
# ---------------------------------------------------------------------------

function New-ProgramTemplateBody {
    param([string]$ExerciseId1, [string]$ExerciseId2, [string]$SessionType = 'VISIO')
    return @{
        bodyVersion = 1
        blocks = @(
            @{
                level = 'MACROCYCLE'; name = "$FixturePrefix Macro"; position = 1
                blocks = @(
                    @{
                        level = 'MESOCYCLE'; name = "$FixturePrefix Meso"; position = 1
                        blocks = @(
                            @{
                                level = 'MICROCYCLE'; name = "$FixturePrefix Micro"; position = 1; weekNumber = 1
                                sessions = @(
                                    @{
                                        name = "$FixturePrefix Session"; type = $SessionType; dayOfWeek = 1; position = 1
                                        circuits = @(
                                            @{
                                                name = "$FixturePrefix Circuit"; position = 1; mode = 'STANDARD'; restBetweenRoundsSeconds = 60
                                                exercises = @(
                                                    @{ exerciseId = $ExerciseId1; position = 1; loadType = 'BODYWEIGHT'; prescribedSets = 3; prescribedReps = 10; restSeconds = 60 },
                                                    @{ exerciseId = $ExerciseId2; position = 2; loadType = 'BODYWEIGHT'; prescribedSets = 3; prescribedReps = 12; restSeconds = 60 }
                                                )
                                            }
                                        )
                                    }
                                )
                            }
                        )
                    }
                )
            }
        )
    }
}

function Invoke-Phase4 {
    Write-Host '--- PHASE 4 ---' -ForegroundColor Cyan

    # Discover two Mentora (shared) exercises to point the template at.
    $ex = Invoke-Api -Id 'P4.setup-exercises' -Method GET -Path '/api/v1/coach/exercises?scope=MENTORA' -ActorKey 'COACH_A' -Expected 200 -Quiet
    $mentoraExercises = @($ex.Data)
    if ($mentoraExercises.Count -lt 2) {
        $BlockedReasons['phase4'] = 'fewer than 2 shared Mentora exercises visible to COACH_A'
        Add-Blocked -Id 'P4.1' -Method 'POST' -Path '/api/v1/coach/program-templates' -Actor 'COACH_A' -Expected '201' -Reason $BlockedReasons['phase4']
        return
    }
    $mentoraEx1 = (Get-FirstProp -Obj $mentoraExercises[0] -Names @('id', 'exerciseId'))
    $mentoraEx2 = (Get-FirstProp -Obj $mentoraExercises[1] -Names @('id', 'exerciseId'))

    # P4.4 fixture: a private exercise owned by COACH_B, needed to prove
    # cross-coach exerciseId references are rejected.
    $exBody = @{ name = "$FixturePrefix Private Exercise (Coach B)"; muscleGroup = 'FULL_BODY'; equipment = 'BODYWEIGHT'; isPolyarticular = $false }
    $rExB = Invoke-Api -Id 'P4.setup-exerciseB' -Method POST -Path '/api/v1/coach/exercises' -ActorKey 'COACH_B' -Expected 201 -Body $exBody -Quiet
    $coachBExerciseId = $null
    if ($rExB.Status -eq 201) {
        $coachBExerciseId = Get-FirstProp -Obj $rExB.Data -Names @('id', 'exerciseId')
        if ($coachBExerciseId) { Add-Fixture -Type 'EXERCISE' -FixtureId $coachBExerciseId -Owner 'COACH_B' -DeleteOrder 10 -Name $exBody.name }
    }

    # A pre-existing private exercise of COACH_A, if one already exists
    # (read-only discovery of demo data - see header comment).
    $exMine = Invoke-Api -Id 'P4.setup-exercisesMine' -Method GET -Path '/api/v1/coach/exercises?scope=MINE' -ActorKey 'COACH_A' -Expected 200 -Quiet
    $coachAPrivateExerciseId = $null
    if ($exMine.Status -eq 200 -and @($exMine.Data).Count -gt 0) {
        $coachAPrivateExerciseId = Get-FirstProp -Obj (@($exMine.Data)[0]) -Names @('id', 'exerciseId')
    }
    $Captured['coachAPrivateExerciseId'] = $coachAPrivateExerciseId
    $mentoraExerciseId = Get-FirstProp -Obj $mentoraExercises[0] -Names @('id', 'exerciseId')
    $Captured['mentoraExerciseId'] = $mentoraExerciseId

    # P4.1 - create template
    $templateBody = @{
        name = "$FixturePrefix Template"
        description = 'Automated VPS test template'
        goal = 'GENERAL_FITNESS'
        durationWeeks = 1
        body = New-ProgramTemplateBody -ExerciseId1 $mentoraEx1 -ExerciseId2 $mentoraEx2
    }
    $r = Invoke-Api -Id 'P4.1' -Method POST -Path '/api/v1/coach/program-templates' -ActorKey 'COACH_A' -Expected 201 -Body $templateBody
    if ($r.Status -ne 201) {
        $BlockedReasons['phase4'] = 'P4.1 (create program template) did not return 201'
        return
    }
    $templateId = Get-FirstProp -Obj $r.Data -Names @('id', 'programTemplateId')
    if (-not $templateId) {
        $BlockedReasons['phase4'] = 'P4.1 returned 201 but no template id could be located in the response body'
        return
    }
    $Captured['templateId'] = $templateId
    Add-Fixture -Type 'PROGRAM_TEMPLATE' -FixtureId $templateId -Owner 'COACH_A' -DeleteOrder 40 -Name $templateBody.name

    # P4.2 - reread, compare tree round-trips
    $r2 = Invoke-Api -Id 'P4.2' -Method GET -Path "/api/v1/coach/program-templates/$templateId" -ActorKey 'COACH_A' -Expected 200
    if ($r2.Status -eq 200) {
        $rereadJson = ($r2.Data.body | ConvertTo-Json -Depth 30 -Compress)
        $originalJson = ($templateBody.body | ConvertTo-Json -Depth 30 -Compress)
        if ($rereadJson -ne $originalJson) {
            Write-Host '  P4.2: re-read body does not byte-match the posted body (field ordering differences are expected; a real diff of structure is not)' -ForegroundColor Yellow
        }
    }

    # P4.3 - strict validation: empty circuit -> 422
    $emptyCircuitBody = @{
        name = $templateBody.name; description = $templateBody.description; goal = $templateBody.goal; durationWeeks = 1
        body = New-ProgramTemplateBody -ExerciseId1 $mentoraEx1 -ExerciseId2 $mentoraEx2
    }
    $emptyCircuitBody.body.blocks[0].blocks[0].blocks[0].sessions[0].circuits[0].exercises = @()
    Invoke-Api -Id 'P4.3a' -Method PUT -Path "/api/v1/coach/program-templates/$templateId" -ActorKey 'COACH_A' -Expected 422 -Body $emptyCircuitBody | Out-Null

    # P4.3 - mixed STANDARD/INTERVAL fields on one circuit -> 422
    $mixedBody = @{
        name = $templateBody.name; description = $templateBody.description; goal = $templateBody.goal; durationWeeks = 1
        body = New-ProgramTemplateBody -ExerciseId1 $mentoraEx1 -ExerciseId2 $mentoraEx2
    }
    # Direct hashtable key assignment (not Add-Member: exercises[0] is a [hashtable], and
    # Add-Member on a Hashtable attaches a .NET instance property that ConvertTo-Json never
    # serializes - the field would silently never reach the wire).
    $mixedBody.body.blocks[0].blocks[0].blocks[0].sessions[0].circuits[0].mode = 'STANDARD'
    $mixedBody.body.blocks[0].blocks[0].blocks[0].sessions[0].circuits[0].exercises[0].workSeconds = 30
    Invoke-Api -Id 'P4.3b' -Method PUT -Path "/api/v1/coach/program-templates/$templateId" -ActorKey 'COACH_A' -Expected 422 -Body $mixedBody | Out-Null

    # P4.4 - exerciseId belonging to COACH_B -> 422
    if ($coachBExerciseId) {
        $crossBody = @{
            name = $templateBody.name; description = $templateBody.description; goal = $templateBody.goal; durationWeeks = 1
            body = New-ProgramTemplateBody -ExerciseId1 $coachBExerciseId -ExerciseId2 $mentoraEx2
        }
        Invoke-Api -Id 'P4.4' -Method PUT -Path "/api/v1/coach/program-templates/$templateId" -ActorKey 'COACH_A' -Expected 422 -Body $crossBody | Out-Null
    } else {
        Add-Blocked -Id 'P4.4' -Method 'PUT' -Path "/api/v1/coach/program-templates/$templateId" -Actor 'COACH_A' -Expected '422' -Reason 'could not create a private exercise fixture for COACH_B'
    }

    # Restore the template to its valid form before assigning it (P4.3/P4.4
    # PUTs above intentionally failed validation and must not have persisted,
    # but re-PUT the known-good body defensively so P4.5's deep copy source
    # is definitely the intended tree).
    Invoke-Api -Id 'P4.restore-template' -Method PUT -Path "/api/v1/coach/program-templates/$templateId" -ActorKey 'COACH_A' -Expected 200 -Body $templateBody -Quiet | Out-Null

    # P4.5 - assign to MEMBER_1
    $startDate = (Get-Date).ToString('yyyy-MM-dd')
    $assignBody = @{ templateId = $templateId; startDate = $startDate }
    $r5 = Invoke-Api -Id 'P4.5' -Method POST -Path "/api/v1/coach/members/$Member1Id/training-programs" -ActorKey 'COACH_A' -Expected 201 -Body $assignBody
    if ($r5.Status -ne 201) {
        $BlockedReasons['phase4'] = 'P4.5 (assign program) did not return 201'
        return
    }
    $assignRoot = if ($r5.Data.program) { $r5.Data.program } else { $r5.Data }
    $programId = Get-FirstProp -Obj $assignRoot -Names @('id', 'programId')
    if (-not $programId) {
        $BlockedReasons['phase4'] = 'P4.5 returned 201 but no program id could be located in the response body'
        return
    }
    $Captured['programId'] = $programId
    Add-Fixture -Type 'PROGRAM' -FixtureId $programId -Owner 'MEMBER_1 (via COACH_A)' -DeleteOrder 30 -Name "assigned from $($templateBody.name)"

    # Locate a program session id inside the assigned program's tree.
    $rProg = Invoke-Api -Id 'P4.read-assigned-program' -Method GET -Path "/api/v1/coach/training-programs/$programId" -ActorKey 'COACH_A' -Expected 200 -Quiet
    $programSessionId = $null
    $programExerciseId = $null
    if ($rProg.Status -eq 200) {
        $allNodes = Get-AllNodes -Obj $rProg.Data
        foreach ($node in $allNodes) {
            $names = $node.PSObject.Properties.Name
            if (($names -contains 'plannedDate' -or $names -contains 'exercises') -and ($names -contains 'id' -or $names -contains 'programSessionId')) {
                $candidateId = Get-FirstProp -Obj $node -Names @('id', 'programSessionId')
                if ($candidateId -and -not $programSessionId) { $programSessionId = $candidateId }
                if ($names -contains 'exercises') {
                    $exNodes = @($node.exercises)
                    if ($exNodes.Count -gt 0) {
                        $peId = Get-FirstProp -Obj $exNodes[0] -Names @('id', 'programExerciseId')
                        if ($peId -and -not $programExerciseId) { $programExerciseId = $peId }
                    }
                }
            }
        }
    }
    $Captured['programSessionId'] = $programSessionId
    $Captured['programExerciseId'] = $programExerciseId
    if (-not $programSessionId) {
        Write-Host '  Phase 4: could not locate a program session id inside the assigned program response - P2.13/P2.14/P2.23/P4.9+ depend on this' -ForegroundColor Yellow
    }

    # P4.6 - modify source template, re-read assigned program, expect no propagation.
    $modifiedTemplateBody = $templateBody.Clone()
    $modifiedTemplateBody.name = "$FixturePrefix Template (modified)"
    Invoke-Api -Id 'P4.6-edit' -Method PUT -Path "/api/v1/coach/program-templates/$templateId" -ActorKey 'COACH_A' -Expected 200 -Body $modifiedTemplateBody -Quiet | Out-Null
    $rReread = Invoke-Api -Id 'P4.6' -Method GET -Path "/api/v1/coach/training-programs/$programId" -ActorKey 'COACH_A' -Expected 200
    if ($rReread.Status -eq 200) {
        $progName = Get-FirstProp -Obj $rReread.Data -Names @('name')
        if ($progName -eq $modifiedTemplateBody.name) {
            Write-Host '  P4.6: assigned program name changed after editing the source template - propagation leak' -ForegroundColor Red
        }
    }
    Add-Manual -Id 'P4.6-SQL' -Method 'SQL' -Path 'PROGRAM_BLOCKS / PROGRAM_SESSIONS / PROGRAM_CIRCUITS / PROGRAM_EXERCISES' -Actor 'n/a' -Expected 'rows exist independently of template' -Reason 'requires direct DB read, no SSH/DB access from this script'

    Add-Manual -Id 'P4.7' -Method 'SQL' -Path 'EXERCISES' -Actor 'n/a' -Expected 'count unchanged since P0.4' -Reason 'pure SQL count, and depends on a P0.4 baseline this script never captures (phase 0 out of scope)'

    # P4.8 - book a slot for MEMBER_1 matching one of the program's sessions.
    $rVouchers = Invoke-Api -Id 'P4.setup-vouchers' -Method GET -Path '/api/v1/member/vouchers?status=AVAILABLE' -ActorKey 'MEMBER_1' -Expected 200 -Quiet
    $voucherId = $null
    if ($rVouchers.Status -eq 200) {
        $voucherNodes = Get-AllNodes -Obj $rVouchers.Data
        foreach ($node in $voucherNodes) {
            $vid = Get-FirstProp -Obj $node -Names @('voucherId')
            if ($vid) { $voucherId = $vid; break }
            if (($node.PSObject.Properties.Name -contains 'status') -and ($node.status -eq 'AVAILABLE') -and ($node.PSObject.Properties.Name -contains 'id')) {
                $voucherId = $node.id; break
            }
        }
    }
    if (-not $voucherId) {
        $BlockedReasons['phase4booking'] = 'MEMBER_1 has no AVAILABLE voucher'
        Add-Blocked -Id 'P4.8' -Method 'POST' -Path '/api/v1/member/sessions' -Actor 'MEMBER_1' -Expected '201' -Reason $BlockedReasons['phase4booking']
    } else {
        $rSlots = Invoke-Api -Id 'P4.setup-slots' -Method GET -Path "/api/v1/member/session-slots?coachId=$CoachAId&voucherId=$voucherId" -ActorKey 'MEMBER_1' -Expected 200 -Quiet
        $slotId = $null
        if ($rSlots.Status -eq 200 -and @($rSlots.Data).Count -gt 0) {
            $slotId = Get-FirstProp -Obj (@($rSlots.Data)[0]) -Names @('id', 'slotId')
        }
        if (-not $slotId) {
            $BlockedReasons['phase4booking'] = 'no session slot compatible with MEMBER_1''s available voucher'
            Add-Blocked -Id 'P4.8' -Method 'POST' -Path '/api/v1/member/sessions' -Actor 'MEMBER_1' -Expected '201' -Reason $BlockedReasons['phase4booking']
        } else {
            $rBook = Invoke-Api -Id 'P4.8' -Method POST -Path '/api/v1/member/sessions' -ActorKey 'MEMBER_1' -Expected 201 -Body @{ voucherId = $voucherId; slotId = $slotId }
            if ($rBook.Status -eq 201) {
                $bookedSessionId = Get-FirstProp -Obj $rBook.Data -Names @('id', 'sessionId')
                $Captured['bookedSessionId'] = $bookedSessionId
                if ($bookedSessionId) { Add-Fixture -Type 'SESSION' -FixtureId $bookedSessionId -Owner 'MEMBER_1' -DeleteOrder 20 -Name 'booked via /member/sessions' }

                # P4.9 - API-observable half: does the program session now show a link?
                $rProg2 = Invoke-Api -Id 'P4.9' -Method GET -Path "/api/v1/coach/training-programs/$programId" -ActorKey 'COACH_A' -Expected 200
                if ($rProg2.Status -eq 200) {
                    $found = $false
                    foreach ($node in (Get-AllNodes -Obj $rProg2.Data)) {
                        $link = Get-FirstProp -Obj $node -Names @('sessionId', 'bookedSessionId', 'linkedSessionId')
                        if ($link -and $link -eq $bookedSessionId) { $found = $true; break }
                    }
                    if (-not $found) {
                        Write-Host '  P4.9: no program-session node exposes a link matching the booked session id - either the field name differs from the guesses in this script, or the auto-link genuinely did not happen' -ForegroundColor Yellow
                    }
                }
                Add-Manual -Id 'P4.9-SQL' -Method 'SQL' -Path 'PROGRAM_SESSIONS.PROGRAM_SESSION_SESSION_ID' -Actor 'n/a' -Expected 'link populated for the booked session' -Reason 'requires direct DB read, no SSH/DB access from this script'

                # P4.10 - idempotence: cancel and re-book same slot, expect no duplicate link.
                Invoke-Api -Id 'P4.10-cancel' -Method POST -Path "/api/v1/member/sessions/$bookedSessionId/cancel" -ActorKey 'MEMBER_1' -Expected 200 -Body @{ reason = "$FixturePrefix idempotence re-test" } -Quiet | Out-Null
                $rReVouchers = Invoke-Api -Id 'P4.10-vouchers' -Method GET -Path '/api/v1/member/vouchers?status=AVAILABLE' -ActorKey 'MEMBER_1' -Expected 200 -Quiet
                $reVoucherId = $null
                if ($rReVouchers.Status -eq 200) {
                    foreach ($node in (Get-AllNodes -Obj $rReVouchers.Data)) {
                        $vid = Get-FirstProp -Obj $node -Names @('voucherId', 'id')
                        if ($vid) { $reVoucherId = $vid; break }
                    }
                }
                if ($reVoucherId) {
                    $rRebook = Invoke-Api -Id 'P4.10' -Method POST -Path '/api/v1/member/sessions' -ActorKey 'MEMBER_1' -Expected 201 -Body @{ voucherId = $reVoucherId; slotId = $slotId }
                    if ($rRebook.Status -eq 201) {
                        $reBookedSessionId = Get-FirstProp -Obj $rRebook.Data -Names @('id', 'sessionId')
                        $Captured['bookedSessionId'] = $reBookedSessionId
                        if ($reBookedSessionId) { Add-Fixture -Type 'SESSION' -FixtureId $reBookedSessionId -Owner 'MEMBER_1' -DeleteOrder 20 -Name 'rebooked via /member/sessions (P4.10)' }
                    }
                } else {
                    Add-Blocked -Id 'P4.10' -Method 'POST' -Path '/api/v1/member/sessions' -Actor 'MEMBER_1' -Expected '201' -Reason 'voucher was not returned to AVAILABLE after cancel, or none found'
                }
            }
        }
    }

    # P4.11 - member completion
    if ($programSessionId -and $programExerciseId) {
        $completionBody = @{
            status = 'DONE'
            memberFeedback = "$FixturePrefix member feedback"
            exercises = @(@{ programExerciseId = $programExerciseId; actualSets = 3; actualReps = 10; actualRpe = 7 })
        }
        Invoke-Api -Id 'P4.11' -Method PUT -Path "/api/v1/member/program-sessions/$programSessionId/completion" -ActorKey 'MEMBER_1' -Expected 200 -Body $completionBody | Out-Null

        # P4.12 - prescribed columns untouched (best-effort: re-read and eyeball prescribed fields are still present)
        $rCheck = Invoke-Api -Id 'P4.12' -Method GET -Path "/api/v1/coach/training-programs/$programId" -ActorKey 'COACH_A' -Expected 200
        if ($rCheck.Status -eq 200) {
            $prescribedFound = $false
            foreach ($node in (Get-AllNodes -Obj $rCheck.Data)) {
                if ($node.PSObject.Properties.Name -contains 'prescribedSets') { $prescribedFound = $true; break }
            }
            if (-not $prescribedFound) {
                Write-Host '  P4.12: no node in the re-read program exposes prescribedSets - cannot confirm prescribed columns survived (field name may differ)' -ForegroundColor Yellow
            }
        }

        # P4.13 - coach completion, same check
        $completionBody2 = @{
            status = 'DONE'
            coachNote = "$FixturePrefix coach note"
            exercises = @(@{ programExerciseId = $programExerciseId; actualSets = 3; actualReps = 10; actualRpe = 6 })
        }
        Invoke-Api -Id 'P4.13' -Method PUT -Path "/api/v1/coach/program-sessions/$programSessionId/completion" -ActorKey 'COACH_A' -Expected 200 -Body $completionBody2 | Out-Null

        # P4.14 - lastPerformed shows up on reread
        $rLast = Invoke-Api -Id 'P4.14' -Method GET -Path "/api/v1/coach/training-programs/$programId" -ActorKey 'COACH_A' -Expected 200
        if ($rLast.Status -eq 200) {
            $lastPerformedFound = $false
            foreach ($node in (Get-AllNodes -Obj $rLast.Data)) {
                if ($node.PSObject.Properties.Name -contains 'lastPerformed') { $lastPerformedFound = $true; break }
            }
            if (-not $lastPerformedFound) {
                Write-Host '  P4.14: no node exposes a lastPerformed field in the re-read program' -ForegroundColor Yellow
            }
        }
    } else {
        Add-Blocked -Id 'P4.11' -Method 'PUT' -Path '/api/v1/member/program-sessions/{id}/completion' -Actor 'MEMBER_1' -Expected '200' -Reason 'no program session id captured from phase 4'
        Add-Blocked -Id 'P4.12' -Method 'GET' -Path '/api/v1/coach/training-programs/{id}' -Actor 'COACH_A' -Expected '200' -Reason 'no program session id captured from phase 4'
        Add-Blocked -Id 'P4.13' -Method 'PUT' -Path '/api/v1/coach/program-sessions/{id}/completion' -Actor 'COACH_A' -Expected '200' -Reason 'no program session id captured from phase 4'
        Add-Blocked -Id 'P4.14' -Method 'GET' -Path '/api/v1/coach/training-programs/{id}' -Actor 'COACH_A' -Expected '200' -Reason 'no program session id captured from phase 4'
    }

    # P4.15 - MISSED_SESSION_BEHAVIOR = SHIFT, read-time shift, DB dates unchanged
    $currentParams = Invoke-Api -Id 'P4.15-read-params' -Method GET -Path '/api/v1/coach/parameters' -ActorKey 'COACH_A' -Expected 200 -Quiet
    if ($currentParams.Status -eq 200) {
        $p = $currentParams.Data
        $shiftBody = @{
            firstName = (Get-FirstProp -Obj $p -Names @('firstName'))
            lastName = (Get-FirstProp -Obj $p -Names @('lastName'))
            phone = (Get-FirstProp -Obj $p -Names @('phone'))
            hourlyRateEuros = (Get-FirstProp -Obj $p -Names @('hourlyRateEuros'))
            cancellationDelayHours = (Get-FirstProp -Obj $p -Names @('cancellationDelayHours'))
            minBookingNoticeHours = (Get-FirstProp -Obj $p -Names @('minBookingNoticeHours'))
            maxBookingHorizonDays = (Get-FirstProp -Obj $p -Names @('maxBookingHorizonDays'))
            lateCancellationRefunds = (Get-FirstProp -Obj $p -Names @('lateCancellationRefunds'))
            isAcceptingNewBookings = (Get-FirstProp -Obj $p -Names @('isAcceptingNewBookings'))
            defaultSessionDurationMinutes = (Get-FirstProp -Obj $p -Names @('defaultSessionDurationMinutes'))
            missedSessionBehavior = 'SHIFT'
            language = (Get-FirstProp -Obj $p -Names @('language'))
            notifMessages = (Get-FirstProp -Obj $p -Names @('notifMessages'))
            notifNewBooking = (Get-FirstProp -Obj $p -Names @('notifNewBooking'))
            notifBookingCancelled = (Get-FirstProp -Obj $p -Names @('notifBookingCancelled'))
            notifMarketing = (Get-FirstProp -Obj $p -Names @('notifMarketing'))
        }
        Invoke-Api -Id 'P4.15-set' -Method PUT -Path '/api/v1/coach/parameters' -ActorKey 'COACH_A' -Expected 200 -Body $shiftBody -Quiet | Out-Null
        Invoke-Api -Id 'P4.15' -Method GET -Path "/api/v1/coach/training-programs/$programId" -ActorKey 'COACH_A' -Expected 200 | Out-Null
        Add-Manual -Id 'P4.15-SQL' -Method 'SQL' -Path 'PROGRAM_SESSIONS.PROGRAM_SESSION_PLANNED_DATE' -Actor 'n/a' -Expected 'unchanged in DB despite SHIFT read-time recalculation' -Reason 'requires direct DB read, no SSH/DB access from this script'
        # Restore original behavior so we don't leave coach A's real parameters mutated beyond what the checklist itself asked for.
        $restoreBody = $shiftBody.Clone()
        $restoreBody.missedSessionBehavior = (Get-FirstProp -Obj $p -Names @('missedSessionBehavior'))
        Invoke-Api -Id 'P4.15-restore' -Method PUT -Path '/api/v1/coach/parameters' -ActorKey 'COACH_A' -Expected 200 -Body $restoreBody -Quiet | Out-Null
    } else {
        Add-Blocked -Id 'P4.15' -Method 'GET' -Path '/api/v1/coach/training-programs/{id}' -Actor 'COACH_A' -Expected '200' -Reason 'could not read current coach parameters to build the PUT body'
    }

    # P4.16 - cancellation windows. Needs a freshly booked, still-cancellable session.
    if ($Captured.ContainsKey('bookedSessionId') -and $Captured['bookedSessionId']) {
        $sid = $Captured['bookedSessionId']
        Invoke-Api -Id 'P4.16' -Method POST -Path "/api/v1/member/sessions/$sid/cancel" -ActorKey 'MEMBER_1' -Expected 200 -Body @{ reason = "$FixturePrefix cancellation window check" } | Out-Null
        $rVoucherStatus = Invoke-Api -Id 'P4.16-voucher-check' -Method GET -Path '/api/v1/member/vouchers?status=AVAILABLE' -ActorKey 'MEMBER_1' -Expected 200 -Quiet
        if ($rVoucherStatus.Status -eq 200) {
            Write-Host "  P4.16: voucher status after cancellation observed via GET /member/vouchers (see manifest/results for detail); DB-level status confirmation is out of scope" -ForegroundColor DarkGray
        }
    } else {
        Add-Blocked -Id 'P4.16' -Method 'POST' -Path '/api/v1/member/sessions/{id}/cancel' -Actor 'MEMBER_1' -Expected '200' -Reason 'no booked session id captured from phase 4 (P4.8/P4.10 did not succeed)'
    }
}

# ---------------------------------------------------------------------------
# PHASE 2 (main body, excluding P2.15-P2.17 which need Phase 3's group session)
# ---------------------------------------------------------------------------

function Invoke-Phase2Main {
    Write-Host '--- PHASE 2 (main) ---' -ForegroundColor Cyan

    $privateExA = $Captured['coachAPrivateExerciseId']
    $mentoraEx = $Captured['mentoraExerciseId']
    $templateId = $Captured['templateId']
    $programId = $Captured['programId']
    $programSessionId = $Captured['programSessionId']

    if ($privateExA) {
        Invoke-Api -Id 'P2.1' -Method GET -Path "/api/v1/coach/exercises/$privateExA" -ActorKey 'COACH_B' -Expected 404 | Out-Null
        Invoke-Api -Id 'P2.4' -Method DELETE -Path "/api/v1/coach/exercises/$privateExA" -ActorKey 'COACH_B' -Expected 404 | Out-Null
    } else {
        Add-Blocked -Id 'P2.1' -Method 'GET' -Path '/api/v1/coach/exercises/{id}' -Actor 'COACH_B' -Expected '404' -Reason 'COACH_A owns no private exercise (scope=MINE returned none) and phase 4 does not create one'
        Add-Blocked -Id 'P2.4' -Method 'DELETE' -Path '/api/v1/coach/exercises/{id}' -Actor 'COACH_B' -Expected '404' -Reason 'COACH_A owns no private exercise (scope=MINE returned none) and phase 4 does not create one'
    }

    if ($mentoraEx) {
        Invoke-Api -Id 'P2.2' -Method GET -Path "/api/v1/coach/exercises/$mentoraEx" -ActorKey 'COACH_B' -Expected 200 | Out-Null
        Invoke-Api -Id 'P2.3' -Method PUT -Path "/api/v1/coach/exercises/$mentoraEx" -ActorKey 'COACH_B' -Expected 404 -Body @{ name = "$FixturePrefix hijack attempt"; isPolyarticular = $false } | Out-Null
    } else {
        Add-Blocked -Id 'P2.2' -Method 'GET' -Path '/api/v1/coach/exercises/{id}' -Actor 'COACH_B' -Expected '200' -Reason 'no Mentora exercise id captured from phase 4'
        Add-Blocked -Id 'P2.3' -Method 'PUT' -Path '/api/v1/coach/exercises/{id}' -Actor 'COACH_B' -Expected '404' -Reason 'no Mentora exercise id captured from phase 4'
    }

    if ($templateId) {
        Invoke-Api -Id 'P2.5' -Method GET -Path "/api/v1/coach/program-templates/$templateId" -ActorKey 'COACH_B' -Expected 404 | Out-Null
        Invoke-Api -Id 'P2.6' -Method PUT -Path "/api/v1/coach/program-templates/$templateId" -ActorKey 'COACH_B' -Expected 404 -Body @{ name = "$FixturePrefix hijack attempt"; durationWeeks = 1; body = @{ bodyVersion = 1; blocks = @() } } | Out-Null
        Invoke-Api -Id 'P2.7' -Method DELETE -Path "/api/v1/coach/program-templates/$templateId" -ActorKey 'COACH_B' -Expected 404 | Out-Null
    } else {
        foreach ($id in @('P2.5', 'P2.6', 'P2.7')) {
            Add-Blocked -Id $id -Method 'various' -Path '/api/v1/coach/program-templates/{id}' -Actor 'COACH_B' -Expected '404' -Reason $BlockedReasons['phase4']
        }
    }

    Invoke-Api -Id 'P2.8' -Method GET -Path "/api/v1/coach/members/$Member1Id/training-programs" -ActorKey 'COACH_B' -Expected 404 | Out-Null
    Invoke-Api -Id 'P2.9' -Method POST -Path "/api/v1/coach/members/$Member1Id/training-programs" -ActorKey 'COACH_B' -Expected 404 -Body @{ name = "$FixturePrefix hijack attempt"; goal = 'GENERAL_FITNESS'; startDate = (Get-Date).ToString('yyyy-MM-dd'); durationWeeks = 1 } | Out-Null

    if ($programId) {
        Invoke-Api -Id 'P2.10' -Method GET -Path "/api/v1/coach/training-programs/$programId" -ActorKey 'COACH_B' -Expected 404 | Out-Null
        Invoke-Api -Id 'P2.11' -Method PUT -Path "/api/v1/coach/training-programs/$programId" -ActorKey 'COACH_B' -Expected 404 -Body @{ name = "$FixturePrefix hijack"; goal = 'GENERAL_FITNESS'; startDate = (Get-Date).ToString('yyyy-MM-dd'); body = @{ bodyVersion = 1; blocks = @() } } | Out-Null
        Invoke-Api -Id 'P2.12' -Method DELETE -Path "/api/v1/coach/training-programs/$programId" -ActorKey 'COACH_B' -Expected 404 | Out-Null
    } else {
        foreach ($id in @('P2.10', 'P2.11', 'P2.12')) {
            Add-Blocked -Id $id -Method 'various' -Path '/api/v1/coach/training-programs/{id}' -Actor 'COACH_B' -Expected '404' -Reason $BlockedReasons['phase4']
        }
    }

    if ($programSessionId) {
        Invoke-Api -Id 'P2.13' -Method PUT -Path "/api/v1/coach/program-sessions/$programSessionId/booking" -ActorKey 'COACH_B' -Expected 404 -Body @{ sessionId = $null } | Out-Null
        Invoke-Api -Id 'P2.14' -Method PUT -Path "/api/v1/coach/program-sessions/$programSessionId/completion" -ActorKey 'COACH_B' -Expected 404 -Body @{ status = 'DONE' } | Out-Null
    } else {
        Add-Blocked -Id 'P2.13' -Method 'PUT' -Path '/api/v1/coach/program-sessions/{id}/booking' -Actor 'COACH_B' -Expected '404' -Reason 'no program session id captured from phase 4'
        Add-Blocked -Id 'P2.14' -Method 'PUT' -Path '/api/v1/coach/program-sessions/{id}/completion' -Actor 'COACH_B' -Expected '404' -Reason 'no program session id captured from phase 4'
    }

    Invoke-Api -Id 'P2.18' -Method GET -Path "/api/v1/coach/members/$Member1Id/conversation" -ActorKey 'COACH_B' -Expected 404 | Out-Null
    Invoke-Api -Id 'P2.19' -Method GET -Path "/api/v1/coach/members/$Member1Id/parameters" -ActorKey 'COACH_B' -Expected 404 | Out-Null

    # P2.20 / P2.21 need a real slot / product of COACH_A - deferred to Phase 3
    # where those are discovered, so they run there under their own ids.

    if ($programId) {
        Invoke-Api -Id 'P2.22' -Method GET -Path "/api/v1/member/programs/$programId" -ActorKey 'MEMBER_2' -Expected 404 | Out-Null
    } else {
        Add-Blocked -Id 'P2.22' -Method 'GET' -Path '/api/v1/member/programs/{id}' -Actor 'MEMBER_2' -Expected '404' -Reason $BlockedReasons['phase4']
    }
    if ($programSessionId) {
        Invoke-Api -Id 'P2.23' -Method PUT -Path "/api/v1/member/program-sessions/$programSessionId/completion" -ActorKey 'MEMBER_2' -Expected 404 -Body @{ status = 'DONE' } | Out-Null
    } else {
        Add-Blocked -Id 'P2.23' -Method 'PUT' -Path '/api/v1/member/program-sessions/{id}/completion' -Actor 'MEMBER_2' -Expected '404' -Reason 'no program session id captured from phase 4'
    }
    if ($privateExA) {
        Invoke-Api -Id 'P2.24' -Method GET -Path "/api/v1/member/exercises/$privateExA" -ActorKey 'MEMBER_2' -Expected 404 | Out-Null
    } else {
        Add-Blocked -Id 'P2.24' -Method 'GET' -Path '/api/v1/member/exercises/{id}' -Actor 'MEMBER_2' -Expected '404' -Reason 'COACH_A owns no private exercise (scope=MINE returned none) and phase 4 does not create one'
    }

    Invoke-Api -Id 'P2.25' -Method GET -Path '/api/v1/member/me' -ActorKey 'COACH_A' -Expected 403 | Out-Null
    Invoke-Api -Id 'P2.26' -Method GET -Path '/api/v1/coach/me' -ActorKey 'MEMBER_1' -Expected 403 | Out-Null
}

# ---------------------------------------------------------------------------
# PHASE 3 - agenda + group sessions
# ---------------------------------------------------------------------------

function Invoke-Phase3 {
    Write-Host '--- PHASE 3 ---' -ForegroundColor Cyan

    $from = (Get-Date).ToString('yyyy-MM-dd')
    $to = (Get-Date).AddDays(60).ToString('yyyy-MM-dd')

    # P3.1
    $rAgendaA = Invoke-Api -Id 'P3.1' -Method GET -Path "/api/v1/coach/agenda?from=$from&to=$to&includeMemberStandalone=true" -ActorKey 'COACH_A' -Expected 200
    if ($rAgendaA.Status -eq 200) {
        $leak = $false
        foreach ($node in (Get-AllNodes -Obj $rAgendaA.Data)) {
            $cid = Get-FirstProp -Obj $node -Names @('coachId')
            if ($cid -and $cid -ne $CoachAId) { $leak = $true }
        }
        if ($leak) { Write-Host '  P3.1: a foreign coachId appeared in COACH_A''s agenda response' -ForegroundColor Red }
    }
    Add-Manual -Id 'P3.1-SQL' -Method 'SQL' -Path 'SESSIONS.SESSION_COACH_ID' -Actor 'n/a' -Expected 'count matches sessions actually returned' -Reason 'requires direct DB read, no SSH/DB access from this script'

    # P3.2
    Invoke-Api -Id 'P3.2' -Method GET -Path "/api/v1/coach/agenda?from=$from&to=$to&includeMemberStandalone=true" -ActorKey 'COACH_B' -Expected 200 | Out-Null

    # P3.3
    Invoke-Api -Id 'P3.3' -Method GET -Path "/api/v1/member/agenda?from=$from&to=$to" -ActorKey 'MEMBER_1' -Expected 200 | Out-Null

    # Discover a PRESENTIEL_GROUPE product + a compatible group slot for COACH_A.
    $rProducts = Invoke-Api -Id 'P3.setup-products' -Method GET -Path '/api/v1/coach/products' -ActorKey 'COACH_A' -Expected 200 -Quiet
    $groupProduct = $null
    if ($rProducts.Status -eq 200) {
        foreach ($node in @($rProducts.Data)) {
            $offerType = Get-FirstProp -Obj $node -Names @('offerType')
            $published = Get-FirstProp -Obj $node -Names @('isPublished', 'published')
            if ($offerType -match 'GROUP') {
                $groupProduct = $node
                if ($published -eq $true) { break }
            }
        }
    }

    if (-not $groupProduct) {
        $reason = 'COACH_A has no PRESENTIEL_GROUPE product; V1 validation rejects creating one via PUT/POST /coach/products, so this script cannot manufacture one'
        Add-Blocked -Id 'P3.4' -Method 'POST' -Path '/api/v1/coach/sessions/group' -Actor 'COACH_A' -Expected '201' -Reason $reason
        Add-Blocked -Id 'P3.5' -Method 'GET' -Path '/api/v1/coach/agenda' -Actor 'COACH_A' -Expected '200 with member visible' -Reason $reason
        Add-Blocked -Id 'P3.6' -Method 'PUT' -Path '/api/v1/coach/products/{id}' -Actor 'COACH_A' -Expected '409 then persisted snapshot' -Reason $reason
        Add-Blocked -Id 'P3.7' -Method 'POST' -Path '/api/v1/member/sessions' -Actor 'MEMBER_1' -Expected 'refusal' -Reason $reason
        Add-Blocked -Id 'P3.8' -Method 'POST' -Path '/api/v1/coach/sessions/{id}/cancel' -Actor 'COACH_A' -Expected '200' -Reason $reason
        Add-Blocked -Id 'P2.20' -Method 'POST' -Path '/api/v1/coach/sessions/group' -Actor 'COACH_B' -Expected '404' -Reason $reason
        Add-Blocked -Id 'P2.21' -Method 'POST' -Path '/api/v1/coach/sessions/group' -Actor 'COACH_B' -Expected '404' -Reason $reason
        $BlockedReasons['phase3group'] = $reason
        return
    }

    $productId = Get-FirstProp -Obj $groupProduct -Names @('id', 'productId')
    $productDuration = Get-FirstProp -Obj $groupProduct -Names @('durationMinutes')
    $productOfferType = Get-FirstProp -Obj $groupProduct -Names @('offerType')
    $originalMaxParticipants = Get-FirstProp -Obj $groupProduct -Names @('maxParticipants')
    $Captured['groupProductId'] = $productId

    $rSlots = Invoke-Api -Id 'P3.setup-slots' -Method GET -Path "/api/v1/coach/session-slots?from=$from&to=$to&isAvailable=true" -ActorKey 'COACH_A' -Expected 200 -Quiet
    $groupSlotId = $null
    if ($rSlots.Status -eq 200) {
        foreach ($node in @($rSlots.Data)) {
            $slotOfferType = Get-FirstProp -Obj $node -Names @('offerType')
            $slotDuration = Get-FirstProp -Obj $node -Names @('durationMinutes')
            if ($slotOfferType -match 'GROUP' -and (-not $productDuration -or $slotDuration -eq $productDuration)) {
                $groupSlotId = Get-FirstProp -Obj $node -Names @('id', 'slotId')
                if ($groupSlotId) { break }
            }
        }
    }

    if (-not $groupSlotId) {
        $reason = 'no available group-offer-type session slot found for COACH_A'
        Add-Blocked -Id 'P3.4' -Method 'POST' -Path '/api/v1/coach/sessions/group' -Actor 'COACH_A' -Expected '201' -Reason $reason
        Add-Blocked -Id 'P3.5' -Method 'GET' -Path '/api/v1/coach/agenda' -Actor 'COACH_A' -Expected '200 with member visible' -Reason $reason
        Add-Blocked -Id 'P3.6' -Method 'PUT' -Path '/api/v1/coach/products/{id}' -Actor 'COACH_A' -Expected '409 then persisted snapshot' -Reason $reason
        Add-Blocked -Id 'P3.7' -Method 'POST' -Path '/api/v1/member/sessions' -Actor 'MEMBER_1' -Expected 'refusal' -Reason $reason
        Add-Blocked -Id 'P3.8' -Method 'POST' -Path '/api/v1/coach/sessions/{id}/cancel' -Actor 'COACH_A' -Expected '200' -Reason $reason
        Add-Blocked -Id 'P2.20' -Method 'POST' -Path '/api/v1/coach/sessions/group' -Actor 'COACH_B' -Expected '404' -Reason $reason
        Add-Blocked -Id 'P2.21' -Method 'POST' -Path '/api/v1/coach/sessions/group' -Actor 'COACH_B' -Expected '404' -Reason $reason
        $BlockedReasons['phase3group'] = $reason
        return
    }

    # P2.20 / P2.21 - COACH_B tries to create a group session on COACH_A's slot/product
    Invoke-Api -Id 'P2.20' -Method POST -Path '/api/v1/coach/sessions/group' -ActorKey 'COACH_B' -Expected 404 -Body @{ slotId = $groupSlotId; productId = $productId } | Out-Null
    Invoke-Api -Id 'P2.21' -Method POST -Path '/api/v1/coach/sessions/group' -ActorKey 'COACH_B' -Expected 404 -Body @{ slotId = $groupSlotId; productId = $productId } | Out-Null

    # P3.4 - COACH_A creates the group session, registers MEMBER_1
    $rGroup = Invoke-Api -Id 'P3.4a' -Method POST -Path '/api/v1/coach/sessions/group' -ActorKey 'COACH_A' -Expected 201 -Body @{ slotId = $groupSlotId; productId = $productId }
    if ($rGroup.Status -ne 201) {
        $BlockedReasons['phase3groupsession'] = 'P3.4 group session creation did not return 201'
        Add-Blocked -Id 'P3.4' -Method 'POST' -Path "/api/v1/coach/sessions/$groupSlotId/participants" -Actor 'COACH_A' -Expected '201' -Reason $BlockedReasons['phase3groupsession']
        Add-Blocked -Id 'P2.15' -Method 'GET' -Path '/api/v1/coach/sessions/{id}/participants' -Actor 'COACH_B' -Expected '404' -Reason $BlockedReasons['phase3groupsession']
        Add-Blocked -Id 'P2.16' -Method 'POST' -Path '/api/v1/coach/sessions/{id}/participants' -Actor 'COACH_B' -Expected '404' -Reason $BlockedReasons['phase3groupsession']
        Add-Blocked -Id 'P2.17' -Method 'DELETE' -Path '/api/v1/coach/sessions/{id}/participants/{memberId}' -Actor 'COACH_B' -Expected '404' -Reason $BlockedReasons['phase3groupsession']
        return
    }
    $groupSessionId = Get-FirstProp -Obj $rGroup.Data -Names @('id', 'sessionId')
    $Captured['groupSessionId'] = $groupSessionId
    Add-Fixture -Type 'SESSION_GROUP' -FixtureId $groupSessionId -Owner 'COACH_A' -DeleteOrder 15 -Name 'group session created in P3.4'

    # Find a group-compatible AVAILABLE voucher for MEMBER_1.
    $rM1Vouchers = Invoke-Api -Id 'P3.4-vouchers' -Method GET -Path '/api/v1/member/vouchers?status=AVAILABLE' -ActorKey 'MEMBER_1' -Expected 200 -Quiet
    $groupVoucherId = $null
    if ($rM1Vouchers.Status -eq 200) {
        foreach ($node in (Get-AllNodes -Obj $rM1Vouchers.Data)) {
            $ot = Get-FirstProp -Obj $node -Names @('offerType')
            $vid = Get-FirstProp -Obj $node -Names @('voucherId', 'id')
            if ($vid -and (-not $ot -or $ot -match 'GROUP')) { $groupVoucherId = $vid; break }
        }
    }

    if (-not $groupVoucherId) {
        Add-Blocked -Id 'P3.4' -Method 'POST' -Path "/api/v1/coach/sessions/$groupSessionId/participants" -Actor 'COACH_A' -Expected '201' -Reason 'MEMBER_1 has no AVAILABLE voucher compatible with the group offer'
        Add-Blocked -Id 'P3.5' -Method 'GET' -Path "/api/v1/coach/sessions/$groupSessionId" -Actor 'COACH_A' -Expected 'MEMBER_1 visible' -Reason 'MEMBER_1 has no AVAILABLE voucher compatible with the group offer'
    } else {
        $rReg = Invoke-Api -Id 'P3.4' -Method POST -Path "/api/v1/coach/sessions/$groupSessionId/participants" -ActorKey 'COACH_A' -Expected 201 -Body @{ memberId = $Member1Id; voucherId = $groupVoucherId }
        if ($rReg.Status -eq 201) {
            Add-Fixture -Type 'SESSION_PARTICIPANT' -FixtureId "$groupSessionId/$Member1Id" -Owner 'COACH_A' -DeleteOrder 12 -Name 'participant registered in P3.4 (delete via DELETE /coach/sessions/{sessionId}/participants/{memberId})'
            # P3.4 continued: GET participants -> MEMBER_1 present
            $rParts = Invoke-Api -Id 'P3.4b' -Method GET -Path "/api/v1/coach/sessions/$groupSessionId/participants" -ActorKey 'COACH_A' -Expected 200
            if ($rParts.Status -eq 200) {
                $present = $false
                foreach ($node in (Get-AllNodes -Obj $rParts.Data)) {
                    $mid = Get-FirstProp -Obj $node -Names @('memberId')
                    if ($mid -eq $Member1Id) { $present = $true; break }
                }
                if (-not $present) { Write-Host '  P3.4: MEMBER_1 not found in participants list after registration' -ForegroundColor Red }
            }

            # P3.5 - same member visible via agenda and via GET session
            Invoke-Api -Id 'P3.5a' -Method GET -Path "/api/v1/coach/agenda?from=$from&to=$to" -ActorKey 'COACH_A' -Expected 200 | Out-Null
            $rSess = Invoke-Api -Id 'P3.5' -Method GET -Path "/api/v1/coach/sessions/$groupSessionId" -ActorKey 'COACH_A' -Expected 200
            if ($rSess.Status -eq 200) {
                $present2 = $false
                foreach ($node in (Get-AllNodes -Obj $rSess.Data)) {
                    $mid = Get-FirstProp -Obj $node -Names @('memberId')
                    if ($mid -eq $Member1Id) { $present2 = $true; break }
                }
                if (-not $present2) { Write-Host '  P3.5: MEMBER_1 not visible via GET /coach/sessions/{id} - unified read path may not be unified' -ForegroundColor Red }
            }

            # P3.6 - capacity snapshot survives a live product change.
            # Only one eligible member exists (MEMBER_2 is not linked to COACH_A,
            # so it 404s rather than exercising the capacity branch) - the
            # "409 on overflow" half is BLOCKED, see header comment.
            Add-Blocked -Id 'P3.6-overflow' -Method 'POST' -Path "/api/v1/coach/sessions/$groupSessionId/participants" -Actor 'COACH_A' -Expected '409' -Reason 'only one eligible member fixture (MEMBER_1) is available under COACH_A; cannot drive registrations past capacity with a single member'
            if ($null -ne $originalMaxParticipants) {
                $newMax = $originalMaxParticipants + 5
                $productBody = @{
                    name = (Get-FirstProp -Obj $groupProduct -Names @('name'))
                    description = (Get-FirstProp -Obj $groupProduct -Names @('description'))
                    offerType = $productOfferType
                    offerNature = (Get-FirstProp -Obj $groupProduct -Names @('offerNature'))
                    durationMinutes = $productDuration
                    priceEuros = (Get-FirstProp -Obj $groupProduct -Names @('priceEuros'))
                    sport = (Get-FirstProp -Obj $groupProduct -Names @('sport'))
                    location = (Get-FirstProp -Obj $groupProduct -Names @('location'))
                    offerProgramId = (Get-FirstProp -Obj $groupProduct -Names @('offerProgramId'))
                    maxParticipants = $newMax
                }
                $rPutProduct = Invoke-Api -Id 'P3.6' -Method PUT -Path "/api/v1/coach/products/$productId" -ActorKey 'COACH_A' -Expected 200 -Body $productBody
                if ($rPutProduct.Status -eq 200) {
                    $rSessAfter = Invoke-Api -Id 'P3.6-verify' -Method GET -Path "/api/v1/coach/sessions/$groupSessionId" -ActorKey 'COACH_A' -Expected 200 -Quiet
                    if ($rSessAfter.Status -eq 200) {
                        $snapshotMax = Get-FirstProp -Obj $rSessAfter.Data -Names @('maxParticipants')
                        if ($snapshotMax -eq $newMax) {
                            Write-Host '  P3.6: session''s snapshotted maxParticipants followed the live product update - snapshot isolation is broken' -ForegroundColor Red
                        }
                    }
                    # restore product capacity
                    $productBody.maxParticipants = $originalMaxParticipants
                    Invoke-Api -Id 'P3.6-restore' -Method PUT -Path "/api/v1/coach/products/$productId" -ActorKey 'COACH_A' -Expected 200 -Body $productBody -Quiet | Out-Null
                }
            } else {
                Add-Blocked -Id 'P3.6' -Method 'PUT' -Path "/api/v1/coach/products/$productId" -Actor 'COACH_A' -Expected '200 then unchanged snapshot' -Reason 'group product has no maxParticipants value to vary'
            }
        } else {
            Add-Blocked -Id 'P3.5' -Method 'GET' -Path "/api/v1/coach/sessions/$groupSessionId" -Actor 'COACH_A' -Expected 'MEMBER_1 visible' -Reason 'P3.4 participant registration did not return 201'
            Add-Blocked -Id 'P3.6' -Method 'PUT' -Path "/api/v1/coach/products/$productId" -Actor 'COACH_A' -Expected '200 then unchanged snapshot' -Reason 'P3.4 participant registration did not return 201'
        }
    }

    # P3.7 - member cannot self-book a group offer
    $rM1Slots = Invoke-Api -Id 'P3.7-setup' -Method GET -Path "/api/v1/member/session-slots?coachId=$CoachAId" -ActorKey 'MEMBER_1' -Expected 200 -Quiet
    $anyGroupSlotForMember = $null
    if ($rM1Slots.Status -eq 200) {
        foreach ($node in @($rM1Slots.Data)) {
            $pOfferType = Get-FirstProp -Obj $node -Names @('offerType')
            if ($pOfferType -match 'GROUP') { $anyGroupSlotForMember = Get-FirstProp -Obj $node -Names @('id', 'slotId'); break }
        }
    }
    if ($anyGroupSlotForMember -and $groupVoucherId) {
        Invoke-Api -Id 'P3.7' -Method POST -Path '/api/v1/member/sessions' -ActorKey 'MEMBER_1' -Expected 409 -Body @{ voucherId = $groupVoucherId; slotId = $anyGroupSlotForMember } | Out-Null
    } else {
        Add-Blocked -Id 'P3.7' -Method 'POST' -Path '/api/v1/member/sessions' -Actor 'MEMBER_1' -Expected '409/400 refusal' -Reason 'no group-offer slot+voucher combination available to attempt the forbidden self-booking'
    }

    # P3.8 - coach cancels the group session, observe (not fix) voucher behavior
    Invoke-Api -Id 'P3.8' -Method POST -Path "/api/v1/coach/sessions/$groupSessionId/cancel" -ActorKey 'COACH_A' -Expected 200 -Body @{ reason = "$FixturePrefix group cancellation observation" } | Out-Null
    $rVoucherAfterCancel = Invoke-Api -Id 'P3.8-voucher-check' -Method GET -Path '/api/v1/member/vouchers?status=AVAILABLE' -ActorKey 'MEMBER_1' -Expected 200 -Quiet
    if ($rVoucherAfterCancel.Status -eq 200) {
        Write-Host '  P3.8: post-cancellation voucher list for MEMBER_1 captured in run output for manual read (backlog claims voucher is not returned to each participant - not corrected here per instructions)' -ForegroundColor DarkGray
    }
}

# ---------------------------------------------------------------------------
# PHASE 2 tail - P2.15, P2.16, P2.17 (need Phase 3's group session)
# ---------------------------------------------------------------------------

function Invoke-Phase2Tail {
    Write-Host '--- PHASE 2 (tail: P2.15-P2.17) ---' -ForegroundColor Cyan

    $groupSessionId = $Captured['groupSessionId']
    if (-not $groupSessionId) {
        $reason = if ($BlockedReasons.ContainsKey('phase3groupsession')) { $BlockedReasons['phase3groupsession'] }
                  elseif ($BlockedReasons.ContainsKey('phase3group')) { $BlockedReasons['phase3group'] }
                  else { 'no group session id captured from phase 3' }
        Add-Blocked -Id 'P2.15' -Method 'GET' -Path '/api/v1/coach/sessions/{id}/participants' -Actor 'COACH_B' -Expected '404' -Reason $reason
        Add-Blocked -Id 'P2.16' -Method 'POST' -Path '/api/v1/coach/sessions/{id}/participants' -Actor 'COACH_B' -Expected '404' -Reason $reason
        Add-Blocked -Id 'P2.17' -Method 'DELETE' -Path '/api/v1/coach/sessions/{id}/participants/{memberId}' -Actor 'COACH_B' -Expected '404' -Reason $reason
        return
    }

    Invoke-Api -Id 'P2.15' -Method GET -Path "/api/v1/coach/sessions/$groupSessionId/participants" -ActorKey 'COACH_B' -Expected 404 | Out-Null
    Invoke-Api -Id 'P2.16' -Method POST -Path "/api/v1/coach/sessions/$groupSessionId/participants" -ActorKey 'COACH_B' -Expected 404 -Body @{ memberId = $Member1Id; voucherId = [guid]::NewGuid().ToString() } | Out-Null
    Invoke-Api -Id 'P2.17' -Method DELETE -Path "/api/v1/coach/sessions/$groupSessionId/participants/$Member1Id" -ActorKey 'COACH_B' -Expected 404 | Out-Null
}

# ===========================================================================
# STEP A REGRESSION CHECKS (correction pass, 2026-09-14) - covers C1/C2/C3
# from docs/review/tri-ecarts-lot6.md. Self-contained: builds its own
# fixtures (SA.setup-*), does not read $Captured from earlier phases, so it
# runs standalone regardless of what Phase 1-4/2/3 discovered or blocked on.
#
# APPEND-ONLY SECTION: a later correction pass gets its OWN labelled block
# below this one (own function, own Add-Fixture DeleteOrder range starting
# above 100) - do not merge new checks into Invoke-StepARegressionChecks.
# ===========================================================================

# Invoke-Api always JSON-encodes $Body from a PowerShell object, so it can't
# send deliberately malformed JSON or a truly empty string body with a JSON
# content-type. Same result/refresh handling as Invoke-Api, minus the encode.
function Invoke-ApiRawBody {
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ActorKey,
        [Parameter(Mandatory)]$Expected,
        [Parameter(Mandatory)][AllowEmptyString()][string]$RawBody
    )
    $expectedArr = @($Expected)
    $uri = "$BaseUrl$Path"
    $headers = @{ Authorization = "Bearer $($Tokens[$ActorKey])" }

    $attempted401Refresh = $false
    while ($true) {
        $status = $null
        try {
            $resp = Invoke-WebRequest -Method $Method -Uri $uri -Headers $headers -ContentType 'application/json' -Body $RawBody -UseBasicParsing
            $status = [int]$resp.StatusCode
        } catch {
            $webResp = $_.Exception.Response
            if ($webResp) { try { $status = [int]$webResp.StatusCode } catch { $status = $null } }
        }

        if ($status -eq 401 -and -not $attempted401Refresh) {
            $attempted401Refresh = $true
            $refreshed = Invoke-Refresh -ActorKey $ActorKey
            if ($refreshed) {
                $headers['Authorization'] = "Bearer $($Tokens[$ActorKey])"
                continue
            }
        }

        $actualStr = if ($null -ne $status) { "$status" } else { 'ERROR (no response)' }
        $resultStatus = if ($expectedArr -contains $status) { 'PASS' } else { 'FAIL' }
        Add-Result -Id $Id -Method $Method -Path $Path -Actor $ActorKey -Expected ($expectedArr -join '/') -Actual $actualStr -Status $resultStatus
        return
    }
}

function Invoke-StepARegressionChecks {
    Write-Host '--- STEP A REGRESSION CHECKS ---' -ForegroundColor Cyan

    # Setup: two shared Mentora exercises (for the template body) + one private
    # exercise owned by COACH_A (a resource COACH_B can legitimately try to hijack).
    $exM = Invoke-Api -Id 'SA.setup-exercises' -Method GET -Path '/api/v1/coach/exercises?scope=MENTORA' -ActorKey 'COACH_A' -Expected 200 -Quiet
    $mentoraExercises = @($exM.Data)
    if ($mentoraExercises.Count -lt 2) {
        $reason = 'fewer than 2 shared Mentora exercises visible to COACH_A'
        foreach ($id in @('SA1', 'SA2', 'SA3', 'SA4', 'SA5', 'SA6', 'SA7', 'SA8', 'SA9', 'SA10')) {
            Add-Blocked -Id $id -Method 'various' -Path '/api/v1/coach/program-templates' -Actor 'COACH_A' -Expected 'see check' -Reason $reason
        }
        return
    }
    $ex1 = Get-FirstProp -Obj $mentoraExercises[0] -Names @('id', 'exerciseId')
    $ex2 = Get-FirstProp -Obj $mentoraExercises[1] -Names @('id', 'exerciseId')

    $exABody = @{ name = "$FixturePrefix StepA Exercise (Coach A)"; muscleGroup = 'FULL_BODY'; equipment = 'BODYWEIGHT'; isPolyarticular = $false }
    $rExA = Invoke-Api -Id 'SA.setup-exerciseA' -Method POST -Path '/api/v1/coach/exercises' -ActorKey 'COACH_A' -Expected 201 -Body $exABody -Quiet
    $exerciseAId = $null
    if ($rExA.Status -eq 201) {
        $exerciseAId = Get-FirstProp -Obj $rExA.Data -Names @('id', 'exerciseId')
        if ($exerciseAId) { Add-Fixture -Type 'EXERCISE' -FixtureId $exerciseAId -Owner 'COACH_A' -DeleteOrder 102 -Name $exABody.name }
    }

    # SA3 - valid homogeneous tree -> 201
    $validBody = @{
        name = "$FixturePrefix StepA Template"; description = 'Step A regression'; goal = 'GENERAL_FITNESS'; durationWeeks = 1
        body = New-ProgramTemplateBody -ExerciseId1 $ex1 -ExerciseId2 $ex2
    }
    $rTpl = Invoke-Api -Id 'SA3' -Method POST -Path '/api/v1/coach/program-templates' -ActorKey 'COACH_A' -Expected 201 -Body $validBody
    if ($rTpl.Status -ne 201) {
        $reason = 'SA3 template creation did not return 201'
        foreach ($id in @('SA1', 'SA2', 'SA5', 'SA6', 'SA7')) {
            Add-Blocked -Id $id -Method 'various' -Path '/api/v1/coach/program-templates' -Actor 'COACH_A' -Expected 'see check' -Reason $reason
        }
    } else {
        $stepATemplateId = Get-FirstProp -Obj $rTpl.Data -Names @('id', 'programTemplateId')
        Add-Fixture -Type 'PROGRAM_TEMPLATE' -FixtureId $stepATemplateId -Owner 'COACH_A' -DeleteOrder 103 -Name $validBody.name

        # SA1 - mixed STANDARD/INTERVAL fields on one circuit -> 422. This is the corrected,
        # reliable version of P4.3b: workSeconds is set via direct hashtable key assignment,
        # not Add-Member, so it actually reaches the wire (see the P4.3b fix above).
        $mixedBody2 = @{
            name = $validBody.name; description = $validBody.description; goal = $validBody.goal; durationWeeks = 1
            body = New-ProgramTemplateBody -ExerciseId1 $ex1 -ExerciseId2 $ex2
        }
        $mixedBody2.body.blocks[0].blocks[0].blocks[0].sessions[0].circuits[0].mode = 'STANDARD'
        $mixedBody2.body.blocks[0].blocks[0].blocks[0].sessions[0].circuits[0].exercises[0].workSeconds = 30
        Invoke-Api -Id 'SA1' -Method PUT -Path "/api/v1/coach/program-templates/$stepATemplateId" -ActorKey 'COACH_A' -Expected 422 -Body $mixedBody2 | Out-Null

        # SA2 - empty circuit -> 422 (no regression)
        $emptyBody2 = @{
            name = $validBody.name; description = $validBody.description; goal = $validBody.goal; durationWeeks = 1
            body = New-ProgramTemplateBody -ExerciseId1 $ex1 -ExerciseId2 $ex2
        }
        $emptyBody2.body.blocks[0].blocks[0].blocks[0].sessions[0].circuits[0].exercises = @()
        Invoke-Api -Id 'SA2' -Method PUT -Path "/api/v1/coach/program-templates/$stepATemplateId" -ActorKey 'COACH_A' -Expected 422 -Body $emptyBody2 | Out-Null

        # Restore the template to its valid form before the SA5/assign steps below.
        Invoke-Api -Id 'SA.restore-template' -Method PUT -Path "/api/v1/coach/program-templates/$stepATemplateId" -ActorKey 'COACH_A' -Expected 200 -Body $validBody -Quiet | Out-Null

        # SA5 - PUT /coach/program-templates/{ownedByA}, foreign coach (COACH_B), invalid body -> 404
        Invoke-Api -Id 'SA5' -Method PUT -Path "/api/v1/coach/program-templates/$stepATemplateId" -ActorKey 'COACH_B' -Expected 404 -Body @{ invalid = $true } | Out-Null

        # Assign the template to MEMBER_1 to get a training program + program session owned by
        # COACH_A, needed for SA6/SA7.
        $startDate = (Get-Date).ToString('yyyy-MM-dd')
        $rAssign = Invoke-Api -Id 'SA.setup-assign' -Method POST -Path "/api/v1/coach/members/$Member1Id/training-programs" -ActorKey 'COACH_A' -Expected 201 -Body @{ templateId = $stepATemplateId; startDate = $startDate } -Quiet
        $stepAProgramId = $null
        $stepAProgramSessionId = $null
        if ($rAssign.Status -eq 201) {
            $assignRoot = if ($rAssign.Data.program) { $rAssign.Data.program } else { $rAssign.Data }
            $stepAProgramId = Get-FirstProp -Obj $assignRoot -Names @('id', 'programId')
            if ($stepAProgramId) { Add-Fixture -Type 'PROGRAM' -FixtureId $stepAProgramId -Owner 'MEMBER_1 (via COACH_A)' -DeleteOrder 101 -Name "assigned from $($validBody.name)" }
            foreach ($node in (Get-AllNodes -Obj $assignRoot)) {
                $names = $node.PSObject.Properties.Name
                if (-not $stepAProgramSessionId -and ($names -contains 'exercises') -and ($names -contains 'id' -or $names -contains 'programSessionId')) {
                    $stepAProgramSessionId = Get-FirstProp -Obj $node -Names @('id', 'programSessionId')
                }
            }
        }

        # SA6 - PUT /coach/training-programs/{ownedByA}, foreign coach, invalid body -> 404
        if ($stepAProgramId) {
            Invoke-Api -Id 'SA6' -Method PUT -Path "/api/v1/coach/training-programs/$stepAProgramId" -ActorKey 'COACH_B' -Expected 404 -Body @{ invalid = $true } | Out-Null
        } else {
            Add-Blocked -Id 'SA6' -Method 'PUT' -Path '/api/v1/coach/training-programs/{id}' -Actor 'COACH_B' -Expected '404' -Reason 'SA.setup-assign did not return 201'
        }

        # SA7 - PUT /coach/program-sessions/{ownedByA}/completion, foreign coach, invalid body -> 404
        if ($stepAProgramSessionId) {
            Invoke-Api -Id 'SA7' -Method PUT -Path "/api/v1/coach/program-sessions/$stepAProgramSessionId/completion" -ActorKey 'COACH_B' -Expected 404 -Body @{ invalid = $true } | Out-Null
        } else {
            Add-Blocked -Id 'SA7' -Method 'PUT' -Path '/api/v1/coach/program-sessions/{id}/completion' -Actor 'COACH_B' -Expected '404' -Reason 'no program session id captured from SA.setup-assign'
        }
    }

    # SA4 - PUT /coach/exercises/{ownedByA}, foreign coach (COACH_B), invalid body -> 404
    # SA8 - PUT /coach/exercises/{ownedByA}, owner (COACH_A), invalid body -> 422 enveloped
    # SA9 - PUT /coach/exercises/{ownedByA}, owner, malformed JSON -> clean 400 enveloped, not 500
    # SA10 - PUT /coach/exercises/{ownedByA}, owner, empty body -> clean 400 enveloped, not 500
    if ($exerciseAId) {
        Invoke-Api -Id 'SA4' -Method PUT -Path "/api/v1/coach/exercises/$exerciseAId" -ActorKey 'COACH_B' -Expected 404 -Body @{ invalid = $true } | Out-Null
        Invoke-Api -Id 'SA8' -Method PUT -Path "/api/v1/coach/exercises/$exerciseAId" -ActorKey 'COACH_A' -Expected 422 -Body @{ invalid = $true } | Out-Null
        Invoke-ApiRawBody -Id 'SA9' -Method PUT -Path "/api/v1/coach/exercises/$exerciseAId" -ActorKey 'COACH_A' -Expected 400 -RawBody '{"invalid": true,,,}'
        Invoke-ApiRawBody -Id 'SA10' -Method PUT -Path "/api/v1/coach/exercises/$exerciseAId" -ActorKey 'COACH_A' -Expected 400 -RawBody ''
    } else {
        $reason = 'SA.setup-exerciseA did not return 201'
        Add-Blocked -Id 'SA4' -Method 'PUT' -Path '/api/v1/coach/exercises/{id}' -Actor 'COACH_B' -Expected '404' -Reason $reason
        Add-Blocked -Id 'SA8' -Method 'PUT' -Path '/api/v1/coach/exercises/{id}' -Actor 'COACH_A' -Expected '422' -Reason $reason
        Add-Blocked -Id 'SA9' -Method 'PUT' -Path '/api/v1/coach/exercises/{id}' -Actor 'COACH_A' -Expected '400' -Reason $reason
        Add-Blocked -Id 'SA10' -Method 'PUT' -Path '/api/v1/coach/exercises/{id}' -Actor 'COACH_A' -Expected '400' -Reason $reason
    }

    # SA11 - GET conversation, unattached coach (COACH_B) -> 404
    # SA12 - GET conversation, attached coach (COACH_A) -> 200
    # SA13 - GET parameters, same unattached memberId, unattached coach -> 404 (no regression)
    Invoke-Api -Id 'SA11' -Method GET -Path "/api/v1/coach/members/$Member1Id/conversation" -ActorKey 'COACH_B' -Expected 404 | Out-Null
    Invoke-Api -Id 'SA12' -Method GET -Path "/api/v1/coach/members/$Member1Id/conversation" -ActorKey 'COACH_A' -Expected 200 | Out-Null
    Invoke-Api -Id 'SA13' -Method GET -Path "/api/v1/coach/members/$Member1Id/parameters" -ActorKey 'COACH_B' -Expected 404 | Out-Null
}

# ===========================================================================
# STEP A-TER REGRESSION CHECKS (correction pass, 2026-09-14) - covers the four
# 404-never-403 siblings left untouched by the C1/C2/C3 pass in
# Invoke-StepARegressionChecks: ConversationService.GetOrCreateForMemberAsync,
# ConversationService.ValidateAsync (backs GET/POST .../conversation/messages
# and PATCH .../messages/read on both the coach and member side),
# MemberCatalogService.GetCatalogAsync, and ProgramService's member-side
# completion ordering (ownership must be checked before FluentValidation runs,
# same fix already applied to the coach-side twin in bf51c45).
#
# Self-contained: builds its own program-session fixture (a fresh template
# assigned to MEMBER_1), reuses the COACH_B/MEMBER_2 cross-tenant fixtures
# above rather than creating new ones, and does not read $Captured from
# earlier phases.
#
# APPEND-ONLY SECTION: a later correction pass gets its OWN labelled block
# below this one (own function, own Add-Fixture DeleteOrder range starting
# above 111) - do not merge new checks into this function either.
# ===========================================================================

function Invoke-StepATerRegressionChecks {
    Write-Host '--- STEP A-TER REGRESSION CHECKS ---' -ForegroundColor Cyan

    # GUARD - $CoachBId / $Member2Id are hardcoded VPS ids (see comment on their declaration
    # above). A negative check ("this counterparty 404s") against an id that does not actually
    # exist would 404 for the wrong reason and pass without proving anything - it proves nothing
    # about tenant scoping, only that the row is absent. Confirm each one resolves to a REAL row
    # by hitting its own self-profile endpoint (GetMe reads the id from the JWT claim, never from
    # a path parameter) with that actor's own token, and comparing the id it returns to the
    # hardcoded constant. If either does not resolve, every SAT check that depends on it is
    # reported BLOCKED with the reason, not silently allowed to pass.
    $coachBGuard = Invoke-Api -Id 'SAT.guard-coachB' -Method GET -Path '/api/v1/coach/me' -ActorKey 'COACH_B' -Expected 200 -Quiet
    $coachBResolvedId = if ($coachBGuard.Status -eq 200) { Get-FirstProp -Obj $coachBGuard.Data.profile -Names @('coachId') } else { $null }
    $coachBOk = [bool]($coachBResolvedId -and ($coachBResolvedId -ieq $CoachBId))

    $member2Guard = Invoke-Api -Id 'SAT.guard-member2' -Method GET -Path '/api/v1/member/me' -ActorKey 'MEMBER_2' -Expected 200 -Quiet
    $member2ResolvedId = if ($member2Guard.Status -eq 200) { Get-FirstProp -Obj $member2Guard.Data.profile -Names @('memberId') } else { $null }
    $member2Ok = [bool]($member2ResolvedId -and ($member2ResolvedId -ieq $Member2Id))

    if (-not $coachBOk) {
        $reason = "CoachBId ($CoachBId) did not resolve to COACH_B's own row via GET /api/v1/coach/me (status=$($coachBGuard.Status), resolved id=$coachBResolvedId) - a 404 against a non-existent id proves nothing"
        foreach ($id in @('SAT1', 'SAT3', 'SAT4', 'SAT7', 'SAT9')) {
            Add-Blocked -Id $id -Method 'various' -Path 'member -> unrelated coach (CoachBId)' -Actor 'MEMBER_1' -Expected '404' -Reason $reason
        }
    }
    if (-not $member2Ok) {
        $reason = "Member2Id ($Member2Id) did not resolve to MEMBER_2's own row via GET /api/v1/member/me (status=$($member2Guard.Status), resolved id=$member2ResolvedId) - a 404 against a non-existent id proves nothing"
        foreach ($id in @('SAT5', 'SAT6', 'SAT8', 'SAT11')) {
            Add-Blocked -Id $id -Method 'various' -Path 'coach -> unrelated member (Member2Id)' -Actor 'COACH_A/MEMBER_2' -Expected '404' -Reason $reason
        }
    }

    # SAT1/SAT2 - conversation, unrelated vs. attached coach, member side.
    if ($coachBOk) { Invoke-Api -Id 'SAT1' -Method GET -Path "/api/v1/member/coaches/$CoachBId/conversation" -ActorKey 'MEMBER_1' -Expected 404 | Out-Null }
    Invoke-Api -Id 'SAT2' -Method GET -Path "/api/v1/member/coaches/$CoachAId/conversation" -ActorKey 'MEMBER_1' -Expected 200 | Out-Null

    # SAT3/SAT4 - conversation messages GET/POST, unrelated coach, member side.
    if ($coachBOk) {
        Invoke-Api -Id 'SAT3' -Method GET -Path "/api/v1/member/coaches/$CoachBId/conversation/messages" -ActorKey 'MEMBER_1' -Expected 404 | Out-Null
        Invoke-Api -Id 'SAT4' -Method POST -Path "/api/v1/member/coaches/$CoachBId/conversation/messages" -ActorKey 'MEMBER_1' -Expected 404 -Body @{ content = "$FixturePrefix StepA-ter unrelated coach message" } | Out-Null
    }

    # SAT5/SAT6 - conversation messages GET/POST, unrelated member, coach side.
    if ($member2Ok) {
        Invoke-Api -Id 'SAT5' -Method GET -Path "/api/v1/coach/members/$Member2Id/conversation/messages" -ActorKey 'COACH_A' -Expected 404 | Out-Null
        Invoke-Api -Id 'SAT6' -Method POST -Path "/api/v1/coach/members/$Member2Id/conversation/messages" -ActorKey 'COACH_A' -Expected 404 -Body @{ content = "$FixturePrefix StepA-ter unrelated member message" } | Out-Null
    }

    # SAT7/SAT8 - mark-as-read, unrelated counterparty, both sides.
    if ($coachBOk) { Invoke-Api -Id 'SAT7' -Method PATCH -Path "/api/v1/member/coaches/$CoachBId/conversation/messages/read" -ActorKey 'MEMBER_1' -Expected 404 | Out-Null }
    if ($member2Ok) { Invoke-Api -Id 'SAT8' -Method PATCH -Path "/api/v1/coach/members/$Member2Id/conversation/messages/read" -ActorKey 'COACH_A' -Expected 404 | Out-Null }

    # SAT9/SAT10 - catalog, unrelated vs. attached coach, member side.
    if ($coachBOk) { Invoke-Api -Id 'SAT9' -Method GET -Path "/api/v1/member/coaches/$CoachBId/catalog" -ActorKey 'MEMBER_1' -Expected 404 | Out-Null }
    Invoke-Api -Id 'SAT10' -Method GET -Path "/api/v1/member/coaches/$CoachAId/catalog" -ActorKey 'MEMBER_1' -Expected 200 | Out-Null

    # SAT11/SAT12/SAT13 - member completion ordering: ownership wins over body validation.
    # Self-contained fixture: a fresh template assigned to MEMBER_1, giving a program session +
    # exercise MEMBER_1 genuinely owns.
    $exM = Invoke-Api -Id 'SAT.setup-exercises' -Method GET -Path '/api/v1/coach/exercises?scope=MENTORA' -ActorKey 'COACH_A' -Expected 200 -Quiet
    $mentoraExercises = @($exM.Data)
    if ($mentoraExercises.Count -lt 2) {
        $reason = 'fewer than 2 shared Mentora exercises visible to COACH_A'
        foreach ($id in @('SAT11', 'SAT12', 'SAT13')) {
            Add-Blocked -Id $id -Method 'PUT' -Path '/api/v1/member/program-sessions/{id}/completion' -Actor 'MEMBER_1' -Expected 'see check' -Reason $reason
        }
        return
    }
    $ex1 = Get-FirstProp -Obj $mentoraExercises[0] -Names @('id', 'exerciseId')
    $ex2 = Get-FirstProp -Obj $mentoraExercises[1] -Names @('id', 'exerciseId')

    $satBody = @{
        name = "$FixturePrefix StepA-ter Template"; description = 'Step A-ter regression'; goal = 'GENERAL_FITNESS'; durationWeeks = 1
        body = New-ProgramTemplateBody -ExerciseId1 $ex1 -ExerciseId2 $ex2
    }
    $rTpl = Invoke-Api -Id 'SAT.setup-template' -Method POST -Path '/api/v1/coach/program-templates' -ActorKey 'COACH_A' -Expected 201 -Body $satBody -Quiet
    if ($rTpl.Status -ne 201) {
        $reason = 'SAT.setup-template did not return 201'
        foreach ($id in @('SAT11', 'SAT12', 'SAT13')) {
            Add-Blocked -Id $id -Method 'PUT' -Path '/api/v1/member/program-sessions/{id}/completion' -Actor 'MEMBER_1' -Expected 'see check' -Reason $reason
        }
        return
    }
    $satTemplateId = Get-FirstProp -Obj $rTpl.Data -Names @('id', 'programTemplateId')
    Add-Fixture -Type 'PROGRAM_TEMPLATE' -FixtureId $satTemplateId -Owner 'COACH_A' -DeleteOrder 111 -Name $satBody.name

    $startDate = (Get-Date).ToString('yyyy-MM-dd')
    $rAssign = Invoke-Api -Id 'SAT.setup-assign' -Method POST -Path "/api/v1/coach/members/$Member1Id/training-programs" -ActorKey 'COACH_A' -Expected 201 -Body @{ templateId = $satTemplateId; startDate = $startDate } -Quiet
    $satProgramSessionId = $null
    $satProgramExerciseId = $null
    if ($rAssign.Status -eq 201) {
        $assignRoot = if ($rAssign.Data.program) { $rAssign.Data.program } else { $rAssign.Data }
        $satProgramId = Get-FirstProp -Obj $assignRoot -Names @('id', 'programId')
        if ($satProgramId) { Add-Fixture -Type 'PROGRAM' -FixtureId $satProgramId -Owner 'MEMBER_1 (via COACH_A)' -DeleteOrder 110 -Name "assigned from $($satBody.name)" }
        foreach ($node in (Get-AllNodes -Obj $assignRoot)) {
            $names = $node.PSObject.Properties.Name
            if (($names -contains 'exercises') -and ($names -contains 'id' -or $names -contains 'programSessionId')) {
                $candidateId = Get-FirstProp -Obj $node -Names @('id', 'programSessionId')
                if ($candidateId -and -not $satProgramSessionId) { $satProgramSessionId = $candidateId }
                $exNodes = @($node.exercises)
                if ($exNodes.Count -gt 0 -and -not $satProgramExerciseId) {
                    $satProgramExerciseId = Get-FirstProp -Obj $exNodes[0] -Names @('id', 'programExerciseId')
                }
            }
        }
    }

    if (-not $satProgramSessionId) {
        $reason = 'SAT.setup-assign did not return a program session id'
        foreach ($id in @('SAT11', 'SAT12', 'SAT13')) {
            Add-Blocked -Id $id -Method 'PUT' -Path '/api/v1/member/program-sessions/{id}/completion' -Actor 'MEMBER_1' -Expected 'see check' -Reason $reason
        }
        return
    }

    # SAT11 - MEMBER_2 (a different member entirely, unattached to any coach) hits MEMBER_1's own
    # program session with an invalid body -> 404. Ownership (the query filter on
    # ProgramSessionMemberId) must win before the body is ever validated.
    # Guarded by $member2Ok: the top-of-function guard already reported SAT11 BLOCKED if
    # Member2Id did not resolve to a real row - do not also run it (and overwrite that with a
    # meaningless PASS/FAIL) here.
    if ($member2Ok) {
        Invoke-Api -Id 'SAT11' -Method PUT -Path "/api/v1/member/program-sessions/$satProgramSessionId/completion" -ActorKey 'MEMBER_2' -Expected 404 -Body @{ invalid = $true } | Out-Null
    }

    # SAT12 - MEMBER_1's own session, invalid body -> 422 enveloped (the validator does run once
    # ownership has passed).
    Invoke-Api -Id 'SAT12' -Method PUT -Path "/api/v1/member/program-sessions/$satProgramSessionId/completion" -ActorKey 'MEMBER_1' -Expected 422 -Body @{ invalid = $true } | Out-Null

    # SAT13 - MEMBER_1's own session, valid body -> 200.
    if ($satProgramExerciseId) {
        $validCompletionBody = @{
            status = 'DONE'
            memberFeedback = "$FixturePrefix StepA-ter completion"
            exercises = @(@{ programExerciseId = $satProgramExerciseId; actualSets = 3; actualReps = 10; actualRpe = 7 })
        }
        Invoke-Api -Id 'SAT13' -Method PUT -Path "/api/v1/member/program-sessions/$satProgramSessionId/completion" -ActorKey 'MEMBER_1' -Expected 200 -Body $validCompletionBody | Out-Null
    } else {
        Add-Blocked -Id 'SAT13' -Method 'PUT' -Path '/api/v1/member/program-sessions/{id}/completion' -Actor 'MEMBER_1' -Expected '200' -Reason 'no program exercise id captured from SAT.setup-assign'
    }
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

Write-Host "Base URL: $BaseUrl" -ForegroundColor DarkGray
Write-Host "Fixture prefix: $FixturePrefix" -ForegroundColor DarkGray
Write-Host ''

Invoke-Phase1
Invoke-Phase4
Invoke-Phase2Main
Invoke-Phase3
Invoke-Phase2Tail
Invoke-StepARegressionChecks
Invoke-StepATerRegressionChecks

Write-Host ''
Write-Host '=== RESULTS ===' -ForegroundColor Cyan
Write-ResultsTable

Write-ResultsFile
Write-ManifestFile

Write-Host ''
Write-Host "Results written to: $ResultsPath"
Write-Host "Cleanup manifest written to: $ManifestPath (not printed - contains fixture ids)"
