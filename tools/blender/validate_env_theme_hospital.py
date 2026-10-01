# ============================================================================
# validate_env_theme_hospital.py
# PURPOSE:
#   Independently validate the delivered ward kit, room catalogue and sources.
#   Re-import actual FBXs and test measured geometry rather than treating a
#   generator assertion or a plausible manifest as proof of an export contract.
# ARCHITECTURAL ROLE: Offline art validator; no Unity runtime dependencies.
# KEY RESPONSIBILITIES:
#   - Verify kit inventory, axes, pivots, budgets, openings and repeat seams.
#   - Verify end caps, floor/wall support, fixture datums and geometric enclosure.
#   - Verify editable source/placement agreement, textures and rendered evidence.
#   - Exercise rejection controls and compare repeat-generation manifest hashes.
# DEPENDENCIES: Blender 5.2 bpy/mathutils/io_scene_fbx, bundled numpy, Python stdlib.
#   Shared support/socket/door checks in validate_env_theme_castle (no generator imported).
# USAGE NOTES:
#   Blender --background --factory-startup --python-exit-code 1 --python this-file
#   -- [--skip-previews] [--record-baseline NAME | --compare-baseline NAME].
#   Baselines are create-only. This file imports neither art generator nor v1.
#   Source and imported-local vertices are compared in Unity Y-up coordinates.
# ============================================================================
import argparse
import copy
import hashlib
import json
import math
from pathlib import Path
import re
import sys

import bpy
from io_scene_fbx import parse_fbx
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT/'Assets/Art/Environment/Hospital'
SOURCE = ROOT/'ArtSource/Environment/Hospital'
OUT = ROOT/'Logs/AgentValidation/Art/EnvHospital'
REQUIRED = {'wall_2m': 'wall', 'wall_door_4m': 'door', 'wall_window_2m': 'window',
            'wall_arc_r4': 'arc', 'wall_arc_r6': 'arc', 'wall_arc_r8': 'arc',
            'corner_in': 'corner', 'corner_out': 'corner', 'pillar': 'pillar',
            'floor_2x2': 'floor', 'ceiling_2x2': 'ceiling', 'trim_base_2m': 'trim',
            'prop_bed': 'prop', 'prop_curtain_rail': 'prop',
            'prop_cabinet': 'prop', 'prop_wheelchair': 'prop'}
SURFACES = {'tile', 'paint', 'grout', 'light', 'vinyl', 'rust', 'stainless',
            'rubber', 'glass', 'curtain', 'linen', 'acoustic', 'door_enamel'}
KINDS = {'wall', 'door', 'window', 'arc', 'corner', 'pillar', 'floor', 'ceiling', 'trim', 'prop', 'pipe', 'duct'}
FIXED_SIZE = {'wall_2m': (2, 3.6, .5), 'wall_door_4m': (4, 3.6, .5),
              'wall_window_2m': (2, 3.6, .5), 'floor_2x2': (2, .16, 2),
              'ceiling_2x2': (2, .18, 2), 'trim_base_2m': (2, .18, .12),
              'corner_in': (.5, 3.6, .5), 'corner_out': (.5, 3.6, .5), 'pillar': (.6, 3.6, .6)}
STEP = {'N': (0, 1), 'E': (1, 0), 'S': (0, -1), 'W': (-1, 0)}
ROT_SIDE = {0: 'N', 90: 'E', 180: 'S', 270: 'W'}


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def close(a, b, tolerance=1e-5):
    return len(a) == len(b) and all(abs(x-y) <= tolerance for x, y in zip(a, b))


def bounds(points):
    return [min(p[i] for p in points) for i in range(3)], [max(p[i] for p in points) for i in range(3)]


def digest(obj, source=False):
    rows = []
    for face in obj.data.polygons:
        points = []
        for index in face.vertices:
            p = obj.data.vertices[index].co
            xyz = (p.x, p.z, -p.y) if source else tuple(p)
            points.append(tuple(0.0 if abs(x) < .000005 else round(x, 5) for x in xyz))
        rows.append((obj.data.materials[face.material_index].name, sorted(points)))
    return hashlib.sha256(json.dumps(sorted(rows), separators=(',', ':')).encode()).hexdigest()


def properties(element):
    node = next((e for e in element.elems if e.id == b'Properties70'), None)
    return {p.props[0].decode(): p.props[4:] for p in node.elems} if node else {}


def raw_fbx(path):
    root, version = parse_fbx.parse(str(path))
    require(version == 7400, f'{path}: FBX version')
    settings = properties(next(e for e in root.elems if e.id == b'GlobalSettings'))
    expected = {'UpAxis': 1, 'UpAxisSign': 1, 'FrontAxis': 2, 'FrontAxisSign': 1,
                'CoordAxis': 0, 'CoordAxisSign': 1, 'UnitScaleFactor': 100.0}
    require(all(settings[k] == [v] for k, v in expected.items()), f'{path}: axes/units')
    elements = next(e for e in root.elems if e.id == b'Objects').elems
    models = [e for e in elements if e.id == b'Model']
    require(len(models) == 1, f'{path}: multiple models')
    props = properties(models[0])
    for key, default in (('Lcl Translation', [0, 0, 0]), ('Lcl Rotation', [0, 0, 0]),
                         ('Lcl Scaling', [1, 1, 1]), ('GeometricTranslation', [0, 0, 0]),
                         ('GeometricRotation', [0, 0, 0]), ('GeometricScaling', [1, 1, 1])):
        require(close(props.get(key, default), default, 1e-6), f'{path}: unapplied {key}')
    for element in elements:
        if element.id not in (b'Texture', b'Video'):
            continue
        relative = next((e for e in element.elems if e.id in (b'RelativeFilename', b'RelativeFileName')), None)
        require(relative is not None, f'{path}: texture lacks relative reference')
        value = relative.props[0].decode().replace('\\', '/')
        require(not Path(value).is_absolute() and ':' not in value, f'{path}: absolute texture {value}')
        target = (path.parent/value).resolve()
        require(target.is_file() and target.parent == path.parent.resolve(), f'{path}: external/missing texture {value}')


def seam(a, b):
    aa = {tuple(round(x, 5) for x in p) for p in a}
    bb = {tuple(round(x, 5) for x in p) for p in b}
    return len(aa) >= 4 and aa == bb


def ends(points, width):
    return ([p for p in points if abs(p[0]+width/2) < 1e-5],
            [p for p in points if abs(p[0]-width/2) < 1e-5])


def opening(tree, points, width, bottom, top, label):
    for x in (-width/2+.001, -width/4, 0, width/4, width/2-.001):
        for y in (bottom+.001, (bottom+top)/2, top-.001):
            for z, direction in ((-1, (0, 0, 1)), (1, (0, 0, -1))):
                require(tree.ray_cast(Vector((x, y, z)), Vector(direction), 2)[0] is None,
                        f'{label}: blocked opening at {(x,y,z)}')
    for x in (-width/2, width/2):
        require(any(abs(p[0]-x) < 1e-5 and abs(p[1]-top) < 1e-5 for p in points), f'{label}: missing jamb/lintel')


def arc_seams(points, radius, degrees):
    half = math.radians(degrees)/2
    edges = []
    for sign in (-1, 1):
        t = sign*half
        actual = [p for p in points if abs(math.atan2(p[0], p[2]+radius)-t) < 1e-6]
        expected = [(r*math.sin(t), y, r*math.cos(t)-radius)
                    for r in (radius-.25, radius+.25) for y in (0, 1.8, 3.6)]
        require(seam(actual, expected), f'r{radius}: radial end cap')
        edges.append(actual)
    t = math.radians(degrees)
    rotated = [(p[0]*math.cos(t)+(p[2]+radius)*math.sin(t), p[1],
                -p[0]*math.sin(t)+(p[2]+radius)*math.cos(t)-radius) for p in edges[0]]
    require(seam(rotated, edges[1]), f'r{radius}: rotated neighbour seam')


def kit_piece(row):
    piece = row['id']
    path = ART/'Kit'/row['file']
    raw_fbx(path)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    objects = list(bpy.context.scene.objects)
    require(len(objects) == 1 and objects[0].type == 'MESH', f'{piece}: export contamination')
    obj = objects[0]
    require(obj.name == 'Hospital_'+piece, f'{piece}: object name')
    require(close(obj.location, (0, 0, 0)) and close(obj.scale, (1, 1, 1)), f'{piece}: transform')
    points = [tuple(v.co) for v in obj.data.vertices]
    low, high = bounds(points)
    require(all(math.isfinite(c) for p in points for c in p), f'{piece}: nonfinite vertices')
    size = [high[i]-low[i] for i in range(3)]
    require(close(size, row['size']), f'{piece}: dimensions disagree with manifest')
    if piece in FIXED_SIZE:
        require(close(size, FIXED_SIZE[piece], .01), f'{piece}: contract dimensions')
    require(abs(low[1]) < 1e-5 and abs(low[0]+high[0]) < 1e-5, f'{piece}: bottom-centre pivot')
    # Open swing leaves retain their wall-plane hinge pivot, not their posed bounds centre.
    if row['kind'] != 'arc' and piece != 'door_double_porthole_4m':
        require(abs(low[2]+high[2]) < 1e-5, f'{piece}: depth-centre pivot')
    require(all(len(f.vertices) == 3 and f.area > 1e-10 for f in obj.data.polygons), f'{piece}: triangles/degeneracy')
    n = len(obj.data.polygons)
    require(0 < n <= (1500 if row['kind'] == 'prop' else 300) and n == row['triangles'], f'{piece}: triangle budget {n}')
    slots = sorted(m.name for m in obj.data.materials)
    require(slots and slots == row['materials'] and set(slots) <= {'hospital_'+s for s in SURFACES}, f'{piece}: material slots')
    require(all(re.fullmatch(r'hospital_[a-z_]+', s) for s in slots), f'{piece}: slot naming')
    require(obj.data.uv_layers.active is not None, f'{piece}: UVs')
    require(all(math.isfinite(c) for loop in obj.data.uv_layers.active.data for c in loop.uv), f'{piece}: nonfinite UVs')
    if row['kind'] in ('wall', 'door', 'window', 'arc', 'floor', 'ceiling', 'pillar'):
        bottom = [p for p in obj.data.polygons if abs(p.center.y-low[1]) < 1e-5]
        top = [p for p in obj.data.polygons if abs(p.center.y-high[1]) < 1e-5]
        require(bottom and top and all(p.normal.y < -.9 for p in bottom) and all(p.normal.y > .9 for p in top), f'{piece}: cap winding')
    require(digest(obj) == row['geometrySha256'], f'{piece}: geometry digest')
    tree = BVHTree.FromPolygons(points, [tuple(p.vertices) for p in obj.data.polygons], all_triangles=True)
    if piece == 'wall_door_4m':
        opening(tree, points, 3.2, 0, 2.8, piece)
    if piece == 'wall_window_2m':
        opening(tree, points, 1.3, 1.1, 2.5, piece)
    if row['kind'] in ('wall', 'window') or piece == 'wall_door_4m':
        require(any(p.center.z < -.20 and p.normal.z < -.9 for p in obj.data.polygons), f'{piece}: -Z face')
    if row['kind'] == 'arc':
        radius = int(piece[-1])
        degrees = {4: 30, 6: 20, 8: 15}[radius]
        require(close(size[:2], (2*(radius+.25)*math.sin(math.radians(degrees)/2), 3.6), .01), f'{piece}: arc dimensions')
        arc_seams(points, radius, degrees)
    return points, tree


def repeat_seams(points):
    left, right = ends(points['wall_2m'], 2)
    require(seam([(x+2, y, z) for x, y, z in left], right), 'wall repeat seam')
    for piece, width in (('wall_window_2m', 2), ('wall_door_4m', 4), ('wall_1m', 1), ('wall_closed_4m', 4)):
        a, b = ends(points[piece], width)
        require(seam([(x+width/2+1, y, z) for x, y, z in a], right), piece+': left seam')
        require(seam([(x-width/2-1, y, z) for x, y, z in b], left), piece+': right seam')
    for piece in ('floor_2x2', 'ceiling_2x2', 'trim_base_2m', 'ceiling_drop_panel_2x2'):
        a, b = ends(points[piece], 2)
        require(seam([(x+2, y, z) for x, y, z in a], b), piece+': X seam')
        if piece != 'trim_base_2m':
            a = [p for p in points[piece] if abs(p[2]+1) < 1e-5]
            b = [p for p in points[piece] if abs(p[2]-1) < 1e-5]
            require(seam([(x, y, z+2) for x, y, z in a], b), piece+': Z seam')


def connected(cells):
    reached = {next(iter(cells))}
    queue = list(reached)
    while queue:
        x, z = queue.pop()
        for dx, dz in STEP.values():
            other = (x+dx, z+dz)
            if other in cells and other not in reached:
                reached.add(other)
                queue.append(other)
    return reached == cells


def boundary_edges(cells):
    result = {}
    for x, z in cells:
        for side, (dx, dz) in STEP.items():
            if (x+dx, z+dz) in cells:
                continue
            if side in 'NS':
                result[((x, z), side)] = ('X', 2*z+(2 if side == 'N' else 0), 2*x, 2*x+2)
            else:
                result[((x, z), side)] = ('Z', 2*x+(2 if side == 'E' else 0), 2*z, 2*z+2)
    return result


def line_piece(p, row):
    side = ROT_SIDE[p['rotY']]
    x, y, z = p['pos']
    width = row['size'][0]
    require(abs(y) < 1e-6, 'wall not at floor level')
    return ('X', z, x-width/2, x+width/2) if side in 'NS' else ('Z', x, z-width/2, z+width/2)


def inside(point, cells):
    x, _, z = point
    return (math.floor(x/2), math.floor(z/2)) in cells


def edge_distance(point, edge):
    axis, line, a, b = edge
    x, _, z = point
    across, along = (z, x) if axis == 'X' else (x, z)
    return math.hypot(across-line, max(a-along, 0, along-b))


def validate_template(t, rows, trees):
    name = t['id']
    require(re.fullmatch(r'hospital_[a-z_]+', name), 'template id')
    require(t['kind'] in ('room', 'hallway', 'junction') and t['shape'] in ('rect', 'L', 'T', 'round', 'irregular'), name+': enums')
    require(t['height'] == 3.6, name+': wall height')
    require(all(len(c) == 2 and all(type(v) is int and v >= 0 for v in c) for c in t['footprint']), name+': footprint cells')
    cells = {tuple(c) for c in t['footprint']}
    require(cells and len(cells) == len(t['footprint']) and connected(cells), name+': disconnected/duplicate footprint')
    n = len(cells)
    size = next(s for limit, s in ((4, 'closet'), (9, 'small'), (20, 'medium'), (40, 'large'), (99999, 'hall')) if n <= limit)
    require(t['sizeClass'] == size, name+': size class')
    if t['shape'] == 'rect':
        require(n == (max(x for x, _ in cells)+1)*(max(z for _, z in cells)+1), name+': false rectangle')
    if t['kind'] == 'hallway':
        require(not any(all((x+dx, z+dz) in cells for dx in range(3) for dz in range(3)) for x, z in cells), name+': hallway wider than two cells')
    require(t['gimmick'] in ('none', 'puzzle', 'freeze', 'traversal'), name+': gimmick')
    require(type(t['minRound']) is int and t['minRound'] >= (1 if t['gimmick'] == 'none' else 3), name+': early gimmick')
    require(math.isfinite(t['weight']) and t['weight'] > 0, name+': weight')
    edges = boundary_edges(cells)
    doors = t['doors']
    require(len(doors) >= (1 if size == 'closet' else 2), name+': too few doors')
    door_keys = [(tuple(d['cell']), d['side']) for d in doors]
    require(len(door_keys) == len(set(door_keys)) and all(k in edges for k in door_keys), name+': non-boundary/duplicate door')
    portal_lines = []
    for d, key in zip(doors, door_keys):
        axis, line, a, b = edges[key]
        center = (a+b)/2+d.get('span',1)-1
        portal_lines.append((axis, line, center-1.6, center+1.6))
        closure = d['closedWith']
        require(len(closure) == 1 and closure[0]['id'] == 'wall_closed_4m', name+': closing alternative')
        require(ROT_SIDE[closure[0]['rotY']] == d['side'], name+': closure facing')
        actual = line_piece(closure[0], rows[closure[0]['id']])
        require(actual[0] == axis and close(actual[1:], (line, center-2, center+2)), name+': closure misaligned')
    anchors = t['anchors']
    require(len(anchors['cake']) >= max(2, (n*2+8)//9), name+': cake count does not scale with area')
    require(len(anchors['goldenCake']) <= 1 and anchors['light'], name+': golden/light sockets')
    require(n < 10 or anchors['hunterSpawn'], name+': hunter spawn absent')
    for kind, values in anchors.items():
        require(kind in ('cake', 'goldenCake', 'light', 'hunterSpawn'), name+': anchor kind')
        for a in values:
            require(len(a) == 3 and all(math.isfinite(v) for v in a) and 0 <= a[1] < 3.6, name+': anchor coordinates')
            require(inside(a, cells), name+': anchor outside footprint')
            # Core walls extend .25m into footprint; clearance measured to inner face.
            require(min(edge_distance(a, e) for e in edges.values()) >= .85-1e-5, name+': anchor wall clearance')
    floors, ceilings, wall_rows = [], [], []
    for p in t['pieces']:
        require(p['id'] in rows, name+': unknown piece '+p['id'])
        require(len(p['pos']) == 3 and all(math.isfinite(v) for v in p['pos']) and math.isfinite(p['rotY']), name+': placement numbers')
        kind = rows[p['id']]['kind']
        if kind == 'floor':
            require(close([p['pos'][1]], [-.16]), name+': floor top is not zero')
            floors.append((round((p['pos'][0]-1)/2), round((p['pos'][2]-1)/2)))
        if kind == 'ceiling':
            require(p['pos'][1] == 3.6, name+': ceiling height')
            ceilings.append((round((p['pos'][0]-1)/2), round((p['pos'][2]-1)/2)))
        if kind == 'wall' or p['id'] == 'wall_door_4m':
            require(p['rotY'] in ROT_SIDE, name+': non-cardinal wall')
            segment = line_piece(p, rows[p['id']])
            axis, line, a, b = segment
            side = ROT_SIDE[p['rotY']]
            coverage = sum(max(0, min(b, e[3])-max(a, e[2])) for (cell, s), e in edges.items()
                           if s == side and e[0] == axis and e[1] == line)
            require(abs(coverage-(b-a)) < 1e-4, name+': wall off boundary/facing outward')
            wall_rows.append((p, segment))
    require(len(floors) == n and set(floors) == cells and len(ceilings) == n and set(ceilings) == cells, name+': floor/ceiling coverage')
    # Pairwise structural spans; intended perpendicular corner joins are not duplicates.
    for i, (_, a) in enumerate(wall_rows):
        for _, b in wall_rows[i+1:]:
            require(a[:2] != b[:2] or min(a[3], b[3])-max(a[2], b[2]) <= 1e-5, name+': overlapping wall pieces')
    door_pieces = [s for p, s in wall_rows if p['id'] == 'wall_door_4m']
    require(len(door_pieces) == len(doors), name+': doorway/socket count')
    for axis, line, a, b in portal_lines:
        require(any(s[0] == axis and close(s[1:], (line, a-.4, b+.4)) for s in door_pieces), name+': doorway/socket alignment')
    # Real imported geometry ray probes, 10cm along every boundary at three heights.
    # A closed wall elsewhere or a manifest span cannot hide a hole in an FBX.
    for axis, line, a, b in edges.values():
        for index in range(20):
            along = a+(index+.5)/10
            candidates = [(p, s) for p, s in wall_rows if s[0] == axis and s[1] == line and s[2] <= along <= s[3]]
            for height in (.35, 1.45, 3.1):
                portal = height < 2.8 and any(q[0] == axis and q[1] == line and q[2] < along < q[3] for q in portal_lines)
                hit = False
                for p, segment in candidates:
                    angle = math.radians(p['rotY'])
                    wx, wz = (along, line) if axis == 'X' else (line, along)
                    dx, dz = wx-p['pos'][0], wz-p['pos'][2]
                    lx = math.cos(angle)*dx-math.sin(angle)*dz
                    # Cast in the piece's local Unity space through its wall plane.
                    if trees[p['id']].ray_cast(Vector((lx, height, -1)), Vector((0, 0, 1)), 2)[0] is not None:
                        hit = True
                require(hit != portal, name+(': blocked socket' if portal else ': enclosure hole')+f' at {axis}/{line}/{along}/{height}')
    return {'id': name, 'cells': n, 'doors': len(doors), 'pieces': len(t['pieces']), 'cakeAnchors': len(anchors['cake'])}


def room_catalogue(rooms, rows, trees):
    require(rooms['theme'] == 'hospital' and rooms['module'] == 2, 'room manifest header')
    templates = rooms['templates']
    require(len(templates) >= 10 and len({t['id'] for t in templates}) == len(templates), 'catalogue count/ids')
    counts = {s: sum(t['kind'] == 'room' and t['sizeClass'] == s for t in templates)
              for s in ('closet', 'small', 'medium', 'large', 'hall')}
    require(counts['closet'] >= 1 and counts['small'] >= 2 and counts['medium'] >= 2 and counts['large'] >= 1 and counts['hall'] >= 1,
            'missing catalogue size coverage')
    require(any(t['kind'] == 'room' and t['sizeClass'] == 'medium' and t['shape'] in ('L', 'round') for t in templates), 'no irregular medium room')
    require(any(t['kind'] == 'hallway' and t['shape'] == 'rect' for t in templates) and
            any(t['kind'] in ('hallway', 'junction') and t['shape'] != 'rect' for t in templates), 'hallway catalogue')
    require(1 <= sum(t['gimmick'] != 'none' for t in templates) <= 2, 'gimmick catalogue')
    return [validate_template(t, rows, trees) for t in templates]


def sources(rows, rooms):
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Kit/HospitalKit.blend'))
    require(bpy.context.scene.unit_settings.scale_length == 1, 'source metre units')
    for piece, row in rows.items():
        obj = bpy.data.objects.get('Hospital_'+piece)
        require(obj is not None and obj.type == 'MESH', piece+': missing source')
        require(close(obj.location, (0, 0, 0)) and close(obj.rotation_euler, (0, 0, 0)) and close(obj.scale, (1, 1, 1)), piece+': source transform')
        require(digest(obj, True) == row['geometrySha256'], piece+': source/export mismatch')
    images = [i for i in bpy.data.images if i.type == 'IMAGE']
    require(len(images) <= 6, 'source texture count')
    for image in images:
        require(image.packed_file and image.filepath.startswith('//'), image.name+': portability')
        require(0 < min(image.size) <= max(image.size) <= 1024, image.name+': resolution')
        exported = Path(bpy.path.abspath(image.filepath))
        authored = SOURCE/'Kit'/exported.name
        require(exported.is_file() and authored.is_file(), image.name+': missing texture')
        require(sha(exported) == sha(authored) and bytes(image.packed_file.data) == exported.read_bytes(), image.name+': texture drift')
    require(len(list((ART/'Kit').glob('*.png'))) <= 6, 'export texture count')
    sheet = bpy.data.collections.get('Kit sheet - every exported piece')
    require(sheet is not None and {o.name.removeprefix('Sheet_') for o in sheet.objects if o.type == 'MESH'} == set(rows), 'kit sheet coverage')
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Rooms/HospitalRooms.blend'))
    for t in rooms['templates']:
        col = bpy.data.collections.get(t['id'])
        require(col is not None, t['id']+': missing source collection')
        instances = {o.get('placementIndex'): o for o in col.objects if o.type == 'MESH'}
        require(len(instances) == len(t['pieces']), t['id']+': source placement count')
        for i, p in enumerate(t['pieces']):
            obj = instances[i]
            require(obj['pieceId'] == p['id'], t['id']+': wrong source piece')
            require(close(obj.location, (p['pos'][0], -p['pos'][2], p['pos'][1])) and close(obj.scale, (1, 1, 1)), t['id']+': source position/scale')
            require(abs((math.degrees(obj.rotation_euler.z)-p['rotY']+180)%360-180) < .001, t['id']+': source Unity yaw')
            require(digest(obj, True) == rows[p['id']]['geometrySha256'], t['id']+': source geometry drift')
            if rows[p['id']]['kind'] == 'ceiling' or p['id'] in (
                    'light_fluorescent_panel', 'light_fluorescent_dead', 'prop_operating_lamp'):
                require(obj.hide_render, t['id']+': cutaway leaves isolated ceiling slab/fixture')
        lamps = [o for o in col.objects if o.type == 'LIGHT' and 'fluorescent' in o.name]
        require(len(lamps) == len(t['anchors']['light']), t['id']+': lamp sockets not assembled')
    for im in [i for i in bpy.data.images if i.type == 'IMAGE']:
        require(im.filepath.startswith('//') and Path(bpy.path.abspath(im.filepath)).is_file(), im.name+': room texture path')


def previews(rooms):
    names = {'kit-sheet': (2100, 1650), 'in-darkness': (1100, 850)}
    names.update({t['id']+'-three-quarter': (1100, 850) for t in rooms['templates']})
    details = []
    for name, resolution in names.items():
        path = OUT/(name+'.png')
        require(path.is_file(), 'missing preview '+name)
        image = bpy.data.images.load(str(path), check_existing=False)
        require(tuple(image.size) == resolution, name+': render resolution')
        pixels = np.empty(len(image.pixels), dtype=np.float32)
        image.pixels.foreach_get(pixels)
        rgb = pixels.reshape(-1, 4)[:, :3]
        bright = float((rgb.max(axis=1) > .12).mean())
        require(float(rgb.std()) > .025 and bright > .08, name+': blank/unreadable preview')
        details.append({'name': name, 'sha256': sha(path), 'rgbStd': float(rgb.std()), 'visibleFraction': bright})
        bpy.data.images.remove(image)
    evidence = json.loads((OUT/'review-scenes.json').read_text(encoding='utf-8'))
    require({r['id'] for r in evidence['rooms']} == {t['id'] for t in rooms['templates']}, 'review provenance')
    require(all(r['cutawayPlacementIndices'] for r in evidence['rooms']), 'missing cutaway ceilings')
    require(evidence['darkness']['worldStrength'] == 0 and evidence['darkness']['otherLights'] == 0 and evidence['darkness']['completeShell'], 'darkness isolation')
    return details


def rejection_controls(rooms, rows, trees):
    template = rooms['templates'][0]
    mutations = []
    t = copy.deepcopy(template); t['footprint'].append([99, 99]); mutations.append(('disconnected footprint', t))
    t = copy.deepcopy(template); t['doors'][0]['side'] = 'N'; mutations.append(('interior door', t))
    t = copy.deepcopy(template); t['anchors']['cake'][0] = [-1, 0, -1]; mutations.append(('outside anchor', t))
    t = copy.deepcopy(template); t['anchors']['cake'][0] = [.1, 0, .1]; mutations.append(('wall-clearance anchor', t))
    t = copy.deepcopy(template); t['pieces'][0]['id'] = 'missing_piece'; mutations.append(('unknown piece', t))
    t = copy.deepcopy(template); t['pieces'].append(copy.deepcopy(next(p for p in t['pieces'] if p['id'] == 'wall_2m'))); mutations.append(('overlapping wall', t))
    t = copy.deepcopy(template); t['pieces'].remove(next(p for p in t['pieces'] if p['id'] == 'wall_2m')); mutations.append(('enclosure gap', t))
    t = copy.deepcopy(template); t['gimmick'] = 'freeze'; mutations.append(('early gimmick', t))
    t = copy.deepcopy(template); t['pieces'][0]['rotY'] = 90; mutations.append(('wrong wall orientation', t))
    rejected = []
    for label, t in mutations:
        try:
            validate_template(t, rows, trees)
        except AssertionError:
            rejected.append(label)
        else:
            raise AssertionError('negative control unexpectedly accepted: '+label)
    require(not close((2.02, 3.6, .5), (2, 3.6, .5), .01), 'dimension rejection control')
    edge = [(1, y, z) for y in (0, 3.6) for z in (-.25, .25)]
    require(seam(edge, edge) and not seam(edge, [(x, y, z+.002) for x, y, z in edge]), 'seam rejection control')
    try:
        arc_seams(edge, 4, 30)
    except AssertionError:
        rejected.append('invalid arc')
    else:
        raise AssertionError('arc rejection control accepted')
    return rejected+['2cm dimension drift', '2mm seam drift']


def check_fixture_attachments(t, rows, points):
    """Use imported mesh heights, not just the origins of bottom-pivot pieces."""
    name = t['id']
    leaves = [p for p in t['pieces'] if p['id'] == 'door_double_porthole_4m']
    require(len(leaves) == len(t['doors']), name+': door leaf/socket count')
    for door in t['doors']:
        x,z = door['cell']; dx,dz = STEP[door['side']]
        position = (2*x+1+dx+(door.get('span',1)-1)*abs(dz), 0,
                    2*z+1+dz+(door.get('span',1)-1)*abs(dx))
        require(sum(close(p['pos'], position, .05) and ROT_SIDE.get(p['rotY']) == door['side']
                    for p in leaves) == 1, name+': detached door leaves')
    for p in t['pieces']:
        lo,hi = bounds(points[p['id']])
        if rows[p['id']]['kind'] == 'ceiling':
            require(abs(p['pos'][1]+lo[1]-t['height']) <= .05, name+': measured ceiling height')
        if p['id'] in ('curtain_track_bay', 'light_fluorescent_panel',
                       'light_fluorescent_dead', 'prop_operating_lamp'):
            require(abs(p['pos'][1]+hi[1]-t['height']) <= .05,
                    name+': detached ceiling fixture '+p['id'])


def fixture_rejection_controls(templates, rows, points):
    base = next(t for t in templates if t['id'] == 'hospital_ward_bed_bays')
    labels = []
    for piece,axis,delta in (('ceiling_drop_panel_2x2',1,-2.6),
                             ('curtain_track_bay',1,-.165),
                             ('light_fluorescent_panel',1,-.15),
                             ('door_double_porthole_4m',0,.3)):
        damaged = copy.deepcopy(base)
        next(p for p in damaged['pieces'] if p['id'] == piece)['pos'][axis] += delta
        try:
            check_fixture_attachments(damaged, rows, points)
        except AssertionError:
            labels.append('detached '+piece)
        else:
            raise AssertionError('negative control accepted: '+piece)
    return labels


def main():
    parser = argparse.ArgumentParser(description='Independent Hospital kit and room validator')
    group = parser.add_mutually_exclusive_group()
    group.add_argument('--record-baseline')
    group.add_argument('--compare-baseline')
    parser.add_argument('--skip-previews', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    require(bpy.app.version[:2] == (5, 2), 'Use Blender 5.2')
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from validate_env_theme_castle import check_door_quality, check_door_previews, placement_regressions
    check_door_quality('hospital', OUT)
    OUT.mkdir(parents=True, exist_ok=True)
    kit_path, room_path = ART/'Kit/HospitalKit.manifest.json', ART/'Rooms/HospitalRooms.manifest.json'
    kit = json.loads(kit_path.read_text(encoding='utf-8'))
    rooms = json.loads(room_path.read_text(encoding='utf-8'))
    rows = {r['id']: r for r in kit['pieces']}
    require(kit['theme'] == 'hospital' and kit['wallHeight'] == 3.6, 'kit header')
    require(len(rows) == len(kit['pieces']) and REQUIRED.items() <= {p: r['kind'] for p, r in rows.items()}.items(), 'contract inventory')
    require({p.name for p in (ART/'Kit').glob('*.fbx')} == {r['file'] for r in rows.values()}, 'extra/missing FBX')
    points, trees = {}, {}
    for piece, row in rows.items():
        require(row['kind'] in KINDS and row['file'] == 'Hospital_'+piece+'.fbx', piece+': manifest kind/file')
        points[piece], trees[piece] = kit_piece(row)
    repeat_seams(points)
    room_details = room_catalogue(rooms, rows, trees)
    controls = rejection_controls(rooms, rows, trees)
    for t in rooms['templates']:
        check_fixture_attachments(t, rows, points)
    controls += fixture_rejection_controls(rooms['templates'], rows, points)
    placement_regressions(rooms['templates'], rows, points)
    if not args.skip_previews:
        check_door_previews('hospital', OUT)
    sources(rows, rooms)
    preview_details = [] if args.skip_previews else previews(rooms)
    snapshot = {'kitManifestSha256': sha(kit_path), 'roomManifestSha256': sha(room_path),
                'geometry': {p: r['geometrySha256'] for p, r in rows.items()},
                'textures': {p.name: sha(p) for p in sorted((ART/'Kit').glob('*.png'))}}
    baseline_name = args.record_baseline or args.compare_baseline
    if baseline_name:
        require(re.fullmatch(r'[a-zA-Z0-9_-]+', baseline_name), 'invalid baseline name')
        baseline = OUT/(baseline_name+'.json')
        if args.record_baseline:
            with baseline.open('x', encoding='utf-8') as stream:
                json.dump(snapshot, stream, indent=2)
        else:
            require(snapshot == json.loads(baseline.read_text(encoding='utf-8')), 'repeat manifest/geometry/texture drift')
    lines = [f'PASS hospital: 16/16 mandatory pieces; {len(rows)} total pieces; manifest/files/geometry agree',
             'PASS hospital: metre dimensions; bottom-centre/wall-plane pivots; Y-up/-Z-forward; applied FBX transforms',
             f"PASS hospital: architecture <=300 triangles (max {max(r['triangles'] for r in rows.values() if r['kind'] != 'prop')}); props <=1500 (max {max(r['triangles'] for r in rows.values() if r['kind'] == 'prop')}); materials/UVs/normals",
             'PASS hospital: straight/interchange/floor/ceiling/trim seams; r4/r6/r8 rotated seams; clear 3.2x2.8 doors and 1.3x1.4 windows',
             f'PASS hospital: {len(room_details)} templates; catalogue sizes/shapes/hallways/gimmick rounds; 4-connected footprints; boundary doors and closures',
             'PASS hospital: anchors inside with >=0.6m wall clearance; area-scaled cakes; medium+ hunter sockets; all piece references resolve',
             'PASS hospital: no overlapping wall spans; complete floor/ceiling coverage; imported-FBX enclosure rays clear only at door sockets',
             'PASS hospital: measured ceiling height, ceiling fixture support and door leaf/socket attachment in every template',
             'PASS hospital: kit source/export and every room-source placement agree; packed relative textures <=1024px and <=6 per theme',
             f'PASS hospital: {len(controls)} rejection controls (kit dimensions/seams/arcs and malformed room contracts)']
    if not args.skip_previews:
        lines.append(f'PASS hospital: {len(preview_details)} nonblank previews; all-piece kit sheet; each room cutaway; theme-only plus flashlight darkness view')
    if args.compare_baseline:
        lines.append('PASS hospital: determinism; both manifest hashes plus geometry/texture hashes identical across two generations')
    report = {'pass': True, 'checks': lines, 'snapshot': snapshot, 'rooms': room_details,
              'negativeControls': controls, 'previews': preview_details,
              'limits': ['No Unity import, navigation, runtime socket closure or artistic acceptance is established.',
                         'Wall nonoverlap refers to collinear spans; perpendicular module corner joins are intentional.',
                         'Span-two corridor sockets require the matching runtime consumer contract.']}
    (OUT/'validation.txt').write_text('\n'.join(lines)+'\n', encoding='utf-8')
    (OUT/'validation.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    print('\n'.join(lines))
    print('Kit manifest SHA256 '+snapshot['kitManifestSha256'])
    print('Room manifest SHA256 '+snapshot['roomManifestSha256'])


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        OUT.mkdir(parents=True, exist_ok=True)
        (OUT/'validation.txt').write_text('FAIL hospital: '+str(error)+'\n', encoding='utf-8')
        (OUT/'validation.json').write_text(json.dumps({'pass': False, 'error': str(error)}, indent=2)+'\n', encoding='utf-8')
        raise
