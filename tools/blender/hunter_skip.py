# ============================================================================
# hunter_skip.py
# PURPOSE:
#   Build a small crooked threshold ambusher with no cartoon face or horns.
#   Wrapped wire-thin limbs and an uneven hood stay readable in a doorway crouch.
# ARCHITECTURAL ROLE:
#   Offline art generator · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Author a 1.2 metre project skin with six standard Generic takes.
#   - Bake cautious stepping and a short reaching interception, without cues.
# DEPENDENCIES:
#   Blender 5.2; shared creature, humanoid, motion and detail geometry tools.
# USAGE NOTES:
#   All dimensions/colours and amplitudes are provisional. No audio or flash.
#   The stationary controller owns relocation; these clips never move the root.
# ============================================================================
import math
import sys
from pathlib import Path
sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import hunter_creature_common as c
from hunter_humanoid_common import humanoid
from hunter_motion_common import biped_gait, pose_delta
from hunter_detail_geometry import SculptCreature

PALETTE={'Cloth':(.31,.38,.37),'Skin':(.57,.57,.48),'Wrap':(.19,.23,.22),'Seam':(.63,.58,.44)}


def build():
    bones=humanoid(1.2,shoulder=.19)[1:]
    bones=[(n,p,(x+.045 if n=='Head' else x,-.11 if n=='Head' else -.035 if n=='Chest' else y,z)) for n,p,(x,y,z) in bones]
    m=SculptCreature('Skip',PALETTE,bones)
    heads={n:b.head_local.copy() for n,b in m.rig.data.bones.items()}
    m.shape('Pelvis','Hips',(0,0,.58),(.23,.19,.24),'Cloth','ico')
    m.shape('WaistWrap','Hips',(0,-.01,.68),(.21,.19,.22),'Wrap','ico')
    m.shape('CrookedCoat','Chest',(.025,-.025,.83),(.29,.21,.39),'Cloth','ico')
    m.shape('Hood','Head',(.045,-.09,1.065),(.25,.26,.27),'Cloth','ico')
    m.shape('CoveredFace','Head',(.045,-.209,1.055),(.155,.025,.16),'Wrap','ico')
    for side,sign in (('Left',1),('Right',-1)):
        for start,end,width in (('Arm','Forearm',.068),('Forearm','Hand',.049),('Thigh','Shin',.083),('Shin','Foot',.058)):
            a,b=heads[side+start],heads[side+end]
            m.bar(side+start,side+start,a,b,width,'Skin')
            m.shape(side+start+'Joint',side+start,a,(width*1.1,)*3,'Skin','ico')
            for j in range(3):
                pos=a.lerp(b,.28+j*.19)
                m.shape(side+start+'Wrap'+str(j),side+start,pos,(width*1.1,width*1.2,.018),'Wrap')
        m.shape(side+'Foot',side+'Foot',(sign*.105,-.065,.04),(.12,.25,.08),'Wrap')
        hand=heads[side+'Hand']
        m.shape(side+'Palm',side+'Hand',hand,(.08,.055,.11),'Skin','ico')
        for i in range(4):
            x=hand.x+(i-1.5)*.020
            m.sweep(side+'Finger'+str(i),side+'Hand',[(x,-.02,hand.z-.03),
                (x+sign*.012,-.065,hand.z-.10),(x+sign*.018,-.09,hand.z-.12)], [.010,.008,.002],'Skin',6)
        m.sweep(side+'HoodRim','Head',[(.045+sign*.01,-.15,1.19),(.045+sign*.115,-.16,1.12),
            (.045+sign*.10,-.17,1.00)],[.012,.014,.007],'Seam')
        for i in range(4):
            x=sign*(.025+i*.030)
            m.sweep(side+'CoatTail'+str(i),'Hips',[(x,0,.64),(x*1.25,-.01,.45),
                (x*1.1,-.03,.36+i*.013)],[.032,.028,.003],'Cloth',6)
    for i in range(7):
        z=.72+i*.037
        m.sweep('CoatStitch'+str(i),'Chest',[(-.027,-.128,z),(.015,-.137,z+.008)], [.006,.006],'Seam',6)
    return m


def motion(rig,role,t):
    wave=math.sin(math.tau*t)
    if role in ('walk','run'):
        biped_gait(rig,t,role=='run',arm_scale=.55)
        pose_delta(rig,'Chest',(15,0,5*wave))
        pose_delta(rig,'Head',(-10,4*wave,0))
    elif role=='idle':
        biped_gait(rig,0,False,stride_scale=0,arm_scale=0)
        pose_delta(rig,'Chest',(24+2*wave,0,4+2*wave))
        pose_delta(rig,'Head',(-8,0,-6+3*wave))
        pose_delta(rig,'LeftArm',(-18,0,0))
    elif role=='ready':
        biped_gait(rig,0,False,stride_scale=0,arm_scale=0)
        pose_delta(rig,'Chest',(12+16*t,0,4))
        pose_delta(rig,'LeftArm',(-45*t,0,0))
        pose_delta(rig,'RightArm',(-25*t,0,0))
    elif role=='attack':
        a=c.envelope(t,[(0,0),(.4,1),(.65,.7),(1,0)])
        pose_delta(rig,'Chest',(28-10*a,0,4))
        pose_delta(rig,'LeftArm',(-45-65*a,0,0))
        pose_delta(rig,'RightArm',(-25-50*a,0,0))
        pose_delta(rig,'LeftForearm',(-20*a,0,0))
    else:
        a=math.sin(math.pi*t)
        pose_delta(rig,'Chest',(-18*a,0,14*a))
        pose_delta(rig,'Head',(12*a,0,-18*a))


def main():
    c.reset()
    m=build()
    clips=c.animate(m,motion)
    manifest=c.save_model(m,clips,{'attack_origin':c.socket(m.rig,'LeftHand',(.26,-.09,.42)),
        'head_or_top':c.socket(m.rig,'Head',(.045,-.09,1.2))},
        {'motion_contract':{'presence':'silent; no animation events','ready':'threshold crouch','attack':'intercept reach',
                            'authored_walk_speed_mps':.7056,'authored_run_speed_mps':1.51704}})
    c.previews(m,clips,manifest)


if __name__=='__main__':
    main()
