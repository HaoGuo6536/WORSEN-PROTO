# ============================================================================
# env_kit_schoolbasement.py
# PURPOSE: Rebuild original School and Basement modular environment art. Authored
#   bevels, worn panels and curved services replace placeholder block silhouettes;
#   the same metre-scale pieces are used in exports and the review-room assembly.
# ARCHITECTURAL ROLE: Offline art generator, outside Unity runtime layers.
# KEY RESPONSIBILITIES:
#   - Build deterministic modular architecture and theme props.
#   - Export applied, Y-up / -Z-forward FBX files and the shared kit manifest.
#   - Save editable sources outside Assets and render assembled review rooms.
# DEPENDENCIES: Blender 5.2 bpy/mathutils and Python standard library only.
# USAGE NOTES: Run headlessly with --python-exit-code 1, then -- --theme all.
#   All authored coordinates below are Unity XYZ metres (Y up, interior -Z).
#   Blender coordinates are (X, -Z, Y). No downloads, Unity calls or shared files.
#   Runtime missing-kit fallback belongs to the procedural generator owner.
# ============================================================================
import argparse
import json
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
HEIGHT = {"school": 3.8, "basement": 3.2}
# Provisional art direction: linear RGB; roughness .82, metal roughness .58.
PALETTES = {
    "school": {"plaster": (.42, .40, .30), "tile": (.19, .28, .22),
               "tile_worn": (.24, .31, .24), "metal": (.10, .15, .14),
               "wood": (.26, .13, .055), "chalk": (.58, .61, .48),
               "board": (.022, .052, .038), "grime": (.12, .13, .085),
               "floor": (.27, .25, .18), "glass": (.095, .15, .16)},
    "basement": {"plaster": (.25, .255, .23), "tile": (.20, .21, .18),
                 "tile_worn": (.29, .28, .23), "metal": (.16, .19, .18),
                 "rust": (.22, .075, .027), "grime": (.075, .09, .065),
                 "floor": (.18, .18, .15), "paint": (.26, .06, .028),
                 "gauge": (.58, .55, .40), "glass": (.07, .10, .09)},
}
COMMON = {"wall_2m": "wall", "wall_door_4m": "door", "wall_window_2m": "window",
          "wall_arc_r4": "arc", "wall_arc_r6": "arc", "wall_arc_r8": "arc",
          "corner_in": "corner", "corner_out": "corner", "pillar": "pillar",
          "floor_2x2": "floor", "ceiling_2x2": "ceiling", "trim_base_2m": "trim"}
PROPS = {"school": {"prop_desk": "prop", "prop_locker_bank": "prop",
                    "prop_chalkboard": "prop", "prop_chair": "prop"},
         "basement": {"pipe_straight_2m": "pipe", "pipe_elbow": "pipe",
                      "pipe_tee": "pipe", "duct_straight_2m": "duct",
                      "duct_elbow": "duct", "prop_valve_wheel": "prop",
                      "prop_boiler": "prop"}}


def xyz(p):
    return Vector((p[0], -p[2], p[1]))


def reset(theme):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    for surface, color in PALETTES[theme].items():
        mat = bpy.data.materials.new(theme + "_" + surface)
        mat.diffuse_color = (*color, 1)
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = (*color, 1)
        shader.inputs["Roughness"].default_value = .58 if surface == "metal" else .82
        shader.inputs["Metallic"].default_value = .65 if surface == "metal" else 0


class Piece:
    """Temporary part assembly; all transforms baked before joining/export."""

    def __init__(self, theme, name):
        self.theme, self.name, self.parts = theme, name, []
        self.rng = random.Random(theme + "/" + name + "/20260930")

    def add(self, obj, surface, bevel=0):
        obj.data.materials.append(bpy.data.materials[self.theme + "_" + surface])
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        if bevel:
            mod = obj.modifiers.new("Authored edge wear", "BEVEL")
            mod.width, mod.segments = bevel, 1
            bpy.ops.object.modifier_apply(modifier=mod.name)
        self.parts.append(obj)
        return obj

    def box(self, center, size, surface, bevel=0):
        bpy.ops.mesh.primitive_cube_add(size=1, location=xyz(center))
        obj = bpy.context.object
        obj.dimensions = (size[0], size[2], size[1])
        return self.add(obj, surface, bevel)

    def mesh(self, vertices, faces, surface):
        data = bpy.data.meshes.new(self.name + "Part")
        data.from_pydata([xyz(p) for p in vertices], [], faces)
        data.update()
        obj = bpy.data.objects.new(self.name + "Part", data)
        bpy.context.collection.objects.link(obj)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        return self.add(obj, surface)

    def rod(self, a, b, radius, surface, sides=12):
        a, b = xyz(a), xyz(b)
        bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius,
                                            depth=(b-a).length, location=(a+b)/2)
        obj = bpy.context.object
        obj.rotation_euler = (b-a).to_track_quat("Z", "Y").to_euler()
        return self.add(obj, surface)

    def sweep(self, centers, radius, surface, sides=12, rectangular=False):
        # Service paths lie in XZ; Y is the stable section vertical.
        vertices, faces = [], []
        for i, center in enumerate(centers):
            tangent = Vector(centers[min(i+1, len(centers)-1)]) - Vector(centers[max(0, i-1)])
            if i == 0:
                tangent = Vector((1, 0, 0))
            elif i == len(centers)-1:
                tangent = Vector((0, 0, 1))
            tangent.normalize()
            side = Vector((-tangent.z, 0, tangent.x))
            if rectangular:
                section = [(-radius[0], -radius[1]), (radius[0], -radius[1]),
                           (radius[0], radius[1]), (-radius[0], radius[1])]
            else:
                section = [(radius*math.cos(j*math.tau/sides), radius*math.sin(j*math.tau/sides)) for j in range(sides)]
            for u, v in section:
                vertices.append(Vector(center) + side*u + Vector((0, v, 0)))
        sides = 4 if rectangular else sides
        for i in range(len(centers)-1):
            for j in range(sides):
                a, b = i*sides+j, i*sides+(j+1)%sides
                faces.append((a, b, b+sides, a+sides))
        faces += [tuple(reversed(range(sides))), tuple((len(centers)-1)*sides+j for j in range(sides))]
        obj = self.mesh(vertices, faces, surface)
        # Recalculate normals, including bend end caps, independent of path direction.
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        return obj

    def finish(self, bottom_center=False):
        bpy.ops.object.select_all(action="DESELECT")
        for obj in self.parts:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = self.parts[0]
        if len(self.parts) > 1:
            bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = self.theme.title() + "_" + self.name
        bpy.context.scene.cursor.location = (0, 0, 0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        if bottom_center:
            lo = Vector(tuple(min(v.co[i] for v in obj.data.vertices) for i in range(3)))
            hi = Vector(tuple(max(v.co[i] for v in obj.data.vertices) for i in range(3)))
            shift = Vector(((lo.x+hi.x)/2, (lo.y+hi.y)/2, lo.z))
            for vertex in obj.data.vertices:
                vertex.co -= shift
        obj.data.update()
        obj["piece_id"], obj["theme"] = self.name, self.theme
        return obj


def wall(p, height, width=2, opening=None):
    # Flat boundary planes are never bevelled: all straight wall seams share
    # exactly these four Y cuts and Z=0/.25 vertices, independent of opening.
    levels = (0, 1.1, 2.8, height)
    for low, high in zip(levels, levels[1:]):
        spans = [(-width/2, width/2)]
        if opening == "door" and low < 2.8:
            spans = [(-2, -1.6), (1.6, 2)]
        elif opening == "window" and low == 1.1:
            spans = [(-1, -.65), (.65, 1)]
        for left, right in spans:
            p.box(((left+right)/2, (low+high)/2, .125), (right-left, high-low, .25), "plaster")
    # Tile/block face relief stays clear of the mating edges and portals.
    spans = [(-width/2+.018, width/2-.018)] if opening != "door" else [(-1.982, -1.62), (1.62, 1.982)]
    for left, right in spans:
        count = 4 if opening != "door" else 1
        for i in range(count):
            w = (right-left)/count
            p.box((left+(i+.5)*w, .53, -.024), (w-.014, 1.04, .048),
                  "tile_worn" if i % 3 == 1 else "tile", .007)
    if opening != "door":
        p.box((0, 1.072, -.027), (1.964, .04, .054), "metal")
    if opening == "window":
        p.box((0, 1.12, .09), (1.3, .04, .20), "metal")
        p.box((0, 2.78, .09), (1.3, .04, .20), "metal")
        p.box((0, 1.95, .12), (.045, 1.66, .045), "metal")
        p.box((0, 2.32, .12), (1.3, .035, .045), "metal")
    # Irregular stain geometry, rather than unsupported procedural shader nodes.
    for i in range(4):
        x = p.rng.uniform(-width/2+.08, width/2-.28)
        y = p.rng.uniform(3.0, height-.08)
        p.mesh([(x, y, -.001), (x+.13, y-.035, -.001),
                (x+.19, y-.18, -.001), (x+.08, y-.12, -.001)], [(0, 1, 2, 3)], "grime")


def arc(p, height, radius, degrees):
    # Radius is the interior surface; pivot is the bottom midpoint of its
    # tangent wall plane. Circle centre is (0,0,-radius), not the piece origin.
    vertices, faces = [], []
    cuts = (0, 1.1, 2.8, height)
    steps = 8
    for i in range(steps+1):
        angle = math.radians(degrees)*(i/steps-.5)
        for r in (radius, radius+.25):
            for y in cuts:
                vertices.append((r*math.sin(angle), y, r*math.cos(angle)-radius))
    for i in range(steps):
        a, b = i*8, (i+1)*8
        for j in range(3):
            faces.extend([(a+j, a+j+1, b+j+1, b+j),
                          (a+4+j, b+4+j, b+5+j, a+5+j)])
        faces.extend([(a, b, b+4, a+4), (a+3, a+7, b+7, b+3)])
    for j in range(3):
        faces.extend([(j, j+4, j+5, j+1),
                      (steps*8+j, steps*8+j+1, steps*8+j+5, steps*8+j+4)])
    obj = p.mesh(vertices, faces, "plaster")
    for surface in ("tile", "tile_worn"):
        obj.data.materials.append(bpy.data.materials[p.theme+"_"+surface])
    for poly in obj.data.polygons:
        y = sum(obj.data.vertices[v].co.z for v in poly.vertices)/len(poly.vertices)
        poly.material_index = 1 + poly.index % 2 if y < 1.1 else 0


def architecture(p, height):
    name = p.name
    if name in ("wall_2m", "wall_door_4m", "wall_window_2m"):
        wall(p, height, 4 if name == "wall_door_4m" else 2,
             "door" if "door" in name else "window" if "window" in name else None)
    elif name.startswith("wall_arc"):
        radius = int(name[-1])
        arc(p, height, radius, {4: 30, 6: 20, 8: 15}[radius])
    elif name.startswith("corner"):
        # 0.5m L return; rotate the outside profile for the exterior corner.
        p.box((0, height/2, .125), (.5, height, .25), "plaster")
        p.box((.125 if name == "corner_in" else -.125, height/2, -.125),
              (.25, height, .25), "tile")
        p.box((0, .10, -.262), (.49, .2, .025), "metal", .005)
    elif name == "pillar":
        p.box((0, height/2, 0), (.34, height, .34), "plaster", .015)
        for y in (.10, height-.10):
            p.box((0, y, 0), (.48, .20, .48), "tile", .015)
    elif name == "floor_2x2":
        p.box((0, .035, 0), (2, .07, 2), "grime")
        for x in (-.5, .5):
            for z in (-.5, .5):
                p.box((x, .085, z), (.988, .03, .988), "floor" if x*z > 0 else "tile_worn", .006)
        # Hairline cracks are geometry and survive the flat URP material mapping.
        p.mesh([(-.78, .1002, -.82), (-.32, .1002, -.25), (-.314, .1002, -.25),
                (-.774, .1002, -.82)], [(0, 1, 2, 3)], "grime")
    elif name == "ceiling_2x2":
        p.box((0, .11, 0), (2, .04, 2), "metal")
        for x in (-.5, .5):
            for z in (-.5, .5):
                p.box((x, .048 if x*z > 0 else .03, z), (.978, .06, .978), "plaster", .006)
    elif name == "trim_base_2m":
        p.box((0, .08, 0), (2, .16, .07), "tile")
        p.box((0, .17, .009), (2, .02, .052), "metal")


def school_prop(p):
    if p.name == "prop_desk":
        p.box((0, .75, 0), (1.12, .055, .65), "wood", .018)
        p.box((0, .56, .08), (.92, .20, .40), "metal", .008)
        for x in (-.44, .44):
            for z in (-.24, .24):
                p.rod((x, .025, z), (x*.91, .72, z*.9), .025, "metal")
            p.rod((x, .18, -.24), (x, .18, .24), .016, "metal")
        for i in range(5):
            x = p.rng.uniform(-.48, .32)
            p.box((x, .778, .08+i*.028), (.15, .001, .003), "grime")
    elif p.name == "prop_chair":
        p.box((0, .46, 0), (.43, .055, .43), "wood", .015)
        p.box((0, .77, .19), (.43, .24, .045), "wood", .012)
        for x in (-.17, .17):
            for z in (-.16, .16):
                p.rod((x*1.15, 0, z*1.15), (x, .46, z), .018, "metal")
            p.rod((x, .38, .16), (x, .86, .20), .018, "metal")
            p.rod((x, .19, -.16), (x, .19, .16), .012, "metal")
    elif p.name == "prop_locker_bank":
        p.box((0, .93, 0), (1.2, 1.86, .42), "metal", .018)
        for i, x in enumerate((-.4, 0, .4)):
            p.box((x, .95, -.223), (.374, 1.74, .028), "tile_worn" if i == 1 else "tile", .008)
            p.box((x+.12, .97, -.253), (.025, .14, .032), "metal", .006)
            p.box((x, 1.59, -.24), (.085, .035, .004), "chalk")
            for y in (1.43, 1.48, 1.53, .28, .33):
                p.box((x, y, -.24), (.23, .012, .004), "grime")
    elif p.name == "prop_chalkboard":
        p.box((0, .69, 0), (1.8, 1.38, .065), "wood", .014)
        p.box((0, .70, -.038), (1.70, 1.25, .018), "board")
        p.box((0, .04, -.082), (1.75, .028, .16), "metal", .007)
        # Erased-looking fragmented lines, not meaningless imported text/fonts.
        for i in range(18):
            x = p.rng.uniform(-.74, .43)
            y = p.rng.choice((.37, .60, .84, 1.08)) + p.rng.uniform(-.018, .018)
            p.box((x, y, -.048), (p.rng.uniform(.025, .19), .006, .001), "chalk")


def valve(p, center=(0, .30, 0), radius=.26):
    # Wheel lies in XY, faces -Z, with a projecting central stem.
    cx, cy, cz = center
    n, sides, vertices, faces = 16, 6, [], []
    for i in range(n):
        a = i*math.tau/n
        for j in range(sides):
            b = j*math.tau/sides
            r = radius+.025*math.cos(b)
            vertices.append((cx+r*math.cos(a), cy+r*math.sin(a), cz+.025*math.sin(b)))
    for i in range(n):
        for j in range(sides):
            faces.append((i*sides+j, ((i+1)%n)*sides+j,
                          ((i+1)%n)*sides+(j+1)%sides, i*sides+(j+1)%sides))
    p.mesh(vertices, faces, "paint")
    for i in range(4):
        a = i*math.tau/4
        p.rod((cx, cy, cz), (cx+radius*math.cos(a), cy+radius*math.sin(a), cz), .017, "metal", 8)
    p.rod((cx, cy, cz-.035), (cx, cy, cz+.18), .043, "metal")


def basement_prop(p):
    name = p.name
    if name == "pipe_straight_2m":
        p.rod((-1, .16, 0), (1, .16, 0), .12, "metal")
        for x in (-.94, .94):
            p.rod((x-.06, .16, 0), (x+.06, .16, 0), .16, "rust")
        for x in (-.55, .55):
            p.rod((x-.018, .16, 0), (x+.018, .16, 0), .135, "metal")
    elif name == "pipe_elbow":
        centers = [(.55*math.sin(i*math.pi/16), .16, .55*(1-math.cos(i*math.pi/16))) for i in range(9)]
        p.sweep(centers, .12, "metal")
        p.rod((-.08, .16, 0), (.04, .16, 0), .16, "rust")
        p.rod((.55, .16, .51), (.55, .16, .63), .16, "rust")
    elif name == "pipe_tee":
        p.rod((-.6, .16, 0), (.6, .16, 0), .12, "metal")
        p.rod((0, .16, 0), (0, .16, .6), .12, "metal")
        for a, b in (((-.6, .16, 0), (-.49, .16, 0)), ((.49, .16, 0), (.6, .16, 0)),
                     ((0, .16, .49), (0, .16, .6))):
            p.rod(a, b, .16, "rust")
    elif name == "duct_straight_2m":
        p.box((0, .28, 0), (2, .5, .66), "metal", .025)
        for x in (-.97, 0, .97):
            p.box((x, .28, 0), (.06, .56, .72), "rust", .008)
        p.mesh([(-.82, .535, -.30), (.78, .535, -.30), (.25, .545, .07),
                (-.43, .535, .30)], [(0, 1, 2), (0, 2, 3)], "tile_worn")
    elif name == "duct_elbow":
        centers = [(.85*math.sin(i*math.pi/12), .28, .85*(1-math.cos(i*math.pi/12))) for i in range(7)]
        p.sweep(centers, (.33, .25), "metal", rectangular=True)
        p.box((0, .28, 0), (.06, .56, .72), "rust", .008)
        p.box((.85, .28, .85), (.72, .56, .06), "rust", .008)
    elif name == "prop_valve_wheel":
        valve(p)
    elif name == "prop_boiler":
        p.rod((0, .20, 0), (0, 1.92, 0), .55, "metal", 20)
        p.rod((0, 1.92, 0), (0, 2.03, 0), .45, "metal", 20)
        for y in (.30, 1.75):
            p.rod((0, y-.035, 0), (0, y+.035, 0), .575, "rust", 20)
        for x in (-.34, .34):
            p.box((x, .11, 0), (.16, .22, .75), "metal", .015)
        p.rod((-.25, 2.0, 0), (-.25, 2.32, 0), .105, "rust")
        p.rod((.27, 1.15, -.42), (.27, 1.15, -.72), .085, "rust")
        valve(p, (.27, 1.15, -.76), .17)
        p.rod((-.19, 1.56, -.47), (-.19, 1.56, -.62), .11, "metal", 16)
        p.rod((-.19, 1.56, -.62), (-.19, 1.56, -.628), .092, "gauge", 16)
        p.rod((-.19, 1.56, -.634), (-.23, 1.615, -.634), .006, "grime", 6)


def export_piece(obj, target):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True, object_types={"MESH"},
        global_scale=1, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        use_mesh_modifiers=True, mesh_smooth_type="FACE", use_triangles=True,
        bake_anim=False, use_custom_props=False, path_mode="AUTO")


def assembly(pieces, theme):
    collection = bpy.data.collections.new("Review room (not exported)")
    bpy.context.scene.collection.children.link(collection)
    for obj in pieces.values():
        obj.hide_render = True
        obj.hide_set(True)

    def place(name, position, yaw=0):
        source = pieces[name]
        obj = bpy.data.objects.new("Review_"+name, source.data)
        collection.objects.link(obj)
        obj.location = xyz(position)
        obj.rotation_euler.z = math.radians(yaw)
        return obj

    h = HEIGHT[theme]
    for x in (-3, -1, 1, 3, 5, 7):
        for z in (-2, 0, 2):
            place("floor_2x2", (x, -.1002, z))
    place("wall_door_4m", (0, 0, 3))
    for x in (-3, 3):
        place("wall_window_2m" if x == -3 else "wall_2m", (x, 0, 3))
        place("trim_base_2m", (x, 0, 2.92))
    for z in (0, 2):
        place("wall_2m", (-4, 0, z), -90)
        place("trim_base_2m", (-3.92, 0, z), -90)
    # Connected r4 arc run: adjacent transformed end faces coincide exactly.
    for degrees in (15, 45, 75):
        a = math.radians(degrees)
        place("wall_arc_r4", (4+4*math.sin(a), 0, -1+4*math.cos(a)), degrees)
    place("pillar", (-3.8, 0, 2.8))
    place("corner_in", (-4, 0, 3))
    for x in (-3, -1):
        place("ceiling_2x2", (x, h, 2))
    if theme == "school":
        place("prop_locker_bank", (2.8, 0, 2.55))
        place("prop_chalkboard", (-3.1, 1.42, 2.89))
        for x, z, yaw in ((-2, .25, 8), (0, .3, -6), (1.8, -1.3, 17)):
            place("prop_desk", (x, 0, z), yaw)
            place("prop_chair", (x+.12, 0, z-.65), yaw+12)
        place("prop_chair", (3.4, 0, -.8), 65)
    else:
        place("prop_boiler", (2.7, 0, 1.9))
        for x in (-2, 0, 2):
            place("pipe_straight_2m", (x, 2.48, 2.62))
            place("duct_straight_2m", (x, 2.54, .8))
        place("pipe_elbow", (3.35, 2.48, 2.3))
        place("pipe_tee", (-1, 2.48, 2.4))
        place("duct_elbow", (3.35, 2.54, .55))
        place("prop_valve_wheel", (-2, 1.9, 2.35))
    return collection


def render_previews(theme, directory):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.cycles.seed = 260930
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1200, 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    for prop in scene.render.bl_rna.properties:
        if prop.identifier.startswith("use_stamp") and prop.type == "BOOLEAN":
            setattr(scene.render, prop.identifier, False)
    scene.world = bpy.data.worlds.new("Review darkness")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.07, .085, .10, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .35
    for name, position, power, color, size in (
        ("Warm entry", (-2, 5, -4), 1500, (1, .77, .50), 5),
        ("Cool ceiling", (3, 5, 1), 1200, (.60, .76, 1), 4),
        ("Room fill", (0, 2, -2), 350, (1, .9, .72), 3)):
        light = bpy.data.lights.new(name, "AREA")
        light.energy, light.color, light.shape, light.size = power, color, "DISK", size
        obj = bpy.data.objects.new(name, light)
        scene.collection.objects.link(obj)
        obj.location = xyz(position)
        obj.rotation_euler = (xyz((1, 1, 1))-obj.location).to_track_quat("-Z", "Y").to_euler()
    camera = bpy.data.objects.new("ReviewCamera", bpy.data.cameras.new("ReviewCamera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type, camera.data.ortho_scale = "ORTHO", 16.5
    for name, position in (("front", (1.5, 5.7, -14)), ("three-quarter", (11, 8, -13))):
        camera.location = xyz(position)
        camera.rotation_euler = (xyz((1.5, 1.3, .6))-camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(directory/(name+".png"))
        bpy.ops.render.render(write_still=True)


def generate(theme, previews=True):
    reset(theme)
    title = theme.title()
    art = ROOT/"Assets/Art/Environment"/title/"Kit"
    source = ROOT/"ArtSource/Environment"/title/"Kit"
    review = ROOT/"Logs/AgentValidation/Art"/("Env"+title)
    for path in (art, source, review):
        path.mkdir(parents=True, exist_ok=True)
    # Establish the editable source path before modelling, never under Assets.
    bpy.ops.wm.save_as_mainfile(filepath=str(source/(title+"Kit.blend")))
    pieces, rows = {}, []
    for name, kind in {**COMMON, **PROPS[theme]}.items():
        p = Piece(theme, name)
        if name in COMMON:
            architecture(p, HEIGHT[theme])
        elif theme == "school":
            school_prop(p)
        else:
            basement_prop(p)
        obj = p.finish(bottom_center=kind in {"prop", "pipe", "duct", "floor", "ceiling", "pillar", "trim"})
        obj.data.calc_loop_triangles()
        triangles = len(obj.data.loop_triangles)
        budget = 1500 if kind in {"prop", "pipe", "duct"} else 300
        if triangles > budget:
            raise ValueError(f"{obj.name}: {triangles} > {budget} triangles")
        points = [v.co for v in obj.data.vertices]
        size = [round(max(v[i] for v in points)-min(v[i] for v in points), 6) for i in (0, 2, 1)]
        filename = obj.name+".fbx"
        export_piece(obj, art/filename)
        pieces[name] = obj
        rows.append({"id": name, "file": filename, "size": size, "kind": kind})
        print(f"BUILT {obj.name}: {triangles} triangles, size={size}")
    (art/(title+"Kit.manifest.json")).write_text(json.dumps(
        {"theme": theme, "wallHeight": HEIGHT[theme], "pieces": rows}, indent=2)+"\n", encoding="utf-8", newline="\n")
    assembly(pieces, theme)
    if previews:
        render_previews(theme, review)
    bpy.ops.wm.save_as_mainfile(filepath=str(source/(title+"Kit.blend")))
    print(f"GENERATED {title}: {len(pieces)} pieces; original geometry, no external assets.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--theme", choices=("school", "basement", "all"), default="all")
    parser.add_argument("--skip-previews", action="store_true", help="Keep existing review PNGs on a repeat generation.")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if bpy.app.version[:2] != (5, 2):
        raise RuntimeError("Use the project Blender 5.2 installation.")
    for theme in HEIGHT if args.theme == "all" else (args.theme,):
        generate(theme, not args.skip_previews)


if __name__ == "__main__":
    main()
