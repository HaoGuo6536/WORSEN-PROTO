# ============================================================================
# validate_env_theme_vaults.py
# PURPOSE:
#   Check optional vault shortcuts against the room's collision envelopes, not
#   their names or decorative ceiling vaults. Keep the gate usable without Blender
#   so placement searches and destructive regression controls exercise it directly.
# ARCHITECTURAL ROLE: Offline art validator · Environment; no Unity runtime layer.
# KEY RESPONSIBILITIES:
#   - Check measured vault dimensions, collision metadata and bidirectional landings.
#   - Preserve full door throats, anchor clearance and ordinary walking bypasses.
#   - Enforce explicit density targets and reject damaged traversal contracts.
# DEPENDENCIES: Python standard library; theme validators supply imported vertices.
# USAGE NOTES:
#   Conservative standing boxes use the checked-in 0.5m/2m navigation agent.
#   The 0.25m lattice is a validation resolution, not a runtime navigation promise.
#   Native import, physics, selected hub/shrine sites and navigation remain Unity gates.
# ============================================================================
import copy
import math

RADIUS = .5
BODY_HEIGHT = 2.0
STEP = .25
LEAVES = {'door_iron_strapped', 'door_double_porthole_4m',
          'prop_classroom_door_leaf', 'prop_bulkhead_leaf'}


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def density(t):
    return 0 if t['sizeClass'] == 'closet' else 2 if len(t['footprint']) > 20 else 1


def rotate(x, z, yaw):
    a = math.radians(yaw)
    return x*math.cos(a)+z*math.sin(a), -x*math.sin(a)+z*math.cos(a)


def socket(d):
    x, z = d['cell']
    dx, dz = {'N': (0, 1), 'S': (0, -1), 'E': (1, 0), 'W': (-1, 0)}[d['side']]
    span = d.get('span', 1)-1
    return (2*x+1+dx+span*abs(dz), 2*z+1+dz+span*abs(dx)), (dx, dz)


def boxes(t, rows, omit=None):
    """Runtime bottom-centred prop boxes; frames have real open apertures."""
    result = []
    for p in t['pieces']:
        row = rows[p['id']]
        kind = row['kind']
        if p is omit or kind in ('floor', 'decal') or p['id'] in LEAVES:
            continue
        x, y, z = p['pos']; w, h, d = row['size']; yaw = p['rotY']
        def add(cx, cy, cz, sx, sy, sz, angle=yaw):
            result.append((cx, cy, cz, sx/2, sy/2, sz/2, angle))
        if kind == 'door':
            for sign in (-1, 1):
                dx, dz = rotate(sign*(w/2+1.6)/2, 0, yaw)
                add(x+dx, y+h/2, z+dz, (w-3.2)/2, h, d)
            add(x, y+(h+2.8)/2, z, 3.2, h-2.8, d)
        elif kind == 'arc' and t['id'].startswith('castle_'):
            radius = int(p['id'][-1]); sweep = {4: 30, 6: 20, 8: 15}[radius]
            for i in range(4):
                angle = -sweep/2+sweep*(i+.5)/4
                a = math.radians(angle)
                dx, dz = rotate(radius*math.sin(a), radius*math.cos(a)-radius, yaw)
                add(x+dx, y+h/2, z+dz, 2*radius*math.sin(math.radians(sweep/8)), h, .82, yaw+angle)
        elif p['id'] == 'wall_round_tangent_r4':
            for sign in (-1, 1):
                dx, dz = rotate(sign*2, -.4, yaw)
                add(x+dx, y+h/2, z+dz, .8, h, .8)
        else:
            add(x, y+h/2, z, w, h, d)
    return result


def intersects(box, a, b, radius=RADIUS, height=BODY_HEIGHT):
    cx, cy, cz, hx, hy, hz, yaw = box
    # Expanding by an axis-aligned square in local space is conservative for a capsule.
    ax, az = rotate(a[0]-cx, a[2]-cz, -yaw)
    bx, bz = rotate(b[0]-cx, b[2]-cz, -yaw)
    low, high = 0., 1.
    for start, end, half in zip((ax, a[1]+height/2-cy, az),
                                (bx, b[1]+height/2-cy, bz),
                                (hx+radius-1e-4, hy+height/2-1e-4, hz+radius-1e-4)):
        delta = end-start
        if abs(delta) < 1e-8:
            if abs(start) >= half:
                return False
        else:
            enter, leave = (-half-start)/delta, (half-start)/delta
            low, high = max(low, min(enter, leave)), min(high, max(enter, leave))
            if low >= high:
                return False
    return True


class WalkMap:
    def __init__(self, t, rows, omit=None):
        self.cells = {tuple(c) for c in t['footprint']}
        if t.get('shape') != 'round':
            # Missing/broken tiles in the collapsed crossing are not floor, even
            # though today's runtime consumer fills the entire occupancy grid.
            supported = set()
            for p in t['pieces']:
                row = rows[p['id']]
                if row['kind'] == 'floor' and 'broken' not in p['id'] and abs(p['pos'][1]+row['size'][1]) < .05:
                    supported.add((round((p['pos'][0]-1)/2), round((p['pos'][2]-1)/2)))
            self.cells &= supported
        self.obstacles = boxes(t, rows, omit)
        self.cache = {}

    def supported(self, p, radius=RADIUS):
        for x in range(math.floor((p[0]-radius+1e-4)/2), math.floor((p[0]+radius-1e-4)/2)+1):
            for z in range(math.floor((p[2]-radius+1e-4)/2), math.floor((p[2]+radius-1e-4)/2)+1):
                if (x, z) not in self.cells:
                    return False
        return True

    def clear(self, p, radius=RADIUS, height=BODY_HEIGHT):
        return self.supported(p, radius) and not any(intersects(b, p, p, radius, height) for b in self.obstacles)

    def sweep(self, a, b):
        return not any(intersects(box, a, b) for box in self.obstacles)

    def near(self, p):
        x, z = math.floor(p[0]/STEP), math.floor(p[2]/STEP)
        return [(x+a, z+b) for a in (0, 1) for b in (0, 1)]

    def point(self, key):
        return [key[0]*STEP, 0, key[1]*STEP]

    def safe(self, key):
        if key not in self.cache:
            self.cache[key] = self.clear(self.point(key))
        return self.cache[key]

    def flood(self, p):
        if not self.clear(p):
            return set()
        seen = {k for k in self.near(p) if self.safe(k) and self.sweep(p, self.point(k))}
        queue = list(seen)
        while queue:
            x, z = queue.pop()
            for k in ((x-1, z), (x+1, z), (x, z-1), (x, z+1)):
                if k not in seen and self.safe(k) and self.sweep(self.point((x, z)), self.point(k)):
                    seen.add(k); queue.append(k)
        return seen

    def reached(self, p, seen):
        return self.clear(p) and any(k in seen and self.sweep(p, self.point(k)) for k in self.near(p))


def hub_sites(t, rows):
    """Mirror pure hub preselection on the pre-vault furnishing; reserve its pair."""
    if t['kind'] != 'room' or t['gimmick'] != 'none' or len(t['footprint']) < 10 or len(t['doors']) < 2:
        return []
    cells = {tuple(c) for c in t['footprint']}
    def clear(p, radius, height):
        for x in range(math.floor((p[0]-radius)/2), math.floor((p[0]+radius)/2)+1):
            for z in range(math.floor((p[2]-radius)/2), math.floor((p[2]+radius)/2)+1):
                if (x, z) not in cells:
                    return False
        for piece in t['pieces']:
            row = rows[piece['id']]
            if piece.get('traversal') == 'vault' or row['kind'] in ('floor', 'ceiling', 'decal'):
                continue
            if piece['pos'][1] >= height or piece['pos'][1]+row['size'][1] <= 0:
                continue
            a = math.radians(piece['rotY'])
            ex = (abs(math.cos(a))*row['size'][0]+abs(math.sin(a))*row['size'][2])/2
            ez = (abs(math.sin(a))*row['size'][0]+abs(math.cos(a))*row['size'][2])/2
            if abs(p[0]-piece['pos'][0]) <= ex+radius and abs(p[2]-piece['pos'][2]) <= ez+radius:
                return False
        return True
    def cake_distance(p):
        return min(sum((a-b)**2 for a, b in zip(p, c)) for c in t['anchors']['cake'])
    players = [[2*x+1, 0, 2*z+1] for x, z in t['footprint']]
    players = sorted((p for p in players if clear(p, .6, 2.8) and cake_distance(p) >= 1), key=cake_distance, reverse=True)
    center = [max(x for x, z in cells)+1, 0, max(z for x, z in cells)+1]
    exits = [[2*x+a, 0, 2*z+b] for x, z in t['footprint'] for a in (1, 2) for b in (1, 2)]
    exits = sorted((p for p in exits if clear(p, 1.6, 3) and cake_distance(p) >= 2.25),
                   key=lambda p: sum((a-b)**2 for a, b in zip(p, center)))
    for exit in exits:
        for player in players:
            if sum((a-b)**2 for a, b in zip(exit, player)) >= 3.2**2:
                return [(player, .6), (exit, 1.6)]
    return []


def check_vault(t, p, rows, connectivity=True, reserved=None):
    label = t['id']+': vault '
    row = rows[p['id']]
    require(row['kind'] == 'prop' and row.get('traversal') == 'vault' and row.get('collision') is True,
            label+'kit traversal/collision metadata')
    require(p.get('traversal') == 'vault' and p.get('collision') is True, label+'placement metadata')
    w, h, d = row['size']
    require(.35 <= h <= 1.2 and abs(p['pos'][1]) < 1e-5, label+'height band/floor datum')
    require(.3 <= d <= .6 and w >= 1.0, label+'probe depth/width')
    endpoints = [p.get('endpointA'), p.get('endpointB')]
    require(all(isinstance(a, list) and len(a) == 3 and all(math.isfinite(v) for v in a) for a in endpoints), label+'finite endpoints')
    normal = rotate(0, 1, p['rotY'])
    for sign, a in zip((-1, 1), endpoints):
        expected = [p['pos'][0]+sign*normal[0]*(d/2+.7), 0, p['pos'][2]+sign*normal[1]*(d/2+.7)]
        require(max(abs(v-u) for v, u in zip(a, expected)) < 1e-4, label+'endpoint transform')
    bare = WalkMap(t, rows, omit=p)
    world = WalkMap(t, rows)
    require(bare.supported(p['pos'], max(w, d)/2), label+'supported obstacle footprint')
    tx, tz = rotate(1, 0, p['rotY'])
    a = [p['pos'][0]-tx*(w-d)/2, 0, p['pos'][2]-tz*(w-d)/2]
    b = [p['pos'][0]+tx*(w-d)/2, 0, p['pos'][2]+tz*(w-d)/2]
    require(not any(intersects(box, a, b, d/2, h) for box in bare.obstacles), label+'obstacle overlap')
    for sign, a in zip((-1, 1), endpoints):
        require(world.clear(a), label+'landing clearance')
        runup = [a[0]+sign*normal[0]*.5, 0, a[2]+sign*normal[1]*.5]
        require(world.clear(runup) and world.sweep(a, runup), label+'run-up clearance')
    # Full lifted player envelope across the obstacle, including overhead services.
    for i in range(9):
        q = [endpoints[0][j]+(endpoints[1][j]-endpoints[0][j])*i/8 for j in range(3)]
        q[1] = h+.08
        require(bare.clear(q, .3, 1.8), label+'overhead clearance')
    obstacle = boxes({'id': t['id'], 'pieces': [p]}, rows)[0]
    for a, radius in hub_sites(t, rows) if reserved is None else reserved:
        require(not intersects(obstacle, a, a, radius+.001), label+'player/exit reservation')
    for kind, anchors in t['anchors'].items():
        if kind == 'light':
            continue
        for a in anchors:
            require(not intersects(obstacle, a, a, .6), label+'anchor clearance '+kind)
    for door in t['doors']:
        (x, z), (dx, dz) = socket(door)
        # Preserve the full 3.2m opening and the first metre inside it, not just its centre.
        a, b = [x, 0, z], [x-dx, 0, z-dz]
        for offset in (-1.6, -.8, 0, .8, 1.6):
            aa = [a[0]+offset*abs(dz), 0, a[2]+offset*abs(dx)]
            bb = [b[0]+offset*abs(dz), 0, b[2]+offset*abs(dx)]
            require(not intersects(obstacle, aa, bb, .05), label+'door span')
    if connectivity:
        seen = world.flood(endpoints[0])
        require(world.reached(endpoints[1], seen), label+'walking bypass')
        for door in t['doors']:
            (x, z), (dx, dz) = socket(door)
            require(world.reached([x-dx, 0, z-dz], seen), label+'door connectivity')
        for kind, anchors in t['anchors'].items():
            if kind != 'light':
                for a in anchors:
                    require(world.reached(a, seen), label+'anchor connectivity '+kind)


def validate_vaults(templates, rows, points):
    details = []
    for t in templates:
        vaults = [p for p in t['pieces'] if p.get('traversal') == 'vault']
        require(len(vaults) == density(t), t['id']+': vault density')
        for p in vaults:
            vertices = points[p['id']]
            measured = [max(v[i] for v in vertices)-min(v[i] for v in vertices) for i in range(3)]
            require(all(abs(a-b) < 1e-4 for a, b in zip(measured, rows[p['id']]['size'])), t['id']+': measured vault bounds')
            require(abs(min(v[1] for v in vertices)) < 1e-4, t['id']+': vault bottom pivot')
            check_vault(t, p, rows)
        details.append({'id': t['id'], 'obstacles': len(vaults), 'heights': [rows[p['id']]['size'][1] for p in vaults]})
    # Each mutation targets the traversal gate, not an unrelated legacy validation.
    base = next(t for t in templates if any(p.get('traversal') == 'vault' for p in t['pieces']))
    rejected = []
    for mutation in ('height', 'landing', 'anchor', 'door', 'collision', 'density', 'bypass'):
        damaged, kit = copy.deepcopy(base), copy.deepcopy(rows)
        p = next(p for p in damaged['pieces'] if p.get('traversal') == 'vault')
        if mutation == 'height':
            kit[p['id']]['size'][1] = 1.21
        elif mutation == 'landing':
            damaged['pieces'].append({'id': p['id'], 'pos': list(p['endpointA']), 'rotY': p['rotY']})
        elif mutation == 'anchor':
            damaged['anchors']['cake'][0] = list(p['pos'])
        elif mutation == 'door':
            (x, z), _ = socket(damaged['doors'][0]); p['pos'] = [x, 0, z]
        elif mutation == 'collision':
            p['collision'] = False
        elif mutation == 'density':
            damaged['pieces'].remove(p)
        elif mutation == 'bypass':
            # A ring of standing blockers encloses the obstacle and its endpoints.
            x, _, z = p['pos']
            kit['test_wall'] = {'kind': 'prop', 'size': [20, 2, .2]}
            damaged['pieces'].append({'id': 'test_wall', 'pos': [x, 0, z], 'rotY': p['rotY']})
        try:
            vaults = [v for v in damaged['pieces'] if v.get('traversal') == 'vault']
            require(len(vaults) == density(damaged), 'density')
            for v in vaults:
                check_vault(damaged, v, kit)
        except AssertionError:
            rejected.append(mutation)
        else:
            raise AssertionError('Vault negative control accepted: '+mutation)
    print(f"PASS {templates[0]['id'].split('_')[0]} vaults: {sum(d['obstacles'] for d in details)}; height/landing/run-up/headroom/doors/anchors/bypass/density; {len(rejected)} controls rejected")
    return details
