# ============================================================================
# hunter_blinder.py
# PURPOSE:
#   Build a stooped powder thrower with a distinct working arm and hip pouch.
#   A bound head, dust-stained apron and exposed forearm replace the vendor goblin.
# ARCHITECTURAL ROLE:
#   Offline art generator · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Author a project-made thrower with the standard six-take Generic contract.
#   - Bake a drawn-back warning, forward release and balanced recovery.
# DEPENDENCIES:
#   Blender 5.2; shared creature, humanoid, motion and detail geometry tools.
# USAGE NOTES:
#   Dimensions, palette and amplitudes are provisional. Projectile VFX is external.
#   Contact/release remains at 40 percent, matching the existing clip contract.
# ============================================================================
import math
import sys
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import hunter_creature_common as c
from hunter_humanoid_common import humanoid
from hunter_motion_common import biped_gait, pose_delta
from hunter_detail_geometry import SculptCreature

PALETTE={'Cloth':(.32,.30,.25),'Skin':(.57,.53,.42),'Leather':(.22,.15,.105),'Powder':(.79,.76,.57)}


def build():
    bones=humanoid(1.6,shoulder=.235)[1:]
    bones=[(n,p,(x,-.19 if n=='Head' else -.06 if n=='Chest' else y,
                    1.32 if n=='Head' else z)) for n,p,(x,y,z) in bones]
    m=SculptCreature('Blinder',PALETTE,bones)
    heads={n:b.head_local.copy() for n,b in m.rig.data.bones.items()}
    m.shape('Pelvis','Hips',(0,0,.79),(.32,.25,.25),'Cloth','ico')
    m.shape('WaistBinding','Hips',(0,-.035,.94),(.30,.28,.30),'Leather','ico')
    m.shape('HunchedBack','Chest',(0,.01,1.17),(.44,.41,.50),'Cloth','ico')
    m.shape('BoundHead','Head',(0,-.23,1.355),(.24,.29,.29),'Cloth','ico')
    m.shape('Blindfold','Head',(0,-.365,1.39),(.23,.026,.07),'Leather')
    for side,sign in (('Left',1),('Right',-1)):
        for start,end,width in (('Arm','Forearm',.12),('Forearm','Hand',.09),('Thigh','Shin',.13),('Shin','Foot',.09)):
            a,b=heads[side+start],heads[side+end]
            mat='Skin' if side=='Right' and start in ('Arm','Forearm') else 'Cloth'
            m.bar(side+start,side+start,a,b,width,mat)
            m.shape(side+start+'Joint',side+start,a,(width,)*3,mat,'ico')
        m.shape(side+'Boot',side+'Foot',(sign*.105,-.085,.055),(.15,.31,.11),'Leather')
        hand=heads[side+'Hand']
        m.shape(side+'Palm',side+'Hand',hand,(.10,.07,.13),'Skin','ico')
        for i in range(4):
            x=hand.x+(i-1.5)*.024
            m.sweep(side+'Finger'+str(i),side+'Hand',[(x,-.025,hand.z-.04),
                (x,-.07,hand.z-.12),(x,-.11,hand.z-.10)],[.014,.010,.003],'Skin',6)
        for i in range(4):
            x=sign*(.025+i*.045)
            m.sweep(side+'ApronFold'+str(i),'Hips',[(x,-.13,.86),(x*1.15,-.18,.64),
                (x*1.05,-.18,.48+.025*(i%2))],[.033,.035,.013],'Leather')
        for i in range(3):
            z=1.29+i*.07
            m.sweep(side+'HeadWrap'+str(i),'Head',[(sign*.10,-.31,z),(sign*.13,-.23,z+.014),
                (sign*.085,-.12,z+.012)],[.012,.016,.012],'Powder')
    m.shape('PowderPouch','Hips',(-.27,-.12,.73),(.25,.23,.30),'Leather','ico')
    m.shape('PouchMouth','Hips',(-.27,-.12,.86),(.21,.19,.055),'Cloth','cylinder')
    m.shape('VisiblePowder','Hips',(-.27,-.12,.885),(.16,.13,.025),'Powder','ico')
    m.sweep('PouchDrawstring','Hips',[(-.38,-.19,.87),(-.29,-.24,.83),(-.21,-.23,.87),
        (-.18,-.22,.71)],[.010]*4,'Powder')
    m.sweep('CrossBodyStrap','Chest',[(-.22,-.13,.97),(-.07,-.215,1.15),(.19,-.14,1.34)],
        [.035]*3,'Leather')
    for i in range(5):
        m.shape('ApronDust'+str(i),'Hips',(-.13+i*.058,-.211,.68+.025*(i%2)),(.035,.009,.061),'Powder','ico')
    return m


def motion(rig,role,t):
    wave=math.sin(math.tau*t)
    if role in ('walk','run'):
        biped_gait(rig,t,role=='run',arm_scale=.8)
        pose_delta(rig,'Chest',(12,0,4*wave))
        pose_delta(rig,'Head',(-8,0,0))
    elif role=='idle':
        pose_delta(rig,'Chest',(10+2*wave,0,0))
        pose_delta(rig,'RightForearm',(-18-6*wave,0,0))
    elif role=='ready':
        pose_delta(rig,'Chest',(10,0,-20*t))
        pose_delta(rig,'RightArm',(70*t,18*t,0))
        pose_delta(rig,'RightForearm',(-90*t,0,0))
        pose_delta(rig,'LeftArm',(-25*t,0,0))
    elif role=='attack':
        a=c.envelope(t,[(0,0),(.2,.08),(.4,1),(.65,.85),(1,0)])
        pre=max(0,1-t/.4)
        pose_delta(rig,'Chest',(10+12*a,0,-20*pre+22*a))
        pose_delta(rig,'RightArm',(70*pre-115*a,18*pre,0))
        pose_delta(rig,'RightForearm',(-90*pre-8*a,0,0))
        pose_delta(rig,'LeftArm',(-25*pre+30*a,0,0))
    else:
        a=math.sin(math.pi*t)
        pose_delta(rig,'Chest',(-15*a,0,-14*a))
        pose_delta(rig,'RightArm',(-35*a,0,0))


def main():
    c.reset()
    m=build()
    clips=c.animate(m,motion)
    manifest=c.save_model(m,clips,{'attack_origin':c.socket(m.rig,'RightHand',(-.305,-.10,.60)),
        'head_or_top':c.socket(m.rig,'Head',(0,-.23,1.5))},
        {'motion_contract':{'ready':'draw throwing arm back','attack':'powder throw; release at 40 percent','hit':'recover balance',
                            'authored_walk_speed_mps':.9408,'authored_run_speed_mps':2.02272}})
    c.previews(m,clips,manifest)


if __name__=='__main__':
    main()
