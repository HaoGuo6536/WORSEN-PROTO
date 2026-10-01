# Generated theme textures

Project-made procedural surfaces; no downloaded images, fonts, packages or assets.
The editable sources are these Python recipes (not a `.blend`). Python 3.13,
NumPy 2.4.3 and Pillow 12.3.0 were the installed generation environment.

## Reproduce and verify

From this worktree:

    python tools/textures/generate_theme_textures.py
    python tools/textures/render_distance_review.py
    python -m unittest discover -s tools/textures -p 'test_*.py' -v

Generation is deterministic per seed and exact slot name; unchanged output bytes
are not rewritten. The script reads literal `PALETTE` dictionaries from the
current `tools/blender/env_theme_*.py` generators without importing or executing
them. It verifies the slots listed by kit manifests, refuses unknown palette
changes, and never edits another owner's generators, exports or manifests.

Optional actual-FBX input audit (read-only, Blender 5.2, never Unity):

    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/textures/inspect_kit_inputs.py

The audit writes `Logs/AgentValidation/Art/Textures/kit-inputs.json` and reports
actual exported material names and UV layers. No model/source file is saved.

## Inventory and encoding

There are 51 sets (153 PNGs and 51 JSON recipes): Castle 7, Hospital 14, School 19,
Basement 11. Every exported slot is covered. Three additional owner-requested
surfaces are ready but not used by existing geometry: `hospital_terrazzo`,
`school_parquet`, `basement_brick`. Existing hospital vinyl, school lino and
basement concrete have not silently been relabelled as those other surfaces.

Output: `Assets/Art/Textures/<Theme>/<slot>_{Albedo,Normal,Smoothness}.png`.
Each `<slot>.json` carries metres-per-tile, palette, seed and encoding details.

- Albedo: full surface RGB in sRGB, not a multiplier texture. The source palette
  remains the adoption identity; local pigments include ivory ceramic glaze,
  orange corrosion, exposed plaster and multicolour terrazzo aggregate.
- Normal: tangent-space RGB, linear, OpenGL/+Y. Height gradients wrap; image rows
  run down and tangent V runs up. No green flip or height-to-normal conversion.
- Smoothness: RGBA data texture, R=metallic, A=smoothness, G=B=0. This is packed
  specifically for URP Lit `_MetallicGlossMap`, not a standalone grayscale map.
- Repeat U/V and mipmaps on. Data sRGB off. Alpha retained, not transparency.
- SurfaceMetres UV0 means one UV unit per metre. Material scale is
  `1 / metresPerTile`, hence `(0.5, 0.5)` for the current two-metre repeat.

## Material adoption and edit preservation

`ProceduralKitAssetSetup.Build` calls `ApplySlotTextures` once per theme-local
material under `Assets/Resources/ProceduralKits/<Theme>/Materials/<slot>.mat`.
The complete set and metadata must exist before any importer/material changes.
Missing files log a warning and leave the original flat colour/material intact.

Only untouched URP flat slots are adopted: matching source palette (sRGB or
linear import representation), original 0.15 smoothness, no custom maps or UV
transform, and default metallic/normal/workflow controls. Initial adoption uses
white tint because the PNG already contains the palette; it retains unrelated
emission, culling and other properties. Custom pre-existing maps/tint/scale are
preserved with a warning rather than overwritten. The `WorsenGeneratedTextures`
material tag records adoption. Subsequent runs configure generated importers but
never reset material maps, tint, UVs, settings, or deliberate map removals. New
texture pixels at the same paths update normally. A metres-per-tile metadata
change requires explicit artist review of already-adopted material tiling.
No shader changes, transparency or emission authoring is included.

## Provisional art values

`generate_theme_textures.py` owns these values:

- CLI `--size`: default 1024, optional 512 for experiments; delivered maps and
  the NUnit inventory fixture require 1024 square.
- `SEED`: 261001, combined with SHA-256 of the exact slot for independent streams.
- `METRES`: 2.0 m square per tile, copied to each JSON recipe.
- `PROFILES`: base smoothness, relief amplitude metres, bare-metal fraction below.
  Wear varies these spatially; smoothness is bounded to 0.02–0.96.

| Recipe | Smoothness | Relief (m) | Metallic |
|---|---:|---:|---:|
| stone | .12 | .030 | 0 |
| mortar | .08 | .004 | 0 |
| plaster | .17 | .003 | 0 |
| concrete | .14 | .008 | 0 |
| wood | .23 | .006 | 0 |
| parquet | .35 | .003 | 0 |
| tile | .55 | .004 | 0 |
| vinyl | .32 | .001 | 0 |
| terrazzo | .42 | .0012 | 0 |
| brick | .10 | .018 | 0 |
| iron | .32 | .005 | .72 |
| steel | .52 | .001 | .85 |
| painted_metal | .38 | .002 | .05 |
| rust | .08 | .006 | .03 |
| fabric | .12 | .007 | 0 |
| rubber | .16 | .0005 | 0 |
| glass | .86 | .00008 | 0 |
| chalkboard | .12 | .0003 | 0 |
| grime | .06 | .001 | 0 |
| paper | .12 | .0002 | 0 |
| acoustic | .08 | .0015 | 0 |
| light | .55 | .00015 | 0 |
| water | .92 | .0003 | 0 |
| hazard | .30 | .001 | .05 |

The table is construction amplitude, not a bound on the total composite height.
`material_fields.py` owns the following provisional dimensional recipe choices:

- `masonry` / `recipe_fields`: ashlar 3 columns x 6 courses, brick 5 x 10;
  joint half-width .005/.003 UV, bevel .007 UV plus up to .003 irregular chips;
  ashlar edge damage subtracts up to .009 m. Mortar stays recessed.
- `timber`: six staggered boards, two lengths per repeat, four nail recesses
  per board; 8 coarse grain cycles across each board, pore multiplier 3;
  herringbone is a rotated interlocking 4:1 tessellation, grid frequency 8,
  board IDs periodic modulo 8. Grain amplitude .20 for timber / .07 parquet,
  grain colour contrast .34/.18. Nail depth .6 of the wood construction relief.
- `recipe_fields` wear: 34 vertical marks, widths .003-.014 UV and lengths
  .055-.23 UV; periodic rising-damp band centred at row .85, width .34 in
  sine coordinates. This repeats vertically; it is not a world-space floor mask.
- Ceramic/vinyl/terrazzo: 4 x 4 cells; ceramic recess .004 m, chips .0025 m;
  glaze is 85% linear ivory (.72,.70,.62) + 15% source palette. Terrazzo has
  2400 angular grains of radius .005 UV, flush aggregate relief .00016 m.
  Vinyl has 100 scuffs (.001-.004 by .012-.065 UV), depth .00035 m.
- Plaster: .0018 m paint skin, .0025 m lifted edge; peel threshold .23-.35;
  School dado transitions at row .48-.49 and .965-.985, narrow line at
  .474-.492, depth .0007 m. It requires upright, consistently phased wall UVs.
- Concrete: eight formwork courses, 4 x 4 tie-hole lattice; board joints
  .005 m, tie holes .006 m, broken grain .0006 m at frequency 80.
- Metal: 16 dents (.012-.045 by .018-.075 UV), depth .014 m; 70 scratches
  (.0008-.002 by .013-.05 UV), depth .0007 m. Painted-metal corrosion threshold
  .18-.43 limits rust to damaged patches. Bare iron uses -.06-.27.
- Cloth: ten fold cycles, .007 m amplitude; 180-cycle weave, .00045 m.
  Chalkboard: 52 erased sweeps plus 34 fine strokes, relief .00025 m.
- Finish: wet masonry adds up to .52 smoothness, wet concrete .50, wet plaster
  .35; dry plaster .13, exposed plaster .06, ceramic glaze .68 minus chip wear.
  Varnish wears down by .27, vinyl scuffs by .30, rust by .32. All clamp to
  .02-.96; rust removes 97% of base metallic response. No runtime tunables added.

Other inline colour/noise coefficients are deterministic recipe constants,
editable in `material_fields.py`, not engine settings.

`lit_wall` / `contact_sheet`: 100 px per repeated tile, 300 px wall; ambient .035,
key 1.1, warm linear RGB (1,.57,.28), cold (.38,.62,1), roughness floor .06.
`render_distance_review.py`: VIEWPORT=384, FOV=60 degrees, DISTANCES=(2,4) m;
2 m front-facing wall projects to 333/166 px. Data are BOX-filtered in linear
space before shading and normals renormalized. The GGX view vector remains a
parallel approximation; these are dark CPU diagnostics, not Unity captures.

## Evidence and visual review

`tools/textures/review/` contains the pass-2 evidence (inside owned scope):

- `<Theme>-contact.png`: all slots; three 3x3 map repeats and warm/cold walls.
- `materials/<slot>.png`: readable individual contact-sheet rows.
- `distance/<slot>.png`: warm/cold at 2 m, then warm/cold at 4 m, fixed exposure.
- `distance-metrics.json`: filtered normal relief at both screen footprints.
- `VISUAL-REVIEW.md`: observations per material and known visual limitations.
- `generation.json`: exact SHA-256 per PNG.
- `python-tests-final.log`: reproduction, packing, seams, periodic board colour,
  normal relief after minification, and preview checks (ignored local log).

Vision review includes full sheets, lit-column crops and 2/4 m renders. Pass 2
fixed a genuine parquet board-colour wrap discontinuity, reduced uniform rust
coverage on paint, separated coarse timber grain from varnished parquet, and
reduced concrete's initially corrugated appearance. Tests retain the original
seam limits and explicitly reject the old near-flat structural normals.
Repeat motifs remain visible; owner acceptance and actual Unity room lighting
remain unverified. Glass/water still need the existing shader/reflection context.

## Coordinator hand-off / known input blockers

The pass-1 actual-FBX audit found no missing texture slots. Castle 45/45 and Hospital
35/35 pieces have SurfaceMetres. School 44/44 and Basement 45/45 lack it.
These are historical input findings, not a new audit of the parallel UV fixes.
Required owner changes, NOT made here:

1. `tools/blender/env_theme_school.py`, `SchoolMesh.finish`: create active UV0
   `SurfaceMetres`, box-project final metre-scale vertices (dominant face axis),
   then regenerate School FBXs under `Assets/Art/Environment/School/Kit/`.
2. `tools/blender/env_theme_basement.py`, `BasementMesh.finish`: same UV0 fix and
   regenerate Basement kit FBXs. Until then assigning maps cannot texture rooms.
3. To use the three additional surfaces: owning `materials`/`architecture`
   functions in `env_theme_hospital.py`, `env_theme_school.py`, and
   `env_theme_basement.py` must add and assign `hospital_terrazzo`,
   `school_parquet`, and `basement_brick` respectively to appropriate floor/wall
   faces, then regenerate affected exports/manifests. No slot substitution here.

Coordinator Unity checks after integration/import:

- Run `ProceduralContentSetup.Configure` via
  `Worsen/Procedural/Wire wave 3c content to selected config` twice with the
  existing ProceduralConfig selected. Check warning-free adoption, material
  GUIDs/references stable, maps/keywords/scale correct and artist edits preserved.
- Run `Worsen.Tests.Art.ProceduralKitTextureNativeTests` (NativeUnity category),
  `Worsen.Tests.Procedural.ProceduralKitAssetSetupTests`, and
  `Worsen.Tests.Editor.RebuildAllSetupTests`. The native cases were not run here.
  Palette-adoption/import idempotency cases are the most sensitive to Unity's
  actual FBX colour representation and URP defaults.
- Capture one generated HorrorRun room per theme under game lighting:
  `Castle-game-room.png`, `Hospital-game-room.png`, `School-game-room.png`,
  `Basement-game-room.png`. Inspect UV density/orientation, import compression,
  normal polarity, reflections, readability and joins between separate pieces.
- Let Unity create all `.meta` files. No Unity process, lease, Synaptic call,
  source/FBX edits, packages, commits or shared Library writes occurred here.
