# ============================================================================
# generate_theme_textures.py
# PURPOSE:
#   Generate original repeatable PBR surfaces for the four environment kits.
#   Periodic fields, wrapped feature placement and wrapped height derivatives
#   avoid image-border seams without painting over or copying edge pixels.
# ARCHITECTURAL ROLE: Offline art generator; outside Unity runtime layers.
# KEY RESPONSIBILITIES:
#   - Read current kit palette declarations without executing Blender scripts.
#   - Generate albedo, tangent normals and URP metallic/smoothness textures.
#   - Publish per-slot scale/palette metadata and reproducibility hashes.
#   - Render tiled contact sheets and dim warm/cold walls for visual inspection.
# DEPENDENCIES: Standard library, NumPy, Pillow, material_fields; no downloads.
# USAGE NOTES:
#   python tools/textures/generate_theme_textures.py [--size 512|1024]
#   All output stays inside this worktree. No Unity, Library or meta operations.
# ============================================================================
import argparse
import ast
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from material_fields import recipe_fields

ROOT = Path(__file__).resolve().parents[2]
TEXTURES = ROOT / 'Assets/Art/Textures'
REVIEW = ROOT / 'tools/textures/review'
THEMES = ('Castle', 'Hospital', 'School', 'Basement')
SEED = 261001
METRES = 2.0

# Recipe, smoothness, construction relief in metres, bare-metal fraction.
# Provisional art values; material_fields adds dimensional damage. Metadata
# retains the source kit palette for adoption, while surfaces include local
# pigments (ivory glaze, rust, exposed plaster and mineral aggregate).
PROFILES = {
    'stone': (.12, .030, 0), 'mortar': (.08, .004, 0),
    'plaster': (.17, .003, 0), 'concrete': (.14, .008, 0),
    'wood': (.23, .006, 0), 'parquet': (.35, .003, 0),
    'tile': (.55, .004, 0), 'vinyl': (.32, .001, 0),
    'terrazzo': (.42, .0012, 0), 'brick': (.1, .018, 0),
    'iron': (.32, .005, .72), 'steel': (.52, .001, .85),
    'painted_metal': (.38, .002, .05), 'rust': (.08, .006, .03),
    'fabric': (.12, .007, 0), 'rubber': (.16, .0005, 0),
    'glass': (.86, .00008, 0), 'chalkboard': (.12, .0003, 0),
    'grime': (.06, .001, 0), 'paper': (.12, .0002, 0),
    'acoustic': (.08, .0015, 0), 'light': (.55, .00015, 0),
    'water': (.92, .0003, 0), 'hazard': (.30, .001, .05),
}
RECIPES = {
    'Castle': {'stone': 'stone', 'stone_dark': 'stone', 'soot': 'grime',
               'mortar': 'mortar', 'wood': 'wood', 'metal': 'iron', 'ember': 'light'},
    'Hospital': {'tile': 'tile', 'paint': 'plaster', 'grout': 'mortar',
                 'light': 'light', 'vinyl': 'vinyl', 'rust': 'rust',
                 'stainless': 'steel', 'rubber': 'rubber', 'glass': 'glass',
                 'curtain': 'fabric', 'linen': 'fabric', 'acoustic': 'acoustic',
                 'door_enamel': 'painted_metal', 'terrazzo': 'terrazzo'},
    'School': {'mustard': 'plaster', 'teal': 'painted_metal',
               'chalk_green': 'chalkboard', 'lino': 'vinyl', 'cream': 'plaster',
               'steel': 'steel', 'fire_red': 'painted_metal',
               'mortar_upper': 'mortar', 'mortar_lower': 'mortar', 'rubber': 'rubber',
               'wood': 'wood', 'scuff': 'grime', 'stain': 'grime', 'glass': 'glass',
               'paper': 'paper', 'tube': 'light', 'dead_tube': 'light',
               'door_laminate': 'wood', 'parquet': 'parquet'},
    'Basement': {'concrete': 'concrete', 'damp': 'grime', 'rust': 'rust',
                 'steel': 'iron', 'galvanised': 'steel', 'insulation': 'fabric',
                 'sodium': 'light', 'hazard': 'hazard', 'door_steel': 'iron',
                 'water': 'water', 'brick': 'brick'},
}


def palettes():
    """AST literal read only: never import/execute somebody else's generator."""
    result = {}
    for theme in THEMES:
        source = ROOT / f'tools/blender/env_theme_{theme.lower()}.py'
        tree = ast.parse(source.read_text(encoding='utf-8'))
        value = next(ast.literal_eval(n.value) for n in tree.body
                     if isinstance(n, ast.Assign)
                     and any(isinstance(t, ast.Name) and t.id == 'PALETTE' for t in n.targets))
        result[theme] = {k: '#' + v.lstrip('#') for k, v in value.items()}
    result['Castle']['ember'] = '#ffb347'  # Additional material in materials().
    result['Basement']['water'] = result['Basement']['steel']
    # Requested surfaces not yet assigned by kit owners. These are ready-to-use,
    # not silently substituted onto concrete walls or existing linoleum floors.
    result['Hospital']['terrazzo'] = result['Hospital']['vinyl']
    result['School']['parquet'] = result['School']['wood']
    result['Basement']['brick'] = result['Basement']['rust']
    for theme, palette in result.items():
        if set(palette) != set(RECIPES[theme]):
            raise ValueError(f'{theme}: palette/recipe drift; review new slots explicitly')
        manifest = json.loads((ROOT / f'Assets/Art/Environment/{theme}/Kit/{theme}Kit.manifest.json').read_text())
        slots = {s for p in manifest['pieces'] for s in p.get('materials', [])}
        missing = slots - {f'{theme.lower()}_{s}' for s in palette}
        if missing:
            raise ValueError(f'Uncovered manifest materials: {sorted(missing)}')
    return result


def linear(rgb):
    rgb = np.asarray(rgb)
    return np.where(rgb <= .04045, rgb / 12.92, ((rgb + .055) / 1.055) ** 2.4)


def srgb(rgb):
    rgb = np.clip(rgb, 0, 1)
    return np.where(rgb <= .0031308, rgb * 12.92, 1.055 * rgb ** (1 / 2.4) - .055)


def noise(size, cells, rng):
    """Periodic cubic-interpolated lattice, sampled at texel centres (no duplicate edge)."""
    grid = rng.uniform(-1, 1, (cells, cells))
    p = (np.arange(size) + .5) * cells / size
    i = np.floor(p).astype(int) % cells
    f = p % 1
    f = f * f * (3 - 2 * f)
    a = grid[i[:, None], i] * (1 - f) + grid[i[:, None], (i + 1) % cells] * f
    b = grid[(i[:, None] + 1) % cells, i] * (1 - f) + grid[(i[:, None] + 1) % cells, (i + 1) % cells] * f
    return a * (1 - f[:, None]) + b * f[:, None]


def flecks(size, rng, count, radius):
    """Small irregular aggregate grains placed on a torus, including across edges."""
    out = np.zeros((size, size))
    for _ in range(count):
        x, y = rng.integers(0, size, 2)
        r = max(1, int(rng.uniform(.45, 1.4) * radius * size))
        yy, xx = np.mgrid[-r:r + 1, -r:r + 1]
        mask = (abs(xx / r) ** 1.4 + abs(yy / r) ** 1.8) < rng.uniform(.55, 1.1)
        out[(y + yy[mask]) % size, (x + xx[mask]) % size] = rng.uniform(.35, 1)
    return out


def normal_from_height(height, metres=METRES):
    # PNG row index points down; Unity tangent +V points up. Green is +Y/OpenGL.
    step = metres / height.shape[0]
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) / (2 * step)
    dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) / (2 * step)
    normal = np.stack((-dx, dy, np.ones_like(dx)), axis=-1)
    return normal / np.linalg.norm(normal, axis=-1, keepdims=True)


def generate_surface(theme, surface, hex_color, size):
    slot = f'{theme.lower()}_{surface}'
    seed = int.from_bytes(hashlib.sha256(f'{SEED}:{slot}'.encode()).digest()[:8], 'little')
    rng = np.random.default_rng(seed)
    recipe = RECIPES[theme][surface]

    y, x = (np.mgrid[0:size, 0:size] + .5) / size
    broad = .55 * noise(size, 4, rng) + .45 * noise(size, 8, rng)
    medium = .65 * noise(size, 16, rng) + .35 * noise(size, 32, rng)
    fine = .6 * noise(size, 64, rng) + .4 * noise(size, 128, rng)
    micro = rng.uniform(-1, 1, (size, size))
    aggregate = flecks(size, rng, 2400 if recipe == 'terrazzo' else 1200,
                       .005 if recipe == 'terrazzo' else .0025)
    base = linear([int(hex_color[i:i + 2], 16) / 255 for i in (1, 3, 5)])
    color, height, smooth, metallic = recipe_fields(
        theme, recipe, base, x, y, broad, medium, fine, micro, aggregate, rng, PROFILES[recipe])
    albedo = srgb(color)
    normal = normal_from_height(height)
    packed = np.stack((np.clip(metallic, 0, 1), np.zeros_like(x), np.zeros_like(x), np.clip(smooth, .02, .96)), axis=-1)
    return albedo, normal, packed


def bytes_image(values):
    return np.rint(np.clip(values, 0, 1) * 255).astype(np.uint8)


def lit_wall(albedo, normal, packed, temperature='warm'):
    """Dim CPU GGX wall at fixed exposure; no tone-map or per-material gain."""
    size = albedo.shape[0]
    y, x = np.mgrid[0:size, 0:size] / size
    light = np.stack((.9 - x, .3 + y, np.full_like(x, .38)), axis=-1)
    light /= np.linalg.norm(light, axis=-1, keepdims=True)
    half = light + np.array([0, 0, 1])
    half /= np.linalg.norm(half, axis=-1, keepdims=True)
    nl = np.maximum(0, np.sum(normal * light, axis=-1))
    nv = np.maximum(.001, normal[..., 2])
    nh = np.maximum(0, np.sum(normal * half, axis=-1))
    vh = np.maximum(0, half[..., 2])
    rough = np.maximum(.06, 1 - packed[..., 3])
    a2 = rough ** 4
    d = a2 / (np.pi * (nh * nh * (a2 - 1) + 1) ** 2)
    k = (rough + 1) ** 2 / 8
    g = nl / (nl * (1 - k) + k) * nv / (nv * (1 - k) + k)
    color = linear(albedo)
    metal = packed[..., 0, None]
    f0 = .04 * (1 - metal) + color * metal
    f = f0 + (1 - f0) * (1 - vh[..., None]) ** 5
    specular = d[..., None] * g[..., None] * f / np.maximum(4 * nl * nv, .001)[..., None]
    illuminant = np.array([1, .57, .28] if temperature == 'warm' else [.38, .62, 1])
    result = color * .035 + ((1 - f) * (1 - metal) * color / np.pi + specular) * nl[..., None] * 1.1 * illuminant
    return bytes_image(srgb(result))


def save_if_changed(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_bytes() != content:
        path.write_bytes(content)


def png_bytes(array):
    import io
    stream = io.BytesIO()
    Image.fromarray(array).save(stream, format='PNG', optimize=False, compress_level=9)
    return stream.getvalue()


def contact_sheet(theme, samples):
    # Three 3x3 map columns plus warm/cold walls. Save row crops for inspection.
    thumb = 100
    panel = thumb * 3
    sheet = Image.new('RGB', (5 * (panel + 12) + 12, len(samples) * (panel + 40) + 60), '#181b20')
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), f'{theme} | {METRES:g} m/tile | Albedo 3x3 / Normal 3x3 / Smoothness 3x3 / Dim warm / Dim cold', fill='white')
    for row, (slot, albedo, normal, packed) in enumerate(samples):
        py = 60 + row * (panel + 40)
        draw.text((12, py - 20), slot, fill='white')
        maps = (bytes_image(albedo), bytes_image(normal * .5 + .5), bytes_image(packed[..., 3]))
        for col, image in enumerate(maps):
            tile = Image.fromarray(image).convert('RGB').resize((thumb, thumb), Image.Resampling.LANCZOS)
            for iy in range(3):
                for ix in range(3):
                    sheet.paste(tile, (12 + col * (panel + 12) + ix * thumb, py + iy * thumb))
        for col, temperature in enumerate(('warm', 'cold'), 3):
            wall = Image.fromarray(lit_wall(albedo, normal, packed, temperature)).resize((panel, panel), Image.Resampling.LANCZOS)
            sheet.paste(wall, (12 + col * (panel + 12), py))
        save_if_changed(REVIEW / 'materials' / f'{slot}.png',
                        png_bytes(np.asarray(sheet.crop((0, py - 24, sheet.width, py + panel + 12)))))
    path = REVIEW / f'{theme}-contact.png'
    save_if_changed(path, png_bytes(np.asarray(sheet)))
    return str(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--size', type=int, choices=(512, 1024), default=1024)
    args = parser.parse_args()
    report = {'seed': SEED, 'resolution': args.size, 'metresPerTile': METRES, 'files': {}, 'contactSheets': []}
    for theme, palette in palettes().items():
        samples = []
        for surface, color in sorted(palette.items()):
            slot = f'{theme.lower()}_{surface}'
            albedo, normal, packed = generate_surface(theme, surface, color, args.size)
            maps = {'Albedo': bytes_image(albedo), 'Normal': bytes_image(normal * .5 + .5), 'Smoothness': bytes_image(packed)}
            for suffix, pixels in maps.items():
                path = TEXTURES / theme / f'{slot}_{suffix}.png'
                content = png_bytes(pixels)
                save_if_changed(path, content)
                report['files'][path.relative_to(ROOT).as_posix()] = hashlib.sha256(content).hexdigest()
            metadata = {'metresPerTile': METRES, 'paletteSrgb': color, 'recipe': RECIPES[theme][surface],
                        'resolution': args.size, 'seed': SEED, 'normalConvention': 'OpenGL +Y',
                        'packing': 'Albedo=sRGB RGB; Normal=linear RGB; Smoothness=linear R:metallic A:smoothness GB:0'}
            save_if_changed(TEXTURES / theme / f'{slot}.json', (json.dumps(metadata, indent=2) + '\n').encode())
            # Reviews use the exact quantized delivered maps, not float sources.
            samples.append((slot, maps['Albedo'] / 255., maps['Normal'] / 127.5 - 1,
                            maps['Smoothness'] / 255.))
        report['contactSheets'].append(contact_sheet(theme, samples))
        print(f'GENERATED {theme}: {len(samples)} slots, {args.size}px, {METRES:g} metres/tile', flush=True)
    save_if_changed(REVIEW / 'generation.json', (json.dumps(report, indent=2) + '\n').encode())
    print(f'TEXTURE_RESULT maps={len(report["files"])} contactSheets={len(report["contactSheets"])}')


if __name__ == '__main__':
    main()
