# ============================================================================
# validate_hunter_skip.py
# PURPOSE:
#   Independently validate the small threshold ambusher after FBX round trip.
#   Reject missing wrapped-limb landmarks and unreadable or ungrounded takes.
# ARCHITECTURAL ROLE:
#   Offline art validator · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Set Skip's silhouette, landmark and displacement acceptance thresholds.
#   - Run shared import, budget, motion and optional visual review checks.
# DEPENDENCIES:
#   Blender 5.2; validate_hunter_ram shared rigid-biped validation harness.
# USAGE NOTES:
#   --render / --detail produce evidence only. No audio or Unity operations.
# ============================================================================
import sys
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from validate_hunter_ram import validate

if __name__=='__main__':
    validate('Skip',(1.19,1.21),dict(idle=.015,walk=.20,run=.3,ready=.15,attack=.20,hit=.15),
             ('CoveredFace','CrookedCoat','LeftFinger0','RightHoodRim','CoatStitch0'))
