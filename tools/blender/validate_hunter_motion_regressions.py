# ============================================================================
# validate_hunter_motion_regressions.py
# PURPOSE:
#   Prove that the motion gate rejects the original statue failure mode and
#   independent root, loop and gait regressions. Mutations are confined to
#   fresh in-memory FBX imports; shipped sources, exports and metas are untouched.
# ARCHITECTURAL ROLE:
#   Offline art test fixture · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Freeze all seven imported bodies and require six amplitude failures each.
#   - Inject a broken seam, root drift and in-phase biped feet independently.
#   - Write counted pass/fail evidence without weakening production thresholds.
# DEPENDENCIES:
#   Blender 5.2, hunter_animation_review and Python standard library.
# USAGE NOTES:
#   Run factory-startup/headless with python-exit-code 1. No Unity operations.
# ============================================================================
import contextlib
import io
import json
import sys
from pathlib import Path
import bpy
sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from hunter_animation_review import ROOT, FOLDER, validate_motion


def load(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/f'Assets/Art/Hunter/{name}/WORSEN_Hunter{name}.fbx'))
    rig = next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    meshes = [o for o in bpy.context.scene.objects if o.type=='MESH']
    clips = {a.name.split('|')[-1]:a for a in bpy.data.actions}
    return rig,meshes,clips


def curves(clip):
    return [curve for layer in clip.layers for strip in layer.strips
            for bag in strip.channelbags for curve in bag.fcurves]


def failures(name,rig,meshes,clips):
    failed = []
    # These deliberately permissive displacement minima still reject a statue;
    # each production validator separately supplies its stricter body thresholds.
    with contextlib.redirect_stdout(io.StringIO()):
        validate_motion(name,rig,meshes,clips,{n:.001 for n in clips},
                        lambda label,ok,detail: failed.append(label) if not ok else None)
    return failed


def main():
    results = []
    for name in ('Echo','Herald','Mannequin','Mimic','Stare','Ticking','Weaver'):
        rig,meshes,clips = load(name)
        for clip in clips.values():
            for curve in curves(clip):
                first = curve.keyframe_points[0].co.y
                for key in curve.keyframe_points:
                    key.co.y = first
                curve.update()
        failed = failures(name,rig,meshes,clips)
        expected = [n+'_skin_amplitude' for n in clips]
        results.append({'test':name+'_frozen_six_clips','passed':all(n in failed for n in expected),'rejected':failed})
    for label,bone,expected in (('seam','Chest','walk_matrix_loop_closed'),
                                ('root','Root','walk_root_transform_fixed')):
        rig,meshes,clips = load('Echo')
        curve = next(c for c in curves(clips['walk']) if c.data_path==f'pose.bones["{bone}"].location' and c.array_index==0)
        curve.keyframe_points[-1].co.y += .25
        curve.update()
        failed = failures('Echo',rig,meshes,clips)
        results.append({'test':label+'_mutation','passed':expected in failed,'rejected':failed})
    rig,meshes,clips = load('Echo')
    for role in ('walk','run'):
        channels = curves(clips[role])
        for curve in channels:
            if not any('Right'+part in curve.data_path for part in ('Thigh','Shin','Foot')):
                continue
            source = next(c for c in channels if c.data_path==curve.data_path.replace('Right','Left') and c.array_index==curve.array_index)
            for target,key in zip(curve.keyframe_points,source.keyframe_points):
                target.co.y = key.co.y
            curve.update()
    failed = failures('Echo',rig,meshes,clips)
    results.append({'test':'in_phase_feet','passed':all(r+'_feet_alternate' in failed for r in ('walk','run')),'rejected':failed})
    report = {'passed':sum(r['passed'] for r in results),'failed':sum(not r['passed'] for r in results),'tests':results}
    FOLDER.mkdir(parents=True,exist_ok=True)
    (FOLDER/'mutation-tests.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('MOTION_REGRESSIONS',json.dumps(report),flush=True)
    if report['failed']:
        raise RuntimeError('Motion gate accepted a regression')


if __name__=='__main__':
    main()
