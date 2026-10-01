# ============================================================================
# render_distance_review.py
# PURPOSE:
#   Check the delivered maps at explicit two- and four-metre screen footprints.
#   This CPU preview filters linear data before shading, so tiny normal details
#   cannot survive by shading at full resolution then shrinking the result.
# ARCHITECTURAL ROLE: Offline art review tool; outside runtime layers; Textures.
# KEY RESPONSIBILITIES:
#   - Load the exact delivered PNG maps, not regenerated float source fields.
#   - Filter maps to a fixed FOV/distance footprint and render warm/cold walls.
#   - Save per-slot comparisons and measured relief statistics for review.
# DEPENDENCIES: NumPy, Pillow and the local generator's CPU GGX swatch renderer.
# USAGE NOTES:
#   python tools/textures/render_distance_review.py
#   Front-facing 2 m wall, 60 degree vertical FOV, 384 px square viewport.
#   GGX view direction is the swatch's parallel approximation, not Unity optics.
# ============================================================================
import json
import numpy as np
from PIL import Image, ImageDraw

import generate_theme_textures as g

VIEWPORT = 384
FOV = 60
DISTANCES = (2, 4)


def filter_map(data, side):
    return np.stack([np.asarray(Image.fromarray(data[..., c].astype(np.float32)).resize(
        (side, side), Image.Resampling.BOX)) for c in range(data.shape[-1])], axis=-1)


def render_slot(theme, slot):
    folder = g.TEXTURES / theme
    albedo = g.linear(np.asarray(Image.open(folder / f'{slot}_Albedo.png')) / 255.)
    normal = np.asarray(Image.open(folder / f'{slot}_Normal.png')) / 127.5 - 1
    packed = np.asarray(Image.open(folder / f'{slot}_Smoothness.png')) / 255.
    row = Image.new('RGB', (VIEWPORT * 4, VIEWPORT + 44), '#101216')
    draw = ImageDraw.Draw(row)
    metrics = {}
    for index, distance in enumerate(DISTANCES):
        side = round(VIEWPORT * g.METRES / (2 * distance * np.tan(np.radians(FOV / 2))))
        a = g.srgb(filter_map(albedo, side))
        n = filter_map(normal, side)
        n /= np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-8)
        p = filter_map(packed, side)
        metrics[str(distance)] = {'wallPixels': side, 'normalXyRms': float(np.sqrt(np.mean(n[..., :2] ** 2)))}
        for temperature_index, temperature in enumerate(('warm', 'cold')):
            column = index * 2 + temperature_index
            origin = column * VIEWPORT
            draw.text((origin + 8, 8), f'{slot} | {distance} m | {temperature}', fill='white')
            image = Image.fromarray(g.lit_wall(a, n, p, temperature))
            row.paste(image, (origin + (VIEWPORT - side) // 2, 36 + (VIEWPORT - side) // 2))
    g.save_if_changed(g.REVIEW / 'distance' / f'{slot}.png', g.png_bytes(np.asarray(row)))
    return metrics


def main():
    metrics = {}
    for theme, palette in g.palettes().items():
        for surface in sorted(palette):
            slot = f'{theme.lower()}_{surface}'
            metrics[slot] = render_slot(theme, slot)
    report = {'viewport': VIEWPORT, 'verticalFov': FOV, 'wallMetres': g.METRES,
              'distancesMetres': DISTANCES, 'materials': metrics}
    g.save_if_changed(g.REVIEW / 'distance-metrics.json', (json.dumps(report, indent=2) + '\n').encode())
    print(f'DISTANCE_RESULT materials={len(metrics)} distances={DISTANCES} lighting=warm,cold')


if __name__ == '__main__':
    main()
