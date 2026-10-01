# Generated theme textures

Project-made procedural surfaces; no downloaded images, fonts, packages or assets.
The editable sources are these Python recipes (not a `.blend`). Python 3.13,
NumPy 2.4.3 and Pillow 12.3.0 were the installed generation environment.

## Reproduce and verify

From this worktree:

    python tools/textures/generate_theme_textures.py
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

- Albedo: full kit-palette RGB in sRGB, not a multiplier texture.
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

- CLI `--size`: default 512, optional 1024; delivered maps are 512 square.
- `SEED`: 261001, combined with SHA-256 of the exact slot for independent streams.
- `METRES`: 2.0 m square per tile, copied to each JSON recipe.
- `PROFILES`: base smoothness, relief amplitude metres, bare-metal fraction below.
  Wear varies these spatially; smoothness is bounded to 0.02–0.96.

| Recipe | Smoothness | Relief (m) | Metallic |
|---|---:|---:|---:|
| stone | .12 | .008 | 0 |
| mortar | .08 | .002 | 0 |
| plaster | .17 | .0018 | 0 |
| concrete | .14 | .004 | 0 |
| wood | .23 | .002 | 0 |
| parquet | .35 | .002 | 0 |
| tile | .55 | .002 | 0 |
| vinyl | .32 | .0005 | 0 |
| terrazzo | .42 | .001 | 0 |
| brick | .10 | .009 | 0 |
| iron | .32 | .002 | .72 |
| steel | .52 | .0005 | .85 |
| painted_metal | .38 | .001 | .05 |
| rust | .08 | .003 | .03 |
| fabric | .12 | .0005 | 0 |
| rubber | .16 | .0004 | 0 |
| glass | .86 | .00008 | 0 |
| chalkboard | .12 | .0003 | 0 |
| grime | .06 | .001 | 0 |
| paper | .12 | .0002 | 0 |
| acoustic | .08 | .0015 | 0 |
| light | .55 | .00015 | 0 |
| water | .92 | .0003 | 0 |
| hazard | .30 | .001 | .05 |

Pattern decisions in `generate_surface`: 4x4 vinyl/ceramic/terrazzo tiles per
repeat (0.5 m); 4x8 running-bond brick (0.5 x 0.25 m); eight timber boards per
repeat (0.25 m); parquet 4x4 alternating squares with four strips per square;
4 diagonal hazard stripe cycles. Grout half-width is .020 of a cell (.045 for
brick), with a flat-bottomed 75% plateau to avoid sub-texel V-groove normal spikes.
Noise lattice frequencies, wear masks and grain coefficients are deterministic
art recipe constants in that function, not new runtime configuration knobs.

Offline preview controls in `lit_wall`/`contact_sheet`: 100 px per repeated tile,
300 px lit wall, diffuse ambient .24, key multiplier 2.4, roughness floor .06.
They are diagnostic studio lighting, not Unity or game-lighting evidence.

## Evidence and visual review

`Logs/AgentValidation/Art/Textures/` contains:

- `Castle-contact.png`: cold stone/mineral pitting, mortar, soot, worn timber,
  dark oxidised iron and ember-coloured surface.
- `Hospital-contact.png`: mint tile/chips, warm vinyl, flecked terrazzo, aged
  plaster, woven cloth and tarnished clinical metal.
- `School-contact.png`: mustard plaster, teal painted metal, wiped chalkboard,
  lino, alternating parquet grain, wood and support surfaces.
- `Basement-contact.png`: grey concrete/damp, brick, orange rust, oily dark steel,
  galvanised services, insulation and hazard stripes.
- `generation.json`: exact SHA-256 per PNG.
- `python-tests.log`: regeneration, normal packing/direction and seam checks.
- `kit-inputs.json` / `kit-inputs.log`: actual-FBX coverage/UV audit.

Each row shows 3x3 albedo, normal and smoothness repeats, then a CPU GGX-lit wall
using those maps. Vision review included the full sheets and full-resolution
crops. Refinement removed pinstripe-like wood, softened lattice-shaped wear and
replaced sharp V-groove grout normals. Final repeats showed no image-edge seams;
pattern repetition remains visible, as expected for provisional two-metre tiles.
Normals are subtle at sheet scale but resolve pitting/grain in the lit closeups.
Dark metal/soot are deliberately dark; game-lighting readability is unverified.

## Coordinator hand-off / known input blockers

The actual-FBX audit found no missing texture slots. Castle 45/45 and Hospital
35/35 pieces have SurfaceMetres. School 44/44 and Basement 45/45 lack it.
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
