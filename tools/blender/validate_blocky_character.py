# ============================================================================
# validate_blocky_character.py
# PURPOSE: Independently re-import the shipped FBXs and fail on contract drift.
# ARCHITECTURAL ROLE: Offline art validation; never starts Unity.
# KEY RESPONSIBILITIES: Check rig, weights, geometry, units, takes and source IK.
# DEPENDENCIES: Blender 5.2 bpy/mathutils and Python standard library only.
# USAGE NOTES: Blender restores Z-up on import. Report Unity Y as Blender Z,
#   and Unity forward as Blender -Y; this is not a live Unity importer test.
# Exact command (Git Bash):
# "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/validate_blocky_character.py
# ============================================================================
import hashlib
import json

from pathlib import Path

import bpy
from mathutils import Matrix

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/Art/Player/BlockyCharacter"
SOURCE = ROOT / "ArtSource/Player/BlockyCharacter"
REPORT = ROOT / "Logs/AgentValidation/Art/BlockyCharacter"
ROWS, DETAILS = [], {}


def check(label, passed, detail):
    ROWS.append(("PASS" if passed else "FAIL", label, str(detail)))


def parents(full):
    result = {"Hips": None, "Spine": "Hips", "Chest": "Spine", "Neck": "Chest", "Head": "Neck"} if full else {"Root": None}
    for side in ("Left", "Right"):
        previous = "Chest" if full else "Root"
        for suffix in ("Shoulder", "UpperArm", "LowerArm", "Hand"):
            result[side + suffix] = previous
            previous = side + suffix
        if full:
            previous = "Hips"
            for suffix in ("UpperLeg", "LowerLeg", "Foot", "Toes"):
                result[side + suffix] = previous
                previous = side + suffix
    return result


def pose(armature, clip, frame):
    armature.animation_data_create().action = clip
    if clip.slots:
        armature.animation_data.action_slot = clip.slots[0]
    bpy.context.scene.frame_set(int(frame), subframe=frame % 1)
    bpy.context.view_layer.update()
    return [value for bone in armature.pose.bones for row in bone.matrix for value in row]


def validate(name, full):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = 30
    bpy.ops.import_scene.fbx(filepath=str(ART / (name + ".fbx")), use_anim=True,
                             automatic_bone_orientation=False, ignore_leaf_bones=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    check(name + " objects", len(rigs) == 1 and rigs[0].name == name and
          {obj.name for obj in meshes} == ({"Body", "Arms"} if full else {"LeftArm", "RightArm"}),
          ", ".join(sorted(obj.name for obj in bpy.context.scene.objects)))
    if len(rigs) != 1:
        return
    armature = rigs[0]
    armature.animation_data_clear()
    for bone in armature.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    actual = {bone.name: bone.parent.name if bone.parent else None for bone in armature.data.bones}
    check(name + " bone hierarchy", actual == parents(full), f"{len(actual)} exact deform bones; no controls/leaf bones")
    check(name + " shared skin", all(obj.parent == armature and len([m for m in obj.modifiers if m.type == "ARMATURE" and m.object == armature]) == 1 for obj in meshes), "one common armature")
    errors, points, counts, rest_error, signature = [], [], {}, 0, []
    for obj in meshes:
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        rest_error = max(rest_error, max((obj.matrix_world @ v.co - evaluated.matrix_world @ e.co).length
                         for v, e in zip(obj.data.vertices, evaluated.data.vertices)))
        obj.data.calc_loop_triangles()
        counts[obj.name] = len(obj.data.loop_triangles)
        for vertex in obj.data.vertices:
            groups = [(obj.vertex_groups[g.group].name, g.weight) for g in vertex.groups]
            if len(groups) != 1 or groups[0][0] not in actual or not armature.data.bones[groups[0][0]].use_deform or abs(groups[0][1] - 1) > 1e-6:
                errors.append(f"{obj.name}[{vertex.index}]={groups}")
            points.append(obj.matrix_world @ vertex.co)
        signature.append((obj.name, [tuple(round(v, 6) for v in obj.matrix_world @ vertex.co) for vertex in obj.data.vertices],
                          [tuple(p.vertices) for p in obj.data.polygons],
                          [[(obj.vertex_groups[g.group].name, round(g.weight, 6)) for g in v.groups] for v in obj.data.vertices]))
    check(name + " rigid weights", not errors, f"{len(points)} vertices; {len(errors)} invalid")
    check(name + " bind pose", rest_error < 1e-5, f"rest deformation error={rest_error:.8f}m")
    check(name + " triangle budget", 0 < sum(counts.values()) < 1000, counts)
    materials = {slot.material.name for obj in meshes for slot in obj.material_slots if slot.material}
    check(name + " materials", materials == {"Skin", "Shirt", "Trousers", "Shoes"}, ", ".join(sorted(materials)))
    if full:
        bottom, top = min(p.z for p in points), max(p.z for p in points)
        check(name + " height / ground", 1.75 <= top - bottom <= 1.85 and abs(bottom) < .001,
              f"height={top - bottom:.6f}m; Unity feet Y={bottom:.6f}m")
        hands = [armature.matrix_world @ armature.data.bones[s + "Hand"].head_local for s in ("Left", "Right")]
        check(name + " T-pose", abs(hands[0].z - hands[1].z) < .001 and abs(hands[0].x) > .7 and abs(hands[1].x) > .7, "hands level and lateral in rest pose")
    else:
        # FBX stores no leaf-bone tail length; measure actual weighted palm vertices.
        hands = []
        for side in ("Left", "Right"):
            obj = next(m for m in meshes if m.name == side + "Arm")
            vertices = [obj.matrix_world @ v.co for v in obj.data.vertices
                        if any(obj.vertex_groups[g.group].name == side + "Hand" for g in v.groups)]
            hands.append(sum(vertices, vertices[0] * 0) / len(vertices))
        check(name + " view pose", all(.2 < abs(p.x) < .4 and -.35 < p.z < -.15 and -.6 < p.y < -.4 for p in hands),
              "hand centres (Blender): " + str([tuple(round(v, 4) for v in p) for p in hands]))
        check(name + " forward clearance", max(p.y for p in points) < -.05, f"nearest depth={-max(p.y for p in points):.6f}m > 0.05m")
    expected = {"Idle": 2, "Walk": 1} if full else {"Hold": 1, "Sway": 2}
    clips = {a.name.split("|")[-1]: a for a in bpy.data.actions}
    check(name + " actions", set(clips) == set(expected), ", ".join(a.name for a in bpy.data.actions))
    for key, seconds in expected.items():
        if key not in clips:
            continue
        clip = clips[key]
        start, end = clip.frame_range
        a, b = pose(armature, clip, start), pose(armature, clip, end)
        middle = pose(armature, clip, start + (end - start) / 4)
        error = max(abs(x - y) for x, y in zip(a, b))
        motion = max(abs(x - y) for x, y in zip(a, middle))
        check(name + " " + key + " duration/loop", abs((end - start) / 30 - seconds) < .001 and error < 1e-4,
              f"{(end - start) / 30:.3f}s; seam={error:.7f}")
        check(name + " " + key + " motion", motion < 1e-5 if key == "Hold" else motion > .0001,
              f"quarter-cycle matrix delta={motion:.6f}")
        signature.append((key, [[round(v, 6) for v in pose(armature, clip, frame)] for frame in range(int(start), int(end) + 1)]))
    signature += sorted(actual.items())
    signature += [(m, tuple(round(v, 6) for v in bpy.data.materials[m].diffuse_color)) for m in sorted(materials)]
    digest = hashlib.sha256(json.dumps(sorted(signature, key=lambda entry: entry[0])).encode()).hexdigest()
    DETAILS[name] = {"bones": actual, "triangles": counts, "vertices": len(points), "weight_errors": errors,
                     "actions": sorted(clips), "content_sha256": digest}


def source_and_previews():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE / "BlockyCharacter.blend"))
    armature = bpy.data.objects["BlockyCharacter"]
    controls = [b for b in armature.data.bones if not b.use_deform]
    constraints = [c for bone in armature.pose.bones for c in bone.constraints if c.type == "IK"]
    check("Blend authoring IK", len(controls) == 8 and len(constraints) == 4 and all(
        c.chain_count == 2 and c.target == armature and c.pole_target == armature and
        c.subtarget in {b.name for b in controls} and c.pole_subtarget in {b.name for b in controls} for c in constraints),
        f"{len(controls)} non-deform controls, {len(constraints)} two-bone IK constraints")
    for name in ("front", "side", "three-quarter", "walk-mid-pose", "first-person"):
        path = REPORT / (name + ".png")
        image = bpy.data.images.load(str(path), check_existing=False)
        pixels = list(image.pixels)
        colors = {tuple(round(pixels[i + c], 3) for c in range(3)) for i in range(0, len(pixels), 16)}
        check("Preview " + name, image.size[0] >= 720 and image.size[1] >= 720 and len(colors) > 10,
              f"{image.size[0]}x{image.size[1]}, {len(colors)} sampled colors")
        bpy.data.images.remove(image)


def main():
    for name, full in (("BlockyCharacter", True), ("BlockyArmsFP", False)):
        try:
            validate(name, full)
        except Exception as exc:
            check(name + " exception", False, repr(exc))
    try:
        source_and_previews()
    except Exception as exc:
        check("Source / preview exception", False, repr(exc))
    table = "RESULT | CHECK | EVIDENCE\n" + "\n".join(" | ".join(row) for row in ROWS)
    print(table, flush=True)
    REPORT.mkdir(parents=True, exist_ok=True)
    (REPORT / "validation.txt").write_text(table + "\n", encoding="utf-8")
    (REPORT / "validation.json").write_text(json.dumps({"checks": ROWS, "models": DETAILS}, indent=2) + "\n", encoding="utf-8")
    if any(row[0] == "FAIL" for row in ROWS):
        raise RuntimeError("Blocky art validation FAILED (see table)")
    print("PASS: all blocky art checks", flush=True)


if __name__ == "__main__":
    main()
