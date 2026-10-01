# ============================================================================
# hunter_animation_review.py
# PURPOSE:
#   Measure the actual imported hunter skins across every clip, rather than
#   accepting nonempty animation curves. Render labelled six-frame strips from
#   the same evaluated FBX poses so visual review tests the shipped artifact.
# ARCHITECTURAL ROLE:
#   Offline art validator/review tool · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Measure skin displacement, local joint amplitude and alternating feet.
#   - Reject root motion, loop seams and ground penetration in locomotion.
#   - Render six labelled evaluated poses per action to the evidence folder.
# DEPENDENCIES:
#   Blender 5.2 bpy/mathutils, Python standard library; no generator callbacks.
# USAGE NOTES:
#   Validators provide independent per-clip minimum displacement in metres.
#   --render writes strips; it never saves a scene or changes a Unity asset.
# ============================================================================
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
FOLDER = ROOT / 'Logs/AgentValidation/Art/HunterAnimations'


def sample(rig, clip, frame):
    rig.animation_data_create().action = clip
    rig.animation_data.action_slot = clip.slots[0]
    bpy.context.scene.frame_set(int(frame), subframe=frame % 1)
    bpy.context.view_layer.update()


def skin_points(meshes):
    graph = bpy.context.evaluated_depsgraph_get()
    return [obj.matrix_world @ v.co for mesh in meshes
            for obj in [mesh.evaluated_get(graph)] for v in obj.data.vertices]


def bone_point(rig, name, rest):
    bone = rig.pose.bones[name]
    return rig.matrix_world @ bone.matrix @ bone.bone.matrix_local.inverted() @ Vector(rest)


def validate_motion(name, rig, meshes, clips, minima, check):
    if 'LeftThigh' in rig.pose.bones:
        legs = ['LeftThigh', 'RightThigh']
        feet = [(s+'Foot', tuple(rig.data.bones[s+'Foot'].head_local)) for s in ('Left', 'Right')]
    elif name == 'Ticking':
        legs = ['LeftLeg', 'RightLeg']
        feet = [('LeftShin', (-.21, -.09, .05)), ('RightShin', (.18, -.09, .05))]
    elif name == 'Weaver':
        legs = [s+str(i)+'Segment0' for s in ('L', 'R') for i in range(4)]
        feet = []
        for s, sign in (('L', -1), ('R', 1)):
            for i, y in enumerate((-.34, -.12, .12, .34)):
                fan = i/1.5-1
                feet.append((s+str(i)+'Segment2', (sign*.89, y+fan*.43, .035)))
    else:
        legs, feet = ['Jaw'], []
    metrics = {}
    for role, clip in clips.items():
        start, end = clip.frame_range
        poses, vertices, roots, rotations, tips = [], [], [], [], []
        for f in range(int(start), int(end)+1):
            sample(rig, clip, f)
            poses.append([v for b in rig.pose.bones for row in b.matrix for v in row])
            vertices.append(skin_points(meshes))
            roots.append([v for row in rig.matrix_world @ rig.pose.bones['Root'].matrix for v in row])
            rotations.append([(rig.pose.bones[n].parent.matrix.inverted() @ rig.pose.bones[n].matrix).to_quaternion() for n in legs])
            tips.append([bone_point(rig, n, p) for n, p in feet])
        movement = max((a-b).length for frame in vertices for a,b in zip(frame,vertices[0]))
        root_error = max(abs(a-b) for frame in roots for a,b in zip(frame,roots[0]))
        seam = max(abs(a-b) for a,b in zip(poses[0], poses[-1]))
        amplitudes = [math.degrees(max(a[i].rotation_difference(b[i]).angle for a in rotations for b in rotations)) for i in range(len(legs))]
        check(role+'_skin_amplitude', movement >= minima[role], {'metres':movement, 'minimum':minima[role]})
        check(role+'_root_transform_fixed', root_error < 1e-5, root_error)
        clearance = min(p.z for frame in vertices for p in frame)
        check(role+'_no_ground_penetration', clearance > -.006, clearance)
        if role in ('idle','walk','run'):
            check(role+'_matrix_loop_closed', seam < 1e-4, seam)
        if role in ('walk','run'):
            check(role+'_joint_peak_to_peak', min(amplitudes) >= 20, dict(zip(legs,amplitudes)))
            if feet:
                vertical = [[p[i].z for p in tips] for i in range(len(feet))]
                lifts = [max(v)-min(v) for v in vertical]
                check(role+'_each_foot_lifts', min(lifts) > .05, lifts)
                # Opposite sides must exchange lead, not march in unison.
                half = len(feet)//2 if name=='Weaver' else 1
                pairs = [(i, i+half) for i in range(half)] if name=='Weaver' else [(0,1)]
                lead = [[p[a].y-p[b].y for p in tips] for a,b in pairs]
                check(role+'_feet_alternate', all(min(v)<-.08 and max(v)>.08 for v in lead), [[min(v),max(v)] for v in lead])

        metrics[role] = {'skin_displacement_m':movement, 'joint_peak_to_peak_deg':dict(zip(legs,amplitudes)), 'loop_error':seam, 'root_error':root_error}
        if name=='Weaver' and role in ('walk','run'):
            groups = ((0,2,5,7),(1,3,4,6))
            for fraction,raised in ((.25,1),(.75,0)):
                sample(rig,clip,start+(end-start)*fraction)
                heights = [bone_point(rig,n,p).z for n,p in feet]
                check(role+f'_tetrapod_{fraction}',
                      min(heights[i] for i in groups[raised]) > max(heights[i] for i in groups[1-raised])+.05,
                      heights)
    if 'walk' in metrics:
        check('run_more_extreme_than_walk', metrics['run']['skin_displacement_m'] > metrics['walk']['skin_displacement_m']*1.1, [metrics[r]['skin_displacement_m'] for r in ('walk','run')])
    if '--render' in sys.argv:
        render_strips(name, rig, meshes, clips)
        render_distant_attack(name, rig, meshes, clips['attack'])
    print('MOTION_METRICS', name, metrics, flush=True)
    return metrics


def validate_source_motion(source, rig, clips, check):
    with bpy.data.libraries.load(str(source),link=False) as (available,loaded):
        loaded.objects = [rig.name]
        loaded.actions = list(clips)
    original = loaded.objects[0]
    bpy.context.collection.objects.link(original)
    errors = {}
    for (role,exported), authored in zip(clips.items(),loaded.actions):
        error = 0
        first,last = exported.frame_range
        a0,a1 = authored.frame_range
        for frame in range(int(first),int(last)+1):
            sample(original,authored,a0+(frame-first)/(last-first)*(a1-a0))
            # Cache before changing the global scene frame for the imported take.
            matrices = {b.name:original.matrix_world @ b.matrix for b in original.pose.bones}
            sample(rig,exported,frame)
            for bone in rig.pose.bones:
                actual = rig.matrix_world @ bone.matrix
                error = max(error,max(abs(a-b) for row,ref in zip(actual,matrices[bone.name]) for a,b in zip(row,ref)))
        errors[role] = error
    check('source_fbx_motion_agreement',max(errors.values())<1e-4,errors)
    bpy.data.objects.remove(original,do_unlink=True)


def render_strips(name, rig, meshes, clips):
    FOLDER.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    originals = [(obj, obj.hide_render) for obj in scene.objects if obj.type=='MESH']
    # Uniform scale across this body's clips, including anticipation/strike.
    all_points = []
    for clip in clips.values():
        start,end = clip.frame_range
        for fraction in (0,.2,.267,.4,.6,.8,1):
            sample(rig,clip,start+(end-start)*fraction)
            all_points.extend(skin_points(meshes))
    height = max(p.z for p in all_points)
    width = max(max(p[i] for p in all_points)-min(p[i] for p in all_points) for i in (0,1))
    scale = max(height*1.28, width*1.3, .45)
    direction = Vector((4,-6,2.5)).normalized()
    rotation = (-direction).to_track_quat('-Z','Y')
    right = rotation @ Vector((1,0,0))
    target = Vector((0,0,height*.47))
    camera_data = bpy.data.cameras.new('StripCamera')
    camera = bpy.data.objects.new('StripCamera',camera_data)
    scene.collection.objects.link(camera)
    camera.location = target + direction*10
    camera.rotation_euler = rotation.to_euler()
    camera_data.type = 'ORTHO'
    camera_data.sensor_fit = 'VERTICAL'
    camera_data.ortho_scale = scale
    scene.camera = camera
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x,scene.render.resolution_y = 2400,400
    scene.render.resolution_percentage = 100
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE' if name=='Mimic' else 'MATERIAL'
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = 'BOTH'
    scene.display.shading.background_type = 'WORLD'
    scene.world = scene.world or bpy.data.worlds.new('ReviewWorld')
    scene.world.color = (.22,.22,.22)
    scene.view_settings.view_transform = 'Standard'
    scene.render.image_settings.file_format = 'PNG'
    for prop in scene.render.bl_rna.properties:
        if prop.identifier.startswith('use_stamp') and prop.type=='BOOLEAN':
            setattr(scene.render,prop.identifier,False)
    for role,clip in clips.items():
        copies = []
        start,end = clip.frame_range
        fractions = (0,.2,.267,.4,.65,1) if role=='attack' else (0,.2,.4,.6,.8,1)
        for index,fraction in enumerate(fractions):
            sample(rig,clip,start+(end-start)*fraction)
            graph = bpy.context.evaluated_depsgraph_get()
            offset = right*((index-2.5)*scale)
            for mesh in meshes:
                evaluated = mesh.evaluated_get(graph)
                data = bpy.data.meshes.new_from_object(evaluated, depsgraph=graph)
                obj = bpy.data.objects.new('PoseSnapshot',data)
                scene.collection.objects.link(obj)
                obj.matrix_world = mesh.matrix_world.copy()
                obj.location += offset
                copies.append(obj)
            font = bpy.data.curves.new('FrameLabel','FONT')
            font.body = f'{name} {role}  {fraction:.0%}'
            font.align_x = 'CENTER'
            font.size = scale*.045
            text = bpy.data.objects.new('FrameLabel',font)
            scene.collection.objects.link(text)
            text.rotation_euler = rotation.to_euler()
            text.location = target + offset + (rotation @ Vector((0,-scale*.46,0)))
            copies.append(text)
        for obj, hidden in originals:
            obj.hide_render = True
        scene.render.filepath = str(FOLDER/(name+'-'+role+'.png'))
        bpy.ops.render.render(write_still=True)
        for obj, hidden in originals:
            obj.hide_render = hidden
        for obj in copies:
            data = obj.data
            bpy.data.objects.remove(obj,do_unlink=True)
            if isinstance(data,bpy.types.Mesh):
                bpy.data.meshes.remove(data)
            else:
                bpy.data.curves.remove(data)
    bpy.data.objects.remove(camera,do_unlink=True)
    bpy.data.cameras.remove(camera_data)


def render_distant_attack(name, rig, meshes, clip):
    """Fixed 10m, 50mm perspective: never auto-enlarge a small hunter."""
    import numpy as np
    scene = bpy.context.scene
    states = [(obj,obj.hide_render) for obj in scene.objects if obj.type=='MESH']
    for obj,hidden in states:
        obj.hide_render = obj not in meshes
    data = bpy.data.cameras.new('DistanceCamera')
    camera = bpy.data.objects.new('DistanceCamera',data)
    scene.collection.objects.link(camera)
    target = Vector((0,0,1.6))
    camera.location = target + Vector((4,-math.sqrt(84),0))
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z','Y').to_euler()
    data.type, data.lens, data.sensor_width = 'PERSP', 50, 36
    scene.camera = camera
    scene.render.resolution_x = scene.render.resolution_y = 512
    frames = []
    start,end = clip.frame_range
    for index,fraction in enumerate((0,.2,.267,.4,.65,1)):
        sample(rig,clip,start+(end-start)*fraction)
        path = FOLDER/(name+f'-attack-10m-frame{index}.png')
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        image = bpy.data.images.load(str(path),check_existing=False)
        pixels = np.empty(512*512*4,dtype=np.float32)
        image.pixels.foreach_get(pixels)
        frames.append(pixels.reshape((512,512,4)))
        bpy.data.images.remove(image)
    strip = bpy.data.images.new('TenMetreStrip',width=3072,height=512)
    strip.pixels.foreach_set(np.concatenate(frames,axis=1).ravel())
    strip.filepath_raw = str(FOLDER/(name+'-attack-10m.png'))
    strip.file_format = 'PNG'
    strip.save()
    bpy.data.images.remove(strip)
    for obj,hidden in states:
        obj.hide_render = hidden
    bpy.data.objects.remove(camera,do_unlink=True)
    bpy.data.cameras.remove(data)
