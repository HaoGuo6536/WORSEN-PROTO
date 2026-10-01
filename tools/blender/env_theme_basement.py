# ============================================================================
# env_theme_basement.py
# PURPOSE: Author the Basement as a low industrial boiler complex, not a reskin
#   of the School shell. Rebuild the complete fallback kit and a finite catalogue
#   of rooms from the same metre-scale geometry used in the exported assets.
# ARCHITECTURAL ROLE: Offline art generator; outside the Unity runtime layers.
# KEY RESPONSIBILITIES:
#   - Build poured concrete, structural steel, services and industrial machinery.
#   - Export applied Y-up/-Z-forward meshes and deterministic kit/room manifests.
#   - Assemble editable sources and render catalogue and darkness review images.
#   - Seat services, preserve end-cap sockets and place optional vault shortcuts.
# DEPENDENCIES: Blender 5.2 bpy/mathutils and Python standard library only.
#   Shared door-review studio from env_theme_castle; no Castle geometry reused.
# USAGE NOTES: --background --factory-startup --python-exit-code 1 --python FILE
#   [-- --skip-previews]. No Unity calls, external assets, textures or v1 imports.
#   Authored coordinates are Unity XYZ; Blender uses (X,-Z,Y). Room yaw around
#   Unity +Y becomes the same signed rotation around Blender +Z. Gameplay and fallback
#   behaviour belong to the coordinator, not to this offline assembly tool.
# ============================================================================
import argparse
import hashlib
import json
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/Art/Environment/Basement"
SOURCE = ROOT / "ArtSource/Environment/Basement"
REVIEW = ROOT / "Logs/AgentValidation/Art/EnvBasement"
HEIGHT = 3.2
# Owner's sRGB colours. Convert to linear, rather than assigning hex as linear RGB.
PALETTE = {"concrete": "5e6061", "damp": "3a3c3d", "rust": "8a4b22",
           "steel": "161514", "galvanised": "8d9396", "insulation": "bdb6a4",
           "sodium": "ff9a3c", "hazard": "c8a62a", "door_steel": "161514"}
KINDS = {
    "wall_2m": "wall", "wall_door_4m": "door", "wall_window_2m": "window",
    "wall_arc_r4": "arc", "wall_arc_r6": "arc", "wall_arc_r8": "arc",
    "corner_in": "corner", "corner_out": "corner", "pillar": "pillar",
    "floor_2x2": "floor", "ceiling_2x2": "ceiling", "trim_base_2m": "trim",
    "pipe_straight_2m": "pipe", "pipe_elbow": "pipe", "pipe_tee": "pipe",
    "duct_straight_2m": "duct", "duct_elbow": "duct", "prop_valve_wheel": "prop",
    "prop_boiler": "prop", "ceiling_joist_2x2": "ceiling", "ibeam_2m": "ceiling",
    "wall_concrete_formtie_2m": "wall", "wall_concrete_1m": "wall",
    "wall_miter_left_1m": "wall", "wall_miter_right_1m": "wall",
    "wall_miter_left_2m": "wall", "wall_miter_right_2m": "wall",
    "floor_grate_2x2": "floor", "catwalk_2m": "floor", "catwalk_rail_2m": "prop",
    "pipe_run_wall_2m": "pipe", "duct_run_ceiling_2m": "duct", "cage_lamp": "prop",
    "cage_lamp_dead": "prop", "prop_pump": "prop", "prop_electrical_cabinet": "prop",
    "prop_gauge_panel": "prop", "puddle_decal_quad": "decal", "prop_storage_cage": "prop",
    "prop_fuel_bunker": "prop", "prop_steam_vent": "prop", "pit_liner_2x2": "prop",
    "pit_retaining_2m": "prop", "prop_bulkhead_leaf": "prop", "floor_sump_2x2": "floor"}


def xyz(p):
    return Vector((p[0], -p[2], p[1]))


def linear(hex_color):
    rgb = [int(hex_color[i:i+2], 16)/255 for i in (0, 2, 4)]
    return tuple(c/12.92 if c <= .04045 else ((c+.055)/1.055)**2.4 for c in rgb)


def materials():
    for name, value in PALETTE.items():
        mat = bpy.data.materials.new("basement_"+name)
        color = (*linear(value), 1)
        mat.diffuse_color = color
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = color
        shader.inputs["Roughness"].default_value = .38 if name in {"steel", "galvanised", "door_steel"} else .86
        shader.inputs["Metallic"].default_value = .75 if name in {"steel", "galvanised", "rust", "door_steel"} else 0
        if name == "sodium":
            shader.inputs["Emission Color"].default_value = color
            shader.inputs["Emission Strength"].default_value = 4
    wet = bpy.data.materials.new("basement_water")
    wet.diffuse_color = (*linear(PALETTE["steel"]), 1)
    wet.use_nodes = True
    shader = wet.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = wet.diffuse_color
    shader.inputs["Roughness"].default_value = .065
    shader.inputs["Metallic"].default_value = .35


class BasementMesh:
    """Direct mesh assembly, avoiding operator-state and random global dependencies."""

    def __init__(self, name):
        self.name = name
        self.vertices, self.faces, self.slots = [], [], []
        self.rng = random.Random("basement/industrial/"+name)

    def mesh(self, vertices, faces, surface):
        start = len(self.vertices)
        self.vertices.extend(xyz(p) for p in vertices)
        self.faces.extend(tuple(start+i for i in face) for face in faces)
        self.slots.extend([surface]*len(faces))

    def box(self, center, size, surface):
        corners = ((-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
                   (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1))
        self.mesh([tuple(center[i]+c[i]*size[i]/2 for i in range(3)) for c in corners],
                  [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)], surface)

    def rod(self, a, b, radius, surface, sides=10):
        a, b = Vector(a), Vector(b)
        direction = (b-a).normalized()
        ref = Vector((0,1,0)) if abs(direction.y) < .9 else Vector((1,0,0))
        u, v = direction.cross(ref).normalized(), direction.cross(direction.cross(ref)).normalized()
        vertices = [p+radius*(u*math.cos(i*math.tau/sides)+v*math.sin(i*math.tau/sides))
                    for p in (a,b) for i in range(sides)]
        faces = [(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]
        faces += [tuple(reversed(range(sides))), tuple(range(sides, sides*2))]
        self.mesh(vertices, faces, surface)

    def ring(self, center, radius, wire, surface, segments=12, sides=4):
        cx,cy,cz = center
        vertices = [(cx+(radius+wire*math.cos(j*math.tau/sides))*math.cos(i*math.tau/segments),
                     cy+(radius+wire*math.cos(j*math.tau/sides))*math.sin(i*math.tau/segments),
                     cz+wire*math.sin(j*math.tau/sides))
                    for i in range(segments) for j in range(sides)]
        self.mesh(vertices, [(i*sides+j, ((i+1)%segments)*sides+j,
                              ((i+1)%segments)*sides+(j+1)%sides, i*sides+(j+1)%sides)
                             for i in range(segments) for j in range(sides)], surface)

    def finish(self, kind):
        data = bpy.data.meshes.new("Basement_"+self.name)
        data.from_pydata(self.vertices, [], self.faces)
        data.update()
        names = list(dict.fromkeys(self.slots))
        for name in names:
            data.materials.append(bpy.data.materials["basement_"+name])
        for poly, name in zip(data.polygons, self.slots):
            poly.material_index = names.index(name)
        obj = bpy.data.objects.new("Basement_"+self.name, data)
        bpy.context.collection.objects.link(obj)
        if kind not in {"wall", "door", "window", "arc", "corner"}:
            lo = Vector(tuple(min(v.co[i] for v in data.vertices) for i in range(3)))
            hi = Vector(tuple(max(v.co[i] for v in data.vertices) for i in range(3)))
            shift = Vector(((lo.x+hi.x)/2, (lo.y+hi.y)/2, lo.z))
            for vertex in data.vertices:
                vertex.co -= shift
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        obj["piece_id"], obj["theme"] = self.name, "basement"
        data.calc_loop_triangles()
        count = len(data.loop_triangles)
        budget = 1500 if kind in {"prop", "pipe", "duct"} else 300
        if count > budget:
            raise ValueError(f"{self.name}: {count} triangles exceeds {budget}")
        return obj


def concrete(p, width=2, opening=None, miter=None):
    # All wall variants have identical end-cap vertices; no masonry or paint band.
    for low, high in zip((0,1.1,2.8), (1.1,2.8,HEIGHT)):
        spans = [(-width/2,width/2)]
        if opening == "door" and low < 2.8:
            spans = [(-2,-1.6),(1.6,2)]
        elif opening == "window" and low == 1.1:
            spans = [(-1,-.65),(.65,1)]
        for a,b in spans:
            if miter:
                back_a,back_b = a+(.25 if miter=="left" else 0),b-(.25 if miter=="right" else 0)
                p.mesh([(a,low,0),(b,low,0),(b,high,0),(a,high,0),
                        (back_a,low,.25),(back_b,low,.25),(back_b,high,.25),(back_a,high,.25)],
                       [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],"concrete")
            else:
                p.box(((a+b)/2,(low+high)/2,.125), (b-a,high-low,.25), "concrete")
    # Irregular planar stains and chalky mineral deposits are exported geometry.
    for i in range(7):
        x = p.rng.uniform(-width/2+.09, width/2-.25)
        y = p.rng.uniform(.4, 3.08)
        if opening == "door":
            continue
        if opening == "window" and abs(x) < .75 and 1.05 < y < 2.9:
            continue
        w,h = p.rng.uniform(.05,.20), p.rng.uniform(.2,.7)
        h = min(h,y-.04)
        p.mesh([(x,y,-.002),(x+w,y-.035,-.002),(x+w*.7,y-h,-.002),
                (x+w*.28,y-h*.78,-.002),(x-.025,y-h*.35,-.002)],
               [(0,1,2,3,4)], "insulation" if i == 2 else "damp")
    for x in (-width*.31,width*.31):
        for y in (.55,1.9,2.95):
            if opening == "door" or (opening == "window" and 1.1 < y < 2.8):
                continue
            p.rod((x,y,-.002),(x,y,-.006),.029,"steel",8)
            p.mesh([(x-.016,y-.035,-.003),(x+.012,y-.035,-.003),
                    (x+.007,y-.24,-.003)],[(0,1,2)],"rust")
    if opening == "door":
        # Riveted steel jamb liners stop exactly at the clear aperture boundary.
        for x in (-1.64,1.64):
            p.box((x,1.4,-.024),(.08,2.8,.048),"steel")
            for y in (.22,1.4,2.6):
                p.rod((x,y,-.048),(x,y,-.06),.018,"galvanised",6)
        p.box((0,2.84,-.024),(3.36,.08,.048),"steel")
    elif opening == "window":
        # Industrial wired observation opening, not a domestic window.
        for x in (-.62,-.31,0,.31,.62):
            p.box((x,1.95,.11),(.026,1.7,.035),"steel")
        for y in (1.12,1.65,2.25,2.78):
            p.box((0,y,.11),(1.3,.026,.035),"steel")


def arc(p, radius):
    angle = math.radians({4:30,6:20,8:15}[radius])
    vertices,faces = [],[]
    for i in range(9):
        a = angle*(i/8-.5)
        for r in (radius,radius+.25):
            for y in (0,1.1,2.8,HEIGHT):
                vertices.append((r*math.sin(a),y,r*math.cos(a)-radius))
    for i in range(8):
        a,b = i*8,(i+1)*8
        for j in range(3):
            faces.extend([(a+j,a+j+1,b+j+1,b+j),(a+4+j,b+4+j,b+5+j,a+5+j)])
        faces.extend([(a,b,b+4,a+4),(a+3,a+7,b+7,b+3)])
    for j in range(3):
        faces.extend([(j,j+4,j+5,j+1),(64+j,65+j,69+j,68+j)])
    p.mesh(vertices,faces,"concrete")


def beam(p, z=0, y=0):
    for level in (.025,.275):
        p.box((0,y+level,z),(2,.05,.22),"steel")
    p.box((0,y+.15,z),(2,.20,.055),"rust")


def grate(p, width=2):
    for x in (-width/2+.04,width/2-.04):
        p.box((x,.06,0),(.08,.12,2),"steel")
    for z in (-.96,.96):
        p.box((0,.06,z),(width-.16,.12,.08),"steel")
    for i in range(10):
        x = -width/2+.15+(width-.30)*i/9
        p.box((x,.055,0),(.026,.11,1.84),"galvanised" if i%4 else "rust")
    for z in (-.6,0,.6):
        p.box((0,.035,z),(width-.16,.025,.032),"steel")


def wheel(p, center=(0,.30,0), radius=.24):
    p.ring(center,radius,.024,"rust")
    x,y,z = center
    for a in (0,math.tau/3,2*math.tau/3):
        p.rod(center,(x+radius*math.cos(a),y+radius*math.sin(a),z),.015,"steel",6)
    p.rod((x,y,z-.03),(x,y,z+.18),.04,"steel",8)


def gauge(p,x,y,z):
    p.rod((x,y,z+.05),(x,y,z),.115,"steel",12)
    p.rod((x,y,z),(x,y,z-.008),.09,"insulation",12)
    p.rod((x,y,z-.012),(x-.052,y+.04,z-.012),.006,"steel",6)


def pipe(p, y=.18, z=0, lag=False):
    p.rod((-1,y,z),(1,y,z),.105,"rust",12)
    if lag:
        for a,b in ((-.87,-.12),(.08,.84)):
            p.rod((a,y,z),(b,y,z),.15,"insulation",12)
        for x in (-.68,-.38,.28,.59):
            p.rod((x-.018,y,z),(x+.018,y,z),.157,"galvanised",12)
    for x in (-.94,.94):
        p.rod((x-.045,y,z),(x+.045,y,z),.18,"steel",12)


def build(p):
    name = p.name
    if name.startswith("wall_miter_"):
        concrete(p,1 if name.endswith("_1m") else 2,miter="left" if "_left_" in name else "right")
    elif name in {"wall_2m","wall_concrete_formtie_2m","wall_concrete_1m","wall_door_4m","wall_window_2m"}:
        concrete(p, 4 if name == "wall_door_4m" else 1 if name == "wall_concrete_1m" else 2,
                 "door" if name == "wall_door_4m" else "window" if name == "wall_window_2m" else None)
    elif name.startswith("wall_arc"):
        arc(p,int(name[-1]))
    elif name.startswith("corner"):
        p.box((0,1.6,.125),(.5,3.2,.25),"concrete")
        p.box((.125 if name == "corner_in" else -.125,1.6,-.125),(.25,3.2,.25),"damp")
    elif name == "pillar":
        for x in (-.17,.17):
            p.box((x,1.6,0),(.06,3.2,.4),"steel")
        p.box((0,1.6,0),(.28,3.2,.065),"rust")
        for y in (.035,3.165):
            p.box((0,y,0),(.48,.07,.48),"steel")
    elif name == "floor_2x2":
        p.box((0,.05,0),(2,.1,2),"damp")
        p.mesh([(-.86,.1001,-.9),(-.17,.1001,-.08),(.59,.1001,.2),(.12,.1001,.75),
                (.10,.1001,.75),(.55,.1001,.205),(-.185,.1001,-.073)],[(0,1,2,3,4,5,6)],"steel")
    elif name in {"ceiling_2x2","ceiling_joist_2x2"}:
        if name == "ceiling_2x2":
            p.box((0,.32,0),(2,.04,2),"damp")
        for z in (-.65,.65):
            beam(p,z)
        for x in (-.72,.72):
            p.box((x,.2,0),(.055,.1,2),"steel")
    elif name == "ibeam_2m":
        beam(p)
    elif name == "trim_base_2m":
        p.box((0,.04,0),(2,.08,.10),"damp")
        p.box((0,.10,.01),(2,.04,.05),"steel")
    elif name == 'floor_sump_2x2':
        # A single structural floor assembly: grate, support walls, basin and
        # water. The basin is not a free-floating prop below the floor datum.
        grate(p)
        for v in p.vertices:
            v.z += 1.2
        p.box((0,.06,0),(2,.12,2),'damp')
        p.box((0,.13,0),(1.84,.01,1.84),'water')
        for x in (-.96,.96):
            p.box((x,.6,0),(.08,1.2,2),'damp')
        for z in (-.96,.96):
            p.box((0,.6,z),(1.84,1.2,.08),'damp')
    elif name in {"floor_grate_2x2","catwalk_2m"}:
        grate(p,2 if name == "floor_grate_2x2" else 1.2)
    elif name == "catwalk_rail_2m":
        for x in (-.94,0,.94):
            p.box((x,.53,0),(.045,1.06,.045),"steel")
        for y in (.12,.58,1.08):
            p.box((0,y,0),(2,.045,.045),"rust")
        for x in (-.84,.84):
            p.box((x,1.08,-.025),(.16,.045,.008),"hazard")
    elif name in {"pipe_straight_2m","pipe_run_wall_2m"}:
        pipe(p,lag=True)
        if name == "pipe_run_wall_2m":
            pipe(p,.59,.16)
            pipe(p,.95,.08,True)
            for x in (-.7,.7):
                p.box((x,.55,.28),(.06,1.1,.10),"steel")
    elif name in {"pipe_elbow","duct_elbow"}:
        is_duct = name == "duct_elbow"
        radius = .65 if is_duct else .45
        vertices,faces = [],[]
        n = 4 if is_duct else 12
        for i in range(9):
            a = i*math.pi/16
            cx,cz = radius*math.sin(a),radius*(1-math.cos(a))
            section = [(-.28,-.22),(.28,-.22),(.28,.22),(-.28,.22)] if is_duct else [(.12*math.cos(j*math.tau/n),.12*math.sin(j*math.tau/n)) for j in range(n)]
            for u,v in section:
                vertices.append((cx-u*math.sin(a),.28+v,cz+u*math.cos(a)))
        for i in range(8):
            for j in range(n):
                a,b = i*n+j,i*n+(j+1)%n
                faces.append((a,b,b+n,a+n))
        faces += [tuple(reversed(range(n))),tuple(range(8*n,9*n))]
        p.mesh(vertices,faces,"galvanised" if is_duct else "rust")
    elif name == "pipe_tee":
        pipe(p)
        p.rod((0,.18,0),(0,.18,.7),.105,"rust",12)
        p.rod((0,.18,.6),(0,.18,.7),.18,"steel",12)
    elif name in {"duct_straight_2m","duct_run_ceiling_2m"}:
        p.box((0,.26,0),(2,.44,.66),"galvanised")
        for x in (-.97,0,.97):
            p.box((x,.26,0),(.06,.52,.74),"steel")
        if name == "duct_run_ceiling_2m":
            pipe(p,.24,.62,True)
            for x in (-.7,.7):
                p.box((x,.58,.28),(.05,.6,.05),"steel")
    elif name == "prop_valve_wheel":
        wheel(p)
    elif name == "prop_boiler":
        for x in (-.49,.49):
            p.box((x,.15,0),(.20,.30,1.2),"steel")
        p.rod((0,.24,0),(0,2.32,0),.73,"damp",20)
        for y in (.35,1.35,2.25):
            p.rod((0,y-.045,0),(0,y+.045,0),.76,"rust",20)
        p.rod((0,.72,-.62),(0,.72,-.84),.36,"steel",16)
        p.rod((0,.72,-.84),(0,.72,-.87),.27,"rust",16)
        for i in range(8):
            a = i*math.tau/8
            p.rod((.31*math.cos(a),.72+.31*math.sin(a),-.84),(.31*math.cos(a),.72+.31*math.sin(a),-.89),.024,"galvanised",6)
        p.rod((-.30,2.24,0),(-.30,2.76,0),.14,"rust",12)
        p.rod((.30,2.24,0),(.30,2.57,0),.11,"insulation",12)
        gauge(p,-.25,1.91,-.69)
        wheel(p,(.31,1.4,-.78),.17)
    elif name == "prop_pump":
        p.box((0,.09,0),(1.5,.18,.85),"steel")
        p.rod((-.60,.44,0),(.15,.44,0),.30,"galvanised",16)
        for x in (-.5,-.35,-.2,-.05):
            p.rod((x-.035,.44,0),(x+.035,.44,0),.33,"steel",12)
        p.rod((.31,.22,0),(.31,.65,0),.31,"rust",16)
        p.rod((.31,.60,0),(.31,1.02,0),.10,"steel",12)
        p.rod((.31,.44,-.24),(.31,.44,-.65),.10,"rust",12)
        gauge(p,.31,.83,-.1)
    elif name == "prop_electrical_cabinet":
        p.box((0,1.05,0),(1.12,2.1,.45),"steel")
        for x in (-.28,.28):
            p.box((x,1.08,-.235),(.535,1.96,.035),"galvanised")
            p.box((x+.17,1.1,-.276),(.028,.24,.048),"steel")
            for y in (.34,.40,.46,1.6,1.66,1.72):
                p.box((x,y,-.258),(.32,.018,.009),"steel")
        p.mesh([(-.12,1.40,-.26),(.12,1.40,-.26),(0,1.62,-.26)],[(0,1,2)],"hazard")
        p.box((0,.16,-.26),(.85,.045,.008),"rust")
    elif name == "prop_gauge_panel":
        p.box((0,.39,0),(1.05,.78,.12),"steel")
        for x in (-.3,0,.3):
            gauge(p,x,.5,-.07)
            p.box((x,.17,-.07),(.12,.045,.04),"rust")
    elif name in {"cage_lamp","cage_lamp_dead"}:
        p.rod((0,.10,0),(0,.47,0),.075,"sodium" if name == "cage_lamp" else "damp",12)
        for y in (.04,.54):
            p.rod((0,y-.025,0),(0,y+.025,0),.16,"steel",12)
        for i in range(8):
            a = i*math.tau/8
            x,z = .145*math.cos(a),.145*math.sin(a)
            p.rod((x,.04,z),(x,.54,z),.013,"steel",6)
        p.box((0,.6,0),(.08,.12,.08),"steel")
    elif name == "puddle_decal_quad":
        vertices = [(0,0,0)]+[(.78*math.cos(i*math.tau/16)*(1+.12*math.sin(i*7)),0,
                              .58*math.sin(i*math.tau/16)*(1+.1*math.cos(i*3))) for i in range(16)]
        p.mesh(vertices,[(0,i+1,(i+1)%16+1) for i in range(16)],"water")
    elif name == "prop_storage_cage":
        for x in (-.7,.7):
            for z in (-.5,.5):
                p.box((x,1.1,z),(.05,2.2,.05),"steel")
        for y in (.15,1.1,2.15):
            for z in (-.5,.5):
                p.box((0,y,z),(1.4,.04,.04),"rust")
        for x in [-.6+i*.12 for i in range(11)]:
            for z in (-.5,.5):
                p.box((x,1.1,z),(.012,2.1,.012),"galvanised")
        for z in (-.4,-.2,0,.2,.4):
            for x in (-.7,.7):
                p.box((x,1.1,z),(.012,2.1,.012),"galvanised")
        p.box((0,.3,0),(1.25,.5,.8),"damp")
    elif name == "prop_fuel_bunker":
        p.box((0,.12,0),(1.8,.24,1.3),"steel")
        for x in (-.85,.85):
            p.box((x,.5,0),(.1,.8,1.3),"rust")
        p.box((0,.5,.6),(1.6,.8,.1),"steel")
        for i in range(14):
            x,z = p.rng.uniform(-.7,.7),p.rng.uniform(-.5,.5)
            p.rod((x,.25,z),(x,p.rng.uniform(.45,.75),z),.17,"steel",5)
    elif name == "prop_steam_vent":
        p.box((0,.10,0),(.9,.2,.7),"steel")
        for x in (-.4,.4):
            p.box((x,.205,0),(.065,.01,.65),"hazard")
        for i in range(7):
            p.box((-.3+i*.1,.23,0),(.033,.04,.55),"galvanised")
        p.rod((0,.08,.3),(0,.08,.58),.07,"rust",10)
    elif name == "pit_liner_2x2":
        p.box((0,.06,0),(2,.12,2),"damp")
        p.box((0,.13,0),(1.98,.01,1.98),"water")
    elif name == "pit_retaining_2m":
        p.box((0,.6,0),(2,1.2,.15),"damp")
    elif name == "prop_bulkhead_leaf":
        p.box((0,1.37,0),(1.53,2.74,.08),"door_steel")
        for face in (-1,1):
            p.box((0,1.4,face*.045),(1.28,2.42,.025),"galvanised")
            for x in (-.64,.64):
                p.box((x,1.4,face*.08),(.055,2.5,.065),"rust")
            for y in (.25,2.5):
                p.box((0,y,face*.08),(1.28,.06,.065),"rust")
            p.box((0,.22,face*.066),(1.42,.30,.018),"galvanised")
            p.box((.43,1.25,face*.066),(.19,.34,.018),"galvanised")
            start = len(p.vertices)
            wheel(p,(.43,1.25,-.12),.14)
            if face > 0:
                # Blender Y is negative Unity Z. Mirror only this face's wheel.
                for vertex in p.vertices[start:]:
                    vertex.y = -vertex.y
    else:
        raise ValueError(name)


def export(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"MESH"},
        global_scale=1, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        use_mesh_modifiers=True, mesh_smooth_type="FACE", use_triangles=True,
        bake_anim=False, use_custom_props=False, path_mode="RELATIVE")


def placement(name,x,y,z,yaw=0):
    return {"id":name,"pos":[round(x,5),round(y,5),round(z,5)],"rotY":yaw}


def boundary(cells):
    result = []
    for x,z in sorted(cells):
        for side,dx,dz in (("S",0,-1),("N",0,1),("W",-1,0),("E",1,0)):
            if (x+dx,z+dz) not in cells:
                result.append(((x,z),side))
    return result


def edge_pose(cell, side):
    x,z = cell
    return {"S":(2*x+1,0,2*z,180),"N":(2*x+1,0,2*z+2,0),
            "W":(2*x,0,2*z+1,270),"E":(2*x+2,0,2*z+1,90)}[side]


def shell(cells, doors):
    # Resolve full physical spans, not just the nominated two-metre socket edge.
    edges = boundary(cells)
    pieces = []
    for cell,side in edges:
        x,y,z,yaw = edge_pose(cell,side)
        horizontal = side in {"N","S"}
        center,line = (x,z) if horizontal else (z,x)
        intervals = [(center-1,center+1)]
        for door in doors:
            dx,_,dz,_ = edge_pose(door["cell"],door["side"])
            dx += (door.get('span',1)-1)*(door['side'] in 'NS')
            dz += (door.get('span',1)-1)*(door['side'] in 'EW')
            dc,dl = (dx,dz) if horizontal else (dz,dx)
            if side != door["side"] or line != dl:
                continue
            remaining = []
            for a,b in intervals:
                if b <= dc-2 or a >= dc+2:
                    remaining.append((a,b))
                else:
                    if a < dc-2:
                        remaining.append((a,dc-2))
                    if b > dc+2:
                        remaining.append((dc+2,b))
            intervals = remaining
        for a,b in intervals:
            mid = (a+b)/2
            name = "wall_2m" if b-a == 2 else "wall_concrete_1m"
            pieces.append(placement(name,mid if horizontal else line,0,line if horizontal else mid,yaw))
    for door in doors:
        x,y,z,yaw = edge_pose(door["cell"],door["side"])
        x += (door.get('span',1)-1)*(door['side'] in 'NS')
        z += (door.get('span',1)-1)*(door['side'] in 'EW')
        pieces.append(placement("wall_door_4m",x,0,z,yaw))
        horizontal = door["side"] in {"N","S"}
        door["closedWith"] = [placement("wall_2m",x+(d if horizontal else 0),0,
                                          z+(0 if horizontal else d),yaw) for d in (-1,1)]
    # Re-entrant corners require mitred back faces. Otherwise perpendicular wall
    # slabs overlap a .25 x .25 square despite having nonoverlapping face spans.
    def concave(x,z):
        gx,gz = round(x/2),round(z/2)
        if abs(x-2*gx)>.00001 or abs(z-2*gz)>.00001:
            return False
        return sum((gx+dx,gz+dz) in cells for dx in (-1,0) for dz in (-1,0))==3
    for entry in pieces:
        if entry["id"] not in {"wall_2m","wall_concrete_1m"}:
            continue
        width = 2 if entry["id"]=="wall_2m" else 1
        x,_,z = entry["pos"]
        a = math.radians(entry["rotY"])
        ends = [(side,concave(x+sign*width/2*math.cos(a),z-sign*width/2*math.sin(a)))
                for side,sign in (("left",-1),("right",1))]
        miters = [side for side,needed in ends if needed]
        if len(miters)>1:
            raise ValueError("Double re-entrant module needs a separately authored profile")
        if miters:
            entry["id"] = f"wall_miter_{miters[0]}_{width}m"
    return pieces


def rectangle(w,d):
    return {(x,z) for x in range(w) for z in range(d)}


def catalogue():
    definitions = [
        ("boiler_room","room","rect",rectangle(7,6),[((1,0),"S"),((5,5),"N")],"none"),
        ("pump_room","room","rect",rectangle(4,4),[((1,0),"S"),((2,3),"N")],"none"),
        ("electrical_room","room","rect",rectangle(3,2),[((1,0),"S"),((1,1),"N")],"none"),
        ("storage_cage","room","rect",rectangle(3,3),[((1,0),"S"),((1,2),"N")],"none"),
        ("fuel_bunker","room","rect",rectangle(4,1),[((1,0),"S")],"none"),
        ("pipe_tunnel","hallway","rect",rectangle(2,6),[((0,0),"S"),((0,5),"N")],"none"),
        ("service_bend","hallway","L",rectangle(2,5)|rectangle(5,2),[((0,4),"N"),((4,0),"E")],"none"),
        ("duct_junction","junction","T",rectangle(6,2)|{(x,z) for x in (2,3) for z in (2,3,4)},
         [((0,0),"W"),((5,0),"E"),((2,4),"N")],"none"),
        ("catwalk_hall","room","rect",rectangle(6,5),[((1,0),"S"),((4,4),"N")],"none"),
        ("sump_room","room","irregular",rectangle(4,4)-{(0,3),(3,0)},[((1,0),"S"),((2,3),"N")],"none"),
        ("valve_gallery","room","L",rectangle(4,2)|rectangle(2,4),[((1,0),"S"),((0,2),"W")],"none"),
        ("steam_vent_traversal","room","rect",rectangle(5,3),[((1,0),"S"),((3,2),"N")],"traversal"),
        ("flooding_pit_freeze","room","rect",rectangle(4,4),[((1,0),"S"),((2,3),"N")],"freeze")]
    templates = []
    for name,kind,shape,cells,sockets,gimmick in definitions:
        doors = [{"cell":list(c),"side":s} for c,s in sockets]
        if kind in ('hallway','junction'):
            for door in doors:
                door['span'] = 2
        pieces = shell(cells,doors)
        w,d = max(x for x,z in cells)+1,max(z for x,z in cells)+1
        count = len(cells)
        size = "closet" if count <= 4 else "small" if count <= 9 else "medium" if count <= 20 else "large" if count <= 40 else "hall"
        pit = name in {"catwalk_hall","sump_room","flooding_pit_freeze"}
        for x,z in sorted(cells):
            grating = pit or kind != "room" or (name == "boiler_room" and x in (2,3))
            pieces.append(placement('floor_sump_2x2' if pit else "floor_grate_2x2" if grating else "floor_2x2",
                                    2*x+1,-1.32 if pit else -.12 if grating else -.1001,2*z+1))
            pieces.append(placement("ceiling_2x2",2*x+1,HEIGHT-.34,2*z+1))

            # Ceiling-only duct hangers remain kit assets, not unsupported
            # floor-or-wall template placements. Wall pipe services follow.

        # Dense wall services, kept away from the full door-wall reserves.
        for entry in list(pieces):
            if entry["id"] == "wall_2m":
                x,y,z = entry["pos"]
                yaw = entry["rotY"]
                a = math.radians(yaw)
                # Do not drive a full-length service module into a perpendicular
                # corner wall. Leave corner bays for elbows, not straight runs.
                tangent = (round(math.cos(a)),round(-math.sin(a)))
                cx,cz = math.floor((x-.1*math.sin(a))/2),math.floor((z-.1*math.cos(a))/2)
                if any((cx+sign*tangent[0],cz+sign*tangent[1]) not in cells for sign in (-1,1)):
                    continue
                # The one-cell-deep bunker has no standing clearance beneath
                # waist-height services opposite its door. Keep those overhead.
                pieces.append(placement("pipe_run_wall_2m",x-.28*math.sin(a),2.1 if name=='fuel_bunker' else .7,z-.28*math.cos(a),yaw))
        # Reserve central cell centres for gameplay. Props occupy selected edge cells.
        blocked = set()
        def prop(piece,x,z,y=0,yaw=0):
            pieces.append(placement(piece,2*x+1,y,2*z+1,yaw))
            blocked.add((x,z))
        if name == "boiler_room":
            for x in (0,3,6):
                prop("prop_boiler",x,4)
                prop("prop_pump",x,3)
                prop("prop_gauge_panel",x,5,1.1)
            for x in (1,5):
                prop("pillar",x,2)
        elif name == "pump_room":
            for x,z in ((0,1),(3,1),(0,3),(3,3)):
                prop("prop_pump",x,z)
        elif name == "electrical_room":
            for x in (0,2):
                prop("prop_electrical_cabinet",x,1)
        elif name == "storage_cage":
            for z in (0,2):
                prop("prop_storage_cage",2,z)
        elif name == "fuel_bunker":
            prop("prop_fuel_bunker",3,0)
        elif name == "valve_gallery":
            for x,z in ((3,0),(0,3)):
                prop("prop_valve_wheel",x,z,1.1)
                prop("prop_gauge_panel",x,z,1.85)
        elif name == "steam_vent_traversal":
            for x in (1,2,3):
                prop("prop_steam_vent",x,1)
            prop("prop_gauge_panel",4,1,1.2)
        elif pit:
            prop("prop_pump",0,1)
            prop("prop_valve_wheel",w-1,d-2,1)
            # A narrow central catwalk flanked by perforated maintenance walkways.
            for z in range(1,d-1):
                pieces.append(placement("catwalk_rail_2m",4.0,0,2*z+1,90))
                pieces.append(placement("catwalk_rail_2m",5.8,0,2*z+1,90))
        else:
            cell = min(sorted(cells),key=lambda c:abs(c[0])+abs(c[1]-(d-2)))
            prop("prop_gauge_panel",*cell,1.3)
        # Non-uniform leaks; no emissive orange outside the sodium lamp material.
        for x,z in sorted(cells):
            if (x+3*z)%7 == 0 and (x,z) not in blocked:
                pieces.append(placement("puddle_decal_quad",2*x+1,.002,2*z+1))
        available = [c for c in sorted(cells,key=lambda c:(c[1],c[0])) if c not in blocked]
        cake_count = max(2,(count*2+8)//9) if count > 4 else 1
        assert len(available) >= cake_count, (name, 'insufficient cake density')
        chosen = available[::max(1,len(available)//cake_count)][:cake_count]
        anchors = {"cake":[[2*x+1,0,2*z+1] for x,z in chosen],"goldenCake":[],"light":[],"hunterSpawn":[]}
        if count >= 10:
            x,z = available[-1]
            anchors["hunterSpawn"] = [[2*x+1,0,2*z+1]]
        if size in {"large","hall"}:
            x,z = available[-2]
            anchors["goldenCake"] = [[2*x+1,0,2*z+1]]
        for x,z in sorted(cells):
            if (x in (0,w-1) and z%3 == 1) or (count < 10 and x == 1 and z == 0):
                live = not (x == w-1 and z == 1 and count > 10)
                pieces.append(placement("cage_lamp" if live else "cage_lamp_dead",2*x+1,2.18,2*z+1))
                if live:
                    anchors["light"].append([2*x+1,2.12,2*z+1])
        if not anchors["light"]:
            x,z = available[0]
            pieces.append(placement("cage_lamp",2*x+1,2.18,2*z+1))
            anchors["light"].append([2*x+1,2.12,2*z+1])
        sys.path.insert(0,str(Path(__file__).resolve().parent))
        from env_theme_castle import seat_wall_props
        seat_wall_props(pieces,'Basement',KINDS,{'prop_gauge_panel','prop_valve_wheel',
            'cage_lamp','cage_lamp_dead'},minimum_width=2)
        for p in pieces:
            if p['id'] in ('cage_lamp','cage_lamp_dead'):
                p['pos'][1] = round(HEIGHT-bpy.data.objects['Basement_'+p['id']].dimensions.z,6)
        anchors['light'] = []
        for p in pieces:
            if p['id']=='cage_lamp':
                a=math.radians(p['rotY'])
                anchors['light'].append([round(p['pos'][0]-.49*math.sin(a),5),
                    round(p['pos'][1]-.06,5),round(p['pos'][2]-.49*math.cos(a),5)])
        templates.append({"id":"basement_"+name,"kind":kind,"sizeClass":size,"shape":shape,
                          "footprint":[list(c) for c in sorted(cells)],"height":HEIGHT,"doors":doors,
                          "anchors":anchors,"gimmick":gimmick,"minRound":3 if gimmick != "none" else 1,
                          "weight":.6 if gimmick != "none" else 1.0,"pieces":pieces})
    corrections = {
        'boiler_room': {'cake': [[1,0,1],[5,0,1],[13,0,1],[5,0,3],[11,0,3],
                               [5,0,5],[11,0,7],[9,0,7],[5,0,9],[3,0,9]]},
        'pump_room': {'cake': [[1,0,1],[7,0,1],[3,0,5],[5,0,5]]},
        'pipe_tunnel': {'cake': [[1,0,1],[2,0,4],[2,0,8]]},
        'service_bend': {'cake': [[1,0,1],[5,0,3],[3,0,3],[9,0,3]]},
        'duct_junction': {'cake': [[1,0,1],[9,0,3],[7,0,3],[7,0,5]]},
        'catwalk_hall': {'cake': [[1,0,1],[9,0,3],[7,0,3],[3,0,5],[9,0,5],[7,0,7],[5,0,7]]},
        'flooding_pit_freeze': {'cake': [[1,0,1],[7,0,1],[5,0,3],[5,0,5]]},
    }
    for t in templates:
        t['anchors'].update(corrections.get(t['id'].removeprefix('basement_'), {}))
    return {"theme":"basement","module":2.0,"templates":templates}


def instance(scene, source, name, pos, yaw=0):
    obj = bpy.data.objects.new(name,source.data)
    scene.collection.objects.link(obj)
    obj.location = xyz(pos)
    obj.rotation_euler.z = math.radians(yaw)
    return obj


def lighting(scene,name,pos,power,color,kind="AREA",target=None,size=5):
    data = bpy.data.lights.new(name,kind)
    data.energy,data.color = power,color
    if kind == "AREA":
        data.shape,data.size = "DISK",size
    elif kind == "POINT":
        data.shadow_soft_size = .13
    else:
        data.spot_size,data.spot_blend = math.radians(52),.55
        data.shadow_soft_size = .04
    obj = bpy.data.objects.new(name,data)
    scene.collection.objects.link(obj)
    obj.location = xyz(pos)
    if target:
        obj.rotation_euler = (xyz(target)-obj.location).to_track_quat("-Z","Y").to_euler()
    return obj


def setup_scene(scene):
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.seed = 260930
    scene.cycles.use_denoising = True
    scene.render.resolution_x,scene.render.resolution_y = 1200,800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world = bpy.data.worlds.new(scene.name+" ambient")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.12,.14,.16,1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .18
    scene.view_settings.view_transform = "AgX"


def camera(scene,pos,target,scale=None):
    obj = bpy.data.objects.new("ReviewCamera",bpy.data.cameras.new("ReviewCamera"))
    scene.collection.objects.link(obj)
    scene.camera = obj
    obj.location = xyz(pos)
    obj.rotation_euler = (xyz(target)-obj.location).to_track_quat("-Z","Y").to_euler()
    obj.data.clip_start = .03
    if scale:
        obj.data.type,obj.data.ortho_scale = "ORTHO",scale
    else:
        obj.data.lens = 23
    return obj


def render(scene,filename):
    bpy.context.window.scene = scene
    scene.render.filepath = str(REVIEW/filename)
    bpy.ops.render.render(write_still=True)
    receipt = {
        "imageSha256":hashlib.sha256((REVIEW/filename).read_bytes()).hexdigest(),
        "kitManifestSha256":hashlib.sha256((ART/"Kit/BasementKit.manifest.json").read_bytes()).hexdigest(),
        "roomsManifestSha256":hashlib.sha256((ART/"Rooms/BasementRooms.manifest.json").read_bytes()).hexdigest(),
        "worldStrength":scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value,
        "lights":[{"name":o.name,"type":o.data.type,"energy":o.data.energy}
                  for o in scene.objects if o.type == "LIGHT" and not o.hide_render],
        "hiddenPlacements":[int(o["placement_index"]) for o in scene.objects
                            if "placement_index" in o and o.hide_render]}
    (REVIEW/(filename+".json")).write_text(json.dumps(receipt,indent=2)+"\n",encoding="utf-8",newline="\n")


def sheet(pieces,previews):
    scene = bpy.data.scenes.new("Basement kit sheet")
    setup_scene(scene)
    scene.render.resolution_x,scene.render.resolution_y = 1800,1400
    for i,(name,obj) in enumerate(pieces.items()):
        x,z = (i%7)*4.4,(i//7)*5
        instance(scene,obj,"Sheet_"+name,(x,0,z))
        text = bpy.data.curves.new("Label_"+name,"FONT")
        text.body,text.size,text.align_x = name,.23,"CENTER"
        label = bpy.data.objects.new("Label_"+name,text)
        scene.collection.objects.link(label)
        label.location = xyz((x,-.02,z-1.5))
        # Text lies on the ground, readable from the south.
        label.data.materials.append(bpy.data.materials["basement_insulation"])
    camera(scene,(31,32,-25),(13,0,14),48)
    lighting(scene,"Sheet softbox",(9,20,-3),9000,(1,.92,.8),target=(13,0,12),size=18)
    lighting(scene,"Sheet fill",(25,13,22),6500,(.72,.83,1),target=(13,0,12),size=14)
    bpy.context.window.scene = scene
    bpy.context.view_layer.update()
    if previews:
        render(scene,"kit-sheet.png")


def rooms_source(pieces,manifest,previews):
    # One independently inspectable scene per template, all at their true origin.
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"Rooms/BasementRooms.blend"))
    scenes = []
    for template in manifest["templates"]:
        scene = bpy.data.scenes.new(template["id"])
        setup_scene(scene)
        scene["template_id"] = template["id"]
        width = 2*(max(c[0] for c in template["footprint"])+1)
        depth = 2*(max(c[1] for c in template["footprint"])+1)
        cutaway = []
        for i,entry in enumerate(template["pieces"]):
            obj = instance(scene,pieces[entry["id"]],f"{i:04d}_{entry['id']}",entry["pos"],entry["rotY"])
            obj["placement_index"] = i
            obj["piece_id"] = entry["id"]
            # Render-only cuts; the saved source retains every manifest placement.
            kind = KINDS[entry["id"]]
            hidden = ((kind in {"wall","door","window"} and entry["rotY"] in (90,180)) or
                      (kind == "ceiling" and (entry["pos"][2] < depth-2 or entry["pos"][0] < width/2)) or
                      (entry["id"] == "duct_run_ceiling_2m" and entry["pos"][2] < depth/2))
            if hidden:
                obj.hide_render = True
                cutaway.append(obj)
        for i,pos in enumerate(template["anchors"]["light"]):
            lighting(scene,"Sodium cage "+str(i),pos,95,linear(PALETTE["sodium"]),"POINT")
        fill = lighting(scene,"Review softbox",(width*.5,11,-3),1900,(.8,.86,1),target=(width/2,0,depth/2),size=9)
        cam = camera(scene,(width+7,max(width,depth)*.8+5,-8),(width/2,.4,depth/2),max(width,depth)*1.5+4)
        bpy.context.window.scene = scene
        bpy.context.view_layer.update()
        scenes.append(scene)
        if previews:
            render(scene,template["id"]+".png")
        if template["id"] == "basement_boiler_room":
            for obj in cutaway:
                obj.hide_render = False
            fill.hide_render = True
            scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0
            cam.data.type,cam.data.lens = "PERSP",23
            cam.location = xyz((3,1.65,.8))
            cam.rotation_euler = (xyz((6,1.5,9))-cam.location).to_track_quat("-Z","Y").to_euler()
            spot = lighting(scene,"Flashlight only",(3,1.58,.95),190,(.84,.9,1),"SPOT",(6,1.4,9))
            if previews:
                render(scene,"in-darkness.png")
            spot.hide_render = True
            for obj in cutaway:
                obj.hide_render = True
            fill.hide_render = False
            scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .18
            cam.data.type,cam.data.ortho_scale = "ORTHO",max(width,depth)*1.5+4
            cam.location = xyz((width+7,max(width,depth)*.8+5,-8))
            cam.rotation_euler = (xyz((width/2,.4,depth/2))-cam.location).to_track_quat("-Z","Y").to_euler()
    bpy.context.window.scene = scenes[0]
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"Rooms/BasementRooms.blend"))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--skip-previews",action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if bpy.app.version[:2] != (5,2):
        raise RuntimeError("Blender 5.2 required")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    for path in (ART/"Kit",ART/"Rooms",SOURCE/"Kit",SOURCE/"Rooms",REVIEW):
        path.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"Kit/BasementKit.blend"))
    bpy.context.scene.name = "Basement source meshes (origin aligned)"
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    materials()
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from env_theme_vaults import PIECES, build_vault, add_vaults
    KINDS[PIECES['basement']] = 'prop'
    pieces,rows = {},[]
    for name,kind in KINDS.items():
        builder = BasementMesh(name)
        if name == PIECES['basement']:
            build_vault('basement', builder)
        else:
            build(builder)
        obj = builder.finish(kind)
        export(obj,ART/"Kit"/(obj.name+".fbx"))
        points = [v.co for v in obj.data.vertices]
        size = [round(max(v[i] for v in points)-min(v[i] for v in points),6) for i in (0,2,1)]
        rows.append({"id":name,"file":obj.name+".fbx","size":size,"kind":kind})
        pieces[name] = obj
        print(f"BUILT {name}: {len(obj.data.loop_triangles)} triangles")
    kit = {"theme":"basement","wallHeight":HEIGHT,"pieces":rows}
    rooms = catalogue()
    add_vaults(rooms['templates'], rows, 'basement')
    for path,value in ((ART/"Kit/BasementKit.manifest.json",kit),(ART/"Rooms/BasementRooms.manifest.json",rooms)):
        path.write_text(json.dumps(value,indent=2)+"\n",encoding="utf-8",newline="\n")
    if not args.skip_previews:
        sys.path.insert(0, str(Path(__file__).resolve().parent))
        from env_theme_castle import render_door_reviews
        render_door_reviews(pieces, KINDS, 'Basement', REVIEW)
    sheet(pieces,not args.skip_previews)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"Kit/BasementKit.blend"))
    rooms_source(pieces,rooms,not args.skip_previews)
    print(f"GENERATED Basement: {len(pieces)} pieces, {len(rooms['templates'])} room templates")


if __name__ == "__main__":
    main()
