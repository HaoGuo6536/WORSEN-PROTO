# ============================================================================
# validate_hunter_blinder.py
# PURPOSE:
#   Independently validate the hunched powder thrower after FBX round trip.
#   Require the powder pouch, exposed throwing hand and a forward release pose.
# ARCHITECTURAL ROLE:
#   Offline art validator · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Set Blinder's silhouette, landmark and displacement acceptance thresholds.
#   - Run shared import, budget, throw-motion and optional visual review checks.
# DEPENDENCIES:
#   Blender 5.2; validate_hunter_ram shared rigid-biped validation harness.
# USAGE NOTES:
#   --render / --detail produce evidence only. No Unity operations.
# ============================================================================
import sys
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from validate_hunter_ram import validate

if __name__=='__main__':
    validate('Blinder',(1.49,1.51),dict(idle=.02,walk=.25,run=.4,ready=.25,attack=.5,hit=.20),
             ('BoundHead','HunchedBack','PowderPouch','VisiblePowder','RightFinger0'))
