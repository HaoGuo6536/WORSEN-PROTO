# ============================================================================
# blocky_character.py
# PURPOSE: Rebuild original, rigid-skinned box characters, actions and previews.
# ARCHITECTURAL ROLE: Offline art generator; no Unity or downloaded asset inputs.
# KEY RESPONSIBILITIES: Metre-scale geometry, deform-only FBX, editable IK source.
# DEPENDENCIES: Blender 5.2 bpy and mathutils only.
# USAGE NOTES: Run from the worktree root; overwrites only the named art outputs.
#   FBXs go to Assets/Art; the authoring .blend goes to ArtSource (outside Assets).
# Exact command (Git Bash):
# "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/blocky_character.py
# ============================================================================
import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/Art/Player/BlockyCharacter"
SOURCE = ROOT / "ArtSource/Player/BlockyCharacter"
PREVIEWS = ROOT / "Logs/AgentValidation/Art/BlockyCharacter"
FPS = 30
COLORS = {"Skin": (0.58, 0.32, 0.19, 1), "Shirt": (0.045, 0.075, 0.085, 1),
          "Trousers": (0.17, 0.205, 0.22, 1), "Shoes": (0.022, 0.027, 0.032, 1)}
# Shared arm dimensions: length along bone, transverse width, thickness (metres).
ARM_PARTS = (("UpperArm", .28, .17, .17, "Shirt"),
             ("LowerArm", .26, .135, .135, "Skin"),
             ("Hand", .16, .145, .10, "Skin"))


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    scene.render.fps = FPS
    bpy.context.preferences.filepaths.save_version = 0
    for name, color in COLORS.items():
        material = bpy.data.materials.new(name)
        material.diffuse_color = color
        material.use_nodes = True
        shader = material.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = color
        shader.inputs["Roughness"].default_value = 1


def rig(name, definitions):
    data = bpy.data.armatures.new(name + "Rig")
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, parent, head, tail in definitions:
        bone = data.edit_bones.new(name)
        bone.head, bone.tail = head, tail
        bone.use_deform = True
        bone.align_roll(Vector((0, 0, 1)) if abs((Vector(tail) - Vector(head)).normalized().z) < .99 else Vector((0, -1, 0)))
        if parent:
            bone.parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.show_in_front = True
    obj.select_set(False)
    return obj


def mesh(name, armature, boxes):
    vertices, faces, assignments, materials = [], [], [], []
    # Consistent outward winding; each disconnected box is rigidly weighted.
    quads = ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
             (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7))
    corners = ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
               (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1))
    for bone, center, size, material, rotation in boxes:
        start = len(vertices)
        transform = Matrix.Translation(Vector(center)) @ rotation.to_4x4()
        vertices.extend(transform @ Vector(tuple(c[i] * size[i] / 2 for i in range(3))) for c in corners)
        faces.extend(tuple(start + i for i in face) for face in quads)
        assignments.append((bone, list(range(start, start + 8))))
        materials.extend([list(COLORS).index(material)] * 6)
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    for material in COLORS:
        data.materials.append(bpy.data.materials[material])
    for polygon, material in zip(data.polygons, materials):
        polygon.material_index = material
        polygon.use_smooth = False
    for bone, indices in assignments:
        group = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
        group.add(indices, 1, "REPLACE")
    obj.parent = armature
    obj.modifiers.new("RigidSkin", "ARMATURE").object = armature
    return obj


def box(bone, center, size, material):
    return bone, center, size, material, Matrix.Identity(3)


def arm_boxes(armature, side):
    boxes = []
    for suffix, length, width, thickness, material in ARM_PARTS:
        bone = armature.data.bones[side + suffix]
        boxes.append((bone.name, (bone.head_local + bone.tail_local) / 2,
                      (width, length - .006, thickness), material, bone.matrix_local.to_3x3()))
    return boxes


def full_character():
    definitions = [("Hips", None, (0, 0, .9), (0, 0, 1.04)),
                   ("Spine", "Hips", (0, 0, 1.04), (0, 0, 1.23)),
                   ("Chest", "Spine", (0, 0, 1.23), (0, 0, 1.46)),
                   ("Neck", "Chest", (0, 0, 1.46), (0, 0, 1.56)),
                   ("Head", "Neck", (0, 0, 1.56), (0, 0, 1.8))]
    for side, sign in (("Left", 1), ("Right", -1)):
        definitions += [(side + "Shoulder", "Chest", (sign * .1, 0, 1.43), (sign * .24, 0, 1.43))]
        for suffix, start, end, parent in (("UpperArm", .24, .52, "Shoulder"),
                                         ("LowerArm", .52, .78, "UpperArm"), ("Hand", .78, .94, "LowerArm")):
            definitions.append((side + suffix, side + parent, (sign * start, 0, 1.43), (sign * end, 0, 1.43)))
        x = sign * .115
        definitions += [(side + "UpperLeg", "Hips", (x, 0, .9), (x, 0, .52)),
                        (side + "LowerLeg", side + "UpperLeg", (x, 0, .52), (x, 0, .13)),
                        (side + "Foot", side + "LowerLeg", (x, 0, .13), (x, -.16, .07)),
                        (side + "Toes", side + "Foot", (x, -.16, .07), (x, -.27, .07))]
    character = rig("BlockyCharacter", definitions)
    boxes = [box("Hips", (0, 0, .945), (.38, .25, .13), "Trousers"),
             box("Spine", (0, 0, 1.10), (.36, .24, .18), "Shirt"),
             box("Chest", (0, 0, 1.34), (.46, .27, .29), "Shirt"),
             box("Neck", (0, 0, 1.51), (.14, .14, .09), "Skin"),
             box("Head", (0, -.012, 1.67), (.28, .255, .26), "Skin")]
    for sign in (-1, 1):
        boxes.append(box("Head", (sign * .065, -.142, 1.692), (.027, .008, .027), "Shoes"))
    for side, sign in (("Left", 1), ("Right", -1)):
        x = sign * .115
        boxes += [box(side + "UpperLeg", (x, 0, .715), (.18, .21, .37), "Trousers"),
                  box(side + "LowerLeg", (x, 0, .33), (.155, .18, .37), "Trousers"),
                  box(side + "Foot", (x, -.03, .075), (.185, .24, .15), "Shoes"),
                  box(side + "Toes", (x, -.215, .05), (.185, .13, .10), "Shoes")]
    body = mesh("Body", character, boxes)
    arms = mesh("Arms", character, arm_boxes(character, "Left") + arm_boxes(character, "Right"))
    return character, [body, arms]


def first_person():
    definitions = [("Root", None, (0, 0, 0), (0, 0, .10))]
    for side, sign in (("Left", 1), ("Right", -1)):
        directions = [Vector((-sign * .10, -.30, .95)).normalized(),
                      Vector((-sign * .22, -.75, .624)).normalized(),
                      Vector((-sign * .12, -.98, .12)).normalized()]
        wrist = Vector((sign * .32, -.52, -.25)) - directions[2] * .08
        elbow = wrist - directions[1] * .26
        shoulder = elbow - directions[0] * .28
        points = [shoulder, elbow, wrist, wrist + directions[2] * .16]
        definitions.append((side + "Shoulder", "Root", shoulder + Vector((sign * .1, 0, -.04)), shoulder))
        for i, (suffix, _, _, _, _) in enumerate(ARM_PARTS):
            parent = "Shoulder" if i == 0 else ARM_PARTS[i - 1][0]
            definitions.append((side + suffix, side + parent, points[i], points[i + 1]))
    arms = rig("BlockyArmsFP", definitions)
    return arms, [mesh(side + "Arm", arms, arm_boxes(arms, side)) for side in ("Left", "Right")]


def clear_pose(armature):
    armature.animation_data_clear()
    for bone in armature.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
        bone.rotation_mode = "XYZ"


def action(armature, name, duration):
    clear_pose(armature)
    clip = bpy.data.actions.new(name)
    clip.use_fake_user = True
    armature.animation_data_create().action = clip
    end = FPS * duration
    for frame in range(end + 1):
        phase = 2 * math.pi * frame / end
        wave = math.sin(phase)
        for bone in armature.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
        if name == "Idle":
            armature.pose.bones["Chest"].rotation_euler.y = math.radians(1.2) * wave
            armature.pose.bones["Spine"].scale.y = 1 + .008 * wave
        elif name == "Walk":
            # Rest skeleton stays T-posed; only the action lowers the arms.
            for side, sign in (("Left", 1), ("Right", -1)):
                armature.pose.bones[side + "UpperArm"].rotation_euler.x = -math.radians(82)
                armature.pose.bones[side + "UpperArm"].rotation_euler.z = sign * math.radians(20) * wave
                armature.pose.bones[side + "LowerArm"].rotation_euler.z = math.radians(9)
                armature.pose.bones[side + "UpperLeg"].rotation_euler.x = sign * math.radians(25) * wave
                armature.pose.bones[side + "LowerLeg"].rotation_euler.x = -math.radians(30) * max(0, -sign * wave)
        elif name == "Sway":
            for side, sign in (("Left", 1), ("Right", -1)):
                armature.pose.bones[side + "Shoulder"].rotation_euler.z = sign * math.radians(1.5) * wave
        for bone in armature.pose.bones:
            for field in ("location", "rotation_euler", "scale"):
                bone.keyframe_insert(field, frame=frame, group=bone.name)
    clear_pose(armature)
    return clip


def export(armature, meshes, actions):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [armature] + meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    armature.animation_data_create()
    # NLA strips select exactly this model's actions, preventing cross-rig takes.
    for clip in actions:
        track = armature.animation_data.nla_tracks.new()
        track.name = clip.name
        track.strips.new(clip.name, 0, clip)
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = 60
    bpy.ops.export_scene.fbx(filepath=str(ART / (armature.name + ".fbx")),
        use_selection=True, object_types={"ARMATURE", "MESH"}, global_scale=1,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, use_armature_deform_only=True, add_leaf_bones=False,
        use_mesh_modifiers=True, mesh_smooth_type="FACE", use_triangles=True,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1, bake_anim_simplify_factor=0, use_custom_props=False)
    clear_pose(armature)


def controls(armature):
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    pairs = []
    collection = armature.data.collections.new("Controls (IK influence initially zero)")
    for side in ("Left", "Right"):
        for limb, lower, endpoint, pole_delta in (("Arm", "LowerArm", "Hand", (0, .4, -.2)),
                                                ("Leg", "LowerLeg", "Foot", (0, -.5, 0))):
            end = armature.data.edit_bones[side + endpoint].head.copy()
            joint = armature.data.edit_bones[side + lower].head.copy()
            names = ["CTRL_" + side + limb + suffix for suffix in ("IK", "Pole")]
            for name, position in zip(names, (end, joint + Vector(pole_delta))):
                bone = armature.data.edit_bones.new(name)
                bone.head, bone.tail = position, position + Vector((0, 0, .12))
                bone.use_deform = False
                collection.assign(bone)
            pairs.append((side + lower, names))
    bpy.ops.object.mode_set(mode="POSE")
    for lower, (target, pole) in pairs:
        constraint = armature.pose.bones[lower].constraints.new("IK")
        constraint.name = "Authoring IK (enable to pose)"
        constraint.target = constraint.pole_target = armature
        constraint.subtarget, constraint.pole_subtarget = target, pole
        constraint.chain_count = 2
        constraint.use_stretch = False
        constraint.influence = 0 # FK actions and the T-pose are the default.
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.select_set(False)


def render(name, position, target, orthographic=True):
    scene = bpy.context.scene
    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = position
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO" if orthographic else "PERSP"
    camera_data.ortho_scale = 2.35
    camera_data.sensor_fit = "VERTICAL"
    camera_data.angle = math.radians(75)
    camera_data.clip_start = .05
    scene.camera = camera
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = (900, 900) if orthographic else (1280, 720)
    scene.render.resolution_percentage = 100
    # PNG metadata otherwise includes wall-clock and render duration on every run.
    for prop in scene.render.bl_rna.properties:
        if prop.identifier.startswith("use_stamp") and prop.type == "BOOLEAN":
            setattr(scene.render, prop.identifier, False)
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "BOTH"
    scene.display.shading.background_type = "WORLD"
    scene.world = scene.world or bpy.data.worlds.new("Neutral")
    scene.world.color = (.19, .19, .19)
    scene.view_settings.view_transform = "Standard"
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(PREVIEWS / (name + ".png"))
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    bpy.data.cameras.remove(camera_data)


def main():
    ART.mkdir(parents=True, exist_ok=True)
    SOURCE.mkdir(parents=True, exist_ok=True)
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    reset()
    character, body_meshes = full_character()
    idle, walk = action(character, "Idle", 2), action(character, "Walk", 1)
    export(character, body_meshes, [idle, walk])
    fp, fp_meshes = first_person()
    hold, sway = action(fp, "Hold", 1), action(fp, "Sway", 2)
    export(fp, fp_meshes, [hold, sway])
    controls(character) # Controls never enter either FBX.
    for obj in fp_meshes:
        obj.hide_render = True
    for name, position in (("front", (0, -4, .95)), ("side", (4, 0, .95)), ("three-quarter", (3, -4, 2.4))):
        render(name, position, (0, 0, .9))
    character.animation_data_create().action = walk
    bpy.context.scene.frame_set(8)
    render("walk-mid-pose", (3, -4, 2.1), (0, 0, .9))
    clear_pose(character)
    for obj in body_meshes:
        obj.hide_render = True
    for obj in fp_meshes:
        obj.hide_render = False
    render("first-person", (0, 0, 0), (0, -1, 0), False)
    for obj in body_meshes:
        obj.hide_render = False
    for obj in fp_meshes + [fp]:
        obj.hide_set(True)
        obj.hide_render = True
    bpy.context.scene.frame_set(0)
    bpy.ops.object.select_all(action="DESELECT")
    character.select_set(True)
    bpy.context.view_layer.objects.active = character
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "BlockyCharacter.blend"))
    print("Generated original BlockyCharacter and BlockyArmsFP; no external assets used.")


if __name__ == "__main__":
    main()
