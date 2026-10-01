# ============================================================================
# hunter_creature_common.py
# ============================================================================
# PURPOSE:
#   Provides the deterministic offline asset pipeline for three hunter prototypes.
#   It keeps export coordinates, manifests and independent FBX measurements alike
#   without importing Unity or changing gameplay, project settings or source art.
# ARCHITECTURAL ROLE:
#   Offline art tooling · no runtime layer · Hunter (SPEC-005 / PLAN-015).
# KEY RESPONSIBILITIES:
#   - Construct rigid primitive skins and metre-scale skeletons.
#   - Bake six named actions and export the manifest/FBX/source contract.
#   - Render neutral Workbench evidence and true-scale lineups.
#   - Measure re-imported content, validate shared invariants and hash semantics.
# DEPENDENCIES:
#   Blender 5.2 bpy/mathutils and Python standard library; no runtime systems.
# USAGE NOTES:
#   All paths resolve from this worktree. Run only in the isolated art checkout.
#   Socket offsets use Blender bone-local metres; the manifest specifies axes.
#   FBX timestamps are excluded from semantic hashes. No Unity lifecycle applies.
# ============================================================================
import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils import Euler, Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
FPS = 30
CLIPS = {"idle": 60, "walk": 30, "run": 20, "ready": 24, "attack": 30, "hit": 24}
LOOPS = {"idle", "walk", "run"}
CONTACT = 13  # 1 + 0.4 * 30; author/import ranges both start at frame 1.


def paths(name):
    stem = "WORSEN_Hunter" + name
    return (ROOT / "ArtSource/Hunter" / name / (stem + ".blend"),
            ROOT / "Assets/Art/Hunter" / name / (stem + ".fbx"),
            ROOT / "Logs/AgentValidation/Art" / ("Hunter" + name))


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    scene.render.fps = FPS


def linear(s):
    return s / 12.92 if s <= .04045 else ((s + .055) / 1.055) ** 2.4


def srgb(l):
    return 12.92 * l if l <= .0031308 else 1.055 * l ** (1 / 2.4) - .055


def material(name, color):
    m = bpy.data.materials.new(name)
    rgba = tuple(linear(c) for c in color[:3]) + (1,)
    m.diffuse_color = rgba
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = rgba
    p.inputs["Roughness"].default_value = 1
    return m


def material_row(m):
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    row = {"name": m.name,
           "base_color_srgb": [round(srgb(c), 6) for c in p.inputs["Base Color"].default_value[:3]] + [1],
           "emission_color_srgb": [round(srgb(c), 6) for c in p.inputs["Emission Color"].default_value[:3]] + [1],
           "emission_strength": round(p.inputs["Emission Strength"].default_value, 6)}
    textures = {l.to_socket.name: Path(l.from_node.image.filepath).name
                for l in m.node_tree.links if l.from_node.type == "TEX_IMAGE" and l.from_node.image}
    if textures:
        row["textures"] = textures
    return row


class Creature:
    def __init__(self, name, palette, definitions):
        self.name = name
        self.materials = {k: material("M_Hunter" + name + "_" + k, v) for k, v in palette.items()}
        self.meshes = []
        self.rig = bpy.data.objects.new("WORSEN_Hunter" + name, bpy.data.armatures.new(name + "Skeleton"))
        bpy.context.collection.objects.link(self.rig)
        bpy.context.view_layer.objects.active = self.rig
        self.rig.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        for key, parent, head in [("Root", None, (0, 0, 0))] + definitions:
            b = self.rig.data.edit_bones.new(key)
            b.head, b.tail = head, Vector(head) + Vector((0, 0, .08))
            if key == "Root":
                # Root points forward, not at the elevated Body child. FBX does
                # not store tail lengths; a collinear single child otherwise
                # becomes connected on re-import and suppresses its translation.
                b.tail = (0, -.08, 0)
            if parent:
                b.parent = self.rig.data.edit_bones[parent]
        bpy.ops.object.mode_set(mode="OBJECT")
        self.rig.select_set(False)
        self.rig.show_in_front = True

    def bind(self, obj, bone, part=None):
        obj.name = part or obj.name
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        obj.vertex_groups.clear()
        obj.vertex_groups.new(name=bone).add(list(range(len(obj.data.vertices))), 1, "REPLACE")
        for p in obj.data.polygons:
            p.use_smooth = False
        obj.parent = self.rig
        obj.modifiers.new("RigidSkin", "ARMATURE").object = self.rig
        self.meshes.append(obj)
        obj.select_set(False)
        return obj

    def shape(self, part, bone, center, size, mat, kind="box", rotation=(0, 0, 0)):
        if kind == "box":
            bpy.ops.mesh.primitive_cube_add(size=1)
        elif kind == "ico":
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1)
        elif kind == "cylinder":
            bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=1, depth=1)
        elif kind == "tooth":
            bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=1, radius2=0, depth=1)
        else:
            raise ValueError(kind)
        obj = bpy.context.object
        # Normalize primitive local bounds so size always means actual dimensions.
        obj.dimensions = size
        obj.location = center
        obj.rotation_euler = rotation
        obj.data.materials.append(self.materials[mat])
        return self.bind(obj, bone, part)

    def bar(self, part, bone, start, end, width, mat):
        a, b = Vector(start), Vector(end)
        rot = (b - a).to_track_quat("Z", "Y").to_euler()
        return self.shape(part, bone, (a + b) / 2, (width, width, (b - a).length), mat, rotation=rot)


def clear_pose(rig):
    rig.animation_data_clear()
    for b in rig.pose.bones:
        b.matrix_basis = Matrix.Identity(4)
        b.rotation_mode = "QUATERNION"
    bpy.context.view_layer.update()


def delta(rig, bone, rotation=(0, 0, 0), translation=(0, 0, 0)):
    b = rig.pose.bones[bone]
    basis = b.bone.matrix_local.to_3x3()
    b.rotation_quaternion = (basis.inverted() @ Euler(rotation).to_matrix() @ basis).to_quaternion()
    b.location = basis.inverted() @ Vector(translation)


def envelope(t, keys):
    for (a, x), (b, y) in zip(keys, keys[1:]):
        if a <= t <= b:
            return x + (y - x) * (t - a) / (b - a)
    return keys[-1][1]


def animate(model, callback):
    clips = []
    for name, duration in CLIPS.items():
        clear_pose(model.rig)
        clip = bpy.data.actions.new(name)
        clip.use_fake_user = True
        model.rig.animation_data_create().action = clip
        for f in range(1, duration + 2):
            for b in model.rig.pose.bones:
                b.matrix_basis = Matrix.Identity(4)
            t = (f - 1) / duration
            callback(model.rig, name, t)
            for b in model.rig.pose.bones:
                for field in ("location", "rotation_quaternion", "scale"):
                    b.keyframe_insert(field, frame=f, group=b.name)
        # Linear samples avoid Bezier overshoot between authored frames.
        for layer in clip.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            key.interpolation = "LINEAR"
        clips.append(clip)
    clear_pose(model.rig)
    return clips


def points(meshes, evaluated=False):
    graph = bpy.context.evaluated_depsgraph_get()
    return [o.matrix_world @ v.co for obj in meshes
            for o in [obj.evaluated_get(graph) if evaluated else obj] for v in o.data.vertices]


def bounds(vertices):
    return [[min(p[i] for p in vertices), max(p[i] for p in vertices)] for i in range(3)]


def triangles(meshes):
    return sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons)


def socket(rig, bone, world_position):
    return {"bone": bone, "local_offset": list(rig.data.bones[bone].matrix_local.inverted() @ Vector(world_position))}


def save_model(model, clips, sockets, extra=None):
    source, fbx, previews = paths(model.name)
    for p in (source.parent, fbx.parent, previews):
        p.mkdir(parents=True, exist_ok=True)
    clear_pose(model.rig)
    box = bounds(points(model.meshes))
    manifest = {"schema_version": 1, "hunter": model.name, "fps": FPS,
                "coordinates": {"source_up": "+Z", "source_forward": "-Y", "fbx_up": "+Y",
                                "fbx_forward": "-Z", "bake_space_transform": True,
                                "unity_bakeAxisConversion": False, "socket_space": "Blender bone-local metres"},
                "height_m": box[2][1] - box[2][0], "width_m": box[0][1] - box[0][0],
                "bounds_m": box, "triangles": triangles(model.meshes),
                "bones": [{"name": b.name, "parent": b.parent.name if b.parent else None,
                           "head_m": list(b.head_local), "rest_matrix": [list(r) for r in b.matrix_local]}
                          for b in model.rig.data.bones],
                "actions": [{"name": n, "frames": [1, d + 1], "loop": n in LOOPS,
                             **({"contact_frame": CONTACT} if n == "attack" else {})} for n, d in CLIPS.items()],
                "materials": [material_row(m) for m in sorted(set(m for o in model.meshes for m in o.data.materials), key=lambda m: m.name)],
                "sockets": sockets}
    if extra:
        manifest.update(extra)
    fbx.with_suffix(".manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [model.rig] + model.meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = model.rig
    for clip in clips:
        track = model.rig.animation_data_create().nla_tracks.new()
        track.name = clip.name
        track.strips.new(clip.name, 1, clip)
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, 61
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={"ARMATURE", "MESH"},
        global_scale=1, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, use_armature_deform_only=True, add_leaf_bones=False,
        use_mesh_modifiers=True, mesh_smooth_type="FACE", use_triangles=True,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1, bake_anim_simplify_factor=0, path_mode="COPY", embed_textures=True)
    clear_pose(model.rig)
    bpy.context.scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    return manifest


def pose(rig, clip, frame):
    rig.animation_data_create().action = clip
    if clip.slots:
        rig.animation_data.action_slot = clip.slots[0]
    bpy.context.scene.frame_set(int(frame), subframe=frame % 1)
    bpy.context.view_layer.update()
    return [v for b in rig.pose.bones for row in b.matrix for v in row]


def render(path, position, target, scale, texture=False, wide=False):
    scene = bpy.context.scene
    data = bpy.data.cameras.new("EvidenceCamera")
    camera = bpy.data.objects.new("EvidenceCamera", data)
    scene.collection.objects.link(camera)
    camera.location = position
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    data.type, data.ortho_scale = "ORTHO", scale
    data.sensor_fit = "VERTICAL"
    scene.camera = camera
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = (1600, 800) if wide else (900, 900)
    scene.render.resolution_percentage = 100
    for p in scene.render.bl_rna.properties:
        if p.identifier.startswith("use_stamp") and p.type == "BOOLEAN":
            setattr(scene.render, p.identifier, False)
    shading = scene.display.shading
    shading.light, shading.color_type = "STUDIO", "TEXTURE" if texture else "MATERIAL"
    shading.show_shadows, shading.show_cavity = True, True
    shading.cavity_type, shading.background_type = "BOTH", "WORLD"
    scene.world = scene.world or bpy.data.worlds.new("Neutral")
    scene.world.color = (.17, .17, .17)
    scene.view_settings.view_transform = "Standard"
    scene.render.image_settings.file_format = "PNG"
    path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    bpy.data.cameras.remove(data)


def reference_box(x):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, 0, .9))
    obj = bpy.context.object
    obj.name = "Reference_1.8m"
    obj.dimensions = (.35, .35, 1.8)
    obj.data.materials.append(material("ReferenceNeutral", (.42, .46, .49)))
    return obj


def previews(model, clips, manifest):
    folder = paths(model.name)[2]
    h, w = manifest["height_m"], manifest["width_m"]
    scale = max(h, w, .4) * 1.4
    for n, p in (("front", (0, -5, h / 2)), ("side", (5, 0, h / 2)),
                 ("three-quarter", (3, -5, h / 2 + 2))):
        render(folder / (n + ".png"), p, (0, 0, h / 2), scale, model.name == "Mimic")
    attack = next(a for a in clips if a.name == "attack")
    # Mimic's bite closes on contact: frame 9 shows the open anticipation.
    pose(model.rig, attack, 9 if model.name == "Mimic" else CONTACT)
    render(folder / "attack-pose.png", (3, -5, h / 2 + 2), (0, 0, h / 2), scale * 1.2, model.name == "Mimic")
    clear_pose(model.rig)
    x = w / 2 + .5
    ref = reference_box(x)
    render(folder / "lineup.png", (0, -6, 1.15), (x / 2, 0, .9), max(2.25, x + w / 2 + .6), model.name == "Mimic")
    bpy.data.objects.remove(ref, do_unlink=True)


def combined_lineup():
    reset()
    for name, x in (("Weaver", -1.7), ("Ticking", .2), ("Mimic", 1.35)):
        source = paths(name)[0]
        if not source.exists():
            raise FileNotFoundError("Generate all three hunters before the combined lineup: " + str(source))
        with bpy.data.libraries.load(str(source), link=False) as (src, dst):
            dst.objects = [n for n in src.objects if n == "WORSEN_Hunter" + name or not n.startswith("Evidence")]
        for obj in dst.objects:
            if obj.type in {"MESH", "ARMATURE"}:
                bpy.context.collection.objects.link(obj)
        rig = next(o for o in dst.objects if o.type == "ARMATURE")
        clear_pose(rig)
        rig.location.x = x
    reference_box(2.2)
    bpy.context.view_layer.update()
    render(ROOT / "Logs/AgentValidation/Art/HunterLineup-hunter-art-b.png",
           (0, -8, 2.2), (0, 0, .85), 3.2, texture=True, wide=True)


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def rounded(values):
    return [round(v, 6) for v in values]


class Validation:
    def __init__(self, name):
        self.name = name
        self.source, self.fbx, self.folder = paths(name)
        self.manifest = json.loads(self.fbx.with_suffix(".manifest.json").read_text(encoding="utf-8"))
        self.checks, self.signature = [], {}
        self.report = {"hunter": name}
        reset()
        bpy.ops.import_scene.fbx(filepath=str(self.fbx), use_anim=True,
                                automatic_bone_orientation=False, ignore_leaf_bones=False)
        self.meshes = sorted((o for o in bpy.context.scene.objects if o.type == "MESH"), key=lambda o: o.name)
        rigs = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
        self.check("one_armature", len(rigs) == 1, len(rigs))
        if len(rigs) != 1:
            self.finish()
        self.rig = rigs[0]
        self.clips = {a.name.split("|")[-1]: a for a in bpy.data.actions}
        clear_pose(self.rig)

    def check(self, label, passed, evidence):
        self.checks.append({"check": label, "passed": bool(passed), "evidence": evidence})

    def shared(self, expected_bones, height_range, facing):
        m, rig = self.manifest, self.rig
        ps = points(self.meshes)
        box = bounds(ps)
        height, width, tris = box[2][1] - box[2][0], box[0][1] - box[0][0], triangles(self.meshes)
        actual = {b.name: b.parent.name if b.parent else None for b in rig.data.bones}
        self.check("bone_count_and_hierarchy", actual == expected_bones, actual)
        self.check("root_at_origin", (rig.matrix_world @ rig.data.bones["Root"].head_local).length < 1e-5 and actual["Root"] is None, list(rig.data.bones["Root"].head_local))
        self.check("height_ground", height_range[0] <= height <= height_range[1] and abs(box[2][0]) < 1e-5, box)
        self.check("triangles", 0 < tris <= 4000, tris)
        self.check("facing_minus_y", facing(self), "geometric front/back landmarks after FBX re-import")
        errors, geometry = [], []
        for o in self.meshes:
            for v in o.data.vertices:
                if len(v.groups) != 1 or abs(v.groups[0].weight - 1) > 1e-6 or o.vertex_groups[v.groups[0].group].name not in actual:
                    errors.append([o.name, v.index])
            self.check("skin_" + o.name, o.parent == rig and len([x for x in o.modifiers if x.type == "ARMATURE" and x.object == rig]) == 1, "one rigid skin")
            geometry.append([o.name, [rounded(o.matrix_world @ v.co) for v in o.data.vertices],
                             [list(p.vertices) + [p.material_index] for p in o.data.polygons],
                             [[(o.vertex_groups[g.group].name, round(g.weight, 6)) for g in v.groups] for v in o.data.vertices],
                             [[rounded(d.uv) for d in uv.data] for uv in o.data.uv_layers],
                             [mat.name for mat in o.data.materials]])
        self.check("rigid_weights", not errors, errors)
        self.check("flat_shading", all(not p.use_smooth for o in self.meshes for p in o.data.polygons), "all polygon normals flat")
        evaluated = points(self.meshes, True)
        bind_error = max((a - b).length for a, b in zip(ps, evaluated))
        self.check("bind_pose", bind_error < 1e-5, bind_error)
        self.check("six_actions", set(self.clips) == set(CLIPS) and len(bpy.data.actions) == 6, sorted(self.clips))
        self.check("fps", bpy.context.scene.render.fps == 30 and m["fps"] == 30, bpy.context.scene.render.fps)
        actions, samples = [], {}
        for name, duration in CLIPS.items():
            if name not in self.clips:
                continue
            clip = self.clips[name]
            start, end = clip.frame_range
            matrices = [pose(rig, clip, f) for f in range(int(start), int(end) + 1)]
            seam = max(abs(a - b) for a, b in zip(matrices[0], matrices[-1]))
            movement = max(abs(a - b) for sample in matrices for a, b in zip(matrices[0], sample))
            self.check(name + "_frames", abs(start - 1) < .001 and abs(end - (duration + 1)) < .001, [start, end])
            if name in LOOPS:
                self.check(name + "_loop", seam < 1e-4, seam)
            self.check(name + "_nonempty", len(matrices) > 1 and (movement > 1e-5 or self.name == "Mimic" and name in LOOPS), movement)
            root_samples = []
            for f in range(int(start), int(end) + 1):
                pose(rig, clip, f)
                root_samples.append(list(rig.matrix_world @ rig.pose.bones["Root"].head))
            self.check(name + "_in_place", max(abs(x) for p in root_samples for x in p) < 1e-5, root_samples[0])
            row = {"name": name, "frames": [round(start), round(end)], "loop": name in LOOPS}
            if name == "attack":
                row["contact_frame"] = CONTACT
            actions.append(row)
            samples[name] = [rounded(sample) for sample in matrices]
        clear_pose(rig)
        materials = [material_row(mat) for mat in sorted(set(mat for o in self.meshes for mat in o.data.materials), key=lambda x: x.name)]
        self.check("material_names", all(x["name"].startswith("M_Hunter" + self.name + "_") for x in materials), [x["name"] for x in materials])
        if self.name != "Mimic":
            self.check("no_textures", all(not x.get("textures") for x in materials), materials)
        mat_match = len(materials) == len(m["materials"])
        for a, b in zip(materials, m["materials"]):
            mat_match &= a["name"] == b["name"] and all(abs(x - y) < .002 for k in ("base_color_srgb", "emission_color_srgb") for x, y in zip(a[k], b[k])) and abs(a["emission_strength"] - b["emission_strength"]) < .002 and a.get("textures", {}) == b.get("textures", {})
        self.check("manifest_materials", mat_match, materials)
        self.check("manifest_geometry", abs(height - m["height_m"]) < 1e-5 and abs(width - m["width_m"]) < 1e-5 and tris == m["triangles"] and max(abs(box[i][j] - m["bounds_m"][i][j]) for i in range(3) for j in range(2)) < 1e-5, {"height_m": height, "width_m": width, "triangles": tris})
        self.check("manifest_bones", actual == {b["name"]: b["parent"] for b in m["bones"]} and all((rig.matrix_world @ rig.data.bones[b["name"]].head_local - Vector(b["head_m"])).length < 1e-5 for b in m["bones"]), len(actual))
        matrix_error = max(abs(a - b) for row in m["bones"]
                           for actual_row, saved_row in zip(rig.matrix_world @ rig.data.bones[row["name"]].matrix_local, row["rest_matrix"])
                           for a, b in zip(actual_row, saved_row))
        self.check("manifest_rest_matrices", matrix_error < 1e-5, matrix_error)
        self.check("manifest_actions", actions == m["actions"], actions)
        self.check("manifest_sockets", set(m["sockets"]) == {"attack_origin", "head_or_top"} and all(s["bone"] in actual and len(s["local_offset"]) == 3 and all(math.isfinite(v) for v in s["local_offset"]) for s in m["sockets"].values()), m["sockets"])
        self.report.update(height_m=height, width_m=width, triangles=tris, bones=actual, bone_count=len(actual), actions=actions)
        self.signature.update(geometry=geometry, bones=[(b.name, rounded(v for r in rig.matrix_world @ b.matrix_local for v in r)) for b in rig.data.bones], actions=samples, materials=materials, manifest=m)
        for name in ("front", "side", "three-quarter", "attack-pose", "lineup"):
            image = bpy.data.images.load(str(self.folder / (name + ".png")), check_existing=False)
            pix = list(image.pixels)
            colors = {tuple(round(pix[i + c], 3) for c in range(3)) for i in range(0, len(pix), 128)}
            self.check("preview_" + name, min(image.size) >= 800 and len(colors) > 10, {"size": list(image.size), "sample_colors": len(colors)})
            bpy.data.images.remove(image)

    def sockets(self, expected):
        errors = {}
        for key, (bone, position) in expected.items():
            s = self.manifest["sockets"][key]
            world = self.rig.matrix_world @ self.rig.data.bones[s["bone"]].matrix_local @ Vector(s["local_offset"])
            errors[key] = (world - Vector(position)).length if s["bone"] == bone else 1000
        self.check("manifest_socket_positions", max(errors.values()) < 1e-5, errors)

    def source_motion(self):
        # Verify source-to-FBX deformation matrices, not only varying F-curves.
        # A Blender-imported connected child can contain animated locations yet
        # ignore them when evaluating its pose; this catches that export failure.
        with bpy.data.libraries.load(str(self.source), link=False) as (src, dst):
            dst.objects = ["WORSEN_Hunter" + self.name]
            dst.actions = list(CLIPS)
        source_rig = dst.objects[0]
        bpy.context.collection.objects.link(source_rig)
        errors = {}
        for name, source_clip in zip(CLIPS, dst.actions):
            imported_clip = self.clips[name]
            error = 0
            for f in range(1, CLIPS[name] + 2):
                pose(source_rig, source_clip, f)
                pose(self.rig, imported_clip, f)
                for bone in self.rig.data.bones:
                    a = source_rig.matrix_world @ source_rig.pose.bones[bone.name].matrix
                    b = self.rig.matrix_world @ self.rig.pose.bones[bone.name].matrix
                    error = max(error, max(abs(x - y) for u, v in zip(a, b) for x, y in zip(u, v)))
            errors[name] = error
        self.check("source_fbx_motion_agreement", max(errors.values()) < 1e-4, errors)
        clear_pose(self.rig)
        bpy.data.objects.remove(source_rig, do_unlink=True)

    def finish(self):
        self.report["checks"] = self.checks
        self.report["manifest_agreement"] = all(c["passed"] for c in self.checks if c["check"].startswith("manifest_"))
        self.report["passed"] = all(c["passed"] for c in self.checks)
        self.report["content_sha256"] = digest(self.signature)
        self.folder.mkdir(parents=True, exist_ok=True)
        (self.folder / "validation.json").write_text(json.dumps(self.report, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(self.report, indent=2), flush=True)
        if not self.report["passed"]:
            raise RuntimeError(self.name + " FBX contract FAILED")


def center(validation, name):
    obj = next(o for o in validation.meshes if o.name == name)
    ps = [obj.matrix_world @ v.co for v in obj.data.vertices]
    return sum(ps, Vector()) / len(ps)
