# ============================================================================
# hunter_mannequin.py
# PURPOSE:
#   Rebuild a faceless store dummy with dark ball joints and a dust sheet.
#   Its stillness and unbending steps distinguish it without eye or face cues.
# ARCHITECTURAL ROLE:
#   Offline art generator (outside runtime layers) · Hunter art.
# KEY RESPONSIBILITIES:
#   - Define the egg head, segmented body, joints and head/shoulder sheet.
#   - Author dead-still idle, stiff gait, abrupt ready and lunge-grab actions.
# DEPENDENCIES:
#   Blender 5.2 and hunter_humanoid_common; no Unity dependencies.
# USAGE NOTES:
#   Run headless with --factory-startup --python-exit-code 1.
#   Geometry, palette, timings and pose angles are provisional authoring values.
# ============================================================================
import math
import sys
from pathlib import Path
sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from hunter_humanoid_common import Body, humanoid, limbs, rotate, envelope, generate

LENGTHS = dict(idle=60, walk=36, run=24, ready=12, attack=30, hit=18)
PALETTE = [('Body',(.79,.77,.70),(0,0,0),0),
           ('Sheet',(.52,.53,.51),(0,0,0),0),
           ('Joints',(.13,.14,.14),(0,0,0),0)]


def build():
    body = Body('Mannequin',humanoid(1.85,shoulder=.285),PALETTE)
    limbs(body,jointed=True,material=0,joint_material=2)
    body.egg('Hips',(0,0,.94),(.34,.23,.24))
    body.egg('Chest',(0,0,1.265),(.40,.25,.48))
    body.link('Head',(0,0,1.46),(0,0,1.65),.055,2)
    body.egg('Head',(0,-.015,1.68),(.245,.23,.31))
    # Head-shaped sheet, widening across shoulders and ending in a flared hem.
    body.cloth('Head',[(0,.025,1.54,.145,.145),(0,.025,1.73,.148,.14),
                       (0,.025,1.83,.085,.09),(0,.025,1.85,.025,.03)],1)
    body.cloth('Chest',[(0,.025,1.37,.345,.185),(0,.025,1.46,.31,.17),
                        (0,.025,1.57,.145,.145)],1)
    return body


def author(rig,action,t):
    wave = math.sin(t*2*math.pi)
    if action=='idle':
        return
    if action in ('walk','run'):
        for side, sign in (('Left',1),('Right',-1)):
            rotate(rig,side+'Thigh',(sign*(32 if action=='run' else 19)*wave,0,0))
            rotate(rig,side+'Arm',(-sign*12*wave,0,0))
        # Knee and elbow bones intentionally remain locked.
    elif action=='ready':
        snap = 0 if t<.25 else 1
        rotate(rig,'LeftArm',(-64*snap,0,-12*snap))
        rotate(rig,'RightArm',(-48*snap,0,18*snap))
        rotate(rig,'Head',(0,0,14*snap))
    elif action=='attack':
        a = envelope(t)
        rotate(rig,'Chest',(-24*a,0,0))
        rotate(rig,'LeftArm',(-78*a,0,-12*a))
        rotate(rig,'RightArm',(-78*a,0,12*a))
        rotate(rig,'LeftHand',(0,0,18*a))
        rotate(rig,'RightHand',(0,0,-18*a))
    else:
        a = envelope(t,.25)
        rotate(rig,'Chest',(13*a,0,12*a))
        rotate(rig,'Head',(0,0,-20*a))


if __name__=='__main__':
    generate('Mannequin',build,author,LENGTHS)
