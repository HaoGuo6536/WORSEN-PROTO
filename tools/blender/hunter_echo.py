# ============================================================================
# hunter_echo.py
# PURPOSE:
#   Rebuild Echo as a narrow, faceless hooded recording of the player.
#   Author a floor-length cover and a repeating gait hitch without external art.
# ARCHITECTURAL ROLE:
#   Offline art generator (outside runtime layers) · Hunter art.
# KEY RESPONSIBILITIES:
#   - Define Echo's primitive silhouette and blue-grey palette.
#   - Author six rigid, in-place actions with a straight reach attack.
# DEPENDENCIES:
#   Blender 5.2 and hunter_humanoid_common; no Unity dependencies.
# USAGE NOTES:
#   Run headless with --factory-startup --python-exit-code 1.
#   Walk matches the blocky player's 30-frame cycle; run is provisional because
#   that source has no Run action. All art numbers below are prototype values.
# ============================================================================
import math
import sys
from pathlib import Path
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from hunter_humanoid_common import Body, humanoid, limbs, rotate, envelope, generate

LENGTHS = dict(idle=60, walk=30, run=20, ready=18, attack=30, hit=18)
PALETTE = [('Cloth', (.37,.44,.49), (0,0,0), 0),
           ('HoodInterior', (.065,.08,.095), (0,0,0), 0),
           ('Hem', (.24,.29,.33), (0,0,0), 0)]


def build():
    body = Body('Echo', humanoid(1.8, shoulder=.22), PALETTE)
    limbs(body, material=0)
    body.cloth('Hips', [(0,0,.035,.235,.16),(0,0,.90,.175,.125),
                        (0,0,1.15,.19,.13)], 0)
    body.cloth('Chest', [(0,0,1.08,.19,.13),(0,0,1.40,.23,.145),
                         (0,0,1.52,.12,.11)], 0)
    body.cloth('Head', [(0,.025,1.47,.15,.14),(0,.025,1.65,.175,.165),
                        (0,.025,1.78,.11,.12),(0,.025,1.8,.045,.06)], 0)
    # Recess is a single blank dark plane, never eyes or facial features.
    body.egg('Head',(0,-.119,1.635),(.205,.024,.225),1)
    body.cloth('Hips',[(0,0,.025,.238,.162),(0,0,.085,.233,.16)],2)
    return body


def author(rig, action, t):
    # One brief repeat per half-step: a deliberately bad recording, not jitter.
    phase = t
    if action in ('walk','run'):
        half = (t*2)%1
        if .40 <= half < .52:
            phase -= .055
    wave = math.sin(phase*2*math.pi)
    if action=='idle':
        rotate(rig,'Chest',(0,1.0*wave,0))
    elif action in ('walk','run'):
        sprint = action=='run'
        for side, sign in (('Left',1),('Right',-1)):
            rotate(rig,side+'Thigh',(sign*(36 if sprint else 25)*wave,0,0))
            rotate(rig,side+'Shin',(-30*max(0,-sign*wave),0,0))
            rotate(rig,side+'Arm',(-sign*(28 if sprint else 20)*wave,0,0))
        rotate(rig,'Chest',(-7 if sprint else 0,0,0))
    elif action=='ready':
        rotate(rig,'LeftArm',(-35*t,0,0))
        rotate(rig,'Head',(6*t,0,0))
    elif action=='attack':
        a = envelope(t)
        rotate(rig,'LeftArm',(-90*a,0,0))
        rotate(rig,'RightArm',(-75*a,0,0))
        rotate(rig,'Chest',(-12*a,0,0))
    else:
        a = envelope(t,.25)
        rotate(rig,'Chest',(18*a,0,-6*a))
        rotate(rig,'Head',(12*a,0,0))


if __name__=='__main__':
    generate('Echo',build,author,LENGTHS)
