# ============================================================================
# hunter_stare.py
# PURPOSE:
#   Rebuild Stare as a tall, thin covered figure with a permanent lateral lean.
#   Unequal shoulders and arm lengths carry recognition, never face detail.
# ARCHITECTURAL ROLE:
#   Offline art generator (outside runtime layers) · Hunter art.
# KEY RESPONSIBILITIES:
#   - Define asymmetric cloth and a faint cool emissive edge.
#   - Author slight sway, gliding walk, hunched run and long-arm sweep.
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
from hunter_humanoid_common import Body, humanoid, limbs, rotate, envelope, generate

LENGTHS = dict(idle=90,walk=60,run=24,ready=24,attack=40,hit=20)
PALETTE = [('Cloth',(.075,.09,.115),(0,0,0),0),
           ('CoolRim',(.15,.20,.25),(.24,.34,.43),.12)]


def build():
    body = Body('Stare',humanoid(2.3,lean=.22,shoulder=.205,long_arm=True),PALETTE)
    limbs(body)
    body.cloth('Hips',[(0,0,.025,.18,.13),(.025,0,.65,.135,.12),
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
    return body


def author(rig,action,t):
    wave = math.sin(t*2*math.pi)
    if action=='idle':
        rotate(rig,'Chest',(0,.65*wave,0))
        rotate(rig,'Head',(0,-.4*wave,0))
    elif action=='walk':
        rotate(rig,'Chest',(0,1.2*wave,0))
        rotate(rig,'LeftArm',(3*wave,0,0))
        rotate(rig,'RightArm',(-2*wave,0,0))
    elif action=='run':
        rotate(rig,'Chest',(-22,0,0))
        rotate(rig,'Head',(12,0,0))
        for side, sign in (('Left',1),('Right',-1)):
            rotate(rig,side+'Thigh',(sign*23*wave,0,0))
            rotate(rig,side+'Arm',(-sign*25*wave,0,sign*8))
    elif action=='ready':
        rotate(rig,'LeftArm',(-40*t,0,-50*t))
        rotate(rig,'Chest',(0,0,12*t))
    elif action=='attack':
        a = envelope(t)
        rotate(rig,'LeftArm',(-82*a,0,(-55+120*min(t/.4,1))*a))
        rotate(rig,'Chest',(-12*a,0,-25*a))
    else:
        a = envelope(t,.25)
        rotate(rig,'Chest',(10*a,12*a,0))
        rotate(rig,'Head',(0,0,-18*a))


if __name__=='__main__':
    generate('Stare',build,author,LENGTHS)
