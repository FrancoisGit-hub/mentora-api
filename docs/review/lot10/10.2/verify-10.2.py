"""Lot 10.2 runtime proof (local only: postgres 5433, API 5243, Development).

usage: python verify-10.2.py <tokens.json> <api-log> <out-dir> [main|h21]
Tokens are read from a file outside the repo and are never printed or written.
"""
import json, os, re, secrets, subprocess, sys, threading, time, urllib.request, urllib.error, uuid

TOKENS, LOG, OUT, MODE = sys.argv[1], sys.argv[2], sys.argv[3], (sys.argv[4] if len(sys.argv) > 4 else "main")
BASE = "http://localhost:5243"
tok = json.load(open(TOKENS, encoding="utf-8-sig"))
COACH_A, COACH_B, MEMBER = tok["coachAAccessToken"], tok["coachBAccessToken"], tok["memberAccessToken"]

report = []
sql_samples = {}
failures = []


def out(line=""):
    report.append(line)
    print(line)


def sql(q):
    r = subprocess.run(["docker", "exec", "mentora-postgres-local", "psql", "-U", "mentora_user", "-d", "mentora_db",
                        "-At", "-F", "|", "-c", q], capture_output=True, text=True)
    if r.returncode != 0:
        raise RuntimeError(r.stderr)
    return r.stdout.strip()


def log_size():
    return os.path.getsize(LOG)


def log_delta(start):
    time.sleep(0.4)
    with open(LOG, "rb") as f:
        f.seek(start)
        text = f.read().decode("utf-8", "replace")
    parts = text.split("info: Microsoft.EntityFrameworkCore.Database.Command")
    return [p.strip() for p in parts[1:] if "Executed DbCommand" in p]


def call(method, path, headers=None, body=None, token=None):
    h = dict(headers or {})
    if token:
        h["Authorization"] = "Bearer " + token
    data = None
    if body is not None:
        data = json.dumps(body).encode()
        h["Content-Type"] = "application/json"
    req = urllib.request.Request(BASE + path, data=data, headers=h, method=method)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            status, text, hdrs = r.status, r.read().decode(), dict(r.headers)
    except urllib.error.HTTPError as e:
        status, text, hdrs = e.code, e.read().decode(), dict(e.headers)
    try:
        js = json.loads(text) if text else None
    except ValueError:
        js = None
    return status, js, hdrs


def step(name, expect, method, path, headers=None, body=None, token=None, extract=None, sample=None):
    start = log_size()
    status, js, hdrs = call(method, path, headers, body, token)
    queries = log_delta(start)
    ok = status == expect
    if sample:
        sql_samples[sample] = queries
    who = "JWT" if token else "no Authorization"
    out(f"- **{name}** `{method} {path}` ({who}{', X-Device-Id=' + headers['X-Device-Id'] if headers and 'X-Device-Id' in headers else ''})"
        f" -> **{status}** (expected {expect}) {'OK' if ok else 'FAIL'}; EF queries: {len(queries)}")
    if extract and js is not None:
        out(f"  - body: `{json.dumps(extract(js), ensure_ascii=False)}`")
    if not ok:
        failures.append(name)
    return status, js, hdrs


def check(name, cond, detail=""):
    out(f"  - check **{name}**: {'OK' if cond else 'FAIL'} {detail}")
    if not cond:
        failures.append(name)


def dev():
    return "l102-" + secrets.token_hex(8)


def dh(d):
    return {"X-Device-Id": d}


def answers_of(session, wrong=0):
    """Correct answers taken from the create response; `wrong` first items answered wrongly."""
    res = []
    for i, q in enumerate(session["questions"]):
        c = q["correctAnswer"]
        if i < wrong:
            c = "A" if c != "A" else "B"
        res.append({"questionId": q["questionId"], "chosenAnswer": c})
    return {"answers": res}


def create(headers=None, token=None):
    s, js, _ = call("POST", "/api/v1/quiz/basic/sessions", headers, None, token)
    assert s == 201, (s, js)
    return js["data"]


def submit(sid, body, headers=None, token=None):
    return call("POST", f"/api/v1/quiz/basic/sessions/{sid}/answers", headers, body, token)


def main_run():
    run_start = open(os.path.join(OUT, "run-start.txt")).read().strip()
    out(f"# Lot 10.2 scenarios - run start {run_start}, executed {time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())}")
    out()
    coach_a = sql("select \"COACH_ID\" from \"COACHES\" c join \"USERS\" u on u.\"USER_ID\"=c.\"USER_ID\" where u.\"USER_EMAIL\"='coach@mentora.fr'")
    coach_b = sql("select \"COACH_ID\" from \"COACHES\" c join \"USERS\" u on u.\"USER_ID\"=c.\"USER_ID\" where u.\"USER_EMAIL\"='l102-coach-b@test.local'")
    out(f"coach A = {coach_a}, coach B = {coach_b}")
    out()

    d1, d2, d3, d4, d6, d7 = dev(), dev(), dev(), dev(), dev(), dev()
    out(f"fixture devices: {d1}, {d2}, {d3}, {d4}, {d6}, {d7}")
    out()

    out("## H1 device mode POST sessions")
    s, js, _ = step("H1", 201, "POST", "/api/v1/quiz/basic/sessions", dh(d1),
                    extract=lambda j: {"sessionId": j["data"]["sessionId"], "expiresAt": j["data"]["expiresAt"],
                                       "questionCount": j["data"]["questionCount"],
                                       "positions": [q["position"] for q in j["data"]["questions"]],
                                       "firstQuestionKeys": [a["key"] for a in j["data"]["questions"][0]["answers"]]},
                    sample="draw")
    h1 = js["data"]
    qs = h1["questions"]
    check("12 questions", len(qs) == 12)
    check("distinct questionIds", len({q["questionId"] for q in qs}) == 12)
    check("positions 1..12 in order", [q["position"] for q in qs] == list(range(1, 13)))
    check("answers A-D in order", all([a["key"] for a in q["answers"]] == list("ABCD") for q in qs))
    row = sql(f"select \"QUIZ_SESSION_STATUS\",\"QUIZ_SESSION_QUESTION_COUNT\",\"QUIZ_SESSION_DEVICE_ID\",coalesce(\"QUIZ_SESSION_COACH_ID\"::text,'NULL'),"
              f"round(extract(epoch from (\"QUIZ_SESSION_EXPIRES_DATE\"-\"QUIZ_SESSION_CREATED_DATE\"))/60,3) "
              f"from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_ID\"='{h1['sessionId']}'")
    out(f"  - SQL session row: `{row}`")
    check("SQL expires - created = 120 min", row.endswith("|120.000"))
    check("SQL device set, coach NULL", row.split("|")[2] == d1 and row.split("|")[3] == "NULL")
    cnt = sql(f"select count(*),count(distinct \"QUIZ_SESSION_QUESTION_QUESTION_ID\"),min(\"QUIZ_SESSION_QUESTION_POSITION\"),max(\"QUIZ_SESSION_QUESTION_POSITION\") "
              f"from \"QUIZ_SESSION_QUESTIONS\" where \"QUIZ_SESSION_QUESTION_SESSION_ID\"='{h1['sessionId']}'")
    out(f"  - SQL session questions (count|distinct|min pos|max pos): `{cnt}`")
    check("SQL 12 distinct, positions 1-12", cnt == "12|12|1|12")

    out("\n## H2 / H3 device id missing or invalid")
    s, js, _ = step("H2", 422, "POST", "/api/v1/quiz/basic/sessions", extract=lambda j: j)
    check("errors key DeviceId", js and "DeviceId" in js.get("errors", {}))
    s, js, _ = step("H3", 422, "POST", "/api/v1/quiz/basic/sessions", dh("abc"), extract=lambda j: j)
    check("errors key DeviceId", js and "DeviceId" in js.get("errors", {}))

    out("\n## H4 garbage bearer / H5 member JWT (status only: 401/403 keep the project's empty-body behaviour)")
    step("H4", 401, "POST", "/api/v1/quiz/basic/sessions", dh(d1), token="garbage.token.value")
    step("H5", 403, "POST", "/api/v1/quiz/basic/sessions", dh(d1), token=MEMBER)

    out("\n## H6 coach A POST sessions")
    s, js, _ = step("H6", 201, "POST", "/api/v1/quiz/basic/sessions", dh(d1), token=COACH_A,
                    extract=lambda j: {"sessionId": j["data"]["sessionId"]})
    a1 = js["data"]
    row = sql(f"select coalesce(\"QUIZ_SESSION_COACH_ID\"::text,'NULL'),coalesce(\"QUIZ_SESSION_DEVICE_ID\",'NULL') from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_ID\"='{a1['sessionId']}'")
    out(f"  - SQL (coach_id|device_id): `{row}` (X-Device-Id sent but must be ignored in coach mode)")
    check("COACH_ID set, DEVICE_ID null", row == f"{coach_a}|NULL")

    out("\n## H7 submit coach A session with the correct answers")
    total_points = sum(q["points"] for q in a1["questions"])
    s, js, _ = step("H7", 200, "POST", f"/api/v1/quiz/basic/sessions/{a1['sessionId']}/answers", None,
                    answers_of(a1), COACH_A,
                    extract=lambda j: {k: j["data"][k] for k in ("score", "correctCount", "questionCount", "totalPoints", "completedAt")},
                    sample="submit")
    d = js["data"]
    check("score = sum of points", d["score"] == total_points, f"({d['score']} vs {total_points})")
    check("correctCount 12", d["correctCount"] == 12)
    check("results ordered by position", [r["position"] for r in d["results"]] == list(range(1, 13)))
    row = sql(f"select \"QUIZ_SESSION_STATUS\",\"QUIZ_SESSION_SCORE\",\"QUIZ_SESSION_CORRECT_COUNT\" from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_ID\"='{a1['sessionId']}'")
    rows = sql(f"select sum(\"QUIZ_SESSION_QUESTION_POINTS_EARNED\"),count(*) filter (where \"QUIZ_SESSION_QUESTION_IS_CORRECT\") from \"QUIZ_SESSION_QUESTIONS\" where \"QUIZ_SESSION_QUESTION_SESSION_ID\"='{a1['sessionId']}'")
    out(f"  - SQL session: `{row}`; SQL question sums (points|correct): `{rows}`")
    check("SQL agrees", row == f"COMPLETED|{d['score']}|12" and rows == f"{d['score']}|12")
    check("totalPoints = SQL owner sum", str(d["totalPoints"]) == sql(f"select sum(\"QUIZ_SESSION_SCORE\") from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_COACH_ID\"='{coach_a}' and \"QUIZ_SESSION_STATUS\"='COMPLETED'"))

    out("\n## H8 resubmit")
    s, js, _ = step("H8", 409, "POST", f"/api/v1/quiz/basic/sessions/{a1['sessionId']}/answers", None, answers_of(a1), COACH_A,
                    extract=lambda j: j)
    check("details.reason ALREADY_SUBMITTED", js["details"]["reason"] == "ALREADY_SUBMITTED")

    out("\n## H9-H12 invalid bodies on a fresh in-progress coach A session")
    a2 = create(token=COACH_A)
    a2sid = a2["sessionId"]
    good = answers_of(a2)
    path = f"/api/v1/quiz/basic/sessions/{a2sid}/answers"
    s, js, _ = step("H9 (11 answers)", 422, "POST", path, None, {"answers": good["answers"][:11]}, COACH_A, extract=lambda j: j.get("errors"))
    bad = json.loads(json.dumps(good)); bad["answers"][3]["chosenAnswer"] = "E"
    s, js, _ = step("H10 (chosenAnswer E)", 422, "POST", path, None, bad, COACH_A, extract=lambda j: j.get("errors"))
    dup = json.loads(json.dumps(good)); dup["answers"][5]["questionId"] = dup["answers"][4]["questionId"]
    s, js, _ = step("H11 (duplicate questionId)", 422, "POST", path, None, dup, COACH_A, extract=lambda j: j.get("errors"))
    foreign = json.loads(json.dumps(good)); foreign["answers"][0]["questionId"] = str(uuid.uuid4())
    s, js, _ = step("H12 (question not drawn)", 422, "POST", path, None, foreign, COACH_A, extract=lambda j: j.get("errors"))
    check("errors key Answers", js and "Answers" in js.get("errors", {}))
    st = sql(f"select \"QUIZ_SESSION_STATUS\" from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_ID\"='{a2sid}'")
    check("session still IN_PROGRESS after the 422s", st == "IN_PROGRESS")

    out("\n## H13-H15 ownership")
    step("H13 coach B submits coach A session", 404, "POST", path, None, good, COACH_B, extract=lambda j: j)
    step("H14 other device submits H1 session", 404, "POST", f"/api/v1/quiz/basic/sessions/{h1['sessionId']}/answers", dh(d2), answers_of(h1), extract=lambda j: j)
    step("H15 unknown session id", 404, "POST", f"/api/v1/quiz/basic/sessions/{uuid.uuid4()}/answers", dh(d2), answers_of(h1), extract=lambda j: j)
    st = sql(f"select \"QUIZ_SESSION_STATUS\" from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_ID\"='{a2sid}'")
    check("coach A session untouched by coach B", st == "IN_PROGRESS")

    out("\n## H16 expired")
    e = create(dh(d3))
    sql(f"update \"QUIZ_SESSIONS\" set \"QUIZ_SESSION_EXPIRES_DATE\" = now() - interval '1 minute' where \"QUIZ_SESSION_ID\"='{e['sessionId']}'")
    s, js, _ = step("H16", 409, "POST", f"/api/v1/quiz/basic/sessions/{e['sessionId']}/answers", dh(d3), answers_of(e), extract=lambda j: j)
    check("details.reason EXPIRED", js["details"]["reason"] == "EXPIRED")

    out("\n## H17 two concurrent submissions")
    c = create(dh(d4))
    body = answers_of(c)
    results = [None, None]
    barrier = threading.Barrier(2)

    def worker(i):
        barrier.wait()
        results[i] = submit(c["sessionId"], body, dh(d4))

    ts = [threading.Thread(target=worker, args=(i,)) for i in range(2)]
    [t.start() for t in ts]; [t.join() for t in ts]
    codes = sorted(r[0] for r in results)
    out(f"- **H17** statuses: {codes}; 409 body: `{json.dumps([r[1] for r in results if r[0] == 409], ensure_ascii=False)}`")
    check("exactly one 200 and one 409", codes == [200, 409])
    check("409 is ALREADY_SUBMITTED", all(r[1]["details"]["reason"] == "ALREADY_SUBMITTED" for r in results if r[0] == 409))
    check("SQL one completed session, score set once",
          sql(f"select count(*),max(\"QUIZ_SESSION_SCORE\") from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_ID\"='{c['sessionId']}' and \"QUIZ_SESSION_STATUS\"='COMPLETED'").startswith("1|"))

    out("\n## H18 GET me")
    # make coach A own 3 completed sessions with different scores
    for wrong in (3, 7):
        x = create(token=COACH_A)
        r, _, _ = submit(x["sessionId"], answers_of(x, wrong), None, COACH_A)
        assert r == 200
    s, js, _ = step("H18 coach A", 200, "GET", "/api/v1/quiz/basic/me", None, token=COACH_A,
                    extract=lambda j: {"totalPoints": j["data"]["totalPoints"], "completedCount": j["data"]["completedCount"],
                                       "items": len(j["data"]["items"]), "nextCursor": j["data"]["nextCursor"]},
                    sample="me")
    sq = sql(f"select coalesce(sum(\"QUIZ_SESSION_SCORE\"),0),count(*) from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_COACH_ID\"='{coach_a}' and \"QUIZ_SESSION_STATUS\"='COMPLETED'")
    out(f"  - SQL (sum|count): `{sq}`")
    check("coach A totals = SQL", f"{js['data']['totalPoints']}|{js['data']['completedCount']}" == sq)
    a_total_before_claim = js["data"]["totalPoints"]
    s, js, _ = step("H18 device d1", 200, "GET", "/api/v1/quiz/basic/me", dh(d1),
                    extract=lambda j: {"totalPoints": j["data"]["totalPoints"], "completedCount": j["data"]["completedCount"]})
    sq = sql(f"select coalesce(sum(\"QUIZ_SESSION_SCORE\"),0),count(*) from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_DEVICE_ID\"='{d1}' and \"QUIZ_SESSION_COACH_ID\" is null and \"QUIZ_SESSION_STATUS\"='COMPLETED'")
    out(f"  - SQL device (sum|count): `{sq}` (d1 only has the in-progress H1 session, so 0|0 is expected)")
    check("device totals = SQL", f"{js['data']['totalPoints']}|{js['data']['completedCount']}" == sq)
    # paginate coach A with limit=1
    seen, cursor, pages = [], None, 0
    while True:
        p = "/api/v1/quiz/basic/me?limit=1" + (f"&cursor={urllib.request.quote(cursor)}" if cursor else "")
        s, j, _ = call("GET", p, None, None, COACH_A)
        assert s == 200, (s, j)
        pages += 1
        seen += [i["sessionId"] for i in j["data"]["items"]]
        cursor = j["data"]["nextCursor"]
        if not cursor or pages > 20:
            break
    expected = sql(f"select \"QUIZ_SESSION_ID\" from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_COACH_ID\"='{coach_a}' and \"QUIZ_SESSION_STATUS\"='COMPLETED' order by \"QUIZ_SESSION_COMPLETED_DATE\" desc,\"QUIZ_SESSION_ID\" desc").split("\n")
    out(f"- pagination limit=1: {pages} pages, ids returned {len(seen)}, SQL expects {len(expected)}")
    check("every session once, in order", seen == expected)
    s, _, _ = step("H18 bad limit", 400, "GET", "/api/v1/quiz/basic/me?limit=51", None, token=COACH_A)
    s, _, _ = step("H18 bad cursor", 400, "GET", "/api/v1/quiz/basic/me?cursor=%25%25", None, token=COACH_A)

    out("\n## H19 claim")
    sess6 = [create(dh(d6)) for _ in range(3)]
    submit(sess6[0]["sessionId"], answers_of(sess6[0]), dh(d6))
    submit(sess6[1]["sessionId"], answers_of(sess6[1], 4), dh(d6))
    sum6 = int(sql(f"select sum(\"QUIZ_SESSION_SCORE\") from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_DEVICE_ID\"='{d6}' and \"QUIZ_SESSION_STATUS\"='COMPLETED'"))
    out(f"- device d6: 2 completed + 1 in progress; SQL completed score sum = {sum6}; coach A total before = {a_total_before_claim}")
    claim_path = "/api/v1/coach/quiz/basic/claim-device"
    s, js, _ = step("H19 claim by coach A", 200, "POST", claim_path, None, {"deviceId": d6}, COACH_A, extract=lambda j: j["data"])
    cd = js["data"]
    check("claimedPoints = SQL sum", cd["claimedPoints"] == sum6)
    check("claimedSessions = 2", cd["claimedSessions"] == 2)
    check("coach A total increased by claimed points", cd["totalPoints"] == a_total_before_claim + sum6)
    out("  - SQL after claim: `" + sql(f"select \"QUIZ_SESSION_STATUS\",coalesce(\"QUIZ_SESSION_COACH_ID\"::text,'NULL')='{coach_a}',\"QUIZ_SESSION_DEVICE_ID\"='{d6}' from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_DEVICE_ID\"='{d6}' order by 1").replace("\n", " ; ")
        + "` (status|coach=A|device kept)")
    out("  - SQL claim row: `" + sql(f"select \"QUIZ_DEVICE_CLAIM_COACH_ID\"='{coach_a}',\"QUIZ_DEVICE_CLAIM_POINTS\" from \"QUIZ_DEVICE_CLAIMS\" where \"QUIZ_DEVICE_CLAIM_DEVICE_ID\"='{d6}'") + "`")
    check("claim row points = claimed points", sql(f"select \"QUIZ_DEVICE_CLAIM_POINTS\" from \"QUIZ_DEVICE_CLAIMS\" where \"QUIZ_DEVICE_CLAIM_DEVICE_ID\"='{d6}'") == str(sum6))
    s, js, _ = step("H19 device d6 GET me after claim", 200, "GET", "/api/v1/quiz/basic/me", dh(d6),
                    extract=lambda j: {"totalPoints": j["data"]["totalPoints"], "completedCount": j["data"]["completedCount"]})
    check("device total 0", js["data"]["totalPoints"] == 0 and js["data"]["completedCount"] == 0)
    s, js, _ = step("H19 retry by coach A (idempotent)", 200, "POST", claim_path, None, {"deviceId": d6}, COACH_A, extract=lambda j: j["data"])
    check("same values", js["data"] == cd)
    check("still one claim row", sql(f"select count(*) from \"QUIZ_DEVICE_CLAIMS\" where \"QUIZ_DEVICE_CLAIM_DEVICE_ID\"='{d6}'") == "1")
    s, js, _ = step("H19 coach B", 409, "POST", claim_path, None, {"deviceId": d6}, COACH_B, extract=lambda j: j)
    check("ALREADY_CLAIMED", js["details"]["reason"] == "ALREADY_CLAIMED")
    s, js, _ = step("H19 fresh device without session", 409, "POST", claim_path, None, {"deviceId": d7}, COACH_A, extract=lambda j: j)
    check("NOTHING_TO_CLAIM", js["details"]["reason"] == "NOTHING_TO_CLAIM")
    check("no claim row for d7", sql(f"select count(*) from \"QUIZ_DEVICE_CLAIMS\" where \"QUIZ_DEVICE_CLAIM_DEVICE_ID\"='{d7}'") == "0")
    step("H19 no JWT", 401, "POST", claim_path, None, {"deviceId": d6})
    step("H19 member JWT", 403, "POST", claim_path, None, {"deviceId": d6}, MEMBER)
    s, js, _ = step("H19 invalid deviceId", 422, "POST", claim_path, None, {"deviceId": "abc"}, COACH_A, extract=lambda j: j.get("errors"))
    check("errors key DeviceId", "DeviceId" in js["errors"])

    out("\n## H20 in-progress session of the claimed device")
    ip = sess6[2]
    step("H20 device mode submit", 404, "POST", f"/api/v1/quiz/basic/sessions/{ip['sessionId']}/answers", dh(d6), answers_of(ip), extract=lambda j: j)
    s, js, _ = step("H20 coach A submit", 200, "POST", f"/api/v1/quiz/basic/sessions/{ip['sessionId']}/answers", None, answers_of(ip), COACH_A,
                    extract=lambda j: {k: j["data"][k] for k in ("score", "correctCount", "totalPoints")})
    check("coach A total = SQL",
          str(js["data"]["totalPoints"]) == sql(f"select sum(\"QUIZ_SESSION_SCORE\") from \"QUIZ_SESSIONS\" where \"QUIZ_SESSION_COACH_ID\"='{coach_a}' and \"QUIZ_SESSION_STATUS\"='COMPLETED'"))

    finish("scenarios-output.md")


def h21_run():
    out(f"# Lot 10.2 H21 rate limit - executed {time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())}")
    out("API restarted with Quiz__AnonymousPermitLimit=5")
    statuses = []
    for i in range(1, 7):
        d = dev()
        s, js, hdrs = call("POST", "/api/v1/quiz/basic/sessions", dh(d))
        statuses.append(s)
        extra = ""
        if s == 429:
            extra = f" body=`{json.dumps(js)}` Retry-After=`{hdrs.get('Retry-After')}`"
        out(f"- anonymous call {i} (device {d}) -> **{s}**{extra}")
    check("5 pass then 429", statuses == [201] * 5 + [429])
    s, js, hdrs = call("POST", "/api/v1/quiz/basic/sessions", dh(dev()))
    out(f"- 7th anonymous call -> {s}")
    check("still limited", s == 429)
    s, js, _ = step("H21 coach call right after", 201, "POST", "/api/v1/quiz/basic/sessions", None, token=COACH_A,
                    extract=lambda j: {"sessionId": j["data"]["sessionId"]})
    check("coach call not 429", s != 429)
    s, js, _ = step("H21 anonymous with X-Forwarded-For 203.0.113.9 from loopback (different partition)", 201, "POST",
                    "/api/v1/quiz/basic/sessions", {"X-Device-Id": dev(), "X-Forwarded-For": "203.0.113.9"},
                    extract=lambda j: {"sessionId": j["data"]["sessionId"]})
    finish("h21-output.md")


def finish(name):
    out()
    out(f"## Result: {'ALL CHECKS OK' if not failures else 'FAILURES: ' + ', '.join(failures)}")
    open(os.path.join(OUT, name), "w", encoding="utf-8").write("\n".join(report) + "\n")
    if sql_samples:
        lines = ["# SQL generated by EF Core (Executed DbCommand entries) per request", ""]
        for key, title in (("draw", "POST /quiz/basic/sessions (H1, device mode)"),
                           ("submit", "POST /quiz/basic/sessions/{id}/answers (H7, coach mode)"),
                           ("me", "GET /quiz/basic/me (H18, coach mode)")):
            qs = sql_samples.get(key, [])
            lines += [f"## {title}: {len(qs)} queries", ""]
            for i, q in enumerate(qs, 1):
                lines += [f"### query {i}", "```sql", re.sub(r"^Executed DbCommand", "-- Executed DbCommand", q, count=1), "```", ""]
        open(os.path.join(OUT, "sql-generated.md"), "w", encoding="utf-8").write("\n".join(lines))


if MODE == "h21":
    h21_run()
else:
    main_run()
sys.exit(1 if failures else 0)
