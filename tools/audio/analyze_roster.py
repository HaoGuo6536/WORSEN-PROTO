# ============================================================================
# analyze_roster.py
# PURPOSE:
#   Ask the installed MOSS model about hunter textures and unwanted animal voices.
#   Reuse bounded inference without exposing candidate names to the model.
# ARCHITECTURAL ROLE:
#   Offline audio tooling utility; outside Unity assemblies.
# KEY RESPONSIBILITIES:
#   - Set the perceptual questionnaire and use the existing provenance runner.
# DEPENDENCIES:
#   analyze_candidates and its pinned local MOSS environment.
# USAGE NOTES:
#   Set MOSS_AUDIO_RESULTS_DIR to the newly prepared directory. Pass
#   --coordinator-admitted --limit N; requires 11 GiB free and caps at 10.5 GiB.
#   No human listening or in-game mix acceptance is implied by model output.
# ============================================================================
import os
import sys
import traceback
from moss_paths import ROOT, RESULT

sys.dont_write_bytecode = True
if not RESULT.resolve().is_relative_to(ROOT / "Logs"):
    raise ValueError("Results must be checkout-local.")
if (RESULT / "inference-status.json").exists():
    raise FileExistsError("Preserve prior inference; prepare a new result directory.")
# Keep even framework caches inside the worker checkout.
import moss_paths
moss_paths.CACHE = RESULT / "cache"
os.environ["TORCH_HOME"] = str(moss_paths.CACHE / "torch")
os.environ["CUDA_CACHE_PATH"] = str(moss_paths.CACHE / "cuda")
import analyze_candidates as runner

runner.PROMPT = (
    "Listen to the audio and describe the actual sound, not a story. "
    "Return a concise JSON object with keys description, pig_orc_goblin, "
    "human_or_creature, size, material, onset, uncertainty. "
    "In pig_orc_goblin assess separately whether it resembles pig snorts/squeals, "
    "a guttural orc grunt, or a goblin voice, or none. "
    "In human_or_creature say human breath/voice, animal/creature, nonvocal, or ambiguous. "
    "Describe perceived size and texture/material (wood, metal, wet, air, etc.), "
    "not a certain physical source. Do not force a creature interpretation on nonvocal audio."
)
if "--describe" in sys.argv:
    sys.argv.remove("--describe")
    runner.PROMPT = "Describe this audio."
if "--plain" in sys.argv:
    sys.argv.remove("--plain")
    runner.PROMPT = ("What sounds can be heard? Describe their texture, apparent size and material. "
                     "Is there human breathing, a creature vocalization, pig snorting, an orc grunt, "
                     "a goblin squeal, or no voice? Answer briefly; say if uncertain.")

if __name__ == "__main__":
    try:
        runner.main()
    except Exception as error:
        runner.persist("inference-status.json", {"state": "failed", "error": repr(error),
                       "traceback": traceback.format_exc(), "utc": runner.utc()})
        raise
