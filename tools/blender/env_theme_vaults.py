# ============================================================================
# env_theme_vaults.py
# PURPOSE:
#   Restore frequent optional vault shortcuts to the authored theme catalogues.
#   Build original low obstacles and choose deterministic interior placements
#   without moving existing gameplay anchors or socket spans. Two crowded-room
#   furnishings are replaced explicitly rather than hiding placement failures.
# ARCHITECTURAL ROLE: Offline art generator · Environment; no Unity runtime layer.
# KEY RESPONSIBILITIES:
#   - Build themed, bottom-centred obstacles with predictable collision envelopes.
#   - Search walking-route positions with explicit bidirectional endpoint metadata.
#   - Fail generation if density or conservative clearance cannot be satisfied.
# DEPENDENCIES: Python stdlib; theme mesh builders; validate_env_theme_vaults gate.
# USAGE NOTES:
#   Kind stays prop for the existing consumer; traversal=vault and collision=true
#   are additive metadata. Endpoint coordinates are room-local Unity XYZ metres.
#   Runtime owner must parse those fields and emit tagged traversal blocks.
# ============================================================================
import math

from validate_env_theme_vaults import check_vault, density, rotate, socket, hub_sites

PIECES = {'hospital': 'prop_vault_partition', 'school': 'prop_vault_low_locker',
          'basement': 'prop_vault_low_duct', 'castle': 'prop_vault_low_wall'}
HEIGHTS = {'hospital': .9, 'school': .8, 'basement': .65, 'castle': .9}


def build_vault(theme, mesh):
    """Distinct silhouettes/materials, all within a 1.2 x height x .5m box."""
    z_up = theme in ('hospital', 'castle')
    def box(center, size, surface):
        if z_up:
            center = (center[0], -center[2], center[1])
            size = (size[0], size[2], size[1])
        mesh.box(center, size, surface)
    height = HEIGHTS[theme]
    if theme == 'hospital':
        box((0, .42, 0), (1.16, .84, .44), 'tile')
        box((0, .87, 0), (1.2, .06, .5), 'vinyl')
        for x in (-.57, .57):
            box((x, .44, 0), (.04, .84, .46), 'stainless')
        box((0, .055, -.23), (1.12, .11, .025), 'rubber')
    elif theme == 'school':
        box((0, .4, 0), (1.2, .8, .5), 'teal')
        for x in (-.39, 0, .39):
            box((x, .415, -.248), (.365, .70, .004), 'mustard')
            box((x+.12, .43, -.249), (.025, .12, .002), 'steel')
            for y in (.2, .24, .64, .68):
                box((x, y, -.2495), (.22, .014, .001), 'rubber')
    elif theme == 'basement':
        box((0, .325, 0), (1.2, height, .46), 'galvanised')
        for x in (-.56, 0, .56):
            box((x, .325, 0), (.065, height, .5), 'steel')
        for x in (-.4, .4):
            box((x, height-.002, 0), (.1, .004, .44), 'hazard')
    else:
        box((0, .41, 0), (1.16, .82, .44), 'mortar')
        for y in (.14, .41, .68):
            for x in (-.38, 0, .38):
                box((x, y, 0), (.365, .25, .48), 'stone' if x else 'stone_dark')
        box((0, .86, 0), (1.2, .08, .5), 'stone')


def add_vaults(templates, rows, theme):
    lookup = {r['id']: r for r in rows}
    pid = PIECES[theme]
    lookup[pid].update(traversal='vault', collision=True)
    for t in templates:
        # The guard room's central table occupied the only usable shortcut lane.
        # Replace that furnishing with low masonry; retain its bench and anchors.
        if t['id'] == 'castle_guard_room':
            t['pieces'] = [p for p in t['pieces'] if p['id'] != 'prop_trestle_table']
        # One lab chair is replaced by the low locker at the cross-aisle; all
        # three laboratory benches and the other seats remain in the room.
        if t['id'] == 'school_science_lab':
            t['pieces'] = [p for p in t['pieces'] if not
                           (p['id'] == 'prop_chair' and p['pos'] == [5, 0, 5.28])]
        target = density(t)
        if not target:
            continue
        reserved = hub_sites(t, lookup)
        # Mid-room candidates first; a small line-distance penalty favours routes
        # between doors rather than corners. No random or catalogue-order effects.
        cells = t['footprint']
        cx = sum(2*x+1 for x, z in cells)/len(cells)
        cz = sum(2*z+1 for x, z in cells)/len(cells)
        doors = [socket(d)[0] for d in t['doors']]
        candidates = []
        for ix in range(4, 8*(max(x for x, z in cells)+1)):
            for iz in range(4, 8*(max(z for x, z in cells)+1)):
                x, z = ix*.25, iz*.25
                for yaw in (0, 90):
                    score = (x-cx)**2+(z-cz)**2
                    if len(doors) > 1:
                        a, b = doors[0], doors[1]
                        dx, dz = b[0]-a[0], b[1]-a[1]
                        norm = math.hypot(dx, dz)
                        if norm:
                            score += 2*((x-a[0])*dz-(z-a[1])*dx)**2/(norm*norm)
                            nx, nz = rotate(0, 1, yaw)
                            score += 3*(1-abs((nx*dx+nz*dz)/norm))
                    candidates.append((score, x, z, yaw))
        added = []
        for _, x, z, yaw in sorted(candidates):
            if any(math.hypot(x-p['pos'][0], z-p['pos'][2]) < 3 for p in added):
                continue
            nx, nz = rotate(0, 1, yaw)
            offset = lookup[pid]['size'][2]/2+.7
            p = {'id': pid, 'pos': [x, 0, z], 'rotY': yaw,
                 'traversal': 'vault', 'collision': True,
                 'endpointA': [round(x-nx*offset, 5), 0, round(z-nz*offset, 5)],
                 'endpointB': [round(x+nx*offset, 5), 0, round(z+nz*offset, 5)]}
            t['pieces'].append(p)
            try:
                check_vault(t, p, lookup, connectivity=False, reserved=reserved)
                for existing in added:
                    check_vault(t, existing, lookup, connectivity=False, reserved=reserved)
                check_vault(t, p, lookup, reserved=reserved)
            except AssertionError:
                t['pieces'].pop()
                continue
            added.append(p)
            if len(added) == target:
                break
        if len(added) != target:
            raise AssertionError(f"{t['id']}: found {len(added)}/{target} safe vault placements")
        print(f"VAULTS {t['id']}: {len(added)} at {[p['pos'] for p in added]}", flush=True)
