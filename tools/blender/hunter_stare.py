# ============================================================================
# hunter_stare.py
# PURPOSE:
#   Rebuild Stare as a tall, thin covered figure with a permanent lateral lean.
#   Unequal shoulders and arm lengths carry recognition beside a fixed long face.
# ARCHITECTURAL ROLE:
#   Offline art generator (outside runtime layers) · Hunter art.
# KEY RESPONSIBILITIES:
#   - Define pleated asymmetric cloth, a long mask and fixed forward eyes.
#   - Author slow lateral sway, stalking steps, hunched run and long-arm sweep.
# DEPENDENCIES:
#   Blender 5.2 and hunter_humanoid_common; no Unity dependencies.
# USAGE NOTES:
#   Run headless with --factory-startup --python-exit-code 1.
#   All shape, colour and motion numbers are provisional prototype values.
# ============================================================================
import math
import sys
from pathlib import Path
sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from hunter_humanoid_common import humanoid, limbs, rotate, envelope, generate
from hunter_motion_common import biped_gait
from hunter_detail_geometry import SculptBody as Body, humanoid_details

LENGTHS = dict(idle=90,walk=60,run=24,ready=24,attack=40,hit=20)
PALETTE = [('Cloth',(.19,.22,.25),(0,0,0),0),
           ('CoolRim',(.32,.38,.43),(.24,.34,.43),.12),
           ('Mask',(.62,.65,.61),(0,0,0),0),
           ('Eyes',(.85,.88,.77),(.40,.48,.42),.16)]


def build():
    body = Body('Stare',humanoid(2.3,lean=.22,shoulder=.205,long_arm=True),PALETTE)
    limbs(body)
    body.cloth('Hips',[(0,0,.23,.18,.13),(.025,0,.65,.135,.12),
                       (.09,0,1.25,.135,.115)],0)
    body.cloth('Chest',[(.07,0,1.12,.13,.115),(.15,0,1.74,.18,.12),
                        (.22,0,1.94,.10,.09)],0)
    body.cloth('Head',[(.20,0,1.93,.10,.10),(.255,0,2.18,.13,.115),
                       (.275,0,2.3,.045,.055)],0)
    heads = {n:p for n,_,p in body.bones}
    # High-side shawl rises with the shoulder; opposite side ends lower.
    body.link('LeftArm',heads['LeftArm'],(.43,0,1.64),.115,0,.065)
    body.link('RightArm',heads['RightArm'],(-.09,0,1.48),.09,0,.055)
    body.link('Chest',(.31,.012,1.3),(.32,.012,1.91),.012,1)
    body.link('Head',(.35,.012,2.02),(.32,.012,2.245),.009,1)
    humanoid_details(body)
    return body


def author(rig,action,t):
    wave = math.sin(t*2*math.pi)
    if action=='idle':
        rotate(rig,'Chest',(2*wave,7*wave,0))
        rotate(rig,'Head',(0,-3*wave,0))
    elif action in ('walk','run'):
        sprint = action=='run'
        biped_gait(rig,t,sprint,arm_scale=1.4)
        rotate(rig,'Chest',(24 if sprint else 8,6*wave,0))
        rotate(rig,'Head',(12 if sprint else 3,-3*wave,0))
    elif action=='ready':
        rotate(rig,'LeftArm',(-40*t,0,-50*t))
        rotate(rig,'Chest',(-12*t,10*t,18*t))
    elif action=='attack':
        a = envelope(t)
        pre = max(0,1-t/.4)
        rotate(rig,'LeftArm',(-40*pre-110*a,0,-50*pre+(-55+100*min(t/.4,1))*a))
        rotate(rig,'Chest',(-12*pre+24*a,10*pre,-35*a+18*pre))
    else:
        a = envelope(t,.25)
        rotate(rig,'Chest',(-24*a,20*a,0))
        rotate(rig,'Head',(0,0,-18*a))


if __name__=='__main__':
    generate('Stare',build,author,LENGTHS)
