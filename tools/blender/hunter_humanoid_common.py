# ============================================================================
# hunter_humanoid_common.py
# PURPOSE:
#   Build and verify the four original SPEC-005 humanoid prototype exports.
#   Share only offline art mechanics so every model retains its own silhouette
#   and movement authoring. No Unity process or external art is used.
# ARCHITECTURAL ROLE:
#   Offline art tooling (outside runtime layers) · Hunter art.
# KEY RESPONSIBILITIES:
#   - Assemble primitive meshes with rigid bone assignments.
#   - Bake six in-place actions and export manifest/source/FBX.
#   - Render neutral, true-scale Workbench previews.
#   - Inspect imported motion, 3–8k triangle/2–4 material budgets and reproducibility.
# DEPENDENCIES:
#   Blender 5.2 bpy/mathutils, Python standard library; model callbacks only.
# USAGE NOTES:
#   Paths derive from this file's worktree. Never run Unity or access Library.
#   The manifest uses sRGB colours and explicitly labelled socket coordinates.
# ============================================================================
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Euler, Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
FPS = 30
NAMES = ('idle', 'walk', 'run', 'ready', 'attack', 'hit')
HEIGHTS = {'Echo': 1.8, 'Mannequin': 1.85, 'Stare': 2.3, 'Herald': 2.5}


def paths(name):
    stem = 'WORSEN_Hunter' + name
    return (ROOT / 'ArtSource/Hunter' / name / (stem + '.blend'),
            ROOT / 'Assets/Art/Hunter' / name / (stem + '.fbx'),
            ROOT / 'Logs/AgentValidation/Art' / ('Hunter' + name))


def linear(value):
    return value / 12.92 if value <= .04045 else ((value + .055) / 1.055) ** 2.4


def srgb(value):
    return value * 12.92 if value <= .0031308 else 1.055 * value ** (1 / 2.4) - .055


def rounded(values):
    return [round(float(v), 6) for v in values]


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    scene.render.fps = FPS


class Body:
    """One deterministic disconnected mesh; every primitive owns one bone."""
    def __init__(self, name, bones, palette):
        self.name, self.bones, self.palette = name, bones, palette
        self.parts = []
        self.materials = []
        for part, color, emission, strength in palette:
            material = bpy.data.materials.new('M_Hunter' + name + '_' + part)
            material.diffuse_color = (*[linear(c) for c in color], 1)
            material.use_nodes = True
            shader = material.node_tree.nodes.get('Principled BSDF')
            shader.inputs['Base Color'].default_value = material.diffuse_color
            shader.inputs['Roughness'].default_value = 1
            shader.inputs['Emission Color'].default_value = (*[linear(c) for c in emission], 1)
            shader.inputs['Emission Strength'].default_value = strength
            self.materials.append(material)

    def part(self, obj, bone, material):
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        obj.data.materials.clear()
        obj.data.materials.append(self.materials[material])
        for polygon in obj.data.polygons:
            polygon.use_smooth = False
        obj.vertex_groups.new(name=bone).add(list(range(len(obj.data.vertices))), 1, 'REPLACE')
        self.parts.append(obj)
        return obj

    def box(self, bone, center, size, material=0):
        bpy.ops.mesh.primitive_cube_add(size=1, location=center)
        obj = bpy.context.object
        obj.scale = size
        return self.part(obj, bone, material)

    def egg(self, bone, center, size, material=0):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=1, location=center)
        obj = bpy.context.object
        obj.scale = tuple(v / 2 for v in size)
        return self.part(obj, bone, material)

    def link(self, bone, start, end, radius, material=0, radius_end=None):
        delta = Vector(end) - Vector(start)
        bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=radius,
            radius2=radius if radius_end is None else radius_end,
            depth=delta.length, location=(Vector(start) + Vector(end)) / 2)
        obj = bpy.context.object
        obj.rotation_euler = delta.to_track_quat('Z', 'Y').to_euler()
        return self.part(obj, bone, material)

    def cloth(self, bone, rings, material=0, segments=12):
        # Polygonal cone/frustum rings; varying centres create authored lean.
        verts = [(cx + rx * math.cos(i * 2 * math.pi / segments),
                  cy + ry * math.sin(i * 2 * math.pi / segments), z)
                 for cx, cy, z, rx, ry in rings for i in range(segments)]
        faces = [tuple(reversed(range(segments)))]
        for ring in range(len(rings) - 1):
            a, b = ring * segments, (ring + 1) * segments
            faces.extend((a+i, a+(i+1)%segments, b+(i+1)%segments, b+i) for i in range(segments))
        faces.append(tuple(range(len(verts) - segments, len(verts))))
        data = bpy.data.meshes.new('Cloth')
        data.from_pydata(verts, [], faces)
        data.update()
        obj = bpy.data.objects.new('Cloth', data)
        bpy.context.collection.objects.link(obj)
        return self.part(obj, bone, material)

    def finish(self):
        bpy.ops.object.select_all(action='DESELECT')
        for part in self.parts:
            part.select_set(True)
        bpy.context.view_layer.objects.active = self.parts[0]
        bpy.ops.object.join()
        mesh = bpy.context.object
        mesh.name = 'Hunter' + self.name + 'Body'
        bpy.ops.object.select_all(action='DESELECT')
        data = bpy.data.armatures.new('Hunter' + self.name + 'Rig')
        rig = bpy.data.objects.new('Hunter' + self.name, data)
        bpy.context.collection.objects.link(rig)
        bpy.context.view_layer.objects.active = rig
        rig.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        for name, parent, head in self.bones:
            bone = data.edit_bones.new(name)
            bone.head, bone.tail = head, Vector(head) + Vector((0, 0, .1))
            if name == 'Root':
                bone.tail = (0, -.1, 0)  # Keep Hips disconnected on FBX re-import.
            bone.align_roll(Vector((0, -1, 0)))
            if parent:
                bone.parent = data.edit_bones[parent]
        bpy.ops.object.mode_set(mode='OBJECT')
        mesh.parent = rig
        mesh.modifiers.new('RigidSkin', 'ARMATURE').object = rig
        return rig, mesh


def humanoid(height, lean=0, shoulder=.25, long_arm=False, ribs=False):
    h = height
    bones = [('Root', None, (0, 0, 0)), ('Hips', 'Root', (0, 0, h*.49)),
             ('Chest', 'Hips', (lean*.55, 0, h*.70)),
             ('Head', 'Chest', (lean, 0, h*.87))]
    for side, sign in (('Left', 1), ('Right', -1)):
        z = h * .79 + (sign * .065 if long_arm else 0)
        x = lean*.65 + sign*shoulder
        wrist = h*(.26 if long_arm and sign == 1 else .40)
        bones += [(side+'Arm', 'Chest', (x, 0, z)),
                  (side+'Forearm', side+'Arm', (x+sign*.035, 0, (z+wrist)/2)),
                  (side+'Hand', side+'Forearm', (x+sign*.07, -.015, wrist)),
                  (side+'Thigh', 'Hips', (sign*.105, 0, h*.49)),
                  (side+'Shin', side+'Thigh', (sign*.105, 0, h*.27)),
                  (side+'Foot', side+'Shin', (sign*.105, 0, .10))]
        if ribs:
            bones.append((side+'Ribs', 'Chest', (sign*.23, .045, h*.72)))
    return bones


def limbs(body, jointed=False, material=0, joint_material=0):
    heads = {n: Vector(p) for n, _, p in body.bones}
    for side in ('Left', 'Right'):
        for start, end, radius in (('Arm', 'Forearm', .065), ('Forearm', 'Hand', .05),
                                   ('Thigh', 'Shin', .075), ('Shin', 'Foot', .052)):
            a, b = heads[side+start], heads[side+end]
            axis = (b-a).normalized()
            body.link(side+start, a+axis*.035, b-axis*.035, radius, material, radius*.8)
            if jointed:
                body.egg(side+start, a, (.115, .115, .115), joint_material)
        hand = heads[side+'Hand']
        body.box(side+'Hand', hand+Vector((0, -.012, -.07)), (.09, .065, .17), material)
        body.box(side+'Foot', (heads[side+'Foot'].x, -.07, .05), (.145, .29, .10), material)


def clear_pose(rig):
    rig.animation_data_clear()
    for bone in rig.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
        bone.rotation_mode = 'QUATERNION'
    bpy.context.view_layer.update()


def rotate(rig, name, degrees):
    bone = rig.pose.bones[name]
    basis = bone.bone.matrix_local.to_3x3()
    rotation = Euler(tuple(math.radians(v) for v in degrees), 'XYZ').to_matrix()
    bone.rotation_quaternion = (basis.inverted() @ rotation @ basis).to_quaternion()


def envelope(t, peak=.4):
    return t / peak if t <= peak else max(0, (1-t)/(1-peak))


def animate(rig, lengths, author):
    clips = []
    for name in NAMES:
        clear_pose(rig)
        clip = bpy.data.actions.new(name)
        clip.use_fake_user = True
        rig.animation_data_create().action = clip
        for frame in range(lengths[name]+1):
            for bone in rig.pose.bones:
                bone.matrix_basis = Matrix.Identity(4)
            author(rig, name, frame/lengths[name])
            for bone in rig.pose.bones:
                for field in ('location', 'rotation_quaternion', 'scale'):
                    bone.keyframe_insert(field, frame=frame, group=bone.name)
        clips.append(clip)
        for layer in clip.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            key.interpolation = 'LINEAR'
    clear_pose(rig)
    return clips


def set_action(rig, clip, frame):
    rig.animation_data_create().action = clip
    if clip.slots:
        rig.animation_data.action_slot = clip.slots[0]
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()


def render(path, position, target, scale=3.1, wide=False):
    scene = bpy.context.scene
    data = bpy.data.cameras.new('ReviewCamera')
    camera = bpy.data.objects.new('ReviewCamera', data)
    scene.collection.objects.link(camera)
    camera.location = position
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z', 'Y').to_euler()
    data.type, data.ortho_scale = 'ORTHO', scale
    data.sensor_fit = 'VERTICAL'
    scene.camera = camera
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x, scene.render.resolution_y = (1600, 800) if wide else (800, 800)
    scene.render.resolution_percentage = 100
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = 'BOTH'
    scene.display.shading.background_type = 'WORLD'
    scene.world = scene.world or bpy.data.worlds.new('Neutral')
    scene.world.color = (.18, .18, .18)
    scene.view_settings.view_transform = 'Standard'
    for prop in scene.render.bl_rna.properties:
        if prop.identifier.startswith('use_stamp') and prop.type == 'BOOLEAN':
            setattr(scene.render, prop.identifier, False)
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    bpy.data.cameras.remove(data)


def reference_box(x):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, .1, .9))
    box = bpy.context.object
    box.name = 'Reference_1.8m'
    box.scale = (.36, .36, 1.8)
    material = bpy.data.materials.new('ReviewReference')
    material.diffuse_color = (.32, .32, .32, 1)
    box.data.materials.append(material)
    return box


def generate(name, build, author, lengths):
    source, fbx, previews = paths(name)
    for directory in (source.parent, fbx.parent, previews):
        directory.mkdir(parents=True, exist_ok=True)
    reset()
    body = build()
    rig, mesh = body.finish()
    clips = animate(rig, lengths, author)
    points = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
    mesh.data.calc_loop_triangles()
    materials = [{'name': m.name, 'base_color_srgb': list(c)+[1],
                  'emission_color_srgb': list(e)+[1], 'emission_strength': s}
                 for m, (_, c, e, s) in zip(body.materials, body.palette)]
    actions = [{'name': n, 'frames': [0, lengths[n]], 'loop': n in NAMES[:3],
                **({'contact_frame': round(lengths[n]*.4)} if n == 'attack' else {})} for n in NAMES]
    attack_bone = 'Chest' if name == 'Herald' else 'LeftHand'
    manifest = {'schema_version': 1, 'hunter': name, 'fps': FPS,
                'height_m': round(max(p.z for p in points)-min(p.z for p in points), 6),
                'width_m': round(max(p.x for p in points)-min(p.x for p in points), 6),
                'triangles': len(mesh.data.loop_triangles),
                'bones': [{'name': n, 'parent': p, 'head_m': list(h)} for n,p,h in body.bones],
                'actions': actions, 'materials': materials,
                'coordinate_system': 'Blender Z-up, forward -Y, metres',
                'socket_space': 'bone-local Blender rest axes, metres; convert with FBX bone basis in Unity',
                'unity_import': {'bakeAxisConversion': False, 'rig': 'Generic', 'root_motion': False},
                'sockets': {'attack_origin': {'bone': attack_bone, 'local_offset': [0, 0, .10]},
                            'head_or_top': {'bone': 'Head', 'local_offset': [0, .10, 0]}}}
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.animation_data_create()
    for clip in clips:
        track = rig.animation_data.nla_tracks.new()
        track.name = clip.name
        track.strips.new(clip.name, 0, clip)
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True,
        object_types={'ARMATURE', 'MESH'}, global_scale=1, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, use_armature_deform_only=True, add_leaf_bones=False,
        use_mesh_modifiers=True, mesh_smooth_type='FACE', use_triangles=True,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1, bake_anim_simplify_factor=0, use_custom_props=False)
    fbx.with_suffix('.manifest.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    clear_pose(rig)
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = 0, max(lengths.values())
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    for view, position in (('front', (0,-6,1.25)), ('side', (6,0,1.25)),
                           ('three-quarter', (4,-6,3.1))):
        render(previews/(view+'.png'), position, (0,0,1.25))
    set_action(rig, next(c for c in clips if c.name=='attack'), round(lengths['attack']*.4))
    render(previews/'attack-pose.png', (4,-6,3.1), (0,0,1.25), 3.5)
    clear_pose(rig)
    box = reference_box(1.05)
    render(previews/'lineup.png', (.4,-7,1.25), (.4,0,1.25))
    bpy.data.objects.remove(box, do_unlink=True)
    print('GENERATED '+name+' '+json.dumps({k: manifest[k] for k in ('height_m','width_m','triangles')}))


def combined_lineup():
    reset()
    for name, x in zip(HEIGHTS, (-2.1,-.7,.7,2.1)):
        source, _, _ = paths(name)
        with bpy.data.libraries.load(str(source), link=False) as (available, loaded):
            loaded.objects = available.objects
        for obj in loaded.objects:
            bpy.context.collection.objects.link(obj)
            if obj.type == 'ARMATURE':
                clear_pose(obj)
                obj.location.x = x
    reference_box(3.15)
    bpy.context.view_layer.update()
    render(ROOT/'Logs/AgentValidation/Art/HunterLineup-hunter-art-a.png', (.4,-10,1.3), (.4,0,1.3), 3.25, True)


def validate(name, expected_lengths, motion_minima):
    """Fresh import measurements, not a checksum of the generator's claims."""
    source, fbx, previews = paths(name)
    rows, report = [], {'hunter': name}
    def check(label, passed, detail):
        rows.append({'check': label, 'passed': bool(passed), 'detail': detail})
    try:
        manifest = json.loads(fbx.with_suffix('.manifest.json').read_text(encoding='utf-8'))
        reset()
        bpy.ops.import_scene.fbx(filepath=str(fbx), use_anim=True,
            automatic_bone_orientation=False, ignore_leaf_bones=False)
        rigs = [o for o in bpy.context.scene.objects if o.type=='ARMATURE']
        meshes = [o for o in bpy.context.scene.objects if o.type=='MESH']
        check('objects', len(rigs)==1 and len(meshes)==1, [o.name for o in bpy.context.scene.objects])
        if len(rigs)!=1 or not meshes:
            raise ValueError('Missing single rig or geometry')
        rig = rigs[0]
        clear_pose(rig)
        bones = {b.name: b.parent.name if b.parent else None for b in rig.data.bones}
        expected_bones = {b['name']: b['parent'] for b in manifest['bones']}
        check('bone hierarchy and count', bones==expected_bones and len(bones)==(18 if name=='Herald' else 16), bones)
        root = rig.matrix_world @ rig.data.bones['Root'].head_local
        check('root at origin', root.length<1e-5 and bones['Root'] is None, rounded(root))
        bone_error = max((rig.matrix_world @ rig.data.bones[b['name']].head_local-Vector(b['head_m'])).length for b in manifest['bones'])
        check('manifest bone positions', bone_error<1e-4, bone_error)
        points, signature, invalid, triangles, bind_error = [], [], [], 0, 0
        for mesh in meshes:
            mesh.data.calc_loop_triangles()
            triangles += len(mesh.data.loop_triangles)
            check('flat shading', all(not p.use_smooth for p in mesh.data.polygons), mesh.name)
            check('single skin', mesh.parent==rig and len([m for m in mesh.modifiers if m.type=='ARMATURE' and m.object==rig])==1, mesh.name)
            evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
            bind_error = max(bind_error, max((mesh.matrix_world@v.co-evaluated.matrix_world@e.co).length for v,e in zip(mesh.data.vertices,evaluated.data.vertices)))
            vertices, weights = [], []
            for vertex in mesh.data.vertices:
                point = mesh.matrix_world @ vertex.co
                points.append(point)
                vertices.append(rounded(point))
                groups = [(mesh.vertex_groups[g.group].name, round(g.weight,6)) for g in vertex.groups]
                weights.append(groups)
                if len(groups)!=1 or groups[0][0] not in bones or abs(groups[0][1]-1)>1e-6:
                    invalid.append(vertex.index)
            # Blender's FBX triangulator permutes polygon output order between
            # processes. Hash oriented topology and its material together in
            # canonical order; never discard winding, weights or connectivity.
            faces = sorted((list(p.vertices), p.material_index) for p in mesh.data.polygons)
            signature.append({'mesh': mesh.name, 'vertices': vertices, 'weights': weights,
                              'oriented_faces_with_material': faces})
        height = max(p.z for p in points)-min(p.z for p in points)
        width = max(p.x for p in points)-min(p.x for p in points)
        check('height and ground', abs(height-HEIGHTS[name])<.002 and abs(min(p.z for p in points))<1e-4, height)
        check('manifest dimensions', abs(height-manifest['height_m'])<1e-4 and abs(width-manifest['width_m'])<1e-4, [height,width])
        from validate_hunter_detail_contract import measure_budget
        measure_budget(meshes, check)
        check('triangle manifest', triangles==manifest['triangles'], triangles)
        check('rigid weights', not invalid, invalid)
        check('bind pose', bind_error<1e-4, bind_error)
        # The actual foot geometry is elongated towards -Y, independently of metadata.
        foot_points = [mesh.matrix_world@v.co for mesh in meshes for v in mesh.data.vertices
                       if any(mesh.vertex_groups[g.group].name.endswith('Foot') for g in v.groups)]
        check('facing -Y', min(p.y for p in foot_points)<-.20 and max(p.y for p in foot_points)<.09,
              [min(p.y for p in foot_points),max(p.y for p in foot_points)])
        clips = {a.name.split('|')[-1]: a for a in bpy.data.actions}
        check('exact six actions', set(clips)==set(NAMES) and len(bpy.data.actions)==6, list(clips))
        check('30 fps', bpy.context.scene.render.fps==30 and manifest['fps']==30, bpy.context.scene.render.fps)
        action_report, animation_signature = [], []
        for n in NAMES:
            clip = clips[n]
            start,end = clip.frame_range
            entry = next(a for a in manifest['actions'] if a['name']==n)
            check(n+' range/manifest', abs(end-start-expected_lengths[n])<.001 and list(entry['frames'])==[0,expected_lengths[n]] and abs(start-1)<.001,
                  {'imported': [start,end], 'source': entry['frames']})
            samples, root_motion = [], 0
            for frame in range(int(start),int(end)+1):
                set_action(rig,clip,frame)
                samples.append([round(v,6) for b in rig.pose.bones for row in b.matrix for v in row])
                root_motion = max(root_motion, (rig.matrix_world@rig.pose.bones['Root'].head).length)
            seam = max(abs(a-b) for a,b in zip(samples[0],samples[-1]))
            motion = max(abs(a-b) for sample in samples for a,b in zip(samples[0],sample))
            check(n+' root fixed', root_motion<1e-4, root_motion)
            check(n+' loop flag', entry['loop']==(n in NAMES[:3]), entry['loop'])
            if n in NAMES[:3]:
                check(n+' seam', seam<1e-4, seam)
            check(n+' authored samples', motion>1e-4, motion)
            if n=='attack':
                check('contact frame', entry['contact_frame']==round(expected_lengths[n]*.4), entry['contact_frame'])
            action_report.append({**entry, 'imported_frames': [start,end], 'seam_error': seam})
            animation_signature.append([n,samples])
        from hunter_animation_review import validate_motion, validate_source_motion
        report['motion'] = validate_motion(name, rig, meshes, clips, motion_minima, check)
        validate_source_motion(source, rig, clips, check)
        check('manifest action set', len(manifest['actions'])==6 and {a['name'] for a in manifest['actions']}==set(NAMES), len(manifest['actions']))
        contact = next(a['contact_frame'] for a in manifest['actions'] if a['name']=='attack')
        set_action(rig,clips['attack'],int(clips['attack'].frame_range[0])+contact)
        if name=='Herald':
            spread = abs((rig.matrix_world@rig.pose.bones['LeftHand'].head).x)
            check('scream spreads arms at contact', spread>.8, spread)
        else:
            reach = (rig.matrix_world@rig.pose.bones['LeftHand'].head).y
            check('attack reaches forward at contact', reach<-.35, reach)
        material_signature = []
        actual_materials = {s.material.name:s.material for m in meshes for s in m.material_slots}

        check('material names', set(actual_materials)=={m['name'] for m in manifest['materials']} and all(n.startswith('M_Hunter'+name+'_') for n in actual_materials), list(actual_materials))
        for entry in manifest['materials']:
            m = actual_materials[entry['name']]
            shader = m.node_tree.nodes.get('Principled BSDF')
            color = [srgb(v) for v in shader.inputs['Base Color'].default_value[:3]]
            check(entry['name']+' base colour', max(abs(a-b) for a,b in zip(color,entry['base_color_srgb']))<.002, rounded(color))
            check(entry['name']+' no textures', not any(n.type=='TEX_IMAGE' for n in m.node_tree.nodes), m.name)
            material_signature.append([m.name,rounded(color)])
        check('sockets', set(manifest['sockets'])=={'attack_origin','head_or_top'} and all(s['bone'] in bones and len(s['local_offset'])==3 and all(math.isfinite(v) for v in s['local_offset']) for s in manifest['sockets'].values()), manifest['sockets'])
        # Source shader emission is authoritative: FBX does not preserve separate
        # emission strength consistently. Unity must create materials from manifest.
        bpy.ops.wm.open_mainfile(filepath=str(source))
        check('source actions', {a.name for a in bpy.data.actions}==set(NAMES), [a.name for a in bpy.data.actions])
        for entry in manifest['materials']:
            shader = bpy.data.materials[entry['name']].node_tree.nodes.get('Principled BSDF')
            e = [srgb(v) for v in shader.inputs['Emission Color'].default_value[:3]]
            strength = shader.inputs['Emission Strength'].default_value
            check(entry['name']+' source emission/manifest', max(abs(a-b) for a,b in zip(e,entry['emission_color_srgb']))<.002 and abs(strength-entry['emission_strength'])<1e-5, [rounded(e),strength])
            limit = .2 if name=='Stare' else .3 if name=='Herald' else 0
            check(entry['name']+' emission budget', 0<=strength<=limit+1e-6, strength)
            if entry['name']=='M_HunterHerald_Cavity':
                check('Herald cavity emission', abs(strength-.3)<1e-6, strength)
        preview_hashes = {}
        for view in ('front','side','three-quarter','attack-pose','lineup'):
            path = previews/(view+'.png')
            image = bpy.data.images.load(str(path),check_existing=False)
            pixels = list(image.pixels)
            colors = {tuple(round(pixels[i+c],3) for c in range(3)) for i in range(0,len(pixels),64)}
            check('preview '+view, tuple(image.size)==(800,800) and len(colors)>10, len(colors))
            preview_hashes[view] = hashlib.sha256(path.read_bytes()).hexdigest()
            bpy.data.images.remove(image)
        payload = {'geometry':signature,'bones':bones,'animation':animation_signature,
                   'materials':material_signature,'manifest':manifest}
        (previews/'content-signature.json').write_text(json.dumps(payload,sort_keys=True,separators=(',',':'))+'\n',encoding='utf-8')
        digest = hashlib.sha256(json.dumps(payload,sort_keys=True,separators=(',',':')).encode()).hexdigest()
        report.update(signature_version=2,height_m=round(height,6),width_m=round(width,6),triangles=triangles,
                      bones=bones,actions=action_report,content_sha256=digest,preview_sha256=preview_hashes)
    except Exception as exc:
        check('exception',False,repr(exc))
    report['checks'] = rows
    report['passed'] = all(r['passed'] for r in rows)
    report['manifest_agreement'] = report['passed']
    target = previews/'validation.json'
    previews.mkdir(parents=True,exist_ok=True)
    # Preserve the first successful content measurement; compare on every rerun.
    baseline = previews/'validation-detail-run1.json'
    if report['passed'] and baseline.exists():
        first = json.loads(baseline.read_text(encoding='utf-8'))
        report['two_run_hash_match'] = first.get('signature_version')==report['signature_version'] and first['content_sha256']==report['content_sha256']
        report['two_run_preview_match'] = first['preview_sha256']==report['preview_sha256']
        if not report['two_run_hash_match'] or not report['two_run_preview_match']:
            report['passed'] = False
    target.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    if report['passed'] and not baseline.exists():
        baseline.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,indent=2),flush=True)
    if not report['passed']:
        raise RuntimeError(name+' validation failed')


if __name__ == '__main__' and '--lineup' in sys.argv:
    combined_lineup()
