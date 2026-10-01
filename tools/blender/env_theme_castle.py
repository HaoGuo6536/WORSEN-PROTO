# ============================================================================
# env_theme_castle.py
# PURPOSE:
#   Author the Castle as a cold Gothic keep, not a reskin of another theme.
#   Rebuild editable kit/room sources, isolated exports and manifest-driven
#   review scenes deterministically, without Unity or external art inputs.
# ARCHITECTURAL ROLE: Offline art generator; outside Unity runtime layers.
# KEY RESPONSIBILITIES:
#   - Build original low-poly masonry, pointed openings, vaults and furnishings.
#   - Export metre-scale pieces with stable IDs, materials and geometry digests.
#   - Author grid/curved rooms, supported dressing, end caps and vault shortcuts.
#   - Assemble exactly the manifest placements and render review evidence.
#   - Render reusable front/back door inspection scenes for all theme generators.
# DEPENDENCIES: Blender 5.2 bpy/bmesh/mathutils, bundled NumPy, Python stdlib.
# USAGE NOTES:
#   Blender --background --factory-startup --python-exit-code 1 --python FILE
#   -- [--skip-previews]. Outputs only Castle art and Logs in this worktree.
#   Blender +Z -> Unity +Y; Blender +Y -> Unity -Z. Seed is explicit.
#   Traversal metadata requests runtime collision/tagging; no Unity API is used.
# ============================================================================
import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import sys

import bmesh
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/Art/Environment/Castle'
SOURCE = ROOT / 'ArtSource/Environment/Castle'
OUTPUT = ROOT / 'Logs/AgentValidation/Art/EnvCastle'
SEED = 260930
HEIGHT = 7.0
PALETTE = {'stone': '#4a4f55', 'stone_dark': '#2e3136', 'soot': '#141416',
           'mortar': '#6b675f', 'wood': '#4a3423', 'metal': '#26282b'}
COMMON = {'wall_2m': 'wall', 'wall_door_4m': 'door', 'wall_window_2m': 'window',
          'wall_arc_r4': 'arc', 'wall_arc_r6': 'arc', 'wall_arc_r8': 'arc',
          'corner_in': 'corner', 'corner_out': 'corner', 'pillar': 'pillar',
          'floor_2x2': 'floor', 'ceiling_2x2': 'ceiling', 'trim_base_2m': 'trim',
          'prop_torch_sconce': 'prop', 'prop_banner': 'prop',
          'prop_barrel': 'prop', 'prop_rubble': 'prop'}
EXTRA = {'arch_pointed_door': 'door', 'wall_arrow_slit_2m': 'window',
         'vault_rib_bay': 'ceiling', 'vault_web_bay': 'ceiling', 'beam_timber_2m': 'ceiling',
         'tower_wall_arc_r4': 'arc', 'stair_stone_2m': 'floor',
         'door_iron_strapped': 'prop', 'prop_candelabra': 'prop',
         'prop_chain_hanging': 'prop', 'prop_weapon_rack': 'prop',
         'prop_portcullis': 'prop', 'prop_trestle_table': 'prop',
         'prop_bench': 'prop', 'prop_altar': 'prop', 'prop_soot_streak': 'prop',
         'prop_torch_dead': 'prop', 'floor_broken_2x2': 'floor',
         'wall_end_1m': 'wall', 'wall_door_closed_4m': 'wall',
         'prop_cell_grille': 'prop', 'prop_cauldron': 'prop',
         'wall_concave_1p2m': 'wall', 'wall_round_tangent_r4': 'wall',
         'floor_disk_r4': 'floor', 'ceiling_disk_r4': 'ceiling',
         'floor_apse_r4': 'floor', 'ceiling_apse_r4': 'ceiling',
         'floor_portal_4m': 'floor'}


def linear(hex_color):
    rgb = [int(hex_color[i:i+2], 16)/255 for i in (1, 3, 5)]
    return tuple(c/12.92 if c <= .04045 else ((c+.055)/1.055)**2.4 for c in rgb)


class MasonMesh:
    """Deterministic mesh accumulator, authored Z-up with +Y detailed face."""
    def __init__(self, piece):
        self.piece = piece
        self.vertices, self.faces, self.surfaces = [], [], []
        self.rng = random.Random(f'{SEED}:{piece}')

    def add(self, vertices, faces, surface):
        start = len(self.vertices)
        self.vertices.extend(tuple(v) for v in vertices)
        self.faces.extend(tuple(start+i for i in f) for f in faces)
        self.surfaces.extend([surface]*len(faces))

    def box(self, center, size, surface, bevel=0):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1)
        for v in bm.verts:
            v.co = Vector(tuple(v.co[i]*size[i]+center[i] for i in range(3)))
        if bevel:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect='EDGES')
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.verts.ensure_lookup_table()
        bm.verts.index_update()
        self.add([v.co for v in bm.verts], [tuple(v.index for v in f.verts) for f in bm.faces], surface)
        bm.free()

    def prism(self, polygon, front, back, surface):
        # polygon is counter-clockwise in the X/Z plane, +Y is front.
        n = len(polygon)
        vertices = [(x, y, z) for y in (back, front) for x, z in polygon]
        faces = [tuple(range(n)), tuple(reversed(range(n, 2*n)))]
        faces += [(i, i+n, (i+1)%n+n, (i+1)%n) for i in range(n)]
        self.add(vertices, faces, surface)

    def panel(self, x0, x1, z0, z1, surface, front=.4, back=.29):
        b = min(.045, (x1-x0)/7, (z1-z0)/7)
        j = self.rng.uniform(-b*.3, b*.3)
        self.add([(x0, back, z0), (x0, back, z1), (x1, back, z1), (x1, back, z0),
                  (x0+b, front, z0+b), (x0+b+j, front, z1-b),
                  (x1-b, front, z1-b+j), (x1-b-j, front, z0+b)],
                 [(4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5),
                  (2, 3, 7, 6), (3, 0, 4, 7)], surface)

    def rod(self, a, b, radius, surface, sides=6):
        a, b = Vector(a), Vector(b)
        rotation = (b-a).to_track_quat('Z', 'Y').to_matrix()
        vertices = [p + rotation @ Vector((radius*math.cos(i*math.tau/sides),
                     radius*math.sin(i*math.tau/sides), 0)) for p in (a, b) for i in range(sides)]
        self.add(vertices, [tuple(reversed(range(sides))), tuple(range(sides, sides*2))] +
                 [(i, (i+1)%sides, (i+1)%sides+sides, i+sides) for i in range(sides)], surface)

    def ring(self, center, radius, surface, vertical=True):
        points = [(center[0]+radius*math.cos(t*math.tau/8),
                   center[1]+(0 if vertical else radius*math.sin(t*math.tau/8)),
                   center[2]+(radius*math.sin(t*math.tau/8) if vertical else 0)) for t in range(8)]
        for a, b in zip(points, points[1:]+points[:1]):
            self.rod(a, b, .017, surface, 4)

    def finish(self, centered=False):
        if centered:
            lo = [min(v[i] for v in self.vertices) for i in range(3)]
            hi = [max(v[i] for v in self.vertices) for i in range(3)]
            shift = ((lo[0]+hi[0])/2, (lo[1]+hi[1])/2, lo[2])
            self.vertices = [tuple(v[i]-shift[i] for i in range(3)) for v in self.vertices]
        data = bpy.data.meshes.new('Castle_'+self.piece)
        data.from_pydata(self.vertices, [], self.faces)
        data.update()
        obj = bpy.data.objects.new(data.name, data)
        bpy.context.collection.objects.link(obj)
        surfaces = sorted(set(self.surfaces))
        for surface in surfaces:
            data.materials.append(bpy.data.materials['castle_'+surface])
        for face, surface in zip(data.polygons, self.surfaces):
            face.material_index = surfaces.index(surface)
        uv = data.uv_layers.new(name='SurfaceMetres')
        for face in data.polygons:
            axis = max(range(3), key=lambda i: abs(face.normal[i]))
            axes = [i for i in range(3) if i != axis]
            for loop in face.loop_indices:
                p = data.vertices[data.loops[loop].vertex_index].co
                uv.data[loop].uv = (p[axes[0]], p[axes[1]])
        bm = bmesh.new()
        bm.from_mesh(data)
        bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method='FIXED', ngon_method='EAR_CLIP')
        bm.to_mesh(data)
        bm.free()
        data.update()
        return obj


def materials():
    import numpy as np
    for surface, color in dict(PALETTE, ember='#ffb347').items():
        mat = bpy.data.materials.new('castle_'+surface)
        mat.diffuse_color = (*linear(color), 1)
        shader = mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value = mat.diffuse_color
        shader.inputs['Roughness'].default_value = .92 if surface != 'metal' else .72
        shader.inputs['Metallic'].default_value = .72 if surface == 'metal' else 0
        if surface == 'ember':
            shader.inputs['Emission Color'].default_value = (*linear(color), 1)
            shader.inputs['Emission Strength'].default_value = 8
        if surface not in ('stone', 'stone_dark', 'wood'):
            continue
        rng = np.random.default_rng(SEED + list(PALETTE).index(surface))
        y, x = np.mgrid[0:512, 0:512]
        wave = np.sin(x*.019 + np.sin(y*.037))*np.cos(y*.025)
        noise = rng.uniform(-.1, .1, (512, 512))
        factor = .87 + .12*wave + noise
        if surface == 'wood':
            factor = .86 + .10*np.sin(x*.24 + np.sin(y*.016)*2) + noise*.45
        srgb = np.array([int(color[i:i+2], 16)/255 for i in (1, 3, 5)])
        pixels = np.ones((512, 512, 4), dtype=np.float32)
        pixels[:, :, :3] = np.clip(factor[:, :, None]*srgb, 0, 1)
        image = bpy.data.images.new('castle_'+surface+'_wear', width=512, height=512)
        image.pixels.foreach_set(pixels.ravel())
        image.filepath_raw = str(SOURCE/'Kit'/(image.name+'.png'))
        image.file_format = 'PNG'
        image.save()
        (ART/'Kit'/(image.name+'.png')).write_bytes((SOURCE/'Kit'/(image.name+'.png')).read_bytes())
        image.filepath = bpy.path.relpath(str(ART/'Kit'/(image.name+'.png')))
        image.pack()
        node = mat.node_tree.nodes.new('ShaderNodeTexImage')
        node.image = image
        mat.node_tree.links.new(node.outputs['Color'], shader.inputs['Base Color'])
        bump = mat.node_tree.nodes.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = .28
        bump.inputs['Distance'].default_value = .055 if surface != 'wood' else .015
        mat.node_tree.links.new(node.outputs['Color'], bump.inputs['Height'])
        mat.node_tree.links.new(bump.outputs['Normal'], shader.inputs['Normal'])


def masonry(m, x0, x1, z0, z1, rows=7, columns=2):
    m.box(((x0+x1)/2, -.055, (z0+z1)/2), (x1-x0, .69, z1-z0), 'mortar')
    # Ends are never relieved: exact 0.8m end caps mate with every wall variant.
    for x in (x0+.012, x1-.012):
        m.box((x, 0, (z0+z1)/2), (.024, .8, z1-z0), 'stone_dark')
    heights = [z0] + [z0+(z1-z0)*(r/rows + .018*math.sin(r*2.1)) for r in range(1, rows)] + [z1]
    for row, (low, high) in enumerate(zip(heights, heights[1:])):
        cuts = [x0+.025] + [x0+(x1-x0)*(c/columns + (.12 if row%2 else -.1))
                          for c in range(1, columns)] + [x1-.025]
        for a, b in zip(cuts, cuts[1:]):
            m.panel(a+.023, b-.023, low+.028, high-.028,
                    'stone_dark' if m.rng.random() < .28 else 'stone',
                    front=m.rng.uniform(.37, .4))


def lancet_points(half_width, spring, rise):
    # Two opposing circular arcs meet at a pointed crown, not a triangular lintel.
    radius = (half_width*half_width + rise*rise)/(2*half_width)
    points = []
    for i in range(5):
        z = rise*i/4
        x = half_width-radius+math.sqrt(max(0, radius*radius-z*z))
        points.append((x, spring+z))
    return [(-x, z) for x, z in points] + list(reversed(points[:-1]))


def pointed_wall(m, width, opening, sill, spring, rise):
    half = opening/2
    # Low-detail jambs reserve budget for cut stone voussoirs above the opening.
    masonry(m, -width/2, -half, 0, HEIGHT, rows=4, columns=1)
    masonry(m, half, width/2, 0, HEIGHT, rows=4, columns=1)
    if sill:
        m.box((0, 0, sill/2), (opening, .8, sill), 'stone_dark')
    pts = lancet_points(half, spring, rise)
    # Each trapezoid spans from its lancet intrados to the 7m wall top.
    for i, (a, b) in enumerate(zip(pts, pts[1:])):
        m.prism([a, b, (b[0], HEIGHT), (a[0], HEIGHT)], .4, -.4,
                'stone_dark' if i%3 == 0 else 'stone')
        # Separate iron-dark joints read as radial voussoir courses.
    # Masonry panels above the crown retain the deep-jointed wall language.
    for a, b in ((-half+.04, -.02), (.02, half-.04)):
        m.panel(a, b, spring+rise+.10, HEIGHT-.04, 'stone', front=.4, back=.34)


def arc(m, radius, degrees):
    half = math.radians(degrees)/2
    steps = 4
    verts = []
    for i in range(steps+1):
        t = -half+2*half*i/steps
        verts += [(r*math.sin(t), radius-r*math.cos(t), z)
                  for r, z in ((radius-.4, 0), (radius+.4, 0), (radius+.4, HEIGHT), (radius-.4, HEIGHT))]
    faces = [(0, 1, 2, 3), tuple(reversed([steps*4+i for i in range(4)]))]
    for i in range(steps):
        faces += [(i*4+j, (i+1)*4+j, (i+1)*4+(j+1)%4, i*4+(j+1)%4) for j in range(4)]
    m.add(verts, faces, 'mortar')
    for row in range(5):
        for col in range(steps):
            t0, t1 = -half+2*half*col/steps, -half+2*half*(col+1)/steps
            mid = (t0+t1)/2
            start = len(m.vertices)
            chord = 2*(radius-.4)*math.sin((t1-t0)/2)
            m.panel(-chord/2+.022, chord/2-.022, row*1.4+.025, (row+1)*1.4-.025,
                    'stone_dark' if (row+col)%4 == 0 else 'stone', front=.025, back=0)
            rr = (radius-.4)*math.cos((t1-t0)/2)
            for k in range(start, len(m.vertices)):
                x, y, z = m.vertices[k]
                m.vertices[k] = (rr*math.sin(mid)+x*math.cos(mid)-y*math.sin(mid),
                                 radius-rr*math.cos(mid)+x*math.sin(mid)+y*math.cos(mid), z)


def architecture(piece):
    m = MasonMesh(piece)
    if piece == 'wall_round_tangent_r4':
        # Paired transition piers join radial arc end planes at +/-30 degrees
        # to the straight, four-metre tangent portal. No scaled arc or overlap.
        for sign in (-1,1):
            polygon = [(sign*2,.4), (sign*2.2,4.4*math.cos(math.pi/6)-4.4),
                       (sign*1.8,3.6*math.cos(math.pi/6)-4.4), (sign*2,-.4)]
            vertices = [(x,-z,y) for y in (0,HEIGHT) for x,z in polygon]
            faces = [(3,2,1,0),(4,5,6,7)]+[(i,(i+1)%4,(i+1)%4+4,i+4) for i in range(4)]
            if sign<0:
                faces = [tuple(reversed(f)) for f in faces]
            m.add(vertices,faces,'stone_dark')
    elif piece == 'floor_portal_4m':
        m.box((0,0,.08),(4,.8,.16),'stone_dark')
    elif piece in ('floor_disk_r4','ceiling_disk_r4','floor_apse_r4','ceiling_apse_r4'):
        apse = 'apse' in piece
        thickness = .18 if piece.startswith('ceiling') else .16
        angles = [math.pi*i/24 for i in range(25)] if apse else [math.tau*i/48 for i in range(48)]
        polygon = [(4.4*math.cos(a),4.4*math.sin(a)) for a in angles]
        n = len(polygon)
        vertices = [(x,-z,y) for y in (0,thickness) for x,z in polygon]
        m.add(vertices,[tuple(range(n)),tuple(reversed(range(n,2*n)))]+
              [(i,i+n,(i+1)%n+n,(i+1)%n) for i in range(n)],'stone_dark')
    elif piece in ('wall_2m', 'wall_end_1m', 'wall_door_closed_4m', 'wall_concave_1p2m'):
        width = {'wall_2m': 2, 'wall_end_1m': 1, 'wall_door_closed_4m': 4,
                 'wall_concave_1p2m': 1.2}[piece]
        masonry(m, -width/2, width/2, 0, HEIGHT, rows=7, columns=2 if width <= 2 else 3)
    elif piece in ('wall_door_4m', 'arch_pointed_door'):
        pointed_wall(m, 4, 3.2, 0, 2.8, 1.65)
    elif piece in ('wall_window_2m', 'wall_arrow_slit_2m'):
        arrow = piece == 'wall_arrow_slit_2m'
        pointed_wall(m, 2, .28 if arrow else 1.3, 2.0 if arrow else 1.1,
                     3.6 if arrow else 2.5, .9 if arrow else 1.0)
    elif 'wall_arc' in piece:
        radius = int(piece[-1])
        arc(m, radius, {4: 30, 6: 20, 8: 15}[radius])
    elif piece.startswith('corner'):
        m.box((0, -.2, HEIGHT/2), (.8, .4, HEIGHT), 'stone_dark')
        m.box((-.2, .2, HEIGHT/2), (.4, .4, HEIGHT), 'stone')
        if piece == 'corner_out':
            m.vertices = [(-x, -y, z) for x, y, z in m.vertices]
    elif piece == 'pillar':
        m.box((0, 0, .13), (.8, .8, .26), 'stone_dark')
        for x, y in ((0, 0), (-.22, 0), (.22, 0), (0, -.22), (0, .22)):
            m.rod((x, y, .26), (x, y, 6.68), .13, 'stone', 8)
        m.box((0, 0, 6.84), (.8, .8, .32), 'stone_dark')
    elif piece in ('floor_2x2', 'ceiling_2x2', 'floor_broken_2x2'):
        ceiling = piece == 'ceiling_2x2'
        if ceiling:
            m.box((0, 0, .09), (2, 2, .18), 'stone_dark')
        elif piece == 'floor_2x2':
            m.box((0, 0, .055), (2, 2, .11), 'mortar')
        for row in range(2):
            cuts = [-1, -.17 if row else .18, 1]
            for col, (a, b) in enumerate(zip(cuts, cuts[1:])):
                if piece == 'floor_broken_2x2' and (row, col) == (1, 1):
                    continue
                start = len(m.vertices)
                m.panel(a+.015, b-.015, -1+row+.02, row-.02,
                        'stone_dark' if ceiling or row == col else 'stone', front=.16, back=.10)
                for i in range(start, len(m.vertices)):
                    x, y, z = m.vertices[i]
                    m.vertices[i] = (x, -z, .18-y if ceiling else y)
        if piece == 'floor_broken_2x2':
            m.box((-.59, 0, .04), (.82, 2, .08), 'stone_dark')
            m.box((.41, .51, .04), (1.18, .98, .08), 'stone_dark')
    elif piece == 'trim_base_2m':
        m.box((0, 0, .12), (2, .18, .24), 'stone_dark')
    elif piece == 'beam_timber_2m':
        m.box((0, 0, .18), (2, .3, .36), 'wood', .025)
        for x in (-.73, .73):
            m.box((x, 0, .18), (.095, .32, .38), 'metal')
    elif piece == 'vault_rib_bay':
        # Four rising curved ribs, one 4x4 bay, spring to pointed crown at +2m.
        for sx, sy in ((-1, -1), (-1, 1), (1, -1), (1, 1)):
            points = [(sx*2*(1-t), sy*2*(1-t), 2*math.sin(t*math.pi/2)) for t in (0, .25, .5, .75, 1)]
            for a, b in zip(points, points[1:]):
                m.rod(a, b, .105, 'stone', 5)
        m.rod((0, 0, 1.8), (0, 0, 2.12), .19, 'stone_dark', 8)
    elif piece == 'vault_web_bay':
        # Closed curved web; separate from ribs so a review cutaway can reveal
        # the ribs without changing the exported room assembly.
        vertices = []
        for thickness in (0, .09):
            for y in range(5):
                for x in range(5):
                    xx, yy = x-2, y-2
                    z = 2*math.sin((1-max(abs(xx),abs(yy))/2)*math.pi/2)
                    vertices.append((xx, yy, z+thickness))
        faces = []
        for y in range(4):
            for x in range(4):
                a = y*5+x
                faces += [(a,a+5,a+6,a+1), (a+25,a+26,a+31,a+30)]
        edge = list(range(5))+[9,14,19,24,23,22,21,20,15,10,5]
        faces += [(a,b,b+25,a+25) for a,b in zip(edge,edge[1:]+edge[:1])]
        m.add(vertices, faces, 'stone_dark')
    elif piece == 'stair_stone_2m':
        for i in range(5):
            m.box((0, -.8+i*.4, (i+1)*.1), (2, .4, (i+1)*.2), 'stone', .02)
    return m.finish(centered=piece in ('vault_rib_bay', 'beam_timber_2m'))


def prop(piece):
    m = MasonMesh(piece)
    if piece in ('prop_torch_sconce', 'prop_torch_dead'):
        m.box((0, -.17, .25), (.20, .07, .50), 'metal', .025)
        m.rod((0, -.14, .14), (0, .2, .4), .035, 'metal')
        m.rod((0, .15, .15), (0, .2, .86), .045, 'wood', 8)
        for x in (-.11, 0, .11):
            m.rod((0, .2, .62), (x, .2, .93), .022, 'metal')
        m.rod((0, .2, .70), (0, .2, .88), .095, 'soot', 8)
        if piece == 'prop_torch_sconce':
            m.prism([(-.08, .86), (.08, .86), (.03, 1.16), (-.035, 1.04)], .24, .16, 'ember')
    elif piece == 'prop_banner':
        m.rod((-.6, 0, 2.6), (.6, 0, 2.6), .035, 'metal')
        for i in range(5):
            x = -.5+i*.2
            m.prism([(x, .15+(i%2)*.18), (x+.2, .08+((i+1)%2)*.22),
                     (x+.2, 2.5), (x, 2.5)], .03*math.sin(i*2)+.012, .03*math.sin(i*2)-.012, 'soot')
        m.box((0, .07, 1.7), (.10, .02, .7), 'stone_dark')
        m.box((0, .07, 1.9), (.46, .02, .08), 'stone_dark')
    elif piece == 'prop_barrel':
        for i in range(12):
            t = i*math.tau/12
            a, b = t+.025, t+math.tau/12-.025
            verts = [(r*math.cos(u), r*math.sin(u), z)
                     for z, r in ((0, .34), (.3, .43), (.8, .43), (1.1, .34)) for u in (a, b)]
            m.add(verts, [(j*2, j*2+1, j*2+3, j*2+2) for j in range(3)], 'wood')
        for z, r in ((.16, .4), (.52, .45), (.94, .4)):
            m.rod((0, 0, z-.035), (0, 0, z+.035), r, 'metal', 12)
        m.rod((0, 0, 0), (0, 0, 1.10), .34, 'wood', 12)
    elif piece == 'prop_rubble':
        for i in range(7):
            x, y = m.rng.uniform(-.6, .6), m.rng.uniform(-.4, .4)
            h = m.rng.uniform(.15, .35)
            m.box((x, y, h/2), (m.rng.uniform(.2, .5), .28, h), 'stone_dark', .045)
    elif piece == 'door_iron_strapped':
        # Continuous structural panel behind the shallow plank relief. Plank
        # joints are not through-gaps; hardware never bridges disconnected boxes.
        m.box((0, 0, 1.37), (3.1, .14, 2.74), 'wood')
        for i in range(10):
            x = -1.55+i*.31
            m.box((x+.155, 0, 1.37), (.30, .15, 2.74), 'wood')
        for face in (-1, 1):
            for z in (.4, 1.5, 2.4):
                m.box((0, face*.09, z), (3.06, .04, .12), 'metal')
                for x in (-1.36, -.9, -.45, 0, .45, .9, 1.36):
                    m.rod((x, face*.11, z), (x, face*.14, z), .032, 'metal', 6)
            m.box((0, face*.084, .19), (3.06, .018, .26), 'metal')
            m.box((1, face*.084, 1.2), (.16, .018, .32), 'metal')
            m.rod((1, face*.09, 1.29), (1, face*.18, 1.29), .027, 'metal', 6)
            m.ring((1, face*.18, 1.2), .11, 'metal')
    elif piece == 'prop_candelabra':
        m.rod((0, 0, .15), (0, 0, 1.4), .055, 'metal')
        for x in (-.4, 0, .4):
            m.rod((0, 0, 1.05), (x, 0, 1.4), .035, 'metal')
            m.rod((x, 0, 1.4), (x, 0, 1.7), .045, 'soot')
            m.prism([(x-.035, 1.7), (x+.035, 1.7), (x, 1.86)], .025, -.025, 'ember')
        for x, y in ((-.3, -.22), (.3, -.22), (0, .3)):
            m.rod((x, y, .03), (0, 0, .25), .03, 'metal')
    elif piece == 'prop_chain_hanging':
        for i in range(9):
            m.ring((0, 0, .15+i*.21), .13, 'metal')
    elif piece == 'prop_weapon_rack':
        for x in (-.8, .8):
            m.box((x, 0, 1.1), (.12, .25, 2.2), 'wood')
        for z in (.3, 1.5):
            m.box((0, 0, z), (1.8, .16, .16), 'wood')
        for x in (-.55, 0, .55):
            m.rod((x, .2, .2), (x+.1, .2, 2.25), .024, 'wood')
            m.prism([(x+.03, 2.10), (x+.1, 2.58), (x+.17, 2.10)], .23, .17, 'metal')
    elif piece in ('prop_portcullis', 'prop_cell_grille'):
        width = 3.1 if piece == 'prop_portcullis' else 1.8
        for i in range(9):
            x = -width/2+width*i/8
            m.rod((x, 0, .08), (x, 0, 3.4), .035, 'metal', 4)
        for z in (.6, 1.5, 2.4, 3.3):
            m.box((0, 0, z), (width+.09, .09, .07), 'metal')
    elif piece in ('prop_trestle_table', 'prop_bench', 'prop_altar'):
        h = .50 if piece == 'prop_bench' else 1.0
        stone = piece == 'prop_altar'
        surface = 'stone_dark' if stone else 'wood'
        depth = 1.2 if stone else .5 if piece == 'prop_bench' else 1.0
        m.box((0, 0, h-.07), (2.4, depth, .14), surface, .025)
        for x in (-.8, .8):
            m.box((x, 0, (h-.14)/2), (.22, depth*.8, h-.14), surface, .02)
        if stone:
            m.box((0, 0, 1.13), (.65, .6, .26), 'stone', .04)
    elif piece == 'prop_soot_streak':
        for i in range(5):
            x = (i-2)*.11
            h = 1.0+1.0*math.cos(i*1.7)**2
            m.prism([(x-.07, 0), (x+.06, .05), (x+.015, h)], .008, -.008, 'soot')
    elif piece == 'prop_cauldron':
        m.rod((0, 0, .24), (0, 0, .66), .48, 'metal', 12)
        m.rod((0, 0, .65), (0, 0, .68), .42, 'soot', 12)
        for x in (-.34, .34):
            m.rod((x, 0, 0), (x, 0, .28), .065, 'metal')
    return m.finish(centered=True)


def digest(obj):
    rows = []
    for face in obj.data.polygons:
        points = []
        for index in face.vertices:
            v = obj.data.vertices[index].co
            points.append(tuple(0.0 if abs(c) < .000005 else round(c, 5) for c in (v.x, v.z, -v.y)))
        rows.append((obj.data.materials[face.material_index].name, sorted(points)))
    return hashlib.sha256(json.dumps(sorted(rows), separators=(',', ':')).encode()).hexdigest()


def export(obj, piece, kind):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(ART/'Kit'/(obj.name+'.fbx')), use_selection=True,
        object_types={'MESH'}, global_scale=1, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE',
        use_triangles=True, bake_anim=False, use_custom_props=False, path_mode='RELATIVE')
    lo = [min(v.co[i] for v in obj.data.vertices) for i in range(3)]
    hi = [max(v.co[i] for v in obj.data.vertices) for i in range(3)]
    triangles = len(obj.data.polygons)
    assert triangles <= (1500 if kind == 'prop' else 300), (piece, triangles)
    return {'id': piece, 'file': obj.name+'.fbx', 'kind': kind,
            'size': [round(hi[i]-lo[i], 6) for i in (0, 2, 1)], 'triangles': triangles,
            'materials': sorted(mat.name for mat in obj.data.materials), 'geometrySha256': digest(obj)}


SIDES = {'N': (0, 1, 0), 'E': (1, 0, 90), 'S': (0, -1, 180), 'W': (-1, 0, 270)}


def placement(piece, x, z, y=0, angle=0):
    return {'id': piece, 'pos': [round(x, 6), round(y, 6), round(z, 6)], 'rotY': angle}


def edge_position(cell, side):
    dx, dz, angle = SIDES[side]
    return 2*cell[0]+1+dx, 2*cell[1]+1+dz, angle


def room(name, cells, doors, kind='room', shape='rect', gimmick='none', low=False):
    cells = sorted(cells)
    occupied = set(cells)
    count = len(cells)
    size = 'closet' if count <= 4 else 'small' if count <= 9 else 'medium' if count <= 20 else 'large' if count <= 40 else 'hall'
    pieces, sockets = [], []
    consumed = set()
    for cell, side in doors:
        x, z, angle = edge_position(cell, side)
        span = 2 if kind in ('hallway', 'junction') else 1
        tangent = (1, 0) if side in ('N', 'S') else (0, 1)
        if span == 2:
            x, z = x+tangent[0], z+tangent[1]
            neighbour = (cell[0]+tangent[0], cell[1]+tangent[1])
            assert neighbour in occupied
            pieces.append(placement('wall_door_4m', x, z, angle=angle))
            sockets.append({'cell': list(cell), 'side': side, 'span': 2,
                            'closedWith': [placement('wall_door_closed_4m', x, z, angle=angle)]})
            consumed.update(((cell, side), (neighbour, side)))
            continue
        # A 4m portal centered on a 2m edge also consumes half of each neighbour.
        tangent = (1, 0) if side in ('N', 'S') else (0, 1)
        for sign in (-1, 1):
            neighbour = (cell[0]+sign*tangent[0], cell[1]+sign*tangent[1])
            assert neighbour in occupied, (name, 'portal needs adjacent boundary cells')
            nx, nz, na = edge_position(neighbour, side)
            dx, dz, _ = SIDES[side]
            assert (neighbour[0]+dx, neighbour[1]+dz) not in occupied
            pieces.append(placement('wall_end_1m', nx+sign*tangent[0]*.5, nz+sign*tangent[1]*.5, angle=na))
            consumed.add((neighbour, side))
        pieces.append(placement('wall_door_4m', x, z, angle=angle))
        sockets.append({'cell': list(cell), 'side': side,
                        'closedWith': [placement('wall_door_closed_4m', x, z, angle=angle)]})
        consumed.add((cell, side))
    boundary = []
    for cell in cells:
        for side, (dx, dz, angle) in SIDES.items():
            if (cell[0]+dx, cell[1]+dz) in occupied:
                continue
            boundary.append((cell, side))
            if (cell, side) not in consumed:
                x, z, angle = edge_position(cell, side)
                # A 0.8m wall is set OUTSIDE the occupied grid to avoid corner
                # volume overlaps: straight runs meet on shared end planes.
                wall = 'wall_arrow_slit_2m' if side == 'N' and cell[0]%3 == 0 else 'wall_2m'
                pieces.append(placement(wall, x, z, angle=angle))
    for x, z in cells:
        pieces.append(placement('floor_2x2', x*2+1, z*2+1, -.16))
        # Castle ceiling datum is the top of the complete assembly, at wall
        # height. Beams/vault crowns terminate at that same datum; no low lids.
        pieces.append(placement('ceiling_2x2', x*2+1, z*2+1, HEIGHT-.18))
    if low:
        for x, z in cells:
            if z%2 == 0:
                pieces.append(placement('beam_timber_2m', 2*x+1, 2*z+1, HEIGHT-.38))
    else:
        for x, z in cells:
            if x%2 == 0 and z%2 == 0 and all((x+a, z+b) in occupied for a, b in ((1, 0), (0, 1), (1, 1))):
                rib_height = bpy.data.objects['Castle_vault_rib_bay'].dimensions.z
                web_height = bpy.data.objects['Castle_vault_web_bay'].dimensions.z
                pieces.append(placement('vault_rib_bay', x*2+2, z*2+2, HEIGHT-rib_height))
                pieces.append(placement('vault_web_bay', x*2+2, z*2+2, HEIGHT-web_height))
    candidates = [[2*x+1, 0, 2*z+1] for x, z in cells]
    # Cell-centre anchors give >=0.6m clearance even with half-thickness walls.
    cake_count = max(2, (count*2+8)//9)
    cake = [candidates[min(len(candidates)-1, i*len(candidates)//cake_count)] for i in range(cake_count)]
    light = []
    usable = [(c, s) for c, s in boundary if (c, s) not in consumed]
    for index, (cell, side) in enumerate(usable[::max(1, len(usable)//max(2, count//5))]):
        x, z, angle = edge_position(cell, side)
        dx, dz, _ = SIDES[side]
        fixture_id = 'prop_torch_dead' if index == 2 else 'prop_torch_sconce'
        inset = bpy.data.objects['Castle_'+fixture_id].dimensions.y/2+.001
        fixture = placement(fixture_id, x-dx*inset, z-dz*inset, 2.1, angle)
        pieces.append(fixture)
        pieces.append(placement('prop_soot_streak', x-dx*.012, z-dz*.012, 3.14, angle))
        if index != 2:
            light.append([round(x-dx*.65, 6), 3.13, round(z-dz*.65, 6)])
    # At re-entrant corners the horizontal wall owns the corner volume. Trim
    # the vertical run by its thickness, avoiding interpenetrating wall solids.
    concave = []
    for vx in range(max(c[0] for c in cells)+2):
        for vz in range(max(c[1] for c in cells)+2):
            if sum((vx+a, vz+b) in occupied for a, b in ((-1,-1),(-1,0),(0,-1),(0,0))) == 3:
                concave.append((2*vx, 2*vz))
    for p in pieces:
        if p['id'] == 'wall_2m' and p['rotY'] in (90, 270):
            for vx, vz in concave:
                if abs(p['pos'][0]-vx) < .001 and abs(abs(p['pos'][2]-vz)-1) < .001:
                    p['id'] = 'wall_concave_1p2m'
                    p['pos'][2] += .4 if p['pos'][2] > vz else -.4
    # Put the inner wall face on the footprint boundary. Adjacent perpendicular
    # walls meet at an edge without intersecting volumes at convex corners.
    for p in pieces:
        if p['id'].startswith('wall_'):
            a = math.radians(p['rotY'])
            p['pos'][0] = round(p['pos'][0]+.4*math.sin(a), 6)
            p['pos'][2] = round(p['pos'][2]+.4*math.cos(a), 6)
    for socket in sockets:
        for p in socket['closedWith']:
            a = math.radians(p['rotY'])
            p['pos'][0] = round(p['pos'][0]+.4*math.sin(a), 6)
            p['pos'][2] = round(p['pos'][2]+.4*math.cos(a), 6)
    return {'id': 'castle_'+name, 'kind': kind, 'sizeClass': size, 'shape': shape,
            'footprint': [list(c) for c in cells], 'height': HEIGHT, 'doors': sockets,
            'anchors': {'cake': cake, 'goldenCake': [candidates[len(candidates)//2]] if count >= 10 else [],
                        'light': light, 'hunterSpawn': [candidates[-2]] if count >= 10 else []},
            'gimmick': gimmick, 'minRound': 3 if gimmick != 'none' else 1, 'weight': 1.0,
            'pieces': pieces}


def rectangle(w, d):
    return [(x, z) for z in range(d) for x in range(w)]


def round_room(apse=False):
    """Cell-centre raster of an r4 disk, with straight tangent portal bays.

    The raster is occupancy, not a stair-stepped replacement for the circular
    wall. Curved floor/roof slabs cover the exact shell, including raster slivers.
    The chapel joins a full semicircle to an eight-metre rectangular nave.
    """
    name = 'chapel_apse' if apse else 'tower_room'
    cz = 8 if apse else 4
    cells = (rectangle(4,4) if apse else [])+[(x,z) for x in range(4)
             for z in range(4 if apse else 0,6 if apse else 4)
             if (2*x+1-4)**2+(2*z+1-cz)**2<=16]
    # Move nave side-wall centre planes inward to meet the radial ends exactly.
    # Their south ends still meet (not overlap) the outward-offset south wall.
    if apse:
        t = room(name,rectangle(4,4),[((1,0),'S'),((0,1),'W')],low=True)
        t['pieces'] = [p for p in t['pieces'] if not (
            (p['id'].startswith('wall_') and p['rotY']==0) or
            p['id'] in ('prop_torch_sconce','prop_torch_dead','prop_soot_streak'))]

        for p in t['pieces']:
            if p['id'].startswith('wall_') and p['rotY'] in (90,270):
                p['pos'][0] += -.4 if p['rotY']==90 else .4
        for s in t['doors']:
            if s['side']=='W':
                for p in s['closedWith']:
                    p['pos'][0] += .4

        angles = (-75,-45,-15,15,45,75)
        t['pieces'] += [placement('floor_apse_r4',4,8,-.16),placement('ceiling_apse_r4',4,8,HEIGHT-.18)]
        t['anchors'] = {'cake':[[3,0,1],[5,0,1],[4,0,5],[1,0,7],[7,0,7]],
                        'goldenCake':[[4,0,8]],'hunterSpawn':[[4,0,3]],'light':[[1,3.13,5],[7,3.13,5]]}
        t['pieces'] += [placement('prop_altar',4,10),placement('prop_candelabra',3,7),
                        placement('prop_candelabra',5,7)]
        # Longitudinal pews leave the west doorway and a broad central aisle clear.
        for x in (1.2,6.8):
            t['pieces'].append(placement('prop_bench',x,5,angle=90))
    else:
        t = {'id':'castle_'+name,'kind':'room','sizeClass':'medium','shape':'round',
             'height':HEIGHT,'doors':[], 'gimmick':'none','minRound':1,'weight':1.0,'pieces':[],
             'anchors':{'cake':[[3,0,3],[5,0,5],[5,0,3]],'goldenCake':[[5,0,3]],
                        'hunterSpawn':[[3,0,5]],'light':[[3,2.1,4],[5,2.1,4]]}}
        for side,z,cell,yaw in (('S',-.4,[1,0],180),('N',8.4,[1,3],0)):
            t['doors'].append({'cell':cell,'side':side,'span':2,
                              'closedWith':[placement('wall_door_closed_4m',4,z,angle=yaw)]})
            t['pieces'] += [placement('wall_door_4m',4,z,angle=yaw),
                            placement('wall_round_tangent_r4',4,z,angle=yaw)]
        t['pieces'] += [placement('floor_disk_r4',4,4,-.16),placement('ceiling_disk_r4',4,4,HEIGHT-.18)]
        angles = (45,75,105,135,225,255,285,315)
        t['pieces'] += [placement('prop_candelabra',3,4),placement('prop_candelabra',5,4)]
    for angle in angles:
        a = math.radians(angle)
        t['pieces'].append(placement('wall_arc_r4',4+4*math.sin(a),cz+4*math.cos(a),angle=angle))
    t['footprint'] = [list(c) for c in sorted(cells)]
    t['shape'] = 'round'
    t['sizeClass'] = 'large' if len(cells)>20 else 'medium'
    return t


def catalogue():
    # Corridors reserve the entire four-metre end cap, never a long-side entry.
    specs = [
        ('watch_closet', rectangle(3, 1), [((1, 0), 'S')], 'room', 'rect', 'none', True),
        ('guard_room', rectangle(3, 3), [((1, 0), 'S'), ((1, 2), 'N')], 'room', 'rect', 'none', True),
        ('armoury', rectangle(3, 2), [((1, 0), 'S'), ((1, 1), 'N')], 'room', 'rect', 'none', True),
        ('cell_block', rectangle(3, 3), [((1, 0), 'S'), ((1, 2), 'N')], 'room', 'rect', 'none', True),
        ('buttery', [(x, z) for x, z in rectangle(4, 4) if x < 3 or z < 2],
         [((1, 0), 'S'), ((1, 3), 'N')], 'room', 'L', 'none', True),
        ('chapel', rectangle(3, 6), [((1, 0), 'S'), ((0, 2), 'W')], 'room', 'rect', 'none', False),
        ('keep_gallery', rectangle(5, 6), [((2, 0), 'S'), ((2, 5), 'N')], 'room', 'rect', 'none', False),
        ('great_hall', rectangle(6, 8), [((2, 0), 'S'), ((2, 7), 'N'), ((5, 3), 'E')], 'room', 'rect', 'none', False),

        ('gallery_straight', rectangle(6, 2), [((0, 0), 'W'), ((5, 0), 'E')], 'hallway', 'rect', 'none', False),
        ('gallery_bend', [(x, z) for x, z in rectangle(5, 5) if z < 2 or x >= 3],
         [((0, 0), 'W'), ((3, 4), 'N')], 'hallway', 'L', 'none', False),
        ('stair_landing', rectangle(6, 2)+[(x,z) for x in (2,3) for z in range(2,6)],
         [((0, 0), 'W'), ((5, 0), 'E'), ((2, 5), 'N')], 'junction', 'T', 'none', False),
        ('portcullis_freeze', rectangle(3, 4), [((1, 0), 'S'), ((1, 3), 'N')], 'room', 'rect', 'freeze', False),
        ('collapsed_crossing', rectangle(5, 5), [((2, 0), 'S'), ((2, 4), 'N')], 'room', 'irregular', 'traversal', False)]
    result = []
    for name, cells, doors, kind, shape, gimmick, low in specs:
        t = room(name, cells, doors, kind, shape, gimmick, low)
        p = t['pieces']
        if name in ('watch_closet', 'guard_room', 'armoury'):
            p += [placement('prop_weapon_rack', 1, 1, angle=90), placement('prop_barrel', 5, 1)]
        if name in ('guard_room', 'armoury'):
            frame = next(v for v in p if v['id'] == 'wall_door_4m')
            p.append(dict(id='door_iron_strapped', pos=list(frame['pos']), rotY=frame['rotY']))
        if name == 'guard_room':
            p += [placement('prop_trestle_table', 3, 3), placement('prop_bench', 3, 4.2)]
        if name == 'cell_block':
            p += [placement('prop_cell_grille', 1, 3), placement('prop_chain_hanging', 1, 4.7, 2)]
        if name == 'buttery':
            p += [placement('prop_trestle_table', 3, 3), placement('prop_cauldron', 6.8, 1),
                  placement('prop_barrel', 1, 5), placement('prop_barrel', 1, 6.2)]
        if name == 'chapel':
            p += [placement('prop_altar', 3, 11.25), placement('stair_stone_2m', 3, 8),
                  placement('prop_candelabra', 1, 11.5), placement('prop_candelabra', 5, 11.5)]
            # Keep both the centre aisle and west approach open to the 1m agent.
            for z in (2.5, 7.5):
                p += [placement('prop_bench', 1, z, angle=90), placement('prop_bench', 5, z, angle=90)]
        if name in ('great_hall', 'keep_gallery'):
            depth = 16 if name == 'great_hall' else 12
            width = 12 if name == 'great_hall' else 10
            for z in range(3, depth-1, 4):
                for x in (2, width-2):
                    p += [placement('pillar', x, z), placement('prop_banner', x, z+.4, 3.4)]
            if name == 'great_hall':
                for z in (4, 7, 10):
                    p += [placement('prop_trestle_table', 6, z), placement('prop_bench', 4, z, angle=90)]
                p += [placement('prop_altar', 8, 14)]
        if name == 'stair_landing':
            p += [placement('stair_stone_2m', 4.5, 3), placement('pillar', 4.5, 5),
                  placement('prop_chain_hanging', 4.5, 5, 4.6)]
        if name in ('gallery_straight', 'gallery_bend'):
            p += [placement('arch_pointed_door', 6, 2, angle=90)]
        if name == 'portcullis_freeze':
            # Raised against the north header, not floating below it or sealing
            # the required walking route. Collision remains on the lifted gate.
            gate_z = 8-bpy.data.objects['Castle_prop_portcullis'].dimensions.y/2-.001
            p += [placement('prop_portcullis', 3, gate_z, 3.4),
                  placement('prop_chain_hanging', 1, 7.2, 3.2), placement('prop_rubble', 5, 4)]
        if name == 'collapsed_crossing':
            # Real void surrounded by a walkable U-shaped perimeter; no floor
            # tiles hidden solely in the preview. Anchors avoid the missing cells.
            holes = {(2, 1), (2, 2), (2, 3)}
            p[:] = [v for v in p if not (v['id'] == 'floor_2x2' and
                    (int(v['pos'][0]//2), int(v['pos'][2]//2)) in holes)]
            for v in p:
                if v['id'] == 'floor_2x2' and v['pos'][0] == 3 and v['pos'][2] in (3,5,7):
                    v['id'] = 'floor_broken_2x2'
            for z in (3, 5, 7):
                p += [placement('prop_rubble', 7.2, z)]
            t['anchors']['goldenCake'] = [[9, 0, 5]]
            t['anchors']['cake'] = [[1, 0, 1], [9, 0, 1], [1, 0, 9], [9, 0, 9], [9, 0, 5], [1, 0, 5]]
        result.append(t)
    result += [round_room(),round_room(apse=True)]
    # Authored corrections verified by ProceduralTemplateAnchorTests against the
    # runtime collision commands (Humanoid radius .5m, height 2m), not mesh centres.
    corrections = {
        'watch_closet': {'cake': [[2.3,0,1],[3.7,0,1]]},
        'guard_room': {'cake': [[1,0,3],[5,0,3]]},
        'armoury': {'cake': [[1,0,3],[3,0,3]]},
        'buttery': {'cake': [[1,0,1],[3,0,5],[3,0,7],[5,0,5]], 'hunterSpawn': [[7,0,3]]},
        'chapel': {'cake': [[3,0,1],[3,0,4],[3,0,6],[3,0,8]],
                   'goldenCake': [[3,0,8]], 'hunterSpawn': [[3,0,5]]},
        'great_hall': {'cake': [[1,0,1],[1,0,9],[3,0,1],[3,0,11],[5,0,3],
                                [5,0,11],[7,0,5],[7,0,11],[9,0,5],[11,0,15],[11,0,7]]},
    }
    for t in result:
        t['anchors'].update(corrections.get(t['id'].removeprefix('castle_'), {}))
        for socket in t['doors']:
            p = socket['closedWith'][0]
            t['pieces'].append(dict(id='floor_portal_4m',pos=[p['pos'][0],-.16,p['pos'][2]],rotY=p['rotY']))
        seat_wall_props(t['pieces'], 'Castle', dict(COMMON,**EXTRA),
                        {'prop_banner','prop_chain_hanging','prop_torch_sconce','prop_torch_dead'})
    return {'theme': 'castle', 'module': 2.0, 'templates': result}


def seat_wall_props(pieces, theme, kinds, ids, minimum_width=0):
    """Seat explicitly wall-mounted assets on the nearest full solid wall.

    Use measured back planes after bottom-centering, and avoid windows/portals.
    This is authoring, not validator repair; validation reimports the FBXs.
    """
    used = []
    for p in pieces:
        if p['id'] not in ids:
            continue
        obj = bpy.data.objects[theme+'_'+p['id']]
        back = max(-v.co.y for v in obj.data.vertices)
        width = obj.dimensions.x
        candidates = []
        for wall in pieces:
            if kinds[wall['id']]!='wall' or 'tangent' in wall['id']:
                continue
            master = bpy.data.objects[theme+'_'+wall['id']]
            if master.dimensions.x < max(width,minimum_width)-.001:
                continue
            front = min(-v.co.y for v in master.data.vertices)
            a = math.radians(wall['rotY'])
            inset = front-back-.008
            pos = [round(wall['pos'][0]+inset*math.sin(a),6),p['pos'][1],
                   round(wall['pos'][2]+inset*math.cos(a),6)]
            def world_bounds(source, position, yaw):
                c,s=math.cos(yaw),math.sin(yaw)
                vertices=[(position[0]+v.co.x*c-v.co.y*s,position[1]+v.co.z,
                           position[2]-v.co.x*s-v.co.y*c) for v in source.data.vertices]
                return ([min(v[i] for v in vertices) for i in range(3)],
                        [max(v[i] for v in vertices) for i in range(3)])
            low,high=world_bounds(obj,pos,a)
            blocked=False
            for other in pieces:
                if kinds[other['id']] not in ('wall','window','door') or other is wall:
                    continue
                lo,hi=world_bounds(bpy.data.objects[theme+'_'+other['id']],other['pos'],math.radians(other['rotY']))
                if all(min(high[i],hi[i])-max(low[i],lo[i])>1e-5 for i in range(3)):
                    blocked=True
                    break
            if blocked:
                continue
            distance = math.hypot(pos[0]-p['pos'][0],pos[2]-p['pos'][2])
            crowded = any(math.dist(pos,q)<max(.5,width) for q in used)
            candidates.append((crowded,distance,pos,wall['rotY']))
        assert candidates, (theme,p['id'],'no full solid support wall')
        _,_,p['pos'],p['rotY'] = min(candidates)
        used.append(p['pos'])


def instantiate(objects, piece, collection, name, offset=(0, 0, 0)):
    obj = objects[piece['id']].copy()
    obj.data = objects[piece['id']].data
    collection.objects.link(obj)
    obj.name = name
    x, y, z = piece['pos']
    obj.location = (x+offset[0], -z+offset[1], y+offset[2])
    obj.rotation_euler.z = math.radians(piece['rotY'])
    obj.hide_render = False
    obj.hide_set(False)
    return obj


def light(collection, name, location, power, color, kind='POINT', target=None, size=.2):
    data = bpy.data.lights.new(name, kind)
    data.energy, data.color = power, color
    if kind == 'AREA':
        data.shape, data.size = 'DISK', size
    else:
        data.shadow_soft_size = size
    if kind == 'SPOT':
        data.spot_size, data.spot_blend = math.radians(52), .45
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.location = location
    if target is not None:
        obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z', 'Y').to_euler()
    return obj


def setup_render():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.cycles.seed = SEED
    scene.render.resolution_x, scene.render.resolution_y = 1200, 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.world = bpy.data.worlds.new('Castle review world')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.20, .26, .34, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .20
    scene.view_settings.view_transform = 'AgX'
    camera_data = bpy.data.cameras.new('CastleReviewCamera')
    camera = bpy.data.objects.new('CastleReviewCamera', camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    return camera


def render(camera, path, location, target, scale, perspective=False):
    camera.location = location
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'PERSP' if perspective else 'ORTHO'
    camera.data.lens = 22
    camera.data.ortho_scale = scale
    bpy.context.scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def previews_and_sources(objects, rooms, skip):
    scene = bpy.context.scene
    for obj in objects.values():
        obj.hide_render = True
        obj.hide_set(True)
    camera = setup_render()
    sheet = bpy.data.collections.new('Kit sheet (review copies, not exports)')
    scene.collection.children.link(sheet)
    for i, (piece, original) in enumerate(objects.items()):
        x, z = (i%8)*5, (i//8)*10
        instantiate(objects, placement(piece, x, z, angle=0), sheet, 'Sheet_'+piece)
        text = bpy.data.curves.new('Label_'+piece, 'FONT')
        text.body, text.size = piece, .25
        label = bpy.data.objects.new(text.name, text)
        sheet.objects.link(label)
        label.location = (x-1.8, -z+1.2, .02)
    studio = bpy.data.collections.new('Review studio')
    scene.collection.children.link(studio)
    light(studio, 'SheetKey', (12, 12, 24), 18000, (.8, .88, 1), 'AREA', (14, -20, 0), 18)
    if not skip:
        scene.render.resolution_x, scene.render.resolution_y = 2000, 1500
        render(camera, OUTPUT/'kit-sheet.png', (40, 30, 55), (17, -20, 2), 63)
        scene.render.resolution_x, scene.render.resolution_y = 1200, 900
    for collection in (sheet, studio):
        collection.hide_render = False
        collection.hide_viewport = False
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Kit/CastleKit.blend'))
    # Each room has its own scene, at its exact manifest origin. No review-only
    # geometry is added. Visibility cutaways live only on the review instances.
    scene_records = []
    for t in rooms['templates']:
        room_scene = bpy.data.scenes.new(t['id'])
        window_scene = bpy.context.window.scene
        bpy.context.window.scene = room_scene
        room_scene.unit_settings.system = 'METRIC'
        room_scene.unit_settings.scale_length = 1
        camera = setup_render()
        coll = bpy.data.collections.new(t['id']+'_placements')
        room_scene.collection.children.link(coll)
        w = 2*(max(c[0] for c in t['footprint'])+1)
        d = 2*(max(c[1] for c in t['footprint'])+1)
        hidden = []
        for i, p in enumerate(t['pieces']):
            obj = instantiate(objects, p, coll, f"{t['id']}__{i:04d}__{p['id']}")
            obj['placementIndex'] = i
            # Cut away south/east boundary and most ceiling tiles, retaining
            # ribs and a narrow northern roof strip to explain construction.
            cut = (p['id'] == 'ceiling_2x2' and p['pos'][2] < d-1.1) or (
                p['id'] == 'vault_web_bay' and p['pos'][2] < d-2.1) or (
                p['id'].startswith('wall_') and (p['pos'][2] < 0 or p['pos'][0] > w))
            if t['shape']=='round':
                cut = cut or p['id'].startswith('ceiling_') or (
                    'wall_arc' in p['id'] and 90<p['rotY']<270)
            if cut:
                obj.hide_render = True
                hidden.append(obj)
        lamps = bpy.data.collections.new(t['id']+'_review_lighting')
        room_scene.collection.children.link(lamps)
        for i, (x, y, z) in enumerate(t['anchors']['light']):
            light(lamps, 'Torch amber '+str(i), (x, -z, y), 120, linear('#ffb347'), size=.10)
        studio = light(lamps, 'Review fill (disabled for darkness)', (w*.5, 3, 12),
                       2200, (.67, .78, 1), 'AREA', (w/2, -d/2, 1.5), max(w, d))
        if not skip:
            render(camera, OUTPUT/(t['id']+'-three-quarter.png'),
                   (w+9, 10, max(w, d)*.8+7), (w/2, -d/2, 2), max(w, d)*1.4+5)
        if t['id'] == 'castle_chapel':
            # Darkness is a fully enclosed, full-ceiling interior, not a lit dollhouse.
            for obj in hidden:
                obj.hide_render = False
            studio.hide_render = True
            room_scene.world.node_tree.nodes['Background'].inputs[1].default_value = 0
            flashlight = light(lamps, 'Flashlight', (3, -1.1, 1.65), 650, (.78, .87, 1), 'SPOT', (3.3, -9, 2.1), .035)
            if not skip:
                render(camera, OUTPUT/'in-darkness.png', (3, -.5, 1.65), (3, -9.5, 2.5), 10, True)
            else:
                camera.location = (3, -.5, 1.65)
                camera.rotation_euler = (Vector((3,-9.5,2.5))-camera.location).to_track_quat('-Z','Y').to_euler()
                camera.data.type = 'PERSP'
                camera.data.lens = 22
            # Retain the actual darkness setup for independent inspection.
            dark = bpy.data.scenes.new('Review_Darkness_CastleChapel')
            dark.world = room_scene.world.copy()
            for original in room_scene.objects:
                obj = original.copy()
                if original.type in ('CAMERA','LIGHT'):
                    obj.data = original.data.copy()
                dark.collection.objects.link(obj)
                if original == camera:
                    dark.camera = obj
            dark['evidenceImage'] = 'in-darkness.png'
            dark.render.engine = 'CYCLES'
            dark.render.resolution_x, dark.render.resolution_y = 1200, 900
            dark.render.resolution_percentage = 100
            flashlight.hide_render = True
            studio.hide_render = False
            room_scene.world.node_tree.nodes['Background'].inputs[1].default_value = .20
            for obj in hidden:
                obj.hide_render = True
            camera.location = (w+9,10,max(w,d)*.8+7)
            camera.rotation_euler = (Vector((w/2,-d/2,2))-camera.location).to_track_quat('-Z','Y').to_euler()
            camera.data.type = 'ORTHO'
            camera.data.ortho_scale = max(w,d)*1.4+5
        room_scene['manifestTemplate'] = t['id']
        room_scene['cutawayNote'] = 'All placements retained. Hidden south/east walls and ceiling are review-only.'
        scene_records.append(t['id'])
        bpy.context.window.scene = window_scene
    bpy.context.window.scene = bpy.data.scenes['castle_great_hall']
    # Packed source images have portable paths relative to this second source.
    for image in bpy.data.images:
        if image.packed_file:
            image.filepath = bpy.path.relpath(str(SOURCE/'Kit'/(image.name+'.png')), start=str(SOURCE/'Rooms'))
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Rooms/CastleRooms.blend'))
    return scene_records


def render_door_reviews(objects, kinds, theme, output):
    """Theme-independent studio; hospital swing leaves use a CLOSED review pose.

    Only copied review geometry is unposed. Export masters and room placements
    remain untouched. A hash receipt binds every image to the delivered FBX.
    """
    original_scene = bpy.context.window.scene
    folder = output/'doors'
    folder.mkdir(parents=True, exist_ok=True)
    for piece, source in objects.items():
        if kinds[piece] != 'door' and piece not in ('door_iron_strapped', 'door_double_porthole_4m', 'prop_classroom_door_leaf', 'prop_bulkhead_leaf'):
            continue
        # Match FBX's explicit BMesh triangulation, not the source viewport's
        # loop-triangle tessellation (different diagonals on some quads).
        import bmesh
        review_mesh = source.data.copy()
        bm = bmesh.new()
        bm.from_mesh(review_mesh)
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bm.to_mesh(review_mesh)
        bm.free()
        review_mesh.calc_loop_triangles()
        geometry = []
        for triangle in review_mesh.loop_triangles:
            coords = [(review_mesh.vertices[i].co.x, review_mesh.vertices[i].co.z,
                       -review_mesh.vertices[i].co.y) for i in triangle.vertices]
            geometry.append((review_mesh.materials[triangle.material_index].name,
                             sorted(tuple(round(c,4)+0.0 for c in p) for p in coords)))
        geometry_hash = hashlib.sha256(json.dumps(sorted(geometry),separators=(',',':')).encode()).hexdigest()
        scene = bpy.data.scenes.new(theme+' door inspection '+piece)
        bpy.context.window.scene = scene
        scene.render.engine = 'CYCLES'
        scene.cycles.samples, scene.cycles.seed = 24, SEED
        scene.cycles.use_denoising = True
        scene.render.resolution_x, scene.render.resolution_y = 1200, 1200
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = 'PNG'
        scene.view_settings.view_transform = 'AgX'
        scene.world = bpy.data.worlds.new(theme+' door studio')
        scene.world.use_nodes = True
        scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.18,.18,.18,1)
        scene.world.node_tree.nodes['Background'].inputs[1].default_value = .5
        obj = bpy.data.objects.new(piece+' inspection', review_mesh)
        scene.collection.objects.link(obj)
        if piece == 'door_double_porthole_4m':
            for vertex in obj.data.vertices:
                x,y,z = vertex.co
                sign = 1 if x > 0 else -1
                angle = math.radians(sign*95)
                vertex.co = (math.cos(angle)*(x-sign*1.6)-math.sin(angle)*y+sign*1.6,
                             math.sin(angle)*(x-sign*1.6)+math.cos(angle)*y, z)
        lo = [min(v.co[i] for v in obj.data.vertices) for i in range(3)]
        hi = [max(v.co[i] for v in obj.data.vertices) for i in range(3)]
        target = Vector(((lo[0]+hi[0])/2, (lo[1]+hi[1])/2, (lo[2]+hi[2])/2))
        extent = max(hi[0]-lo[0],hi[2]-lo[2])
        cam = bpy.data.objects.new('DoorCamera', bpy.data.cameras.new('DoorCamera'))
        scene.collection.objects.link(cam)
        scene.camera = cam
        cam.data.type, cam.data.ortho_scale = 'ORTHO', extent*1.2
        for side, sign in (('front',1),('back',-1)):
            lamps = []
            for x,power in ((-extent*.6,700),(extent*.6,400)):
                lamp = light(scene.collection, 'Door softbox',
                             (target.x+x,target.y+sign*extent,target.z+extent*.5),
                             power, (1,1,1), 'AREA', target, extent)
                lamps.append(lamp)
            cam.location = target+Vector((0,sign*extent*3,0))
            cam.rotation_euler = (target-cam.location).to_track_quat('-Z','Y').to_euler()
            path = folder/(piece+'-'+side+'.png')
            scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            exported = ROOT/'Assets/Art/Environment'/theme/'Kit'/(theme+'_'+piece+'.fbx')
            receipt = {'piece':piece, 'face':side, 'pose':'closed inspection' if piece == 'door_double_porthole_4m' else 'exported',
                       'imageSha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                       'geometrySha256':geometry_hash,
                       'fbxSha256':hashlib.sha256(exported.read_bytes()).hexdigest()}
            path.with_suffix('.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
            for lamp in lamps:
                bpy.data.objects.remove(lamp, do_unlink=True)
        bpy.context.window.scene = original_scene
        mesh = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.meshes.remove(mesh)
        bpy.data.objects.remove(cam, do_unlink=True)
        bpy.data.scenes.remove(scene)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--skip-previews', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if bpy.app.version[:2] != (5, 2):
        raise RuntimeError('Use Blender 5.2')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1
    for path in (ART/'Kit', ART/'Rooms', SOURCE/'Kit', SOURCE/'Rooms', OUTPUT):
        path.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Kit/CastleKit.blend'))
    materials()
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from env_theme_vaults import PIECES, build_vault, add_vaults
    from env_kit_furnishings import ids, build_furnishing
    from env_theme_furnished import publish_expansion
    EXTRA.update({pid: 'prop' for pid in ids('castle')})
    EXTRA[PIECES['castle']] = 'prop'
    objects, rows = {}, []
    for piece, kind in dict(COMMON, **EXTRA).items():
        if piece in ids('castle'):
            mesh = MasonMesh(piece)
            build_furnishing('castle', mesh, piece)
            obj = mesh.finish(centered=True)
        elif piece == PIECES['castle']:
            mesh = MasonMesh(piece)
            build_vault('castle', mesh)
            obj = mesh.finish(centered=True)
        else:
            obj = prop(piece) if kind == 'prop' else architecture(piece)
        objects[piece] = obj
        rows.append(export(obj, piece, kind))
    kit = {'theme': 'castle', 'wallHeight': HEIGHT, 'paletteSrgb': PALETTE,
           'lightColorSrgb': '#ffb347', 'pieces': rows}
    rooms = catalogue()
    add_vaults(rooms['templates'], rows, 'castle')
    for path, value in ((ART/'Kit/CastleKit.manifest.json', kit), (ART/'Rooms/CastleRooms.manifest.json', rooms)):
        path.write_text(json.dumps(value, indent=2)+'\n', encoding='utf-8', newline='\n')
        print('MANIFEST SHA256', path.name, hashlib.sha256(path.read_bytes()).hexdigest())
    if not args.skip_previews:
        render_door_reviews(objects, dict(COMMON, **EXTRA), 'Castle', OUTPUT)
    previews_and_sources(objects, rooms, args.skip_previews)
    publish_expansion('castle', globals(), objects, rows, args.skip_previews)
    print(f'GENERATED Castle: {len(rows)} pieces; {len(rooms["templates"])} rooms')


if __name__ == '__main__':
    main()
