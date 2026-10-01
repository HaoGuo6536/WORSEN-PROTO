# ============================================================================
# validate_exit_door.py
# PURPOSE:
#   Independently verify the saved source and exported door rather than trusting
#   generator metadata. Reject lost hinge origins, reversed apertures and debris.
# ARCHITECTURAL ROLE: Offline art validator · Floor/Exit, outside runtime layers.
# KEY RESPONSIBILITIES:
#   - Re-import FBX and check topology budget, names, metre scale and hinge motion.
#   - Check packed source texture and complete rendered evidence.
#   - Record measured evidence and artifact hashes without modifying inputs.
# DEPENDENCIES: Blender 5.2 and Python standard library only; not the generator.
# USAGE NOTES: Run with --background --factory-startup --python-exit-code 1.
#   Unity material mapping, import axes and URP rendering remain coordinator gates.
# ============================================================================
import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/Exit/WeatheredDoor/WORSEN_WeatheredExitDoor.blend'
FBX = ROOT / 'Assets/Art/Exit/WeatheredDoor/WORSEN_WeatheredExitDoor.fbx'
REVIEW = ROOT / 'Logs/AgentValidation/Art/ExitDoor'


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def bounds(obj):
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    return [[min(p[i] for p in points), max(p[i] for p in points)] for i in range(3)]


def measure(label):
    objects = {o.name: o for o in bpy.context.scene.objects}
    check(set(objects) == {'DoorFrame', 'DoorLeaf', 'Threshold', 'EscapeSurface'}, label+': runtime-only export')
    check(all(o.type == 'MESH' for o in objects.values()), label+': mesh-only assembly')
    leaf = objects['DoorLeaf']
    check((leaf.matrix_world.translation-Vector((-1, 0, 0))).length < .001, label+': hinge origin')
    check(all(abs(s-1) < .001 for o in objects.values() for s in o.scale), label+': applied scales')
    frame = bounds(objects['DoorFrame'])
    check(abs(frame[0][0]+1.2) < .002 and abs(frame[0][1]-1.2) < .002, label+': frame width')
    check(abs(frame[2][0]) < .002 and abs(frame[2][1]-3.21) < .002, label+': frame height')
    closed = bounds(leaf)
    check(abs(closed[0][0]+.99) < .002 and abs(closed[0][1]-.99) < .002, label+': closed aperture width')
    check(abs(closed[2][0]-.02) < .002 and abs(closed[2][1]-2.98) < .002, label+': leaf height')
    portal = objects['EscapeSurface']
    check(len(portal.data.vertices) == 4, label+': one aperture quad')
    normal = (portal.matrix_world.to_3x3() @ portal.data.polygons[0].normal).normalized()
    check(normal.dot(Vector((0, 1, 0))) > .999, label+': aperture faces front')
    pb = bounds(portal)
    check(abs(pb[1][0]+.11) < .001 and abs(pb[1][1]+.11) < .001, label+': portal behind closed leaf')
    check(pb[0][0] >= -1.001 and pb[0][1] <= 1.001 and pb[2][0] >= 0 and pb[2][1] <= 3.001, label+': aperture bounds')
    before = leaf.matrix_world.translation.copy()
    leaf.rotation_euler.z += math.radians(100)
    bpy.context.view_layer.update()
    opened = bounds(leaf)
    check((leaf.matrix_world.translation-before).length < .001, label+': stationary hinge during swing')
    check(opened[0][1] < -.80 and opened[1][1] > 1.9, label+': leaf clears aperture toward front')
    leaf.rotation_euler.z -= math.radians(100)
    triangles = sum(len(p.vertices)-2 for o in objects.values() for p in o.data.polygons)
    check(0 < triangles < 12000, label+': triangle budget')
    check(all(len(o.data.materials) > 0 for o in objects.values()), label+': material slots')
    return dict(label=label, triangles=triangles, frame_bounds_blender=frame,
                leaf_closed_bounds_blender=closed, leaf_open_bounds_blender=opened)


def main():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    check(any(image.packed_file for image in bpy.data.images if image.name == 'ExitPaint'), 'source texture packed')
    source = measure('source')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(FBX), use_anim=False)
    exported = measure('reimported FBX')
    check(source['triangles'] == exported['triangles'], 'export topology preserved')
    paths = [SOURCE, FBX, ROOT/'Assets/Art/Exit/WeatheredDoor/ExitPaint.png']
    for name in ('closed-front', 'open-front', 'open-back', 'swing-1', 'swing-2', 'swing-3', 'swing-strip'):
        path = REVIEW/(name+'.png')
        image = bpy.data.images.load(str(path), check_existing=False)
        check(image.size[0] >= 640 and image.size[1] == 720, name+': rendered dimensions')
        paths.append(path)
    report = dict(status='PASS', measurements=[source, exported],
                  hashes={str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},
                  native_checks_pending=['Unity imported axes and material mapping', 'front/back open-door screenshots',
                                         'physical crossing and collision', 'audio and Lumen spill'])
    (REVIEW/'validation.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    print('EXIT_DOOR_VALIDATION', json.dumps(report))


if __name__ == '__main__':
    main()
