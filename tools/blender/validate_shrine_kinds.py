# ============================================================================
# validate_shrine_kinds.py
# PURPOSE:
#   Independently re-import every shrine FBX and check the static art contract.
#   Measured geometry and source comparisons catch missing slots, scale or axes drift.
# ARCHITECTURAL ROLE:
#   Offline art validator (outside runtime layers) · Shrine.
# KEY RESPONSIBILITIES:
#   - Check bounds, ground pivot, topology, interaction marker and material coverage.
#   - Compare source and round-tripped export geometry without binary timestamp hashes.
#   - Require the full preview set and write machine-readable measured evidence.
# DEPENDENCIES:
#   Blender 5.2, mathutils and Python standard library; no generator imports.
# USAGE NOTES:
#   Run with --background --factory-startup --python-exit-code 1 after generation.
#   This proves export structure, not Unity import or visual acceptance in game lighting.
# ============================================================================
import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'Logs/AgentValidation/Art/Shrine'
KINDS = ('Chance', 'Bargain', 'Pacification', 'Wick', 'Passage', 'Protection', 'Echo', 'Purgatory')
SLOTS = {'M_ShrineBody', 'M_ShrineAccent'}


def close(actual, expected, label):
    assert len(actual) == len(expected), label
    assert all(abs(a-b) < .0001 for a, b in zip(actual, expected)), (label, actual, expected)


def inspect(kind):
    objects = list(bpy.context.scene.objects)
    assert len(objects) == 2, [(o.name, o.type) for o in objects]
    mesh, = [o for o in objects if o.type == 'MESH']
    marker, = [o for o in objects if o.type == 'EMPTY']
    assert mesh.name == 'WORSEN_Shrine'+kind
    assert marker.name == 'InteractionPoint'
    assert not mesh.modifiers and not mesh.animation_data
    assert not mesh.parent and not marker.parent
    close(mesh.matrix_world.translation, (0, 0, 0), 'floor origin')
    coords = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
    assert coords and all(math.isfinite(c) for v in coords for c in v)
    lo = [min(v[i] for v in coords) for i in range(3)]
    hi = [max(v[i] for v in coords) for i in range(3)]
    assert abs(lo[2]) < .0001, ('not grounded', lo)
    assert .8 <= hi[2] <= 1.6, ('height', hi[2])
    assert .4 < hi[0]-lo[0] < 1.3 and .4 < hi[1]-lo[1] < 1.3, ('footprint', lo, hi)
    assert {m.name for m in mesh.data.materials} == SLOTS
    mesh.data.calc_loop_triangles()
    assert 100 <= len(mesh.data.loop_triangles) <= 6000
    assert all(len(p.vertices) == 3 for p in mesh.data.polygons), 'Non-triangulated export'
    slots = {name: 0 for name in SLOTS}
    canonical = []
    for tri in mesh.data.loop_triangles:
        a, b, c = [coords[v] for v in tri.vertices]
        assert (b-a).cross(c-a).length > 1e-10, 'Degenerate triangle'
        mat = mesh.data.materials[tri.material_index].name
        slots[mat] += 1
        # Sorting vertex and triangle order tolerates FBX index reordering.
        # FBX round trips can turn a grounded +0 into -0 by floating-point rotation.
        # JSON distinguishes their spelling despite numeric equality; normalise only zero.
        canonical.append((mat, sorted(tuple(round(c, 4) or 0.0 for c in coords[v]) for v in tri.vertices)))
    assert all(n > 0 for n in slots.values()), slots
    digest = hashlib.sha256(json.dumps(sorted(canonical)).encode()).hexdigest()
    return dict(bounds={'min': lo, 'max': hi}, height=hi[2]-lo[2],
                footprint=[hi[0]-lo[0], hi[1]-lo[1]], triangles=len(canonical),
                material_triangles=slots, geometry_sha256=digest,
                interaction=list(marker.matrix_world.translation))


def image_check(path, expected):
    assert path.is_file(), path
    image = bpy.data.images.load(str(path), check_existing=False)
    try:
        assert tuple(image.size) == expected, (path, tuple(image.size))
        pixels = list(image.pixels)
        sample = pixels[0::64]
        assert max(sample)-min(sample) > .03, ('Blank preview', path)
    finally:
        bpy.data.images.remove(image)


def main():
    assert bpy.app.version[:2] == (5, 2), bpy.app.version_string
    results = []
    for kind in KINDS:
        stem = 'WORSEN_Shrine'+kind
        source = ROOT / 'ArtSource/Shrine' / kind / (stem+'.blend')
        export = ROOT / 'Assets/Art/Shrine' / kind / (stem+'.fbx')
        manifest = json.loads(source.with_suffix('.manifest.json').read_text(encoding='utf-8'))
        assert manifest['kind'] == kind and manifest['schema'] == 1
        assert manifest['units'] == 'metres' and manifest['bakeAxisConversion'] is False
        assert manifest['unity_axes'] == 'Y up, -Z front'
        assert manifest['source'] == source.relative_to(ROOT).as_posix()
        assert manifest['export'] == export.relative_to(ROOT).as_posix()
        assert {m['name'] for m in manifest['materials']} == SLOTS
        bpy.ops.wm.open_mainfile(filepath=str(source))
        original = inspect(kind)
        accent = bpy.data.materials['M_ShrineAccent'].node_tree.nodes.get('Principled BSDF')
        assert accent.inputs['Emission Strength'].default_value > 0
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(export), use_anim=False)
        imported = inspect(kind)
        for bound in ('min', 'max'):
            close(imported['bounds'][bound], original['bounds'][bound], 'source/export '+bound)
            close(imported['bounds'][bound], manifest['bounds_blender'][bound], 'manifest '+bound)
        close(imported['interaction'], (0, -.405, .30), 'front interaction point')
        close(imported['interaction'], manifest['interaction_blender'], 'interaction manifest')
        p = imported['interaction']
        close((p[0], p[2], p[1]), manifest['interaction_unity'], 'Unity interaction coordinates')
        close(imported['footprint'], manifest['footprint'], 'footprint')
        close((imported['height'],), (manifest['height'],), 'height')
        assert imported['triangles'] == original['triangles'] == manifest['triangles']
        assert imported['material_triangles'] == original['material_triangles']
        assert imported['geometry_sha256'] == original['geometry_sha256'], 'Geometry changed in FBX round trip'
        for view in ('front', 'three-quarter', 'side'):
            image_check(EVIDENCE / f'{kind}-{view}.png', (640, 720))
        results.append(dict(kind=kind, **imported))
        print('SHRINE_VALID', kind, imported['triangles'], round(imported['height'], 4), flush=True)
    assert len({r['geometry_sha256'] for r in results}) == 8, 'Duplicate geometry'
    image_check(EVIDENCE / 'lineup.png', (2400, 650))
    report = dict(status='PASS', blender=bpy.app.version_string, models=results, preview_count=25,
                  unity_import_verified=False, visual_review='Separate vision-tool review required.')
    (EVIDENCE / 'validation.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    print('SHRINE_VALIDATION PASS: 8 source/FBX pairs; 25 previews; distinct geometry; two used material slots.')


if __name__ == '__main__':
    main()
