# Castle Gothic keep — PLAN-026 art hand-off

Original authored art; no downloaded assets, packages, Unity operations or v1 script imports. The new generator is `tools/blender/env_theme_castle.py`; the independent re-import validator is `tools/blender/validate_env_theme_castle.py`. Both resolve paths from their own worktree. Do not run the v1 Castle/Hospital generator to regenerate these v2 Castle exports.

## Sources and review

- `Kit/CastleKit.blend`: origin-centred export meshes and a labelled review sheet. Three packed 512px textures also live beside this source and are copied beside the FBXs.
- `Rooms/CastleRooms.blend`: one scene per manifest template. Every piece, transform and mesh is checked against the two manifests. Review-only visibility cuts hide south/east walls and most ceiling webs, retaining ribs. Unhide those instances to inspect the complete room.
- `Review_Darkness_CastleChapel` preserves the fully enclosed, full-ceiling darkness scene with zero world illumination, amber torches/candles and one flashlight spot. Studio lighting is disabled there.
- `Logs/AgentValidation/Art/EnvCastle/`: kit sheet, thirteen room renders, `in-darkness.png`, validator reports and immutable determinism baselines. Preview checks establish nonblank content, not artistic acceptance.

## Regenerate and validate

From this worktree, run Blender 5.2 with `--background --factory-startup --python-exit-code 1 --python tools/blender/env_theme_castle.py`. The optional script argument `-- --skip-previews` skips PNG rendering, not either editable source or the manifests.

Validate with the same Blender flags and `--python tools/blender/validate_env_theme_castle.py`. For determinism, append `-- --record-baseline <new-name>`, regenerate, then validate with `-- --compare-baseline <same-name>`. Baseline names must be unique; existing baselines are never overwritten. FBX timestamps and Blender session metadata are not required to be byte-identical; both manifest, canonical imported geometry and texture hashes are compared.

## Placement semantics and current limits

- Every structural wall is 7m high and 0.8m thick. Its inner face, not its centre plane, follows the occupied 2m cell boundary: the wall pivot is offset 0.4m outward. Floor tops are at Y=0, using tile pivot Y=-0.16m. Do not re-centre meshes from their bounds during import.
- Pointed door crowns rise above the mandatory unobstructed 3.2m by 2.8m rectangle. Contract windows retain the clear 1.3m by 1.4m rectangle, with a pointed crown above it; the additional arrow slits are narrow non-traversal openings.
- A 4m portal centred on a single 2m boundary edge consumes that edge and half each adjacent edge. `wall_end_1m` closes the remaining portions. The socket's `closedWith` replaces the entire `wall_door_4m`, not just its gap. Re-entrant corners use `wall_concave_1p2m` so wall solids do not overlap.
- Hallways remain two cells wide. Their clear sockets are lateral portals in opposite end regions, not centred on the short end caps: a 3.2m opening centred on either cell edge of a 4m end cap otherwise crosses a side wall. An offset-within-edge socket convention is needed for centred short-end portals.
- No circular spawnable footprint is claimed. Exact square-cell boundary enclosure and a circular envelope require a shared raster/boundary convention. The complete r4/r6/r8 arc kit and `tower_wall_arc_r4` are delivered; the required medium non-rectangular catalogue slot is filled by the L-shaped buttery. Round tower and chapel-apse templates remain a coordinator/owner decision, not a passing gate.
- The collapsed crossing has three missing floor tiles and three genuinely broken edge tiles, not preview-only hidden geometry. A perimeter route remains. The portcullis is authored raised; its movement and freeze behaviour require runtime integration.
- Existing runtime code in this checkout does not consume these manifests: `ProceduralDriver.Build` still uses `CreateBlock` primitive geometry. No runtime fallback implementation or Unity verification is claimed by this art task.

## Provisional authoring values

Owner-fixed values are the palette, 7m wall height, 0.8m thickness, 2m module and 3.2m by 2.8m door clearance. Remaining values are authored defaults in the generator, not new Unity configuration fields:

- Seed 260930; three 512px textures; masonry seven irregular courses, two main blocks/course; stone relief up to 0.11m; material roughness 0.92 (iron 0.72), iron metallic 0.72; emission strength 8 for light-source tips only; bump strength 0.28, distance 0.055m (wood 0.015m).
- Door lancet rise 1.65m; contract-window rise 1m; arrow-slit width 0.28m, sill 2m, spring 3.6m, rise 0.9m. Iron door leaf 3.1m wide and 2.74m high. Stairs: five 0.2m rises and 0.4m treads. Prop dimensions are authored directly in `prop`, not intended as gameplay tuning.
- Ceiling seating convention: the measured **top** of every Castle ceiling piece is at the 7m wall datum (tolerance 0.05m). Flat cap bottom is 6.82m; beam bottom is 6.62m; vault rib/web origins are derived from their measured heights so their crowns meet 7m. The former 4.2m lids in 7m shells are removed. Vaults remain 4m square with a 2m rise; this is not a change to the kit's wall height.
- Template weight 1; ordinary minimum round 1, gimmicks 3. Cake sockets `max(2, ceil(cells/6))`; one golden and one hunter socket in medium or larger rooms. Torch anchors 3.13m high and 0.65m inside the boundary; each room's third selected fixture is dead when present.
- Review only: Cycles 24 samples with denoising; 1200x900 room images, 2000x1500 sheet; torch point lights 120W, flashlight 650W/52deg with 0.45 blend, perspective lens 22mm. Room studio fill 2200W, sheet key 18000W, world strength 0.20 (zero in darkness). Light powers are not Unity intensity defaults.

## Requests to the coordinator

### Art-fixes door/ceiling regression slice

The iron-strapped leaf has a continuous wood backing behind shallow plank relief, straps/rivets, kick plates and ring-handle plates on both faces. Guard-room and armoury leaves now occupy their first door frame rather than standing loose in the room. The independent validator rasterizes the body at 2.5mm, rejects 5/10/80mm gap controls and missing face hardware, checks measured ceiling seating and orphan leaves, and verifies geometry-bound front/back PNG receipts under `Logs/AgentValidation/Art/EnvCastle/doors/`.

This is partial PLAN-026 acceptance, not completion of the art-fixes task. Round templates and centred hallway caps still need the shared-contract decisions below. All four validators now enforce full transformed prop-envelope containment, including footprint holes, and conservative prop-envelope versus actual wall-triangle/interior clearance. They reject outside, hole-crossing and wall-embedded controls. Envelopes can reject valid hollow-object interlocks; no such unresolved overlap is accepted in the delivered catalogue. Torches are inset by measured half-depth plus 1mm, eliminating their previous wall penetration. Support of every non-ceiling placement remains unresolved: ceiling-hung fixtures, tabletop objects and subfloor liners need explicit support semantics. Passing the current validator does not claim that missing gate. Shared review/check functions reside in the owned Castle scripts and are called by the other three themes; no Castle shell or geometry is reused by them.

Provisional review/check values: door raster 2.5mm, geometric contact epsilon 0.01mm, ceiling/leaf tolerance 50mm; review PNGs 1200 square, 24 samples, seed 260930, orthographic extent multiplier 1.2, white area lights 700/400W and world strength .5 (`render_door_reviews`). Door body dimensions remain 3.1 x 2.74m; backing depth .14m, plank relief .15m, strap levels .4/1.5/2.4m, strap thickness .04m (`prop`). These are authored art/review values, not runtime tuning.

1. `ArtSource/README.md`, inventory: add `Environment/Castle/Kit/CastleKit.blend` -> `Assets/Art/Environment/Castle/Kit/` and `Environment/Castle/Rooms/CastleRooms.blend` -> `Assets/Art/Environment/Castle/Rooms/CastleRooms.manifest.json`; provenance: `tools/blender/env_theme_castle.py`, validated by `validate_env_theme_castle.py`; three original packed 512px textures, no per-room FBX.
2. `tools/blender/README.md`, Castle section: add the generation/validation commands above, preview/source paths, and the v2 overrides (0.8m walls, pointed crowns, preserved clear openings). Leave the v1 combined generator as historical tooling; do not use it for this Castle kit.
3. `Assets/Scripts/Domain/Procedural/Driver/ProceduralDriver.cs`, `Build`/`CreateBlock`: the owning worker should consume the approved template catalogue and kit, retain primitive fallback for missing kit/pieces, replace full portal frames when applying `closedWith`, and route lights/gimmicks through their owners. This task does not edit runtime scripts or scene/prefab wiring.
4. `Assets/Scripts/Domain/Procedural/Config/ProceduralThemeConfig.cs`, `_castle`/`Castle.InheritMaterials`: replace inherited legacy warm materials with the exact Castle slots/palette when the importer is integrated. Keep texture base colours and `castle_ember` emission; amber is light-only. Import FBX with `bakeAxisConversion = false`; Unity creates all new metadata.
5. Shared room-template contract: specify circular raster footprints and centred short-end hallway socket offsets before round tower/apse and end-cap hallway alternatives are published. No schema extension was invented here.
6. Shared support contract: decide whether floor support may be indirect (for example a globe on a desk), ceiling fixtures may be ceiling-supported, and subfloor pit assemblies have a declared negative floor datum. The literal floor-or-wall-only rule rejects those existing designs. Recommend measured support contacts with a floor/wall/ceiling/subfloor classification, not per-piece unconditional exemptions.
7. Run the Unity integration, navigation, import/material/light and fallback checks after integration. Review `ProceduralThemeUtilityTests.ThemeSwapChangesOnlyItsManifestSuffixAcrossSeedSample` (currently assumes unchanged topology/inherited Castle materials) and `EnvironmentThemeConsumerTests.HospitalReplacesTorchesButPreservesEveryIdentityAndSocket` (fixture identities). Neither fixture was changed; no C# public symbol changed in this task. Refresh the shared graph through the coordinator.
