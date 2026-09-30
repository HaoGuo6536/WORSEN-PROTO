# ============================================================================
# env_theme_school.py
# PURPOSE: Author the post-war School as a distinct cinder-block environment.
#   This offline builder replaces the first kit's generic shell and publishes a
#   deterministic room catalogue. The catalogue, not the review scenes, owns all
#   runtime placements; the coordinator assembles it in Unity.
# ARCHITECTURAL ROLE: Offline art generator; outside Unity runtime layers.
# KEY RESPONSIBILITIES:
#   - Build original metre-scale School architecture and furniture.
#   - Export applied Y-up/-Z-forward meshes and the mandatory kit catalogue.
#   - Author enclosed rooms with gameplay sockets and socket-attached door leaves.
#   - Save editable sources and render kit, cutaway and darkness reviews.
# DEPENDENCIES: Blender 5.2 bpy/mathutils, Python standard library only.
# USAGE NOTES: Headless --python-exit-code 1; -- --skip-previews for repeat runs.
#   Writes only School art and Logs in this worktree. No Unity or v1 imports.
#   Coordinates are Unity XYZ; Blender is (X,-Z,Y). All randomness is local and
#   seeded. Door sockets reserve 4m frames; 1m end modules close odd-cell offsets.
# ============================================================================
import argparse
import hashlib
import json
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/Art/Environment/School'
SOURCE = ROOT / 'ArtSource/Environment/School'
REVIEW = ROOT / 'Logs/AgentValidation/Art/EnvSchool'
HEIGHT = 3.8
DADO = 1.2
THICKNESS = .25
SEED = 260930
# Owner colours are sRGB hex; conversion prevents accidental washed-out exports.
PALETTE = {
    'mustard': '#c9a23a', 'teal': '#4f7f7a', 'chalk_green': '#3f5a45',
    'lino': '#6d5540', 'cream': '#e8dfc4', 'steel': '#7c8084', 'fire_red': '#8e2f2a',
    'mortar_upper': '#beb79f', 'mortar_lower': '#355b57', 'rubber': '#292d29',
    'wood': '#796044', 'scuff': '#887057', 'stain': '#969075',
    'glass': '#718c87', 'paper': '#d2c9ab', 'tube': '#eff5d4', 'dead_tube': '#777d6b',
}
KINDS = {
    'wall_2m': 'wall', 'wall_door_4m': 'door', 'wall_window_2m': 'window',
    'wall_arc_r4': 'arc', 'wall_arc_r6': 'arc', 'wall_arc_r8': 'arc',
    'corner_in': 'corner', 'corner_out': 'corner', 'pillar': 'pillar',
    'floor_2x2': 'floor', 'ceiling_2x2': 'ceiling', 'trim_base_2m': 'trim',
    'prop_desk': 'prop', 'prop_locker_bank': 'prop', 'prop_chalkboard': 'prop',
    'prop_chair': 'prop', 'wall_cinderblock_dado_2m': 'wall',
    'wall_cinderblock_end_1m': 'wall', 'door_classroom_transom': 'door',
    'window_multipane_2m': 'window', 'window_boarded_2m': 'window',
    'locker_bank_2m': 'prop', 'chalk_rail_board_2m': 'prop', 'bulletin_board': 'prop',
    'drinking_fountain': 'prop', 'stair_banister_2m': 'prop',
    'ceiling_tile_2x2': 'ceiling', 'ceiling_tile_damaged_2x2': 'ceiling',
    'prop_teacher_desk': 'prop', 'prop_globe': 'prop', 'prop_fluorescent': 'prop',
    'prop_fluorescent_dead': 'prop', 'prop_fire_bell': 'prop',
    'prop_classroom_door_leaf': 'prop', 'prop_bookcase': 'prop',
    'prop_lab_bench': 'prop', 'prop_cafeteria_table': 'prop', 'prop_bleacher': 'prop',
    'prop_basketball_hoop': 'prop', 'prop_washbasin': 'prop', 'prop_toilet': 'prop',
    'prop_stall_partition': 'prop', 'prop_mop_bucket': 'prop', 'stair_flight_2m': 'prop',
}


def xyz(p):
    return Vector((p[0], -p[2], p[1]))


def linear(hex_color):
    rgb = [int(hex_color[i:i+2], 16) / 255 for i in (1, 3, 5)]
    return tuple(c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4 for c in rgb)


def materials():
    for name, hex_color in PALETTE.items():
        mat = bpy.data.materials.new('school_' + name)
        color = (*linear(hex_color), 1)
        mat.diffuse_color = color
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value = color
        shader.inputs['Roughness'].default_value = .48 if name == 'steel' else .84
        shader.inputs['Metallic'].default_value = .35 if name == 'steel' else 0
        if name == 'tube':
            shader.inputs['Emission Color'].default_value = color
            shader.inputs['Emission Strength'].default_value = 3


class SchoolMesh:
    """Batched geometry avoids operator context dependencies and unapplied scale."""
    def __init__(self, name):
        self.name = name
        self.vertices, self.faces, self.surfaces = [], [], []
        self.rng = random.Random(f'{SEED}/{name}')

    def mesh(self, vertices, faces, surface):
        offset = len(self.vertices)
        self.vertices.extend(vertices)
        self.faces.extend(tuple(offset + i for i in f) for f in faces)
        self.surfaces.extend([surface] * len(faces))

    def box(self, center, size, surface):
        corners = ((-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
                   (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1))
        self.mesh([tuple(center[i] + c[i]*size[i]/2 for i in range(3)) for c in corners],
                  ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)), surface)

    def face(self, left, right, bottom, top, depth, surface):
        if right > left and top > bottom:
            self.mesh([(left,bottom,depth),(left,top,depth),(right,top,depth),(right,bottom,depth)],
                      [(0,1,2,3)], surface)

    def rod(self, a, b, radius, surface, sides=8):
        a, b = Vector(a), Vector(b)
        tangent = (b-a).normalized()
        u = tangent.cross(Vector((0,1,0)) if abs(tangent.y) < .9 else Vector((1,0,0))).normalized()
        v = tangent.cross(u)
        vertices = [tuple(end + radius*(u*math.cos(j*math.tau/sides) + v*math.sin(j*math.tau/sides)))
                    for end in (a,b) for j in range(sides)]
        faces = [(j,(j+1)%sides,(j+1)%sides+sides,j+sides) for j in range(sides)]
        faces += [tuple(reversed(range(sides))), tuple(range(sides,2*sides))]
        self.mesh(vertices, faces, surface)

    def finish(self, kind):
        vertices = [xyz(p) for p in self.vertices]
        if kind in {'prop','floor','ceiling','pillar','trim'}:
            lo = Vector(tuple(min(v[i] for v in vertices) for i in range(3)))
            hi = Vector(tuple(max(v[i] for v in vertices) for i in range(3)))
            shift = Vector(((lo.x+hi.x)/2, (lo.y+hi.y)/2, lo.z))
            vertices = [v-shift for v in vertices]
        mesh = bpy.data.meshes.new('School_' + self.name)
        mesh.from_pydata(vertices, [], self.faces)
        mesh.update()
        names = sorted(set(self.surfaces))
        for name in names:
            mesh.materials.append(bpy.data.materials['school_' + name])
        for polygon, surface in zip(mesh.polygons, self.surfaces):
            polygon.material_index = names.index(surface)
        obj = bpy.data.objects.new(mesh.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj['piece_id'] = self.name
        mesh.calc_loop_triangles()
        budget = 1500 if kind == 'prop' else 300
        if len(mesh.loop_triangles) > budget:
            raise ValueError(f'{self.name}: {len(mesh.loop_triangles)} > {budget}')
        return obj


def cinder_wall(p, width=2, opening=None):
    # Identical mating profiles even for windows/doors. Mortar is actual exported
    # shallow geometry (not a Blender-only procedural shader), at .4m courses.
    levels = (0, DADO, 2.8, HEIGHT)
    hole = (-1.6,1.6,0,2.8) if opening == 'door' else (-.78,.78,1.2,2.8) if opening == 'window' else None
    regions = []
    for low, high in zip(levels,levels[1:]):
        spans = [(-width/2,width/2)]
        if hole and low < hole[3] and high > hole[2]:
            spans = [(-width/2,hole[0]),(hole[1],width/2)]
        for left,right in spans:
            p.box(((left+right)/2,(low+high)/2,.125),(right-left,high-low,.25),
                  'teal' if high <= DADO else 'cream')
            regions.append((left,right,low,high))

    def clipped(left,right,low,high,surface):
        for a,b,c,d in regions:
            p.face(max(left,a+.001),min(right,b-.001),max(low,c+.001),min(high,d-.001),-.003,surface)

    for row in range(10):
        y = row*.4
        surface = 'mortar_lower' if y < DADO-.01 else 'mortar_upper'
        if row:
            clipped(-width/2+.012,width/2-.012,y-.006,y+.006,surface)
        for k in range(-5,6):
            x = k*.8 + (.4 if row%2 else 0)
            if -width/2+.02 < x < width/2-.02:
                clipped(x-.006,x+.006,y+.01,min(y+.39,HEIGHT-.02),surface)
    # Dado edging and rubber skirting stay clear of door openings.
    spans = [(-width/2+.012,width/2-.012)] if opening != 'door' else [(-1.988,-1.61),(1.61,1.988)]
    for a,b in spans:
        p.box(((a+b)/2,.055,-.014),(b-a,.11,.028),'rubber')
        p.box(((a+b)/2,DADO,-.011),(b-a,.025,.022),'mustard')
    # Sparse paint damage, never readable marks or gore.
    for left,right,low,high in regions:
        if right-left > .4 and high-low > .4:
            x = left+.09
            y = high-.15
            p.mesh([(x,y,-.004),(x+.16,y+.02,-.004),(x+.12,y-.10,-.004),(x+.06,y-.21,-.004)],
                   [(0,1,2,3)],'stain' if low >= DADO else 'mortar_lower')


def window(p, boarded=False):
    # Steel six-light sash, with one opaque painted pane. Geometry is shared by
    # the wall-window contract piece, never another theme's shell.
    for x in (-.76,0,.76):
        p.box((x,2,.055),(.035,1.6,.10),'steel')
    for y in (1.22,1.75,2.28,2.78):
        p.box((0,y,.055),(1.56,.035,.10),'steel')
    for i,x in enumerate((-.38,.38)):
        for j,y in enumerate((1.485,2.015,2.535)):
            p.face(x-.35,x+.35,y-.245,y+.245,.08,'cream' if (i,j)==(1,2) else 'glass')
    p.box((0,1.19,-.07),(1.62,.06,.22),'steel')
    if boarded:
        for y in (1.55,2.12):
            p.box((0,y,-.09),(1.59,.15,.035),'wood')


def transom(p):
    # The 3.2 x 2.8 aperture is strictly empty; glazing is ABOVE it.
    for x in (-1.66,1.66):
        p.box((x,1.41,-.03),(.10,2.82,.06),'mustard')
    for y in (2.85,3.42):
        p.box((0,y,-.03),(3.4,.07,.06),'mustard')
    p.face(-1.6,1.6,2.9,3.38,-.012,'glass')
    for x in (-.8,0,.8):
        p.face(x-.0125,x+.0125,2.905,3.375,-.04,'steel')
    for i in range(15):
        x = -1.5+i*.2
        p.face(x,x+.006,2.91,3.37,-.031,'mortar_lower')
    for y in (3.0,3.15,3.3):
        p.face(-1.59,1.59,y,y+.006,-.032,'mortar_lower')


def curved_wall(p, radius):
    degrees = {4:30,6:20,8:15}[radius]
    steps = 6
    levels = (0,DADO,2.8,HEIGHT)
    vertices,faces,surfaces = [],[],[]
    for i in range(steps+1):
        a = math.radians(degrees)*(i/steps-.5)
        for r in (radius,radius+.25):
            for y in levels:
                vertices.append((r*math.sin(a),y,r*math.cos(a)-radius))
    for i in range(steps):
        a,b = i*8,(i+1)*8
        for j in range(3):
            faces += [(a+j,a+j+1,b+j+1,b+j),(a+4+j,b+4+j,b+5+j,a+5+j)]
            surfaces += ['teal' if j==0 else 'cream']*2
        faces += [(a,b,b+4,a+4),(a+3,a+7,b+7,b+3)]
        surfaces += ['teal','cream']
    for j in range(3):
        faces += [(j,j+4,j+5,j+1),(steps*8+j,steps*8+j+1,steps*8+j+5,steps*8+j+4)]
        surfaces += ['teal' if j==0 else 'cream']*2
    p.mesh(vertices,faces,'cream')
    p.surfaces[-len(faces):] = surfaces
    # Courses follow the same faceted inner radius, with .004m inward relief.
    for y in (.4,.8,1.2,1.6,2,2.4,2.8,3.2,3.6):
        for i in range(steps):
            aa,bb = [math.radians(degrees)*(k/steps-.5) for k in (i,i+1)]
            r = radius-.004
            p.mesh([(r*math.sin(a),h,r*math.cos(a)-radius) for a,h in ((aa,y-.006),(aa,y+.006),(bb,y+.006),(bb,y-.006))],
                   [(0,1,2,3)],'mortar_lower' if y<1.2 else 'mortar_upper')


def architecture(p):
    n = p.name
    if n in {'wall_2m','wall_cinderblock_dado_2m','wall_cinderblock_end_1m'}:
        cinder_wall(p,1 if n.endswith('1m') else 2)
    elif n in {'wall_door_4m','door_classroom_transom'}:
        cinder_wall(p,4,'door')
        transom(p)
    elif n in {'wall_window_2m','window_multipane_2m','window_boarded_2m'}:
        cinder_wall(p,2,'window')
        window(p,n=='window_boarded_2m')
    elif n.startswith('wall_arc'):
        curved_wall(p,int(n[-1]))
    elif n.startswith('corner'):
        # Orthogonal return: interior corner or reverse exterior return.
        for y,h,s in ((.6,1.2,'teal'),(2.5,2.6,'cream')):
            p.box((0,y,.125),(.5,h,.25),s)
            p.box((.125 if n=='corner_in' else -.125,y,-.125),(.25,h,.25),s)
        for y in (.4,.8,1.6,2,2.4,2.8,3.2,3.6):
            p.face(-.245,.245,y,y+.012,-.002,'mortar_lower' if y<1.2 else 'mortar_upper')
    elif n=='pillar':
        p.box((0,.6,0),(.4,1.2,.4),'teal')
        p.box((0,2.5,0),(.4,2.6,.4),'cream')
        p.box((0,.05,0),(.44,.1,.44),'rubber')
    elif n=='floor_2x2':
        p.box((0,.035,0),(2,.07,2),'lino')
        for i in range(10):
            x,z = p.rng.uniform(-.85,.65),p.rng.uniform(-.85,.85)
            p.mesh([(x,.0702,z),(x+.15,.0702,z+.014),(x+.22,.0702,z+.025),(x+.03,.0702,z+.02)],
                   [(0,1,2,3)],'scuff' if i%3 else 'rubber')
    elif n.startswith('ceiling'):
        # Acoustic mineral panels with a narrow T-grid, distinct from concrete,
        # barrel-vault stone and tiled ward ceilings. One authored missing panel.
        for x in (-.5,.5):
            for z in (-.5,.5):
                if n=='ceiling_tile_damaged_2x2' and x==.5 and z==.5:
                    continue
                p.box((x,.042,z),(.974,.05,.974),'cream' if x!=z else 'paper')
        for a in (-.99,0,.99):
            p.box((a,.016,0),(.02,.032,2),'steel')
            p.box((0,.016,a),(2,.032,.02),'steel')
        for i in range(20):
            x,z = p.rng.uniform(-.9,.9),p.rng.uniform(-.9,.9)
            if n=='ceiling_tile_damaged_2x2' and x>0 and z>0:
                continue
            p.mesh([(x-.0065,.0165,z-.008),(x+.0065,.0165,z-.008),
                    (x+.0065,.0165,z+.008),(x-.0065,.0165,z+.008)],[(3,2,1,0)],'stain')
    elif n=='trim_base_2m':
        p.box((0,.055,0),(2,.11,.04),'rubber')
        p.box((0,.12,.005),(2,.02,.03),'teal')


def furniture(p):
    n = p.name
    if n in {'prop_desk','prop_teacher_desk','prop_lab_bench','prop_cafeteria_table'}:
        w,d,h = {'prop_desk':(1.1,.65,.76),'prop_teacher_desk':(1.8,.8,.8),
                 'prop_lab_bench':(1.8,.85,.92),'prop_cafeteria_table':(1.8,.8,.76)}[n]
        p.box((0,h-.035,0),(w,.07,d),'rubber' if n=='prop_lab_bench' else 'mustard')
        p.box((0,h-.16,.06),(w-.15,.18,d-.15),'teal')
        for x in (-w/2+.12,w/2-.12):
            for z in (-d/2+.1,d/2-.1):
                p.rod((x,0,z),(x,h-.05,z),.025,'steel')
        if n=='prop_teacher_desk':
            for y in (.2,.4,.6):
                p.box((.58,y,0),(.47,.18,.68),'wood')
                p.box((.58,y,-.35),(.17,.022,.022),'steel')
        if n=='prop_lab_bench':
            p.box((.45,.932,0),(.45,.024,.40),'steel')
            p.box((.45,.946,0),(.35,.006,.3),'rubber')
            p.rod((.55,.94,.23),(.55,1.18,.23),.019,'steel')
            p.rod((.55,1.18,.23),(.55,1.18,.02),.019,'steel')
            for x in (-.5,-.22):
                p.rod((x,.92,0),(x,1.11,0),.07,'glass')
        if n=='prop_cafeteria_table':
            for z in (-.78,.78):
                p.box((0,.43,z),(1.8,.065,.28),'wood')
                for x in (-.65,.65):
                    p.rod((x,0,z),(x,.42,z),.025,'steel')
        for i in range(4):
            p.box((-.32+i*.14,h+.0005,-.1),(.10,.001,.004),'scuff')
    elif n=='prop_chair':
        p.box((0,.45,0),(.44,.055,.44),'mustard')
        p.box((0,.75,.19),(.44,.28,.045),'mustard')
        for x in (-.17,.17):
            for z in (-.17,.17):
                p.rod((x*1.1,0,z*1.1),(x,.46,z),.018,'steel')
            p.rod((x,.4,.17),(x,.89,.20),.018,'steel')
    elif n in {'prop_locker_bank','locker_bank_2m'}:
        p.box((0,1.06,0),(1.96,2.12,.43),'mortar_lower')
        for i,x in enumerate((-.735,-.245,.245,.735)):
            p.box((x,1.08,-.23),(.464,1.99,.045),'mustard' if i in (0,3) else 'teal')
            p.box((x+.15,1.12,-.267),(.033,.14,.03),'steel')
            p.box((x,1.79,-.255),(.10,.035,.008),'paper')
            for y in (.29,.34,.39,1.55,1.6,1.65):
                p.face(x-.14,x+.14,y,y+.013,-.255,'rubber')
        p.box((0,2.15,0),(1.98,.06,.45),'steel')
    elif n in {'prop_chalkboard','chalk_rail_board_2m','bulletin_board'}:
        p.box((0,.65,0),(1.94,1.3,.065),'wood')
        p.box((0,.67,-.038),(1.83,1.16,.015),'wood' if n=='bulletin_board' else 'chalk_green')
        p.box((0,.035,-.10),(1.9,.04,.18),'steel')
        if n=='bulletin_board':
            for x,y,w,h in ((-.6,.85,.36,.46),(-.12,.71,.43,.54),(.45,.88,.35,.4),(.62,.35,.3,.22)):
                p.face(x-w/2,x+w/2,y-h/2,y+h/2,-.049,'paper')
                p.box((x,y+h/2-.018,-.055),(.025,.025,.015),'fire_red')
        else:
            for i in range(18):
                x,y = p.rng.uniform(-.8,.5),p.rng.uniform(.22,1.1)
                p.face(x,x+p.rng.uniform(.04,.2),y,y+.007,-.049,'stain')
            p.box((-.53,.066,-.12),(.22,.042,.06),'rubber')
    elif n=='drinking_fountain':
        p.box((0,.54,.03),(.48,1.08,.38),'teal')
        p.box((0,1.11,-.035),(.61,.09,.52),'steel')
        p.box((0,1.159,-.055),(.44,.009,.32),'rubber')
        p.rod((.17,1.13,.13),(.17,1.24,.13),.028,'steel')
        p.rod((.17,1.24,.13),(.11,1.24,.06),.025,'steel')
    elif n in {'stair_banister_2m','stair_flight_2m'}:
        if n=='stair_flight_2m':
            for i in range(8):
                h = (i+1)*.16
                p.box((0,h/2,-.875+i*.25),(1.8,h,.25),'lino')
                p.box((0,h+.005,-.982+i*.25),(1.8,.01,.032),'mustard')
        else:
            for i in range(9):
                x = -1+i*.25
                p.rod((x,(x+1)*.64,0),(x,1+(x+1)*.64,0),.014,'steel')
            p.rod((-1,1,0),(1,2.28,0),.035,'steel')
            p.rod((-1,.18,0),(1,1.46,0),.018,'steel')
    elif n=='prop_globe':
        p.box((0,.025,0),(.32,.05,.27),'wood')
        p.rod((0,.04,0),(0,.28,0),.025,'steel')
        # Low-poly latitude strips with invented land patches; no external map.
        for lat in range(1,8):
            for lon in range(12):
                vertices=[]
                for a,b in ((lat-1,lon),(lat-1,lon+1),(lat,lon+1),(lat,lon)):
                    phi = -.5*math.pi+a*math.pi/7
                    theta = b*math.tau/12
                    vertices.append((.22*math.cos(phi)*math.cos(theta),.42+.22*math.sin(phi),.22*math.cos(phi)*math.sin(theta)))
                # Pole triangles avoid degenerate quads.
                faces = [(0,2,3)] if lat==1 else [(0,1,2)] if lat==7 else [(0,1,2,3)]
                p.mesh(vertices,faces,'mustard' if (lat*3+lon*7)%11<4 else 'teal')
    elif n in {'prop_fluorescent','prop_fluorescent_dead'}:
        p.box((0,.11,0),(1.48,.08,.36),'steel')
        for z in (-.12,.12):
            p.rod((-.65,.05,z),(.65,.05,z),.028,'tube' if n=='prop_fluorescent' else 'dead_tube')
        for x in (-.69,.69):
            p.box((x,.06,0),(.08,.09,.33),'cream')
            p.rod((x,.15,0),(x,.43,0),.008,'steel',6)
    elif n=='prop_fire_bell':
        p.rod((0,.19,.035),(0,.19,-.065),.19,'fire_red',16)
        p.rod((0,.19,-.07),(0,.19,-.083),.035,'steel')
    elif n=='prop_classroom_door_leaf':
        # Narrow vertical safety window, kick plate, handle. Separate leaf lets
        # the runtime owner hinge/lock it. The authored pose is closed in its socket.
        # One continuous leaf: left stile -0.70..-0.28, window column -0.28..-0.02,
        # right stile -0.02..0.70 (it previously started at +0.06, leaving a full-height seam).
        for x,w in ((-.49,.42),(.34,.72)):
            p.box((x,1.34,0),(w,2.68,.06),'mustard')
        p.box((-.15,.57,0),(.26,1.14,.06),'mustard')
        p.box((-.15,2.51,0),(.26,.34,.06),'mustard')
        p.face(-.28,-.02,1.14,2.34,-.005,'glass')
        for y in (1.25,1.45,1.65,1.85,2.05,2.25):
            p.face(-.28,-.02,y,y+.004,-.006,'mortar_lower')
        # Kick plate wraps both faces; a handle on each side of the leaf.
        p.box((0,.195,0),(1.40,.23,.066),'steel')
        for z in (-.05,.05):
            p.box((.52,1.12,z),(.05,.20,.04),'steel')
        # Two 1.56m leaves fit a 3.2m socket with 2cm jamb/meeting clearances.
        p.vertices = [(x*(1.56/1.4),y,z) for x,y,z in p.vertices]
    elif n=='prop_bookcase':
        for x in (-.9,.9):
            p.box((x,1.15,0),(.07,2.3,.42),'wood')
        p.box((0,1.15,.2),(1.8,2.3,.035),'teal')
        for y in (.08,.6,1.12,1.64,2.26):
            p.box((0,y,0),(1.8,.05,.42),'wood')
        for row in range(4):
            for col in range(12):
                if (row,col) in {(1,3),(1,4),(3,8)}:
                    continue
                h = .27+.10*p.rng.random()
                p.box((-.78+col*.14,.12+row*.52+h/2,-.04),(.11,h,.25),
                      ('mustard','teal','cream','fire_red','chalk_green')[col%5])
    elif n=='prop_bleacher':
        for i in range(4):
            y,z = .25+i*.3,-.9+i*.6
            p.box((0,y,z),(1.98,.08,.55),'wood')
            for x in (-.82,.82):
                p.box((x,y/2,z),(.055,y,.4),'steel')
    elif n=='prop_basketball_hoop':
        p.box((0,.78,.16),(1.7,1.1,.07),'cream')
        for x in (-.29,.29):
            p.box((x,.62,.117),(.024,.38,.008),'fire_red')
        for y in (.43,.81):
            p.box((0,y,.117),(.6,.024,.008),'fire_red')
        for i in range(16):
            a,b = i*math.tau/16,(i+1)*math.tau/16
            p.rod((.27*math.cos(a),.4,-.28+.27*math.sin(a)),
                  (.27*math.cos(b),.4,-.28+.27*math.sin(b)),.015,'fire_red',6)
            if i%2==0:
                p.rod((.27*math.cos(a),.39,-.28+.27*math.sin(a)),
                      (.17*math.cos(a),0,-.28+.17*math.sin(a)),.005,'paper',6)
    elif n=='prop_washbasin':
        p.box((0,.41,0),(.18,.82,.22),'cream')
        p.box((0,.86,0),(.62,.10,.47),'cream')
        p.box((0,.914,-.02),(.46,.008,.30),'steel')
        p.rod((0,.88,.18),(0,1.1,.18),.02,'steel')
        p.rod((0,1.1,.18),(0,1.1,0),.02,'steel')
    elif n=='prop_toilet':
        p.box((0,.23,0),(.31,.46,.47),'cream')
        p.box((0,.48,-.04),(.47,.06,.60),'cream')
        p.box((0,.515,-.06),(.31,.012,.40),'rubber')
        p.box((0,.69,.23),(.46,.42,.17),'cream')
        p.box((.15,.87,.13),(.09,.025,.025),'steel')
    elif n=='prop_stall_partition':
        p.box((0,1.15,0),(.045,1.7,1.7),'teal')
        for z in (-.8,.8):
            p.rod((0,0,z),(0,2.03,z),.024,'steel')
    elif n=='prop_mop_bucket':
        p.box((0,.21,0),(.40,.42,.36),'mustard')
        p.box((0,.425,0),(.3,.01,.26),'rubber')
        p.rod((0,.22,0),(.28,1.6,.08),.022,'wood')
        p.box((-.07,.055,-.03),(.31,.11,.19),'paper')
    else:
        raise ValueError(n)


def export_mesh(obj, path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},
        global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',axis_up='Y',bake_space_transform=True,use_mesh_modifiers=True,
        mesh_smooth_type='FACE',use_triangles=True,bake_anim=False,use_custom_props=False,path_mode='RELATIVE')


def placement(piece, x, y, z, yaw=0):
    return {'id':piece,'pos':[round(x,5),round(y,5),round(z,5)],'rotY':yaw%360}


def rect(w,d):
    return {(x,z) for z in range(d) for x in range(w)}


def boundary(cells):
    result = {}
    for x,z in sorted(cells):
        for side,dx,dz in (('N',0,1),('E',1,0),('S',0,-1),('W',-1,0)):
            if (x+dx,z+dz) in cells:
                continue
            axis = 'X' if side in 'NS' else 'Z'
            fixed = 2*(z+(side=='N')) if axis=='X' else 2*(x+(side=='E'))
            start = 2*x if axis=='X' else 2*z
            result.setdefault((side,fixed),[]).append(start)
    runs=[]
    for (side,fixed), starts in sorted(result.items()):
        starts.sort()
        low,high=starts[0],starts[0]+2
        for start in starts[1:]:
            if start==high:
                high+=2
            else:
                runs.append((side,fixed,low,high))
                low,high=start,start+2
        runs.append((side,fixed,low,high))
    return runs


def wall_pos(side,fixed,center,piece):
    return placement(piece,center if side in 'NS' else fixed,0,
                     fixed if side in 'NS' else center,{'N':0,'E':90,'S':180,'W':270}[side])


def make_room(name,cells,doors,kind='room',shape='rect',gimmick='none'):
    n=len(cells)
    size = 'closet' if n<=4 else 'small' if n<=9 else 'medium' if n<=20 else 'large' if n<=40 else 'hall'
    t={'id':'school_'+name,'kind':kind,'sizeClass':size,'shape':shape,
       'footprint':[list(c) for c in sorted(cells,key=lambda c:(c[1],c[0]))],
       'height':HEIGHT,'doors':[],'anchors':{},'gimmick':gimmick,
       'minRound':3 if gimmick!='none' else 1,'weight':.65 if gimmick!='none' else 1.0,'pieces':[]}
    for cell,side in doors:
        x,z=cell
        fixed=2*(z+(side=='N')) if side in 'NS' else 2*(x+(side=='E'))
        center=2*(x if side in 'NS' else z)+1
        t['doors'].append({'cell':list(cell),'side':side,'closedWith':[
            wall_pos(side,fixed,center-1,'wall_2m'),wall_pos(side,fixed,center+1,'wall_2m')]})
    for side,fixed,low,high in boundary(cells):
        portals=[]
        for cell,s in doors:
            x,z=cell
            f=2*(z+(s=='N')) if s in 'NS' else 2*(x+(s=='E'))
            center=2*(x if s in 'NS' else z)+1
            if s==side and f==fixed and low<=center<=high:
                if not low<=center-2<center+2<=high:
                    raise ValueError(f'{name}: door frame exceeds boundary run')
                portals.append(center)
        cursor=low
        spans=[]
        for center in sorted(portals):
            spans.append((cursor,center-2))
            t['pieces'].append(wall_pos(side,fixed,center,'door_classroom_transom'))
            cursor=center+2
        spans.append((cursor,high))
        for a,b in spans:
            while a<b-.01:
                width=min(2,b-a)
                piece='wall_cinderblock_end_1m' if width==1 else 'wall_cinderblock_dado_2m'
                if width==2 and side in 'NE' and int(a+fixed)%4==0 and name!='janitor_closet':
                    piece='window_boarded_2m' if int(a)%6==0 else 'window_multipane_2m'
                t['pieces'].append(wall_pos(side,fixed,a+width/2,piece))
                a+=width
    for i,(x,z) in enumerate(sorted(cells)):
        t['pieces'].append(placement('floor_2x2',2*x+1,-.0702,2*z+1))
        t['pieces'].append(placement('ceiling_tile_damaged_2x2' if i==len(cells)//2 else 'ceiling_tile_2x2',2*x+1,HEIGHT,2*z+1))
    # Boundary cell centres have 1m wall clearance. Scatter by sorted area rank,
    # then choose unoccupied floor positions after furnishing below.
    t['anchors']={'cake':[],'goldenCake':[],'light':[],'hunterSpawn':[]}
    return t


def furnish(t):
    name=t['id'].removeprefix('school_')
    cells={tuple(c) for c in t['footprint']}
    w=2*(max(x for x,z in cells)+1)
    d=2*(max(z for x,z in cells)+1)
    def put(pid,x,z,y=0,yaw=0):
        t['pieces'].append(placement(pid,x,y,z,yaw))
    # Wall furnishings have bottoms above floor, and face into the room.
    if name in {'classroom','locked_classroom','science_lab'}:
        put('chalk_rail_board_2m',1.2,d-.12,1.3)
        put('prop_teacher_desk',1.4,d-1.3)
        if name!='science_lab':
            put('prop_globe',1.8,d-1.3,.82)
        for x in (2,4,6):
            for z in (2,4):
                if (int(x/2),int(z/2)) in cells:
                    put('prop_lab_bench' if name=='science_lab' else 'prop_desk',x,z)
                    put('prop_chair',x,z-.72)
        if name=='locked_classroom':
            # Frozen tableau is art only; the runtime owns lock/freeze behaviour.
            put('prop_chair',5.3,5.4,yaw=137)
    elif name=='library':
        for x in (1.1,3.2,5.3,7.4):
            put('prop_bookcase',x,d-.38)
        for z in (2,4,6):
            put('prop_bookcase',.35,z,yaw=270)
        for x,z in ((3,3),(5,3),(3,5)):
            put('prop_desk',x,z)
            put('prop_chair',x,z-.7)
    elif name in {'gymnasium','bleacher_traversal'}:
        for x in range(2,int(w)-1,2):
            put('prop_bleacher',x,d-1.4)
        for z in (3,d-4):
            put('prop_basketball_hoop',.25,z,2.2,270)
        if name=='bleacher_traversal':
            put('prop_bleacher',w/2,4,yaw=90)
            put('prop_bleacher',w/2+2,4,yaw=270)
    elif name=='cafeteria':
        for x in (2.5,5,7.5):
            for z in (2.5,6):
                put('prop_cafeteria_table',x,z)
        put('drinking_fountain',w-.3,d-1,0,90)
    elif name=='principal_office':
        put('prop_teacher_desk',2,d-1.7)
        put('prop_globe',2.5,d-1.7,.82)
        put('prop_chair',2,d-2.6)
        put('prop_bookcase',1.1,d-.38)
        put('bulletin_board',w-.12,2,1.3,90)
    elif name=='janitor_closet':
        put('prop_mop_bucket',w-1,.7)
        put('prop_bookcase',1.1,d-.28)
    elif name=='washroom':
        for x in (1.2,2.6,4):
            put('prop_toilet',x,d-.5)
            put('prop_stall_partition',x+.62,d-1)
        for z in (1.1,2.4):
            put('prop_washbasin',.35,z,yaw=270)
    elif name=='locker_hallway':
        for z in (5,7,9,11):
            put('locker_bank_2m',.30,z,yaw=270)
            put('locker_bank_2m',w-.30,z,yaw=90)
        put('drinking_fountain',.3,d-1,0,270)
        put('bulletin_board',w-.12,1.2,1.35,90)
    elif name=='stairwell_bend':
        put('stair_flight_2m',1.15,5.1)
        put('stair_banister_2m',2.1,5.1,0,270)
        for x in (5,7):
            put('locker_bank_2m',x,3.68)
        put('drinking_fountain',.3,1.2,0,270)
    put('prop_fire_bell',.12,.7,2.7,270)
    # Closed leaves share the socket plane, rather than standing perpendicular
    # to it in the room. Runtime setup owns opening/closing this paired door.
    for door in t['doors']:
        x,z=door['cell']; side=door['side']
        cx,cz=2*x+1,2*z+1
        yaw={'N':0,'E':90,'S':180,'W':270}[side]
        if side in 'NS':
            cz=2*(z+(side=='N'))
            for sign in (-1,1):
                put('prop_classroom_door_leaf',cx+sign*.8,cz,0,yaw)
        else:
            cx=2*(x+(side=='E'))
            for sign in (-1,1):
                put('prop_classroom_door_leaf',cx,cz+sign*.8,0,yaw)
    # Keep small rooms readable; the long hall has alternated dead twin-tubes.
    candidates=[]
    for x,z in sorted(cells,key=lambda c:(c[1],c[0])):
        if x%2==0 and z%3==1:
            candidates.append((2*x+1,2*z+1))
    if not candidates:
        x,z=sorted(cells)[len(cells)//2]; candidates=[(2*x+1,2*z+1)]
    for i,(x,z) in enumerate(candidates):
        dead=i%4==3
        put('prop_fluorescent_dead' if dead else 'prop_fluorescent',x,z,3.30)
        if not dead:
            t['anchors']['light'].append([x,3.30,z])
    return t


def safe_anchors(t,pieces):
    # Reject furniture bounds (including door leaves) with extra player clearance;
    # candidates remain at least .6m from EVERY real perimeter, including L notches.
    cells={tuple(c) for c in t['footprint']}
    boxes=[]
    for row in t['pieces']:
        if KINDS[row['id']]!='prop' or row['pos'][1]>2.0:
            continue
        obj=pieces[row['id']]
        yaw=math.radians(row['rotY'])
        pts=[]
        for v in obj.data.vertices:
            x,y,z=v.co.x,v.co.z,-v.co.y
            pts.append((row['pos'][0]+x*math.cos(yaw)+z*math.sin(yaw),
                        row['pos'][2]-x*math.sin(yaw)+z*math.cos(yaw)))
        boxes.append((min(p[0] for p in pts)-.2,max(p[0] for p in pts)+.2,
                      min(p[1] for p in pts)-.2,max(p[1] for p in pts)+.2))
    candidates=[]
    for x,z in sorted(cells,key=lambda c:(c[1],c[0])):
        for dx,dz in ((1,1),(.7,.7),(1.3,1.3),(.7,1.3),(1.3,.7)):
            px,pz=2*x+dx,2*z+dz
            if any(a<px<b and c<pz<d for a,b,c,d in boxes):
                continue
            candidates.append([px,0,pz])
    need=max(2,math.ceil(len(cells)/6))
    chosen=[]
    for p in candidates:
        if all(math.hypot(p[0]-q[0],p[2]-q[2])>=1.4 for q in chosen):
            chosen.append(p)
    if len(chosen)<need+2:
        raise ValueError(t['id']+': insufficient unobstructed anchors')
    t['anchors']['cake']=chosen[:need]
    t['anchors']['goldenCake']=[chosen[-1]] if t['sizeClass'] in {'large','hall'} or t['gimmick']!='none' else []
    t['anchors']['hunterSpawn']=[chosen[-2]] if len(cells)>=10 else []


def templates(pieces):
    definitions=[
        ('classroom',rect(4,4),[((1,0),'S'),((3,1),'E')],{}),
        ('science_lab',rect(4,4)-rect(2,2),[((1,3),'N'),((3,1),'E')],{'shape':'L'}),
        ('library',rect(6,6)-{(x,z) for x in (4,5) for z in (0,1)},[((1,0),'S'),((5,3),'E')],{'shape':'L'}),
        ('gymnasium',rect(7,7),[((1,0),'S'),((6,3),'E')],{}),
        ('cafeteria',rect(5,5),[((1,0),'S'),((4,2),'E')],{}),
        ('principal_office',rect(3,3),[((1,0),'S'),((2,1),'E')],{}),
        ('janitor_closet',rect(4,1),[((1,0),'S')],{}),
        ('washroom',rect(3,3),[((1,0),'S'),((2,1),'E')],{}),
        ('locker_hallway',rect(2,8),[((0,1),'W'),((1,6),'E')],{'kind':'hallway'}),
        ('stairwell_bend',rect(6,2)|rect(2,6),[((4,0),'S'),((0,4),'W')],{'kind':'junction','shape':'L'}),
        ('bleacher_traversal',rect(6,5),[((1,0),'S'),((5,1),'E')],{'gimmick':'traversal'}),
        ('locked_classroom',rect(4,4),[((1,0),'S'),((3,1),'E')],{'gimmick':'freeze'}),
    ]
    result=[]
    for name,cells,doors,opts in definitions:
        t=furnish(make_room(name,cells,doors,**opts))
        safe_anchors(t,pieces)
        result.append(t)
    return {'theme':'school','module':2.0,'templates':result}


def instance(scene,source,name,pos,yaw=0):
    obj=bpy.data.objects.new(name,source.data)
    scene.collection.objects.link(obj)
    obj.location=xyz(pos)
    obj.rotation_euler.z=math.radians(yaw)
    return obj


def configure_render(scene):
    scene.render.engine='CYCLES'
    scene.cycles.samples=24
    scene.cycles.seed=SEED
    scene.cycles.use_denoising=True
    scene.render.resolution_x,scene.render.resolution_y=1440,1000
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='AgX'
    scene.world=bpy.data.worlds.new(scene.name+'_World')
    scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.12,.15,.18,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.3


def light(scene,name,pos,target,power,size=1,kind='AREA'):
    data=bpy.data.lights.new(name,kind)
    data.energy=power
    data.color=(.85,1,.77) if name.startswith('Fluorescent') else (1,.91,.75)
    if kind=='AREA':
        data.shape='RECTANGLE'; data.size=size; data.size_y=.3 if name.startswith('Fluorescent') else size
    elif kind=='SPOT':
        data.spot_size=math.radians(48); data.spot_blend=.55; data.shadow_soft_size=.045
    obj=bpy.data.objects.new(name,data)
    scene.collection.objects.link(obj)
    obj.location=xyz(pos)
    obj.rotation_euler=(xyz(target)-obj.location).to_track_quat('-Z','Y').to_euler()
    return obj


def camera(scene,pos,target,ortho=None):
    obj=bpy.data.objects.new(scene.name+'_Camera',bpy.data.cameras.new(scene.name+'_Camera'))
    scene.collection.objects.link(obj)
    obj.location=xyz(pos)
    obj.rotation_euler=(xyz(target)-obj.location).to_track_quat('-Z','Y').to_euler()
    obj.data.lens=23
    obj.data.clip_start=.03
    if ortho:
        obj.data.type='ORTHO'; obj.data.ortho_scale=ortho
    scene.camera=obj
    return obj


def render(scene,path):
    bpy.context.window.scene=scene
    scene.render.filepath=str(path)
    bpy.ops.render.render(write_still=True)


def kit_sheet(pieces,previews):
    scene=bpy.context.scene
    scene.name='School Kit - editable masters and labelled review'
    configure_render(scene)
    scene.render.resolution_x,scene.render.resolution_y=2400,1800
    for i,(name,obj) in enumerate(pieces.items()):
        obj.hide_render=True
        obj.hide_set(True)
        x,z=(i%6)*4.8,(i//6)*5
        shown=instance(scene,obj,'Sheet_'+name,(x,0,z))
        # Small props need legible thumbnails; only review instances are enlarged.
        # Export masters retain metre scale and applied transforms at the origin.
        if KINDS[name]=='prop':
            extent=max(max(v.co[j] for v in obj.data.vertices)-min(v.co[j] for v in obj.data.vertices) for j in range(3))
            shown.scale=(max(1,2.2/extent),)*3
        text=bpy.data.curves.new('Label_'+name,'FONT')
        text.body=name
        text.size=.24
        text.align_x='CENTER'
        label=bpy.data.objects.new(text.name,text)
        scene.collection.objects.link(label)
        label.location=xyz((x,.02,z-1.6))
        text.materials.append(bpy.data.materials['school_cream'])
    light(scene,'Sheet softbox',(9,22,-3),(10,0,16),14000,15)
    light(scene,'Sheet rim',(22,14,30),(10,1,15),10000,12)
    camera(scene,(29,39,-34),(12,0,15),52)
    for obj in scene.objects:
        if obj.type=='FONT':
            obj.rotation_euler=scene.camera.rotation_euler
    if previews:
        render(scene,REVIEW/'kit-sheet.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Kit/SchoolKit.blend'))


def room_scenes(catalogue,pieces,previews):
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Rooms/SchoolRooms.blend'))
    scenes={}
    for t in catalogue['templates']:
        scene=bpy.data.scenes.new(t['id'])
        scene.unit_settings.system='METRIC'
        scene['template_id']=t['id']
        configure_render(scene)
        cells=t['footprint']
        w=2*(max(c[0] for c in cells)+1); d=2*(max(c[1] for c in cells)+1)
        for i,row in enumerate(t['pieces']):
            obj=instance(scene,pieces[row['id']],f"Placement_{i:04d}_{row['id']}",row['pos'],row['rotY'])
            obj['placement_index']=i
            obj['piece_id']=row['id']
            # Cut away south/west walls and all but the far ceiling strip.
            obj.hide_render=(KINDS[row['id']] in {'wall','door','window'} and row['rotY'] in (180,270)) or (
                KINDS[row['id']]=='ceiling' and row['pos'][2]<d-1.01)
        for i,pos in enumerate(t['anchors']['light']):
            light(scene,f'Fluorescent_{i}',(pos[0],3.28,pos[2]),(pos[0],0,pos[2]),100,1.3)
        light(scene,'Review softbox',(-3,10,-4),(w/2,0,d/2),1800,8)
        light(scene,'Review rim',(w+2,8,d),(w/2,1,d/2),1000,6)
        camera(scene,(-w*.75, max(w,d)*.9,-d*.72),(w/2,1,d/2),max(w,d)*1.48+3)
        scenes[t['id']]=scene
        if previews:
            render(scene,REVIEW/(t['id']+'-three-quarter.png'))
    # Separate full-shell scene: no studio lights, no world light, no cutaway.
    base=scenes['school_locker_hallway']
    dark=bpy.data.scenes.new('School darkness - fluorescent and flashlight only')
    dark.unit_settings.system='METRIC'
    configure_render(dark)
    dark.world.node_tree.nodes['Background'].inputs['Strength'].default_value=0
    for original in base.objects:
        if original.type=='CAMERA' or original.name.startswith('Review'):
            continue
        obj=original.copy()
        dark.collection.objects.link(obj)
        obj.hide_render=False
        if obj.type=='LIGHT':
            obj.data=original.data.copy(); obj.data.energy=32
    light(dark,'Flashlight',(2,1.65,1.1),(1.5,1.35,10),95,kind='SPOT')
    camera(dark,(2,1.65,.72),(1.9,1.5,14))
    if previews:
        render(dark,REVIEW/'in-darkness.png')
    bpy.context.window.scene=scenes['school_classroom']
    # Keep kit scene too, so all linked meshes remain editable in the room source.
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Rooms/SchoolRooms.blend'))


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--skip-previews',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if bpy.app.version[:2]!=(5,2):
        raise RuntimeError('Use Blender 5.2')
    for folder in (ART/'Kit',ART/'Rooms',SOURCE/'Kit',SOURCE/'Rooms',REVIEW):
        folder.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Kit/SchoolKit.blend'))
    materials()
    pieces,rows={},[]
    for name,kind in KINDS.items():
        builder=SchoolMesh(name)
        (furniture if kind=='prop' else architecture)(builder)
        obj=builder.finish(kind)
        pieces[name]=obj
        filename=obj.name+'.fbx'
        export_mesh(obj,ART/'Kit'/filename)
        points=[v.co for v in obj.data.vertices]
        size=[round(max(v[i] for v in points)-min(v[i] for v in points),6) for i in (0,2,1)]
        rows.append({'id':name,'file':filename,'size':size,'kind':kind})
        print(f'BUILT {name}: triangles={len(obj.data.loop_triangles)} size={size}')
    kit={'theme':'school','wallHeight':HEIGHT,'pieces':rows}
    rooms=templates(pieces)
    for path,data in ((ART/'Kit/SchoolKit.manifest.json',kit),(ART/'Rooms/SchoolRooms.manifest.json',rooms)):
        path.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8',newline='\n')
    kit_sheet(pieces,not args.skip_previews)
    room_scenes(rooms,pieces,not args.skip_previews)
    if not args.skip_previews:
        inputs = [Path(__file__),ART/'Kit/SchoolKit.manifest.json',ART/'Rooms/SchoolRooms.manifest.json']
        provenance = {str(path.relative_to(ROOT)).replace('\\','/'):
                      hashlib.sha256(path.read_bytes()).hexdigest() for path in inputs}
        (REVIEW/'preview-inputs.json').write_text(json.dumps(provenance,indent=2)+'\n',encoding='utf-8')
    print(f'GENERATED School: {len(pieces)} pieces, {len(rooms["templates"])} templates; no external assets or textures.')


if __name__=='__main__':
    main()
