# ============================================================================
# validate_hunter_stare.py
# PURPOSE:
#   Re-import Stare's FBX into a fresh scene and reject export-contract drift.
#   Record measured content hashes and preview evidence independently of build.
# ARCHITECTURAL ROLE:
#   Offline art validator (outside runtime layers) · Hunter art.
# KEY RESPONSIBILITIES:
#   - Supply independently declared Stare clip timing expectations.
#   - Require visible skin motion, grounded alternating steps and closed loops.
# DEPENDENCIES:
#   Blender 5.2 and hunter_humanoid_common; no generator or Unity dependency.
# USAGE NOTES:
#   Run with --background --factory-startup --python-exit-code 1.
#   Nonzero exit means hand-off is blocked; see HunterStare/validation.json.
# ============================================================================
import sys
from pathlib import Path
sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from hunter_humanoid_common import validate

if __name__=='__main__':
    validate('Stare',dict(idle=90,walk=60,run=24,ready=24,attack=40,hit=20),
             dict(idle=.04,walk=.30,run=.45,ready=.30,attack=.60,hit=.25))
