# ============================================================================
# audit_pass3.py
# PURPOSE:
#   Join pass-3 path-only bindings to completed MOSS evidence and source hashes.
#   Produce local provenance without copying licensed audio or changing Unity.
# ARCHITECTURAL ROLE:
#   Offline audio tooling utility; outside Unity assemblies.
# KEY RESPONSIBILITIES:
#   - Verify run counts, selected source identities and description/fit coverage.
#   - Preserve exact prompts/responses with human_listened explicitly false.
# DEPENDENCIES:
#   Python standard library; completed pass-3 inference logs in this worktree.
# USAGE NOTES:
#   Run with -B. Refuses an existing output; source audio is read-only.
# ============================================================================
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RUNS = ("pass3-describe-001", "pass3-describe-002", "pass3-describe-003",
        "pass3-fit-001", "pass3-fit-002", "pass3-fit-003")


def bindings(path, only_pass3=False):
    clips, cues = {}, []
    for line in path.read_text(encoding="utf-8").splitlines():
        cells = [cell.strip() for cell in line.split("|")]
        if len(cells) != 8:
            continue
        if cells[1].startswith("clip:"):
            clips[cells[1][5:]] = cells[2]
        if cells[1].startswith("cue:"):
            cues.append(cells)
    selected = {}
    for row in cues:
        for key in (row[4] + "," + row[5]).split(","):
            key = key.strip()
            if key in ("silence", "missing", "-") or only_pass3 and not key.startswith("p3-"):
                continue
            selected.setdefault(clips[key], []).append(row[1][4:])
    return selected


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to(ROOT / "Logs") or output.exists():
        raise ValueError("Use a new checkout-local Logs output.")
    selected = bindings(ROOT / "Assets/Audio/HunterRoster/PASS3-BANKS.md")
    for path, cues in bindings(ROOT / "Assets/Audio/HunterRoster/SELECTION.md", True).items():
        selected.setdefault(path, []).extend(cues)
    evidence, counts = {}, {}
    for run in RUNS:
        directory = ROOT / "Logs/AgentValidation/Horror/audio-analysis" / run
        status = json.loads((directory / "inference-status.json").read_text())
        candidates = json.loads((directory / "candidates.json").read_text())["candidates"]
        payload = json.loads((directory / "inference-results.json").read_text())
        results = payload["results"]
        if status["state"] != "completed" or not status["gpu_memory_released"]:
            raise ValueError("Incomplete run: " + run)
        if status["completed"] != len(results) or len(results) != len(candidates) or {r["id"] for r in results} != {c["id"] for c in candidates}:
            raise ValueError("Incomplete or duplicate selection: " + run)
        counts[run] = len(results)
        for row in results:
            path = row["relative_path"]
            if path not in selected:
                continue
            if row["human_listened"] is not False:
                raise ValueError("Model output cannot establish human listening.")
            if hashlib.sha256(Path(row["absolute_path"]).read_bytes()).hexdigest() != row["sha256"]:
                raise ValueError("Selected source drift: " + path)
            evidence.setdefault(path, []).append({"run": run, "sha256": row["sha256"],
                "prompt": row.get("prompt", payload["provenance"]["prompt"]),
                "description_or_fit": row["model_output"], "human_listened": False})
    for path in selected:
        records = evidence.get(path, [])
        if not any("describe" in r["run"] for r in records) or not any("fit" in r["run"] for r in records):
            raise ValueError("Missing description/fit evidence: " + path)
    report = {"status": "source_and_model_evidence_verified_not_Unity_or_human_acceptance",
              "runs": counts, "newly_selected_unique_paths": len(selected), "human_listened": False,
              "selections": [{"path": path, "cues": cues, "evidence": evidence[path]} for path, cues in sorted(selected.items())]}
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(report, stream, indent=2, ensure_ascii=False)
    print(json.dumps({"runs": counts, "unique_paths": len(selected), "output": str(output), "human_listened": False}))


if __name__ == "__main__":
    main()
