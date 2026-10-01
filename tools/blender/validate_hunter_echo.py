# ============================================================================
# validate_hunter_echo.py
# PURPOSE:
#   Re-import Echo's FBX into a fresh scene and reject export-contract drift.
#   Record measured content hashes and preview evidence independently of build.
# ARCHITECTURAL ROLE:
#   Offline art validator (outside runtime layers) · Hunter art.
# KEY RESPONSIBILITIES:
#   - Supply independently declared Echo clip timing expectations.
#   - Execute shared geometry, rig, animation and manifest checks.
# DEPENDENCIES:
#   Blender 5.2 and hunter_humanoid_common; no generator or Unity dependency.
# USAGE NOTES:
#   Run with --background --factory-startup --python-exit-code 1.
#   Nonzero exit means hand-off is blocked; see HunterEcho/validation.json.
# ============================================================================
import sys
from pathlib import Path
sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from hunter_humanoid_common import validate

if __name__=='__main__':
    validate('Echo',dict(idle=60,walk=30,run=20,ready=18,attack=30,hit=18))
