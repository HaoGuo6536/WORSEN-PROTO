# ============================================================================
# exit_door.py
# PURPOSE:
#   Generate an original weathered freestanding door, not a copy of film assets.
#   One quiet, timeless painted-wood design works across all four building themes.
# ARCHITECTURAL ROLE: Offline art generator · Floor/Exit, outside runtime layers.
# KEY RESPONSIBILITIES:
#   - Author a frame, hinge-origin leaf, threshold and front-only aperture quad.
#   - Export metre-scale FBX (-Z forward, Y up, applied conversion) and packed source.
#   - Render closed/open/back evidence and an eased-swing contact sheet.
# DEPENDENCIES: Blender 5.2, Python standard library; no external content/network.
# USAGE NOTES:
#   blender --background --factory-startup --python-exit-code 1 --python <this>
#   Unity contract: aperture 2x3 m; DoorLeaf origin (-1,0,0), extends toward +X;
#   EscapeSurface XY at Z=.11 faces -Z; opening is local Y=+100 degrees.
#   Blender coordinates are (Unity X, -Unity Z, Unity Y). Studio and escape preview
#   nodes are not Unity shader evidence. Runtime materials need editor remapping.
# ============================================================================
import json
import math
import random
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/Exit/WeatheredDoor'
EXPORT = ROOT / 'Assets/Art/Exit/WeatheredDoor'
REVIEW = ROOT / 'Logs/AgentValidation/Art/ExitDoor'
NAME = 'WORSEN_WeatheredExitDoor'


def material(name, color, metallic=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = .78
    bsdf.inputs['Metallic'].default_value = metallic
    return mat


def paint_texture(mat):
    rng = random.Random(1901)
    width, height = 256, 512
    image = bpy.data.images.new('ExitPaint', width, height)
    chips = [(rng.random(), rng.random(), rng.uniform(.002, .012), rng.uniform(.01, .10)) for _ in range(45)]
    pixels = []
    for y in range(height):
        v = y / height
        for x in range(width):
            u = x / width
            grain = .055 * math.sin(u * 470 + math.sin(v * 11) * 2) + rng.uniform(-.025, .025)
            chipped = any(((u-cx)/rx)**2 + ((v-cy)/ry)**2 < 1 for cx, cy, rx, ry in chips)
            base = (.19, .125, .071) if chipped else (.49, .60, .53)
            dirt = 1 - .32 * max(0, 1-v*7)
            pixels.extend([max(.01, (c+grain)*dirt) for c in base] + [1])
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(EXPORT / 'ExitPaint.png')
    image.file_format = 'PNG'
    image.save()
    image.pack()
    tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image = image
    mat.node_tree.links.new(tex.outputs['Color'], mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])


def cube(name, center, size, mat, bevel=.012):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(center[0], -center[2], center[1]))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new('Worn edges', 'BEVEL')
        mod.width, mod.segments = bevel, 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def join(parts, name, origin):
    bpy.ops.object.select_all(action='DESELECT')
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.context.scene.cursor.location = origin
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return obj


def build():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    paint = material('Exit_PatinatedPaint', (.49, .60, .53))
    paint_texture(paint)
    edge = material('Exit_WornEdges', (.28, .20, .12))
    brass = material('Exit_OldBrass', (.28, .17, .055), .65)
    stone = material('Exit_ThresholdStone', (.22, .235, .225))
    frame_parts = [cube('Jamb', (x, 1.5, 0), (.16, 3, .26), paint) for x in (-1.08, 1.08)]
    frame_parts += [cube('Lintel', (0, 3.08, 0), (2.32, .16, .26), paint),
                    cube('Crown', (0, 3.18, 0), (2.4, .06, .30), edge)]
    for x in (-1.085, 1.085):
        frame_parts.append(cube('Face bead', (x, 1.5, -.145), (.05, 3, .035), edge, .006))
        frame_parts.append(cube('Foot shoe', (x, .13, 0), (.22, .26, .34), brass))
    frame = join(frame_parts, 'DoorFrame', (0, 0, 0))
    leaf_parts = [cube('Core', (0, 1.5, 0), (1.98, 2.96, .11), paint)]
    for face in (-1, 1):
        for x in (-.89, 0, .89):
            leaf_parts.append(cube('Stile', (x, 1.5, face*.065), (.16, 2.96, .035), paint, .006))
        for y in (.14, 1.06, 2.86):
            leaf_parts.append(cube('Rail', (0, y, face*.065), (1.96, .19, .035), paint, .006))
        for x in (-.45, .45):
            for y, h in ((.60, .64), (1.97, 1.48)):
                leaf_parts.append(cube('Recessed panel', (x, y, face*.058), (.62, h, .025), paint, .025))
                # Thin worn molding around each inset, both sides.
                for dx in (-.34, .34):
                    leaf_parts.append(cube('Panel bead', (x+dx, y, face*.08), (.028, h+.08, .027), edge, .004))
                for dy in (-h/2-.025, h/2+.025):
                    leaf_parts.append(cube('Panel bead', (x, y+dy, face*.08), (.70, .028, .027), edge, .004))
        leaf_parts.append(cube('Latch plate', (.82, 1.31, face*.095), (.1, .28, .024), brass))
        leaf_parts.append(cube('Handle', (.72, 1.36, face*.135), (.25, .045, .055), brass))
        leaf_parts.append(cube('Keyhole', (.82, 1.24, face*.111), (.026, .04, .008), edge, .002))
    for y in (.45, 1.5, 2.55):
        leaf_parts.append(cube('Hinge strap', (-.89, y, -.09), (.19, .11, .04), brass))
    leaf = join(leaf_parts, 'DoorLeaf', (-1, 0, 0))
    threshold = cube('Threshold', (0, .02, 0), (2.32, .04, .40), stone, .01)
    # A single quad, normal +Y in Blender => -Z in Unity. Origin stays zero.
    mesh = bpy.data.meshes.new('EscapeAperture')
    mesh.from_pydata([(-1, -.11, .04), (-1, -.11, 3), (1, -.11, 3), (1, -.11, .04)], [], [(0, 1, 2, 3)])
    mesh.uv_layers.new(name='UVMap')
    for loop, uv in zip(mesh.uv_layers.active.data, ((0, 0), (0, 1), (1, 1), (1, 0))):
        loop.uv = uv
    portal = bpy.data.objects.new('EscapeSurface', mesh)
    bpy.context.collection.objects.link(portal)
    portal.data.materials.append(material('Exit_EscapeSurface', (1, .65, .3)))
    return [frame, leaf, threshold, portal]


def smooth(a, b, value):
    t = max(0, min(1, (value-a)/(b-a)))
    return t*t*(3-2*t)


def preview_escape(portal, eye):
    # CPU reference of ExitPortal.shader, sampled for the actual review camera.
    mat = portal.data.materials[0]
    mat.node_tree.nodes.clear()
    out = mat.node_tree.nodes.new('ShaderNodeOutputMaterial')
    emission = mat.node_tree.nodes.new('ShaderNodeEmission')
    texture = mat.node_tree.nodes.new('ShaderNodeTexImage')
    image = bpy.data.images.new('ReviewEscape', 256, 384)
    sun_dir = Vector((.22, .12, 1)).normalized()
    values = []
    eye_u = Vector((eye[0], eye[2], -eye[1]))
    for y in range(384):
        for x in range(256):
            ray = (Vector((-1+2*x/255, .04+2.96*y/383, .11))-eye_u).normalized()
            sky = smooth(-.015, .025, ray.y)
            ridge = .012*math.sin(ray.x*19)+.006*math.sin(ray.x*43)
            edge = smooth(ridge-.004, ridge+.004, ray.y)
            t = max(0, min(1, ray.y*2))
            color = [a*(1-t)+b*t for a, b in zip((1, .64, .32), (.18, .38, .62))]
            color = [f*(.65+.35*sky)*(1-edge)+c*edge for f, c in zip((.18, .24, .095), color)]
            sun = 1-smooth(.035*.88, .035, (ray-sun_dir).length)
            values.extend([(c+s*sun*edge)*1.25 for c, s in zip(color, (3, 2.2, 1.1))]+[1])
    image.pixels.foreach_set(values)
    texture.image = image
    mat.node_tree.links.new(texture.outputs['Color'], emission.inputs['Color'])
    mat.node_tree.links.new(emission.outputs[0], out.inputs['Surface'])


def render_review(objects):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 640, 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.world.color = (.12, .12, .12)
    scene.view_settings.view_transform = 'AgX'
    ground = material('ReviewGround', (.048, .055, .067))
    cube('ReviewGround', (0, -.09, 0), (200, .1, 200), ground, 0)
    for pos, energy, size in (((-3, 4, 6), 950, 5), ((3, -3, 4), 700, 4)):
        data = bpy.data.lights.new('ReviewSoftbox', 'AREA')
        data.energy, data.shape, data.size = energy, 'DISK', size
        light = bpy.data.objects.new('ReviewSoftbox', data)
        scene.collection.objects.link(light)
        light.location = pos
        light.rotation_euler = (Vector((0, 0, 1.5))-light.location).to_track_quat('-Z', 'Y').to_euler()
    data = bpy.data.cameras.new('ReviewCamera')
    camera = bpy.data.objects.new('ReviewCamera', data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    data.lens = 43
    leaf, portal = objects[1], objects[3]
    images = []
    for index, progress in enumerate((0, .25, .5, .75, 1, 1)):
        behind = index == 5
        eye = (4.4, -7, 2.8) if behind else (-1.1, 7.8, 1.8)
        camera.location = eye
        camera.rotation_euler = (Vector((0, 0, 1.55))-camera.location).to_track_quat('-Z', 'Y').to_euler()
        leaf.rotation_euler.z = math.radians(100 * progress*progress*(3-2*progress))
        portal.hide_render = progress == 0 or behind
        if not portal.hide_render:
            preview_escape(portal, eye)
        name = 'open-back' if behind else ('closed-front' if index == 0 else 'open-front' if index == 4 else f'swing-{index}')
        scene.render.filepath = str(REVIEW / (name+'.png'))
        bpy.ops.render.render(write_still=True)
        images.append(Path(scene.render.filepath))
    # Contact sheet uses actual rendered pixels, not a synthetic image mock-up.
    strip = bpy.data.images.new('ExitDoorSwingStrip', 640*5, 720)
    pixels = [0.0] * (640*5*720*4)
    for index, path in enumerate(images[:5]):
        image = bpy.data.images.load(str(path), check_existing=False)
        data = list(image.pixels)
        for y in range(720):
            start = (y*640*5+index*640)*4
            pixels[start:start+640*4] = data[y*640*4:(y+1)*640*4]
    strip.pixels.foreach_set(pixels)
    strip.filepath_raw = str(REVIEW/'swing-strip.png')
    strip.file_format = 'PNG'
    strip.save()


def main():
    for directory in (SOURCE, EXPORT, REVIEW):
        directory.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = 'METRIC'
    objects = build()
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(NAME+'.blend')))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(EXPORT/(NAME+'.fbx')), use_selection=True,
        object_types={'MESH'}, global_scale=1, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, bake_anim=False, path_mode='STRIP', use_mesh_modifiers=True)
    manifest = dict(name=NAME, provenance='Original procedural geometry and texture; no third-party assets.',
        aperture_metres=[2, 3], unity_front=[0, 0, -1], unity_leaf_pivot=[-1, 0, 0],
        objects=[obj.name for obj in objects], export=dict(axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        unity_bakeAxisConversion=False), triangles=sum(len(p.vertices)-2 for obj in objects for p in obj.data.polygons),
        material_mapping={'Exit_EscapeSurface': 'Worsen/ExitPortal', 'other_slots': 'Universal Render Pipeline/Lit'},
        preview='CPU ray-shader reference in Blender, not native URP evidence')
    (SOURCE/(NAME+'.manifest.json')).write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    render_review(objects)
    print('EXIT_DOOR_GENERATED', json.dumps(manifest))


if __name__ == '__main__':
    main()
