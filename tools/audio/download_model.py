# ============================================================================
# download_model.py
# PURPOSE:
#   Fetch and verify the pinned official model files. Reuse matching local files so a project checkout does not need its own model download.
# ARCHITECTURAL ROLE:
#   Offline audio tooling utility; outside Unity runtime and editor assemblies.
# KEY RESPONSIBILITIES:
#   Downloads model data and writes download manifests; no audio upload.
# DEPENDENCIES:
#   Python standard library and moss_paths.
# USAGE NOTES:
#   No Unity lifecycle or asset writes. See README.md for GPU admission and
#   fixed-output overwrite behavior; preserve prior evidence before rerunning.
# ============================================================================
"""Fetch pinned official model files locally; never sends project audio."""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import time
import urllib.request

from moss_paths import MODEL as DEST, RESULT
MODEL = "OpenMOSS-Team/MOSS-Audio-4B-Instruct"
REVISION = "6907a499dc0e87cc77c8ae0fe23fd0eb5476a02d"
DEST.mkdir(parents=True, exist_ok=True)
RESULT.mkdir(parents=True, exist_ok=True)

def get_json(url):
    with urllib.request.urlopen(url, timeout=45) as response:
        return json.load(response)

tree = get_json(f"https://huggingface.co/api/models/{MODEL}/tree/{REVISION}?recursive=true&expand=false")
entries = [entry for entry in tree if entry["type"] == "file" and "/" not in entry["path"]
           and entry["path"].endswith((".json", ".safetensors", ".jinja", ".txt", ".md"))]
(RESULT / "model-tree.json").write_text(json.dumps({"model": MODEL, "revision": REVISION, "files": tree}, indent=2), encoding="utf-8")

def sha256(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for data in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            value.update(data)
    return value.hexdigest()

def download(entry):
    name = entry["path"]
    target = DEST / name
    expected = entry.get("lfs", {}).get("oid")
    if target.exists() and target.stat().st_size == entry["size"]:
        digest = sha256(target)
        if expected is None or digest == expected:
            print(f"READY {name} {target.stat().st_size}", flush=True)
            return {"path": name, "bytes": target.stat().st_size, "sha256": digest}
    partial = target.with_name(target.name + ".partial")
    url = f"https://huggingface.co/{MODEL}/resolve/{REVISION}/{name}?download=true"
    print(f"FETCH {name} {entry['size']}", flush=True)
    for attempt in range(3):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": "WORSEN-local-audio-evaluation/1.0"})
            with urllib.request.urlopen(request, timeout=90) as response, partial.open("wb") as out:
                total = 0
                next_report = 512 * 1024 * 1024
                for data in iter(lambda: response.read(1024 * 1024), b""):
                    out.write(data)
                    total += len(data)
                    if total >= next_report:
                        print(f"PROGRESS {name} {total}/{entry['size']}", flush=True)
                        next_report += 512 * 1024 * 1024
            if partial.stat().st_size != entry["size"]:
                raise RuntimeError(f"Size mismatch: {name}")
            digest = sha256(partial)
            if expected and digest != expected:
                raise RuntimeError(f"SHA256 mismatch: {name}")
            partial.replace(target)
            print(f"READY {name} {total}", flush=True)
            return {"path": name, "bytes": total, "sha256": digest}
        except Exception as error:
            print(f"RETRY {name} {attempt + 1}: {error!r}", flush=True)
            if attempt == 2:
                raise
            time.sleep(2)

with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    files = list(pool.map(download, entries))
(RESULT / "model-files.json").write_text(json.dumps({"model": MODEL, "revision": REVISION, "files": files}, indent=2), encoding="utf-8")
print("DOWNLOAD_COMPLETE", flush=True)
