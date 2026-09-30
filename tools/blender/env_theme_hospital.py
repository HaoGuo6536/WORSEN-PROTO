# ============================================================================
# env_theme_hospital.py
# PURPOSE:
#   Author an original 1970s institutional ward, not a recoloured generic shell.
#   Export a deterministic modular kit and spawnable room-placement catalogue;
#   save editable sources and lit/cutaway review scenes without operating Unity.
# ARCHITECTURAL ROLE: Offline art generator; outside Unity's runtime layers.
# KEY RESPONSIBILITIES:
#   - Model the tiled shell, clinical doors and ceiling-supported ward fixtures.
#   - Bake four portable surface textures and export metre-scale isolated FBXs.
#   - Author enclosed, socketed rooms from the exported kit alone.
#   - Assemble sources from manifest placements and render review evidence.
# DEPENDENCIES: Blender 5.2 bpy/bmesh/mathutils, bundled numpy, Python stdlib.
# USAGE NOTES:
#   Blender --background --factory-startup --python-exit-code 1 --python this-file
#   -- [--skip-previews]. Run only in the assigned unopened hospital worktree.
#   Blender (x,y,z) maps to Unity (x,z,-y). No v1 module is imported.
#   Authoring values below are provisional; owner palette and kit sizes are fixed.
# ============================================================================
import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import shutil
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/Art/Environment/Hospital'
SOURCE = ROOT / 'ArtSource/Environment/Hospital'
OUT = ROOT / 'Logs/AgentValidation/Art/EnvHospital'
HEIGHT = 3.6
SEED = 260930
TEXTURE_SIZE = 512
DADO = 1.8
LIGHT_WATTS = 105
SAMPLES = 24
PALETTE = {'tile': '#a8c8b0', 'paint': '#e4e1d4', 'grout': '#8e948f',
           'light': '#d8f4ff', 'vinyl': '#b9b3a5', 'rust': '#7a4a2a',
           'stainless': '#9aa3a8', 'rubber': '#303a36', 'glass': '#455e58',
           'curtain': '#8eafa0', 'linen': '#ddd9c8', 'acoustic': '#e4e1d4'}
COMMON = {'wall_2m': 'wall', 'wall_door_4m': 'door', 'wall_window_2m': 'window',
          'wall_arc_r4': 'arc', 'wall_arc_r6': 'arc', 'wall_arc_r8': 'arc',
          'corner_in': 'corner', 'corner_out': 'corner', 'pillar': 'pillar',
          'floor_2x2': 'floor', 'ceiling_2x2': 'ceiling', 'trim_base_2m': 'trim',
          'prop_bed': 'prop', 'prop_curtain_rail': 'prop',
          'prop_cabinet': 'prop', 'prop_wheelchair': 'prop'}
EXTRA = {'wall_1m': 'wall', 'wall_closed_4m': 'wall',
         'door_double_porthole_4m': 'door', 'ceiling_drop_panel_2x2': 'ceiling',
         'light_fluorescent_panel': 'prop', 'light_fluorescent_dead': 'prop',
         'wall_handrail_2m': 'trim', 'corner_guard': 'trim',
         'curtain_track_bay': 'prop', 'wall_tile_dado_2m': 'trim',
         'window_ward_2m': 'prop', 'prop_iv_stand': 'prop', 'prop_gurney': 'prop',
         'prop_nurse_counter': 'prop', 'prop_waiting_bench': 'prop',
         'prop_signage_frame': 'prop', 'prop_scrub_sink': 'prop',
         'prop_xray_screen': 'prop', 'prop_operating_lamp': 'prop'}
KINDS = dict(COMMON, **EXTRA)


def linear(hex_color):
    channels = [int(hex_color[i:i+2], 16)/255 for i in (1, 3, 5)]
    return tuple(c/12.92 if c <= .04045 else ((c+.055)/1.055)**2.4 for c in channels)


class WardMesh:
    """Deterministic Z-up mesh accumulator. No unapplied object transforms."""
    def __init__(self, piece):
        self.piece = piece
        self.vertices, self.faces, self.surfaces = [], [], []

    def add(self, vertices, faces, surface):
        offset = len(self.vertices)
        self.vertices.extend(tuple(v) for v in vertices)
        self.faces.extend(tuple(offset+i for i in face) for face in faces)
        self.surfaces.extend([surface]*len(faces))

    def box(self, center, size, surface, bevel=0):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1)
        for vertex in bm.verts:
            vertex.co = Vector(tuple(vertex.co[i]*size[i] for i in range(3)))
        if bevel:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect='EDGES')
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.verts.ensure_lookup_table()
        bm.verts.index_update()
        self.add([v.co+Vector(center) for v in bm.verts],
                 [tuple(v.index for v in f.verts) for f in bm.faces], surface)
        bm.free()

    def tube(self, a, b, radius, surface, sides=8):
        a, b = Vector(a), Vector(b)
        rotation = (b-a).to_track_quat('Z', 'Y').to_matrix()
        vertices = [end + rotation @ Vector((radius*math.cos(i*2*math.pi/sides),
                    radius*math.sin(i*2*math.pi/sides), 0)) for end in (a, b) for i in range(sides)]
        faces = [tuple(reversed(range(sides))), tuple(sides+i for i in range(sides))]
        faces += [(i, (i+1)%sides, (i+1)%sides+sides, i+sides) for i in range(sides)]
        self.add(vertices, faces, surface)

    def ring(self, center, radius, thick, surface, axis='X', sides=12):
        rotation = Matrix.Rotation(math.pi/2, 3, 'Y' if axis == 'X' else 'X')
        vertices = []
        for i in range(sides):
            for j in range(4):
                t, u = i*2*math.pi/sides, j*math.pi/2
                vertices.append(Vector(center)+rotation @ Vector((
                    (radius+thick*math.cos(u))*math.cos(t),
                    (radius+thick*math.cos(u))*math.sin(t), thick*math.sin(u))))
        self.add(vertices, [(i*4+j, ((i+1)%sides)*4+j, ((i+1)%sides)*4+(j+1)%4,
                             i*4+(j+1)%4) for i in range(sides) for j in range(4)], surface)

    def finish(self, center=False):
        if center:
            low = [min(v[i] for v in self.vertices) for i in range(3)]
            high = [max(v[i] for v in self.vertices) for i in range(3)]
            shift = ((low[0]+high[0])/2, (low[1]+high[1])/2, low[2])
            self.vertices = [tuple(v[i]-shift[i] for i in range(3)) for v in self.vertices]
        data = bpy.data.meshes.new('Hospital_'+self.piece)
        data.from_pydata(self.vertices, [], self.faces)
        data.update()
        surfaces = sorted(set(self.surfaces))
        for surface in surfaces:
            data.materials.append(bpy.data.materials['hospital_'+surface])
        for face, surface in zip(data.polygons, self.surfaces):
            face.material_index = surfaces.index(surface)
        uv = data.uv_layers.new(name='SurfaceMetres')
        for face in data.polygons:
            axis = max(range(3), key=lambda i: abs(face.normal[i]))
            axes = [i for i in range(3) if i != axis]
            for index in face.loop_indices:
                co = data.vertices[data.loops[index].vertex_index].co
                uv.data[index].uv = (co[axes[0]], co[axes[1]])
        bm = bmesh.new()
        bm.from_mesh(data)
        bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method='FIXED', ngon_method='EAR_CLIP')
        bm.to_mesh(data)
        bm.free()
        data.update()
        obj = bpy.data.objects.new(data.name, data)
        bpy.context.collection.objects.link(obj)
        return obj


def materials():
    """Four original 512px one-metre repeat textures, including real tile grout."""
    rng = np.random.default_rng(SEED)
    y, x = np.mgrid[0:TEXTURE_SIZE, 0:TEXTURE_SIZE]/TEXTURE_SIZE
    noise = rng.random(x.shape)
    for name, color in PALETTE.items():
        mat = bpy.data.materials.new('hospital_'+name)
        rgb = linear(color)
        mat.diffuse_color = (*rgb, 1)
        shader = mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value = (*rgb, 1)
        shader.inputs['Roughness'].default_value = .24 if name == 'tile' else .42 if name == 'vinyl' else .78
        if name == 'stainless':
            shader.inputs['Metallic'].default_value = .8
            shader.inputs['Roughness'].default_value = .32
        if name == 'light':
            shader.inputs['Emission Color'].default_value = (*rgb, 1)
            shader.inputs['Emission Strength'].default_value = 3.5
        if name not in ('tile', 'paint', 'vinyl', 'acoustic'):
            continue
        pixels = np.ones((*x.shape, 4), dtype=np.float32)
        pixels[:, :, :3] = rgb
        if name == 'tile':
            # 200mm square glazed tiles, 4mm mortar joints. Not brick bond.
            joint = ((x*5) % 1 < .025) | ((y*5) % 1 < .025)
            variation = .94+.06*np.sin(np.floor(x*5)*3+np.floor(y*5)*7)
            pixels[:, :, :3] *= variation[:, :, None]
            pixels[joint, :3] = linear(PALETTE['grout'])
            # Fine rust bleeding downward from two joints; sparse rather than gore.
            stain = np.exp(-((x-.204)/.012)**2)*np.maximum(0, .6-y)*.7
        elif name == 'paint':
            stain = np.maximum(0, np.sin(x*9+np.sin(y*15))*np.cos(y*8))**5*.20
            pixels[:, :, :3] *= (.965+noise*.035)[:, :, None]
        elif name == 'vinyl':
            pixels[:, :, :3] *= (.94+noise*.10)[:, :, None]
            flecks = noise > .982
            pixels[flecks, :3] = linear('#8e948f')
            pixels[noise < .012, :3] = linear('#e4e1d4')
            stain = np.maximum(0, np.sin(x*5+y*7))**12*.06
        else:
            holes = ((x*40) % 1 < .15) & ((y*40) % 1 < .15)
            pixels[holes, :3] *= .60
            stain = np.maximum(0, np.sin(x*7+2)*np.sin(y*5))**5*.25
        pixels[:, :, :3] = pixels[:, :, :3]*(1-stain[:, :, None]) + np.array(linear(PALETTE['rust']))*stain[:, :, None]
        # Non-colour PNG holds linear values: avoid applying an sRGB transfer twice.
        image = bpy.data.images.new('hospital_'+name+'_surface', width=TEXTURE_SIZE, height=TEXTURE_SIZE)
        image.colorspace_settings.name = 'Non-Color'
        # Encode sRGB before writing, and use the conventional sRGB texture setting.
        pixels[:, :, :3] = np.where(pixels[:, :, :3] <= .0031308,
            pixels[:, :, :3]*12.92, 1.055*np.power(pixels[:, :, :3], 1/2.4)-.055)
        image.pixels.foreach_set(pixels.ravel())
        image.filepath_raw = str(SOURCE/'Kit'/(image.name+'.png'))
        image.file_format = 'PNG'
        image.save()
        image.colorspace_settings.name = 'sRGB'
        shutil.copyfile(SOURCE/'Kit'/(image.name+'.png'), ART/'Kit'/(image.name+'.png'))
        image.filepath = bpy.path.relpath(str(ART/'Kit'/(image.name+'.png')), start=str(SOURCE/'Kit'))
        image.pack()
        texture = mat.node_tree.nodes.new('ShaderNodeTexImage')
        texture.image = image
        texture.interpolation = 'Linear'
        mat.node_tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
        if name == 'tile':
            bump = mat.node_tree.nodes.new('ShaderNodeBump')
            bump.inputs['Strength'].default_value = .18
            bump.inputs['Distance'].default_value = .012
            mat.node_tree.links.new(texture.outputs['Color'], bump.inputs['Height'])
            mat.node_tree.links.new(bump.outputs['Normal'], shader.inputs['Normal'])


def wall(m, x0, x1, low=0, high=HEIGHT):
    # Shared vertical seam subdivisions on ALL straight contract walls.
    cuts = sorted({low, high} | {z for z in (1.1, DADO, 2.5, 2.8) if low < z < high})
    for a, b in zip(cuts, cuts[1:]):
        surface = 'tile' if b <= DADO else 'paint'
        m.box(((x0+x1)/2, -.01, (a+b)/2), (x1-x0, .48, b-a), surface)
    face_cuts = sorted(set(cuts) | {z for z in (.15, DADO-.0175, DADO+.0175) if low < z < high})
    for a, b in zip(face_cuts, face_cuts[1:]):
        mid = (a+b)/2
        surface = 'rubber' if mid < .15 else 'grout' if abs(mid-DADO) < .018 else 'tile' if mid < DADO else 'paint'
        m.add([(x0+.007, .25, a), (x1-.007, .25, a),
               (x1-.007, .25, b), (x0+.007, .25, b)], [(3, 2, 1, 0)], surface)


def arc(m, radius, degrees):
    half = math.radians(degrees)/2
    levels = (0, DADO, HEIGHT)
    for a, b in zip(levels, levels[1:]):
        vertices = []
        for i in range(5):
            t = -half+2*half*i/4
            for r, z in ((radius-.25, a), (radius+.25, a), (radius+.25, b), (radius-.25, b)):
                vertices.append((r*math.sin(t), radius-r*math.cos(t), z))
        faces = [(3, 2, 1, 0), (16, 17, 18, 19)]
        faces += [(i*4+j, i*4+(j+1)%4, (i+1)*4+(j+1)%4, (i+1)*4+j)
                  for i in range(4) for j in range(4)]
        m.add(vertices, [tuple(reversed(f)) for f in faces], 'tile' if b == DADO else 'paint')


def ceiling(m):
    m.box((0, 0, .15), (2, 2, .06), 'grout')
    # Four 1m acoustic coffers and a visibly fine suspended T-grid, not stone slabs.
    for x in (-.5, .5):
        for y in (-.5, .5):
            m.box((x, y, .083), (.966, .966, .106), 'acoustic')
    for s in (-.985, 0, .985):
        m.box((s, 0, .015), (.03, 2, .03), 'stainless')
        m.box((0, s, .015), (1.94, .03, .03), 'stainless')


def door_leaves(m):
    # Two open swing leaves with inset circular porthole glazing and kick plates.
    # Open 95 degrees: no leaf intrudes into the mandatory 3.2m clear throat.
    for sign in (-1, 1):
        start = len(m.vertices)
        m.box((sign*.80, 0, 1.39), (1.56, .075, 2.78), 'tile')
        m.box((sign*.80, .044, .30), (1.47, .012, .52), 'stainless')
        m.box((sign*.24, .062, 1.15), (.07, .035, .38), 'stainless')
        # Nested discs produce a round polished bezel, never a square ward window.
        m.tube((sign*.80, .038, 2.05), (sign*.80, .064, 2.05), .275, 'stainless', 12)
        m.tube((sign*.80, .065, 2.05), (sign*.80, .071, 2.05), .225, 'glass', 12)
        hinge = Vector((sign*1.6, 0, 0))
        rotation = Matrix.Rotation(math.radians(-sign*95), 3, 'Z')
        for i in range(start, len(m.vertices)):
            m.vertices[i] = hinge+rotation @ (Vector(m.vertices[i])-hinge)


def wheels(m, width, length, z=.12):
    for x in (-width/2, width/2):
        for y in (-length/2, length/2):
            m.tube((x-.035, y, z), (x+.035, y, z), z, 'rubber', 8)
            m.tube((x, y, z), (x, y, .42), .023, 'stainless')


def curtain(m, bay):
    length = 2.6 if bay else 2
    m.tube((-length/2, 0, 2.95), (length/2, 0, 2.95), .027, 'stainless')
    if bay:
        for x in (-length/2, length/2):
            m.tube((x, 0, 2.95), (x, 2, 2.95), .027, 'stainless')
    for x in (-length/2+.1, length/2-.1):
        # Bay geometry is bottom-centred by finish(): its curtain hem is .535m
        # before recentering and its placement is .52m. Hangers must reach 3.6m.
        top = HEIGHT+.535-.52 if bay else 3.45
        m.tube((x, 0, 2.95), (x, 0, top), .012, 'stainless')
    for i in range(10):
        x = -length/2+.055+i*.070
        m.box((x, .045 if i % 2 else -.045, 1.66), (.073, .02, 2.25-(i%3)*.013), 'curtain')
        m.tube((x, 0, 2.79), (x, 0, 2.95), .012, 'stainless', 6)
    if bay:
        for i in range(8):
            m.box((-length/2+(.04 if i%2 else -.04), .10+i*.23, 1.66),
                  (.025, .24, 2.24), 'curtain')


def furniture(m, piece):
    if piece in ('prop_bed', 'prop_gurney'):
        bed = piece == 'prop_bed'
        width = 1.0 if bed else .72
        z = .60 if bed else .88
        wheels(m, width-.16, 1.68)
        m.box((0, 0, z-.10), (width, 2.05, .12), 'stainless', .02)
        m.box((0, 0, z+.035), (width-.04, 2, .17), 'linen' if bed else 'rubber', .04)
        m.box((0, -.7, z+.17), (width*.75, .45, .12), 'linen', .035)
        for y in (-.96, .96):
            for x in (-(width-.14)/2, (width-.14)/2):
                m.tube((x, y, z-.13), (x, y, z+.48), .024, 'stainless')
            for level in (z+.24, z+.46):
                m.tube((-(width-.14)/2, y, level), ((width-.14)/2, y, level), .024, 'stainless')
        for x in (-width/2, width/2):
            m.tube((x, -.66, z+.25), (x, .66, z+.25), .024, 'stainless')
    elif piece in ('prop_curtain_rail', 'curtain_track_bay'):
        curtain(m, piece == 'curtain_track_bay')
    elif piece == 'prop_cabinet':
        m.box((0, 0, 1.05), (.86, .44, 1.8), 'paint', .025)
        for x in (-.205, .205):
            m.box((x, .235, 1.10), (.39, .025, 1.52), 'tile')
            m.box((x, .251, 1.42), (.29, .012, .70), 'glass')
            m.tube((x*.3, .28, .87), (x*.3, .28, 1.02), .014, 'stainless')
        for x in (-.34, .34):
            for y in (-.16, .16):
                m.box((x, y, .09), (.06, .06, .18), 'stainless')
    elif piece == 'prop_wheelchair':
        m.box((0, 0, .5), (.47, .43, .065), 'rubber')
        m.box((0, -.20, .75), (.47, .07, .46), 'curtain')
        for x in (-.32, .32):
            m.ring((x, -.10, .32), .285, .035, 'rubber')
            m.ring((x*1.1, -.10, .32), .235, .012, 'stainless')
            for i in range(6):
                a = i*math.pi/3
                m.tube((x, -.10, .32), (x, -.10+.26*math.cos(a), .32+.26*math.sin(a)), .006, 'stainless', 4)
            m.tube((x*.78, -.23, .25), (x*.78, -.23, 1.04), .017, 'stainless')
            m.tube((x*.78, -.23, .48), (x*.78, .36, .17), .018, 'stainless')
            m.tube((x*.78, -.23, .73), (x*.78, .20, .73), .02, 'stainless')
            m.tube((x*.78-.03, .32, .09), (x*.78+.03, .32, .09), .085, 'rubber')
            m.box((x*.62, .42, .13), (.18, .22, .035), 'stainless')
    elif piece == 'prop_iv_stand':
        m.tube((0, 0, .14), (0, 0, 2.12), .022, 'stainless')
        m.tube((-.22, 0, 2.10), (.22, 0, 2.10), .017, 'stainless')
        for x in (-.2, .2):
            m.tube((x, 0, 2.1), (x, 0, 1.98), .014, 'stainless')
        m.box((.15, 0, 1.77), (.16, .07, .30), 'linen', .014)
        m.tube((.15, 0, 1.63), (.12, 0, .80), .006, 'grout', 6)
        for i in range(5):
            a = i*2*math.pi/5
            x, y = .32*math.cos(a), .32*math.sin(a)
            m.tube((0, 0, .17), (x, y, .08), .022, 'stainless')
            m.tube((x-.03, y, .06), (x+.03, y, .06), .06, 'rubber', 6)
    elif piece == 'prop_nurse_counter':
        m.box((0, 0, .53), (2.8, .7, 1.06), 'tile')
        m.box((0, 0, 1.10), (2.96, .85, .08), 'vinyl', .02)
        for x in (-.9, 0, .9):
            m.box((x, .36, .57), (.85, .024, .90), 'paint')
            m.box((x, .39, .89), (.30, .03, .035), 'stainless')
        m.box((.85, -.14, 1.20), (.38, .26, .12), 'rubber', .025)
    elif piece == 'prop_waiting_bench':
        m.box((0, -.25, .79), (1.8, .075, .52), 'curtain')
        for x in (-.60, 0, .60):
            m.box((x, 0, .49), (.55, .53, .11), 'curtain', .035)
        for x in (-.72, .72):
            for y in (-.20, .20):
                m.tube((x, y, .03), (x, y, .47), .023, 'stainless')
    elif piece == 'prop_signage_frame':
        m.box((0, 0, .25), (.9, .045, .5), 'stainless')
        m.box((0, .026, .25), (.84, .012, .44), 'paint')
        # Abstract blocks only: no readable signage or fake typography.
        for x in (-.25, -.05, .15):
            m.box((x, .035, .26), (.10, .006, .10), 'grout')
    elif piece == 'prop_scrub_sink':
        m.box((0, 0, .84), (.95, .62, .16), 'stainless', .04)
        m.box((0, 0, .928), (.62, .41, .014), 'rubber')
        for x in (-.34, .34):
            m.tube((x, -.20, 0), (x, -.20, .80), .035, 'stainless')
        m.tube((0, -.22, .91), (0, -.22, 1.2), .025, 'stainless')
        m.tube((0, -.22, 1.2), (0, .08, 1.2), .025, 'stainless')
    elif piece == 'prop_xray_screen':
        m.box((0, 0, 1.23), (1.42, .10, 2.12), 'tile')
        m.box((0, .057, 1.30), (1.23, .022, 1.68), 'glass')
        for x in (-.52, .52):
            m.box((x, 0, .08), (.10, .62, .16), 'stainless')
    elif piece == 'prop_operating_lamp':
        m.tube((0, 0, .10), (0, 0, .88), .036, 'stainless')
        m.tube((0, 0, .12), (.55, 0, .12), .04, 'stainless')
        m.tube((.55, 0, .12), (.55, 0, 0), .30, 'stainless', 12)
        m.tube((.55, 0, -.005), (.55, 0, -.014), .25, 'paint', 12)


def build_piece(piece):
    m = WardMesh(piece)
    if piece in ('wall_2m', 'wall_1m', 'wall_closed_4m'):
        width = {'wall_2m': 2, 'wall_1m': 1, 'wall_closed_4m': 4}[piece]
        wall(m, -width/2, width/2)
    elif piece == 'wall_door_4m':
        wall(m, -2, -1.6)
        wall(m, 1.6, 2)
        wall(m, -1.6, 1.6, 2.8, HEIGHT)
    elif piece == 'wall_window_2m':
        wall(m, -1, -.65)
        wall(m, .65, 1)
        wall(m, -.65, .65, 0, 1.1)
        wall(m, -.65, .65, 2.5, HEIGHT)
    elif piece.startswith('wall_arc_'):
        radius = int(piece[-1])
        arc(m, radius, {4: 30, 6: 20, 8: 15}[radius])
    elif piece in ('corner_in', 'corner_out'):
        for a, b in ((0, DADO), (DADO, HEIGHT)):
            m.box((0, -.125, (a+b)/2), (.5, .25, b-a), 'tile' if a == 0 else 'paint')
            m.box((-.125, .125, (a+b)/2), (.25, .25, b-a), 'tile' if a == 0 else 'paint')
        if piece == 'corner_out':
            m.vertices = [(-x, -y, z) for x, y, z in m.vertices]
    elif piece == 'pillar':
        m.box((0, 0, .90), (.6, .6, 1.8), 'tile')
        m.box((0, 0, 2.7), (.6, .6, 1.8), 'paint')
    elif piece == 'floor_2x2':
        m.box((0, 0, .08), (2, 2, .16), 'vinyl')
    elif piece in ('ceiling_2x2', 'ceiling_drop_panel_2x2'):
        ceiling(m)
    elif piece == 'trim_base_2m':
        m.box((0, 0, .09), (2, .12, .18), 'rubber')
    elif piece == 'door_double_porthole_4m':
        door_leaves(m)
    elif piece.startswith('light_fluorescent'):
        m.box((0, 0, .07), (.98, .98, .14), 'stainless')
        m.box((0, 0, .008), (.89, .89, .016), 'light' if piece.endswith('panel') else 'grout')
        for x in (-.29, 0, .29):
            m.box((x, 0, .005), (.012, .88, .01), 'stainless')
    elif piece == 'wall_handrail_2m':
        m.box((0, 0, .045), (2, .09, .09), 'curtain', .018)
        for x in (-.72, .72):
            m.box((x, -.07, .045), (.065, .12, .055), 'stainless')
    elif piece == 'corner_guard':
        m.box((0, -.028, .90), (.12, .018, 1.8), 'stainless')
        m.box((-.051, .025, .90), (.018, .088, 1.8), 'stainless')
    elif piece == 'wall_tile_dado_2m':
        m.box((0, 0, .9), (2, .028, 1.8), 'tile')
    elif piece == 'window_ward_2m':
        m.box((0, 0, .70), (1.29, .05, 1.39), 'glass')
        for x in (-.61, 0, .61):
            m.box((x, .031, .70), (.04, .03, 1.39), 'stainless')
        for z in (.02, 1.37):
            m.box((0, .031, z), (1.29, .03, .04), 'stainless')
    else:
        furniture(m, piece)
    # Wall-plane pivots stay fixed. Portable props use their measured bottom centre.
    return m.finish(center=KINDS[piece] == 'prop' or piece in ('wall_handrail_2m', 'corner_guard'))


def geometry_hash(obj):
    rows = []
    for face in obj.data.polygons:
        points = []
        for index in face.vertices:
            p = obj.data.vertices[index].co
            points.append(tuple(0.0 if abs(c) < .000005 else round(c, 5) for c in (p.x, p.z, -p.y)))
        rows.append((obj.data.materials[face.material_index].name, sorted(points)))
    return hashlib.sha256(json.dumps(sorted(rows), separators=(',', ':')).encode()).hexdigest()


def export_piece(obj, piece):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(ART/'Kit'/(obj.name+'.fbx')), use_selection=True,
        object_types={'MESH'}, global_scale=1, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE',
        use_triangles=True, bake_anim=False, use_custom_props=False, path_mode='RELATIVE')
    low = [min(v.co[i] for v in obj.data.vertices) for i in range(3)]
    high = [max(v.co[i] for v in obj.data.vertices) for i in range(3)]
    triangles = len(obj.data.polygons)
    assert triangles <= (1500 if KINDS[piece] == 'prop' else 300), (piece, triangles)
    return {'id': piece, 'file': obj.name+'.fbx', 'kind': KINDS[piece],
            'size': [round(high[i]-low[i], 6) for i in (0, 2, 1)], 'triangles': triangles,
            'materials': sorted(mat.name for mat in obj.data.materials), 'geometrySha256': geometry_hash(obj)}


SIDES = {'S': (0, -1, 180), 'N': (0, 1, 0), 'W': (-1, 0, 270), 'E': (1, 0, 90)}


def placement(piece, x, y, z, angle=0, **metadata):
    return dict(id=piece, pos=[round(x, 5), round(y, 5), round(z, 5)], rotY=angle, **metadata)


def edges(cells):
    result = []
    for x, z in sorted(cells):
        for side, (dx, dz, angle) in SIDES.items():
            if (x+dx, z+dz) not in cells:
                result.append(((x, z), side, (2*x+1+dx, 2*z+1+dz), angle))
    return result


def assemble_template(name, cells, kind, shape, doors, props, gimmick='none'):
    cells = set(cells)
    n = len(cells)
    size_class = next(size for limit, size in ((4, 'closet'), (9, 'small'), (20, 'medium'), (40, 'large'), (999, 'hall')) if n <= limit)
    boundary = edges(cells)
    by_line = {}
    for _, side, (x, z), _ in boundary:
        line, along = (z, x) if side in 'NS' else (x, z)
        by_line.setdefault((side, line), []).append((along-1, along+1))
    pieces, sockets = [], []
    portal_spans = {}
    for cell, side in doors:
        entry = next(e for e in boundary if e[0] == cell and e[1] == side)
        _, _, (x, z), angle = entry
        line, along = (z, x) if side in 'NS' else (x, z)
        portal_spans.setdefault((side, line), []).append((along-2, along+2))
        closed = placement('wall_closed_4m', x, 0, z, angle)
        sockets.append({'cell': list(cell), 'side': side, 'closedWith': [closed]})
        pieces.append(placement('wall_door_4m', x, 0, z, angle))
        pieces.append(placement('door_double_porthole_4m', x, 0, z, angle))
    for (side, line), spans in sorted(by_line.items()):
        # Merge boundary cell edges, subtract complete four-metre portal frames.
        runs = []
        for low, high in sorted(spans):
            if runs and runs[-1][1] == low:
                runs[-1][1] = high
            else:
                runs.append([low, high])
        for low, high in portal_spans.get((side, line), []):
            assert any(a <= low and high <= b for a, b in runs), (name, 'portal runs off boundary')
            runs = [part for a, b in runs for part in
                    ([[a, b]] if b <= low or a >= high else [[a, low], [high, b]]) if part[1] > part[0]]
        for low, high in runs:
            while high-low > .001:
                width = min(2, high-low)
                center = low+width/2
                x, z = (center, line) if side in 'NS' else (line, center)
                angle = SIDES[side][2]
                pieces.append(placement('wall_2m' if width == 2 else 'wall_1m', x, 0, z, angle))
                if width == 2:
                    dx, dz, _ = SIDES[side]
                    pieces.append(placement('wall_handrail_2m', x-dx*.34, .95, z-dz*.34, angle))
                    if int(center+line) % 4 == 1:
                        pieces.append(placement('prop_signage_frame', x-dx*.28, 2.05, z-dz*.28, angle))
                low += width
    for x, z in sorted(cells):
        pieces.append(placement('floor_2x2', x*2+1, -.16, z*2+1))
        pieces.append(placement('ceiling_drop_panel_2x2', x*2+1, HEIGHT, z*2+1))
    # Light distribution is authored and sparse; a dead luminaire has no light socket.
    light_cells = [(x, z) for x, z in sorted(cells) if (x+2*z)%4 == 1]
    if not light_cells:
        light_cells = [sorted(cells)[0]]
    lights = []
    for i, (x, z) in enumerate(light_cells):
        dead = i % 5 == 4
        pieces.append(placement('light_fluorescent_dead' if dead else 'light_fluorescent_panel',
                                2*x+1, HEIGHT-.14, 2*z+1))
        if not dead:
            lights.append([2*x+1, HEIGHT-.18, 2*z+1])
    pieces.extend(props)
    # Centerline gameplay sockets; reject furniture footprints before sampling.
    occupied = []
    for p in props:
        if p['pos'][1] > 1.0:
            continue
        obj = bpy.data.objects['Hospital_'+p['id']]
        sx, sy = obj.dimensions.x, obj.dimensions.y
        angle = math.radians(p['rotY'])
        ex = abs(sx*math.cos(angle))+abs(sy*math.sin(angle))
        ez = abs(sx*math.sin(angle))+abs(sy*math.cos(angle))
        occupied.append((p['pos'][0], p['pos'][2], ex/2+.35, ez/2+.35))
    candidates = [[2*x+1, 0, 2*z+1] for x, z in sorted(cells)
                  if not any(abs(2*x+1-a) < ex and abs(2*z+1-b) < ez for a, b, ex, ez in occupied)]
    assert len(candidates) >= 2, (name, 'no free cake sockets')
    count = min(len(candidates), max(2, math.ceil(n/5)))
    selected = [candidates[round(i*(len(candidates)-1)/max(1, count-1))] for i in range(count)]
    return {'id': 'hospital_'+name, 'kind': kind, 'sizeClass': size_class, 'shape': shape,
            'footprint': [list(c) for c in sorted(cells)], 'height': HEIGHT, 'doors': sockets,
            'anchors': {'cake': selected, 'goldenCake': [candidates[len(candidates)//2]] if n >= 10 else [],
                        'light': lights, 'hunterSpawn': [candidates[-1]] if n >= 10 else []},
            'gimmick': gimmick, 'minRound': 3 if gimmick != 'none' else 1, 'weight': .6 if gimmick != 'none' else 1.0,
            'pieces': pieces}


def catalogue():
    def rect(w, d):
        return {(x, z) for x in range(w) for z in range(d)}
    def p(piece, x, z, angle=0, y=0):
        return placement(piece, x, y, z, angle)
    def bed_bay(x, z, angle=0):
        return [p('prop_bed', x, z, angle), p('curtain_track_bay', x, z, angle, y=.52), p('prop_iv_stand', x+.72, z+.65)]
    standard = [((1, 0), 'S'), ((1, 3), 'N')]
    specs = []
    specs.append(('ward_bed_bays', rect(4, 4), 'room', 'rect', standard,
                  bed_bay(1.8, 5.8)+bed_bay(6.2, 5.8)+[p('prop_cabinet', 7.1, 1.0, 90)], 'none'))
    lcells = rect(4, 3) | {(x, 3) for x in range(2)}
    specs.append(('nurse_station', lcells, 'junction', 'L', [((1, 0), 'S'), ((3, 1), 'E'), ((0, 2), 'W')],
                  [p('prop_nurse_counter', 3.0, 2.9), p('prop_cabinet', 1, 6.8, 180), p('prop_wheelchair', 1.1, 4.4, 90)], 'none'))
    specs.append(('operating_theatre', rect(4, 3), 'room', 'rect', [((1, 0), 'S'), ((2, 2), 'N')],
                  [p('prop_gurney', 4.5, 3.1), p('prop_operating_lamp', 4.6, 3.2,
                    y=HEIGHT-bpy.data.objects['Hospital_prop_operating_lamp'].dimensions.z),
                   p('prop_scrub_sink', 6.9, .65, 180), p('prop_cabinet', 7.3, 4.2, 90), p('prop_iv_stand', 3.4, 3.2)], 'none'))
    specs.append(('recovery_annex', lcells, 'room', 'L', [((1, 0), 'S'), ((0, 2), 'W')],
                  bed_bay(6.1, 3.9)+[p('prop_waiting_bench', 2, 6.9), p('prop_iv_stand', 4.9, 4.5)], 'none'))
    specs.append(('waiting_room', rect(3, 3), 'room', 'rect', [((1, 0), 'S'), ((1, 2), 'N')],
                  [p('prop_waiting_bench', .8, 3, 270), p('prop_waiting_bench', 5.2, 3, 90), p('prop_wheelchair', 5.1, 4.8, 90)], 'none'))
    # Four cells arranged as a narrow isolation suite; side socket fits the full portal.
    specs.append(('isolation_room', rect(1, 4), 'room', 'rect', [((0, 1), 'W')],
                  [p('prop_bed', 1, 6.35), p('prop_signage_frame', 1, 7.72, y=2.0)], 'none'))
    specs.append(('sluice_utility', rect(3, 2), 'room', 'rect', [((1, 0), 'S'), ((1, 1), 'N')],
                  [p('prop_scrub_sink', .8, 2, 270), p('prop_cabinet', 5.25, 2, 90)], 'none'))
    specs.append(('xray_room', rect(3, 3), 'room', 'rect', [((1, 0), 'S'), ((1, 2), 'N')],
                  [p('prop_gurney', 4.4, 3.2), p('prop_xray_screen', 1.25, 3.6, 90), p('prop_cabinet', 5.2, 1, 90)], 'none'))
    specs.append(('long_ward_corridor', rect(2, 7), 'hallway', 'rect', [((0, 1), 'W'), ((1, 5), 'E')],
                  [p('prop_wheelchair', .75, 7.2, 180), p('prop_signage_frame', 2, 13.7, y=2.05)], 'none'))
    bend = rect(2, 5) | {(x, z) for x in range(2, 5) for z in range(3, 5)}
    specs.append(('corridor_bend', bend, 'hallway', 'L', [((0, 1), 'W'), ((3, 4), 'N')],
                  [p('prop_gurney', .8, 6.2), p('corner_guard', 3.75, 6.25, y=0)], 'none'))
    specs.append(('day_room', rect(6, 4), 'room', 'rect', [((1, 0), 'S'), ((4, 3), 'N')],
                  [p('prop_waiting_bench', x, z, a) for x, z, a in ((1, 4, 270), (11, 4, 90), (5, 6.8, 0), (8, 6.8, 0))]
                  +[p('prop_nurse_counter', 8.2, 1.1, 180), p('prop_wheelchair', 5.8, 4.3, 25)], 'none'))
    specs.append(('nightingale_landmark', rect(7, 6), 'room', 'rect', [((2, 0), 'S'), ((4, 5), 'N'), ((6, 2), 'E')],
                  sum([bed_bay(x, z, a) for x, z, a in ((2, 3, 0), (2, 7, 0), (11.8, 3, 0), (11.8, 7, 0))], [])
                  +[p('prop_nurse_counter', 7, 10.5), p('prop_gurney', 7, 6.5, 90)], 'none'))
    specs.append(('isolation_door_freeze', rect(3, 3), 'room', 'rect', [((1, 0), 'S'), ((1, 2), 'N')],
                  [p('prop_bed', 4.7, 3.5), p('prop_xray_screen', 1.1, 4, 90), p('prop_iv_stand', 4.0, 3.4)], 'freeze'))
    specs.append(('gurney_maze', rect(5, 4), 'room', 'rect', [((1, 0), 'S'), ((3, 3), 'N')],
                  [p('prop_gurney', x, z, 90) for x, z in ((2, 2.5), (5, 4.0), (8, 5.6))]
                  +[p('prop_xray_screen', 8.7, 2.6, 90), p('prop_cabinet', 1, 6.8)], 'traversal'))
    return [assemble_template(*spec) for spec in specs]


def instance(source, collection, name, pos, angle=0):
    obj = source.copy()
    obj.data = source.data
    obj.name = name
    collection.objects.link(obj)
    obj.hide_render = False
    obj.hide_set(False)
    obj.location = (pos[0], -pos[2], pos[1])
    obj.rotation_euler.z = math.radians(angle)
    return obj


def lamp(collection, name, location, power, kind='AREA', target=None):
    data = bpy.data.lights.new(name, kind)
    data.energy = power
    data.color = linear(PALETTE['light']) if kind == 'AREA' else (1, .94, .84)
    if kind == 'AREA':
        data.shape, data.size = 'SQUARE', .86
    else:
        data.spot_size, data.spot_blend = math.radians(48), .5
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.location = location
    if target is not None:
        obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z', 'Y').to_euler()
    return obj


def render_setup():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = SAMPLES
    scene.cycles.use_denoising = True
    scene.cycles.seed = SEED
    scene.render.resolution_x, scene.render.resolution_y = 1100, 850
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.world = bpy.data.worlds.new('Hospital review ambient')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.30, .32, .31, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .28
    scene.view_settings.view_transform = 'AgX'
    data = bpy.data.cameras.new('HospitalReviewCamera')
    camera = bpy.data.objects.new('HospitalReviewCamera', data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    data.type = 'ORTHO'
    return camera


def aim(camera, location, target, scale):
    camera.location = location
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.ortho_scale = scale


def render(name):
    bpy.context.scene.render.filepath = str(OUT/(name+'.png'))
    bpy.ops.render.render(write_still=True)


def kit_sheet(objects, camera, skip):
    col = bpy.data.collections.new('Kit sheet - every exported piece')
    bpy.context.scene.collection.children.link(col)
    for i, (piece, obj) in enumerate(objects.items()):
        x, z = i%7*5, i//7*6
        instance(obj, col, 'Sheet_'+piece, (x, 0, z))
        data = bpy.data.curves.new('Label_'+piece, 'FONT')
        data.body = piece
        data.size = .22
        data.align_x = 'CENTER'
        label = bpy.data.objects.new('Label_'+piece, data)
        col.objects.link(label)
        label.location = (x, -z+1.6, .03)
        data.materials.append(bpy.data.materials['hospital_rubber'])
    key = lamp(col, 'Sheet softbox', (12, -10, 20), 7000, target=(12, -12, 0))
    key.data.size = 25
    aim(camera, (33, 23, 32), (15, -15, 1), 46)
    if not skip:
        old = (bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y)
        bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y = 2100, 1650
        render('kit-sheet')
        bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y = old
    col.hide_render = True
    col.hide_viewport = True


def room_sources(objects, templates, camera, skip):
    scene = bpy.context.scene
    evidence = []
    room_cols = []
    for template in templates:
        col = bpy.data.collections.new(template['id'])
        scene.collection.children.link(col)
        room_cols.append(col)
        cells = template['footprint']
        w, d = 2*(max(c[0] for c in cells)+1), 2*(max(c[1] for c in cells)+1)
        hidden = []
        for i, p in enumerate(template['pieces']):
            obj = instance(objects[p['id']], col, f"{template['id']}__{i:04d}__{p['id']}", p['pos'], p['rotY'])
            obj['pieceId'] = p['id']
            obj['placementIndex'] = i
            # Only the review visibility changes. The source retains EVERY placement.
            # A cutaway removes the roof AND its luminaires. Leaving isolated
            # dark luminaire backs can look like slabs over the beds.
            # Exact full-height placements remain in the manifest/source.
            ceiling_piece = KINDS[p['id']] == 'ceiling' or p['id'] in (
                'light_fluorescent_panel', 'light_fluorescent_dead', 'prop_operating_lamp')
            near_wall = KINDS[p['id']] in ('wall', 'door') and p['rotY'] in (180, 90)
            near_dressing = p['id'] in ('wall_handrail_2m', 'prop_signage_frame') and p['rotY'] in (180, 90)
            cut = ceiling_piece or near_wall or near_dressing
            if cut:
                obj.hide_render = True
                hidden.append(i)
        for i, a in enumerate(template['anchors']['light']):
            lamp(col, f"{template['id']}_fluorescent_{i}", (a[0], -a[2], a[1]-.025), LIGHT_WATTS)
        aim(camera, (w*1.22, d*.70, max(w, d)*.86+4), (w*.48, -d*.48, 1.1), max(w, d)*1.45+2)
        if not skip:
            render(template['id']+'-three-quarter')
        evidence.append({'id': template['id'], 'placements': len(template['pieces']),
                         'cutawayPlacementIndices': hidden, 'reviewOnly': True})
        col.hide_render = True
        col.hide_viewport = True
    representative = templates[0]
    col = room_cols[0]
    col.hide_render = False
    col.hide_viewport = False
    # Darkness view uses the complete room, not a cutaway or an invisible fill lamp.
    for obj in col.objects:
        obj.hide_render = False
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = 0
    fluorescent = [o for o in col.objects if o.type == 'LIGHT']
    for i, obj in enumerate(fluorescent):
        obj.data.energy = 40 if i == 0 else 0
    flash = lamp(col, 'Darkness flashlight ONLY', (3.2, -.45, 1.5), 65, 'SPOT', (4.3, -6.2, 1.15))
    camera.data.type = 'PERSP'
    camera.data.lens = 21
    aim(camera, (3.1, -.48, 1.62), (4.0, -5.8, 1.50), 12)
    if not skip:
        render('in-darkness')
    flash.hide_render = True
    for obj in fluorescent:
        obj.data.energy = LIGHT_WATTS
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .28
    camera.data.type = 'ORTHO'
    for index in evidence[0]['cutawayPlacementIndices']:
        bpy.data.objects[f"{representative['id']}__{index:04d}__{representative['pieces'][index]['id']}"].hide_render = True
    aim(camera, (9.76, 5.6, 10.88), (3.84, -3.84, 1.1), 13.6)
    scene['manifest'] = str((ART/'Rooms/HospitalRooms.manifest.json').relative_to(ROOT))
    scene['reviewInstructions'] = 'One collection per template at local origin. Toggle collections; cutaway affects renders only. All manifest placements retained.'
    (OUT/'review-scenes.json').write_text(json.dumps({'rooms': evidence, 'darkness': {
        'room': representative['id'], 'worldStrength': 0, 'fluorescentWatts': 40,
        'flashlightWatts': 65, 'otherLights': 0, 'completeShell': True}}, indent=2)+'\n', encoding='utf-8')
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Rooms/HospitalRooms.blend'))


def main():
    parser = argparse.ArgumentParser(description='Self-contained 1970s Hospital kit and room authoring')
    parser.add_argument('--skip-previews', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    assert bpy.app.version[:2] == (5, 2), 'Use Blender 5.2'
    assert ROOT.name in ('theme-hospital', 'art-fixes') and (ROOT/'.git').is_file(), \
        'Publish only to an authorized isolated worktree, never the shared checkout'
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    for directory in (ART/'Kit', ART/'Rooms', SOURCE/'Kit', SOURCE/'Rooms', OUT):
        directory.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Kit/HospitalKit.blend'))
    materials()
    objects, rows = {}, []
    for piece in KINDS:
        obj = build_piece(piece)
        objects[piece] = obj
        rows.append(export_piece(obj, piece))
    kit = {'theme': 'hospital', 'wallHeight': HEIGHT, 'style': '1970s institutional ward',
           'palette': PALETTE, 'pieces': rows}
    templates = catalogue()
    rooms = {'theme': 'hospital', 'module': 2.0, 'templates': templates}
    for path, value in ((ART/'Kit/HospitalKit.manifest.json', kit), (ART/'Rooms/HospitalRooms.manifest.json', rooms)):
        path.write_text(json.dumps(value, indent=2)+'\n', encoding='utf-8', newline='\n')
        print(f'GENERATED {path.name}: SHA256 {hashlib.sha256(path.read_bytes()).hexdigest()}')
    for obj in objects.values():
        obj.hide_render = True
        obj.hide_set(True)
    camera = render_setup()
    kit_sheet(objects, camera, args.skip_previews)
    sheet = bpy.data.collections['Kit sheet - every exported piece']
    sheet.hide_render = False
    sheet.hide_viewport = False
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Kit/HospitalKit.blend'))
    sheet.hide_render = True
    sheet.hide_viewport = True
    room_sources(objects, templates, camera, args.skip_previews)
    print(f'GENERATED hospital: {len(rows)} pieces; {len(templates)} templates; four 512px textures')


if __name__ == '__main__':
    main()
