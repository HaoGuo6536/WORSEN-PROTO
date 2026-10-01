# ============================================================================
# shrine_kinds.py
# PURPOSE:
#   Author eight original static shrine models with a shared funerary altar base.
#   Function-specific silhouettes remain readable without labels or colour coding.
# ARCHITECTURAL ROLE:
#   Offline art generator (outside runtime layers) · Shrine.
# KEY RESPONSIBILITIES:
#   - Build low-poly meshes with body and emissive accent material slots.
#   - Export metre-scale FBX, editable sources and measured interaction manifests.
#   - Render three low-light views per kind and a true-scale family lineup.
# DEPENDENCIES:
#   Blender 5.2 bpy/mathutils and Python standard library only; original geometry.
# USAGE NOTES:
#   Run headless with --factory-startup --python-exit-code 1. No Unity or Library I/O.
#   Blender -Y front, Z up; FBX -Z forward, Y up, bake_space_transform=True.
#   --only Kind limits regeneration; --no-render skips previews for geometry probes.
# ============================================================================
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Euler, Vector

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'Logs/AgentValidation/Art/Shrine'
KINDS = ('Chance', 'Bargain', 'Pacification', 'Wick', 'Passage', 'Protection', 'Echo', 'Purgatory')
COLORS = ((.65, .8, 1), (1, .65, .25), (.4, .85, .65), (1, .45, .15),
          (.3, .75, 1), (.5, .6, 1), (.85, .5, 1), (1, .25, .3))
BODY_COLOR = (.24, .27, .29)  # sRGB charcoal stone / weathered iron, provisional.
EMISSION = .65  # Mirrors the existing DriverConfig default; surface emission only.
INTERACTION = (0, -.405, .30)  # Blender metres; raised front offering seal.


def linear(c):
    return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1


def material(name, color, emission=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*map(linear, color), 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = mat.diffuse_color
    shader.inputs['Roughness'].default_value = .82
    shader.inputs['Emission Color'].default_value = (*color, 1)
    shader.inputs['Emission Strength'].default_value = emission
    return mat


class Altar:
    def __init__(self, kind):
        self.kind = kind
        self.parts = []
        self.materials = [material('M_ShrineBody', BODY_COLOR),
                          material('M_ShrineAccent', COLORS[KINDS.index(kind)], EMISSION)]

    def finish_part(self, obj, name, accent=False, bevel=0):
        obj.name = name
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        obj.data.materials.append(self.materials[int(accent)])
        if bevel:
            mod = obj.modifiers.new('Chiselled edges', 'BEVEL')
            mod.width, mod.segments = bevel, 1
            bpy.ops.object.modifier_apply(modifier=mod.name)
        self.parts.append(obj)
        return obj

    def box(self, name, pos, size, accent=False, rotation=(0, 0, 0), bevel=.012):
        bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
        obj = bpy.context.object
        obj.scale, obj.rotation_euler = size, rotation
        return self.finish_part(obj, name, accent, bevel)

    def link(self, name, start, end, radius, accent=False, tip=None, sides=8):
        delta = Vector(end) - Vector(start)
        bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=radius,
            radius2=radius if tip is None else tip, depth=delta.length,
            location=(Vector(start) + Vector(end)) / 2)
        obj = bpy.context.object
        obj.rotation_euler = delta.to_track_quat('Z', 'Y').to_euler()
        return self.finish_part(obj, name, accent)

    def orb(self, name, pos, size, accent=False):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1, location=pos)
        obj = bpy.context.object
        obj.scale = size
        return self.finish_part(obj, name, accent)

    def rings(self, name, rings, sides=12, accent=False):
        verts = [(x + rx * math.cos(i * math.tau / sides),
                  y + ry * math.sin(i * math.tau / sides), z)
                 for x, y, z, rx, ry in rings for i in range(sides)]
        faces = [tuple(reversed(range(sides)))]
        for j in range(len(rings) - 1):
            a, b = j * sides, (j + 1) * sides
            faces += [(a+i, a+(i+1)%sides, b+(i+1)%sides, b+i) for i in range(sides)]
        faces.append(tuple(range(len(verts) - sides, len(verts))))
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        return self.finish_part(obj, name, accent)

    def outline(self, name, points, radius, accent=False, closed=False):
        pairs = list(zip(points, points[1:]))
        if closed:
            pairs.append((points[-1], points[0]))
        for a, b in pairs:
            self.link(name, a, b, radius, accent)

    def base(self):
        self.box('Foot slab', (0, 0, .07), (.84, .76, .14), bevel=.04)
        self.box('Stepped plinth', (0, 0, .185), (.72, .64, .09), bevel=.025)
        self.box('Altar block', (0, .025, .32), (.56, .48, .19), bevel=.025)
        self.box('Overhanging altar lip', (0, .015, .445), (.72, .63, .06), bevel=.02)
        # The front-only seal and steps establish facing even on rotational motifs.
        self.box('Front offering step', (0, -.33, .20), (.30, .24, .12), bevel=.025)
        self.link('Interaction seal', (0, -.39, .30), INTERACTION, .062, True, sides=6)
        for x in (-.23, .23):
            self.box('Altar face incision', (x, -.222, .33), (.015, .01, .10), True, bevel=0)

    def join(self):
        bpy.ops.object.select_all(action='DESELECT')
        for obj in self.parts:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = self.parts[0]
        bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = 'WORSEN_Shrine' + self.kind
        tri = obj.modifiers.new('Explicit triangles', 'TRIANGULATE')
        bpy.ops.object.modifier_apply(modifier=tri.name)
        for face in obj.data.polygons:
            face.use_smooth = False
        return obj


def chance(a):
    a.rings('Dice cradle', [(0, 0, .47, .23, .20), (0, 0, .59, .13, .13)])
    center = Vector((0, 0, .92))
    rot = Euler((.20, .32, -.14)).to_matrix()
    a.box('Unbalanced die', center, (.48, .48, .48), rotation=(.20, .32, -.14), bevel=.035)
    # Five front pips, three right pips, two top pips: actual faces, not painted text.
    for u, v in ((-.13, -.13), (.13, -.13), (0, 0), (-.13, .13), (.13, .13)):
        p = center + rot @ Vector((u, -.243, v))
        a.link('Five pips', p, p + rot @ Vector((0, -.009, 0)), .032, True)
    for v in (-.13, 0, .13):
        p = center + rot @ Vector((.243, v, v))
        a.link('Three pips', p, p + rot @ Vector((.009, 0, 0)), .032, True)
    for v in (-.12, .12):
        p = center + rot @ Vector((v, v, .243))
        a.link('Two pips', p, p + rot @ Vector((0, 0, .009)), .032, True)
    for sign in (-1, 1):
        a.outline('Grasping cradle', [(sign*.20, .08, .49), (sign*.36, .08, .68),
            (sign*.37, .08, .98), (sign*.30, .08, 1.12)], .036)


def bargain(a):
    a.link('Balance spine', (0, .04, .47), (0, .04, 1.30), .052)
    a.orb('Balance finial', (0, .04, 1.38), (.075, .075, .10))
    a.link('Unequal beam', (-.43, 0, 1.25), (.43, 0, 1.12), .035)
    for sign, z in ((-1, .87), (1, .69)):
        x = sign * .37
        for y in (-.13, .13):
            a.link('Pan chains', (x, 0, 1.19-sign*.065), (x, y, z+.03), .011)
        a.rings('Offering pan', [(x, 0, z-.03, .09, .07), (x, 0, z+.015, .18, .14)])
        if sign == -1:
            for i in range(3):
                a.link('Payment coins', (x, 0, z+.02+i*.028), (x, 0, z+.042+i*.028), .065, True)
        else:
            a.orb('Curse weight', (x, 0, z+.15), (.10, .085, .14))
            a.outline('Curse slash', [(x-.04, -.073, z+.24), (x+.035, -.09, z+.08)], .016, True)
            a.link('Weight spike', (x, 0, z+.25), (x+.04, 0, z+.35), .037, tip=0)


def pacification(a):
    # Wide downward-facing bell dominates; the low yoke is not a portal silhouette.
    for x in (-.30, .30):
        a.link('Bell fork', (x, .03, .47), (x, .03, 1.08), .037)
    a.link('Bell axle', (-.33, .03, 1.09), (.33, .03, 1.09), .035)
    a.rings('Lullaby bell', [(0, 0, .66, .31, .26), (0, 0, .72, .30, .25),
        (0, 0, .81, .22, .19), (0, 0, 1.00, .12, .105), (0, 0, 1.055, .08, .07)])
    a.rings('Bell lip inlay', [(0, 0, .69, .312, .262), (0, 0, .708, .312, .262)], accent=True)
    a.link('Clapper stem', (0, 0, .52), (0, 0, .71), .018)
    a.orb('Clapper', (0, 0, .55), (.05, .05, .06))
    # Closed eyes: a calm rather than an alarm face.
    for sign in (-1, 1):
        a.outline('Closed eye', [(sign*.035, -.193, .87), (sign*.08, -.202, .854),
            (sign*.135, -.177, .87)], .012, True)


def wick(a):
    a.rings('Candle holder', [(0, 0, .47, .25, .22), (0, 0, .53, .17, .15),
        (0, 0, .58, .23, .20)])
    a.rings('Wax pillar', [(0, 0, .55, .125, .12), (0, 0, 1.18, .115, .11),
        (0, 0, 1.22, .09, .09)])
    for angle, length in ((-.7, .21), (-1.5, .30), (-2.5, .16), (.3, .24), (2, .13)):
        x, y = .117*math.cos(angle), .117*math.sin(angle)
        a.link('Wax rivulet', (x, y, 1.18-length), (x, y, 1.20), .025)
        a.orb('Wax droplet', (x, y, 1.18-length), (.026, .026, .035))
    a.link('Black wick', (0, 0, 1.21), (0, 0, 1.29), .012)
    a.rings('Frozen flame', [(0, 0, 1.26, .025, .024), (0, 0, 1.33, .072, .06),
        (.025, 0, 1.43, .035, .03), (.055, 0, 1.53, .001, .001)], sides=7, accent=True)
    for x in (-.22, .22):
        a.link('Holder thorn', (x, 0, .54), (x*.8, 0, .74), .028, tip=0)


def passage(a):
    points = [(-.32, .08, .47), (-.32, .08, 1.10), (-.22, .08, 1.28),
              (0, .08, 1.46), (.22, .08, 1.28), (.32, .08, 1.10), (.32, .08, .47)]
    a.outline('Broken gothic portal', points, .076)
    a.outline('Portal inner light', [(x*.84, y-.072, z-.022) for x, y, z in points], .014, True)
    for i in range(5):
        a.box('Pocket bridge tread', (0, -.25+i*.12, .51+i*.018), (.29, .10, .04), bevel=.009)
    for x in (-.10, .10):
        a.link('Bridge stringer', (x, -.29, .48), (x, .28, .558), .022)
        a.link('Bridge rear foot', (x, .24, .47), (x, .24, .558), .022)
    a.box('Threshold', (0, -.31, .51), (.46, .08, .045), bevel=.01)
    a.orb('Keystone', (0, .08, 1.46), (.095, .085, .10))


def protection(a):
    a.link('Ward spine', (0, .07, .47), (0, .07, 1.15), .055)
    # Closed extruded kite shield, raised border and central eye/ward sigil.
    pts = [(-.30, 1.19), (0, 1.32), (.30, 1.19), (.27, .84), (0, .58), (-.27, .84)]
    verts = [(x, y, z) for y in (-.08, .08) for x, z in pts]
    faces = [tuple(reversed(range(6))), tuple(range(6, 12))]
    faces += [(i, (i+1)%6, (i+1)%6+6, i+6) for i in range(6)]
    mesh = bpy.data.meshes.new('Kite shield')
    mesh.from_pydata(verts, [], faces); mesh.update()
    obj = bpy.data.objects.new('Kite shield', mesh); bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active = obj
    a.finish_part(obj, 'Kite shield', bevel=.018)
    a.outline('Shield rim', [(x*.93, -.105, .95+(z-.95)*.93) for x, z in pts], .018, True, True)
    a.outline('Ward eye', [(-.15, -.12, 1.02), (0, -.145, 1.11), (.15, -.12, 1.02),
        (0, -.145, .93)], .02, True, True)
    a.link('Ward iris', (0, -.15, .975), (0, -.15, 1.065), .02, True)
    for sign in (-1, 1):
        for z, end in ((.84, .86), (1.04, 1.09), (1.19, 1.33)):
            a.link('Defensive ray', (sign*.25, .03, z), (sign*.43, .03, end), .033, tip=0)


def echo(a):
    # Matched, outward-leaning mirrors, each with the same spectral face incision.
    for sign in (-1, 1):
        center = Vector((sign*.21, 0, .95))
        rot = Euler((0, sign*.17, 0)).to_matrix()
        def p(x, y, z):
            return center + rot @ Vector((x, y, z))
        ring = [p(.145*math.cos(i*math.tau/10), 0, .37*math.sin(i*math.tau/10)) for i in range(10)]
        a.outline('Twin mirror frame', ring, .033, closed=True)
        a.box('Obsidian mirror', center, (.21, .07, .55), rotation=(0, sign*.17, 0), bevel=.035)
        for x in (-.048, .048):
            a.link('Repeated eyes', p(x-.018, -.049, .09), p(x+.018, -.049, .09), .012, True)
        a.outline('Reflected face', [p(-.078, -.05, .17), p(-.082, -.05, -.05),
            p(0, -.05, -.17), p(.082, -.05, -.05), p(.078, -.05, .17)], .012, True)
        a.link('Mirror foot', (sign*.16, 0, .47), p(0, 0, -.34), .035)
    a.outline('Second-use link', [(-.20, -.11, .55), (0, -.16, .49), (.20, -.11, .55)], .014, True)


def purgatory(a):
    a.rings('Prison floor', [(0, 0, .47, .29, .26), (0, 0, .53, .29, .26)], sides=8)
    for i in range(8):
        angle = i*math.tau/8 + math.pi/8
        x, y = .26*math.cos(angle), .23*math.sin(angle)
        a.link('Prison bars', (x, y, .53), (x, y, 1.15), .016)
        a.link('Crown ribs', (x, y, 1.15), (x*.28, y*.28, 1.37), .019)
    a.rings('Cage collar', [(0, 0, 1.13, .30, .265), (0, 0, 1.19, .30, .265)], sides=8)
    a.link('Cage finial', (0, 0, 1.31), (0, 0, 1.47), .045, tip=0)
    a.orb('Trapped head', (.018, -.025, .93), (.10, .087, .12))
    a.rings('Bound shroud', [(0, .035, .55, .14, .105), (0, .035, .67, .105, .09),
        (0, 0, .80, .075, .06)], sides=7)
    for x in (-.025, .059):
        a.orb('Captive eyes', (x, -.105, .95), (.017, .01, .022), True)
    for sign in (-1, 1):
        a.outline('Captive grasp', [(sign*.06, 0, .79), (sign*.13, -.05, .72),
            (sign*.17, -.13, .89)], .024)
        a.orb('Bound hand', (sign*.17, -.13, .89), (.032, .023, .046), True)
    a.link('Front lock', (0, -.265, .65), (0, -.29, .65), .057)
    a.box('Lock slit', (0, -.298, .65), (.014, .01, .032), True, bevel=0)


BUILDERS = dict(zip(KINDS, (chance, bargain, pacification, wick, passage, protection, echo, purgatory)))


def studio():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'AgX'
    world = bpy.data.worlds.new('Dim room')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (.035, .045, .065, 1)
    world.node_tree.nodes['Background'].inputs[1].default_value = .20
    scene.world = world
    bpy.ops.mesh.primitive_plane_add(size=200)
    bpy.context.object.name = 'Preview floor (not exported)'
    bpy.context.object.data.materials.append(material('Preview floor', (.075, .085, .10)))
    for name, pos, power, size, color in (
        ('Cold side light', (-3, -4, 5), 380, 4, (.58, .72, 1)),
        ('Warm rim', (2, 2, 4), 500, 3, (1, .60, .32)),
        ('Soft frontal fill', (0, -4, 2), 65, 3, (.72, .8, 1))):
        data = bpy.data.lights.new(name, 'AREA'); data.energy = power; data.shape = 'DISK'; data.size = size; data.color = color
        obj = bpy.data.objects.new(name, data); scene.collection.objects.link(obj); obj.location = pos
        obj.rotation_euler = (Vector((0, 0, .7))-obj.location).to_track_quat('-Z', 'Y').to_euler()
    data = bpy.data.cameras.new('Review camera'); obj = bpy.data.objects.new('Review camera', data)
    scene.collection.objects.link(obj); scene.camera = obj
    data.type = 'ORTHO'; data.lens = 50
    return obj


def render(camera, path, position, target=(0, 0, .77), scale=1.85, size=(640, 720)):
    camera.location = position
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.ortho_scale = scale
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def generate(kind, previews=True):
    reset()
    altar = Altar(kind); altar.base(); BUILDERS[kind](altar)
    mesh = altar.join()
    coords = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
    lo = [min(v[i] for v in coords) for i in range(3)]
    hi = [max(v[i] for v in coords) for i in range(3)]
    source = ROOT / 'ArtSource/Shrine' / kind / (mesh.name + '.blend')
    fbx = ROOT / 'Assets/Art/Shrine' / kind / (mesh.name + '.fbx')
    source.parent.mkdir(parents=True, exist_ok=True); fbx.parent.mkdir(parents=True, exist_ok=True)
    # Both source and export include an explicit authoring interaction marker.
    marker = bpy.data.objects.new('InteractionPoint', None)
    bpy.context.collection.objects.link(marker); marker.location = INTERACTION
    marker.empty_display_size = .05; marker.select_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'MESH', 'EMPTY'},
        axis_forward='-Z', axis_up='Y', bake_space_transform=True, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', global_scale=1, bake_anim=False, use_mesh_modifiers=True,
        use_triangles=True, path_mode='AUTO', add_leaf_bones=False)
    manifest = dict(schema=1, kind=kind, source=str(source.relative_to(ROOT)).replace('\\', '/'),
        export=str(fbx.relative_to(ROOT)).replace('\\', '/'), units='metres',
        authoring_axes='Z up, -Y front', unity_axes='Y up, -Z front', bakeAxisConversion=False,
        height=round(hi[2]-lo[2], 6), footprint=[round(hi[0]-lo[0], 6), round(hi[1]-lo[1], 6)],
        bounds_blender={'min': lo, 'max': hi}, interaction_blender=list(INTERACTION),
        interaction_unity=[INTERACTION[0], INTERACTION[2], INTERACTION[1]],
        triangles=len(mesh.data.polygons), materials=[
            dict(name='M_ShrineBody', color_srgb=list(BODY_COLOR), emission=0, roughness=.82),
            dict(name='M_ShrineAccent', color_srgb=list(COLORS[KINDS.index(kind)]), emission=EMISSION, roughness=.82)],
        runtime_contract='Static mesh only; two named material slots; no colliders or scripts. Authored scale, not fallback Size.')
    (source.parent / (mesh.name + '.manifest.json')).write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    if previews:
        camera = studio()
        for view, pos in (('front', (0, -4, 1.55)), ('three-quarter', (3, -4, 2.3)), ('side', (4, 0, 1.55))):
            render(camera, EVIDENCE / f'{kind}-{view}.png', pos)
    return manifest


def lineup():
    reset()
    for i, kind in enumerate(KINDS):
        path = ROOT / 'ArtSource/Shrine' / kind / f'WORSEN_Shrine{kind}.blend'
        with bpy.data.libraries.load(str(path), link=False) as (data, result):
            result.objects = [n for n in data.objects if n == 'WORSEN_Shrine'+kind]
        obj = result.objects[0]; bpy.context.collection.objects.link(obj); obj.location.x = (i-3.5)*1.17
    camera = studio()
    # Broad soft sources maintain identical, dim illumination across the full row.
    for obj in bpy.data.objects:
        if obj.type == 'LIGHT':
            obj.data.size = 10; obj.data.energy *= 2.5
    render(camera, EVIDENCE / 'lineup.png', (2, -11, 4), target=(0, 0, .70), scale=10.1, size=(2400, 650))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--only', choices=KINDS)
    parser.add_argument('--no-render', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    assert bpy.app.version[:2] == (5, 2), bpy.app.version_string
    for kind in (args.only,) if args.only else KINDS:
        manifest = generate(kind, not args.no_render)
        print('SHRINE_BUILT', kind, manifest['height'], manifest['triangles'], flush=True)
    if not args.no_render and all((ROOT / 'ArtSource/Shrine' / k / f'WORSEN_Shrine{k}.blend').exists() for k in KINDS):
        lineup()


if __name__ == '__main__':
    main()
