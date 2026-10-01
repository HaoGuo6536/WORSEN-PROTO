# ============================================================================
# hunter_ram.py
# PURPOSE:
#   Build a heavy, low-front charging body rather than a recycled upright satyr.
#   Broad shoulder shields and a blunt horn yoke carry its silhouette at distance.
# ARCHITECTURAL ROLE:
#   Offline art generator · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Author the project-made rigid skin and six compatible Generic takes.
#   - Bake grounded strides, planted stamps, a driving attack and wall stagger.
# DEPENDENCIES:
#   Blender 5.2; shared creature, humanoid, motion and detail geometry tools.
# USAGE NOTES:
#   Metres, colours and pose amplitudes are provisional art tuning. No audio.
#   Runtime scales the measured stance speed; the run is not an 18 m root slide.
# ============================================================================
import math
import sys
from pathlib import Path
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import hunter_creature_common as c
from hunter_humanoid_common import humanoid
from hunter_motion_common import biped_gait, pose_delta
from hunter_detail_geometry import SculptCreature

PALETTE = {'Hide': (.36,.32,.29), 'Horn': (.69,.63,.48),
           'Binding': (.18,.145,.12), 'Scar': (.48,.39,.32)}


def build():
    bones = humanoid(1.8, shoulder=.51)[1:]
    bones = [(n,p,(x*2.6 if n.endswith(('Thigh','Shin','Foot')) else x,
                   -.20 if n == 'Chest' else -.54 if n == 'Head' else y,
                   1.13 if n == 'Head' else 1.19 if n == 'Chest' else z)) for n,p,(x,y,z) in bones]
    m = SculptCreature('Ram', PALETTE, bones)
    heads = {n: tuple(b.head_local) for n,b in m.rig.data.bones.items()}
    m.shape('Haunch','Hips',(0,.06,.86),(.79,.58,.56),'Hide','ico')
    m.shape('ShoulderMass','Chest',(0,-.17,1.26),(1.25,.85,.72),'Hide','ico')
    m.shape('ImpactSkull','Head',(0,-.66,1.16),(.72,.65,.51),'Horn','ico')
    m.shape('Muzzle','Head',(0,-.92,1.03),(.48,.29,.28),'Hide','box')
    m.shape('NoseShield','Head',(0,-1.058,1.07),(.40,.04,.15),'Binding','box')
    for side, sign in (('Left',1),('Right',-1)):
        for start,end,width in (('Arm','Forearm',.36),('Forearm','Hand',.30),('Thigh','Shin',.32),('Shin','Foot',.23)):
            m.bar(side+start,side+start,heads[side+start],heads[side+end],width,'Hide')
            m.shape(side+start+'Joint',side+start,heads[side+start],(width*.8,)*3,'Hide','ico')
        x = heads[side+'Foot'][0]
        for toe in (-1,1):
            m.shape(side+'SplitHoof'+str(toe),side+'Foot',(x+toe*.07,-.085,.055),(.13,.33,.11),'Horn')
        m.shape(side+'Knuckle',side+'Hand',(sign*.58,-.045,.61),(.30,.25,.26),'Binding','ico')
        m.shape(side+'Scapula','Chest',(sign*.49,-.19,1.37),(.41,.63,.40),'Scar','ico')
        m.sweep(side+'Horn','Head',[(sign*.27,-.56,1.26),(sign*.53,-.49,1.29),
            (sign*.72,-.58,1.15),(sign*.70,-.84,1.04),(sign*.55,-1.02,1.11)],
            [.15,.15,.12,.075,.006],'Horn',12)
        for i in range(4):
            y=-.43+i*.13
            m.sweep(side+'ShoulderFold'+str(i),'Chest',[(sign*.15,y,1.56),
                (sign*.40,y,1.58),(sign*.62,y,1.44)],[.014,.023,.008],'Binding')
        for i in range(3):
            z=.95+i*.09
            m.sweep(side+'SkullScar'+str(i),'Head',[(sign*.14,-.91,z),
                (sign*.27,-.88,z+.025),(sign*.32,-.77,z+.02)],[.007,.012,.005],'Scar')
    return m


def motion(rig, role, t):
    wave = math.sin(math.tau*t)
    if role in ('walk','run'):
        if role == 'run':
            charge_gait(rig,t)
        else:
            biped_gait(rig,t,False,stride_scale=1.08,arm_scale=.6)
        pose_delta(rig,'Chest',(10 if role=='run' else 3,0,-3*wave))
        pose_delta(rig,'Head',(-8 if role=='run' else -2,0,0))
    elif role == 'idle':
        pose_delta(rig,'Chest',(4*wave,0,0))
        pose_delta(rig,'Head',(-5*wave,0,0))
    elif role == 'ready':
        stamp = c.envelope(t,[(0,0),(.25,1),(.42,0),(.57,0),(.75,.8),(.9,0),(1,0)])
        pose_delta(rig,'LeftThigh',(-32*stamp,0,0))
        pose_delta(rig,'LeftShin',(42*stamp,0,0))
        pose_delta(rig,'LeftFoot',(-10*stamp,0,0))
        pose_delta(rig,'Chest',(12*t,0,0))
        pose_delta(rig,'Head',(-6*t,0,0))
    elif role == 'attack':
        # The explicit attack take must articulate too, not just nod its torso.
        # Reuse the charge bound; runtime charge still selects the run slot.
        charge_gait(rig,t)
        a=c.envelope(t,[(0,0),(.4,1),(.65,.6),(1,0)])
        pose_delta(rig,'Chest',(12+18*a,0,0))
        pose_delta(rig,'Head',(-6-12*a,0,0))
    else:
        a=c.envelope(t,[(0,0),(.16,1),(.38,.5),(.60,.7),(1,0)])
        pose_delta(rig,'Chest',(-24*a,0,9*a))
        pose_delta(rig,'Head',(18*a,0,-14*a))
        pose_delta(rig,'LeftArm',(-25*a,0,0))
        pose_delta(rig,'RightArm',(-25*a,0,0))


def charge_gait(rig, phase):
    # A charge bounds between short planted contacts, rather than playing a
    # walking duty cycle seven times faster at 18 m/s. The airborne intervals
    # carry the long ground stride; the fixed root still belongs to the motor.
    h=rig.data.bones['Hips'].head_local.z
    stride=h*.46
    crouch=h*.20
    bob=h*.035*(1-math.cos(4*math.pi*phase))
    pose_delta(rig,'Hips',offset=(0,0,-crouch+bob))
    for side,shift in (('Left',0),('Right',.5)):
        p=(phase+shift)%1
        if p<.18:
            y=stride*(-1+2*p/.18)
            lift=0
        else:
            swing=(p-.18)/.82
            y=stride*(1-2*swing)
            lift=h*.32*math.sin(math.pi*swing)
        upper=rig.data.bones[side+'Thigh'].head_local.z-rig.data.bones[side+'Shin'].head_local.z
        lower=rig.data.bones[side+'Shin'].head_local.z-rig.data.bones[side+'Foot'].head_local.z
        down=upper+lower-crouch+bob-lift
        distance=math.hypot(y,down)
        assert distance<upper+lower, 'Charge foot beyond reach'
        knee=math.acos(max(-1,min(1,(distance*distance-upper*upper-lower*lower)/(2*upper*lower))))
        thigh=math.atan2(y,down)-math.atan2(lower*math.sin(knee),upper+lower*math.cos(knee))
        pose_delta(rig,side+'Thigh',(math.degrees(thigh),0,0))
        pose_delta(rig,side+'Shin',(math.degrees(knee),0,0))
        pose_delta(rig,side+'Foot',(-math.degrees(thigh+knee),0,0))
        pose_delta(rig,side+'Arm',(-math.degrees(math.atan2(y,down))*.6,0,0))


def main():
    c.reset()
    m=build()
    clips=c.animate(m,motion)
    manifest=c.save_model(m,clips,{'attack_origin':c.socket(m.rig,'Head',(0,-1.07,1.10)),
        'head_or_top':c.socket(m.rig,'Head',(0,-.65,1.40))},
        {'motion_contract':{'ready':'two planted stamps then locked line','run':'bounding charge; 0.18 stance duty per foot',
                            'hit':'wall stagger','playback':'velocity / measured stance speed',
                            'authored_walk_speed_mps':1.143072,'authored_run_speed_mps':6.762,
                            'charge_18mps_playback':18/6.762}})
    c.previews(m,clips,manifest)


if __name__ == '__main__':
    main()
