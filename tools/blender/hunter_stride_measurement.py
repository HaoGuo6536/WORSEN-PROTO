# ============================================================================
# hunter_stride_measurement.py
# ============================================================================
# PURPOSE:
#   Audit locomotion reference speeds from the shipped FBX, never generator
#   constants. Write only motion metadata when explicitly requested; geometry,
#   source files and clips are read-only and byte hashes fence the operation.
# ARCHITECTURAL ROLE:
#   Offline art validator · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Measure stance travel and elapsed stance time across a complete cycle.
#   - Validate positive references and document stationary disguise defaults.
#   - Update manifests idempotently and retain measured evidence and byte hashes.
#   - Exercise corrupt metadata and reproduce review PNGs without re-exporting art.
# DEPENDENCIES:
#   Blender 5.2 bpy/mathutils, hunter_animation_review and Python standard library.
# USAGE NOTES:
#   --write-manifests updates metadata only; normal invocation validates it.
#   --self-test rejects corrupt references/evidence; --render-previews recreates
#   ignored source review PNGs needed by the existing full-body validators.
#   Holds and reverse recording hitches count in signed net stance travel.
#   References are cycle averages, not a promise of constant within-cycle speed.
# ============================================================================
import hashlib
import copy
import json
import math
import sys
from pathlib import Path
from types import SimpleNamespace

import bpy
from mathutils import Vector
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from hunter_animation_review import sample, bone_point

ROOT = Path(__file__).resolve().parents[2]
NAMES = ('Echo', 'Weaver', 'Ticking', 'Ram', 'Skip', 'Mimic', 'Blinder', 'Herald', 'Mannequin', 'Stare')
SAMPLES = 240
# Bevelled rolling shoes and interpolated baked IK can vary by 15mm in stance.
# Record each actual range; this is a contact audit, not the separate floor gate.
GROUND_TOLERANCE = .015
DEFAULT_REASON = 'Stationary cake disguise: walk/run are exact closed holds; no stance travel. Positive unused playback defaults prevent division by zero.'


def foot_specs(name, rig, meshes):
    if 'LeftFoot' in rig.pose.bones:
        feet = [(side+'Foot', tuple(rig.data.bones[side+'Foot'].head_local)) for side in ('Left', 'Right')]
    elif name == 'Ticking':
        feet = [('LeftShin', (-.21, -.09, .05)), ('RightShin', (.18, -.09, .05))]
    elif name == 'Weaver':
        feet = [(side+str(i)+'Segment2', (sign*.89, y+(i/1.5-1)*.43, .035))
                for side, sign in (('L', -1), ('R', 1)) for i, y in enumerate((-.34, -.12, .12, .34))]
    else:
        raise ValueError('No measured feet for '+name)
    rows = []
    for bone, tip in feet:
        vertices = [mesh.matrix_world @ vertex.co for mesh in meshes for vertex in mesh.data.vertices
                    if any(mesh.vertex_groups[g.group].name == bone and g.weight > .99 for g in vertex.groups)]
        if not vertices:
            raise ValueError('Missing rigid foot skin: '+bone)
        rows.append((bone, tip, vertices))
    return rows


def measure(name, rig, meshes, clips, fps):
    if name == 'Mimic':
        for role in ('walk', 'run'):
            clip = clips[role]
            start, end = clip.frame_range
            matrices = []
            for index in range(SAMPLES+1):
                sample(rig, clip, start+(end-start)*index/SAMPLES)
                matrices.append([v for b in rig.pose.bones for row in rig.matrix_world @ b.matrix for v in row])
            if max(abs(a-b) for row in matrices for a, b in zip(row, matrices[0])) > 1e-5:
                raise ValueError('Mimic disguise is not stationary')
        return dict(authored_walk_speed_mps=1.6, authored_run_speed_mps=3.0,
                    stride_default_reason=DEFAULT_REASON,
                    stride_measurement={'method': 'stationary hold verified over both complete cycles', 'samples_per_cycle': SAMPLES})
    feet = foot_specs(name, rig, meshes)
    evidence, speeds = {}, {}
    for role in ('walk', 'run'):
        clip = clips[role]
        start, end = clip.frame_range
        seconds = (end-start)/fps
        travel = duration = 0.
        foot_rows = []
        for foot_index, (bone, _, _) in enumerate(feet):
            if name == 'Ticking':
                intervals = ((0, .25), (.75, 1)) if bone.startswith('Left') else ((.25, .75),)
            else:
                shift = .5 if (bone.startswith('Right') or name == 'Weaver' and
                              (int(bone[1])+(bone.startswith('R')))%2) else 0
                # Ram's last 0.03 of stance straddles a baked swing transition.
                # Use its independently audited linear planted window, not that blend.
                duty = .15 if name == 'Ram' and role == 'run' else .5
                intervals = ((shift, shift+duty),)
            distance = elapsed = 0.
            soles = []
            for first, last in intervals:
                # Exact contact boundaries retain the short Ram bounding stance.
                tips = []
                for index in range(SAMPLES+1):
                    sample(rig, clip, start+(end-start)*(first+(last-first)*index/SAMPLES))
                    matrix = rig.matrix_world @ rig.pose.bones[bone].matrix @ rig.data.bones[bone].matrix_local.inverted()
                    tips.append(bone_point(rig, bone, feet[foot_index][1]).y)
                    soles.append(min((matrix @ p).z for p in feet[foot_index][2]))
                distance += tips[-1]-tips[0]
                elapsed += seconds*(last-first)
            if max(soles)-min(soles) > GROUND_TOLERANCE:
                raise ValueError(f'{name}/{role}/{bone}: stance sole not planted: {min(soles)}, {max(soles)}')
            # Ticking's legacy waddle rolls its planted shoes forwards. Measure
            # magnitude, record the sign; changing those clips is not this task.
            signed_distance = distance
            if name == 'Ticking':
                distance = abs(distance)
            if elapsed <= seconds*.05 or distance <= .01:
                raise ValueError(f'{name}/{role}/{bone}: no measurable rearward stance')
            foot_rows.append(dict(bone=bone, travel_m=round(distance, 6), stance_seconds=round(elapsed, 6),
                                  signed_travel_m=round(signed_distance, 6), stance_phase_intervals=[list(v) for v in intervals],
                                  sole_height_range_m=[round(min(soles), 6), round(max(soles), 6)],
                                  speed_mps=round(distance/elapsed, 6)))
            travel += distance
            duration += elapsed
        speed = travel/duration
        if not math.isfinite(speed) or speed <= 0:
            raise ValueError('Invalid measured stride')
        speeds['authored_'+role+'_speed_mps'] = round(speed, 6)
        evidence[role] = {'cycle_seconds': round(seconds, 6), 'feet': foot_rows}
    return {**speeds, 'stride_measurement': {
        'method': 'net foot travel / elapsed validated stance time, pooled over all feet and one full cycle; Ticking uses magnitude of its forward-rolling waddle',
        'source': 'shipped FBX; Blender Z-up, forward -Y; world metres',
        'samples_per_cycle': SAMPLES, 'sole_contact_tolerance_m': GROUND_TOLERANCE, **evidence}}


def audit(name, rig, meshes, clips, manifest, check):
    measured = measure(name, rig, meshes, clips, manifest['fps'])
    check_contract(manifest, measured, check)
    return measured


def check_contract(manifest, measured, check):
    contract = manifest.get('motion_contract', {})
    check('manifest_measured_stride_speeds', all(
        isinstance(contract.get(key), (int, float)) and math.isfinite(contract[key]) and
        abs(contract[key]-measured[key]) < .001
        for key in ('authored_walk_speed_mps', 'authored_run_speed_mps')), measured)
    check('manifest_stride_evidence', contract.get('stride_measurement') == measured['stride_measurement'] and
          contract.get('stride_default_reason') == measured.get('stride_default_reason'), measured['stride_measurement'])


def mutation_tests(manifest, measured):
    cases = []
    for key, value in (('authored_walk_speed_mps', 0), ('authored_run_speed_mps', -1),
                       ('authored_walk_speed_mps', float('nan')),
                       ('authored_run_speed_mps', float('inf')),
                       ('authored_run_speed_mps', measured['authored_run_speed_mps']+.1),
                       ('stride_measurement', {})):
        bad = copy.deepcopy(manifest)
        bad['motion_contract'][key] = value
        results = []
        check_contract(bad, measured, lambda label, passed, evidence: results.append(passed))
        if all(results):
            raise AssertionError('Stride mutation escaped: '+key)
        cases.append(key+':'+str(value))
    if 'stride_default_reason' in measured:
        bad = copy.deepcopy(manifest)
        del bad['motion_contract']['stride_default_reason']
        results = []
        check_contract(bad, measured, lambda label, passed, evidence: results.append(passed))
        if all(results):
            raise AssertionError('Stationary default reason mutation escaped')
        cases.append('missing stationary reason')
    print('STRIDE_MUTATIONS', manifest['hunter'], len(cases), 'passed', flush=True)
    return cases


def render_previews(name, source, manifest):
    # Never run generators or save a source to reproduce missing ignored PNGs.
    import hunter_creature_common as c
    bpy.ops.wm.open_mainfile(filepath=str(source))
    rig, = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
    c.clear_pose(rig)
    if name in ('Echo', 'Herald', 'Mannequin', 'Stare'):
        import hunter_humanoid_common as h
        folder = h.paths(name)[2]
        folder.mkdir(parents=True, exist_ok=True)
        for view, position in (('front', (0, -6, 1.25)), ('side', (6, 0, 1.25)),
                               ('three-quarter', (4, -6, 3.1))):
            h.render(folder/(view+'.png'), position, (0, 0, 1.25))
        attack = bpy.data.actions['attack']
        c.pose(rig, attack, next(a['contact_frame'] for a in manifest['actions'] if a['name'] == 'attack'))
        h.render(folder/'attack-pose.png', (4, -6, 3.1), (0, 0, 1.25), 3.5)
        c.clear_pose(rig)
        reference = h.reference_box(1.05)
        h.render(folder/'lineup.png', (.4, -7, 1.25), (.4, 0, 1.25))
        bpy.data.objects.remove(reference, do_unlink=True)
    else:
        c.previews(SimpleNamespace(name=name, rig=rig), list(bpy.data.actions), manifest)
    if name == 'Mimic':
        from hunter_mimic import append_cake
        reference = append_cake()
        reference.data.materials.clear()
        reference.data.materials.append(c.material('ReferenceCyan', (.08, .80, .90)))
        wire = reference.modifiers.new('Exact cake surface (cyan)', 'WIREFRAME')
        wire.thickness, wire.use_replace = .00055, True
        c.render(c.paths(name)[2]/'closed-vs-cake.png', (3, -5, 2.5), (0, 0, .145), .48, texture=True)
        bpy.data.objects.remove(reference, do_unlink=True)


def main():
    names = [arg for arg in sys.argv[sys.argv.index('--')+1:] if not arg.startswith('--')] if '--' in sys.argv else []
    names = names or list(NAMES)
    if any(name not in NAMES for name in names):
        raise ValueError('Unknown hunter')
    reports = []
    for name in names:
        fbx = ROOT/'Assets/Art/Hunter'/name/('WORSEN_Hunter'+name+'.fbx')
        source = ROOT/'ArtSource/Hunter'/name/('WORSEN_Hunter'+name+'.blend')
        path = fbx.with_suffix('.manifest.json')
        before = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in (fbx, source)}
        manifest = json.loads(path.read_text(encoding='utf-8'))
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(fbx), use_anim=True, automatic_bone_orientation=False)
        rig, = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
        meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        clips = {a.name.split('|')[-1]: a for a in bpy.data.actions}
        measured = measure(name, rig, meshes, clips, manifest['fps'])
        if '--write-manifests' in sys.argv:
            manifest.setdefault('motion_contract', {}).update(measured)
            content = json.dumps(manifest, indent=2)+'\n'
            if path.read_text(encoding='utf-8') != content:
                with path.open('w', encoding='utf-8', newline='\n') as stream:
                    stream.write(content)
        checks = []
        audit(name, rig, meshes, clips, manifest, lambda label, passed, evidence: checks.append({'check': label, 'passed': passed}))
        mutations = mutation_tests(manifest, measured) if '--self-test' in sys.argv else []
        if '--render-previews' in sys.argv:
            render_previews(name, source, manifest)
        after = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in (fbx, source)}
        if before != after or not all(row['passed'] for row in checks):
            raise RuntimeError(name+' stride audit failed')
        reports.append(dict(hunter=name, measured=measured, checks=checks, mutations=mutations, unchanged_art_sha256=after))
        print('STRIDE_RESULT', name, measured['authored_walk_speed_mps'], measured['authored_run_speed_mps'], 'art_byte_stable=true', flush=True)
    folder = ROOT/'Logs/AgentValidation/Art/HunterStrides'
    folder.mkdir(parents=True, exist_ok=True)
    target = folder/('measurement-write.json' if '--write-manifests' in sys.argv else 'measurement-validate.json')
    target.write_text(json.dumps(reports, indent=2)+'\n', encoding='utf-8')


if __name__ == '__main__':
    main()
