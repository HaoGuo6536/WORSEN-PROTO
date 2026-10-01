# ============================================================================
# validate_hunter_mannequin.py
# PURPOSE:
#   Re-import Mannequin's FBX into a fresh scene and reject contract drift.
#   Verify held/snap idle and record measured hashes independently of build.
# ARCHITECTURAL ROLE:
#   Offline art validator (outside runtime layers) · Hunter art.
# KEY RESPONSIBILITIES:
#   - Supply independently declared Mannequin clip timing expectations.
#   - Require visible skin motion, grounded alternating steps and closed loops.
# DEPENDENCIES:
#   Blender 5.2 and hunter_humanoid_common; no generator or Unity dependency.
# USAGE NOTES:
#   Run with --background --factory-startup --python-exit-code 1.
#   Nonzero exit blocks hand-off; see HunterMannequin/validation.json.
# ============================================================================
import sys
from pathlib import Path
sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from hunter_humanoid_common import validate

if __name__=='__main__':
    validate('Mannequin',dict(idle=60,walk=36,run=24,ready=12,attack=30,hit=18),
             dict(idle=.025,walk=.25,run=.40,ready=.25,attack=.40,hit=.20))
