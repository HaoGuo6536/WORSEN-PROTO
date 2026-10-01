# ============================================================================
# validate_hunter_mimic.py
# ============================================================================
# PURPOSE:
#   Re-imports Mimic and independently appends the original cake for comparison.
#   It proves the closed exterior and palette UVs survived export, checks that
#   mouth geometry stays inside the disguise and all disguise slots are still.
#   Bite/recovery retain the readable motion thresholds of the earlier pass.
# ARCHITECTURAL ROLE:
#   Offline art validator · no runtime layer · Hunter / PLAN-015.
# KEY RESPONSIBILITIES:
#   - Validate the two-bone rigid FBX and all six correctly ranged actions.
#   - Compare closed cake bounds, exterior surface samples and UV correspondence.
#   - Verify concealed rest teeth, in-place jaw cycles, bite and palette provenance.
#   - Write failure evidence and content hashes without modifying the cake source.
# DEPENDENCIES:
#   Blender 5.2 bpy/mathutils; hunter_creature_common; own cake source read-only.
# USAGE NOTES:
#   Use --background --factory-startup --python-exit-code 1. No Unity operations.
#   Surface error is bidirectional in metres; source and FBX both use Blender Z-up.
# ============================================================================
import hashlib
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import hunter_creature_common as c
from hunter_animation_review import validate_motion, skin_points, render_strips


def surface(objects):
    vertices, faces, samples, uv = [], [], [], set()
    for obj in objects:
        start = len(vertices)
        local = [obj.matrix_world @ v.co for v in obj.data.vertices]
        vertices.extend(local)
        obj.data.calc_loop_triangles()
        for tri in obj.data.loop_triangles:
            faces.append(tuple(start + i for i in tri.vertices))
            samples.append(sum((local[i] for i in tri.vertices), Vector()) / 3)
        layer = obj.data.uv_layers.active
        for loop in obj.data.loops:
            uv.add(tuple(c.rounded(local[loop.vertex_index])) + tuple(c.rounded(layer.data[loop.index].uv)))
    return BVHTree.FromPolygons(vertices, faces, all_triangles=True), vertices + samples, uv


def enclosed(tree, p):
    # Oblique ray avoids triangulated coplanar diagonals. Stepping past each hit
    # counts distinct boundary crossings, not the number of triangles at an edge.
    direction = Vector((.381, .517, .766)).normalized()
    origin, crossings = p.copy(), 0
    for _ in range(128):
        hit, _, _, _ = tree.ray_cast(origin, direction, 5)
        if hit is None:
            return crossings % 2 == 1
        crossings += 1
        origin = hit + direction * 1e-7
    raise RuntimeError("Cake containment ray did not terminate")


def component_trees(obj):
    # The cake is a union of individually closed layers. Ray parity across their
    # entire triangle soup is incorrect at touching caps or overlapping frosting.
    vertices = [obj.matrix_world @ v.co for v in obj.data.vertices]
    adjacent = {v.index: set() for v in obj.data.vertices}
    for edge in obj.data.edges:
        a, b = edge.vertices
        adjacent[a].add(b)
        adjacent[b].add(a)
    remaining, trees = set(adjacent), []
    obj.data.calc_loop_triangles()
    while remaining:
        todo, found = [min(remaining)], set()
        while todo:
            i = todo.pop()
            if i not in found:
                found.add(i)
                todo.extend(adjacent[i] - found)
        remaining -= found
        triangles = [tuple(t.vertices) for t in obj.data.loop_triangles if t.vertices[0] in found]
        trees.append(BVHTree.FromPolygons(vertices, triangles, all_triangles=True))
    return trees


def uv_error(a, b):
    # Compare position/UV pairs by tolerance, not decimal bins: a sub-micron
    # FBX axis round trip can put identical points on opposite rounding edges.
    def error(source, target):
        return max(min(max(abs(x - y) for x, y in zip(p, q)) for q in target) for p in source)
    return max(error(a, b), error(b, a))


def normal_error(objects, reference):
    def samples(meshes):
        result = []
        for obj in meshes:
            normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
            for loop, normal in zip(obj.data.loops, obj.data.corner_normals):
                result.append((obj.matrix_world @ obj.data.vertices[loop.vertex_index].co,
                               (normal_matrix @ normal.vector).normalized()))
        return result
    def compare(source, target):
        tree = KDTree(len(target))
        for i, (position, _) in enumerate(target):
            tree.insert(position, i)
        tree.balance()
        return max(min(((normal-target[i][1]).length for _,i,_ in tree.find_range(position, 2e-6)),
                       default=2) for position,normal in source)
    actual, expected = samples(objects), samples([reference])
    return max(compare(actual, expected), compare(expected, actual))


def main():
    v = c.Validation("Mimic")
    try:
        v.shared({"Root": None, "Jaw": "Root"}, (.28908, .28910),
                 lambda a: a.rig.data.bones["Jaw"].head_local.y > .14 and v.manifest["sockets"]["attack_origin"]["bone"] == "Jaw")
        v.sockets({"attack_origin": ("Jaw", (0, -.13, .136)), "head_or_top": ("Jaw", (0, .046, .289090693))})
        cake = c.ROOT / "ArtSource/Horror/Cake/WORSEN_CakePickup.blend"
        before = hashlib.sha256(cake.read_bytes()).hexdigest()
        with bpy.data.libraries.load(str(cake), link=False) as (src, dst):
            dst.objects = ["WORSEN_CakePickup"]
        reference = dst.objects[0]
        bpy.context.collection.objects.link(reference)
        exterior = [o for o in v.meshes if o.name in {"CakeBase", "CakeTop"}]
        v.check("split_base_and_top", len(exterior) == 2 and {g.name for o in exterior for g in o.vertex_groups} == {"Root", "Jaw"}, [o.name for o in exterior])
        tree, samples, uv = surface(exterior)
        cake_tree, cake_samples, cake_uv = surface([reference])
        surface_error = max([cake_tree.find_nearest(p)[3] for p in samples] + [tree.find_nearest(p)[3] for p in cake_samples])
        cake_bounds = c.bounds(c.points([reference]))
        closed_bounds = c.bounds(c.points(v.meshes))
        bounds_error = max(abs(a - b) for x, y in zip(cake_bounds, closed_bounds) for a, b in zip(x, y))
        v.check("closed_surface_within_5mm", surface_error <= .005, surface_error)
        v.check("closed_bounds_exact", bounds_error < 1e-6, bounds_error)
        correspondence_error = uv_error(uv, cake_uv)
        v.check("cake_uv_preserved", len(uv) == len(cake_uv) and correspondence_error < 1.1e-6,
                {"reference_pairs": len(cake_uv), "mimic_pairs": len(uv), "max_pair_error": correspondence_error})
        shading_error = normal_error(exterior, reference)
        v.check('authored_cake_corner_normals_preserved', shading_error < .002, shading_error)
        # Teeth and gum are tested against the closed original cake, not merely
        # against its axis-aligned box (which would accept protruding wedge teeth).
        detail = [o for o in v.meshes if o.name.startswith(('Frosting_', 'JamTell'))]
        v.check('no_added_disguise_tells', not detail, [o.name for o in detail])
        hidden = [o for o in v.meshes if o not in exterior]
        solids = component_trees(reference)
        outside = []
        for obj in hidden:
            for vertex in obj.data.vertices:
                p = obj.matrix_world @ vertex.co
                distance = cake_tree.find_nearest(p)[3]
                if distance > 1e-6 and not any(enclosed(solid, p) for solid in solids):
                    outside.append([obj.name, vertex.index, list(p)])
        v.check("teeth_and_mouth_concealed", not outside, outside)
        # The owner now requires exact stillness, not the old breathing/pulsing
        # locomotion tests. Measure every evaluated skin vertex in every frame.
        c.clear_pose(v.rig)
        closed = skin_points(v.meshes)
        for role in ('idle', 'walk', 'run'):
            error = 0
            for frame in range(1, c.CLIPS[role]+2):
                c.pose(v.rig, v.clips[role], frame)
                error = max(error, max((a-b).length for a,b in zip(closed,skin_points(v.meshes))))
            v.check(role+'_exact_disguise_hold', error < 1e-6, error)
        v.report['motion'] = validate_motion('Mimic',v.rig,v.meshes,
            {role:v.clips[role] for role in ('ready','attack','hit')},
            dict(ready=.07,attack=.20,hit=.15),v.check)
        if '--render' in sys.argv:
            reference.hide_render = True
            render_strips('Mimic',v.rig,v.meshes,v.clips)
        c.pose(v.rig, v.clips["idle"], 1)
        rest = v.rig.pose.bones["Jaw"].matrix.to_quaternion()
        c.pose(v.rig, v.clips["attack"], 9)
        open_angle = rest.rotation_difference(v.rig.pose.bones["Jaw"].matrix.to_quaternion()).angle
        c.pose(v.rig, v.clips["attack"], 13)
        contact_angle = rest.rotation_difference(v.rig.pose.bones["Jaw"].matrix.to_quaternion()).angle
        v.check("bite_opens_then_contacts_at_40pct", open_angle > 1.1 and contact_angle < 1e-4, {"open_radians": open_angle, "contact_radians": contact_angle, "contact_frame": 13})
        c.pose(v.rig, v.clips["hit"], 25)
        slump = rest.rotation_difference(v.rig.pose.bones["Jaw"].matrix.to_quaternion()).angle
        v.check("hit_slumped_open", .60 < slump < .70, slump)
        c.clear_pose(v.rig)
        original_images = {node.image.name.split(".")[0]: node.image for node in reference.data.materials[0].node_tree.nodes if node.type == "TEX_IMAGE"}
        palette_hashes = {}
        for key in ("CakePalette", "CakeEmission"):
            image = original_images[key]
            source_hash = hashlib.sha256(bytes(image.packed_file.data)).hexdigest()
            shipped_hash = hashlib.sha256((v.fbx.parent / (key + ".png")).read_bytes()).hexdigest()
            palette_hashes[key + ".png"] = shipped_hash
            v.check(key + "_packed_palette_exact", source_hash == shipped_hash, shipped_hash)
            imported_material = next(mat for obj in exterior for mat in obj.data.materials if mat.name == "M_HunterMimic_Cake")
            imported_image = next(node.image for node in imported_material.node_tree.nodes
                                  if node.type == "TEX_IMAGE" and Path(node.image.filepath).stem == key)
            pixel_error = max(abs(a - b) for a, b in zip(image.pixels, imported_image.pixels))
            v.check(key + "_fbx_pixels", list(image.size) == list(imported_image.size) and pixel_error < 1e-6, pixel_error)
        v.check("manifest_disguise", before == v.manifest["disguise"]["source_sha256"] and palette_hashes == v.manifest["disguise"]["packed_textures_sha256"] and max(abs(a - b) for x, y in zip(cake_bounds, v.manifest["disguise"]["cake_bounds_m"]) for a, b in zip(x, y)) < 1e-6, before)
        v.check("overlay_preview", (v.folder / "closed-vs-cake.png").is_file(), "cyan original wire surface over closed Mimic")
        v.check("cake_source_unchanged", before == hashlib.sha256(cake.read_bytes()).hexdigest(), before)
        v.report["disguise"] = {"surface_error_m": surface_error, "bounds_error_m": bounds_error,
                                "source_sha256": before, "packed_textures_sha256": palette_hashes}
        v.signature["disguise"] = v.report["disguise"]
        v.source_motion()
    except Exception as exc:
        v.check("exception", False, repr(exc))
    v.finish()


if __name__ == "__main__":
    main()
