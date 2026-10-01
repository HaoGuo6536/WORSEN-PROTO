# ============================================================================
# validate_hunter_ram.py
# PURPOSE:
#   Re-import the new rigid hunter exports and measure their actual skins.
#   Verify grounded motion and record natural stance speed for runtime playback.
# ARCHITECTURAL ROLE:
#   Offline art validator · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Check Generic skeleton, six takes, detail budget and source/FBX agreement.
#   - Measure planted feet, stamp/attack limb amplitudes and archetype landmarks.
#   - Render the existing animation and dim-light review scripts on request.
# DEPENDENCIES:
#   Blender 5.2 and shared creature/animation/detail review utilities.
# USAGE NOTES:
#   --render writes clip strips; --detail invokes the existing Cycles review.
#   No generator callback is used as validation truth. Never runs Unity.
# ============================================================================
import math
import sys
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import hunter_creature_common as c
from hunter_animation_review import validate_motion, bone_point


def validate(name, height_range, minima, landmarks):
    v=c.Validation(name)
    try:
        bones={'Root':None,'Hips':'Root','Chest':'Hips','Head':'Chest'}
        for side in ('Left','Right'):
            for child,parent in (('Arm','Chest'),('Forearm',side+'Arm'),('Hand',side+'Forearm'),
                                 ('Thigh','Hips'),('Shin',side+'Thigh'),('Foot',side+'Shin')):
                bones[side+child]=parent
        v.shared(bones,height_range,lambda a:c.center(a,landmarks[0]).y < c.center(a,landmarks[1]).y)
        v.report['motion']=validate_motion(name,v.rig,v.meshes,v.clips,minima,v.check)
        names={o.name for o in v.meshes}
        v.check('identity_landmarks',set(landmarks)<=names,landmarks)
        speeds={}
        for role in ('walk','run'):
            clip=v.clips[role]
            start,end=clip.frame_range
            rest=tuple(v.rig.data.bones['LeftFoot'].head_local)
            tips=[]
            fractions=(0,.05,.10,.15) if name=='Ram' and role=='run' else (0,.1,.2,.3,.4)
            for fraction in fractions:
                c.pose(v.rig,clip,start+(end-start)*fraction)
                tips.append(bone_point(v.rig,'LeftFoot',rest))
            speed=(tips[-1].y-tips[0].y)/((end-start)*fractions[-1]/c.FPS)
            variation=max(p.z for p in tips)-min(p.z for p in tips)
            v.check(role+'_planted_stance',variation<.001 and speed>.1,{'height_variation_m':variation,'speed_mps':speed})
            speeds[role]=speed
        v.report['authored_speed_mps']=speeds
        v.check('manifest_authored_speeds',all(abs(speeds[role]-v.manifest['motion_contract']['authored_'+role+'_speed_mps'])<.001
                for role in ('walk','run')),speeds)
        if name=='Ram':
            v.report['charge_18mps_playback']=18/speeds['run']
            c.clear_pose(v.rig)
            box=c.bounds(c.points(v.meshes))
            v.check('broad_low_front',box[0][1]-box[0][0]>1.3 and box[2][1]<1.7 and c.center(v,'ImpactSkull').z<c.center(v,'ShoulderMass').z,box)
            foot=tuple(v.rig.data.bones['LeftFoot'].head_local)
            c.pose(v.rig,v.clips['ready'],7)
            lifted=bone_point(v.rig,'LeftFoot',foot).z
            c.pose(v.rig,v.clips['ready'],25)
            planted=bone_point(v.rig,'LeftFoot',foot).z
            v.check('stamp_lifts_then_plants',lifted>planted+.04,[lifted,planted])
            for role in ('ready','attack'):
                amplitude=limb_amplitudes(v.rig,v.clips[role])
                v.check(role+'_articulated_limb_at_least_ten_degrees',max(amplitude.values())>=10,amplitude)
            # Hold all attack limb channels constant, leaving the torso/head
            # motion intact: batch 27's nod-only charge must fail this gate.
            import bpy
            frozen=v.clips['attack'].copy()
            try:
                for layer in frozen.layers:
                    for strip in layer.strips:
                        for bag in strip.channelbags:
                            for curve in bag.fcurves:
                                if any('"'+side+part+'"' in curve.data_path for side in ('Left','Right')
                                       for part in ('Arm','Forearm','Hand','Thigh','Shin','Foot')):
                                    value=curve.keyframe_points[0].co.y
                                    for key in curve.keyframe_points:
                                        key.co.y=value
                                    curve.update()
                amplitude=limb_amplitudes(v.rig,frozen)
                v.check('nod_only_attack_mutation_rejected',max(amplitude.values())<10,amplitude)
            finally:
                c.clear_pose(v.rig)
                bpy.data.actions.remove(frozen)
        elif name=='Blinder':
            c.pose(v.rig,v.clips['ready'],25)
            arm=v.rig.pose.bones['RightArm'].matrix.to_quaternion()
            c.pose(v.rig,v.clips['attack'],13)
            swing=math.degrees(arm.rotation_difference(v.rig.pose.bones['RightArm'].matrix.to_quaternion()).angle)
            v.check('throw_changes_arm_by_90_degrees',swing>90,swing)
            hand=bone_point(v.rig,'RightHand',tuple(v.rig.data.bones['RightHand'].head_local))
            v.check('release_forward',hand.y<-.4,list(hand))
        v.source_motion()
    except Exception as exc:
        v.check('exception',False,repr(exc))
    v.finish()
    if '--detail' in sys.argv:
        from hunter_detail_review import review
        review(name)


def limb_amplitudes(rig,clip):
    names=[side+part for side in ('Left','Right') for part in ('Arm','Forearm','Hand','Thigh','Shin','Foot')]
    start,end=clip.frame_range
    rotations={name:[] for name in names}
    # Same 25 fractions as the native PlayableGraph test, independent of generator.
    for sample in range(25):
        c.pose(rig,clip,start+(end-start)*sample/24)
        for name in names:
            bone=rig.pose.bones[name]
            rotations[name].append((bone.parent.matrix.inverted() @ bone.matrix).to_quaternion())
    return {name:math.degrees(max(a.rotation_difference(b).angle for a in values for b in values))
            for name,values in rotations.items()}


if __name__=='__main__':
    validate('Ram',(1.5,1.7),dict(idle=.03,walk=.35,run=.5,ready=.15,attack=.15,hit=.2),
             ('ImpactSkull','ShoulderMass','LeftHorn','RightHorn','LeftSplitHoof1'))
