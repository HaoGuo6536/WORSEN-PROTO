# ============================================================================
# analyze_ambience.py
# PURPOSE:
#   Evaluate a fixed middle excerpt of the authored ambience loop. Keep measurements and model outputs separate from perceptual in-game acceptance.
# ARCHITECTURAL ROLE:
#   Offline audio tooling utility; outside Unity runtime and editor assemblies.
# KEY RESPONSIBILITIES:
#   Creates an excerpt and manifests, then delegates inference to analyze_candidates.
# DEPENDENCIES:
#   NumPy, Soundfile, analyze_candidates, and the authored project WAV.
# USAGE NOTES:
#   No Unity lifecycle or asset writes. See README.md for GPU admission and
#   fixed-output overwrite behavior; preserve prior evidence before rerunning.
# ============================================================================
"""Evaluate a documented middle excerpt of the authored loop; preserve prior results."""
import hashlib
import json
import math
from pathlib import Path
import shutil

import numpy as np
import soundfile as sf
import analyze_candidates as runner

BASE_RESULT = runner.RESULT
SOURCE_AUDIO = runner.ROOT / "Logs/AgentStaging/Horror/audio/WORSEN_RoomPressure.wav"
RESULT = BASE_RESULT / "fl-room-pressure"
RESULT.mkdir(parents=True, exist_ok=True)
data, sample_rate = sf.read(SOURCE_AUDIO, dtype="float32", always_2d=True)
start_seconds = 7.0
duration_seconds = 9.5
start_frame = round(start_seconds * sample_rate)
end_frame = start_frame + round(duration_seconds * sample_rate)
if end_frame > data.shape[0]:
    raise RuntimeError("Authored loop is shorter than the prospectively selected excerpt.")
excerpt = data[start_frame:end_frame]
excerpt_path = RESULT / "WORSEN_RoomPressure_excerpt_7.0-16.5s.wav"
sf.write(excerpt_path, excerpt, sample_rate, subtype="PCM_16")

def db(value):
    return float(20 * np.log10(max(float(value), 1e-12)))

mono = data.mean(axis=1).astype(np.float64)
spectral_power = np.abs(np.fft.rfft(mono)) ** 2
frequencies = np.fft.rfftfreq(mono.size, 1 / sample_rate)
total_power = float(spectral_power.sum())
metrics = {
    "original_path": str(SOURCE_AUDIO),
    "original_sha256": hashlib.sha256(SOURCE_AUDIO.read_bytes()).hexdigest(),
    "frames": int(data.shape[0]), "sample_rate": sample_rate, "channels": int(data.shape[1]),
    "duration_seconds": data.shape[0] / sample_rate,
    "peak_dbfs": db(np.max(np.abs(data))),
    "rms_dbfs": db(np.sqrt(np.mean(data.astype(np.float64) ** 2))),
    "clipped_samples": int(np.count_nonzero(np.abs(data) >= 1.0)),
    "maximum_stereo_seam_step_dbfs": db(np.max(np.abs(data[0].astype(np.float64) - data[-1]))),
    "maximum_internal_sample_step_dbfs": db(np.max(np.abs(np.diff(data.astype(np.float64), axis=0)))),
    "mean_channel_correlation": float(np.corrcoef(data[:, 0], data[:, 1])[0, 1]),
    "mono_energy_fraction_by_band": {label: float(spectral_power[(frequencies >= low) & (frequencies < high)].sum() / total_power)
        for label, low, high in [("below80Hz", 0, 80), ("80to250Hz", 80, 250), ("250to2000Hz", 250, 2000), ("above2000Hz", 2000, sample_rate)]},
    "gain_0_5_expected_rms_dbfs": db(np.sqrt(np.mean(data.astype(np.float64) ** 2))) + 20 * math.log10(0.5),
    "excerpt_start_seconds": start_seconds, "excerpt_duration_seconds": duration_seconds,
    "excerpt_path": str(excerpt_path), "excerpt_sha256": hashlib.sha256(excerpt_path.read_bytes()).hexdigest(),
    "excerpt_selection": "Middle7.0–16.5s chosen before inference to avoid start/end boundary treatment; original loop unchanged.",
    "human_listened": False,
}
(RESULT / "full-loop-metrics.json").write_text(json.dumps(metrics, indent=2), encoding="utf-8")
candidate = {"id": "room-pressure-middle", "absolute_path": str(excerpt_path),
    "relative_path": str(excerpt_path.relative_to(runner.ROOT)), "sha256": metrics["excerpt_sha256"],
    "sample_rate": sample_rate, "channels": int(excerpt.shape[1]), "duration_seconds": duration_seconds,
    "original_source_path": str(SOURCE_AUDIO), "original_source_sha256": metrics["original_sha256"],
    "excerpt_start_seconds": start_seconds, "human_listened": False}
(RESULT / "candidates.json").write_text(json.dumps({"candidates": [candidate]}, indent=2), encoding="utf-8")
shutil.copyfile(BASE_RESULT / "model-files.json", RESULT / "model-files.json")
runner.RESULT = RESULT
runner.PROMPT = (
    "Describe only what you hear in this audio, without guessing its filename or intended source. "
    "Assess its suitability as a quiet background loop in a dark first-person horror game. "
    "Return one concise JSON object with keys: description, mood, tonal_balance, "
    "attention_demand (low, medium or high), best_role (room_ambience, prominent_music, enemy_warning or other), "
    "background_fit (integer1 to5), potential_masking_notes, reason (one sentence). "
    "Use audible evidence and state uncertainty. This excerpt does not establish seamless looping or final in-game volume."
)
print(json.dumps({"full_loop_metrics": metrics}, indent=2), flush=True)
if __name__ == "__main__":
    try:
        runner.main()
    except Exception as error:
        runner.persist("inference-status.json", {"state": "failed", "error": repr(error), "utc": runner.utc()})
        raise
