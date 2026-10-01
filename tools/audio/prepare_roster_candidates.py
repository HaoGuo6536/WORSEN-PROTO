# ============================================================================
# prepare_roster_candidates.py
# PURPOSE:
#   Freeze read-only hunter candidates and verify the installed model offline.
#   Preserve the pre-review selection and source hashes without copying audio.
# ARCHITECTURAL ROLE:
#   Offline audio tooling utility; outside Unity assemblies.
# KEY RESPONSIBILITIES:
#   - Measure whole WAVs, snapshot selection text, and verify model file hashes.
# DEPENDENCIES:
#   Python standard library, NumPy, Soundfile and moss_paths.
# USAGE NOTES:
#   Explicit read-only source checkout; writes only to a new checkout-local result
#   directory. Never downloads, cuts clips, writes vendor files or starts Unity.
# ============================================================================
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import soundfile as sf
from moss_paths import ROOT, MODEL

EXTRA = {
    "step4": "Assets/External/MonstersSFX/PackGhostsAndZombies/Zombie_Footstep_v4/Zombie_FootStep_v4_Wav.wav",
    "step5": "Assets/External/MonstersSFX/PackGhostsAndZombies/Zombie_Footstep_v5/Zombie_FootStep_v5_wav.wav",
    "whisper": "Assets/External/Audio/Horror Elements/Misc/Misc_Whisper.wav",
    "whisper-fear": "Assets/External/Audio/Mangled_Screams_free/Sounds/sb_voxpat_whispering fear.wav",
    "human-female": "Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/scsh_human_female_028.wav",
    "human-breath": "Assets/External/Audio/Horror Elements/Misc/Misc_breath.wav",
    "hate-breath": "Assets/External/Audio/Mangled_Screams_free/Sounds/sb_voxpat_hate breath.wav",
    "wood-creak": "Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/ucns_woodenbed_creaks_04.wav",
    "wind-up": "Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/antiques_camera_super-8_bauer-88b_wind-up_10.wav",
    "pig-control": "Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/ca_pig_grunt_13.wav",
    "orc-control": "Assets/External/MonstersSFX/PackEnemies/Orcs_Attack/Orcs_Attack_v1_wav.wav",
    "goblin-control": "Assets/External/MonstersSFX/PackEnemies/Goblin_Attack/Goblin_Attack_v1_wav.wav",
    "humanoid-breath": "Assets/External/MonstersSFX/PackCinematicMonsters/Primary(HumanoidGruntBreath01).wav",
    "giant-breath": "Assets/External/MonstersSFX/PackMonstersSounds/Monster_Giant_v1/Monster_giant_breath_v1.wav",
    "undead-breath": "Assets/External/MonstersSFX/MonstersUpdateOne/FantasyCreatures/Undead/SFX_Undead_Breath_ReleaseAir_Throat.wav",
    "legacy-growl1": "Assets/Audio/Horror/Expansion/WORSEN_growl_01.wav",
    "legacy-growl2": "Assets/Audio/Horror/Expansion/WORSEN_growl_02.wav",
    "legacy-scream1": "Assets/Audio/Horror/Expansion/WORSEN_scream_01.wav",
    "legacy-scream2": "Assets/Audio/Horror/Expansion/WORSEN_scream_02.wav",
    "legacy-scream3": "Assets/Audio/Horror/Expansion/WORSEN_scream_03.wav",
}


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--results", type=Path, required=True)
    parser.add_argument("--model-manifest", type=Path, required=True)
    parser.add_argument("--only", nargs="*", help="Candidate keys to freeze; omitted freezes the whole catalogue.")
    args = parser.parse_args()
    result = args.results.resolve()
    if not result.is_relative_to(ROOT / "Logs"):
        raise ValueError("Results must be inside this checkout's Logs.")
    result.mkdir(parents=True, exist_ok=False)
    original = (ROOT / "Assets/Audio/HunterRoster/SELECTION.md").read_text(encoding="utf-8")
    (result / "selection-before.md").write_text(original, encoding="utf-8")
    candidates = {}
    for line in original.splitlines():
        cells = [s.strip() for s in line.split("|")]
        if len(cells) == 8 and cells[1].startswith("clip:"):
            candidates[cells[1][5:]] = cells[2]
    candidates.update(EXTRA)
    if args.only:
        candidates = {key: candidates[key] for key in args.only}
    records, excluded = [], []
    for key, relative in candidates.items():
        path = args.source_root / relative
        if not path.is_file():
            excluded.append({"id": key, "path": relative, "reason": "not installed"})
            continue
        info = sf.info(path)
        if info.duration > 12:
            excluded.append({"id": key, "path": relative, "reason": "whole file exceeds 12-second bound", "seconds": info.duration})
            continue
        data, rate = sf.read(path, dtype="float32", always_2d=True)
        records.append({"id": key, "relative_path": relative, "absolute_path": str(path.resolve()),
                        "sha256": digest(path), "sample_rate": rate, "channels": data.shape[1],
                        "frames": len(data), "duration_seconds": len(data) / rate,
                        "peak_dbfs": float(20 * np.log10(max(1e-12, np.max(np.abs(data))))),
                        "rms_dbfs": float(20 * np.log10(max(1e-12, np.sqrt(np.mean(data.astype(np.float64) ** 2))))),
                        "human_listened": False})
    manifest = json.loads(args.model_manifest.read_text(encoding="utf-8-sig"))
    if manifest["revision"] != "6907a499dc0e87cc77c8ae0fe23fd0eb5476a02d":
        raise ValueError("Unexpected model revision")
    for entry in manifest["files"]:
        path = MODEL / entry["path"]
        if path.stat().st_size != entry["bytes"] or digest(path) != entry["sha256"]:
            raise ValueError("Installed model differs from manifest: " + entry["path"])
    manifest["verified_from"] = str(args.model_manifest.resolve())
    (result / "model-files.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    payload = {"method": "Previous roster plus alternative textures and diagnostic controls. Whole files only; model receives no names or proposed roles.",
               "source_root": str(args.source_root.resolve()), "candidates": records, "excluded": excluded}
    (result / "candidates.json").write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(json.dumps({"candidates": len(records), "excluded": excluded, "verified_model_files": len(manifest["files"]), "results": str(result)}, indent=2))


if __name__ == "__main__":
    main()
