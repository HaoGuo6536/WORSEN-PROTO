"""Render actual C# geometry-probe TSV output, not regenerated manifest geometry.

Editor evidence tool, Tests/Procedural. Requires the already-installed Pillow.
Usage: python Assets/Editor/Tests/Procedural/render_geometry_evidence.py <directory>
This is a collision/layout cutaway, not an imported-art or Unity lighting render.
"""
import csv
import itertools
import math
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def rotate(q, p):
    x, y, z, w = q
    a, b, c = p
    u = (y*c-z*b, z*a-x*c, x*b-y*a)
    v = (y*u[2]-z*u[1], z*u[0]-x*u[2], x*u[1]-y*u[0])
    return tuple(p[i] + 2*(w*u[i]+v[i]) for i in range(3))


def render(path):
    rows = list(csv.reader(path.open(encoding="utf-8"), delimiter="\t"))
    boxes, cakes = [], []
    for row in rows:
        center = tuple(map(float, row[3:6]))
        if row[0] == "Cake":
            cakes.append(center)
            continue
        size, rotation = tuple(map(float, row[6:9])), tuple(map(float, row[9:13]))
        corners = []
        for signs in itertools.product((-1, 1), repeat=3):
            offset = rotate(rotation, tuple(size[i]*signs[i]/2 for i in range(3)))
            corners.append(tuple(center[i]+offset[i] for i in range(3)))
        boxes.append((row[0], row[1], row[2], center, size, corners))
    floors = [b for b in boxes if b[0] == "Floor"]
    low = [min(p[i] for b in floors for p in b[5]) for i in (0, 2)]
    high = [max(p[i] for b in floors for p in b[5]) for i in (0, 2)]
    scale = min(510/(high[i]-low[i]) for i in range(2))
    image = Image.new("RGB", (1800, 700), (23, 26, 32))
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 18)
    small = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 14)
    draw.text((20, 12), path.stem + " | actual C# block output", font=font, fill="white")
    upper = max((max(p[1] for p in b[5]) for b in floors if 3 <= max(p[1] for p in b[5]) <= 3.61), default=3.2)
    for panel, level in enumerate((0, upper)):
        def xy(p):
            return (30+panel*600+(p[0]-low[0])*scale, 615-(p[2]-low[1])*scale)
        draw.text((30+panel*600, 50), f"Standing level {level:.1f} m", font=font, fill="white")
        for b in sorted(boxes, key=lambda item: item[3][1]):
            kind, role, piece, center, size, corners = b
            ymin, ymax = min(p[1] for p in corners), max(p[1] for p in corners)
            if kind == "Ceiling" or role in ("VisualOnly", "KitVisual"):
                continue
            floor = kind == "Floor" and abs(ymax-level) < .015
            obstacle = ymax > level+.02 and ymin < level+2
            if not floor and not obstacle:
                continue
            color = (67, 73, 82) if floor else (185, 137, 89)
            if role == "StairRamp":
                color = (48, 153, 186)
            elif role == "PlayerOnly":
                color = (166, 91, 162)
            elif piece and kind != "Floor":
                color = (124, 92, 69)
            points = sorted({xy(p) for p in corners})
            cx = sum(p[0] for p in points)/len(points)
            cy = sum(p[1] for p in points)/len(points)
            points.sort(key=lambda p: math.atan2(p[1]-cy, p[0]-cx))
            draw.polygon(points, fill=color, outline=(26, 30, 38))
        for p in cakes:
            if abs(p[1]-level) < .2:
                x, y = xy(p)
                draw.ellipse((x-3, y-3, x+3, y+3), fill=(255, 222, 93))
    # Isometric cutaway: remove the roof and full-height exterior wall commands.
    draw.text((1225, 50), "Upper deck / ramp cutaway", font=font, fill="white")
    def iso(p):
        x, y, z = p
        return (1460+(x-low[0]-(z-low[1]))*19, 530+(x-low[0]+z-low[1])*8-y*34)
    visible = [b for b in boxes if b[0] != "Ceiling" and b[1] not in ("VisualOnly", "KitVisual") and
               not (b[0] == "Wall" and b[4][1] > 3.3)]
    for kind, role, piece, center, size, corners in sorted(visible, key=lambda b: (b[3][1], b[3][0]+b[3][2])):
        color = (92, 101, 116) if kind == "Floor" else (132, 94, 62)
        if role == "StairRamp":
            color = (48, 153, 186)
        elif role == "PlayerOnly":
            color = (166, 91, 162)
        for face in ((0, 1, 5, 4), (4, 5, 7, 6), (2, 3, 7, 6)):
            draw.polygon([iso(corners[i]) for i in face], fill=color, outline=(30, 34, 42))
    for p in cakes:
        if p[1] >= 3:
            x, y = iso(p)
            draw.ellipse((x-3, y-3, x+3, y+3), fill=(255, 222, 93))
    draw.text((25, 660), "Grey: floor/deck   Brown: collision   Cyan: hunter ramp   Purple: player-only platform   Yellow: cake", font=small, fill="white")
    image.save(path.with_suffix(".png"))
    print(path.with_suffix(".png"))


if __name__ == "__main__":
    root = Path(sys.argv[1])
    if root.is_file():
        render(root)
        raise SystemExit(0)
    for theme in ("castle", "hospital", "school", "basement"):
        paths = sorted(root.glob(theme + "*-turn*.tsv"))
        if not paths:
            raise SystemExit("Missing real geometry evidence for " + theme)
        render(paths[0])
