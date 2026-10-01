# ============================================================================
# hunter_mimic.py
# ============================================================================
# PURPOSE:
#   Turns the project's own cake pickup into a stationary hinged-jaw prototype.
#   Original exterior vertices, corner normals and palette UVs are retained in
#   the closed pose. No extra frosting, jam tell or idle movement reveals it.
# ARCHITECTURAL ROLE:
#   Offline art generator · no runtime layer · Hunter, SPEC-005 §2.7 / PLAN-015.
# KEY RESPONSIBILITIES:
#   - Append only the exported cake object and reuse its packed palette images.
#   - Split existing cake layers into a rigid base and hinged Jaw without scaling.
#   - Conceal teeth and gum folds and export the six-action art contract.
#   - Render an exact-position closed cake overlay for disguise review.
# DEPENDENCIES:
#   Blender 5.2 bpy/bmesh; hunter_creature_common; own WORSEN_CakePickup.blend.
# USAGE NOTES:
#   The cake source is read-only and its archived concepts are never appended.
#   Jaw hinge and motion are provisional. The root/base remain stationary;
#   disguise loops stay completely closed under the owner's current direction.
#   Add -- --lineup to generate the combined lineup after all hunters exist.
# ============================================================================
import hashlib
import math
import sys
from pathlib import Path

import bmesh
import bpy

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import hunter_creature_common as c
from hunter_detail_geometry import SculptCreature

CAKE = c.ROOT / "ArtSource/Horror/Cake/WORSEN_CakePickup.blend"
HINGE = (0, .145, .136)
PALETTE = {"Teeth": (.89, .84, .68), "Mouth": (.24, .035, .045)}


def append_cake():
    with bpy.data.libraries.load(str(CAKE), link=False) as (src, dst):
        if "WORSEN_CakePickup" not in src.objects:
            raise RuntimeError("Exported cake object not present in source")
        dst.objects = ["WORSEN_CakePickup"]
    obj = dst.objects[0]
    bpy.context.collection.objects.link(obj)
    return obj


def jaw_vertices(obj):
    adjacent = {v.index: set() for v in obj.data.vertices}
    for e in obj.data.edges:
        a, b = e.vertices
        adjacent[a].add(b)
        adjacent[b].add(a)
    remaining, jaw = set(adjacent), set()
    while remaining:
        todo, component = [min(remaining)], set()
        while todo:
            i = todo.pop()
            if i in component:
                continue
            component.add(i)
            todo.extend(adjacent[i] - component)
        remaining -= component
        # The original lower sponge ends at .136m. The next complete cream
        # component starts there; choosing whole components preserves every face.
        if min(obj.data.vertices[i].co.z for i in component) >= .13599:
            jaw.update(component)
    return jaw


def split_copy(source, keep, name):
    obj = bpy.data.objects.new(name, source.data.copy())
    obj.matrix_world = source.matrix_world.copy()
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return obj


def interior(model, name, bone, z, downward=False):
    # Inset triangular gum surface stays entirely inside the cake wedge closed.
    data = bpy.data.meshes.new(name)
    verts = [(0, -.137, z), (.094, .117, z), (-.094, .117, z)]
    data.from_pydata(verts, [], [(2, 1, 0) if downward else (0, 1, 2)])
    data.materials.append(model.materials["Mouth"])
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    model.bind(obj, bone)


def build():
    original = append_cake()
    original.name = "CakeReference"
    m = SculptCreature("Mimic", PALETTE, [("Jaw", "Root", HINGE)])
    cake_mat = original.data.materials[0]
    cake_mat.name = "M_HunterMimic_Cake"
    textures = {}
    export_dir = c.paths("Mimic")[1].parent
    export_dir.mkdir(parents=True, exist_ok=True)
    for node in cake_mat.node_tree.nodes:
        if node.type != "TEX_IMAGE":
            continue
        image = node.image
        if not image.packed_file:
            raise RuntimeError("Cake palette must be packed: " + image.name)
        filename = image.name + ".png"
        data = bytes(image.packed_file.data)
        (export_dir / filename).write_bytes(data)
        textures[filename] = hashlib.sha256(data).hexdigest()
        image.filepath = str(export_dir / filename)
        if image.name == "CakePalette":
            cake_mat.node_tree.nodes.active = node
            shader = cake_mat.node_tree.nodes.get("Principled BSDF")
            cake_mat.node_tree.links.new(node.outputs["Alpha"], shader.inputs["Alpha"])
    selected = jaw_vertices(original)
    for name, bone, keep in (("CakeBase", "Root", set(range(len(original.data.vertices))) - selected),
                             ("CakeTop", "Jaw", selected)):
        part = split_copy(original, keep, name)
        # Rigid binding deliberately marks polygons flat. Preserve the real
        # cake's authored split normals as custom normals: cream and cherry
        # must not acquire a faceted shading tell when made into a skin.
        normals = [tuple(normal.vector) for normal in part.data.corner_normals]
        m.bind(part, bone)
        part.data.normals_split_custom_set(normals)
        # FBX's final triangulation otherwise recomputes flat loop spaces and
        # damages custom normals. Triangulate explicitly while preserving them.
        triangulate = part.modifiers.new('CakeSurfaceTriangles', 'TRIANGULATE')
        triangulate.keep_custom_normals = True
        bpy.context.view_layer.objects.active = part
        bpy.ops.object.modifier_apply(modifier=triangulate.name)
    interior(m, "LowerGum", "Root", .1361)
    interior(m, "UpperGum", "Jaw", .1359, True)
    # Opposing staggered teeth nest inside opaque cake layers when closed.
    for j, y in enumerate((-.093, -.042, .012, .067, .105)):
        width = (y + .168) * .33
        for sign in (-1, 1):
            x = sign * width * .80
            m.shape("UpperTooth%d_%d" % (j, sign), "Jaw", (x, y, .124),
                    (.012, .018, .024), "Teeth", "tooth", (math.pi, 0, 0))
            m.shape("LowerTooth%d_%d" % (j, sign), "Root", (x * .83, y + .01, .148),
                    (.010, .015, .024), "Teeth", "tooth")
    # Folded palate is concealed inside opaque sponge until the lid unfolds.
    for sign in (-1, 1):
        for j, y in enumerate((.025, .085)):
            m.shape('PalateFold%d_%d' % (sign, j), 'Jaw', (sign*.025, y, .171),
                    (.028, .047, .019), 'Mouth', 'ico')
    original.hide_render = True
    original.hide_set(True)
    return m, original, textures


def motion(rig, name, t):
    if name == "ready":
        angle = -.35*t
    elif name == "attack":
        angle = c.envelope(t, [(0, -.35), (.13, -.65), (.267, -1.65), (.4, 0), (.48, -.30), (.60, 0), (1, 0)])
    elif name == "hit":
        angle = c.envelope(t, [(0, 0), (.20, -1.15), (.42, -.35), (.65, -.80), (1, -.65)])
    else:
        # All locomotion slots are exact disguise holds. No breathing lid tell.
        angle = 0
    c.delta(rig, "Jaw", (angle, 0, 0))


def main():
    before = hashlib.sha256(CAKE.read_bytes()).hexdigest()
    c.reset()
    m, reference, textures = build()
    clips = c.animate(m, motion)
    reference_bounds = c.bounds(c.points([reference]))
    # Remove the review reference before saving; it is not part of the asset.
    reference_data = reference.data.copy()
    bpy.data.objects.remove(reference, do_unlink=True)
    manifest = c.save_model(m, clips, {"attack_origin": c.socket(m.rig, "Jaw", (0, -.13, .136)),
                                      "head_or_top": c.socket(m.rig, "Jaw", (0, .046, .289090693))},
                           {"disguise": {"source": str(CAKE.relative_to(c.ROOT)).replace("\\", "/"),
                                         "source_sha256": before, "object": "WORSEN_CakePickup",
                                         "closed_tolerance_m": .005, "cake_bounds_m": reference_bounds,
                                         "packed_textures_sha256": textures}})
    # Packed images remain portable and also have worktree-relative disk fallbacks.
    for image in bpy.data.images:
        if image.packed_file:
            image.filepath = bpy.path.relpath(image.filepath, start=str(c.paths("Mimic")[0].parent))
    bpy.ops.wm.save_as_mainfile(filepath=str(c.paths("Mimic")[0]))
    c.previews(m, clips, manifest)
    reference = bpy.data.objects.new("CakeReferenceOverlay", reference_data)
    bpy.context.collection.objects.link(reference)
    reference.data.materials.clear()
    reference.data.materials.append(c.material("ReferenceCyan", (.08, .80, .90)))
    wire = reference.modifiers.new("Exact cake surface (cyan)", "WIREFRAME")
    wire.thickness, wire.use_replace = .00055, True
    c.render(c.paths("Mimic")[2] / "closed-vs-cake.png", (3, -5, 2.5), (0, 0, .145), .48, texture=True)
    if hashlib.sha256(CAKE.read_bytes()).hexdigest() != before:
        raise RuntimeError("Read-only cake source changed during generation")
    if "--lineup" in sys.argv:
        c.combined_lineup()


if __name__ == "__main__":
    main()
