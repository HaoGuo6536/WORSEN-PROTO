# ============================================================================
# validate_hunter_herald.py
# PURPOSE:
#   Re-import Herald's FBX into a fresh scene and reject export-contract drift.
#   Check the extra rib bones and record measured hashes independently of build.
# ARCHITECTURAL ROLE:
#   Offline art validator (outside runtime layers) · Hunter art.
# KEY RESPONSIBILITIES:
#   - Supply independently declared Herald clip timing expectations.
#   - Execute shared geometry, rig, animation and manifest checks.
# DEPENDENCIES:
#   Blender 5.2 and hunter_humanoid_common; no generator or Unity dependency.
# USAGE NOTES:
#   Run with --background --factory-startup --python-exit-code 1.
#   Nonzero exit means hand-off is blocked; see HunterHerald/validation.json.
# ============================================================================
import sys
from pathlib import Path
sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from hunter_humanoid_common import validate

if __name__=='__main__':
    validate('Herald',dict(idle=90,walk=40,run=24,ready=24,attack=40,hit=20))
