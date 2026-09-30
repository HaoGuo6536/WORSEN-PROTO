# ============================================================================
# prepare_candidates.py
# PURPOSE:
#   Measure the existing twelve-clip candidate sample without changing source audio. Save a prospective input manifest for the offline model runner.
# ARCHITECTURAL ROLE:
#   Offline audio tooling utility; outside Unity runtime and editor assemblies.
# KEY RESPONSIBILITIES:
#   Reads WAV files and writes the candidate manifest; does not load the model.
# DEPENDENCIES:
#   NumPy, Soundfile, moss_paths, and project audio files.
# USAGE NOTES:
#   No Unity lifecycle or asset writes. See README.md for GPU admission and
#   fixed-output overwrite behavior; preserve prior evidence before rerunning.
# ============================================================================
"""Read-only candidate census; filenames select a sample, not model conclusions."""
import hashlib
import json
from pathlib import Path
import numpy as np
import soundfile as sf

from moss_paths import ROOT, RESULT

RESULT.mkdir(parents=True, exist_ok=True)
OUTPUT = RESULT / "candidates.json"
CANDIDATES = [
    ("windup-a", "attack warning", "Assets/External/MonstersSFX/MonstersUpdateOne/FantasyCreatures/Undead/SFX_Undead_MadGrowl_LowPitchVoice_Short_01.wav"),
    ("windup-b", "attack warning", "Assets/External/MonstersSFX/PackMonsterLowVoices/Growl_Fast_01/SFX-Growl-Fast-01_wav.wav"),
    ("windup-c", "attack warning", "Assets/External/MonstersSFX/PackMonsterLowVoices/Roar_Throat_01/SFX-Monster-Throat-01_wav.wav"),
    ("growl-a", "low monster growl", "Assets/External/MonstersSFX/PackMonsterLowVoices/Growl_Generic_01/SFX-Growl-Generic-01_wav.wav"),
    ("growl-b", "low monster growl", "Assets/External/MonstersSFX/PackMonsterLowVoices/Growl_Generic_01/SFX-Growl-Generic-02_wav.wav"),
    ("growl-c", "low monster growl", "Assets/External/MonstersSFX/MonstersUpdateOne/FantasyCreatures/Undead/SFX_Undead_MadGrowl_LowPitchVoice_Long_01.wav"),
    ("impact-a", "physical impact", "Assets/External/RegularImpactsSFX/ImpactPunchBag01/SFX_impactpunchbag01.wav"),
    ("impact-b", "physical impact", "Assets/External/RegularImpactsSFX/ImpactRaw01/SFX_impactraw01.wav"),
    ("ui-a", "UI confirmation", "Assets/External/CelerisLab/CompleteUISFX/basic_interactions_and_navigation/selection_01.wav"),
    ("ui-b", "UI confirmation", "Assets/External/CelerisLab/CompleteUISFX/basic_interactions_and_navigation/selection_03.wav"),
    ("pickup-a", "cake pickup", "Assets/External/Vefects/Pixel Craft VFX/Audio/WAV/Fer/SFX_Vefects_Item_Pick_Up_01.wav"),
    ("pickup-b", "cake pickup", "Assets/External/CelerisLab/CompleteUISFX/shop_and_economy/purchase_success_01.wav"),
]
records = []
for identity, proposed_role, relative in CANDIDATES:
    path = ROOT / relative
    data, rate = sf.read(path, dtype="float32", always_2d=True)
    mono = data.mean(axis=1)
    peak = float(np.max(np.abs(mono)))
    audible = np.flatnonzero(np.abs(mono) >= max(0.001, peak * 0.05))
    record = {
        "id": identity, "proposed_role_from_filename_only": proposed_role,
        "relative_path": relative, "absolute_path": str(path),
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "sample_rate": rate, "channels": data.shape[1], "frames": data.shape[0],
        "duration_seconds": data.shape[0] / rate,
        "peak_amplitude": peak,
        "rms_dbfs": float(20 * np.log10(max(1e-12, np.sqrt(np.mean(mono.astype(np.float64) ** 2))))),
        "first_5_percent_peak_seconds": float(audible[0] / rate) if audible.size else None,
        "last_5_percent_peak_seconds": float(audible[-1] / rate) if audible.size else None,
        "human_listened": False,
    }
    if record["duration_seconds"] > 12:
        raise RuntimeError(f"Candidate too long for bounded inference: {identity}")
    records.append(record)
    print(f"{identity}: {record['duration_seconds']:.3f}s, {record['rms_dbfs']:.1f}dBFS, onset {record['first_5_percent_peak_seconds']:.3f}s")
OUTPUT.write_text(json.dumps({"method": "Prospective filename-stratified sample; model receives audio and common prompt only, not filenames or proposed roles.", "candidates": records}, indent=2), encoding="utf-8")
