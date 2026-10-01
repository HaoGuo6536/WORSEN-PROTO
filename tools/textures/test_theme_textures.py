# ============================================================================
# test_theme_textures.py
# PURPOSE:
#   Validate generated PNGs independently of Unity and of visual judgement.
#   Byte reproduction, physical normals and repeat-boundary checks catch stale
#   outputs or regressions; contact-sheet inspection is still required.
# ARCHITECTURAL ROLE: Offline art tests; outside Unity runtime layers.
# KEY RESPONSIBILITIES:
#   - Verify every declared material has reproducible complete PBR maps.
#   - Verify encoded normals, colour space metadata and packed channel ranges.
#   - Check wrap boundaries and periodic derivative invariance.
# DEPENDENCIES: unittest, NumPy, Pillow and the local texture generator.
# USAGE NOTES: python -m unittest discover -s tools/textures -p test_*.py -v
#   Read-only against generated Assets. Does not contact Unity or the network.
# ============================================================================
import hashlib
import json
import unittest

import numpy as np
from PIL import Image

import generate_theme_textures as g


class ThemeTextureTests(unittest.TestCase):
    def test_palette_and_manifest_inventory(self):
        palette = g.palettes()
        self.assertEqual({t: len(p) for t, p in palette.items()},
                         {'Castle': 7, 'Hospital': 14, 'School': 19, 'Basement': 11})
        for theme, surfaces in palette.items():
            expected = {f'{theme.lower()}_{s}_{kind}.png' for s in surfaces
                        for kind in ('Albedo', 'Normal', 'Smoothness')}
            self.assertEqual({p.name for p in (g.TEXTURES / theme).glob('*.png')}, expected)

    def test_byte_reproduction_and_metadata(self):
        report = json.loads((g.REVIEW / 'generation.json').read_text())
        checked = 0
        for theme, surfaces in g.palettes().items():
            for surface, palette in surfaces.items():
                with self.subTest(theme=theme, surface=surface):
                    slot = f'{theme.lower()}_{surface}'
                    metadata = json.loads((g.TEXTURES / theme / f'{slot}.json').read_text())
                    self.assertEqual(metadata['metresPerTile'], 2)
                    self.assertEqual(metadata['paletteSrgb'], palette)
                    self.assertEqual(metadata['normalConvention'], 'OpenGL +Y')
                    albedo, normal, packed = g.generate_surface(theme, surface, palette, metadata['resolution'])
                    maps = (albedo, normal * .5 + .5, packed)
                    for suffix, image in zip(('Albedo', 'Normal', 'Smoothness'), maps):
                        path = g.TEXTURES / theme / f'{slot}_{suffix}.png'
                        content = path.read_bytes()
                        self.assertEqual(content, g.png_bytes(g.bytes_image(image)), path.name)
                        self.assertEqual(hashlib.sha256(content).hexdigest(), report['files'][path.relative_to(g.ROOT).as_posix()])
                        checked += 1
        self.assertEqual(checked, 153)

    def test_normal_direction_and_wrapped_derivative(self):
        y, x = (np.mgrid[0:512, 0:512] + .5) / 512
        height = .004 * (np.sin(2 * np.pi * x) + np.cos(2 * np.pi * y))
        normal = g.normal_from_height(height)
        self.assertLess(normal[0, 0, 0], 0)  # Rising right => points left.
        self.assertLess(normal[128, 0, 1], 0)  # Falling down => tangent -V.
        rolled = np.roll(height, (37, 61), axis=(0, 1))
        np.testing.assert_allclose(g.normal_from_height(rolled), np.roll(normal, (37, 61), axis=(0, 1)))
        tiled = np.tile(height, (3, 3))
        # Preserve physical texel size when the evaluated patch is three tiles.
        np.testing.assert_allclose(g.normal_from_height(tiled, 6)[512:1024, 512:1024], normal)

    def test_normal_unit_length_and_channel_packing(self):
        for path in g.TEXTURES.glob('*/*_Normal.png'):
            with self.subTest(path=path.name):
                normal = np.asarray(Image.open(path), dtype=float) / 127.5 - 1
                self.assertLess(np.max(np.abs(np.linalg.norm(normal, axis=-1) - 1)), .008)
                self.assertGreater(normal[..., 2].min(), 0)
                self.assertGreater(normal[..., :2].std(), .001)
        for path in g.TEXTURES.glob('*/*_Smoothness.png'):
            with self.subTest(path=path.name):
                image = Image.open(path)
                self.assertEqual(image.mode, 'RGBA')
                data = np.asarray(image)
                self.assertEqual(int(data[..., 1:3].max()), 0)
                self.assertGreaterEqual(int(data[..., 3].min()), 5)
                self.assertLessEqual(int(data[..., 3].max()), 245)
                self.assertGreater(float(data[..., 3].std()), 0)

    def test_repeat_boundary_not_an_outlier(self):
        # Statistical alarm, not proof by equal edge texels: true texel-centred
        # textures have distinct border samples. Compare wrap jumps to the 99th
        # percentile of interior line jumps (including real grout/plank joints).
        for path in g.TEXTURES.glob('*/*.png'):
            data = np.asarray(Image.open(path), dtype=float)
            for axis in (0, 1):
                with self.subTest(path=path.name, axis=axis):
                    wrap = np.abs(np.take(data, 0, axis) - np.take(data, -1, axis)).mean()
                    interior = np.abs(np.diff(data, axis=axis)).mean(axis=tuple(i for i in range(3) if i != axis))
                    limit = 1.5 * max(float(np.percentile(interior, 99)), .5)
                    self.assertLessEqual(wrap, limit)

    def test_same_seed_and_slot_are_deterministic(self):
        a = g.generate_surface('Basement', 'concrete', '#5e6061', 512)
        b = g.generate_surface('Basement', 'concrete', '#5e6061', 512)
        for left, right in zip(a, b):
            np.testing.assert_array_equal(left, right)
        for path in g.REVIEW.glob('*-contact.png'):
            with Image.open(path) as image:
                self.assertEqual(image.width, 1260)
                self.assertGreater(image.height, 1000)


if __name__ == '__main__':
    unittest.main()
