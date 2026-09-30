# Castle kit — original Blender art

Generated with Blender 5.2.1 LTS by `tools/blender/env_kit_castlehospital.py`.
No downloaded meshes, textures, add-ons or external art inputs. This source
family and the Hospital family implement the owner's 2026-09-30 PLAN-026 kit
contract; they do not implement the procedural generator's runtime fallback.

## Rebuild and check

From this worktree root, using Git Bash:

```sh
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/env_kit_castlehospital.py -- --theme all
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/validate_env_kit_castlehospital.py -- --theme all
```

`--theme castle` or `--theme hospital` limits either tool to one family. Generator
`--skip-previews` avoids rendering (the saved source then contains only original
pieces at the origin). Default generation also saves the assembled review room,
with exact export meshes hidden at the origin. Unhide the named `Castle_<id>`
object to edit an individual piece; `Preview_*` objects are shared-mesh review
instances, never FBX exports.

For a fresh repeat test: validate with `--record-baseline`, regenerate, then
validate with `--compare-baseline`. Evidence is in
`Logs/AgentValidation/Art/EnvCastle/` and `EnvHospital/`. The validator refuses to
overwrite an existing `determinism-baseline.json`; preserve or move that evidence
before starting a new comparison. A normal validation needs no baseline.
FBX creation timestamps and Blender metadata are not byte-deterministic. The
manifest, five-decimal triangle/material digest and PNG textures are compared.

## Placement and integration contract

- Source: `ArtSource/Environment/Castle/Kit/CastleKit.blend`.
- Runtime: `Assets/Art/Environment/Castle/Kit/Castle_<id>.fbx`,
  `CastleKit.manifest.json`, `castle_stone_wear.png`.
- Blender Z-up, +Y interior becomes exported Y-up, -Z interior. FBX global
  scale 1, metre units, -Z forward/Y up, baked space transform, identity model
  transforms, no lights, cameras, animation, collision or review geometry.
- Unity importer: `bakeAxisConversion = false`. Coordinator generates `.meta`
  files and material remapping; none were hand-authored here.
- Straight wall plane is Z=0 in exported coordinates; backing reaches Z=+.25,
  maximum relief reaches Z=-.25. Exact end vertices lie on the structural core;
  recessed joints, not mismatched wall bounds, separate the relief panels.
- Arc pivot is bottom-centre of the mid-span wall plane, NOT circle centre or
  bounding-box centre. Circle centre is local `(0, 0, -radius)` in exported
  coordinates. End angles are +/-15, +/-10 and +/-7.5 degrees. Radius refers to
  wall mid-plane, so the outer envelope is wider than the mid-plane chord.
  Rotate/translate about that circle centre to join arc segments; do not place
  arcs on a straight 2m interval. Their manifest sizes are measured envelopes.
- Corner pieces are 0.5m L caps with 0.25m legs. `corner_in` occupies the negative
  Blender X/Y legs; `corner_out` is its 180-degree complement. Their pivot is
  the L junction at the footprint centre. Align to wall planes, not outer edges.
- Floors sit above their bottom pivot: place at walking-surface Y minus .16m.
  Ceiling underside is pivot Y; place at the wall height. Flat tiles do not
  supply a curved floor filler; the preview deliberately leaves that strip open.
- Door clear aperture is exactly 3.2m x 2.8m. Windows are empty openings (no glass
  or bars) so the movement owner may preserve traversal. Window dimensions below
  are provisional, not an additional shared gameplay contract.
- Sconce and banner use bottom-centred bounding-box pivots, like all props; their
  wall mounting surface is not the pivot. Only the sconce's authored ember mesh
  emits in Blender. Runtime lights/audio/interaction remain the owning systems.

## Manifest and materials

The required `theme`, `wallHeight`, `pieces`, `id`, `file`, `size`, `kind` fields
are unchanged. Extra per-piece fields `triangles`, `materials`, `geometrySha256`
are offline validation evidence. Consumers may ignore these extra fields.
`size` order is X/Y/Z, metres. Digest input is sorted triangles of rounded
export-space positions with material names, compact JSON, SHA-256; signed zero
is normalized. Independent normal checks guard against inverted faces.

Slots: `castle_stone`, `castle_stone_dark`, `castle_mortar`, `castle_wood`,
`castle_metal`, `castle_cloth`, `castle_ember` (each piece uses only its subset).
Map `castle_stone_wear.png` to stone base color, with white base tint to avoid
multiplying the baked color twice. Other slots use the linear palette in
`PALETTES`; do not assume the preview's lighting is embedded in the mesh.
The 512px image is packed in the source and has a relative external path.

## Provisional authored values

These are offline art/preview constants in the generator, not new runtime
config or C# gameplay tunables. Fixed module width, theme heights, door aperture,
arc radii/angles and triangle limits come from the owner contract.

- `SEED`: 260930; separate `random.Random` per theme/piece/texture. No wall clock
  or global random state. `TEXTURE_SIZE`: 512; one image Castle, two Hospital.
- `PALETTES['castle']` linear RGB: stone (.31,.285,.235), dark stone (.22,.225,.20),
  mortar (.13,.14,.13), wood (.19,.095,.038), metal (.105,.115,.12),
  cloth (.24,.035,.025), ember (.95,.26,.025). Roughness .86; metal metallic .65;
  ember emission strength 2. The surface shader values live in `make_materials`.
- `dressed_wall`: core .44m deep, centred Blender Y=-.03; relief .19 to .25m;
  stone courses/columns about 1m, bond shift .07 of wall width, joints .028m;
  inset bevel .025m, inset jitter +/-20% bevel, relief variation .228-.25m,
  dark-block probability .2. Hospital values are in its sibling README.
- `arc_wall`: .5m core thickness, four chord facets, five Castle/four Hospital
  relief rows, .03m panel gaps, .025m relief, .014m bevel. Mid-plane radius stays
  exact at facet endpoints; the straight facet chord lies inside the ideal arc.
- `architecture`: window 1.3m wide, sill 1.1m, lintel 2.5m; corner footprint .5m
  with .25m legs; Castle pillar .8m widest diameter, eight sides, .29m shaft
  radius; floor .16m thick, ceiling .18m; inset tile depth .028m; Castle 3x3 tiles,
  Hospital 4x4, .024m joints; trim 2 x .18 x .12m.
- `castle_prop` measured exported X/Y/Z bounds (m): sconce
  (.24,1.045,.464127), banner (1.1,2.283287,.146665), barrel (.914,1.13,.914),
  rubble (1.567320,.292082,.946659). All internal component dimensions are authored
  geometry in that function: folded cloth 8x5 panels; barrel 12 tapered staves;
  rubble nine seeded beveled fragments. These are not gameplay collision sizes.
- `preview_room`: cutaway partial floor/ceiling, 1200x900, Cycles 32 samples,
  deterministic seed, denoising; ambient strength .30, lights 1800/350/800 W,
  area sizes 7/3/5m; orthographic span 12.8m Castle / 11.6m Hospital. Camera,
  instance positions and lighting colors are literal review-only composition.
- Validator tolerance .01m for dimensions and .00001m for seams; digest five
  decimal places. Prop baseline sizes are separately recorded in `expected_size`.

## Requests to coordinator (shared files intentionally untouched)

`ArtSource/README.md`, Inventory: add

| [Environment/Castle/Kit/CastleKit.blend](Environment/Castle/Kit/CastleKit.blend) | `Assets/Art/Environment/Castle/Kit/`: 16 piece FBXs, `CastleKit.manifest.json`, `castle_stone_wear.png` | Original deterministic Blender 5.2 art; `tools/blender/env_kit_castlehospital.py --theme castle`; independently validated by `validate_env_kit_castlehospital.py`; details in the source folder README. |

`tools/blender/README.md`: add a Castle/Hospital environment-kit section linking
this README and the Hospital README, with the rebuild/validate commands above,
the `--theme` selector, and preview locations. The coordinator must also connect
the generator worker's kit selection and missing-piece/whole-kit primitive
fallback. That C# path is deliberately outside this art worker's scope.

## Verification limits

The validator measures all exported pieces, raw FBX axes/transforms, normals,
triangle/material/UV consistency, exact seams (including rotated arcs), doorway
clearance, source agreement, packed textures and preview image content. It is not
a Unity importer, collider, navigation, lighting or artistic-approval test.
Coordinator: inspect the two views, material remapping and joins under gameplay
lighting/fog, run the Unity tests and verify partial/absent kit fallbacks. No
Unity operation or shared-index refresh is performed by these offline tools.
