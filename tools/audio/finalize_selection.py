# ============================================================================
# finalize_selection.py
# PURPOSE:
#   Summarize the existing twelve-candidate evaluation with its recorded selection rationale. Retain the distinction between model descriptions and human listening.
# ARCHITECTURAL ROLE:
#   Offline audio tooling utility; outside Unity runtime and editor assemblies.
# KEY RESPONSIBILITIES:
#   Checks completed outputs and input hashes, then writes selection reports.
# DEPENDENCIES:
#   Python standard library, moss_paths, and completed analysis results.
# USAGE NOTES:
#   No Unity lifecycle or asset writes. See README.md for GPU admission and
#   fixed-output overwrite behavior; preserve prior evidence before rerunning.
# ============================================================================
"""Summarize completed local-model evidence without claiming a human audition."""
import hashlib
import json
from pathlib import Path

from moss_paths import ROOT, RESULT as OUT
data = json.loads((OUT / "inference-results.json").read_text(encoding="utf-8"))
by_id = {row["id"]: row for row in data["results"]}
assert len(by_id) == 12
for row in by_id.values():
    assert row["parsed_output"] is not None
    assert row["output_tokens"] < data["provenance"]["max_new_tokens"]
    assert hashlib.sha256(Path(row["absolute_path"]).read_bytes()).hexdigest() == row["sha256"]

decisions = [
    ("hunter attack warning", "windup-a", "Primary", "A compact0.433s guttural burst with a measured16ms onset. MOSS describes it as forceful, raspy and threatening. Its short duration makes it a stronger attack-anticipation candidate than the2.27s sustained throat sound.", "Play at windup start. Calibrate loudness against player footsteps; this is not a completed mix."),
    ("hunter attack warning variation", "windup-b", "Alternate", "A0.590s resonant low growl with18ms onset. MOSS also judges it threatening. Reserve as a second enemy variation rather than stack both on every attack.", "Keep the same gameplay warning timing as the primary."),
    ("hunter presence growl", "growl-c", "Primary", "MOSS distinguishes a wet, raspy guttural texture. At2.295s it can provide occasional nearby-creature presence without being mistaken for the brief windup cue.", "Use intermittently and spatially; do not loop a single growl continuously."),
    ("physical hit", "impact-b", "Provisional", "MOSS assigns physical_impact and hears an abrupt metallic transient. This fits a hard collision better than the alternative punchbag sample, which the model interpreted as a warning click.", "Its RMS is-34.8dBFS, so level matching is required. Perceived body/weight remains unauditioned."),
    ("UI confirmation", "ui-b", "Provisional", "The0.284s sample is the shortest evaluated UI sound. MOSS hears an abrupt mechanical click and labels it attack_warning; our UI use is an integration judgment based on that texture and brevity, not agreement with the model's role label.", "Keep it quiet and dry. Do not reuse the same sound for an actual enemy attack indicator."),
    ("cake pickup", "pickup-a", "Provisional", "MOSS hears a clean isolated metallic confirmation. The0.403s sound is usable for reward feedback, but its-14.1dBFS RMS is much louder than the warning sample and risks dominating frequent Cake chains.", "Start about10dB lower (linear gain0.32), then verify in-game. A quieter80–150ms custom tick would better serve dense repeated collection."),
]
selections = []
for role, identity, status, reason, integration in decisions:
    row = by_id[identity]
    selections.append({"role": role, "candidate_id": identity, "status": status,
        "absolute_path": row["absolute_path"], "relative_path": row["relative_path"], "sha256": row["sha256"],
        "duration_seconds": row["duration_seconds"], "model_best_role": row["parsed_output"]["best_role"],
        "model_horror_fit": row["parsed_output"]["horror_fit"], "selection_reason": reason,
        "integration_notes": integration, "human_listened": False})
summary = {"method": "Actual offline local MOSS-Audio4B-Instruct inference plus objective WAV measurements and explicit integration judgment.",
    "model_revision": data["provenance"]["model_revision"], "source_commit": data["provenance"]["source_commit"],
    "evaluated": len(by_id), "all_outputs_complete_json": True,
    "total_generation_seconds": sum(row["seconds"] for row in by_id.values()),
    "peak_allocated_gib": max(row["peak_allocated_bytes"] for row in by_id.values()) / 1024**3,
    "peak_reserved_gib": max(row["peak_reserved_bytes"] for row in by_id.values()) / 1024**3,
    "gpu_unloaded": True, "audio_uploaded": False, "game_assets_modified": False, "human_listened": False,
    "selections": selections,
    "avoid_for_repeated_pickups": {"candidate_id": "pickup-b", "path": by_id["pickup-b"]["absolute_path"],
        "reason": "MOSS described a piercing metallic screech and assigned attack_warning. It is also1.294s long, making frequent Cake collection overly busy."},
    "authoring_gaps_in_evaluated_sample": [
        "A restrained seamless20–30s dark room ambience bed, keeping warning/footstep transients readable.",
        "Optional soft80–150ms pickup tick for frequent collections if the louder imported confirmation remains intrusive."],
    "limits": [
        "This is a prospective12-file sample, not a full-library comparison or a human listening test.",
        "The model produced similar descriptions across several unrelated one-shots; exact source attribution and role labels remain uncertain.",
        "Repeated-use risk cannot be established from a single isolated clip. The model's uniformly low repetition-risk field is retained raw but not accepted as validation.",
        "These are cue selections, not evidence of the final mix or in-game warning intelligibility."]}
(OUT / "selection.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
lines = ["# Local MOSS-Audio selection", "", "Twelve imported WAVs were analyzed locally with the official MOSS-Audio4B-Instruct model. No audio was uploaded and no game asset was changed. This is model-assisted selection, not a human audition.", "", "| Use | Candidate | Duration | Decision |", "|---|---|---:|---|"]
for selected in selections:
    lines.append(f"| {selected['role']} | `{selected['candidate_id']}` | {selected['duration_seconds']:.3f}s | {selected['status']} |")
lines += ["", "## Selected paths and reasoning", ""]
for selected in selections:
    lines += [f"### {selected['role']}", "", f"`{selected['relative_path']}`", "", selected["selection_reason"], "", selected["integration_notes"], ""]
lines += ["## Authoring handoff", "", "The FL Studio worker received two priorities: a quiet20–30s room-ambience loop, and an optional short muted pickup tick. Monster sounds are already well represented by the evaluated imports. Reject `purchase_success_01.wav` as a frequent Cake cue: the model heard a piercing warning/screech.", "", "## Evidence and limits", "", f"Model revision: `{summary['model_revision']}`. Source commit: `{summary['source_commit']}` (clean checkout). Python3.11.15, PyTorch2.9.1 CUDA12.8, Transformers4.57.1; bf16, batch1, SDPA language attention and eager audio attention. All12 outputs are complete parseable JSON. Total generation {summary['total_generation_seconds']:.2f}s; peak allocated {summary['peak_allocated_gib']:.3f}GiB and peak reserved {summary['peak_reserved_gib']:.3f}GiB. The process exited0 and released GPU memory.", "", "`inference-results.json` retains exact prompts, original audio hashes, processed waveform hashes, outputs and timing. `model-files.json` retains all downloaded hashes; the three weight shards matched Hugging Face's published SHA256 values. `environment-lock.txt` records the isolated runtime.", ""]
lines += ["- " + value for value in summary["limits"]]
lines += ["", "Official sources: [repository and local quickstart](https://github.com/OpenMOSS/MOSS-Audio), [pinned model](https://huggingface.co/OpenMOSS-Team/MOSS-Audio-4B-Instruct/tree/6907a499dc0e87cc77c8ae0fe23fd0eb5476a02d). The repository's original model/processor source was used unchanged; WAV decoding used soundfile followed by the same torchaudio resampler to avoid the optional TorchCodec/FFmpeg loader on Windows."]
(OUT / "SELECTION.md").write_text("\n".join(lines).replace("0.433s", "0.433 s").replace("16ms", "16 ms"), encoding="utf-8")
print(json.dumps({key: summary[key] for key in ["evaluated", "total_generation_seconds", "peak_allocated_gib", "peak_reserved_gib", "gpu_unloaded"]}, indent=2))
