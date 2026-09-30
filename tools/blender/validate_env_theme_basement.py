# ============================================================================
# validate_env_theme_basement.py
# PURPOSE: Independently verify the Basement exports, physical room envelopes and
#   saved review assemblies. Re-import every FBX rather than trusting the art
#   generator's dimensions, and reject broken templates with negative controls.
# ARCHITECTURAL ROLE: Offline acceptance tool; outside Unity runtime layers.
# KEY RESPONSIBILITIES:
#   - Verify inventory, geometry, axes, transforms, pivots, seams and materials.
#   - Verify connected footprints, door spans, anchors and complete wall coverage.
#   - Compare source assemblies and rendered evidence with the room manifest.
#   - Record and compare deterministic manifest and semantic geometry hashes.
# DEPENDENCIES: Blender 5.2 bpy/mathutils, bundled FBX parser, standard library.
# USAGE NOTES: Same Blender flags as generator. -- --record-baseline records the
#   first successful run; -- --compare-baseline verifies the second regeneration.
#   --skip-previews is for intermediate diagnostics and is never final acceptance.
#   Does not import either generator or v1 validator, edit assets, or run Unity.
# ============================================================================
import argparse
import copy
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
ART = ROOT/"Assets/Art/Environment/Basement"
SOURCE = ROOT/"ArtSource/Environment/Basement"
REVIEW = ROOT/"Logs/AgentValidation/Art/EnvBasement"
EPS = .00002
REQUIRED = {"wall_2m":"wall","wall_door_4m":"door","wall_window_2m":"window",
            "wall_arc_r4":"arc","wall_arc_r6":"arc","wall_arc_r8":"arc",
            "corner_in":"corner","corner_out":"corner","pillar":"pillar",
            "floor_2x2":"floor","ceiling_2x2":"ceiling","trim_base_2m":"trim",
            "pipe_straight_2m":"pipe","pipe_elbow":"pipe","pipe_tee":"pipe",
            "duct_straight_2m":"duct","duct_elbow":"duct","prop_valve_wheel":"prop",
            "prop_boiler":"prop"}
ALLOWED_KINDS = {"wall","door","window","arc","corner","pillar","floor","ceiling","trim","prop","pipe","duct"}
SURFACES = {"concrete","damp","rust","steel","galvanised","insulation","sodium","hazard","water"}


def require(condition,message):
    if not condition:
        raise AssertionError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def unity(p):
    return Vector((p.x,p.z,-p.y))


def facts(obj):
    points = [unity(obj.matrix_world@v.co) for v in obj.data.vertices]
    lo = [min(p[i] for p in points) for i in range(3)]
    hi = [max(p[i] for p in points) for i in range(3)]
    obj.data.calc_loop_triangles()
    slots = [s.material.name if s.material else "" for s in obj.material_slots]
    triangles = [(tuple(t.vertices),slots[t.material_index]) for t in obj.data.loop_triangles]
    return {"points":points,"lo":lo,"hi":hi,"size":[b-a for a,b in zip(lo,hi)],
            "materials":slots,"triangles":triangles,"origin":unity(obj.matrix_world.translation)}


def semantic(data):
    triangles = sorted((tuple(sorted(tuple(round(float(v),5)+0.0 for v in data["points"][i])
                                    for i in indices)),material) for indices,material in data["triangles"])
    return hashlib.sha256(json.dumps(triangles,separators=(",",":")).encode()).hexdigest()


def near_sets(a,b):
    return bool(a) and bool(b) and all(min((p-q).length for q in b)<EPS for p in a) and all(min((p-q).length for q in a)<EPS for p in b)


def properties(element):
    entry = next((e for e in element.elems if e.id == b"Properties70"),None)
    return {e.props[0].decode():list(e.props[4:]) for e in entry.elems} if entry else {}


def raw_fbx(path):
    root,version = parse_fbx.parse(str(path))
    require(version == 7400,path.name+": FBX version")
    settings = properties(next(e for e in root.elems if e.id == b"GlobalSettings"))
    expected = {"UpAxis":[1],"UpAxisSign":[1],"FrontAxis":[2],"FrontAxisSign":[1],
                "CoordAxis":[0],"CoordAxisSign":[1],"UnitScaleFactor":[100.0]}
    require(all(settings.get(k)==v for k,v in expected.items()),path.name+": axes/metres")
    entries = next(e for e in root.elems if e.id == b"Objects").elems
    models = [e for e in entries if e.id == b"Model"]
    geometry = [e for e in entries if e.id == b"Geometry"]
    require(len(models)==len(geometry)==1,path.name+": one exported mesh only")
    props = properties(models[0])
    for key,default in (("Lcl Translation",[0,0,0]),("Lcl Rotation",[0,0,0]),("Lcl Scaling",[1,1,1]),
                        ("GeometricTranslation",[0,0,0]),("GeometricRotation",[0,0,0]),("GeometricScaling",[1,1,1]),
                        ("PreRotation",[0,0,0]),("PostRotation",[0,0,0]),("RotationPivot",[0,0,0]),("ScalingPivot",[0,0,0])):
        require(all(abs(a-b)<EPS for a,b in zip(props.get(key,default),default)),path.name+": applied "+key)
    coords = next(e for e in geometry[0].elems if e.id == b"Vertices").props[0]
    return [Vector(coords[i:i+3]) for i in range(0,len(coords),3)]


def clipped_area(triangle,bounds):
    polygon = [(p.x,p.y) for p in triangle]
    for axis,edge,greater in ((0,bounds[0],True),(0,bounds[1],False),(1,bounds[2],True),(1,bounds[3],False)):
        output = []
        for a,b in zip(polygon,polygon[1:]+polygon[:1]):
            ia = a[axis]>=edge if greater else a[axis]<=edge
            ib = b[axis]>=edge if greater else b[axis]<=edge
            if ia != ib:
                t = (edge-a[axis])/(b[axis]-a[axis])
                output.append(tuple(a[i]+t*(b[i]-a[i]) for i in range(2)))
            if ib:
                output.append(b)
        polygon = output
        if not polygon:
            return 0
    return abs(sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(polygon,polygon[1:]+polygon[:1])))/2


def check_door(data):
    for indices,_ in data["triangles"]:
        require(clipped_area([data["points"][i] for i in indices],(-1.599,1.599,.001,2.799))<1e-8,
                "door: triangle blocks the 3.2 x 2.8 m opening")
    for x in (-1.6,1.6):
        require(any(abs(p.x-x)<EPS and abs(p.y)<EPS for p in data["points"]),"door jamb")
    require(any(abs(p.x-2)<EPS and abs(p.y-2.8)<EPS for p in data["points"]),"door lintel")


def check_dimensions(name,kind,data):
    require(data["origin"].length<EPS and abs(data["lo"][1])<EPS,name+": bottom pivot")
    require(abs(data["lo"][0]+data["hi"][0])<EPS,name+": centred X pivot")
    if kind not in {"wall","door","window","arc","corner"}:
        require(abs(data["lo"][2]+data["hi"][2])<EPS,name+": centred Z pivot")
    if kind in {"wall","door","window"}:
        width = 4 if kind == "door" else 1 if name.endswith("_1m") else 2
        require(abs(data["size"][0]-width)<EPS and abs(data["size"][1]-3.2)<EPS,name+": width/height")
        require(abs(data["hi"][2]-.25)<EPS and data["lo"][2] <= 0,name+": exterior +Z/interior -Z")
        require(any(abs(p.z)<EPS and abs(p.y)<EPS for p in data["points"]),name+": tangent wall plane")
    if kind == "arc":
        radius = int(name[-1])
        angle = math.radians({4:30,6:20,8:15}[radius])/2
        require(abs(data["size"][1]-3.2)<EPS,name+": height")
        for p in data["points"]:
            r = math.hypot(p.x,p.z+radius)
            require(min(abs(r-radius),abs(r-radius-.25))<EPS,name+": radii")
        angles = [math.atan2(p.x,p.z+radius) for p in data["points"]]
        require(abs(min(angles)+angle)<EPS and abs(max(angles)-angle)<EPS,name+": arc sweep")
        require(any(abs(p.x)+abs(p.y)+abs(p.z)<EPS for p in data["points"]),name+": arc tangent pivot")
    if name in {"floor_2x2","floor_grate_2x2","ceiling_2x2","ceiling_joist_2x2"}:
        require(abs(data["size"][0]-2)<EPS and abs(data["size"][2]-2)<EPS,name+": 2x2 tile")
    if name in {"pipe_straight_2m","duct_straight_2m","trim_base_2m","ibeam_2m"}:
        require(abs(data["size"][0]-2)<EPS,name+": 2m module")
    if kind in {"pillar","corner"}:
        require(abs(data["size"][1]-3.2)<EPS,name+": height")


def seams(records):
    def ends(data,width):
        return [[Vector((0,p.y,p.z)) for p in data["points"] if abs(p.x-sign*width/2)<EPS] for sign in (-1,1)]
    reference = ends(records["wall_2m"],2)[0]
    for name,width in (("wall_2m",2),("wall_window_2m",2),("wall_door_4m",4),("wall_concrete_1m",1)):
        for edge in ends(records[name],width):
            require(near_sets(edge,reference),name+": wall mating seam")
    for name in ("floor_2x2","ceiling_2x2","trim_base_2m","floor_grate_2x2"):
        require(near_sets(*ends(records[name],2)),name+": X seam")
        if name != "trim_base_2m":
            edges = [[Vector((p.x,p.y,0)) for p in records[name]["points"] if abs(p.z-sign)<EPS] for sign in (-1,1)]
            require(near_sets(*edges),name+": Z seam")
    for r,d in ((4,30),(6,20),(8,15)):
        data = records[f"wall_arc_r{r}"]
        a = math.radians(d)
        left,right = [[p for p in data["points"] if abs(math.atan2(p.x,p.z+r)-s*a/2)<EPS] for s in (-1,1)]
        rotated = [Vector((p.x*math.cos(a)+(p.z+r)*math.sin(a),p.y,-p.x*math.sin(a)+(p.z+r)*math.cos(a)-r)) for p in left]
        require(near_sets(rotated,right),f"r{r}: curved seam")


def kit_validation(manifest):
    require(manifest["theme"] == "basement" and manifest["wallHeight"] == 3.2,"kit theme/height")
    rows = manifest["pieces"]
    lookup = {r["id"]:r for r in rows}
    require(len(rows)==len(lookup) and REQUIRED.keys()<=lookup.keys(),"unique IDs and mandatory contract inventory")
    require({p.name for p in (ART/"Kit").glob("*.fbx")} == {r["file"] for r in rows},"FBX file inventory")
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/"Kit/BasementKit.blend"))
    source = {}
    for row in rows:
        name = row["id"]
        obj = bpy.data.objects.get("Basement_"+name)
        require(obj is not None and obj.type=="MESH",name+": source mesh")
        require(obj.location.length<EPS and obj.rotation_euler.to_matrix().is_identity and
                all(abs(s-1)<EPS for s in obj.scale) and not obj.modifiers,name+": applied source")
        source[name] = facts(obj)
    sheet = bpy.data.scenes.get("Basement kit sheet")
    require(sheet is not None,"kit sheet assembly")
    bpy.context.window.scene = sheet
    bpy.context.view_layer.update()
    require({o.name.removeprefix("Sheet_") for o in sheet.objects if o.name.startswith("Sheet_")}==set(lookup),"every kit piece on sheet")
    inverse_camera = sheet.camera.matrix_world.inverted()
    half_width = sheet.camera.data.ortho_scale/2
    half_height = half_width*sheet.render.resolution_y/sheet.render.resolution_x
    for obj in sheet.objects:
        if obj.name.startswith("Sheet_"):
            for corner in obj.bound_box:
                point = inverse_camera@(obj.matrix_world@Vector(corner))
                require(abs(point.x)<half_width and abs(point.y)<half_height and point.z<0,"kit piece clipped by sheet camera")
    require(all(image.type=="RENDER_RESULT" for image in bpy.data.images),"no unaccounted textures (render buffers excluded)")
    records,details = {},[]
    for row in rows:
        name,kind = row["id"],row["kind"]
        require(kind in ALLOWED_KINDS and (name not in REQUIRED or REQUIRED[name]==kind),name+": kind")
        require(row["file"] == "Basement_"+name+".fbx",name+": filename")
        path = ART/"Kit"/row["file"]
        raw = raw_fbx(path)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
        objects = list(bpy.context.scene.objects)
        require(len(objects)==1 and objects[0].type=="MESH" and objects[0].name==path.stem,name+": export isolation")
        data = facts(objects[0])
        records[name] = data
        check_dimensions(name,kind,data)
        require(near_sets(raw,data["points"]),name+": baked axes")
        require(near_sets(source[name]["points"],data["points"]),name+": source vertices")
        require(len(source[name]["triangles"])==len(data["triangles"]),name+": source triangle count")
        require(set(source[name]["materials"])==set(data["materials"]),name+": source material slots")
        require(len(row["size"])==3 and all(math.isfinite(v) and abs(v-b)<EPS for v,b in zip(row["size"],data["size"])),name+": manifest bounds")
        budget = 1500 if kind in {"prop","pipe","duct"} else 300
        count = len(data["triangles"])
        require(0<count<=budget,name+": triangle budget")
        require(data["materials"] and all(re.fullmatch(r"basement_[a-z_]+",m) and m.removeprefix("basement_") in SURFACES for m in data["materials"]),name+": material convention")
        require("basement_sodium" not in data["materials"] or name=="cage_lamp",name+": orange is light-only")
        for indices,_ in data["triangles"]:
            a,b,c = [data["points"][i] for i in indices]
            require((b-a).cross(c-a).length>1e-10,name+": degenerate triangle")
        if kind == "door":
            check_door(data)
        details.append({"id":name,"triangles":count,"budget":budget,"semanticSha256":semantic(data),"fbxSha256":sha(path)})
        print(f"PASS {name}: dimensions/pivots/axes/transforms/materials/source; triangles={count}/{budget}")
    seams(records)
    return lookup,details,records


def convex_hull(points):
    def cross(a,b,c):
        return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    points = sorted(set(points))
    lower,upper = [],[]
    for p in points:
        while len(lower)>1 and cross(lower[-2],lower[-1],p)<=0:
            lower.pop()
        lower.append(p)
    for p in reversed(points):
        while len(upper)>1 and cross(upper[-2],upper[-1],p)<=0:
            upper.pop()
        upper.append(p)
    return lower[:-1]+upper[:-1]


def wall_sections(data,height):
    # Slice actual imported concrete triangles. Separate connected contours keep
    # door jambs apart; a single bounding box would incorrectly fill the aperture.
    adjacency = {}
    for indices,material in data["triangles"]:
        if material != "basement_concrete":
            continue
        triangle = [data["points"][i] for i in indices]
        points = []
        for a,b in zip(triangle,triangle[1:]+triangle[:1]):
            if min(a.y,b.y)<height<max(a.y,b.y):
                t = (height-a.y)/(b.y-a.y)
                points.append((round(a.x+t*(b.x-a.x),6),round(a.z+t*(b.z-a.z),6)))
        if len(points)==2 and points[0]!=points[1]:
            a,b = points
            adjacency.setdefault(a,set()).add(b)
            adjacency.setdefault(b,set()).add(a)
    sections = []
    unseen = set(adjacency)
    while unseen:
        seed = next(iter(unseen))
        connected,stack = set(),[seed]
        while stack:
            p = stack.pop()
            if p in connected:
                continue
            connected.add(p)
            stack.extend(adjacency[p]-connected)
        unseen -= connected
        hull = convex_hull(connected)
        if len(hull)>=3:
            sections.append(hull)
    return sections


def intersection_area(first,second):
    polygon = first
    for a,b in zip(second,second[1:]+second[:1]):
        def signed(p):
            return (b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0])
        output = []
        for start,end in zip(polygon,polygon[1:]+polygon[:1]):
            sa,sb = signed(start),signed(end)
            if (sa>=0)!=(sb>=0):
                t = sa/(sa-sb)
                output.append(tuple(start[i]+t*(end[i]-start[i]) for i in range(2)))
            if sb>=0:
                output.append(end)
        polygon = output
        if not polygon:
            return 0
    return abs(sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(polygon,polygon[1:]+polygon[:1])))/2


def check_wall_solids(template,lookup,sections):
    for height in (.5,1.4,3.0):
        solids = []
        for entry in template["pieces"]:
            if lookup[entry["id"]]["kind"] not in {"wall","door","window"}:
                continue
            a = math.radians(entry["rotY"])
            x,_,z = entry["pos"]
            for polygon in sections[(entry["id"],height)]:
                transformed = [(x+px*math.cos(a)+pz*math.sin(a),z-px*math.sin(a)+pz*math.cos(a)) for px,pz in polygon]
                for other in solids:
                    require(intersection_area(transformed,other)<1e-7,template["id"]+": physical concrete wall intersection")
                solids.append(transformed)


def boundary(cells):
    # Axis, coordinate, interval start/end, outward-facing yaw.
    edges = []
    for x,z in cells:
        for dx,dz,side in ((0,-1,"S"),(0,1,"N"),(-1,0,"W"),(1,0,"E")):
            if (x+dx,z+dz) not in cells:
                if side in {"S","N"}:
                    edges.append(("x",2*z+(2 if side=="N" else 0),2*x,2*x+2,180 if side=="S" else 0))
                else:
                    edges.append(("z",2*x+(2 if side=="E" else 0),2*z,2*z+2,90 if side=="E" else 270))
    return edges


def distance_edge(x,z,edge):
    axis,line,a,b,_ = edge
    u,v = (x,z) if axis=="x" else (z,x)
    return math.hypot(v-line,max(a-u,0,u-b))


def wall_interval(entry,lookup):
    x,y,z = entry["pos"]
    yaw = entry["rotY"]
    width = lookup[entry["id"]]["size"][0]
    require(y==0 and yaw in (0,90,180,270),"wall height/cardinal yaw")
    axis,line,center = ("x",z,x) if yaw in (0,180) else ("z",x,z)
    return axis,line,center-width/2,center+width/2,yaw


def check_envelope(template,lookup):
    cells = {tuple(c) for c in template["footprint"]}
    edges = boundary(cells)
    walls = [p for p in template["pieces"] if lookup[p["id"]]["kind"] in {"wall","door","window"}]
    intervals = [wall_interval(p,lookup) for p in walls]
    # Exact interval sweep: no sparse ray sampling that could miss narrow leaks.
    for key in {(a,line,yaw) for a,line,_,_,yaw in edges+intervals}:
        expected = [(a,b) for ax,ln,a,b,yw in edges if (ax,ln,yw)==key]
        actual = [(a,b) for ax,ln,a,b,yw in intervals if (ax,ln,yw)==key]
        cuts = sorted({v for interval in expected+actual for v in interval})
        for a,b in zip(cuts,cuts[1:]):
            if b-a<EPS:
                continue
            mid = (a+b)/2
            ec = sum(lo<mid<hi for lo,hi in expected)
            ac = sum(lo<mid<hi for lo,hi in actual)
            require(ac<=1,"overlapping wall pieces")
            require(ac==ec,"wall enclosure gap or wall outside footprint boundary")
    door_pieces = [p for p in walls if lookup[p["id"]]["kind"]=="door"]
    require(len(door_pieces)==len(template["doors"]),"one aperture per socket")
    for door in template["doors"]:
        x,z = door["cell"]
        side = door["side"]
        require(side in {"N","E","S","W"} and (x,z) in cells,"door cell/side")
        nx,nz = {"N":(x,z+1),"S":(x,z-1),"E":(x+1,z),"W":(x-1,z)}[side]
        require((nx,nz) not in cells,"door boundary edge")
        pos,yaw = {"S":([2*x+1,0,2*z],180),"N":([2*x+1,0,2*z+2],0),
                   "W":([2*x,0,2*z+1],270),"E":([2*x+2,0,2*z+1],90)}[side]
        matching = [p for p in door_pieces if p["pos"]==pos and p["rotY"]==yaw]
        require(len(matching)==1,"door centred on nominated edge with inward-facing aperture")
        closed = door.get("closedWith",[])
        require(len(closed)==2 and all(p["id"]=="wall_2m" for p in closed),"explicit closedWith wall pair")
        original = wall_interval(matching[0],lookup)
        alternatives = sorted([wall_interval(p,lookup) for p in closed],key=lambda i:i[2])
        require(all((a[0],a[1],a[4])==(original[0],original[1],original[4]) for a in alternatives),"closedWith pose")
        require(abs(alternatives[0][2]-original[2])<EPS and abs(alternatives[0][3]-alternatives[1][2])<EPS and
                abs(alternatives[1][3]-original[3])<EPS,"closedWith exactly replaces 4m door wall")


def check_template(t,lookup):
    require(re.fullmatch(r"basement_[a-z_]+",t["id"]) is not None,"room ID")
    require(t["kind"] in {"room","hallway","junction"},"room kind")
    require(t["shape"] in {"rect","L","T","round","irregular"},"room shape")
    cells = {tuple(c) for c in t["footprint"]}
    require(cells and len(cells)==len(t["footprint"]) and all(len(c)==2 and all(type(v)==int and v>=0 for v in c) for c in cells),"integer unique footprint")
    require(min(c[0] for c in cells)==min(c[1] for c in cells)==0,"south-west origin")
    visited,frontier = set(),[next(iter(cells))]
    while frontier:
        c = frontier.pop()
        if c in visited:
            continue
        visited.add(c)
        frontier.extend(n for n in ((c[0]-1,c[1]),(c[0]+1,c[1]),(c[0],c[1]-1),(c[0],c[1]+1)) if n in cells and n not in visited)
    require(visited==cells,"4-connected footprint")
    n = len(cells)
    expected = "closet" if n<=4 else "small" if n<=9 else "medium" if n<=20 else "large" if n<=40 else "hall"
    require(t["sizeClass"]==expected and t["height"]==3.2,"area class/height")
    require(len(t["doors"]) >= (1 if expected=="closet" else 2),"door count")
    require(t["gimmick"] in {"none","puzzle","freeze","traversal"} and t["minRound"] >= (1 if t["gimmick"]=="none" else 3),"gimmick round gating")
    require(math.isfinite(t["weight"]) and t["weight"]>0,"weight")
    if t["kind"]=="hallway":
        # Eroding a passage by one cell must leave no 3x3 region (<=2 cells wide).
        require(not any(all((x+dx,z+dz) in cells for dx in (-1,0,1) for dz in (-1,0,1)) for x,z in cells),"hallway width")
        require(len(t["doors"])==2,"hallway endpoints")
    for p in t["pieces"]:
        require(p["id"] in lookup,"unknown kit piece")
        require(len(p["pos"])==3 and all(math.isfinite(v) for v in p["pos"]) and math.isfinite(p["rotY"]),"finite placement")
    anchors = t["anchors"]
    require(set(anchors)=={"cake","goldenCake","light","hunterSpawn"},"anchor families")
    require(len(anchors["cake"]) >= (1 if n<=4 else max(2,math.ceil(n/6))),"cake density scales with area")
    require(len(anchors["goldenCake"])<=1 and anchors["light"],"golden/light count")
    require(n<10 or anchors["hunterSpawn"],"hunter spawn")
    edges = boundary(cells)
    for family,points in anchors.items():
        require(len({tuple(p) for p in points})==len(points),"duplicate anchors")
        for x,y,z in points:
            require(all(math.isfinite(v) for v in (x,y,z)) and 0<=y<=3.2,"anchor vertical range")
            require((math.floor(x/2),math.floor(z/2)) in cells,"anchor outside footprint")
            require(min(distance_edge(x,z,e) for e in edges)>=.6-EPS,"anchor wall clearance")
            if family=="light":
                require(any(p["id"]=="cage_lamp" and abs(p["pos"][0]-x)<EPS and abs(p["pos"][2]-z)<EPS for p in t["pieces"]),"light anchor has a live cage fixture")
    floors = [p for p in t["pieces"] if lookup[p["id"]]["kind"]=="floor"]
    require({(int(p["pos"][0]//2),int(p["pos"][2]//2)) for p in floors}==cells,"floor module coverage")
    check_envelope(t,lookup)


def room_validation(manifest,lookup):
    require(manifest["theme"]=="basement" and manifest["module"]==2,"room manifest theme/module")
    templates = manifest["templates"]
    require(len(templates)>=10 and len({t["id"] for t in templates})==len(templates),"catalogue count/unique IDs")
    for t in templates:
        check_template(t,lookup)
        print(f"PASS {t['id']}: connected/boundary doors/anchors/piece IDs/no wall overlap/full enclosure/closedWith")
    for size,minimum in (("closet",1),("small",2),("medium",2),("large",1),("hall",1)):
        require(sum(t["sizeClass"]==size and t["kind"]=="room" for t in templates)>=minimum,"catalogue size diversity "+size)
    require(any(t["sizeClass"]=="medium" and t["shape"] in {"L","round"} and t["kind"]=="room" for t in templates),"nonrect medium room")
    require(any(t["kind"]=="hallway" and t["shape"]=="rect" for t in templates) and any(t["kind"] in {"hallway","junction"} and t["shape"] in {"L","T"} for t in templates),"straight and bent/junction corridors")
    require(1<=sum(t["gimmick"]!="none" for t in templates)<=2,"gimmick catalogue count")
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/"Rooms/BasementRooms.blend"))
    for t in templates:
        scene = bpy.data.scenes.get(t["id"])
        require(scene is not None,"source template scene")
        placed = {int(o["placement_index"]):o for o in scene.objects if "placement_index" in o}
        require(len(placed)==len(t["pieces"]),"source/manifest placement count")
        for i,p in enumerate(t["pieces"]):
            obj = placed[i]
            require(obj["piece_id"]==p["id"],"source placement ID")
            require((unity(obj.location)-Vector(p["pos"])).length<EPS,"source placement position")
            a = math.radians(p["rotY"])
            expected_inward = Vector((-math.sin(a),0,-math.cos(a)))
            actual_inward = unity(obj.rotation_euler.to_matrix()@Vector((0,1,0)))
            require((actual_inward-expected_inward).length<EPS,"Unity inward direction versus Blender rotation")
            require(all(abs(v-1)<EPS for v in obj.scale),"no template piece scaling")
        lights = [o for o in scene.objects if o.type=="LIGHT" and o.name.startswith("Sodium cage")]
        require(len(lights)==len(t["anchors"]["light"]),"source sodium lights")
        require(scene.camera is not None and any(o.hide_render and o.get("piece_id")=="ceiling_2x2" for o in scene.objects),"cutaway ceiling/camera")
    return templates


def negative_controls(template,lookup):
    def rejects(mutator,label):
        damaged = copy.deepcopy(template)
        mutator(damaged)
        try:
            check_template(damaged,lookup)
        except (AssertionError,KeyError):
            return
        raise AssertionError("negative control accepted "+label)
    rejects(lambda t:t["footprint"].append([99,99]),"disconnected cell")
    rejects(lambda t:t["doors"][0].update(cell=[1,1],side="N"),"interior door")
    rejects(lambda t:t["anchors"]["cake"].append([-1,0,-1]),"outside anchor")
    rejects(lambda t:t["anchors"]["cake"].append([.1,0,.1]),"wall clearance")
    rejects(lambda t:t["pieces"][0].update(id="missing_piece"),"missing piece")
    wall_index = next(i for i,p in enumerate(template["pieces"]) if p["id"]=="wall_2m")
    rejects(lambda t:t["pieces"].append(copy.deepcopy(t["pieces"][wall_index])),"wall overlap")
    rejects(lambda t:t["pieces"].pop(wall_index),"enclosure gap")
    rejects(lambda t:t["doors"][0]["closedWith"][0]["pos"].__setitem__(0,99),"bad closedWith")
    rejects(lambda t:t.update(gimmick="freeze",minRound=1),"early gimmick")
    require(clipped_area([Vector((-3,0,0)),Vector((3,0,0)),Vector((0,4,0))],(-1.599,1.599,.001,2.799))>0,"door clipping negative control")
    require(not near_sets([Vector((0,0,0))],[Vector((0,.005,0))]),"seam negative control")
    data = {"origin":Vector((.005,0,0)),"lo":[-1,0,0],"hi":[1,3.2,.25],"size":[2,3.2,.25],"points":[Vector((0,0,0))]}
    try:
        check_dimensions("wall_2m","wall",data)
    except AssertionError:
        pass
    else:
        raise AssertionError("pivot drift negative control")
    print("PASS validator negative controls: disconnected footprint, interior door, outside/near-wall anchors, unknown piece, overlap, gap, closure, early gimmick, aperture, seam, pivot")


def previews(templates):
    result = []
    for filename,size in [("kit-sheet.png",(1800,1400)),("in-darkness.png",(1200,800))]+[(t["id"]+".png",(1200,800)) for t in templates]:
        path = REVIEW/filename
        require(path.is_file(),"missing preview "+filename)
        receipt = json.loads((REVIEW/(filename+".json")).read_text(encoding="utf-8"))
        require(receipt["imageSha256"]==sha(path),"preview image receipt")
        require(receipt["kitManifestSha256"]==sha(ART/"Kit/BasementKit.manifest.json") and
                receipt["roomsManifestSha256"]==sha(ART/"Rooms/BasementRooms.manifest.json"),"preview matches current manifests")
        if filename=="in-darkness.png":
            require(receipt["worldStrength"]==0 and not receipt["hiddenPlacements"],"darkness: no ambient or cutaway")
            require(sum(l["type"]=="SPOT" for l in receipt["lights"])==1 and
                    all(l["type"]=="POINT" and l["name"].startswith("Sodium cage") or
                        l["type"]=="SPOT" and l["name"].startswith("Flashlight only") for l in receipt["lights"]),"darkness: sodium and flashlight only")
        elif filename!="kit-sheet.png":
            require(receipt["hiddenPlacements"] and any(l["type"]=="POINT" for l in receipt["lights"]),"room render: cutaway and lit fixture")
        image = bpy.data.images.load(str(path),check_existing=False)
        require(tuple(image.size)==size,"preview resolution")
        pixels = list(image.pixels)
        samples = [sum(pixels[i:i+3])/3 for i in range(0,len(pixels),400)]
        require(max(samples)-min(samples)>.15,"blank/unreadable preview")
        result.append({"file":filename,"sha256":sha(path),"min":min(samples),"max":max(samples),"mean":sum(samples)/len(samples)})
        bpy.data.images.remove(image)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--record-baseline",action="store_true")
    parser.add_argument("--compare-baseline",action="store_true")
    parser.add_argument("--skip-previews",action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    require(bpy.app.version[:2]==(5,2),"Blender 5.2 required")
    require(not(args.record_baseline and args.compare_baseline),"select one determinism operation")
    REVIEW.mkdir(parents=True,exist_ok=True)
    (REVIEW/"validation.json").write_text('{"passed":false,"status":"started"}\n',encoding="utf-8")
    (REVIEW/"validation.txt").write_text("INCOMPLETE: validation started\n",encoding="utf-8")
    kit_path,room_path = ART/"Kit/BasementKit.manifest.json",ART/"Rooms/BasementRooms.manifest.json"
    kit,rooms = [json.loads(p.read_text(encoding="utf-8")) for p in (kit_path,room_path)]
    lookup,details,records = kit_validation(kit)
    templates = room_validation(rooms,lookup)
    negative_controls(templates[0],lookup)
    sections = {(name,height):wall_sections(data,height) for name,data in records.items()
                if lookup[name]["kind"] in {"wall","door","window"} for height in (.5,1.4,3.0)}
    require(all(polygons for polygons in sections.values()),"nonempty wall cross-section evidence")
    for template in templates:
        check_wall_solids(template,lookup,sections)
    bad_corner = copy.deepcopy(next(t for t in templates if t["id"]=="basement_valve_gallery"))
    for entry in bad_corner["pieces"]:
        if entry["id"].startswith("wall_miter_"):
            entry["id"] = "wall_2m" if entry["id"].endswith("_2m") else "wall_concrete_1m"
    try:
        check_wall_solids(bad_corner,lookup,sections)
    except AssertionError:
        pass
    else:
        raise AssertionError("negative control accepted perpendicular wall slab overlap")
    images = [] if args.skip_previews else previews(templates)
    fingerprints = {"kitManifestSha256":sha(kit_path),"roomsManifestSha256":sha(room_path),
                    "geometry":{r["id"]:r["semanticSha256"] for r in details}}
    baseline = REVIEW/"determinism-baseline.json"
    lines = [f"PASS Basement kit: {len(details)} FBXs, {len(REQUIRED)} mandatory IDs; dimensions, pivots, applied transforms, Y-up/-Z-forward, materials, source parity, triangle budgets",
             "PASS Basement seams: straight wall/window/door, floor/ceiling/trim/grate, r4/r6/r8 arcs; clear 3.2 x 2.8 m door",
             f"PASS Basement rooms: {len(templates)} connected templates; boundary doors, inside anchors with 0.6 m clearance, kit IDs, no wall overlap, full enclosure, closedWith alternatives",
             "PASS Basement catalogue: closet, two small, nonrect medium, large, hall, straight/bent hallways, two round-3 gimmicks",
             "PASS Basement source: every room placement and Unity yaw matches its saved Blender scene",
             "PASS Basement physical walls: imported concrete cross-sections do not intersect; concave corners are mitred; overlap negative control rejected",
             "PASS validator negative controls: all malformed geometry/template probes rejected"]
    if not args.skip_previews:
        lines.append(f"PASS Basement previews: all {len(images)} images present, correct dimensions, nonblank and manifest-hash bound; darkness uses only sodium lamps plus one flashlight")
    if args.record_baseline:
        require(not baseline.exists(),"refusing to overwrite determinism baseline; preserve previous run evidence")
        baseline.write_text(json.dumps(fingerprints,indent=2)+"\n",encoding="utf-8")
        lines.append("PASS determinism baseline recorded after complete validation")
    if args.compare_baseline:
        require(baseline.is_file(),"first-run determinism baseline missing")
        require(json.loads(baseline.read_text(encoding="utf-8"))==fingerprints,"repeat generation changed manifest or semantic geometry hashes")
        lines.append("PASS determinism: two generations have identical kit/room manifest SHA-256 and every FBX semantic geometry hash")
    result = {"passed":not args.skip_previews,"diagnosticOnly":args.skip_previews,"fingerprints":fingerprints,
              "pieces":details,"templates":[t["id"] for t in templates],"previews":images,"checks":lines,
              "limitations":["Unity import/material setup, gameplay gimmicks, fallback behaviour and owner visual judgement are coordinator gates."]}
    (REVIEW/"validation.json").write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8",newline="\n")
    (REVIEW/"validation.txt").write_text("\n".join(lines)+"\n",encoding="utf-8",newline="\n")
    for line in lines:
        print(line)


if __name__=="__main__":
    main()
