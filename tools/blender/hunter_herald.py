# ============================================================================
# hunter_herald.py
# PURPOSE:
#   Rebuild Herald as a gaunt, tall figure whose open chest carries the scream.
#   Two hinged rib doors and long arms make the inhale legible without a face.
# ARCHITECTURAL ROLE:
#   Offline art generator (outside runtime layers) · Hunter art.
# KEY RESPONSIBILITIES:
#   - Define tapered bones, hinged ribs, vertebrae, open jaw and red cavity.
#   - Author full-body inhale and chest-flaring scream actions.
# DEPENDENCIES:
#   Blender 5.2 and hunter_humanoid_common; no Unity dependencies.
# USAGE NOTES:
#   Run headless with --factory-startup --python-exit-code 1.
#   All authored dimensions, colours and motion values are prototype tuning.
# ============================================================================
import math
import sys
from pathlib import Path
sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from hunter_humanoid_common import humanoid, limbs, rotate, envelope, generate
from hunter_motion_common import biped_gait
from hunter_detail_geometry import SculptBody as Body, humanoid_details

LENGTHS = dict(idle=90,walk=40,run=24,ready=24,attack=40,hit=20)
PALETTE = [('Skin',(.49,.48,.46),(0,0,0),0),
           ('Cavity',(.17,.022,.03),(.30,.025,.035),.3),
           ('Bone',(.76,.72,.59),(0,0,0),0)]


def build():
    bones = humanoid(2.5,shoulder=.31,ribs=True)
    # Extend both arms down toward the knees rather than normal player wrists.
    bones = [(n,p,(h[0],h[1],.78 if n.endswith('Hand') else 1.30 if n.endswith('Forearm') else h[2])) for n,p,h in bones]
    body = Body('Herald',bones,PALETTE)
    limbs(body)
    body.egg('Hips',(0,0,1.23),(.32,.20,.23))
    body.link('Chest',(0,.06,1.3),(0,.06,2.04),.045,0)
    body.link('Head',(0,0,2.01),(0,0,2.30),.048,0)
    body.egg('Head',(0,-.005,2.34),(.195,.205,.32))
    # Recessed red back wall leaves a real empty volume behind the ribs.
    body.box('Chest',(0,.105,1.765),(.37,.035,.49),1)
    for side, sign in (('Left',1),('Right',-1)):
        body.link('Chest',(sign*.025,.045,2.01),(sign*.31,0,1.975),.045)
        for z in (1.52,1.64,1.76,1.88,1.99):
            # Each articulated door is a comb of curved polygonal rib segments.
            points = [(sign*.23,.045,z),(sign*.275,.01,z-.003),
                      (sign*.29,-.045,z-.012),(sign*.275,-.11,z-.022),
                      (sign*.235,-.15,z-.035),(sign*.15,-.18,z-.05),
                      (sign*.07,-.19,z-.06)]
            body.sweep(side+'Ribs',points,[.023,.027,.028,.027,.024,.019,.009],2)
        body.link(side+'Ribs',(sign*.23,.045,1.46),(sign*.23,.045,2.015),.027)
    humanoid_details(body)
    return body


def chest_open(rig,amount):
    for side,sign in (('Left',1),('Right',-1)):
        rotate(rig,side+'Ribs',(0,0,sign*amount))


def author(rig,action,t):
    wave = math.sin(t*2*math.pi)
    if action=='idle':
        chest_open(rig,12+10*wave)
        rotate(rig,'Chest',(5*wave,0,0))
        rotate(rig,'Head',(-7*wave,0,0))
    elif action in ('walk','run'):
        biped_gait(rig,t,action=='run',arm_scale=1.1)
        rotate(rig,'Chest',(18 if action=='run' else 5,3*wave,-5*wave))
        chest_open(rig,15+10*math.sin(4*math.pi*t))
    elif action=='ready':
        chest_open(rig,65*t)
        rotate(rig,'Chest',(-18*t,0,0))
        rotate(rig,'Head',(-32*t,0,0))
        for side,sign in (('Left',1),('Right',-1)):
            rotate(rig,side+'Arm',(0,-sign*28*t,0))
    elif action=='attack':
        from hunter_creature_common import envelope as keyed
        a = keyed(t,[(0,0),(.18,.05),(.4,1),(.64,.92),(1,0)])
        pre = max(0,1-t/.4)
        chest_open(rig,65*pre+100*a)
        rotate(rig,'Head',(-32*pre-24*a,0,0))
        rotate(rig,'Chest',(-18*pre+22*a,0,0))
        for side,sign in (('Left',1),('Right',-1)):
            rotate(rig,side+'Arm',(-18*a,-sign*(28*pre+72*a),0))
    else:
        a = envelope(t,.25)
        chest_open(rig,15*a)
        rotate(rig,'Chest',(-30*a,0,15*a))
        rotate(rig,'Head',(-15*a,0,0))


if __name__=='__main__':
    generate('Herald',build,author,LENGTHS)
