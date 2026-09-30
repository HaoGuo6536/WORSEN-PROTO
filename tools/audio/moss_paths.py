# ============================================================================
# moss_paths.py
# PURPOSE:
#   Resolve the shared MOSS installation independently of this Unity checkout.
#   Keep machine-local dependencies outside the project while retaining analysis
#   results in the checkout that invokes these tools.
# ARCHITECTURAL ROLE:
#   Offline audio tooling utility; outside Unity runtime and editor assemblies.
# KEY RESPONSIBILITIES:
#   Define installation, model, cache, and project-result paths without I/O.
# DEPENDENCIES:
#   Python standard library; MOSS_AUDIO_HOME and MOSS_AUDIO_RESULTS_DIR overrides.
# USAGE NOTES:
#   No Unity lifecycle or engine side effects. Relative overrides are resolved
#   against the project root, independent of the process working directory.
# ============================================================================
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
INSTALL = Path(os.environ.get("MOSS_AUDIO_HOME", str(Path.home() / "selfhosted/moss-audio"))).expanduser()
if not INSTALL.is_absolute():
    INSTALL = ROOT / INSTALL
SOURCE = INSTALL / "source"
MODEL = INSTALL / "weights/4B-Instruct"
CACHE = INSTALL / "cache"
RESULT = Path(os.environ.get("MOSS_AUDIO_RESULTS_DIR", "Logs/AgentValidation/Horror/audio-analysis")).expanduser()
if not RESULT.is_absolute():
    RESULT = ROOT / RESULT
