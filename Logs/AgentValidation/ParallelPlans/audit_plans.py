from pathlib import Path
import re, json, collections
from urllib.parse import unquote
root = Path.cwd()
planning = root / "PLANNING"
index = (planning / "index.md").read_text(encoding="utf-8-sig")
errors = []
rows = {}
for line in index.splitlines():
    if re.match(r"^\|\s*(?:PLAN|SPEC)-\d{3}\s*\|", line):
        cols = [p.strip() for p in line.strip().strip("|").split("|")]
        ident = cols[0]
        if ident in rows: errors.append(f"Duplicate registry row: {ident}")
        rows[ident] = cols
docs = sorted(p for p in planning.rglob("*.md") if p.name != "index.md")
records = []
for doc in docs:
    body = doc.read_text(encoding="utf-8-sig")
    fm = re.match(r"^---\r?\n(.*?)\r?\n---", body, re.S)
    if not fm:
        errors.append(f"No metadata: {doc}"); continue
    meta = dict(re.findall(r"^(\w+):\s*(.*)$", fm.group(1), re.M))
    ident = meta.get("id")
    row = rows.get(ident)
    if not row: errors.append(f"Unregistered: {doc}")
    else:
        for key, col in [("type",1),("title",2),("status",3),("created",4),("updated",5)]:
            if meta.get(key) != row[col]: errors.append(f"{ident}: registry {key} differs")
        target = re.search(r"\]\(([^)]+)\)",row[9])
        if not target or (planning / target.group(1)).resolve() != doc.resolve():
            errors.append(f"{ident}: registry path differs")
    terminal = meta.get("status") in ["COMPLETED","CANCELLED","SUPERSEDED"]
    if ("archive" in doc.parts) != terminal: errors.append(f"{ident}: wrong status location")
    if ident and ident.startswith("PLAN-") and int(ident[5:]) >= 2:
        sections = re.findall(r"^## (\d+)\. ",body,re.M)
        if sections != [str(n) for n in range(1,10)]: errors.append(f"{ident}: section sequence {sections}")
        for key in ("owner","specs","source","supersedes","superseded_by","evidence","archived"):
            if key not in meta: errors.append(f"{ident}: missing {key}")
        if len(body.splitlines())>200: errors.append(f"{ident}: exceeds 200 lines")
        if "../../tools/coordination/README.md" not in body: errors.append(f"{ident}: no testing protocol")
    records.append({"id":ident,"path":str(doc.relative_to(root)).replace("\\","/"),"status":meta.get("status"),"lines":len(body.splitlines())})
if set(rows) != {r["id"] for r in records}: errors.append("Registry/document ID set differs")
def strip_fences(s):
    fence=chr(96)*3
    return re.sub(r"(?ms)^"+fence+r"[^\n]*\n.*?^"+fence+r"[^\n]*$", "",s)
def slugs(path):
    s=strip_fences(path.read_text(encoding="utf-8-sig"))
    counts=collections.Counter()
    out=set()
    for heading in re.findall(r"^#{1,6}\s+(.+?)\s*#*\s*$",s,re.M):
        h=re.sub(r"<[^>]+>","",heading).lower()
        h=re.sub(r"[^\w\- ]","",h).replace(" ","-")
        n=counts[h]; counts[h]+=1
        out.add(h if n==0 else f"{h}-{n}")
    out.update(re.findall(r'\bid=["\']([^"\']+)["\']',s))
    return out
link_count=0
checked = [planning/"index.md",*docs,root/"AGENTS.md",root/"CLAUDE.md",root/"tools/coordination/README.md"]
for doc in checked:
    body=strip_fences(doc.read_text(encoding="utf-8-sig"))
    for ref in re.findall(r"\[[^\]\n]*\]\(([^)\n]+)\)",body):
        ref=ref.strip("<>")
        if re.match(r"^[a-z]+://",ref,re.I) or ref.startswith("mailto:"): continue
        link_count+=1
        filename, _, fragment=ref.partition("#")
        target=(doc.parent/unquote(filename)).resolve() if filename else doc
        if not target.exists():
            errors.append(f"{doc.relative_to(root)} missing link: {ref}")
        elif fragment and target.suffix.lower()==".md" and unquote(fragment) not in slugs(target):
            errors.append(f"{doc.relative_to(root)} missing anchor: {ref}")
result={"documents":len(docs),"livePlans":sum(r["id"].startswith("PLAN-") and r["status"]=="LIVE" for r in records),"liveSpecs":sum(r["id"].startswith("SPEC-") and r["status"]=="LIVE" for r in records),"archived":sum(r["status"] not in ["LIVE","DRAFT"] for r in records),"checkedLocalLinks":link_count,"errors":errors,"records":records}
out=root/"Logs/AgentValidation/ParallelPlans"
out.mkdir(parents=True,exist_ok=True)
(out/"planning-audit.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
print(json.dumps({k:v for k,v in result.items() if k!="records"},indent=2))
raise SystemExit(bool(errors))
