# ============================================================================
# env_kit_castlehospital.py
# PURPOSE:
#   Rebuild original Castle and Hospital modular art without external assets.
#   Authored relief, bevels and wear replace featureless primitive rooms while
#   retaining exact structural boundaries for the procedural room assembler.
# ARCHITECTURAL ROLE: Offline art generator; outside the Unity runtime layers.
# KEY RESPONSIBILITIES:
#   - Build deterministic metre-scale meshes and material textures.
#   - Export isolated pieces and a measured, reproducible manifest.
#   - Save editable sources outside Assets and render assembled-room previews.
# DEPENDENCIES: Blender 5.2 bpy/bmesh/mathutils and Python standard library.
# USAGE NOTES:
#   Run Blender headless with --python-exit-code 1 --python this-file --
#   --theme castle|hospital|all. --skip-previews is for repeat-generation checks.
#   Blender +Z becomes exported +Y; Blender +Y becomes exported -Z (interior).
#   No Unity operations. Only use in the assigned, unopened art worktree.
# ============================================================================
import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
HEIGHTS = {"castle": 7.0, "hospital": 3.6}
SEED = 260930
TEXTURE_SIZE = 512
PALETTES = {
    "castle": {"stone": (.31, .285, .235), "stone_dark": (.22, .225, .20),
               "mortar": (.13, .14, .13), "wood": (.19, .095, .038),
               "metal": (.105, .115, .12), "cloth": (.24, .035, .025),
               "ember": (.95, .26, .025)},
    "hospital": {"plaster": (.49, .50, .43), "tile": (.27, .40, .37),
                 "tile_stained": (.20, .29, .25), "metal": (.22, .26, .25),
                 "rubber": (.025, .032, .031), "fabric": (.42, .49, .43),
                 "glass": (.105, .19, .18)},
}
COMMON = ("wall_2m", "wall_door_4m", "wall_window_2m", "wall_arc_r4",
          "wall_arc_r6", "wall_arc_r8", "corner_in", "corner_out", "pillar",
          "floor_2x2", "ceiling_2x2", "trim_base_2m")
PROPS = {"castle": ("prop_torch_sconce", "prop_banner", "prop_barrel", "prop_rubble"),
         "hospital": ("prop_bed", "prop_curtain_rail", "prop_cabinet", "prop_wheelchair")}


class KitMesh:
    """Small mesh accumulator. Coordinates are Blender Z-up, +Y interior."""

    def __init__(self, theme, name):
        self.theme, self.name = theme, name
        self.vertices, self.faces, self.surfaces = [], [], []
        self.rng = random.Random(f"{SEED}:{theme}:{name}")

    def add(self, vertices, faces, surface):
        start = len(self.vertices)
        self.vertices.extend(tuple(v) for v in vertices)
        self.faces.extend(tuple(start + i for i in f) for f in faces)
        self.surfaces.extend([surface] * len(faces))

    def box(self, center, size, surface, bevel=0):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1)
        for v in bm.verts:
            v.co = Vector(tuple(v.co[i] * size[i] for i in range(3)))
        if bevel:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel,
                           segments=1, affect="EDGES")
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.verts.ensure_lookup_table()
        bm.verts.index_update()
        self.add([v.co + Vector(center) for v in bm.verts],
                 [tuple(v.index for v in f.verts) for f in bm.faces], surface)
        bm.free()

    def panel(self, x0, x1, z0, z1, surface, front=.25, back=.19, bevel=.025):
        # Open back is buried in the continuous structural core; 10 triangles.
        # Exact outer corners, uneven inset corners: relief never breaks seams.
        b = min(bevel, (x1 - x0) / 5, (z1 - z0) / 5)
        j = self.rng.uniform(-b * .2, b * .2)
        vertices = [(x0, back, z0), (x0, back, z1), (x1, back, z1), (x1, back, z0),
                    (x0+b, front, z0+b), (x0+b+j, front, z1-b),
                    (x1-b, front, z1-b+j), (x1-b-j, front, z0+b)]
        self.add(vertices, [(4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5),
                            (2, 3, 7, 6), (3, 0, 4, 7)], surface)

    def tube(self, a, b, radius, surface, sides=8, profile=None):
        a, b = Vector(a), Vector(b)
        length = (b-a).length
        rotation = (b-a).to_track_quat("Z", "Y").to_matrix()
        profile = profile or [(0, radius), (length, radius)]
        vertices = [a + rotation @ Vector((r*math.cos(2*math.pi*i/sides),
                    r*math.sin(2*math.pi*i/sides), z)) for z, r in profile for i in range(sides)]
        faces = [tuple(reversed(range(sides))),
                 tuple((len(profile)-1)*sides+i for i in range(sides))]
        for row in range(len(profile)-1):
            for i in range(sides):
                j = (i+1) % sides
                faces.append((row*sides+i, row*sides+j, (row+1)*sides+j, (row+1)*sides+i))
        self.add(vertices, faces, surface)

    def ring(self, center, radius, thickness, surface, axis="X", sides=16):
        transform = Matrix.Rotation(math.pi/2, 3, "Y") if axis == "X" else Matrix.Identity(3)
        vertices = []
        for i in range(sides):
            u = 2*math.pi*i/sides
            for j in range(4):
                v = 2*math.pi*j/4
                p = Vector(((radius+thickness*math.cos(v))*math.cos(u),
                            (radius+thickness*math.cos(v))*math.sin(u), thickness*math.sin(v)))
                vertices.append(Vector(center) + transform @ p)
        faces = [(i*4+j, ((i+1)%sides)*4+j, ((i+1)%sides)*4+(j+1)%4,
                  i*4+(j+1)%4) for i in range(sides) for j in range(4)]
        self.add(vertices, faces, surface)

    def finish(self, centered=False):
        if centered:
            lo = [min(v[i] for v in self.vertices) for i in range(3)]
            hi = [max(v[i] for v in self.vertices) for i in range(3)]
            shift = ((lo[0]+hi[0])/2, (lo[1]+hi[1])/2, lo[2])
            self.vertices = [tuple(v[i]-shift[i] for i in range(3)) for v in self.vertices]
        data = bpy.data.meshes.new(self.name)
        data.from_pydata(self.vertices, [], self.faces)
        data.update()
        obj = bpy.data.objects.new(self.name, data)
        bpy.context.collection.objects.link(obj)
        surfaces = sorted(set(self.surfaces))
        for surface in surfaces:
            data.materials.append(bpy.data.materials[f"{self.theme}_{surface}"])
        for polygon, surface in zip(data.polygons, self.surfaces):
            polygon.material_index = surfaces.index(surface)
        # Explicit world-scale box projection; UVs travel with FBX exports.
        uv = data.uv_layers.new(name="SurfaceMetres")
        for p in data.polygons:
            axis = max(range(3), key=lambda i: abs(p.normal[i]))
            axes = [i for i in range(3) if i != axis]
            for loop in p.loop_indices:
                co = data.vertices[data.loops[loop].vertex_index].co
                uv.data[loop].uv = (co[axes[0]], co[axes[1]])
        # Freeze triangulation before hashing/export, not importer-dependent.
        bm = bmesh.new()
        bm.from_mesh(data)
        bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method="FIXED", ngon_method="EAR_CLIP")
        bm.to_mesh(data)
        bm.free()
        data.update()
        return obj


def make_materials(theme, art):
    for surface, rgb in PALETTES[theme].items():
        name = f"{theme}_{surface}"
        material = bpy.data.materials.new(name)
        material.diffuse_color = (*rgb, 1)
        shader = material.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = (*rgb, 1)
        shader.inputs["Roughness"].default_value = .48 if surface in ("tile", "glass") else .86
        shader.inputs["Metallic"].default_value = .65 if surface == "metal" else 0
        if surface == "ember":
            shader.inputs["Emission Color"].default_value = (*rgb, 1)
            shader.inputs["Emission Strength"].default_value = 2
        if surface not in ("stone", "plaster", "tile"):
            continue
        image = bpy.data.images.new(name + "_wear", width=TEXTURE_SIZE, height=TEXTURE_SIZE)
        rng = random.Random(f"{SEED}:{name}:texture")
        pixels = []
        for y in range(TEXTURE_SIZE):
            for x in range(TEXTURE_SIZE):
                # Periodic low-frequency damp blooms plus fine mineral pitting.
                u, v = 2*math.pi*x/TEXTURE_SIZE, 2*math.pi*y/TEXTURE_SIZE
                stain = max(0, math.sin(u+1.1*math.sin(v)) * math.cos(2*v-.6*math.sin(u)))
                value = .90 - .34*stain + rng.uniform(-.065, .065)
                # Material base values are linear; PNG pixels are encoded sRGB.
                linear = [max(0, min(1, c*value)) for c in rgb]
                pixels.extend([12.92*c if c <= .0031308 else 1.055*c**(1/2.4)-.055 for c in linear] + [1])
        image.pixels.foreach_set(pixels)
        image.filepath_raw = str(art / (name + "_wear.png"))
        image.file_format = "PNG"
        image.save()
        image.pack()
        node = material.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = image
        material.node_tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])


def dressed_wall(mesh, x0, x1, z0, z1):
    castle = mesh.theme == "castle"
    base = "mortar" if castle else "plaster"
    mesh.box(((x0+x1)/2, -.03, (z0+z1)/2), (x1-x0, .44, z1-z0), base)
    if castle:
        rows = max(1, round((z1-z0)/1.0))
        columns = max(1, round((x1-x0)/1.0))
        for row in range(rows):
            # Alternating bond widths without additional boundary vertices.
            cuts = [x0] + [x0+(x1-x0)*(i/columns + (.07 if row % 2 else -.07))
                           for i in range(1, columns)] + [x1]
            for a, b in zip(cuts, cuts[1:]):
                low, high = z0+(z1-z0)*row/rows, z0+(z1-z0)*(row+1)/rows
                surface = "stone_dark" if mesh.rng.random() < .2 else "stone"
                mesh.panel(a+.014, b-.014, low+.014, high-.014, surface,
                           front=.25 if row == 0 else mesh.rng.uniform(.228, .25))
    else:
        tile_top = min(z1, 1.45)
        if tile_top > z0:
            rows = max(1, round((tile_top-z0)/.48))
            columns = max(1, round((x1-x0)/.5))
            for row in range(rows):
                for col in range(columns):
                    a, b = x0+(x1-x0)*col/columns, x0+(x1-x0)*(col+1)/columns
                    low, high = z0+(tile_top-z0)*row/rows, z0+(tile_top-z0)*(row+1)/rows
                    surface = "tile_stained" if mesh.rng.random() < .22 else "tile"
                    mesh.panel(a+.008, b-.008, low+.008, high-.008, surface, bevel=.008)
        if z1 > max(z0, 1.45):
            mesh.panel(x0+.012, x1-.012, max(z0, 1.45)+.012, z1-.012,
                       "plaster", front=.25, bevel=.012)


def arc_wall(mesh, radius, angle, height):
    steps = 4
    half = math.radians(angle)/2
    vertices = []
    # Continuous core follows radius +/- .25. Interior surface is r-.25.
    for i in range(steps+1):
        t = -half+2*half*i/steps
        for r, z in ((radius-.25, 0), (radius+.25, 0), (radius+.25, height), (radius-.25, height)):
            vertices.append((r*math.sin(t), radius-r*math.cos(t), z))
    faces = [(3, 2, 1, 0), tuple(steps*4+i for i in range(4))]
    for i in range(steps):
        for j in range(4):
            faces.append((i*4+j, i*4+(j+1)%4, (i+1)*4+(j+1)%4, (i+1)*4+j))
    mesh.add(vertices, [tuple(reversed(face)) for face in faces],
             "mortar" if mesh.theme == "castle" else "plaster")
    # 4x5 shallow bevelled blocks; no detail reaches either radial end cap.
    rows = 5 if mesh.theme == "castle" else 4
    for row in range(rows):
        for col in range(steps):
            t0, t1 = -half+2*half*col/steps, -half+2*half*(col+1)/steps
            # Add panels in an unrolled wall, then map to the chord facet.
            start = len(mesh.vertices)
            surface = "stone_dark" if row % 3 == 0 else "stone"
            if mesh.theme == "hospital":
                surface = "tile" if row < 2 else "plaster"
            width = 2*(radius-.25)*math.sin((t1-t0)/2)
            mesh.panel(-width/2+.015, width/2-.015, row*height/rows+.015,
                       (row+1)*height/rows-.015, surface, front=.025, back=0, bevel=.014)
            mid = (t0+t1)/2
            rr = (radius-.25)*math.cos((t1-t0)/2)
            for i in range(start, len(mesh.vertices)):
                x, y, z = mesh.vertices[i]
                mesh.vertices[i] = (rr*math.sin(mid)+x*math.cos(mid)-y*math.sin(mid),
                                    radius-rr*math.cos(mid)+x*math.sin(mid)+y*math.cos(mid), z)


def architecture(theme, piece):
    m = KitMesh(theme, theme.title()+"_"+piece)
    h = HEIGHTS[theme]
    stone = "stone" if theme == "castle" else "tile"
    if piece == "wall_2m":
        dressed_wall(m, -1, 1, 0, h)
    elif piece == "wall_door_4m":
        dressed_wall(m, -2, -1.6, 0, h)
        dressed_wall(m, 1.6, 2, 0, h)
        dressed_wall(m, -1.6, 1.6, 2.8, h)
    elif piece == "wall_window_2m":
        dressed_wall(m, -1, -.65, 0, h)
        dressed_wall(m, .65, 1, 0, h)
        dressed_wall(m, -.65, .65, 0, 1.1)
        dressed_wall(m, -.65, .65, 2.5, h)
        # Clear 1.3 x 1.4 m opening; no bars or pane block a traversal route.
    elif piece.startswith("wall_arc"):
        r = int(piece.rsplit("r", 1)[1])
        arc_wall(m, r, {4: 30, 6: 20, 8: 15}[r], h)
    elif piece.startswith("corner"):
        # L cap: 0.5 x 0.5 footprint, 0.25 thick legs. Pivot at wall-plane junction.
        m.box((0, -.125, h/2), (.5, .25, h), stone)
        m.box((-.125, .125, h/2), (.25, .25, h), stone)
        for z in (.18, h-.18):
            m.box((0, -.125, z), (.5, .25, .24), stone, .018)
        if piece == "corner_out":
            m.vertices = [(-x, -y, z) for x, y, z in m.vertices]
    elif piece == "pillar":
        if theme == "castle":
            m.tube((0, 0, 0), (0, 0, h), .32, stone, 8,
                   [(0, .4), (.18, .4), (.30, .29), (h-.35, .29), (h-.18, .4), (h, .4)])
        else:
            m.box((0, 0, h/2), (.5, .5, h), "plaster", .028)
            m.box((0, 0, .12), (.6, .6, .24), "tile", .025)
            m.box((0, 0, h-.08), (.6, .6, .16), "metal", .018)
    elif piece in ("floor_2x2", "ceiling_2x2"):
        ceiling = piece.startswith("ceiling")
        thick = .18 if ceiling else .16
        core_height = thick-.028
        core_center = core_height/2 + (.028 if ceiling else 0)
        m.box((0, 0, core_center), (2, 2, core_height), "mortar" if theme == "castle" else "metal")
        start = len(m.vertices)
        count = 3 if theme == "castle" else 4
        for row in range(count):
            for col in range(count):
                a, b = -1+2*col/count, -1+2*(col+1)/count
                c, d = -1+2*row/count, -1+2*(row+1)/count
                surface = stone if not ceiling else ("stone_dark" if theme == "castle" else "plaster")
                m.panel(a+.012, b-.012, c+.012, d-.012, surface,
                        front=0, back=.028, bevel=.012)
        # Turn panel +Y normals into floor +Z / ceiling -Z. Relief is inset,
        # never floats above the structural tile or changes its snap dimensions.
        for i in range(start, len(m.vertices)):
            x, y, z = m.vertices[i]
            m.vertices[i] = (x, z if ceiling else -z, y if ceiling else thick-y)
    elif piece == "trim_base_2m":
        m.box((0, 0, .09), (2, .12, .18), stone)
        # Raised middle profile stops before ends; core seam remains exact.
        m.box((0, .015, .095), (1.97, .09, .13), stone, .012)
    return m.finish()


def castle_prop(piece):
    m = KitMesh("castle", "Castle_"+piece)
    if piece == "prop_torch_sconce":
        m.box((0, -.12, .30), (.20, .06, .45), "metal", .025)
        m.tube((0, -.10, .25), (0, .19, .38), .032, "metal")
        m.tube((0, .16, .22), (0, .20, .80), .043, "wood", 10)
        m.tube((0, .20, .65), (0, .20, .9), .10, "metal", 10,
               [(0, .06), (.16, .12), (.21, .11)])
        m.tube((0, .20, .82), (0, .20, 1.12), .07, "ember", 7,
               [(0, .065), (.13, .09), (.30, .005)])
    elif piece == "prop_banner":
        m.tube((-.55, 0, 2.3), (.55, 0, 2.3), .035, "metal", 10)
        # Two-sided thick, folded and frayed cloth; no backface-only sheet.
        vertices = []
        columns, rows = 8, 5
        for side in (-1, 1):
            for row in range(rows+1):
                for col in range(columns+1):
                    x = -.45+.9*col/columns
                    bottom = .05+(.12 if col % 3 == 0 else 0)
                    z = bottom+(2.22-bottom)*row/rows
                    y = .045*math.cos(col*math.pi/2)+.018*math.sin(row*1.7)+side*.006
                    vertices.append((x, y, z))
        faces = []
        n = (rows+1)*(columns+1)
        for side in range(2):
            for row in range(rows):
                for col in range(columns):
                    a = side*n+row*(columns+1)+col
                    face = (a, a+columns+1, a+columns+2, a+1)
                    faces.append(face if side else tuple(reversed(face)))
        boundary = list(range(columns+1)) + [r*(columns+1)+columns for r in range(1, rows+1)]
        boundary += list(range(rows*(columns+1)+columns-1, rows*(columns+1)-1, -1))
        boundary += [r*(columns+1) for r in range(rows-1, 0, -1)]
        faces += [(a, b, b+n, a+n) for a, b in zip(boundary, boundary[1:]+boundary[:1])]
        m.add(vertices, faces, "cloth")
        m.box((0, .07, 1.45), (.11, .008, .56), "wood")
        m.box((0, .075, 1.56), (.40, .008, .10), "wood")
    elif piece == "prop_barrel":
        # Separate tapered staves and raised hoops, not a straight cylinder.
        sides = 12
        for i in range(sides):
            t0, t1 = 2*math.pi*(i+.025)/sides, 2*math.pi*(i+.975)/sides
            vertices = [(r*math.cos(t), r*math.sin(t), z)
                        for z, r in ((0, .33), (.18, .40), (.57, .45), (.98, .4), (1.12, .33))
                        for t in (t0, t1)]
            m.add(vertices, [(j*2, j*2+1, j*2+3, j*2+2) for j in range(4)], "wood")
        for z, r in ((.16, .4), (.55, .457), (.98, .4)):
            m.tube((0, 0, z-.04), (0, 0, z+.04), r, "metal", 12)
        m.tube((0, 0, 0), (0, 0, 1.12), .325, "wood", 12)
        for y in (-.20, -.067, .067, .20):
            m.box((0, y, 1.125), (.46, .008, .01), "metal")
    elif piece == "prop_rubble":
        for i in range(9):
            x, y = m.rng.uniform(-.65, .65), m.rng.uniform(-.4, .4)
            size = (m.rng.uniform(.2, .48), m.rng.uniform(.18, .4), m.rng.uniform(.1, .3))
            first = len(m.vertices)
            m.box((0, 0, size[2]/2), size, "stone_dark" if i % 3 else "stone", .035)
            rotation = Matrix.Rotation(m.rng.uniform(-1, 1), 3, "Z")
            for j in range(first, len(m.vertices)):
                m.vertices[j] = tuple(rotation @ Vector(m.vertices[j]) + Vector((x, y, 0)))
    return m.finish(centered=True)


def hospital_prop(piece):
    m = KitMesh("hospital", "Hospital_"+piece)
    if piece == "prop_bed":
        m.box((0, 0, .47), (1.0, 2.05, .12), "metal", .025)
        m.box((0, 0, .60), (.96, 1.98, .20), "fabric", .05)
        m.box((0, -.72, .74), (.70, .43, .12), "fabric", .045)
        for y in (-.97, .97):
            for x in (-.43, .43):
                m.tube((x, y, .12), (x, y, 1.08), .024, "metal")
                m.ring((x, y, .10), .075, .025, "rubber", sides=12)
            for z in (.78, 1.05):
                m.tube((-.43, y, z), (.43, y, z), .023, "metal")
        for x in (-.47, .47):
            m.tube((x, -.70, .82), (x, .70, .82), .022, "metal")
    elif piece == "prop_curtain_rail":
        m.tube((-1, 0, 2.5), (1, 0, 2.5), .025, "metal")
        for x in (-.9, .9):
            m.tube((x, 0, 2.5), (x, 0, 2.8), .016, "metal")
        # Bunched curtain occupies only one side, preserving a walk-through gap.
        for i in range(12):
            x = -.95+i*.065
            y = .04 if i % 2 else -.04
            m.box((x, y, 1.35), (.068, .018, 2.1-m.rng.uniform(0, .04)), "fabric")
            m.ring((x, 0, 2.43), .05, .008, "metal", sides=8)
    elif piece == "prop_cabinet":
        m.box((0, 0, 1.0), (.86, .44, 1.80), "metal", .035)
        for x in (-.205, .205):
            m.box((x, .245, 1.10), (.395, .04, 1.52), "tile", .015)
            m.box((x, .27, 1.42), (.29, .02, .70), "glass", .012)
            m.tube((x*.3, .30, .87), (x*.3, .30, 1.02), .014, "metal")
        for x in (-.34, .34):
            for y in (-.16, .16):
                m.box((x, y, .09), (.065, .065, .18), "metal")
    elif piece == "prop_wheelchair":
        m.box((0, 0, .5), (.47, .43, .065), "fabric", .025)
        m.box((0, -.20, .75), (.47, .07, .46), "fabric", .025)
        for x in (-.32, .32):
            m.ring((x, -.10, .32), .285, .035, "rubber")
            m.ring((x*1.10, -.10, .32), .235, .012, "metal")
            for i in range(6):
                angle = 2*math.pi*i/6
                m.tube((x, -.10, .32), (x, -.10+.26*math.cos(angle), .32+.26*math.sin(angle)),
                       .006, "metal", 4)
            m.tube((x*.78, -.23, .25), (x*.78, -.23, 1.04), .017, "metal")
            m.tube((x*.78, -.23, .48), (x*.78, .36, .17), .018, "metal")
            m.tube((x*.78, -.23, .73), (x*.78, .20, .73), .020, "metal")
            m.ring((x*.78, .32, .09), .065, .025, "rubber", sides=10)
            m.box((x*.62, .42, .13), (.18, .22, .035), "metal")
    return m.finish(centered=True)


def geometry_hash(obj):
    rows = []
    for p in obj.data.polygons:
        vertices = []
        for i in p.vertices:
            v = obj.data.vertices[i].co
            vertices.append(tuple(0.0 if abs(c) < .000005 else round(c, 5) for c in (v.x, v.z, -v.y)))
        rows.append((obj.data.materials[p.material_index].name, sorted(vertices)))
    return hashlib.sha256(json.dumps(sorted(rows), separators=(",", ":")).encode()).hexdigest()


def piece_kind(piece):
    if piece.startswith("prop_"):
        return "prop"
    if piece.startswith("wall_"):
        return "door" if "door" in piece else "window" if "window" in piece else "arc" if "arc" in piece else "wall"
    return piece.split("_")[0] if not piece.startswith("corner") else "corner"


def export_piece(obj, piece, art):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(art/(obj.name+".fbx")), use_selection=True,
        object_types={"MESH"}, global_scale=1, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type="FACE",
        use_triangles=True, bake_anim=False, use_custom_props=False, path_mode="RELATIVE")
    lo = [min(v.co[i] for v in obj.data.vertices) for i in range(3)]
    hi = [max(v.co[i] for v in obj.data.vertices) for i in range(3)]
    kind = piece_kind(piece)
    triangles = len(obj.data.polygons)
    assert triangles <= (1500 if kind == "prop" else 300), (piece, triangles)
    return {"id": piece, "file": obj.name+".fbx", "size": [round(hi[i]-lo[i], 6) for i in (0, 2, 1)],
            "kind": kind, "triangles": triangles,
            "materials": sorted(mat.name for mat in obj.data.materials), "geometrySha256": geometry_hash(obj)}


def preview_room(theme, objects, output):
    scene = bpy.context.scene
    h = HEIGHTS[theme]
    originals = list(objects.values())
    for obj in originals:
        obj.hide_render = True
        obj.hide_set(True)
    preview = bpy.data.collections.new("Preview assembled room (not exported)")
    scene.collection.children.link(preview)

    def place(piece, xyz, angle=0):
        obj = objects[piece].copy()
        obj.data = objects[piece].data
        preview.objects.link(obj)
        obj.name = "Preview_"+piece
        obj.hide_render = False
        obj.hide_set(False)
        obj.location = xyz
        obj.rotation_euler.z = math.radians(angle)
        return obj

    # Open-front cutaway: the curved wing meets the door wall tangentially.
    # Partial floor/ceiling expose the arc run without bespoke filler geometry.
    place("wall_door_4m", (-2, -3, 0))
    for y in (-2, 0, 2):
        place("wall_window_2m" if y == -2 else "wall_2m", (-4, y, 0), -90)
        place("trim_base_2m", (-3.72, y, 0), -90)
    for x in (-3, -1):
        for y in (-2, 0, 2):
            place("floor_2x2", (x, y, -.16))
        # Partial ceiling leaves camera visibility, but demonstrates the roof.
        place("ceiling_2x2", (x, -2, h))
    for x, y in ((1, 0), (1, 2), (3, 2)):
        place("floor_2x2", (x, y, -.16))
    place("wall_2m", (4, 2, 0), 90)
    place("trim_base_2m", (3.72, 2, 0), 90)
    place("pillar", (-3.7, -.6, 0))
    # Adjacent r4 arc segments share one center. Lower camera sees the interior.
    center = Vector((0, 1.0, 0))
    for degrees in (15, 45, 75):
        t = math.radians(degrees)
        pos = center+Vector((4*math.sin(t), -4*math.cos(t), 0))
        place("wall_arc_r4", pos, degrees)
    if theme == "castle":
        place("prop_banner", (-2.0, -2.70, 3.2))
        place("prop_torch_sconce", (-3.8, -2.6, 1.7))
        place("prop_barrel", (-2.8, .7, 0))
        place("prop_rubble", (1.5, 1.0, 0))
    else:
        place("prop_bed", (-2.3, -.9, 0), 12)
        place("prop_curtain_rail", (-2.0, .2, 0))
        place("prop_cabinet", (1.0, -.6, 0), -8)
        place("prop_wheelchair", (1.5, 1.1, 0), -22)
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.cycles.seed = SEED
    scene.render.resolution_x, scene.render.resolution_y = 1200, 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world = bpy.data.worlds.new("Preview dark ambient")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (.11, .15, .19, 1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = .30
    for name, location, power, color, size in (
            ("Key", (0, 5, h*.85), 1800, (1, .76, .53) if theme == "castle" else (.72, .87, 1), 7),
            ("Door bounce", (0, -1, 2.5), 350, (1, .36, .09) if theme == "castle" else (.6, .9, .77), 3),
            ("Fill", (5, 3, 5), 800, (.51, .66, 1), 5)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.color, data.shape, data.size = power, color, "DISK", size
        lamp = bpy.data.objects.new(name, data)
        preview.objects.link(lamp)
        lamp.location = location
        lamp.rotation_euler = (Vector((0, 0, 1.5))-lamp.location).to_track_quat("-Z", "Y").to_euler()
    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    preview.objects.link(camera)
    scene.camera = camera
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 12.8 if theme == "castle" else 11.6
    for name, position in (("front", (0, 17, h*.69)), ("three-quarter", (10, 15, h+4))):
        camera.location = position
        camera.rotation_euler = (Vector((0, -.3, h*.42))-camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(output/(name+".png"))
        bpy.ops.render.render(write_still=True)
    # Source opens as the assembled review room. Originals remain exact at origin
    # in their own hidden collection for editing/export, never translated apart.


def generate(theme, skip_previews):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    art = ROOT / "Assets/Art/Environment" / theme.title() / "Kit"
    source = ROOT / "ArtSource/Environment" / theme.title() / "Kit"
    output = ROOT / "Logs/AgentValidation/Art" / ("Env"+theme.title())
    for directory in (art, source, output):
        directory.mkdir(parents=True, exist_ok=True)
    # Save the authoring path before modelling; never put .blend into Assets.
    bpy.ops.wm.save_as_mainfile(filepath=str(source/(theme.title()+"Kit.blend")))
    make_materials(theme, art)
    objects, rows = {}, []
    for piece in COMMON+PROPS[theme]:
        obj = architecture(theme, piece) if piece in COMMON else (castle_prop(piece) if theme == "castle" else hospital_prop(piece))
        objects[piece] = obj
        rows.append(export_piece(obj, piece, art))
    manifest = {"theme": theme, "wallHeight": HEIGHTS[theme], "pieces": rows}
    path = art/(theme.title()+"Kit.manifest.json")
    path.write_text(json.dumps(manifest, indent=2)+"\n", encoding="utf-8", newline="\n")
    if not skip_previews:
        preview_room(theme, objects, output)
    for image in bpy.data.images:
        if image.packed_file:
            image.filepath = bpy.path.relpath(str(art/(image.name+".png")), start=str(source))
    bpy.ops.wm.save_as_mainfile(filepath=str(source/(theme.title()+"Kit.blend")))
    print(f"GENERATED {theme}: {len(rows)} pieces; manifest SHA256 {hashlib.sha256(path.read_bytes()).hexdigest()}")


def main():
    parser = argparse.ArgumentParser(description="Deterministic Castle / Hospital kits; Blender 5.2 only.")
    parser.add_argument("--theme", choices=("castle", "hospital", "all"), default="all")
    parser.add_argument("--skip-previews", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if bpy.app.version[:2] != (5, 2):
        raise RuntimeError("Use the project-approved Blender 5.2")
    for theme in HEIGHTS if args.theme == "all" else (args.theme,):
        generate(theme, args.skip_previews)


if __name__ == "__main__":
    main()
