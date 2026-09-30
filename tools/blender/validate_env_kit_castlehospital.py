# ============================================================================
# validate_env_kit_castlehospital.py
# PURPOSE:
#   Independently inspect the delivered kits, rather than trusting generator
#   assertions. Re-import every FBX and compare measured geometry with the fixed
#   owner contract, its manifest, and the editable Blender source.
# ARCHITECTURAL ROLE: Offline art validator; no Unity runtime dependencies.
# KEY RESPONSIBILITIES:
#   - Check inventory, dimensions, pivots, axes, budgets and material slots.
#   - Check exact straight/curved seams and clear door/window openings.
#   - Check source/export agreement, textures, previews and repeat manifests.
# DEPENDENCIES: Blender 5.2 bpy/mathutils/io_scene_fbx, bundled NumPy, Python stdlib.
# USAGE NOTES:
#   Blender --background --factory-startup --python-exit-code 1 --python this-file
#   -- --theme all [--record-baseline | --compare-baseline] [--skip-previews].
#   Imported local vertices retain FBX Y-up coordinates; the importer's +90-degree
#   X object rotation restores Blender Z-up. Raw FBX model transforms must be identity.
#   Evidence lives under Logs/AgentValidation/Art/EnvCastle and EnvHospital.
# ============================================================================
import argparse
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
HEIGHTS = {"castle": 7.0, "hospital": 3.6}
COMMON = {"wall_2m": "wall", "wall_door_4m": "door", "wall_window_2m": "window",
          "wall_arc_r4": "arc", "wall_arc_r6": "arc", "wall_arc_r8": "arc",
          "corner_in": "corner", "corner_out": "corner", "pillar": "pillar",
          "floor_2x2": "floor", "ceiling_2x2": "ceiling", "trim_base_2m": "trim"}
PROPS = {"castle": ("prop_torch_sconce", "prop_banner", "prop_barrel", "prop_rubble"),
         "hospital": ("prop_bed", "prop_curtain_rail", "prop_cabinet", "prop_wheelchair")}
SURFACES = {"castle": {"stone", "stone_dark", "mortar", "wood", "metal", "cloth", "ember"},
            "hospital": {"plaster", "tile", "tile_stained", "metal", "rubber", "fabric", "glass"}}
TOLERANCE = .01
SEAM_TOLERANCE = .00001


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def close(a, b, tolerance=TOLERANCE):
    return len(a) == len(b) and all(abs(x-y) <= tolerance for x, y in zip(a, b))


def bounds(vertices):
    return ([min(v[i] for v in vertices) for i in range(3)],
            [max(v[i] for v in vertices) for i in range(3)])


def mesh_hash(obj, source=False):
    # Independent implementation of the published five-decimal triangle digest.
    rows = []
    for polygon in obj.data.polygons:
        points = []
        for index in polygon.vertices:
            p = obj.data.vertices[index].co
            xyz = (p.x, p.z, -p.y) if source else tuple(p)
            points.append(tuple(0.0 if abs(x) < .000005 else round(x, 5) for x in xyz))
        rows.append((obj.data.materials[polygon.material_index].name, sorted(points)))
    return hashlib.sha256(json.dumps(sorted(rows), separators=(",", ":")).encode()).hexdigest()


def properties(element):
    node = next((e for e in element.elems if e.id == b"Properties70"), None)
    return {p.props[0].decode(): p.props[4:] for p in node.elems} if node else {}


def raw_fbx_contract(path):
    root, version = parse_fbx.parse(str(path))
    require(version == 7400, f"{path.name}: unexpected FBX version {version}")
    settings = properties(next(e for e in root.elems if e.id == b"GlobalSettings"))
    # FBX's FrontAxisSign describes parity, not the Blender export UI's forward
    # vector. This is Blender's actual -Z forward / Y up encoding.
    expected = {"UpAxis": 1, "UpAxisSign": 1, "FrontAxis": 2,
                "FrontAxisSign": 1, "CoordAxis": 0, "CoordAxisSign": 1,
                "UnitScaleFactor": 100.0}
    require(all(settings[k] == [v] for k, v in expected.items()), f"{path.name}: axes/units {settings}")
    objects = next(e for e in root.elems if e.id == b"Objects")
    models = [e for e in objects.elems if e.id == b"Model"]
    require(len(models) == 1, f"{path.name}: model count {len(models)}")
    props = properties(models[0])
    for key, default in (("Lcl Translation", [0, 0, 0]), ("Lcl Rotation", [0, 0, 0]),
                         ("Lcl Scaling", [1, 1, 1]), ("GeometricTranslation", [0, 0, 0]),
                         ("GeometricRotation", [0, 0, 0]), ("GeometricScaling", [1, 1, 1])):
        require(close(props.get(key, default), default, 1e-6), f"{path.name}: unapplied {key}")


def seam_match(left, right):
    a = {tuple(round(x, 5) for x in p) for p in left}
    b = {tuple(round(x, 5) for x in p) for p in right}
    return len(a) >= 4 and a == b


def straight_edges(vertices, width):
    return ([p for p in vertices if abs(p[0]+width/2) < SEAM_TOLERANCE],
            [p for p in vertices if abs(p[0]-width/2) < SEAM_TOLERANCE])


def test_opening(obj, width, bottom, top):
    points = [v.co.copy() for v in obj.data.vertices]
    tree = BVHTree.FromPolygons(points, [tuple(p.vertices) for p in obj.data.polygons], all_triangles=True)
    # Ray probes include near-jamb and near-lintel positions, both directions.
    for x in (-width/2+.001, -width/4, 0, width/4, width/2-.001):
        for y in (bottom+.001, (bottom+top)/2, top-.001):
            for z, direction in ((-1, (0, 0, 1)), (1, (0, 0, -1))):
                require(tree.ray_cast(Vector((x, y, z)), Vector(direction), 2)[0] is None,
                        f"{obj.name}: obstructed opening at {(x,y,z)}")
    # Exact geometric jamb and lintel surfaces, not just empty sampled space.
    for x in (-width/2, width/2):
        require(any(abs(p.x-x) < 1e-5 and abs(p.y-top) < 1e-5 for p in points),
                f"{obj.name}: missing opening boundary {x}/{top}")
    return tree


def check_arc(vertices, radius, degrees, height):
    half = math.radians(degrees)/2
    ends = []
    for sign in (-1, 1):
        theta = sign*half
        actual = [p for p in vertices if abs(math.atan2(p[0], p[2]+radius)-theta) < 1e-6]
        expected = [(r*math.sin(theta), y, r*math.cos(theta)-radius)
                    for r in (radius-.25, radius+.25) for y in (0, height)]
        require(seam_match(actual, expected), f"r{radius}: radius/angle/end plane mismatch")
        ends.append(actual)
    # Rotate a neighbouring segment around the room centre, not around its pivot.
    angle = math.radians(degrees)
    rotated = [(p[0]*math.cos(angle)+(p[2]+radius)*math.sin(angle), p[1],
                -p[0]*math.sin(angle)+(p[2]+radius)*math.cos(angle)-radius) for p in ends[0]]
    require(seam_match(rotated, ends[1]), f"r{radius}: adjacent rotated seams disagree")


def expected_size(theme, piece):
    h = HEIGHTS[theme]
    fixed = {"wall_2m": (2, h, .5), "wall_door_4m": (4, h, .5),
             "wall_window_2m": (2, h, .5), "floor_2x2": (2, .16, 2),
             "ceiling_2x2": (2, .18, 2), "trim_base_2m": (2, .18, .12),
             "corner_in": (.5, h, .5), "corner_out": (.5, h, .5),
             "pillar": (.8, h, .8) if theme == "castle" else (.6, h, .6)}
    # Prop sizes are provisional authored baselines, NOT owner contract values.
    authored = {"prop_torch_sconce": (.24, 1.045, .464127), "prop_banner": (1.1, 2.283287, .146665),
                "prop_barrel": (.914, 1.13, .914), "prop_rubble": (1.56732, .292082, .946659),
                "prop_bed": (1, 1.08, 2.14), "prop_curtain_rail": (2, 2.495568, .116),
                "prop_cabinet": (.86, 1.9, .534), "prop_wheelchair": (.728, 1.04, .95)}
    return fixed.get(piece, authored.get(piece))


def validate_piece(theme, row, art):
    piece = row["id"]
    path = art/row["file"]
    raw_fbx_contract(path)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    objects = list(bpy.context.scene.objects)
    require(len(objects) == 1 and objects[0].type == "MESH", f"{piece}: export contamination")
    obj = objects[0]
    require(obj.name == theme.title()+"_"+piece, f"{piece}: object naming")
    require(close(obj.location, (0, 0, 0), 1e-6) and close(obj.scale, (1, 1, 1), 1e-6), f"{piece}: pivot/scale")
    points = [tuple(v.co) for v in obj.data.vertices]
    lo, hi = bounds(points)
    size = [hi[i]-lo[i] for i in range(3)]
    require(all(math.isfinite(c) for p in points for c in p), f"{piece}: nonfinite vertex")
    require(close(size, row["size"], .00001), f"{piece}: manifest dimensions {size} != {row['size']}")
    expected = expected_size(theme, piece)
    if expected:
        require(close(size, expected), f"{piece}: dimensions {size} != {expected}")
    require(abs(lo[1]) < 1e-5 and abs(lo[0]+hi[0]) < 1e-5, f"{piece}: bottom/centre pivot")
    if row["kind"] != "arc":
        require(abs(lo[2]+hi[2]) < 1e-5, f"{piece}: depth-centre / wall-plane pivot")
    require(all(len(p.vertices) == 3 for p in obj.data.polygons), f"{piece}: non-triangulated export")
    triangles = len(obj.data.polygons)
    require(0 < triangles <= (1500 if row["kind"] == "prop" else 300), f"{piece}: budget {triangles}")
    require(triangles == row["triangles"], f"{piece}: manifest triangle count")
    materials = sorted(m.name for m in obj.data.materials)
    allowed = {f"{theme}_{s}" for s in SURFACES[theme]}
    require(materials == row["materials"] and set(materials) <= allowed and len(materials) > 0,
            f"{piece}: material slots {materials}")
    require(all(re.fullmatch(theme+r"_[a-z_]+", name) for name in materials), f"{piece}: material naming")
    require(obj.data.uv_layers.active is not None, f"{piece}: no exported UVs")
    require(all(p.area > 1e-10 for p in obj.data.polygons), f"{piece}: degenerate triangles")
    if row["kind"] in ("wall", "door", "window", "arc", "floor", "ceiling", "pillar"):
        bottom = [p for p in obj.data.polygons if abs(p.center.y-lo[1]) < 1e-5]
        top = [p for p in obj.data.polygons if abs(p.center.y-hi[1]) < 1e-5]
        require(bottom and top and all(p.normal.y < -.9 for p in bottom)
                and all(p.normal.y > .9 for p in top), f"{piece}: inverted top/bottom surfaces")
    digest = mesh_hash(obj)
    require(digest == row["geometrySha256"], f"{piece}: geometry digest {digest} != {row['geometrySha256']}")
    if row["kind"] in ("wall", "door", "window"):
        # Detail surfaces must actually be on the -Z room side, not mirrored.
        front = [p for p in obj.data.polygons if p.center.z < -.20 and p.normal.z < -.3]
        require(len(front) >= 2, f"{piece}: detailed face is not -Z forward")
    if piece == "wall_door_4m":
        test_opening(obj, 3.2, 0, 2.8)
    if piece == "wall_window_2m":
        test_opening(obj, 1.3, 1.1, 2.5)
    if row["kind"] == "arc":
        radius = int(piece[-1])
        require(abs(size[1]-HEIGHTS[theme]) <= TOLERANCE, f"{piece}: arc height")
        require(abs(size[0]-2*(radius+.25)*math.sin(math.radians({4:30, 6:20, 8:15}[radius])/2)) < TOLERANCE,
                f"{piece}: arc chord envelope")
        check_arc(points, radius, {4:30, 6:20, 8:15}[radius], HEIGHTS[theme])
    return points, {"id": piece, "triangles": triangles, "size": size,
                    "geometrySha256": digest, "fileSha256": sha(path)}


def check_sources(theme, rows):
    source = ROOT/"ArtSource/Environment"/theme.title()/"Kit"/(theme.title()+"Kit.blend")
    require(source.is_file(), f"{theme}: missing editable source")
    bpy.ops.wm.open_mainfile(filepath=str(source))
    require(bpy.context.scene.unit_settings.scale_length == 1, f"{theme}: source not metre scale")
    for row in rows:
        obj = bpy.data.objects.get(theme.title()+"_"+row["id"])
        require(obj is not None and obj.type == "MESH", f"{theme}: missing source piece {row['id']}")
        require(close(obj.location, (0, 0, 0), 1e-6) and close(obj.rotation_euler, (0, 0, 0), 1e-6)
                and close(obj.scale, (1, 1, 1), 1e-6), f"{row['id']}: source transforms")
        require(mesh_hash(obj, source=True) == row["geometrySha256"], f"{row['id']}: source/export drift")
    images = [im for im in bpy.data.images if im.type == "IMAGE"]
    require(0 < len(images) <= 4, f"{theme}: texture budget {len(images)}")
    for image in images:
        require(image.packed_file is not None and image.filepath.startswith("//"), f"{image.name}: source portability")
        require(max(image.size) <= 1024 and min(image.size) > 0, f"{image.name}: image dimensions")
        path = Path(bpy.path.abspath(image.filepath))
        require(path.is_file() and bytes(image.packed_file.data) == path.read_bytes(), f"{image.name}: packed texture drift")
    return source


def check_previews(output):
    import numpy as np  # Bundled with Blender; no package installation.
    for name in ("front", "three-quarter"):
        path = output/(name+".png")
        require(path.is_file(), f"missing preview {path}")
        image = bpy.data.images.load(str(path), check_existing=False)
        require(tuple(image.size) == (1200, 900), f"{name}: preview resolution")
        pixels = np.empty(len(image.pixels), dtype=np.float32)
        image.pixels.foreach_get(pixels)
        rgb = pixels.reshape(-1, 4)[:, :3]
        require(float(rgb.std()) > .035 and float((rgb.max(axis=1) > .12).mean()) > .12,
                f"{name}: blank or unreadably dark preview")
        bpy.data.images.remove(image)


def validate(theme, args):
    output = ROOT/"Logs/AgentValidation/Art"/("Env"+theme.title())
    output.mkdir(parents=True, exist_ok=True)
    art = ROOT/"Assets/Art/Environment"/theme.title()/"Kit"
    manifest_path = art/(theme.title()+"Kit.manifest.json")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    rows = manifest["pieces"]
    expected = dict(COMMON, **{piece: "prop" for piece in PROPS[theme]})
    require(manifest["theme"] == theme and manifest["wallHeight"] == HEIGHTS[theme], f"{theme}: theme/height")
    require(len(rows) == len(expected) and {r["id"] for r in rows} == set(expected), f"{theme}: contract inventory")
    require({p.name for p in art.glob("*.fbx")} == {theme.title()+"_"+p+".fbx" for p in expected},
            f"{theme}: missing or extra FBX files")
    points, details = {}, []
    for row in rows:
        require(row["file"] == theme.title()+"_"+row["id"]+".fbx" and row["kind"] == expected[row["id"]],
                f"{row['id']}: manifest filename/kind")
        points[row["id"]], detail = validate_piece(theme, row, art)
        details.append(detail)
    # Full end-vertex equality at 2m; door endpoints additionally snap at 4m.
    wall_left, wall_right = straight_edges(points["wall_2m"], 2)
    require(seam_match([(p[0]+2, p[1], p[2]) for p in wall_left], wall_right), f"{theme}: 2m wall repeat seam")
    for piece, width in (("wall_window_2m", 2), ("wall_door_4m", 4)):
        left, right = straight_edges(points[piece], width)
        require(seam_match([(p[0]+width/2+1, p[1], p[2]) for p in left], wall_right), f"{piece}: left interchange seam")
        require(seam_match([(p[0]-width/2-1, p[1], p[2]) for p in right], wall_left), f"{piece}: right interchange seam")
    for piece in ("floor_2x2", "ceiling_2x2", "trim_base_2m"):
        left, right = straight_edges(points[piece], 2)
        require(seam_match([(p[0]+2, p[1], p[2]) for p in left], right), f"{piece}: X repeat seam")
        if piece != "trim_base_2m":
            near = [p for p in points[piece] if abs(p[2]+1) < 1e-5]
            far = [p for p in points[piece] if abs(p[2]-1) < 1e-5]
            require(seam_match([(p[0], p[1], p[2]+2) for p in near], far), f"{piece}: Z repeat seam")
    source = check_sources(theme, rows)
    if not args.skip_previews:
        check_previews(output)
    snapshot = {"manifestSha256": sha(manifest_path), "geometry": {d["id"]: d["geometrySha256"] for d in details},
                "textures": {p.name: sha(p) for p in sorted(art.glob("*.png"))}}
    baseline = output/"determinism-baseline.json"
    if args.record_baseline:
        require(not baseline.exists(), f"Refusing to replace baseline {baseline}")
        baseline.write_text(json.dumps(snapshot, indent=2)+"\n", encoding="utf-8")
    if args.compare_baseline:
        require(snapshot == json.loads(baseline.read_text(encoding="utf-8")), f"{theme}: repeat-generation drift")
    lines = [f"PASS {theme}: {len(rows)}/{len(expected)} contract pieces; manifest/files/geometry agree",
             f"PASS {theme}: dimensions <=1cm; bottom-centre pivots; Y-up/-Z-forward; applied FBX transforms",
             f"PASS {theme}: architecture <=300 triangles (max {max(d['triangles'] for d in details if not d['id'].startswith('prop_'))}); props <=1500 (max {max(d['triangles'] for d in details if d['id'].startswith('prop_'))}); material slots/UVs",
             f"PASS {theme}: 2m wall seams; door/window interchange; floor/ceiling/trim seams; r4/r6/r8 rotated seams",
             f"PASS {theme}: 3.2x2.8m unobstructed doorway; 1.3x1.4m unobstructed window",
             f"PASS {theme}: editable source/export agreement; packed relative textures <=1024px/4 per theme"]
    if not args.skip_previews:
        lines.append(f"PASS {theme}: front/three-quarter assembled previews 1200x900; nonblank (not artistic acceptance)")
    if args.compare_baseline:
        lines.append(f"PASS {theme}: repeat manifest/geometry/texture hashes identical; manifest SHA256 {snapshot['manifestSha256']}")
    (output/"validation.txt").write_text("\n".join(lines)+"\n", encoding="utf-8")
    (output/"validation.json").write_text(json.dumps({"pass": True, "pieces": details, "snapshot": snapshot,
        "source": str(source.relative_to(ROOT)), "checks": lines}, indent=2)+"\n", encoding="utf-8")
    print("\n".join(lines))


def negative_controls():
    require(not close((2.02, 7, .5), (2, 7, .5)), "dimension negative control did not fail")
    seam = [(1, 0, -.25), (1, 7, -.25), (1, 0, .19), (1, 7, .19)]
    require(seam_match(seam, seam), "matching seam control failed")
    changed = [(x, y, z+.002) for x, y, z in seam]
    require(not seam_match(seam, changed), "seam negative control did not fail")
    try:
        check_arc([(0, 0, 0), (1, 0, 0), (0, 7, 0), (1, 7, 0)], 4, 30, 7)
    except AssertionError:
        pass
    else:
        raise AssertionError("arc negative control did not fail")
    print("PASS validator negative controls: 2cm size drift, 2mm seam drift, invalid arc rejected")


def main():
    parser = argparse.ArgumentParser(description="Re-import and verify both environment kits without Unity")
    parser.add_argument("--theme", choices=("castle", "hospital", "all"), default="all")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--record-baseline", action="store_true")
    group.add_argument("--compare-baseline", action="store_true")
    parser.add_argument("--skip-previews", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    require(bpy.app.version[:2] == (5, 2), "Use Blender 5.2")
    negative_controls()
    for theme in HEIGHTS if args.theme == "all" else (args.theme,):
        try:
            validate(theme, args)
        except Exception as error:
            output = ROOT/"Logs/AgentValidation/Art"/("Env"+theme.title())
            output.mkdir(parents=True, exist_ok=True)
            message = f"FAIL {theme}: {error}"
            (output/"validation.txt").write_text(message+"\n", encoding="utf-8")
            (output/"validation.json").write_text(json.dumps({"pass": False, "error": str(error)}, indent=2)+"\n",
                                                 encoding="utf-8")
            raise


if __name__ == "__main__":
    main()
