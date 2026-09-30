"""Lot 10.2b runtime proof (S1-S8): compares the captured responses (baseline/ and after/) and the live API with SQL.

usage: python verify-10.2b.py <tokens.json> <api-log> <out-dir>
Read-only: only GET requests and SELECTs. Tokens are read from a file outside the repo, never printed or written.
"""
import json, os, re, subprocess, sys, time, urllib.parse, urllib.request, urllib.error

TOKENS, LOG, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
BASE = "http://localhost:5243"
tok = json.load(open(TOKENS, encoding="utf-8-sig"))
report, failures = [], []

FROM = "àâäÀÂÄéèêëÉÈÊËîïÎÏôöÔÖùûüÙÛÜÿŸçÇ"
TO = "aaaAAAeeeeEEEEiiIIooOOuuuUUUyYcC"


def out(line=""):
    report.append(line)
    print(line)


def check(name, cond, detail=""):
    out(f"- **{name}**: {'OK' if cond else 'FAIL'} {detail}")
    if not cond:
        failures.append(name)


def sql(q):
    r = subprocess.run(["docker", "exec", "-i", "mentora-postgres-local", "psql", "-U", "mentora_user", "-d", "mentora_db",
                        "-At", "-F", "|"], input=q, capture_output=True, text=True, encoding="utf-8")
    if r.returncode != 0:
        raise RuntimeError(r.stderr)
    return [l for l in r.stdout.strip().split("\n") if l]


def load(tag, name):
    return json.load(open(os.path.join(OUT, tag, name + ".json"), encoding="utf-8"))


def items(d):
    return [i for p in d["pages"] for i in (p["body"]["data"]["items"] if isinstance(p["body"]["data"], dict) else p["body"]["data"])]


def ids(d, key="exerciseId"):
    return [i[key] for i in items(d)]


def names(d):
    return [i["name"] for i in items(d)]


def get(path, token):
    req = urllib.request.Request(BASE + path, headers={"Authorization": "Bearer " + token})
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.status, json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read().decode() or "null")


def log_delta(start):
    time.sleep(0.5)
    with open(LOG, "rb") as f:
        f.seek(start)
        text = f.read().decode("utf-8", "replace")
    return [p.strip() for p in text.split("info: Microsoft.EntityFrameworkCore.Database.Command")[1:] if "Executed DbCommand" in p]


def expected_ids(term, member=True, extra_where="", coach_id=None):
    """Independent SQL implementation of the ranking (translate + lower on both sides)."""
    esc = term.strip().replace("\\", "\\\\").replace("%", "\\%").replace("_", "\\_").replace("'", "''")
    fold = lambda c: f"lower(translate({c}, '{FROM}', '{TO}'))"
    t = fold(f"'{esc}'")
    n, en, tags = fold('"EXERCISE_NAME"'), fold('coalesce("EXERCISE_NAME_EN",\'\')'), fold('coalesce("EXERCISE_SEARCH_TAGS",\'\')')
    if member:
        member_id = sql("select m.\"MEMBER_ID\" from \"MEMBERS\" m join \"USERS\" u on u.\"USER_ID\"=m.\"USER_ID\" where u.\"USER_EMAIL\"='member@mentora.fr'")[0]
        vis = (f'"EXERCISE_IS_ACTIVE" AND ("EXERCISE_COACH_ID" IS NULL OR EXISTS (SELECT 1 FROM "MEMBER_COACHES" mc '
               f'WHERE mc."MEMBER_ID"=\'{member_id}\' AND mc."COACH_ID"="EXERCISE_COACH_ID"))')
    else:
        vis = f'"EXERCISE_IS_ACTIVE" AND ("EXERCISE_COACH_ID" IS NULL OR "EXERCISE_COACH_ID"=\'{coach_id}\')'
    q = f"""
    SELECT id, rank FROM (
      SELECT "EXERCISE_ID" AS id, "EXERCISE_NAME" AS nm,
        CASE WHEN {n} LIKE {t} || '%' ESCAPE '\\' THEN 0
             WHEN {n} LIKE '% ' || {t} || '%' ESCAPE '\\' OR {n} LIKE '%-' || {t} || '%' ESCAPE '\\'
               OR {n} LIKE '%''' || {t} || '%' ESCAPE '\\' OR {n} LIKE '%’' || {t} || '%' ESCAPE '\\'
               OR {n} LIKE '%(' || {t} || '%' ESCAPE '\\' THEN 1
             WHEN {n} LIKE '%' || {t} || '%' ESCAPE '\\' THEN 2
             WHEN {en} LIKE '%' || {t} || '%' ESCAPE '\\' OR {tags} LIKE '%' || {t} || '%' ESCAPE '\\' THEN 3
             ELSE 4 END AS rank
      FROM "EXERCISES" WHERE {vis} {extra_where}
    ) x WHERE rank <= 3 ORDER BY rank, nm, id"""
    return [r.split("|") for r in sql(q)]


def main():
    out(f"# Lot 10.2b runtime proof - executed {time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())}")
    out(f"baseline run start: {open(os.path.join(OUT, 'baseline-run-start.txt')).read().strip()}, after run start: {open(os.path.join(OUT, 'after-run-start.txt')).read().strip()}")
    out()

    # ---------------------------------------------------------------- S1
    out("## S1 search=dév (member)")
    base = load("baseline", "m_dev"); after = load("after", "m_dev")
    exp = expected_ids("dév")
    a_ids, a_names = ids(after), names(after)
    ranks = [int(r[1]) for r in exp]
    out(f"- baseline (before the change): {len(ids(base))} results, first = `{names(base)[0]}` (sorted by name, no relevance)")
    out(f"- after: {len(a_ids)} results, first = `{a_names[0]}`; ranks from SQL: {ranks}")
    check("list equals the independent SQL ranking (ids, order)", a_ids == [r[0] for r in exp])
    starts = [i for i, nm in enumerate(a_names) if nm.lower().replace("é", "e").startswith("dev")]
    others = [i for i in range(len(a_names)) if i not in starts]
    check("every name starting with Dév comes before any other result", not others or max(starts) < min(others),
          f"({len(starts)} names start with Dév, positions 0..{max(starts) if starts else '-'})")
    cpm = [i for i, nm in enumerate(a_names) if nm == "Chest press machine"]
    out(f"- `Chest press machine`: position {cpm[0] + 1 if cpm else 'absent'} of {len(a_names)}, rank "
        f"{ranks[cpm[0]] if cpm else '-'}. Its name does not contain dév/dev; the only field that matches is EXERCISE_SEARCH_TAGS "
        f"(`développé machine`), an alternative-name field, hence rank 3 (after every name match).")
    check("Chest press machine is last group (rank 3)", bool(cpm) and ranks[cpm[0]] == 3 and cpm[0] >= len(starts))
    out("- returned order: " + " | ".join(a_names))

    # ---------------------------------------------------------------- S2
    out("\n## S2 dev / DÉV give the same list as dév")
    for n in ("m_dev_ascii", "m_DEV_upper"):
        check(f"{n} identical to m_dev (after)", load("after", n)["pages"] == after["pages"])
    out(f"- for reference the baseline gave {len(ids(load('baseline', 'm_dev_ascii')))} results for `dev` (accent-sensitive ILIKE) and {len(ids(load('baseline', 'm_DEV_upper')))} for `DÉV`")

    # ---------------------------------------------------------------- S3
    out("\n## S3 pagination limit=2")
    l2 = load("after", "m_dev_limit2")
    l2_ids = ids(l2)
    check("concatenated pages == limit=50 list (order, no duplicate, none missing)", l2_ids == a_ids and len(set(l2_ids)) == len(l2_ids),
          f"({len(l2['pages'])} pages)")
    last_cursors = [p["body"]["data"]["nextCursor"] for p in l2["pages"]]
    check("last page has nextCursor null, others not", last_cursors[-1] is None and all(last_cursors[:-1]))
    sample = json.loads(__import__("base64").b64decode(last_cursors[0]).decode())
    out(f"- cursor content (search): `{json.dumps(sample, ensure_ascii=False)}`")

    # ---------------------------------------------------------------- S4
    out("\n## S4 without search: identical to the baseline")
    for n in ("m_nosearch", "m_nosearch_limit7", "m_blank"):
        b, a = load("baseline", n), load("after", n)
        check(f"{n}: responses identical (every page, incl. nextCursor)", a["pages"] == b["pages"], f"({len(a['pages'])} pages, {len(ids(a))} items)")
    b = load("baseline", "m_nosearch")["pages"][0]["body"]["data"]["nextCursor"]
    out(f"- no-search cursor format (unchanged): `{__import__('base64').b64decode(b).decode()}`")

    # ---------------------------------------------------------------- S5
    out("\n## S5 search=dév + equipmentId (Haltères)")
    hal = sql("select \"EQUIPMENT_ID\" from \"EQUIPMENT\" where \"EQUIPMENT_NAME\"='Haltères'")[0]
    a5 = load("after", "m_dev_halteres")
    exp5 = expected_ids("dév", extra_where=f' AND ("EXERCISE_PRIMARY_EQUIPMENT_ID"=\'{hal}\' OR "EXERCISE_SECONDARY_EQUIPMENT_ID"=\'{hal}\')')
    check("equals SQL", ids(a5) == [r[0] for r in exp5], f"({len(ids(a5))} results)")
    b5 = load("baseline", "m_dev_halteres")
    check("same result set as the baseline (only the order may differ)", sorted(ids(a5)) == sorted(ids(b5)))
    out("- order: " + " | ".join(names(a5)))

    # ---------------------------------------------------------------- S6
    out("\n## S6 no match / blank / malformed cursors")
    nm = load("after", "m_nomatch")["pages"][0]["body"]["data"]
    check("no match: empty items and nextCursor null", nm["items"] == [] and nm["nextCursor"] is None)
    for n in ("m_badcursor_nosearch", "m_badcursor_search", "m_notjson_cursor_search"):
        b, a = load("baseline", n)["pages"][0], load("after", n)["pages"][0]
        check(f"{n}: same status and body as before", a == b, f"({a['status']} {json.dumps(a['body'])})")
    old = load("baseline", "m_nosearch")["pages"][0]["body"]["data"]["nextCursor"]
    s, body = get("/api/v1/member/exercises?" + urllib.parse.urlencode({"search": "dév", "cursor": old}), tok["memberAccessToken"])
    check("old-format (name,id) cursor with a search: 400 like a malformed cursor", s == 400 and body["error"] == "Invalid cursor.", f"({s} {json.dumps(body)})")
    for term in ("%%", "de_", "\\\\"):
        s, body = get("/api/v1/member/exercises?" + urllib.parse.urlencode({"search": term}), tok["memberAccessToken"])
        check(f"wildcard characters are literal: search={term!r}", s == 200 and body["data"]["items"] == [], f"({s}, {len(body['data']['items'])} items)")

    # ---------------------------------------------------------------- S7
    out("\n## S7 generated SQL and queries per request (member, search=dév, limit=50)")
    start = os.path.getsize(LOG)
    s, body = get("/api/v1/member/exercises?" + urllib.parse.urlencode({"search": "dév", "limit": 50}), tok["memberAccessToken"])
    qs = log_delta(start)
    out(f"- status {s}, EF queries for this request: **{len(qs)}**")
    lines = ["# SQL generated by EF Core", ""]
    lines += [f"## GET /api/v1/member/exercises?search=dév&limit=50: {len(qs)} query(ies)", ""]
    for i, q in enumerate(qs, 1):
        lines += [f"### query {i}", "```sql", q, "```", ""]
    start = os.path.getsize(LOG)
    s, body = get("/api/v1/coach/exercises?" + urllib.parse.urlencode({"search": "dév"}), tok["coachAAccessToken"])
    qc = log_delta(start)
    out(f"- coach list search=dév: status {s}, EF queries: **{len(qc)}** (split query: main query + objectives collection)")
    lines += [f"## GET /api/v1/coach/exercises?search=dév: {len(qc)} query(ies)", ""]
    for i, q in enumerate(qc, 1):
        lines += [f"### query {i}", "```sql", q, "```", ""]
    start = os.path.getsize(LOG)
    get("/api/v1/member/exercises?limit=50", tok["memberAccessToken"])
    qn = log_delta(start)
    out(f"- member list without search (unchanged path): EF queries: **{len(qn)}**")
    lines += [f"## GET /api/v1/member/exercises?limit=50 (no search): {len(qn)} query(ies)", ""]
    for i, q in enumerate(qn, 1):
        lines += [f"### query {i}", "```sql", q, "```", ""]
    open(os.path.join(OUT, "sql-generated.md"), "w", encoding="utf-8").write("\n".join(lines))
    text = "\n".join(qs)
    check("ranking happens in SQL (CASE ... ORDER BY, translate, LIMIT)", "translate(" in text and "CASE" in text and "ORDER BY" in text and "LIMIT" in text)

    # ---------------------------------------------------------------- S8
    out("\n## S8 coach endpoint (GET /api/v1/coach/exercises, no pagination: S3 not applicable)")
    coach_id = sql("select c.\"COACH_ID\" from \"COACHES\" c join \"USERS\" u on u.\"USER_ID\"=c.\"USER_ID\" where u.\"USER_EMAIL\"='coach@mentora.fr'")[0]
    ca, cb = load("after", "c_dev"), load("baseline", "c_dev")
    exp8 = expected_ids("dév", member=False, coach_id=coach_id)
    check("S1 on coach: list equals the independent SQL ranking", ids(ca) == [r[0] for r in exp8], f"({len(ids(ca))} results; baseline had {len(ids(cb))}, name only)")
    ca_names = names(ca)
    st = [i for i, n in enumerate(ca_names) if n.lower().replace("é", "e").startswith("dev")]
    ot = [i for i in range(len(ca_names)) if i not in st]
    check("every name starting with Dév before any other result", not ot or max(st) < min(ot))
    check("dev and DÉV give the same list as dév", load("after", "c_dev_ascii")["pages"] == ca["pages"] and load("after", "c_DEV_upper")["pages"] == ca["pages"])
    bmap = {i["id"] if "id" in i else i["exerciseId"]: i for i in items(cb)}
    amap = {i["id"] if "id" in i else i["exerciseId"]: i for i in items(ca)}
    same = all(amap.get(k) == v for k, v in bmap.items())
    check("items also returned before are byte-identical (details/Includes intact)", same and set(bmap) <= set(amap))
    check("S4 coach without search identical to the baseline", load("after", "c_nosearch")["pages"] == load("baseline", "c_nosearch")["pages"],
          f"({len(items(load('after', 'c_nosearch')))} items)")
    check("coach blank search identical to the baseline", load("after", "c_blank")["pages"] == load("baseline", "c_blank")["pages"])
    check("coach no match: empty list", items(load("after", "c_nomatch")) == [])
    hal = sql("select \"EQUIPMENT_ID\" from \"EQUIPMENT\" where \"EQUIPMENT_NAME\"='Haltères'")[0]
    exp8b = expected_ids("dév", member=False, coach_id=coach_id,
                         extra_where=f' AND ("EXERCISE_PRIMARY_EQUIPMENT_ID"=\'{hal}\' OR "EXERCISE_SECONDARY_EQUIPMENT_ID"=\'{hal}\')')
    check("coach dév + equipmentId equals SQL", ids(load("after", "c_dev_halteres")) == [r[0] for r in exp8b])
    out("- coach order: " + " | ".join(ca_names))

    out()
    out(f"## Result: {'ALL CHECKS OK' if not failures else 'FAILURES: ' + ', '.join(failures)}")
    open(os.path.join(OUT, "verify-output.md"), "w", encoding="utf-8").write("\n".join(report) + "\n")


main()
sys.exit(1 if failures else 0)
