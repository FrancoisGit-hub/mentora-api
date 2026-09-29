"""Lot 9.4 - V10: diff of two OpenAPI documents (baseline vs current). Usage: swagger-diff.py <baseline.json> <current.json>"""
import json
import sys


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def op_params(op):
    return {(p["in"], p["name"]): p for p in op.get("parameters", [])}


def schema_props(doc):
    return {name: s.get("properties", {}) for name, s in doc.get("components", {}).get("schemas", {}).items()}


def show(prop):
    if "$ref" in prop:
        return prop["$ref"].split("/")[-1].split(".")[-1]
    t = prop.get("type", "?")
    if t == "array":
        t = "array<" + show(prop.get("items", {})) + ">"
    if prop.get("nullable"):
        t += "?"
    if "format" in prop:
        t += "(" + prop["format"] + ")"
    return t


base, cur = load(sys.argv[1]), load(sys.argv[2])
bp, cp = base.get("paths", {}), cur.get("paths", {})

print("Added paths:")
for p in sorted(set(cp) - set(bp)):
    for m, op in cp[p].items():
        print(f"  + {m.upper()} {p}  [{', '.join(op.get('tags', []))}]")
print("Removed paths:")
for p in sorted(set(bp) - set(cp)):
    print(f"  - {p}")
if set(bp) - set(cp) == set():
    print("  (none)")

print("Changed operations:")
changed = False
for p in sorted(set(bp) & set(cp)):
    for m in sorted(set(bp[p]) & set(cp[p])):
        a, b = bp[p][m], cp[p][m]
        pa, pb = op_params(a), op_params(b)
        lines = []
        for k in sorted(set(pb) - set(pa)):
            lines.append(f"    + query/param {k[1]} ({show(pb[k].get('schema', {}))})")
        for k in sorted(set(pa) - set(pb)):
            lines.append(f"    - param {k[1]}")
        for k in sorted(set(pa) & set(pb)):
            x, y = pa[k], pb[k]
            if x.get("deprecated") != y.get("deprecated"):
                lines.append(f"    ~ param {k[1]}: deprecated {x.get('deprecated', False)} -> {y.get('deprecated', False)}")
            if x.get("schema") != y.get("schema"):
                lines.append(f"    ~ param {k[1]}: schema {json.dumps(x.get('schema'), ensure_ascii=False)} -> {json.dumps(y.get('schema'), ensure_ascii=False)}")
            if x.get("required") != y.get("required"):
                lines.append(f"    ~ param {k[1]}: required {x.get('required')} -> {y.get('required')}")
        ra, rb = sorted(a.get("responses", {})), sorted(b.get("responses", {}))
        if ra != rb:
            lines.append(f"    ~ responses {ra} -> {rb}")
        if lines:
            changed = True
            print(f"  {m.upper()} {p}")
            print("\n".join(lines))
if not changed:
    print("  (none)")

print("Schemas:")
bs, cs = schema_props(base), schema_props(cur)
short = lambda n: n.split(".")[-1]
for n in sorted(set(cs) - set(bs)):
    print(f"  + schema {short(n)}: " + ", ".join(f"{k}: {show(v)}" for k, v in cs[n].items()))
for n in sorted(set(bs) - set(cs)):
    print(f"  - schema {short(n)}")
for n in sorted(set(bs) & set(cs)):
    a, b = bs[n], cs[n]
    added = [k for k in b if k not in a]
    removed = [k for k in a if k not in b]
    retyped = [k for k in a if k in b and a[k] != b[k]]
    if added or removed or retyped:
        print(f"  ~ schema {short(n)}")
        for k in added:
            print(f"      + {k}: {show(b[k])}")
        for k in removed:
            print(f"      - {k}: {show(a[k])}")
        for k in retyped:
            print(f"      ~ {k}: {show(a[k])} -> {show(b[k])}")
