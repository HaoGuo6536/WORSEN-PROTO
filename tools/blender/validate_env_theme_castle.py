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
    if row['kind'] != 'arc':
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
    for cell,side,center,tangent in edges:
        dx,dz = SIDES[side]
        for i in range(40):
            u = -.975+i*.05
            x,z = center[0]+u*tangent[0],center[1]+u*tangent[1]
            is_door = False
            for socket in sockets:
                if socket['side'] != side:
                    continue
                sx,sz = socket['cell']
                sc = (2*sx+1+dx,2*sz+1+dz)
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
        x,z = socket['cell']
        center = (x*2+1+dx,z*2+1+dz)
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
    return controls


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--skip-previews',action='store_true')
    group = parser.add_mutually_exclusive_group()
    group.add_argument('--record-baseline')
    group.add_argument('--compare-baseline')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    require(bpy.app.version[:2] == (5,2), 'Use Blender 5.2')
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
    details = [validate_room(t,meshes) for t in rooms]
    ordinary = [t for t in rooms if t['kind'] == 'room' and t['gimmick'] == 'none']
    for size,n in (('closet',1),('small',2),('medium',2),('large',1),('hall',1)):
        require(sum(t['sizeClass'] == size for t in ordinary) >= n, 'catalogue missing '+size)
    require(any(t['sizeClass'] == 'medium' and t['shape'] in ('L','round') for t in ordinary), 'medium L/round')
    require(sum(t['kind'] == 'hallway' for t in rooms) >= 2 and any(t['kind']=='hallway' and t['shape']=='rect' for t in rooms)
            and any(t['kind'] in ('hallway','junction') and t['shape'] in ('L','T') for t in rooms), 'hallway catalogue')
    require(1 <= sum(t['gimmick'] != 'none' for t in rooms) <= 2, 'gimmick catalogue')
    controls = negative_controls(rooms,meshes)
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
