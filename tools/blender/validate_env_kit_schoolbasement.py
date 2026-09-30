# ============================================================================
# validate_env_kit_schoolbasement.py
# PURPOSE: Independently inspect the saved kit sources and re-import every FBX.
#   Fail on missing pieces, changed contract dimensions, broken seams or export
#   axes, rather than trusting the generator or its self-reported manifest.
# ARCHITECTURAL ROLE: Offline art acceptance tool; no Unity runtime dependency.
# KEY RESPONSIBILITIES:
#   - Check contract inventory, dimensions, pivots, materials and triangle limits.
#   - Inspect FBX axes/transforms and compare exports with editable source meshes.
#   - Verify straight/curved seams, clear door openings and preview artifacts.
#   - Emit per-piece evidence and repeat-generation semantic hashes.
# DEPENDENCIES: Blender 5.2 bpy, bundled io_scene_fbx parser, Python standard library.
# USAGE NOTES: Same headless flags as the generator; -- --theme all by default.
#   Read-only for ArtSource/Assets. Reports go only to Logs/AgentValidation/Art.
#   Does not import generator code. Unity importer/material/wiring QA is separate.
# ============================================================================
import argparse
import hashlib
import json
import math
import re
import sys
from pathlib import Path

import bpy
from io_scene_fbx import parse_fbx
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
HEIGHTS = {"school": 3.8, "basement": 3.2}
COMMON = {"wall_2m": "wall", "wall_door_4m": "door", "wall_window_2m": "window",
          "wall_arc_r4": "arc", "wall_arc_r6": "arc", "wall_arc_r8": "arc",
          "corner_in": "corner", "corner_out": "corner", "pillar": "pillar",
          "floor_2x2": "floor", "ceiling_2x2": "ceiling", "trim_base_2m": "trim"}
PROPS = {"school": {"prop_desk": "prop", "prop_locker_bank": "prop",
                    "prop_chalkboard": "prop", "prop_chair": "prop"},
         "basement": {"pipe_straight_2m": "pipe", "pipe_elbow": "pipe", "pipe_tee": "pipe",
                      "duct_straight_2m": "duct", "duct_elbow": "duct",
                      "prop_valve_wheel": "prop", "prop_boiler": "prop"}}
SURFACES = {"school": {"plaster", "tile", "tile_worn", "metal", "wood", "chalk", "board", "grime", "floor", "glass"},
            "basement": {"plaster", "tile", "tile_worn", "metal", "rust", "grime", "floor", "paint", "gauge", "glass"}}
# Provisional authored bounds, not additional owner contract requirements.
ART_SIZES = {"prop_desk": (1.12, .755162, .65), "prop_locker_bank": (1.2, 1.86, .479),
             "prop_chalkboard": (1.8, 1.38, .1945), "prop_chair": (.43, .891366, .432938),
             "pipe_straight_2m": (2, .32, .32), "pipe_elbow": (.79, .32, .79),
             "pipe_tee": (1.2, .32, .76), "duct_straight_2m": (2, .56, .72),
             "duct_elbow": (1.24, .56, 1.24), "prop_valve_wheel": (.57, .57, .215),
             "prop_boiler": (1.15, 2.32, 1.37)}
TOLERANCE = .01
EPS = .00002


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def quantized(point):
    return tuple(round(float(v), 5) + 0.0 for v in point)


def unity_point(point):
    return Vector((point.x, point.z, -point.y))


def facts(obj):
    points = [unity_point(obj.matrix_world @ v.co) for v in obj.data.vertices]
    lo = [min(v[i] for v in points) for i in range(3)]
    hi = [max(v[i] for v in points) for i in range(3)]
    obj.data.calc_loop_triangles()
    materials = [slot.material.name if slot.material else "" for slot in obj.material_slots]
    triangles = [(tuple(t.vertices), materials[t.material_index]) for t in obj.data.loop_triangles]
    return {"points": points, "lo": lo, "hi": hi, "size": [b-a for a, b in zip(lo, hi)],
            "materials": materials, "triangles": triangles,
            "origin": unity_point(obj.matrix_world.translation)}


def signature(data):
    triangles = sorted((tuple(sorted(quantized(data["points"][i]) for i in indices)), material)
                       for indices, material in data["triangles"])
    return hashlib.sha256(json.dumps(triangles, separators=(",", ":")).encode()).hexdigest()


def properties(element):
    prop = next((e for e in element.elems if e.id == b"Properties70"), None)
    return {e.props[0].decode(): list(e.props[4:]) for e in prop.elems} if prop else {}


def raw_fbx(path):
    root, version = parse_fbx.parse(str(path))
    require(version == 7400, f"{path.name}: unsupported FBX version {version}")
    settings = properties(next(e for e in root.elems if e.id == b"GlobalSettings"))
    # FBX stores front parity (+1), not the '-Z' exporter UI string. These are
    # Blender's -Z-forward/Y-up export settings; baked geometry is checked too.
    expected = {"UpAxis": [1], "UpAxisSign": [1], "FrontAxis": [2], "FrontAxisSign": [1],
                "CoordAxis": [0], "CoordAxisSign": [1], "UnitScaleFactor": [100.0]}
    require(all(settings.get(k) == v for k, v in expected.items()), f"{path.name}: FBX axis/unit metadata")
    objects = next(e for e in root.elems if e.id == b"Objects")
    models = [e for e in objects.elems if e.id == b"Model"]
    geometries = [e for e in objects.elems if e.id == b"Geometry"]
    require(len(models) == len(geometries) == 1, f"{path.name}: one mesh, no review objects")
    props = properties(models[0])
    for key, default in (("Lcl Translation", [0, 0, 0]), ("Lcl Rotation", [0, 0, 0]),
                         ("Lcl Scaling", [1, 1, 1]), ("GeometricTranslation", [0, 0, 0]),
                         ("GeometricRotation", [0, 0, 0]), ("GeometricScaling", [1, 1, 1]),
                         ("PreRotation", [0, 0, 0]), ("PostRotation", [0, 0, 0]),
                         ("RotationPivot", [0, 0, 0]), ("ScalingPivot", [0, 0, 0])):
        require(all(abs(a-b) < EPS for a, b in zip(props.get(key, default), default)),
                f"{path.name}: unapplied {key}")
    coordinates = next(e for e in geometries[0].elems if e.id == b"Vertices").props[0]
    return [Vector(coordinates[i:i+3]) for i in range(0, len(coordinates), 3)]


def near_sets(first, second):
    return (bool(first) and bool(second) and
            all(min((a-b).length for b in second) < EPS for a in first) and
            all(min((a-b).length for b in first) < EPS for a in second))


def check_dimensions(name, kind, data, height):
    expected = None
    if kind in {"wall", "door", "window"}:
        expected = (4 if kind == "door" else 2, height, .298 if kind == "door" else .304)
    elif kind == "arc":
        radius = int(name[-1])
        angle = math.radians({4: 30, 6: 20, 8: 15}[radius])/2
        expected = (2*(radius+.25)*math.sin(angle), height, .25+radius*(1-math.cos(angle)))
        radii = [math.hypot(p.x, p.z+radius) for p in data["points"]]
        require(all(min(abs(r-radius), abs(r-radius-.25)) < EPS for r in radii), name+": arc radii")
        angles = [math.atan2(p.x, p.z+radius) for p in data["points"]]
        require(abs(min(angles)+angle) < EPS and abs(max(angles)-angle) < EPS, name+": arc sweep")
        require(any(abs(p.x) < EPS and abs(p.z) < EPS and abs(p.y) < EPS for p in data["points"]),
                name+": tangent-plane pivot")
    elif kind == "corner":
        expected = (.5, height, .5245)
    elif kind == "pillar":
        expected = (.48, height, .48)
    elif kind == "floor":
        expected = (2, .1002, 2)
    elif kind == "ceiling":
        expected = (2, .13, 2)
    elif kind == "trim":
        expected = (2, .18, .07)
    else:
        expected = ART_SIZES[name]
    require(all(abs(a-b) <= TOLERANCE for a, b in zip(data["size"], expected)),
            f"{name}: dimensions {data['size']} expected {expected}")
    require(data["origin"].length < EPS and abs(data["lo"][1]) < EPS, name+": grounded origin")
    require(abs(data["lo"][0]+data["hi"][0]) < EPS, name+": X-centred pivot")
    if kind in {"prop", "pipe", "duct", "floor", "ceiling", "pillar", "trim"}:
        require(abs(data["lo"][2]+data["hi"][2]) < EPS, name+": Z-centred pivot")
    if kind in {"wall", "door", "window"}:
        require(any(abs(v.z) < EPS and abs(v.y) < EPS for v in data["points"]), name+": wall plane at Z=0")
        require(data["lo"][2] < -.02 and abs(data["hi"][2]-.25) < EPS,
                name+": relief faces interior -Z, structure exterior +Z")


def clipped_area(triangle, bounds):
    """Projected triangle area inside a strict door rectangle, including spans
    whose vertices all lie outside the rectangle (vertex-only tests miss these)."""
    polygon = [(v.x, v.y) for v in triangle]
    for axis, edge, keep_greater in ((0, bounds[0], True), (0, bounds[1], False),
                                     (1, bounds[2], True), (1, bounds[3], False)):
        output = []
        for start, end in zip(polygon, polygon[1:]+polygon[:1]):
            inside_a = start[axis] >= edge if keep_greater else start[axis] <= edge
            inside_b = end[axis] >= edge if keep_greater else end[axis] <= edge
            if inside_a != inside_b:
                t = (edge-start[axis])/(end[axis]-start[axis])
                output.append(tuple(start[i]+t*(end[i]-start[i]) for i in range(2)))
            if inside_b:
                output.append(end)
        polygon = output
        if not polygon:
            return 0
    return abs(sum(a[0]*b[1]-b[0]*a[1] for a, b in zip(polygon, polygon[1:]+polygon[:1])))/2


def check_door(data):
    points = data["points"]
    for indices, _ in data["triangles"]:
        require(clipped_area([points[i] for i in indices], (-1.599, 1.599, .001, 2.799)) < 1e-8,
                "wall_door_4m: triangle obstructs 3.2 x 2.8 opening")
    for x in (-1.6, 1.6):
        require(any(abs(v.x-x) < EPS and abs(v.y) < EPS for v in points), "door jamb dimensions")
    require(any(abs(v.y-2.8) < EPS and abs(v.x-2) < EPS for v in points), "door lintel height")


def straight_edges(data, width):
    return [[Vector((0, p.y, p.z)) for p in data["points"] if abs(p.x-sign*width/2) < EPS]
            for sign in (-1, 1)]


def check_seams(records):
    reference = straight_edges(records["wall_2m"], 2)[0]
    for name, width in (("wall_2m", 2), ("wall_window_2m", 2), ("wall_door_4m", 4)):
        for edge in straight_edges(records[name], width):
            require(near_sets(edge, reference), name+": translated mating edge vertices differ")
    for name in ("floor_2x2", "ceiling_2x2", "trim_base_2m"):
        edges = straight_edges(records[name], 2)
        require(near_sets(*edges), name+": 2m X seam")
        if name != "trim_base_2m":
            edges = [[Vector((p.x, p.y, 0)) for p in records[name]["points"] if abs(p.z-sign) < EPS]
                     for sign in (-1, 1)]
            require(near_sets(*edges), name+": 2m Z seam")
    for radius, degrees in ((4, 30), (6, 20), (8, 15)):
        name = f"wall_arc_r{radius}"
        angle = math.radians(degrees)
        points = records[name]["points"]
        edges = [[p for p in points if abs(math.atan2(p.x, p.z+radius)-sign*angle/2) < EPS]
                 for sign in (-1, 1)]
        turned = [Vector((p.x*math.cos(angle)+(p.z+radius)*math.sin(angle), p.y,
                          -p.x*math.sin(angle)+(p.z+radius)*math.cos(angle)-radius)) for p in edges[0]]
        require(near_sets(turned, edges[1]), name+": rotated mating edges differ")


def self_tests():
    # Negative tests demonstrate that checks reject a blocked opening, a gap,
    # and a sub-centimetre pivot drift instead of rubber-stamping.
    triangle = [Vector((-3, 0, 0)), Vector((3, 0, 0)), Vector((0, 4, 0))]
    require(clipped_area(triangle, (-1.599, 1.599, .001, 2.799)) > 0, "self-test clipping")
    require(not near_sets([Vector((0, 0, 0))], [Vector((0, .005, 0))]), "self-test seam rejection")
    data = {"points": [Vector((0, 0, 0))], "triangles": [((0, 0, 0), "test")],
            "size": [2, 3.8, .304], "lo": [-1, 0, -.054], "hi": [1, 3.8, .25],
            "origin": Vector((.005, 0, 0))}
    try:
        check_dimensions("wall_2m", "wall", data, 3.8)
    except AssertionError:
        pass
    else:
        raise AssertionError("self-test pivot rejection")
    print("PASS validator negative controls: aperture clipping, seam gap, pivot drift")


def validate(theme):
    title = theme.title()
    art = ROOT/"Assets/Art/Environment"/title/"Kit"
    source = ROOT/"ArtSource/Environment"/title/"Kit"/(title+"Kit.blend")
    report_dir = ROOT/"Logs/AgentValidation/Art"/("Env"+title)
    report_dir.mkdir(parents=True, exist_ok=True)
    # Invalidate prior evidence before reading inputs, so a failed retry cannot
    # leave an older PASS report looking like the result of the current run.
    (report_dir/"validation.json").write_text('{"passed": false, "status": "validation started"}\n', encoding="utf-8")
    (report_dir/"validation.txt").write_text("INCOMPLETE: validation started\n", encoding="utf-8")
    manifest_path = art/(title+"Kit.manifest.json")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    required = {**COMMON, **PROPS[theme]}
    require(manifest["theme"] == theme and manifest["wallHeight"] == HEIGHTS[theme], "manifest theme/height")
    rows = manifest["pieces"]
    require(len(rows) == len(required) and {r["id"] for r in rows} == set(required), "exact contract inventory")
    filenames = {title+"_"+name+".fbx" for name in required}
    require({p.name for p in art.glob("*.fbx")} == filenames, "manifest matches FBX file inventory")
    require(source.is_file(), "editable source missing")
    bpy.ops.wm.open_mainfile(filepath=str(source))
    originals = {}
    for name in required:
        obj = bpy.data.objects.get(title+"_"+name)
        require(obj is not None and obj.type == "MESH", name+": source object")
        require(obj.location.length < EPS and obj.rotation_euler.to_matrix().is_identity and
                all(abs(s-1) < EPS for s in obj.scale) and not obj.modifiers, name+": applied source transforms")
        originals[name] = facts(obj)
    review = bpy.data.collections.get("Review room (not exported)")
    require(review is not None, "assembled review room missing")
    shown = {o.name.split(".")[0].removeprefix("Review_") for o in review.objects}
    require({"wall_2m", "wall_door_4m", "wall_arc_r4", "floor_2x2", "ceiling_2x2", "trim_base_2m"} <= shown,
            "review room missing required architecture")
    require(set(PROPS[theme]) <= shown, "review room missing theme props")
    records, details, lines = {}, [], []
    for row in rows:
        name, kind = row["id"], row["kind"]
        require(kind == required[name] and row["file"] == title+"_"+name+".fbx", name+": manifest identity/kind")
        path = art/row["file"]
        raw_points = raw_fbx(path)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
        objects = list(bpy.context.scene.objects)
        require(len(objects) == 1 and objects[0].type == "MESH" and objects[0].name == path.stem,
                name+": export contains only named piece")
        data = facts(objects[0])
        records[name] = data
        check_dimensions(name, kind, data, HEIGHTS[theme])
        require(near_sets(raw_points, data["points"]), name+": baked Y-up vertices differ after import")
        require(near_sets(originals[name]["points"], data["points"]), name+": source/export vertex mismatch")
        require(len(originals[name]["triangles"]) == len(data["triangles"]), name+": source/export triangle mismatch")
        require(set(originals[name]["materials"]) == set(data["materials"]), name+": source/export material mismatch")
        require(len(row["size"]) == 3 and all(math.isfinite(v) and abs(v-a) < EPS for v, a in zip(row["size"], data["size"])),
                name+": manifest size disagrees with exported bounds")
        budget = 1500 if kind in {"prop", "pipe", "duct"} else 300
        count = len(data["triangles"])
        require(0 < count <= budget, name+": triangle budget")
        allowed = {theme+"_"+surface for surface in SURFACES[theme]}
        require(bool(data["materials"]) and all(re.fullmatch(theme+r"_[a-z_]+", m) and m in allowed for m in data["materials"]),
                name+": material slot convention")
        for indices, _ in data["triangles"]:
            a, b, c = (data["points"][i] for i in indices)
            require((b-a).cross(c-a).length > 1e-10, name+": degenerate triangle")
        if kind == "door":
            check_door(data)
        details.append({"id": name, "triangles": count, "size": list(map(lambda v: round(v, 6), data["size"])),
                        "materials": sorted(set(data["materials"])), "semanticSha256": signature(data),
                        "fbxSha256": hashlib.sha256(path.read_bytes()).hexdigest()})
        lines.append(f"PASS {title}_{name}: dimensions/pivot/axes/materials/source/manifest; triangles={count}/{budget}")
    check_seams(records)
    lines.append(f"PASS {title}: 2m straight wall/window seams, 4m door joins, floor/ceiling/trim seams; r4/r6/r8 arc seams")
    lines.append(f"PASS {title}: 3.2m x 2.8m clear doorway; every contract piece present ({len(required)})")
    previews = []
    for name in ("front", "three-quarter"):
        path = report_dir/(name+".png")
        require(path.is_file(), "missing preview "+str(path))
        image = bpy.data.images.load(str(path), check_existing=False)
        require(tuple(image.size) == (1200, 800), "preview dimensions")
        pixels = list(image.pixels)
        samples = [sum(pixels[i:i+3])/3 for i in range(0, len(pixels), 400)]
        require(max(samples)-min(samples) > .15, "blank or unreadable preview")
        previews.append({"file": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
        bpy.data.images.remove(image)
    lines.append(f"PASS {title}: source review assembly and two nonblank 1200x800 previews")
    result = {"theme": theme, "passed": True, "pieces": details, "previews": previews,
              "manifestSha256": hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
              "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
              "limitations": ["Not a Unity importer or material/wiring test.", "Image checks do not establish artistic acceptance."]}
    (report_dir/"validation.json").write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8", newline="\n")
    (report_dir/"validation.txt").write_text("\n".join(lines)+"\n", encoding="utf-8", newline="\n")
    for line in lines:
        print(line)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--theme", choices=("school", "basement", "all"), default="all")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    require(bpy.app.version[:2] == (5, 2), "Use Blender 5.2")
    self_tests()
    for theme in HEIGHTS if args.theme == "all" else (args.theme,):
        validate(theme)
    print("PASS School/Basement requested kit validation complete")


if __name__ == "__main__":
    main()
