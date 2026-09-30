# Hospital kit — original Blender art

Source: `ArtSource/Environment/Hospital/Kit/HospitalKit.blend`.
Exports: `Assets/Art/Environment/Hospital/Kit/Hospital_<id>.fbx` (16 pieces),
`HospitalKit.manifest.json`, `hospital_plaster_wear.png`, `hospital_tile_wear.png`.

See [the Castle kit handoff](../../Castle/Kit/README.md) for commands, axes,
manifest extras, arc placement, determinism, ownership and verification limits.
Both kits use the same generator/independent validator with `--theme hospital`
or `--theme all`. Hospital previews: `Logs/AgentValidation/Art/EnvHospital/`.

## Hospital appearance and provisional authored values

These are offline asset values in `tools/blender/env_kit_castlehospital.py`, not
new runtime tunables. Wall height 3.6m and common module/door/arc dimensions are
fixed by the owner contract.

- `PALETTES['hospital']` linear RGB: plaster (.49,.50,.43), tile (.27,.40,.37),
  stained tile (.20,.29,.25), metal (.22,.26,.25), rubber (.025,.032,.031),
  fabric (.42,.49,.43), glass (.105,.19,.18). Tile/glass roughness .48;
  other surfaces .86; metal metallic .65. Glass is opaque dark glazing,
  not transparency-dependent rendering.
- Material slots use the `hospital_` prefix above. Base-color images for plaster
  and tile are baked colored 512px wear maps: map with white base tint, not the
  palette multiplied again. Texture nodes, UVs, relative paths and packed copies
  are in the source. No external image downloads or Blender-only noise nodes.
- `dressed_wall`: tile dado top 1.45m, approximately .5m columns/.48m rows,
  .016m tile joints, .008m tile bevels, .22 stained-tile probability; upper plaster
  relief bevel .012m. Lower wall, window, doorway and arc surfaces are geometric.
- `architecture`: Hospital pillar .6 x 3.6 x .6m overall, .5m shaft, .028m shaft
  bevel; .24m tiled base and .16m metal capital. Floors and ceilings have 4x4
  relief panels; core/tile thicknesses and seam treatment match the shared handoff.
- `hospital_prop` measured exported X/Y/Z bounds (m): bed (1,1.08,2.14), curtain
  rail (2,2.495568,.116), cabinet (.86,1.9,.534), wheelchair (.728,1.04,.95).
  The cabinet fronts, bed mattress/pillow and chair cushions have real bevels.
  Bed has four caster rings; wheelchair has two large spoked wheels, push rims,
  front casters and footrests. Curtain uses twelve alternating folds bunched to
  one side, leaving an open passage. Components are rigid meshes, not rigs.
- The rail bottom pivot is the curtain's lowest point, not a support on the floor:
  raise the whole prop roughly .30m for the preview's hanging height as needed.
  Every prop's measured bounding-box bottom is Y=0; no invisible locator extends
  its bounds. Mounting/clearance placement remains a generator-owner decision.
- `preview_room`: same render settings as Castle, with cool key light
  (.72,.87,1), green door bounce (.6,.9,.77), orthographic span 11.6m. This is a
  partial-floor cutaway, not a ready-to-play collider or navigation scene.

## Requests to coordinator (shared files intentionally untouched)

`ArtSource/README.md`, Inventory: add

| [Environment/Hospital/Kit/HospitalKit.blend](Environment/Hospital/Kit/HospitalKit.blend) | `Assets/Art/Environment/Hospital/Kit/`: 16 piece FBXs, `HospitalKit.manifest.json`, two packed-source wear PNGs | Original deterministic Blender 5.2 art; `tools/blender/env_kit_castlehospital.py --theme hospital`; independently validated by `validate_env_kit_castlehospital.py`; details in the source folder README. |

`tools/blender/README.md`: include this family in the requested joint section.
Unity setup/material remapping, `.meta` generation, gameplay prop placement and
primitive fallback tests belong to the coordinator/generator worker. Do not
silently treat a present but unbound mesh as verified runtime art.
