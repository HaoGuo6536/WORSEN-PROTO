# ============================================================================
# validate_env_theme_castle.py
# PURPOSE:
#   Independently re-import Castle exports and test the delivered geometry,
#   rather than accepting assertions from the authoring script. Validate room
#   topology, actual wall solids, source instances, previews and repeat hashes.
# ARCHITECTURAL ROLE: Offline art validator; outside Unity runtime layers.
# KEY RESPONSIBILITIES:
#   - Enforce inventory, axes, pivots, dimensions, budgets, openings and seams.
#   - Verify room schema, topology, sockets, anchors and geometric enclosure.
#   - Compare source geometry/placements and reject broken preview evidence.
#   - Record immutable determinism baselines and exercise negative controls.
#   - Supply measured support, end-cap, door and spatial regression checks.
# DEPENDENCIES: Blender 5.2 bpy/mathutils/io_scene_fbx, bundled NumPy, stdlib.
# USAGE NOTES:
#   Run in Blender with -- [--skip-previews] [--record-baseline NAME |
#   --compare-baseline NAME]. Baselines and reports live under EnvCastle Logs.
#   No generator import, Unity process, network dependency or shared-file write.
#   Owner-approved v2 overrides: .8m walls and pointed crowns above preserved
#   clear door/window rectangles. Zero-area boundary contact is not overlap.
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

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT/'Assets/Art/Environment/Castle'
SOURCE = ROOT/'ArtSource/Environment/Castle'
OUT = ROOT/'Logs/AgentValidation/Art/EnvCastle'
REQUIRED = {'wall_2m': 'wall', 'wall_door_4m': 'door', 'wall_window_2m': 'window',
            'wall_arc_r4': 'arc', 'wall_arc_r6': 'arc', 'wall_arc_r8': 'arc',
            'corner_in': 'corner', 'corner_out': 'corner', 'pillar': 'pillar',
            'floor_2x2': 'floor', 'ceiling_2x2': 'ceiling', 'trim_base_2m': 'trim',
            'prop_torch_sconce': 'prop', 'prop_banner': 'prop', 'prop_barrel': 'prop', 'prop_rubble': 'prop'}
KINDS = {'wall', 'door', 'window', 'arc', 'corner', 'pillar', 'floor', 'ceiling', 'trim', 'prop', 'pipe', 'duct'}
SIDES = {'N': (0, 1), 'E': (1, 0), 'S': (0, -1), 'W': (-1, 0)}
PALETTE = {'stone': '#4a4f55', 'stone_dark': '#2e3136', 'soot': '#141416',
           'mortar': '#6b675f', 'wood': '#4a3423', 'metal': '#26282b'}


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def close(a, b, tolerance=1e-5):
    return len(a) == len(b) and all(abs(x-y) <= tolerance for x, y in zip(a, b))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def bounds(points):
    return ([min(v[i] for v in points) for i in range(3)],
            [max(v[i] for v in points) for i in range(3)])


def mesh_hash(obj, source=False):
    faces = []
    for face in obj.data.polygons:
        points = []
        for index in face.vertices:
            p = obj.data.vertices[index].co
            xyz = (p.x, p.z, -p.y) if source else tuple(p)
            points.append(tuple(0.0 if abs(x) < .000005 else round(x, 5) for x in xyz))
        faces.append((obj.data.materials[face.material_index].name, sorted(points)))
    return hashlib.sha256(json.dumps(sorted(faces), separators=(',', ':')).encode()).hexdigest()


def fbx_properties(element):
    node = next((e for e in element.elems if e.id == b'Properties70'), None)
    return {p.props[0].decode(): p.props[4:] for p in node.elems} if node else {}


def check_fbx(path):
    root, version = parse_fbx.parse(str(path))
    require(version == 7400, f'{path}: FBX version')
    settings = fbx_properties(next(e for e in root.elems if e.id == b'GlobalSettings'))
    expected = {'UpAxis': 1, 'UpAxisSign': 1, 'FrontAxis': 2, 'FrontAxisSign': 1,
                'CoordAxis': 0, 'CoordAxisSign': 1, 'UnitScaleFactor': 100.0}
    require(all(settings[k] == [v] for k, v in expected.items()), f'{path}: axes/units')
    objects = next(e for e in root.elems if e.id == b'Objects')
    models = [e for e in objects.elems if e.id == b'Model']
    require(len(models) == 1, f'{path}: export model count')
    props = fbx_properties(models[0])
    for key, default in (('Lcl Translation', [0,0,0]), ('Lcl Rotation', [0,0,0]), ('Lcl Scaling', [1,1,1]),
                         ('GeometricTranslation', [0,0,0]), ('GeometricRotation', [0,0,0]), ('GeometricScaling', [1,1,1])):
        require(close(props.get(key, default), default, 1e-6), f'{path}: unapplied {key}')
    for node in objects.elems:
        if node.id == b'Texture':
            rel = next((e for e in node.elems if e.id == b'RelativeFilename'), None)
            require(rel is not None, f'{path}: no relative texture path')
            value = rel.props[0].decode().replace('\\', '/')
            require(not re.match(r'^[A-Za-z]:|^/', value), f'{path}: absolute texture path {value}')
            require((path.parent/value).is_file(), f'{path}: missing texture {value}')


def same_points(a, b):
    return len(a) >= 4 and {tuple(round(x, 5) for x in v) for v in a} == {tuple(round(x, 5) for x in v) for v in b}


def tree_for(points, faces):
    return BVHTree.FromPolygons([Vector(v) for v in points], faces, all_triangles=True)


def opening(tree, width, bottom, top, name):
    for x in (-width/2+.002, -width/4, 0, width/4, width/2-.002):
        for y in (bottom+.002, (bottom+top)/2, top-.002):
            for z, dz in ((-1, 1), (1, -1)):
                require(tree.ray_cast(Vector((x,y,z)), Vector((0,0,dz)), 2)[0] is None,
                        f'{name}: obstructed opening {(x,y,z)}')


def check_piece(row):
    path = ART/'Kit'/row['file']
    check_fbx(path)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    objects = list(bpy.context.scene.objects)
    require(len(objects) == 1 and objects[0].type == 'MESH', row['id']+': export contamination')
    obj = objects[0]
    name = row['id']
    require(obj.name == 'Castle_'+name, name+': naming')
    points = [tuple(v.co) for v in obj.data.vertices]
    faces = [tuple(p.vertices) for p in obj.data.polygons]
    lo, hi = bounds(points)
    size = [hi[i]-lo[i] for i in range(3)]
    require(all(math.isfinite(c) for p in points for c in p), name+': nonfinite geometry')
    require(close(size, row['size']), f'{name}: measured dimensions {size} != {row["size"]}')
    require(close(obj.location, (0,0,0)) and close(obj.scale, (1,1,1)), name+': transform')
    require(abs(lo[1]) < 1e-5 and abs(lo[0]+hi[0]) < 1e-5, name+': bottom-centre pivot')
    if row['kind'] != 'arc' and name not in ('wall_round_tangent_r4','floor_apse_r4','ceiling_apse_r4'):
        require(abs(lo[2]+hi[2]) < 1e-5, name+': depth-centre pivot')
    fixed = {'wall_2m': (2,7,.8), 'wall_door_4m': (4,7,.8), 'wall_window_2m': (2,7,.8),
             'wall_arrow_slit_2m': (2,7,.8), 'floor_2x2': (2,.16,2), 'ceiling_2x2': (2,.18,2),
             'corner_in': (.8,7,.8), 'corner_out': (.8,7,.8), 'pillar': (.8,7,.8),
             'trim_base_2m': (2,.24,.18)}
    if name in fixed:
        require(close(size, fixed[name], .01), f'{name}: owner dimensions {size}')
    require(all(len(f) == 3 for f in faces), name+': triangulation')
    require(all(p.area > 1e-10 for p in obj.data.polygons), name+': degenerate face')
    budget = 1500 if row['kind'] == 'prop' else 300
    require(0 < len(faces) <= budget and len(faces) == row['triangles'], name+': triangle budget/count')
    mats = sorted(m.name for m in obj.data.materials)
    require(mats == row['materials'] and mats and all(re.fullmatch(r'castle_[a-z_]+', m) for m in mats), name+': slots')
    require(set(mats) <= {'castle_'+s for s in dict(PALETTE, ember='#ffb347')}, name+': palette slot')
    require(obj.data.uv_layers.active is not None, name+': UVs')
    require(mesh_hash(obj) == row['geometrySha256'], name+': geometry hash')
    if name in REQUIRED and row['kind'] in ('wall','door','window','arc','floor','ceiling','pillar'):
        for axis_y, sign in ((lo[1], -1), (hi[1], 1)):
            end = [p for p in obj.data.polygons if abs(p.center.y-axis_y) < 1e-5]
            require(end and all(p.normal.y*sign > .9 for p in end), name+': inverted top/bottom')
    if row['kind'] in ('wall', 'door', 'window'):
        require(any(p.center.z < -.2 and p.normal.z < -.3 for p in obj.data.polygons), name+': -Z face')
    tree = tree_for(points, faces)
    if name in ('wall_door_4m', 'arch_pointed_door'):
        opening(tree, 3.2, 0, 2.8, name)
        require(tree.ray_cast(Vector((0,4.3,-1)), Vector((0,0,1)), 2)[0] is None, name+': pointed crown missing')
        require(tree.ray_cast(Vector((1.4,4.3,-1)), Vector((0,0,1)), 2)[0] is not None, name+': crown not pointed')
    if name == 'wall_window_2m':
        opening(tree, 1.3, 1.1, 2.5, name)
    if name == 'wall_arrow_slit_2m':
        opening(tree, .28, 2, 3.6, name)
    if row['kind'] == 'arc':
        radius = int(name[-1])
        angle = math.radians({4:30,6:20,8:15}[radius])
        ends = []
        for sign in (-1, 1):
            t = sign*angle/2
            actual = [p for p in points if abs(math.atan2(p[0], p[2]+radius)-t) < 1e-6]
            expected = [(r*math.sin(t), y, r*math.cos(t)-radius)
                        for r in (radius-.4, radius+.4) for y in (0,7)]
            require(same_points(actual, expected), name+': radius/angle/end seam')
            ends.append(actual)
        turned = [(p[0]*math.cos(angle)+(p[2]+radius)*math.sin(angle), p[1],
                   -p[0]*math.sin(angle)+(p[2]+radius)*math.cos(angle)-radius) for p in ends[0]]
        require(same_points(turned, ends[1]), name+': rotated seam')
    return {'points': points, 'faces': faces, 'tree': tree, 'row': row}


def check_seams(meshes):
    points = {k: v['points'] for k, v in meshes.items()}
    def ends(piece, width):
        return ([v for v in points[piece] if abs(v[0]+width/2) < 1e-5],
                [v for v in points[piece] if abs(v[0]-width/2) < 1e-5])
    left, right = ends('wall_2m', 2)
    require(same_points([(x+2,y,z) for x,y,z in left], right), 'wall repeat seam')
    for name, width in (('wall_door_4m',4), ('wall_window_2m',2), ('wall_arrow_slit_2m',2)):
        a, b = ends(name, width)
        require(same_points([(x+width/2+1,y,z) for x,y,z in a], right), name+': left interchange')
        require(same_points([(x-width/2-1,y,z) for x,y,z in b], left), name+': right interchange')
    for name in ('floor_2x2','ceiling_2x2','trim_base_2m'):
        a, b = ends(name, 2)
        require(same_points([(x+2,y,z) for x,y,z in a], b), name+': X seam')
        if name != 'trim_base_2m':
            a = [v for v in points[name] if abs(v[2]+1) < 1e-5]
            b = [v for v in points[name] if abs(v[2]-1) < 1e-5]
            require(same_points([(x,y,z+2) for x,y,z in a], b), name+': Z seam')


def transform(point, p):
    a = math.radians(p['rotY'])
    x,y,z = point
    return (x*math.cos(a)+z*math.sin(a)+p['pos'][0], y+p['pos'][1],
            -x*math.sin(a)+z*math.cos(a)+p['pos'][2])


def boundary_edges(cells):
    edges = []
    for x,z in cells:
        for side,(dx,dz) in SIDES.items():
            if (x+dx,z+dz) not in cells:
                center = (2*x+1+dx, 2*z+1+dz)
                tangent = (1,0) if dx == 0 else (0,1)
                edges.append(((x,z), side, center, tangent))
    return edges


def segment_distance(x,z,center,tangent):
    u = max(-1, min(1, (x-center[0])*tangent[0]+(z-center[1])*tangent[1]))
    return math.hypot(x-center[0]-u*tangent[0], z-center[1]-u*tangent[1])


def connected(cells):
    seen = {next(iter(cells))}
    pending = list(seen)
    while pending:
        x,z = pending.pop()
        for dx,dz in SIDES.values():
            c = x+dx,z+dz
            if c in cells and c not in seen:
                seen.add(c)
                pending.append(c)
    return seen == cells


def socket_center(socket):
    x,z = socket['cell']
    dx,dz = SIDES[socket['side']]
    span = socket.get('span',1)
    require(type(span) is int and span in (1,2), 'socket span must be 1 or 2')
    return (2*x+1+dx+(span-1)*abs(dz), 2*z+1+dz+(span-1)*abs(dx))


def check_hallway_endcaps(t):
    """An arm ends in a complete 1/2-cell transverse run with inward depth.

    Discover caps independently of the declared sockets. A long side cannot
    qualify merely because its socket is close to a bounding-box extremity.
    The second transverse row must be identical; every discovered arm gets
    exactly one socket, including all three arms of a T junction.
    """
    if t['kind'] not in ('hallway','junction'):
        return
    cells = {tuple(c) for c in t['footprint']}
    caps = set()
    for cell,side,_,_ in boundary_edges(cells):
        dx,dz = SIDES[side]
        tx,tz = abs(dz),abs(dx)
        if (cell[0]-tx,cell[1]-tz) in cells:
            continue
        run = []
        c = cell
        while c in cells:
            run.append(c)
            c = c[0]+tx,c[1]+tz
        if len(run) not in (1,2):
            continue
        if any((x+dx,z+dz) in cells or (x-dx,z-dz) not in cells for x,z in run):
            continue
        behind = [(x-dx,z-dz) for x,z in run]
        if (behind[0][0]-tx,behind[0][1]-tz) in cells or (behind[-1][0]+tx,behind[-1][1]+tz) in cells:
            continue
        caps.add((cell,side,len(run)))
    require(all(type(s.get('span',1)) is int and s.get('span',1) in (1,2)
                for s in t['doors']), t['id']+': invalid end-cap span')
    actual = [(tuple(s['cell']),s['side'],s.get('span',1)) for s in t['doors']]
    require(len(actual)==len(set(actual)) and set(actual)==caps and len(caps)>=2,
            t['id']+': hallway sockets must centre every end cap, never a long side')


def check_piece_support(t, rows, points, wall_meshes):
    """Check imported bottoms/back vertices, never placement origins alone.

    Floor fabric defines the zero datum (its top, not its buried underside).
    Even ceiling-height fixtures need floor-cell overlap or actual
    contact between back vertices and a horizontal wall-face normal.
    """

    floors, walls = [], []
    for p in t['pieces']:
        pid = p['id']
        if rows[pid]['kind']=='floor':
            lo,hi = bounds([transform(v,p) for v in points[pid]])
            if abs(hi[1])<=.05:
                floors.append((lo,hi))
        if pid in wall_meshes:
            verts,faces = wall_meshes[pid]
            walls.append((p,tree_for([transform(v,p) for v in verts],faces)))
    cells = {tuple(c) for c in t['footprint']}
    for p in t['pieces']:
        pid = p['id']; kind = rows[pid]['kind']
        world = [transform(v,p) for v in points[pid]]
        lo,hi = bounds(world)
        if kind=='ceiling':
            continue  # The separate datum regression checks every ceiling.

        if kind=='floor' and pid!='stair_stone_2m':
            require(abs(hi[1])<=.05,
                    t['id']+': floor datum '+pid)
            continue
        ground = abs(lo[1])<=.05 and any(
            max(lo[0],a[0])<=min(hi[0],b[0])+1e-5 and
            max(lo[2],a[2])<=min(hi[2],b[2])+1e-5 and
            any(2*x<=b[0] and a[0]<=2*x+2 and 2*z<=b[2] and a[2]<=2*z+2 for x,z in cells)
            for a,b in floors)
        if ground:
            continue
        back = max(v[2] for v in points[pid])
        a = math.radians(p['rotY'])
        outward = Vector((math.sin(a),0,math.cos(a)))
        contacts = []
        for v in points[pid]:
            if back-v[2]>.005:
                continue
            vertex = Vector(transform(v,p))
            for wall,tree in walls:
                if wall is p:
                    continue
                hit,normal,_,distance = tree.find_nearest(vertex)
                if hit is not None and distance<=.05+1e-5 and abs(normal.y)<.1 and normal.dot(outward)<-.8:
                    contacts.append(vertex)
        require(len(contacts)>=2, f'{t["id"]}: unsupported piece {pid} at {p["pos"]}: bottom={lo[1]:.4f}; no wall-back contact')


def validate_room(t, meshes):
    name = t['id']
    require(re.fullmatch(r'castle_[a-z_]+', name), name+': id')
    require(t['kind'] in ('room','hallway','junction'), name+': kind')
    require(t['shape'] in ('rect','L','T','round','irregular'), name+': shape')
    require(t['height'] == 7 and t['weight'] > 0, name+': height/weight')
    require(t['gimmick'] in ('none','puzzle','freeze','traversal'), name+': gimmick')
    require(t['minRound'] >= (1 if t['gimmick'] == 'none' else 3), name+': round gate')
    raw = t['footprint']
    require(raw and all(len(c) == 2 and all(type(v) is int and v >= 0 for v in c) for c in raw), name+': grid')
    cells = {tuple(c) for c in raw}
    require(len(cells) == len(raw) and connected(cells), name+': 4-connectivity/duplicates')
    count = len(cells)
    size = 'closet' if count <= 4 else 'small' if count <= 9 else 'medium' if count <= 20 else 'large' if count <= 40 else 'hall'
    require(t['sizeClass'] == size, name+': area class')
    if t['shape'] == 'rect':
        require(len(cells) == (max(x for x,z in cells)+1)*(max(z for x,z in cells)+1), name+': rectangle')
    if t['kind'] == 'hallway':
        # No 3x3 occupied block: no corridor cross-section can balloon to 3 cells.
        require(not any(all((x+a,z+b) in cells for a in range(3) for b in range(3)) for x,z in cells), name+': hallway width')
    if t['shape'] == 'round':
        require(any(p['id'].startswith('wall_arc_r') for p in t['pieces']), name+': missing round walls')
    edges = boundary_edges(cells)
    sockets = t['doors']
    check_hallway_endcaps(t)
    require(len(sockets) >= (1 if size == 'closet' else 2), name+': socket count')
    require(len({(tuple(s['cell']),s['side']) for s in sockets}) == len(sockets), name+': duplicate socket')
    for s in sockets:
        require(any(tuple(s['cell']) == c and s['side'] == side for c,side,_,_ in edges), name+': non-boundary door')
        require(s.get('closedWith'), name+': missing closedWith')
    anchors = t['anchors']
    require(set(anchors) == {'cake','goldenCake','light','hunterSpawn'}, name+': anchor schema')
    require(len(anchors['cake']) >= max(2, math.ceil(count/6)), name+': cake scaling')
    require(len(anchors['goldenCake']) <= 1 and anchors['light'], name+': golden/light count')
    require(size not in ('medium','large','hall') or anchors['hunterSpawn'], name+': hunter spawn')
    for kind, values in anchors.items():
        for p in values:
            require(len(p) == 3 and all(math.isfinite(v) for v in p) and 0 <= p[1] < 7, name+': anchor coordinates')
            x,y,z = p
            require((math.floor(x/2),math.floor(z/2)) in cells, name+': anchor outside '+kind)
            require(min(segment_distance(x,z,c,tan) for _,_,c,tan in edges) >= .6-1e-5, name+': anchor wall clearance '+kind)
    for p in t['pieces'] + [p for s in sockets for p in s['closedWith']]:
        require(p['id'] in meshes, name+': unknown piece '+p['id'])
        require(len(p['pos']) == 3 and all(math.isfinite(v) for v in p['pos']) and math.isfinite(p['rotY']), name+': placement coordinates')
        if meshes[p['id']]['row']['kind'] in ('wall','door','window'):
            require(p['rotY'] in (0,90,180,270), name+': wall rotation')
    walls = [p for p in t['pieces'] if meshes[p['id']]['row']['kind'] in ('wall','door','window','arc')]
    solids, vertices, faces = [], [], []
    for p in walls:
        pts = [transform(v,p) for v in meshes[p['id']]['points']]
        lo,hi = bounds(pts)
        solids.append((lo,hi,p['id']))
        start = len(vertices)
        vertices.extend(pts)
        faces.extend(tuple(start+i for i in f) for f in meshes[p['id']]['faces'])
    if t['shape']=='round':
        check_curved_wall_solids(t,meshes)
    else:
        for i,(lo,hi,pid) in enumerate(solids):
            for lo2,hi2,pid2 in solids[i+1:]:
                require(not all(min(hi[k],hi2[k])-max(lo[k],lo2[k]) > 1e-5 for k in range(3)),
                        f'{name}: overlapping walls {pid}/{pid2} at {lo}/{lo2}')
    tree = tree_for(vertices,faces)
    for values in anchors.values():
        for x,y,z in values:
            nearest = tree.find_nearest(Vector((x,max(.1,y),z)))
            require(nearest[0] is not None and nearest[3] >= .6-1e-5,
                    name+': anchor too close to actual wall mesh')
    # Sample imported triangles, not a self-declared edge coverage list. All
    # boundary edges are checked every 5cm at four heights below window sills.
    # Open sockets are full 3.2m apertures, including their neighbour half-edges.
    if t['shape']=='round':
        check_curved_enclosure(t,tree)
    for cell,side,center,tangent in ([] if t['shape']=='round' else edges):
        dx,dz = SIDES[side]
        for i in range(40):
            u = -.975+i*.05
            x,z = center[0]+u*tangent[0],center[1]+u*tangent[1]
            is_door = False
            for socket in sockets:
                if socket['side'] != side:
                    continue
                sc = socket_center(socket)
                if abs((x-sc[0])*dx+(z-sc[1])*dz) < .001 and abs((x-sc[0])*tangent[0]+(z-sc[1])*tangent[1]) < 1.6-.001:
                    is_door = True
            for height in (.1,.5,1.0,2.79,4.75,6.95):
                # Arrow slits begin at 2m; require closure below their sill,
                # still test door clearance all the way to 2.8m.
                if not is_door and 1.1 < height < 4.75:
                    continue
                hit = tree.ray_cast(Vector((x-dx*.08,height,z-dz*.08)), Vector((dx,0,dz)), 1.0)[0]
                expect_open = is_door and height < 2.8
                require((hit is None) == expect_open, f'{name}: enclosure/door error {side} {(x,height,z)} door={expect_open}')
    for socket in sockets:
        rows = socket['closedWith']
        verts, polys = [], []
        for p in rows:
            start = len(verts)
            verts.extend(transform(v,p) for v in meshes[p['id']]['points'])
            polys.extend(tuple(start+i for i in f) for f in meshes[p['id']]['faces'])
        closed = tree_for(verts,polys)
        dx,dz = SIDES[socket['side']]
        center = socket_center(socket)
        for u in (-1.59,0,1.59):
            px,pz = center[0]+u*abs(dz),center[1]+u*abs(dx)
            require(closed.ray_cast(Vector((px-dx*.08,1,pz-dz*.08)),Vector((dx,0,dz)),1)[0] is not None, name+': closedWith gap')
    # Actual floor support under every gameplay anchor; traversal voids cannot
    # silently contain cakes/spawns. Lights have no ground requirement.
    floor_points, floor_faces = [], []
    for p in t['pieces']:
        if meshes[p['id']]['row']['kind'] != 'floor':
            continue
        start = len(floor_points)
        floor_points.extend(transform(v,p) for v in meshes[p['id']]['points'])
        floor_faces.extend(tuple(start+i for i in f) for f in meshes[p['id']]['faces'])
    floors = tree_for(floor_points,floor_faces)
    for key in ('cake','goldenCake','hunterSpawn'):
        for x,y,z in anchors[key]:
            require(floors.ray_cast(Vector((x,y+.4,z)),Vector((0,-1,0)),.8)[0] is not None, name+': unsupported '+key)
    return {'id': name, 'cells': count, 'pieces': len(t['pieces']), 'doors': len(sockets)}


def check_curved_wall_solids(t, meshes):
    """Slice imported triangles into solid contours, not arc bounding boxes.

    Tessellating each closed contour preserves concave transition piers and
    separate jambs. Open relief contours use a conservative hull.
    """
    sys.path.insert(0,str(Path(__file__).resolve().parent))
    from validate_env_theme_basement import convex_hull, intersection_area
    from mathutils.geometry import tessellate_polygon
    for height in (.1,.5,1.0,2.79,4.75,6.95):
        sections=[]
        for p in t['pieces']:
            mesh=meshes[p['id']]
            if mesh['row']['kind'] not in ('wall','door','window','arc'):
                continue
            pts=[transform(v,p) for v in mesh['points']]
            adjacency={}
            for face in mesh['faces']:
                tri=[pts[i] for i in face]; cuts=[]
                for a,b in zip(tri,tri[1:]+tri[:1]):
                    if min(a[1],b[1])<height<max(a[1],b[1]):
                        u=(height-a[1])/(b[1]-a[1])
                        cuts.append((round(a[0]+u*(b[0]-a[0]),5),round(a[2]+u*(b[2]-a[2]),5)))
                if len(cuts)==2 and cuts[0]!=cuts[1]:
                    a,b=cuts
                    adjacency.setdefault(a,set()).add(b); adjacency.setdefault(b,set()).add(a)
            unseen=set(adjacency)
            while unseen:
                pending=[next(iter(unseen))]; seen=set()
                while pending:
                    a=pending.pop()
                    if a in seen: continue
                    seen.add(a); pending.extend(adjacency[a]-seen)
                unseen-=seen
                contour=convex_hull(seen)
                if all(len(adjacency[v])==2 for v in seen):
                    start=min(seen); contour=[start]; previous=None; current=start
                    while True:
                        nxt=next(v for v in sorted(adjacency[current]) if v!=previous)
                        if nxt==start: break
                        contour.append(nxt); previous,current=current,nxt
                if len(contour)<3: continue
                triangles=tessellate_polygon([[Vector((x,z,0)) for x,z in contour]])
                for tri in triangles:
                    hull=convex_hull([contour[i] for i in tri])
                    for other,owner in sections:
                        if owner is not p:
                            require(intersection_area(hull,other)<2e-5,
                                    f'{t["id"]}: curved wall solid overlap {p["id"]}/{owner["id"]} at {height}')
                    sections.append((hull,p))


def check_curved_enclosure(t, tree):
    """Independent r4 design contour; every 5cm below/above the apertures.

    Raster occupancy is independently reconstructed from cell centres. It is
    deliberately not used as a staircase wall contour: arcs enclose the disk.
    """
    apse=t['id']=='castle_chapel_apse'
    require(apse or t['id']=='castle_tower_room','unknown curved template design')
    cz=8 if apse else 4
    expected={(x,z) for x in range(4) for z in range(6 if apse else 4)
              if (apse and z<4) or (2*x+1-4)**2+(2*z+1-cz)**2<=16}
    require({tuple(c) for c in t['footprint']}==expected,t['id']+': incorrect disk raster')
    if apse:
        contour=[(.4,0),(7.6,0),(7.6,8)]+[(4+3.6*math.sin(math.radians(a)),8+3.6*math.cos(math.radians(a)))
                  for a in [90-i*7.5 for i in range(1,25)]]
    else:
        contour=[(2,8),(6,8)]+[(4+3.6*math.sin(math.radians(a)),4+3.6*math.cos(math.radians(a)))
                  for a in [30+i*7.5 for i in range(17)]]+[(6,0),(2,0)]+[
                  (4+3.6*math.sin(math.radians(a)),4+3.6*math.cos(math.radians(a)))
                  for a in [210+i*7.5 for i in range(17)]]
    signed=sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(contour,contour[1:]+contour[:1]))
    for a,b in zip(contour,contour[1:]+contour[:1]):
        length=math.dist(a,b)
        dx,dz=(b[1]-a[1])/length,-(b[0]-a[0])/length
        if signed<0: dx,dz=-dx,-dz
        for i in range(math.ceil(length/.05)):
            u=(i+.5)/math.ceil(length/.05)
            x,z=a[0]+u*(b[0]-a[0]),a[1]+u*(b[1]-a[1])
            portal=False
            for socket in t['doors']:
                sx,sz=socket_center(socket); nx,nz=SIDES[socket['side']]
                if dx*nx+dz*nz>.99 and abs((x-sx)*nx+(z-sz)*nz)<.41 and abs((x-sx)*abs(nz)+(z-sz)*abs(nx))<1.599:
                    portal=True
            for height in (.1,.5,1.0,2.79,4.75,6.95):
                hit=tree.ray_cast(Vector((x-.08*dx,height,z-.08*dz)),Vector((dx,0,dz)),1.0)[0]
                require((hit is None)==(portal and height<2.8),
                        f'{t["id"]}: curved enclosure/door gap at {(x,height,z)}')


def check_sources(rows, rooms):
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Kit/CastleKit.blend'))
    require(bpy.context.scene.unit_settings.scale_length == 1, 'source units')
    for row in rows:
        obj = bpy.data.objects.get('Castle_'+row['id'])
        require(obj is not None and obj.type == 'MESH', row['id']+': missing source')
        require(close(obj.location,(0,0,0)) and close(obj.rotation_euler,(0,0,0)) and close(obj.scale,(1,1,1)), row['id']+': source transform')
        require(mesh_hash(obj,True) == row['geometrySha256'], row['id']+': source/export mismatch')
    images = [im for im in bpy.data.images if im.type == 'IMAGE']
    require(len(images) <= 6, 'texture budget')
    for im in images:
        require(im.packed_file and im.filepath.startswith('//') and max(im.size) <= 1024, im.name+': portability/budget')
        source_path = SOURCE/'Kit'/(im.name+'.png')
        export_path = ART/'Kit'/(im.name+'.png')
        require(source_path.read_bytes() == export_path.read_bytes() == bytes(im.packed_file.data), im.name+': texture drift')
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Rooms/CastleRooms.blend'))
    dark = bpy.data.scenes.get('Review_Darkness_CastleChapel')
    require(dark is not None and dark.world.node_tree.nodes['Background'].inputs[1].default_value == 0,
            'darkness source: missing/ambient light')
    active_lights = [o for o in dark.objects if o.type == 'LIGHT' and not o.hide_render]
    require(sum(o.data.type == 'SPOT' for o in active_lights) == 1 and
            all(o.data.type in ('SPOT','POINT') for o in active_lights), 'darkness source: non-theme studio light')
    require(all(not o.hide_render for o in dark.objects if 'placementIndex' in o), 'darkness source: cutaway remains')
    for t in rooms:
        scene = bpy.data.scenes.get(t['id'])
        require(scene is not None, t['id']+': missing source scene')
        instances = [o for o in scene.objects if 'placementIndex' in o]
        require(len(instances) == len(t['pieces']), t['id']+': source placement count')
        for obj in instances:
            p = t['pieces'][obj['placementIndex']]
            x,y,z = p['pos']
            require(close(obj.location,(x,-z,y)) and abs(obj.rotation_euler.z-math.radians(p['rotY'])) < 1e-5,
                    t['id']+': source placement drift')
            row = next(r for r in rows if r['id'] == p['id'])
            require(mesh_hash(obj,True) == row['geometrySha256'], t['id']+': source mesh drift')


def check_previews(rooms):
    import numpy as np
    names = ['kit-sheet','in-darkness'] + [t['id']+'-three-quarter' for t in rooms]
    for name in names:
        path = OUT/(name+'.png')
        require(path.is_file(), 'missing preview '+name)
        image = bpy.data.images.load(str(path), check_existing=False)
        require(tuple(image.size) == ((2000,1500) if name == 'kit-sheet' else (1200,900)), name+': resolution')
        pixels = np.empty(len(image.pixels),dtype=np.float32)
        image.pixels.foreach_get(pixels)
        rgb = pixels.reshape(-1,4)[:,:3]
        require(float(rgb.std()) > .025 and float((rgb.max(axis=1) > .08).mean()) > .08, name+': blank/unreadable')
        bpy.data.images.remove(image)
    return len(names)


def negative_controls(rooms, meshes):
    controls = []
    base = rooms[1]
    def reject(label, mutate):
        t = copy.deepcopy(base)
        mutate(t)
        try:
            validate_room(t,meshes)
        except AssertionError:
            controls.append(label)
        else:
            raise AssertionError('negative control accepted: '+label)
    reject('disconnected footprint', lambda t: t['footprint'].__setitem__(0,[90,90]))
    reject('interior door', lambda t: t['doors'][0].update(cell=[1,1]))
    reject('outside anchor', lambda t: t['anchors']['cake'].__setitem__(0,[-1,0,0]))
    reject('wall clearance', lambda t: t['anchors']['cake'].__setitem__(0,[.1,0,1]))
    reject('missing piece', lambda t: t['pieces'][0].update(id='not_in_kit'))
    reject('overlap', lambda t: t['pieces'].append(copy.deepcopy(t['pieces'][0])))
    reject('wall gap', lambda t: t['pieces'].pop(next(i for i,p in enumerate(t['pieces']) if p['id']=='wall_2m')))
    reject('early gimmick', lambda t: t.update(gimmick='freeze',minRound=1))
    reject('missing closedWith', lambda t: t['doors'][0].pop('closedWith'))
    require(not close((2.02,7,.8),(2,7,.8),.01), 'dimension negative control')
    controls.append('2cm dimension drift')
    a = [(1,0,-.4),(1,7,-.4),(1,0,.4),(1,7,.4)]
    require(not same_points(a,[(x,y,z+.002) for x,y,z in a]), 'seam negative control')
    controls.append('2mm seam drift')
    for ident in ('castle_tower_room','castle_chapel_apse'):
        base = next(t for t in rooms if t['id']==ident)
        reject(ident+' missing arc', lambda t: t['pieces'].pop(next(
            i for i,p in enumerate(t['pieces']) if p['id']=='wall_arc_r4')))
        reject(ident+' overlapping arc', lambda t: t['pieces'].append(copy.deepcopy(next(
            p for p in t['pieces'] if p['id']=='wall_arc_r4'))))
        reject(ident+' incorrect raster', lambda t: t['footprint'].append([0,5]))
    return controls


def door_projection(points, triangles, rectangle, material, glazing=None):
    """Rasterize BODY triangles, not hardware that could conceal a broken panel.

    Pixel centres are at most 2.5mm apart in both axes. The rectangle is an
    independent authored leaf envelope; shrinking a defective mesh cannot pass.
    An optional explicit glazing rectangle is the only permitted exemption.
    """
    import numpy as np
    left, right, bottom, top = rectangle
    nx, ny = math.ceil((right-left)/.0025), math.ceil((top-bottom)/.0025)
    xs = left+(np.arange(nx)+.5)*(right-left)/nx
    ys = bottom+(np.arange(ny)+.5)*(top-bottom)/ny
    covered = np.zeros((ny, nx), dtype=bool)
    if glazing:
        x,y = np.meshgrid(xs,ys)
        covered |= (x >= glazing[0]) & (x <= glazing[1]) & (y >= glazing[2]) & (y <= glazing[3])
    for ids, surface in triangles:
        if surface != material:
            continue
        a, b, c = [points[i] for i in ids]
        area = (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
        if abs(area) < 1e-10:
            continue
        ix = np.flatnonzero((xs >= min(a[0], b[0], c[0])-1e-6) & (xs <= max(a[0], b[0], c[0])+1e-6))
        iy = np.flatnonzero((ys >= min(a[1], b[1], c[1])-1e-6) & (ys <= max(a[1], b[1], c[1])+1e-6))
        if not len(ix) or not len(iy):
            continue
        x, y = np.meshgrid(xs[ix], ys[iy])
        u = ((b[0]-x)*(c[1]-y)-(b[1]-y)*(c[0]-x))/area
        v = ((c[0]-x)*(a[1]-y)-(c[1]-y)*(a[0]-x))/area
        w = 1-u-v
        covered[np.ix_(iy, ix)] |= (u >= -1e-6) & (v >= -1e-6) & (w >= -1e-6)
    require(bool(covered.all()), f'door panel silhouette: {int((~covered).sum())} uncovered 2.5mm samples')


def door_face_details(points, triangles, body, hardware, glass=None):
    body_ids = {i for ids, mat in triangles if mat == body for i in ids}
    require(body_ids, 'door panel material absent')
    lo, hi = bounds([points[i] for i in body_ids])
    for sign in (-1, 1):
        face = lo[2] if sign < 0 else hi[2]
        for label, surface, y0, y1, minimum in (
                ('handle/plate', hardware, 1.0, 1.35, .002),
                ('kick plate', hardware, .08, .65, .04),
                *([('glazing', glass, 1.7, 2.4, .02)] if glass else [])):
            area = 0
            for ids, mat in triangles:
                if mat != surface:
                    continue
                a, b, c = [points[i] for i in ids]
                cy = (a[1]+b[1]+c[1])/3
                if label == 'handle/plate' and abs((a[0]+b[0]+c[0])/3) < .33:
                    continue
                plane = 0 if label == 'glazing' else face
                if not y0 <= cy <= y1 or min(sign*(p[2]-plane) for p in (a,b,c)) < -1e-5:
                    continue
                area += abs((b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]))/2
            require(area >= minimum, f'door face {sign}: missing {label}')


def check_door_quality(theme, output):
    """Independent FBX checks; called by every theme validator before its gates."""
    specs = {
        'castle': ('door_iron_strapped', 'wood', 'metal', None, 3.1, 2.74,
                   {'stone','stone_dark','mortar'}),
        'hospital': ('door_double_porthole_4m', 'door_enamel', 'stainless', 'glass', 1.56, 2.78,
                     {'tile','paint','grout','acoustic'}),
        'school': ('prop_classroom_door_leaf', 'door_laminate', 'steel', 'glass', 1.56, 2.68,
                   {'mustard','teal','cream','mortar_upper','mortar_lower'}),
        'basement': ('prop_bulkhead_leaf', 'door_steel', 'galvanised', None, 1.53, 2.74,
                     {'concrete','damp','insulation'}),
    }
    piece, body, hardware, glass, width, height, forbidden = specs[theme]
    prefix = theme+'_'
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = ROOT/'Assets/Art/Environment'/theme.title()/'Kit'/(theme.title()+'_'+piece+'.fbx')
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    obj, = bpy.context.scene.objects
    points = [tuple(v.co) for v in obj.data.vertices]
    triangles = [(tuple(p.vertices), obj.data.materials[p.material_index].name) for p in obj.data.polygons]
    require(not {prefix+s for s in forbidden} & {mat for _,mat in triangles}, piece+': wall surface on door')
    panels = []
    for sign in ((-1,1) if theme == 'hospital' else (0,)):
        if sign:
            # Undo the delivered 95-degree swing around the measured wall-plane
            # hinge. Test each physical leaf separately, never their combined AABB.
            yaw = math.radians(sign*95)
            local = [(math.cos(yaw)*(x-sign*1.6)+math.sin(yaw)*z+sign*.8,
                      y, -math.sin(yaw)*(x-sign*1.6)+math.cos(yaw)*z) for x,y,z in points]
            faces = [(ids,mat) for ids,mat in triangles if all(points[i][0]*sign > 0 for i in ids)]
        else:
            local, faces = points, triangles
        glazing = (-.28*1.56/1.4,-.02*1.56/1.4,1.14,2.34) if theme == 'school' else None
        door_projection(local, faces, (-width/2,width/2,0,height), prefix+body, glazing)
        door_face_details(local, faces, prefix+body, prefix+hardware, prefix+glass if glass else None)
        panels.append({'leaf':sign, 'rasterPitchMetres':.0025, 'rectangle':[width,height]})
    # Remove all hardware on one face; the remaining face cannot satisfy it.
    controls = []
    for sign in (-1,1):
        damaged = [(ids,mat) for ids,mat in faces if mat != prefix+hardware or
                   sum(local[i][2] for i in ids)*sign <= 0]
        try:
            door_face_details(local, damaged, prefix+body, prefix+hardware, prefix+glass if glass else None)
        except AssertionError:
            controls.append(f'missing hardware face {sign}')
        else:
            raise AssertionError('door hardware negative control accepted')
    # Genuine geometry counterexamples: even a 5mm through-gap must fail.
    for gap in (.005,.01,.08):
        vertices = [(-.5,0,0),(-gap/2,0,0),(-gap/2,1,0),(-.5,1,0),
                    (gap/2,0,0),(.5,0,0),(.5,1,0),(gap/2,1,0)]
        faces = [((0,1,2),'body'),((0,2,3),'body'),((4,5,6),'body'),((4,6,7),'body')]
        try:
            door_projection(vertices, faces, (-.5,.5,0,1), 'body')
        except AssertionError:
            controls.append(f'{gap}m panel gap')
        else:
            raise AssertionError('door gap negative control accepted')

    output.mkdir(parents=True, exist_ok=True)
    report = {'passed':True, 'piece':piece, 'panels':panels, 'negativeControls':controls,
              'fbxSha256':sha(path), 'scope':'Door geometry only; not room spatial acceptance.'}
    (output/'door-quality.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(f'PASS {theme} door quality: {len(panels)} continuous panels at 2.5mm; both faces detailed; no wall surfaces; {len(controls)} rejection controls')


def check_door_previews(theme, output):
    """Hash-bound front and back evidence, including frames as well as leaves."""
    import numpy as np
    art = ROOT/'Assets/Art/Environment'/theme.title()
    kit = json.loads((art/'Kit'/(theme.title()+'Kit.manifest.json')).read_text())
    count = 0
    for row in kit['pieces']:
        if row['kind'] != 'door' and row['id'] not in ('door_iron_strapped','prop_classroom_door_leaf','prop_bulkhead_leaf'):
            continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(art/'Kit'/row['file']),use_anim=False)
        obj, = bpy.context.scene.objects
        obj.data.calc_loop_triangles()
        geometry = [(obj.data.materials[t.material_index].name,
                     sorted(tuple(round(c,4)+0.0 for c in obj.data.vertices[i].co) for i in t.vertices))
                    for t in obj.data.loop_triangles]
        digest = hashlib.sha256(json.dumps(sorted(geometry),separators=(',',':')).encode()).hexdigest()
        for side in ('front','back'):
            path = output/'doors'/(row['id']+'-'+side+'.png')
            require(path.is_file() and path.with_suffix('.json').is_file(), 'missing door preview '+str(path))
            receipt = json.loads(path.with_suffix('.json').read_text())
            require(receipt['face'] == side and receipt['piece'] == row['id'] and
                    receipt['geometrySha256'] == digest and receipt['imageSha256'] == sha(path),
                    'stale door preview '+str(path))
            image = bpy.data.images.load(str(path),check_existing=False)
            require(tuple(image.size) == (1200,1200), 'door preview size')
            pixels = np.empty(len(image.pixels),dtype=np.float32)
            image.pixels.foreach_get(pixels)
            require(float(pixels.reshape(-1,4)[:,:3].std()) > .025, 'blank door preview')
            bpy.data.images.remove(image)
            count += 1
    print(f'PASS {theme} door previews: {count} current front/back close-ups')
    return count


def check_ceiling_and_leaf_placement(t, rows, points):
    """Measured seating datum: Castle/Basement top; ward/school soffit bottom."""
    theme = t['id'].split('_')[0]
    leaf_ids = {'door_iron_strapped','door_double_porthole_4m','prop_classroom_door_leaf','prop_bulkhead_leaf'}
    for p in t['pieces']:
        pid = p['id']
        if rows[pid]['kind'] == 'ceiling':
            axis = [v[1] for v in points[pid]]
            datum = max(axis) if theme in ('castle','basement') else min(axis)
            require(abs(p['pos'][1]+datum-t['height']) <= .05,
                    t['id']+': ceiling seating datum '+pid)
        if pid not in leaf_ids:
            continue
        candidates = []
        for door in t['doors']:
            x,z = door['cell']; side = door['side']
            dx,dz = SIDES[side]
            yaw = {'N':0,'E':90,'S':180,'W':270}[side]
            cx,cz = socket_center(door)
            if theme == 'castle':
                cx,cz = cx+.4*dx,cz+.4*dz
            offsets = (-.8,.8) if pid in ('prop_classroom_door_leaf','prop_bulkhead_leaf') else (0,)
            for u in offsets:
                position = (cx+u*abs(dz),0,cz+u*abs(dx))
                if close(p['pos'],position,.05) and p['rotY'] == yaw:
                    candidates.append(position)
        require(len(candidates) == 1,t['id']+': detached door leaf '+pid)


def check_prop_footprints(t, rows, points):
    """Conservative full-bounds containment, including holes/re-entrant corners.

    A prop's origin being inside is insufficient. Requiring its complete XZ
    bounding box inside the occupied-cell union also covers every triangle.
    Socket-attached leaves deliberately straddle the boundary and have their
    own placement check. This does not replace actual prop/wall collision tests.
    """
    cells = {tuple(c) for c in t['footprint']}
    leaves = {'door_iron_strapped','door_double_porthole_4m','prop_classroom_door_leaf','prop_bulkhead_leaf'}
    for p in t['pieces']:
        if rows[p['id']]['kind'] not in ('prop','pipe','duct') or p['id'] in leaves:
            continue
        lo, hi = bounds([transform(v,p) for v in points[p['id']]])
        for x in range(math.floor((lo[0]+1e-5)/2), math.ceil((hi[0]-1e-5)/2)):
            for z in range(math.floor((lo[2]+1e-5)/2), math.ceil((hi[2]-1e-5)/2)):
                require((x,z) in cells, f'{t["id"]}: prop footprint {p["id"]} at {p["pos"]} crosses unoccupied cell {(x,z)}')


def triangle_inside_box(triangle, lo, hi):
    """Separating-axis test for positive penetration; mere contact is allowed."""
    center = Vector(tuple((a+b)/2 for a,b in zip(lo,hi)))
    half = [(b-a)/2 for a,b in zip(lo,hi)]
    verts = [Vector(v)-center for v in triangle]
    edges = [verts[1]-verts[0],verts[2]-verts[1],verts[0]-verts[2]]
    basis = [Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1))]
    axes = basis+[edges[0].cross(edges[1])]+[edge.cross(axis) for edge in edges for axis in basis]
    for axis in axes:
        if axis.length < 1e-10:
            continue
        axis.normalize()
        radius = sum(half[i]*abs(axis[i]) for i in range(3))
        projection = [v.dot(axis) for v in verts]
        if min(projection) >= radius-1e-5 or max(projection) <= -radius+1e-5:
            return False
    return True


def load_wall_triangles(theme, rows):
    walls = {}
    for pid,row in rows.items():
        if row['kind'] not in ('wall','window','door','arc') or pid=='door_double_porthole_4m':
            continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path = ROOT/'Assets/Art/Environment'/theme.title()/'Kit'/row['file']
        bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
        obj, = bpy.context.scene.objects
        walls[pid] = ([tuple(v.co) for v in obj.data.vertices], [tuple(f.vertices) for f in obj.data.polygons])
    return walls


def check_prop_wall_bounds(t, rows, points, wall_meshes):
    """Conservative prop envelope versus actual wall triangles/closed interiors.

    Using actual wall surfaces avoids false clashes against window apertures or
    a handrail's height-independent bounding box. A hollow prop's envelope may
    still reject a valid interlock; such placement needs mesh-level review.
    """
    leaves = {'door_iron_strapped','door_double_porthole_4m','prop_classroom_door_leaf','prop_bulkhead_leaf'}
    walls, props = [], []
    for p in t['pieces']:
        if p['id'] in leaves:
            continue
        kind = rows[p['id']]['kind']
        if kind not in ('wall','window','door','arc','prop','pipe','duct'):
            continue
        if kind in ('wall','window','door','arc'):
            pts,faces = wall_meshes[p['id']]
            pts = [transform(v,p) for v in pts]
            walls.append((*bounds(pts),p,pts,faces,tree_for(pts,faces)))
        else:
            props.append((*bounds([transform(v,p) for v in points[p['id']]]),p))
    for lo,hi,p in props:
        for low,high,wall,vertices,faces,tree in walls:
            if not all(min(hi[a],high[a])-max(lo[a],low[a]) > 1e-5 for a in range(3)):
                continue
            center = Vector(tuple((a+b)/2 for a,b in zip(lo,hi)))
            near,normal,_,distance = tree.find_nearest(center)
            inside = distance > 1e-5 and (center-near).dot(normal) < -1e-5
            contact = any(triangle_inside_box([vertices[i] for i in face],lo,hi) for face in faces)
            require(not inside and not contact,
                    f'{t["id"]}: prop/wall penetration {p["id"]} at {p["pos"]} / {wall["id"]} at {wall["pos"]}')


def placement_regressions(templates, rows, points):
    wall_meshes = load_wall_triangles(templates[0]['id'].split('_')[0],rows)
    for t in templates:
        check_hallway_endcaps(t)
        check_ceiling_and_leaf_placement(t,rows,points)
        check_prop_footprints(t,rows,points)
        check_prop_wall_bounds(t,rows,points,wall_meshes)
        check_piece_support(t,rows,points,wall_meshes)
    # Call the precise new gates, so an unrelated schema failure cannot make
    # these negative controls look successful.
    corridor = next(t for t in templates if t['kind']=='hallway')
    for label,mutate in (
            ('long-side socket',lambda d:d['doors'][0].update(side='W' if d['doors'][0]['side'] in 'NS' else 'S')),
            ('off-centre span',lambda d:d['doors'][0].pop('span')),
            ('missing arm',lambda d:d['doors'].pop())):
        damaged = copy.deepcopy(corridor); mutate(damaged)
        try:
            check_hallway_endcaps(damaged)
        except AssertionError:
            pass
        else:
            raise AssertionError('end-cap control accepted: '+label)
    supported = next((t,i) for t in templates for i,p in enumerate(t['pieces'])
                     if rows[p['id']]['kind']=='prop' and abs(p['pos'][1])<.01
                     and not 'leaf' in p['id'] and not p['id'].startswith('door_'))
    damaged = copy.deepcopy(supported[0])
    damaged['pieces'][supported[1]]['pos'][1] += .15
    try:
        check_piece_support(damaged,rows,points,wall_meshes)
    except AssertionError:
        pass
    else:
        raise AssertionError('floating-piece control accepted')
    mounted = next((t,i) for t in templates for i,p in enumerate(t['pieces'])
                   if rows[p['id']]['kind']=='prop' and p['pos'][1]>.1)
    damaged = copy.deepcopy(mounted[0])
    p = damaged['pieces'][mounted[1]]
    yaw = math.radians(p['rotY'])
    p['pos'][0] -= .15*math.sin(yaw)
    p['pos'][2] -= .15*math.cos(yaw)
    try:
        check_piece_support(damaged,rows,points,wall_meshes)
    except AssertionError:
        pass
    else:
        raise AssertionError('detached-wall-mount control accepted')
    damaged = copy.deepcopy(supported[0])
    damaged['pieces'] = [p for p in damaged['pieces'] if rows[p['id']]['kind']!='floor']
    try:
        check_piece_support(damaged,rows,points,wall_meshes)
    except AssertionError:
        pass
    else:
        raise AssertionError('missing-floor-support control accepted')
    print('PASS end-cap/support gates: every arm centred; measured floor/back/ceiling support; long-side, span, missing-arm, floating-piece, detached-wall-mount and missing-floor controls rejected')
    base = templates[0]
    damaged = copy.deepcopy(base)
    next(p for p in damaged['pieces'] if rows[p['id']]['kind'] == 'ceiling')['pos'][1] -= .10
    try:
        check_ceiling_and_leaf_placement(damaged,rows,points)
    except AssertionError:
        pass
    else:
        raise AssertionError('ceiling 10cm drift control accepted')
    leaf = next(pid for pid in ('door_iron_strapped','door_double_porthole_4m','prop_classroom_door_leaf','prop_bulkhead_leaf') if pid in rows)
    damaged = copy.deepcopy(base)
    damaged['pieces'].append({'id':leaf,'pos':[99,0,99],'rotY':0})
    try:
        check_ceiling_and_leaf_placement(damaged,rows,points)
    except AssertionError:
        pass
    else:
        raise AssertionError('orphan leaf control accepted')
    prop = next(pid for pid,r in rows.items() if r['kind'] == 'prop' and pid != leaf)
    damaged = copy.deepcopy(base)
    damaged['pieces'].append({'id':prop,'pos':[99,0,99],'rotY':37})
    try:
        check_prop_footprints(damaged,rows,points)
    except AssertionError:
        pass
    else:
        raise AssertionError('outside rotated prop control accepted')
    # A long prop may have both ends in occupied cells while crossing a hole.
    damaged = {'id':'containment_control', 'footprint':[[0,0],[2,0]],
               'pieces':[{'id':'probe','pos':[0,0,0],'rotY':0}]}
    try:
        check_prop_footprints(damaged, {'probe':{'kind':'prop'}},
                              {'probe':[(.5,0,.5),(5.5,1,1.5)]})
    except AssertionError:
        pass
    else:
        raise AssertionError('prop spanning footprint hole control accepted')
    damaged = copy.deepcopy(base)
    wall = next(p for p in damaged['pieces'] if rows[p['id']]['kind']=='wall')
    damaged['pieces'].append({'id':prop,'pos':list(wall['pos']),'rotY':wall['rotY']})
    try:
        check_prop_wall_bounds(damaged,rows,points,wall_meshes)
    except AssertionError:
        pass
    else:
        raise AssertionError('prop embedded in wall control accepted')
    print(f'PASS {base["id"].split("_")[0]} measured ceilings, socket leaves, full prop containment and conservative wall clearance; drift/orphan/outside/hole/collision controls rejected')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--skip-previews',action='store_true')
    group = parser.add_mutually_exclusive_group()
    group.add_argument('--record-baseline')
    group.add_argument('--compare-baseline')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    require(bpy.app.version[:2] == (5,2), 'Use Blender 5.2')
    check_door_quality('castle', OUT)
    kit_path, rooms_path = ART/'Kit/CastleKit.manifest.json', ART/'Rooms/CastleRooms.manifest.json'
    kit, catalogue = json.loads(kit_path.read_text()), json.loads(rooms_path.read_text())
    rows, rooms = kit['pieces'], catalogue['templates']
    require(kit['theme'] == catalogue['theme'] == 'castle' and kit['wallHeight'] == 7 and catalogue['module'] == 2, 'theme/module')
    require(kit['paletteSrgb'] == PALETTE and kit['lightColorSrgb'] == '#ffb347', 'owner palette')
    ids = {r['id'] for r in rows}
    require(len(ids) == len(rows) and set(REQUIRED) <= ids, 'contract inventory/duplicate IDs')
    require({p.name for p in (ART/'Kit').glob('*.fbx')} == {r['file'] for r in rows}, 'FBX inventory')
    meshes = {}
    for row in rows:
        require(row['kind'] in KINDS and row['file'] == 'Castle_'+row['id']+'.fbx', row['id']+': filename/kind')
        if row['id'] in REQUIRED:
            require(row['kind'] == REQUIRED[row['id']], row['id']+': mandatory kind')
        meshes[row['id']] = check_piece(row)
    check_seams(meshes)
    require(len(rooms) >= 10 and len({t['id'] for t in rooms}) == len(rooms), 'room catalogue inventory')
    require({'castle_tower_room','castle_chapel_apse'} <=
            {t['id'] for t in rooms if t['shape']=='round'}, 'required round-room catalogue')
    details = [validate_room(t,meshes) for t in rooms]
    ordinary = [t for t in rooms if t['kind'] == 'room' and t['gimmick'] == 'none']
    for size,n in (('closet',1),('small',2),('medium',2),('large',1),('hall',1)):
        require(sum(t['sizeClass'] == size for t in ordinary) >= n, 'catalogue missing '+size)
    require(any(t['sizeClass'] == 'medium' and t['shape'] in ('L','round') for t in ordinary), 'medium L/round')
    require(sum(t['kind'] == 'hallway' for t in rooms) >= 2 and any(t['kind']=='hallway' and t['shape']=='rect' for t in rooms)
            and any(t['kind'] in ('hallway','junction') and t['shape'] in ('L','T') for t in rooms), 'hallway catalogue')
    require(1 <= sum(t['gimmick'] != 'none' for t in rooms) <= 2, 'gimmick catalogue')
    controls = negative_controls(rooms,meshes)
    placement_regressions(rooms, {r['id']:r for r in rows}, {k:v['points'] for k,v in meshes.items()})
    if not args.skip_previews:
        check_door_previews('castle', OUT)
    check_sources(rows,rooms)
    preview_count = check_previews(rooms) if not args.skip_previews else None
    snapshot = {'kitManifestSha256': sha(kit_path), 'roomsManifestSha256': sha(rooms_path),
                'geometry': {r['id']: r['geometrySha256'] for r in rows},
                'textures': {p.name: sha(p) for p in sorted((ART/'Kit').glob('*.png'))}}
    baseline_name = args.record_baseline or args.compare_baseline
    if baseline_name:
        require(re.fullmatch(r'[a-zA-Z0-9_-]+',baseline_name), 'baseline must be a name, not a path')
        baseline = OUT/(baseline_name+'.json')
        if args.record_baseline:
            require(not baseline.exists(), 'refusing to overwrite determinism baseline')
            baseline.write_text(json.dumps(snapshot,indent=2)+'\n',encoding='utf-8')
        else:
            require(snapshot == json.loads(baseline.read_text()), 'repeat manifest/mesh/texture drift')
    lines = [f'PASS castle kit: {len(REQUIRED)}/16 mandatory IDs; {len(rows)} exports; measured dimensions, pivots, Y-up/-Z-forward, applied transforms',
             f'PASS castle geometry: architecture <=300 triangles; props <=1500; UV/material slots; source/export hashes; no degenerate faces',
             'PASS castle seams: 2m repeats, door/window interchange, floor/ceiling/trim, r4/r6/r8 rotated seams',
             'PASS castle openings: clear 3.2x2.8m doors, 1.3x1.4m contract windows, pointed crowns and arrow slits; 0.8m masonry',
             f'PASS castle rooms: {len(rooms)} templates; catalogue classes; 4-connectivity, boundary sockets, anchors/clearance/support, valid pieces',
             'PASS castle enclosure: imported-mesh boundary probes, clear sockets/closedWith, no positive-volume wall overlaps',
             'PASS castle sources: kit geometry and every room placement match manifests; packed relative textures <=1024px/6',
             f'PASS castle validator: {len(controls)} negative controls rejected']
    if preview_count is not None:
        lines.append(f'PASS castle previews: {preview_count} nonblank images; kit sheet, every room, torch/flashlight darkness (not artistic acceptance)')
    if args.compare_baseline:
        lines.append('PASS castle determinism: two generations have identical kit/room manifest, geometry and texture hashes')
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/'validation-v2.txt').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    (OUT/'validation-v2.json').write_text(json.dumps({'pass':True,'checks':lines,'rooms':details,
        'negativeControls':controls,'snapshot':snapshot},indent=2)+'\n',encoding='utf-8')
    print('\n'.join(lines))
    print(json.dumps(snapshot,indent=2))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        OUT.mkdir(parents=True,exist_ok=True)
        (OUT/'validation-v2.txt').write_text('FAIL castle: '+str(error)+'\n',encoding='utf-8')
        raise
