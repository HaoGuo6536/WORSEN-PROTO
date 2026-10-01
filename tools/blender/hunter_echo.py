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
from hunter_motion_common import biped_gait

LENGTHS = dict(idle=60, walk=30, run=20, ready=18, attack=30, hit=18)
PALETTE = [('Cloth', (.37,.44,.49), (0,0,0), 0),
           ('HoodInterior', (.065,.08,.095), (0,0,0), 0),
           ('Hem', (.24,.29,.33), (0,0,0), 0)]


def build():
    body = Body('Echo', humanoid(1.8, shoulder=.22), PALETTE)
    limbs(body, material=0)
    body.cloth('Hips', [(0,0,.19,.235,.16),(0,0,.90,.175,.125),
                        (0,0,1.15,.19,.13)], 0)
    body.cloth('Chest', [(0,0,1.08,.19,.13),(0,0,1.40,.23,.145),
                         (0,0,1.52,.12,.11)], 0)
    body.cloth('Head', [(0,.025,1.47,.15,.14),(0,.025,1.65,.175,.165),
                        (0,.025,1.78,.11,.12),(0,.025,1.8,.045,.06)], 0)
    # Recess is a single blank dark plane, never eyes or facial features.
    body.egg('Head',(0,-.119,1.635),(.205,.024,.225),1)
    body.cloth('Hips',[(0,0,.18,.238,.162),(0,0,.24,.233,.16)],2)
    return body


def author(rig, action, t):
    # Replay a short section twice per cycle, with a held frame before catchup.
    from hunter_creature_common import envelope as keyed
    phase = keyed(t, [(0,0),(.18,.20),(.24,.14),(.30,.14),(.5,.5),
                      (.68,.70),(.74,.64),(.80,.64),(1,1)])
    wave = math.sin(phase*2*math.pi)
    if action=='idle':
        rotate(rig,'Chest',(3*wave,4*wave,0))
        rotate(rig,'Head',(-2*wave,0,3*wave))
    elif action in ('walk','run'):
        sprint = action=='run'
        biped_gait(rig,phase,sprint,arm_scale=1.4)
        rotate(rig,'Chest',(18 if sprint else 5,3*wave,-5*wave))
        rotate(rig,'Head',(5,0,6*wave))
    elif action=='ready':
        rotate(rig,'LeftArm',(28*t,0,-18*t))
        rotate(rig,'RightArm',(15*t,0,12*t))
        rotate(rig,'Chest',(-16*t,0,8*t))
        rotate(rig,'Head',(-12*t,0,0))
    elif action=='attack':
        a = keyed(t,[(0,0),(.20,.08),(.4,1),(.53,1),(.65,.78),(.72,.90),(1,0)])
        pre = max(0,1-t/.4)
        rotate(rig,'LeftArm',(28*pre-110*a,0,-18*pre))
        rotate(rig,'RightArm',(15*pre-100*a,0,12*pre))
        rotate(rig,'Chest',(-16*pre+24*a,0,8*pre))
        rotate(rig,'Head',(-12*pre-10*a,0,0))
    else:
        a = envelope(t,.25)
        rotate(rig,'Chest',(-30*a,0,-14*a))
        rotate(rig,'Head',(18*a,0,0))
        rotate(rig,'LeftArm',(35*a,0,-15*a))


if __name__=='__main__':
    generate('Echo',build,author,LENGTHS)
