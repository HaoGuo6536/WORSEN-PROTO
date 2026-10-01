# School art handoff — PLAN-026

2026-10-01 expansion: [Furnished rooms](../FurnishedRooms.md) inventories ten additional assembled rooms and eight new furniture meshes in these same Kit/Rooms sources. The expansion manifest is intentionally pending coordinator activation; the twelve active templates below are preserved.

## Deliverables

`Kit/SchoolKit.blend` contains editable, origin-centred export masters and a labelled sheet. Small props are enlarged only in the sheet for legible thumbnails; export masters and room instances remain metre-scale. `Rooms/SchoolRooms.blend` contains a separate, fully assembled scene for every room template, plus the full-shell darkness scene. All room instances are generated directly from `Assets/Art/Environment/School/Rooms/SchoolRooms.manifest.json`; there are no room FBXs. Viewport geometry keeps the full shell; render visibility cuts away south/west walls and most ceiling tiles for the room sheets.

Original procedural geometry only. No downloads, external art, baked textures, readable signage or gore. The new builder does not import or modify `env_kit_schoolbasement.py`. Running that legacy builder for School will overwrite this restyle: use the new builder for School and retain the old builder for Basement only until its owner replaces it.

The 44-piece kit preserves all 16 mandatory identifiers. Architecture is painted .25 m cinder block, .4 m courses, a 1.2 m teal dado with a thin mustard line, cream upper walls, steel multi-pane windows, mustard classroom leaves with narrow safety windows, wired transoms, brown sheet linoleum and acoustic ceiling T-grid. Fluorescents are suspended, exposed twin tubes, not ward panels. Lockers, drinking fountains, chalk rails, fire bells and steel stair banisters carry the School silhouette. Sparse scuffs, missing ceiling panels, painted/boarded panes and dead fixtures supply damage.

## Rebuild and validate

Run from the theme-school worktree using Blender 5.2, not Unity:

    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/env_theme_school.py
    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/validate_env_theme_school.py -- --determinism

The validator launches two real regeneration runs with `--skip-previews`, compares both manifest SHA-256 values, reimports every FBX and checks saved room instances. Existing previews must match the generator and manifest input hashes. Reports, retained run logs and PNGs are under `Logs/AgentValidation/Art/EnvSchool/`. FBX timestamps and Blender session metadata are not required to be byte-identical.

## Templates

| ID suffix | Cells | Class / shape | Role |
| --- | --- | --- | --- |
| classroom | 16 | medium / rect | Student desks, teacher desk and chalk rail |
| science_lab | 12 | medium / L | Sink benches and stools/chairs around an L footprint |
| library | 32 | large / L | Bookshelves and reading desks |
| gymnasium | 49 | hall / rect | Wall hoops and bleachers |
| cafeteria | 25 | large / rect | Long shared tables and benches |
| principal_office | 9 | small / rect | Pedestal desk, bookcase and bulletin board |
| janitor_closet | 4 | closet / rect | Long narrow supply room with mop bucket |
| washroom | 9 | small / rect | Basins, toilet stalls and teal partitions |
| locker_hallway | 16 | medium / rect | Two-cell-wide straight hall, locker banks both sides |
| stairwell_bend | 20 | medium / L | Two-cell-wide bent junction with steel-banister stair flight |
| bleacher_traversal | 30 | large / rect | Round-3 traversal tableau |
| locked_classroom | 16 | medium / rect | Round-3 freeze tableau |

The two gimmick values are runtime selection metadata, not implemented gameplay. The freeze-room leaves are authored closed; the runtime owner must wire their open/locked state and trigger. Stair meshes are visual geometry; ramp collision, landing admission and traversal links belong to the runtime owner.

## Socket and placement interpretation

All positions are Unity metres (+Y up, +Z north); all exports have applied transforms with -Z forward/Y up. Wall plane pivots are at floor height, with the wall mass outside the room; props and floor/ceiling meshes use bottom-centre pivots. Floor placements are lowered by .0702 m so their walkable top is Y=0. Ceiling bottom is Y=3.8 m.

A socket centred on a cell edge has an odd-metre tangent coordinate. Its 4 m frame therefore ends between normal 2 m boundary segments. `wall_cinderblock_end_1m` closes the remaining 1 m ends without overlapping another wall or shrinking the 3.2 m aperture. This auxiliary width does not replace the mandatory 2 m module.

The run-3 owner clarification puts `span: 2` sockets on the short end caps, centred across the 4m hallway width. The straight hall has S/N sockets; the stairwell bend has E/N sockets. The recorded cell is the lower tangent-axis cell (X for N/S, Z for E/W), and the whole 4m frame/leaf pair spans both cells.

`closedWith` is a list of two normal placement objects, replacing the complete 4 m door-frame placement. It is not additional geometry over the existing door frame. Remove the two adjacent leaf props as well when sealing a socket. The validator checks the closure's full 4 m coverage.

Floor anchors avoid furniture bounds plus .2 m clearance and are at least .6 m from every footprint boundary. This is not native capsule/NavMesh reachability evidence. Light anchors correspond only to live tubes; dead fixtures have no light socket.

## Authoring defaults (provisional, not new runtime tunables)

Definitions live in `tools/blender/env_theme_school.py`:

- `DADO = 1.2`, wall depth .25 m, coursing .4 m; geometry functions contain the other fixed authored dimensions.
- `SEED = 260930`; independent deterministic per-piece random streams.
- `materials`: roughness .84; steel .48 roughness/.35 metallic; tube emission 3. Colours convert owner sRGB hex to linear RGB before assignment. Seven owner colours are fixed requirements, not provisional alternatives. Additional supporting colours are the entries in `PALETTE` (mortar, rubber, wood, scuff, stain, glass, paper and live/dead tubes).
- `make_room`: ordinary `weight=1.0`, gimmick `weight=.65`; minRound 1/3 respectively. Gimmick floor limits/curve are runtime-owned, not encoded in these weights.
- `safe_anchors`: cakes `max(2, ceil(cells/6))`, furniture margin .2 m, selected anchor separation 1.4 m; golden anchors in large/hall or gimmick rooms; hunter anchors in medium and larger rooms.
- `furnish`: fixture bottoms derive from measured height so their tops meet 3.8m; every fourth fixture is dead. Run-3 wall seating uses an 8mm gap; light anchors are .46m inward from the fixture centre. These poses are explicit manifest data.
- `configure_render`, `kit_sheet`, `room_scenes`: Cycles 24 samples; room/dark previews 1440x1000, sheet 2400x1800. Review fixture power 100 W, dark fixture power 32 W, flashlight 95 W at 48 degrees. Darkness world strength 0. Studio and sheet lights exist only for art review.

No Unity Config or assembly references changed.

## Requests to other owners

1. `ArtSource/README.md`, Inventory: add `Environment/School/Kit/SchoolKit.blend` -> `Assets/Art/Environment/School/Kit/School_*.fbx`; provenance `tools/blender/env_theme_school.py`, validator `tools/blender/validate_env_theme_school.py`. Add `Environment/School/Rooms/SchoolRooms.blend` -> `Assets/Art/Environment/School/Rooms/SchoolRooms.manifest.json`; manifest-authoritative review assembly, no room FBXs.
2. `tools/blender/README.md`, new School section: add the two commands above, source/export paths and preview directory; state that the old School/Basement generator must no longer regenerate School.
3. `Assets/Scripts/Domain/Procedural/Driver/ProceduralDriver.cs`, `Build` (or the procedural owner's approved replacement): consume the School room catalogue, honour socket direction and `closedWith`, preserve missing-piece/whole-kit primitive fallback, and provide ramp/landing/bleacher collision. No implementation was made here.
4. Coordinator-owned School import/setup: Unity must generate `.meta` files; retain `bakeAxisConversion=false`; map every exported `school_*` material using the generator's palette, including live tube emission and non-emissive dead tubes. Bind manifest light sockets and both gimmick behaviours. No such School importer exists in this worktree, so no symbol is invented here.
5. After integration, run import/material, door closure, capsule clearance, navigation, gimmick and missing-kit tests. Existing `ProceduralThemeUtilityTests` and `EnvironmentThemeConsumerTests` cover only castle/hospital and need separate School coverage. They were read but not changed.

## Acceptance limits

The classroom leaf is now one connected panel extrusion around its deliberate safety-glazing hole. It retains the coordinator's seam fix, adds glazing/beading on both faces and uses `school_door_laminate` (`#c9a23a`) rather than a wall-paint slot. Hardware stays on both faces. Independent body rasterization at 2.5mm exempts only the explicit window rectangle; it rejects 5/10/80mm through-gaps and missing face hardware. Geometry-bound front/back close-ups are under `Logs/AgentValidation/Art/EnvSchool/doors/`. Repeat-generation checks now compare every imported mesh's semantic hash as well as both manifests.

Run 3 independently discovers corridor end caps and measures every non-ceiling placement's floor or back-face support. Tabletop globes stay in the kit but are no longer unsupported room placements. Fluorescents retain their run-2 ceiling-height tops and now also meet solid wall faces; light sockets are inset for the required wall clearance. Full prop containment and wall-clearance checks remain, including outside/hole/embedded controls. Unity integration and artistic approval are separate gates.

Provisional spatial values in `furnish`: wall-mounted detail gap .012m and minimum extra wall-span width .02m; library shelf inset .55m; locker inset .50m (stairwell north placement Z=3.50m); stall partitions Z=depth-1.1m; science-lab workstations (X,Z)=(2,6),(5,6),(6,2)m. Hoop inset is measured half-depth plus .012m; fixture height is derived from measured mesh height, not a tunable constant.

Offline kit, room, repeat-generation, compile and lint evidence is under Logs. No Unity process, lease, scene, prefab, C# file or shared checkout was operated or modified. The main index has no target for the offline generator (UNKNOWN, not a low-risk verdict); no Unity callable symbol was changed. Graph refresh is coordinator-owned.

The PNGs are rendered artifacts, not Unity screenshots. Pixel and scene checks do not establish visual taste, fog readability or player reachability. Owner review should start with `in-darkness.png`, then `school_locker_hallway-three-quarter.png`, `school_classroom-three-quarter.png` and `kit-sheet.png`.
