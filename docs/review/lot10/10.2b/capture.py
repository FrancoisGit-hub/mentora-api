"""Lot 10.2b: captures the exercise list responses (member + coach), every page, into <out-dir>/<name>.json.

usage: python capture.py <tokens.json> <out-dir>
Run once on the code BEFORE the change (baseline/) and once after (after/). Tokens are read from a
file outside the repo and are never printed or written. The saved bodies contain no token.
"""
import json, os, subprocess, sys, urllib.parse, urllib.request, urllib.error

TOKENS, OUT = sys.argv[1], sys.argv[2]
BASE = "http://localhost:5243"
tok = json.load(open(TOKENS, encoding="utf-8-sig"))
os.makedirs(OUT, exist_ok=True)


def sql(q):
    r = subprocess.run(["docker", "exec", "mentora-postgres-local", "psql", "-U", "mentora_user", "-d", "mentora_db",
                        "-At", "-c", q], capture_output=True, text=True, encoding="utf-8")
    return r.stdout.strip()


def get(path, token):
    req = urllib.request.Request(BASE + path, headers={"Authorization": "Bearer " + token})
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.status, json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        body = e.read().decode()
        try:
            return e.code, json.loads(body)
        except ValueError:
            return e.code, body


def qs(**kw):
    return "&".join(f"{k}={urllib.parse.quote(str(v), safe='')}" for k, v in kw.items() if v is not None)


def member_pages(name, **params):
    pages, cursor = [], None
    while True:
        p = dict(params)
        if cursor:
            p["cursor"] = cursor
        status, body = get("/api/v1/member/exercises?" + qs(**p), tok["memberAccessToken"])
        pages.append({"status": status, "body": body})
        if status != 200 or not body["data"]["nextCursor"] or len(pages) > 200:
            break
        cursor = body["data"]["nextCursor"]
    json.dump({"request": params, "pages": pages}, open(os.path.join(OUT, name + ".json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    print(name, [p["status"] for p in pages][:3], "pages:", len(pages))


def coach_list(name, **params):
    status, body = get("/api/v1/coach/exercises?" + qs(**params), tok["coachAAccessToken"])
    json.dump({"request": params, "pages": [{"status": status, "body": body}]},
              open(os.path.join(OUT, name + ".json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(name, status)


halteres = sql("select \"EQUIPMENT_ID\" from \"EQUIPMENT\" where \"EQUIPMENT_NAME\"='Haltères'")

member_pages("m_nosearch", limit=50)
member_pages("m_nosearch_limit7", limit=7)
member_pages("m_dev", search="dév", limit=50)
member_pages("m_dev_ascii", search="dev", limit=50)
member_pages("m_DEV_upper", search="DÉV", limit=50)
member_pages("m_dev_limit2", search="dév", limit=2)
member_pages("m_dev_halteres", search="dév", equipmentId=halteres, limit=50)
member_pages("m_nomatch", search="zzzzqq", limit=50)
member_pages("m_blank", search="   ", limit=50)
member_pages("m_badcursor_nosearch", cursor="%%%", limit=50)
member_pages("m_badcursor_search", search="dév", cursor="%%%", limit=50)
member_pages("m_notjson_cursor_search", search="dév", cursor="e30=", limit=50)  # base64 of {} : valid JSON, wrong shape

coach_list("c_nosearch")
coach_list("c_dev", search="dév")
coach_list("c_dev_ascii", search="dev")
coach_list("c_DEV_upper", search="DÉV")
coach_list("c_dev_halteres", search="dév", equipmentId=halteres)
coach_list("c_nomatch", search="zzzzqq")
coach_list("c_blank", search="   ")
