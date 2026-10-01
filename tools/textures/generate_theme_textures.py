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
#   - Render tiled contact sheets and a lit material wall for visual inspection.
# DEPENDENCIES: Python standard library, NumPy and Pillow; no downloads.
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

ROOT = Path(__file__).resolve().parents[2]
TEXTURES = ROOT / 'Assets/Art/Textures'
REVIEW = ROOT / 'Logs/AgentValidation/Art/Textures'
THEMES = ('Castle', 'Hospital', 'School', 'Basement')
SEED = 261001
METRES = 2.0

# Recipe, smoothness, peak relief in metres, bare-metal fraction. Artistic,
# provisional values, not runtime tunings. The palette remains the kit's own.
PROFILES = {
    'stone': (.12, .008, 0), 'mortar': (.08, .002, 0),
    'plaster': (.17, .0018, 0), 'concrete': (.14, .004, 0),
    'wood': (.23, .002, 0), 'parquet': (.35, .002, 0),
    'tile': (.55, .002, 0), 'vinyl': (.32, .0005, 0),
    'terrazzo': (.42, .001, 0), 'brick': (.1, .009, 0),
    'iron': (.32, .002, .72), 'steel': (.52, .0005, .85),
    'painted_metal': (.38, .001, .05), 'rust': (.08, .003, .03),
    'fabric': (.12, .0005, 0), 'rubber': (.16, .0004, 0),
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
    gloss, relief, metal = PROFILES[recipe]
    y, x = (np.mgrid[0:size, 0:size] + .5) / size
    broad = .55 * noise(size, 4, rng) + .45 * noise(size, 8, rng)
    medium = .65 * noise(size, 16, rng) + .35 * noise(size, 32, rng)
    fine = .6 * noise(size, 64, rng) + .4 * noise(size, 128, rng)
    micro = rng.uniform(-1, 1, (size, size))
    dirt = np.clip((broad + .18) * 1.1, 0, .75)
    value = .97 + .17 * medium + .045 * fine - .27 * dirt
    height = .2 * medium + .20 * fine + .06 * micro
    smooth = gloss + .08 * medium - .15 * dirt
    metallic = np.full((size, size), metal)
    tint = np.ones((size, size, 3))
    if recipe in ('stone', 'concrete', 'mortar', 'plaster', 'grime', 'rust', 'acoustic'):
        pits = flecks(size, rng, 2200 if recipe == 'acoustic' else 950, .0025)
        value -= pits * (.45 if recipe == 'acoustic' else .20)
        height -= pits * .4
        # Fractured mineral contours / worn plaster boundaries, all periodic.
        if recipe in ('stone', 'concrete'):
            cracks = np.exp(-np.abs(medium + .25 * broad) * 150) * np.clip(-broad * 2, 0, 1)
            value -= cracks * .25
            height -= cracks * .35
        if recipe == 'rust':
            value += .3 * fine
            tint[..., 1] *= .82 + .25 * medium
        if recipe == 'plaster':
            exposed = np.clip((medium + broad + .4 * fine - .50) * 4, 0, 1)
            height -= exposed * .3
            value -= exposed * .18
    elif recipe in ('wood', 'parquet'):
        # Integer-frequency growth rings with periodic wandering grain.
        if recipe == 'parquet':
            bx, by = np.floor(x * 4), np.floor(y * 4)
            alternate = (bx + by) % 2 == 0
            u, v = np.where(alternate, x, y), np.where(alternate, y, x)
            joint = (np.minimum((u * 16) % 1, 1 - (u * 16) % 1) < .025) | (np.minimum((v * 4) % 1, 1 - (v * 4) % 1) < .012)
            value += .12 * np.sin(bx * 2.1 + by * 4.2)
        else:
            u, v = x, y
            joint = np.minimum((u * 8) % 1, 1 - (u * 8) % 1) < .016
        grain = np.sin(2 * np.pi * (u * 42 + 1.8 * np.sin(v * 2 * np.pi) + 1.4 * medium))
        grain += .45 * np.sin(2 * np.pi * (u * 117 + .7 * np.sin(v * 4 * np.pi) + medium))
        value = np.where(joint, .42 + .05 * fine, value + .16 * grain)
        height = np.where(joint, -.6, height + .15 * grain)
        smooth = np.where(joint, .10, smooth + .06 * grain)
    elif recipe in ('tile', 'brick', 'vinyl', 'terrazzo'):
        rows = 8 if recipe == 'brick' else 4
        columns = 4
        row = np.floor(y * rows)
        u = (x * columns + (row % 2) * (.5 if recipe == 'brick' else 0)) % 1
        v = (y * rows) % 1
        distance = np.minimum(np.minimum(u, 1 - u), np.minimum(v, 1 - v))
        width = .045 if recipe == 'brick' else .020
        # Flat-bottomed grout rather than a sub-texel V groove: the latter
        # creates a sharp opposing-normal line at the repeat boundary.
        joint = np.clip((width - distance) / (width * .25), 0, 1)
        joint = joint * joint * (3 - 2 * joint)
        aggregate = flecks(size, rng, 3000 if recipe == 'terrazzo' else 1600, .0035)
        value += aggregate * (.7 if recipe == 'terrazzo' else .15) - joint * .43
        height -= joint * .65
        smooth -= joint * .3
        chips = np.clip((medium - .1) * 3, 0, 1) * (distance < .065)
        if recipe in ('brick', 'tile'):
            value -= chips * .25
            height -= chips * .4
        if recipe == 'brick':
            value += .25 * fine
            tint[..., 1] *= .88
    elif recipe in ('iron', 'steel', 'painted_metal', 'hazard'):
        rust = np.clip((medium + .6 * broad + .35 * fine - .28) * 3, 0, 1)
        if recipe == 'steel':
            rust *= .2
        # Grain stays periodic and never creates a border-specific stripe.
        brush = np.sin(2 * np.pi * (y * 190 + .1 * np.sin(x * 2 * np.pi)))
        value += .035 * brush - .2 * rust
        height += .07 * brush + .4 * rust
        smooth -= .30 * rust
        metallic *= 1 - .95 * rust
        tint[..., 0] += rust * .7
        tint[..., 1] *= 1 - rust * .24
        tint[..., 2] *= 1 - rust * .55
        if recipe == 'hazard':
            stripes = ((x + y) * 4) % 1 < .5
            value *= np.where(stripes, .12, 1)
    elif recipe == 'fabric':
        weave = np.sin(x * 2 * np.pi * 160) * np.sin(y * 2 * np.pi * 160)
        value += .08 * weave
        height += .18 * weave
    elif recipe == 'chalkboard':
        wiped = np.maximum(0, noise(size, 8, rng)) * (.7 + .3 * np.sin(y * 2 * np.pi * 16))
        value += .7 * wiped
        smooth -= .08 * wiped
    elif recipe in ('glass', 'water', 'light', 'rubber', 'paper'):
        value = 1 + .09 * medium - .16 * dirt
        height *= .15
        if recipe == 'light':
            value += .06 * np.cos(x * 2 * np.pi * 64)
        if recipe == 'water':
            height = np.sin(2 * np.pi * (x * 5 + .2 * np.sin(y * 4 * np.pi))) * .4
    base = linear([int(hex_color[i:i + 2], 16) / 255 for i in (1, 3, 5)])
    albedo = srgb(base * np.maximum(value[..., None], .08) * tint)
    normal = normal_from_height(height * relief)
    packed = np.stack((np.clip(metallic, 0, 1), np.zeros_like(x), np.zeros_like(x), np.clip(smooth, .02, .96)), axis=-1)
    return albedo, normal, packed


def bytes_image(values):
    return np.rint(np.clip(values, 0, 1) * 255).astype(np.uint8)


def lit_wall(albedo, normal, packed):
    """Offline GGX wall swatch, not a claim about Unity/game lighting."""
    size = albedo.shape[0]
    y, x = np.mgrid[0:size, 0:size] / size
    light = np.stack((.4 - x, .6 + y, np.full_like(x, .75)), axis=-1)
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
    result = color * .24 + ((1 - f) * (1 - metal) * color / np.pi + specular) * nl[..., None] * 2.4
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
    # Every map shown tiled 3x3; fourth column is a normal/smoothness-lit wall.
    thumb = 100
    panel = thumb * 3
    sheet = Image.new('RGB', (4 * (panel + 12) + 12, len(samples) * (panel + 40) + 60), '#181b20')
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), f'{theme} | {METRES:g} metres/tile | Albedo 3x3 / Normal 3x3 / Smoothness 3x3 / GGX lit wall', fill='white')
    for row, (slot, albedo, normal, packed) in enumerate(samples):
        py = 60 + row * (panel + 40)
        draw.text((12, py - 20), slot, fill='white')
        maps = (bytes_image(albedo), bytes_image(normal * .5 + .5), bytes_image(packed[..., 3]))
        for col, image in enumerate(maps):
            tile = Image.fromarray(image).convert('RGB').resize((thumb, thumb), Image.Resampling.LANCZOS)
            for iy in range(3):
                for ix in range(3):
                    sheet.paste(tile, (12 + col * (panel + 12) + ix * thumb, py + iy * thumb))
        wall = Image.fromarray(lit_wall(albedo, normal, packed)).resize((panel, panel), Image.Resampling.LANCZOS)
        sheet.paste(wall, (12 + 3 * (panel + 12), py))
    path = REVIEW / f'{theme}-contact.png'
    save_if_changed(path, png_bytes(np.asarray(sheet)))
    return str(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--size', type=int, choices=(512, 1024), default=512)
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
            samples.append((slot, albedo, normal, packed))
        report['contactSheets'].append(contact_sheet(theme, samples))
        print(f'GENERATED {theme}: {len(samples)} slots, {args.size}px, {METRES:g} metres/tile', flush=True)
    save_if_changed(REVIEW / 'generation.json', (json.dumps(report, indent=2) + '\n').encode())
    print(f'TEXTURE_RESULT maps={len(report["files"])} contactSheets={len(report["contactSheets"])}')


if __name__ == '__main__':
    main()
