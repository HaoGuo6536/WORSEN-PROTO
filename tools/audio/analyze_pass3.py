# ============================================================================
# analyze_pass3.py
# PURPOSE:
#   Ask an explicit fit question of frozen clips using the pinned local MOSS model.
#   Preserve captions separately so a leading question cannot erase disagreement.
# ARCHITECTURAL ROLE:
#   Offline audio tooling utility; outside Unity assemblies.
# KEY RESPONSIBILITIES:
#   - Supply a recorded question to the existing bounded provenance runner.
# DEPENDENCIES:
#   analyze_candidates and moss_paths; pinned local MOSS environment.
# USAGE NOTES:
#   New checkout-local prepared result directory, coordinator admission, no Unity.
#   One clip at a time; unchanged 11 GiB free guard and 10.5 GiB allocator cap.
# ============================================================================
import argparse
import os
import sys
import traceback
from moss_paths import ROOT, RESULT


def main():
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--question", required=True)
    args, remaining = parser.parse_known_args()
    if not args.question.strip():
        raise ValueError("A nonempty fit question is required.")
    if not RESULT.resolve().is_relative_to(ROOT / "Logs"):
        raise ValueError("Results must be checkout-local.")
    if (RESULT / "inference-status.json").exists():
        raise FileExistsError("Prepare a new directory; never overwrite inference evidence.")
    import moss_paths
    moss_paths.CACHE = RESULT / "cache"
    os.environ["TORCH_HOME"] = str(moss_paths.CACHE / "torch")
    os.environ["CUDA_CACHE_PATH"] = str(moss_paths.CACHE / "cuda")
    import analyze_candidates as runner
    runner.PROMPT = args.question
    sys.argv = [sys.argv[0], *remaining]
    try:
        runner.main()
    except Exception as error:
        runner.persist("inference-status.json", {"state": "failed", "error": repr(error),
                       "traceback": traceback.format_exc(), "utc": runner.utc()})
        raise


if __name__ == "__main__":
    main()
