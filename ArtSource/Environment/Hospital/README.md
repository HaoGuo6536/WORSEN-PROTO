# Hospital — 1970s institutional ward (PLAN-026)

This is original procedural art, authored entirely in Blender 5.2 without downloaded assets. The restyle is self-contained: `tools/blender/env_theme_hospital.py` does not import or modify the Castle/Hospital v1 generator. It writes only Hospital art and ignored validation output in the `theme-hospital` worktree. No Unity process, import, scene, prefab, script, metadata or shared Library is changed.

## Sources and delivered content

- `Kit/HospitalKit.blend`: every export at its exact origin, plus a labelled review-sheet collection. Originals are hidden; the sheet is visible on opening. Unhide an original to edit/export it.
- `Kit/hospital_{tile,paint,vinyl,acoustic}_surface.png`: four original 512 × 512 texture sources, also packed in the blend and copied to the export directory.
- `Rooms/HospitalRooms.blend`: one collection per template, each at its own manifest-local origin. Ward is initially visible; toggle collections to inspect the others. All placements, including the complete ceilings and camera-side walls, exist. Cutaways affect rendering only.
- `Assets/Art/Environment/Hospital/Kit/HospitalKit.manifest.json`: all 16 mandatory pieces plus 19 additional pieces, measured dimensions, triangle counts, material slots and geometry hashes. All FBXs use metres, applied transforms, -Z forward/Y up. Keep Unity `bakeAxisConversion = false`.
- `Assets/Art/Environment/Hospital/Rooms/HospitalRooms.manifest.json`: 14 templates, with no per-room FBX. Both the editable assemblies and previews are built from these exact placements.

The architecture is a 1.8 m glazed mint tile dado, off-white upper band, vinyl cove, continuous handrails, thin suspended acoustic ceiling grid, flush cyan-white fluorescent panels, and enamel-finished double swing doors with round inset portholes and stainless kick/push plates on both faces of each leaf. Door enamel uses the separate `hospital_door_enamel` slot (`#82988b`, roughness .78); no glazed wall tile appears on the door leaf. Wall height is 3.6 m. Floors are speckled sheet vinyl rather than masonry. Ward bays have ceiling tracks, folded curtains, tubular beds and IV stands. Painted damp blooms, rust bleed at grout joints and selected dead light panels provide non-gory wrongness. The runtime worker owns animation/flicker and interactive doors; the FBXs have static, open leaves.

### Catalogue

| Template id (prefix `hospital_`) | Kind | Class | Cells | Intent |
| --- | --- | --- | --- | --- |
| ward_bed_bays | room | medium | 16 | Two curtained bed bays, clear central aisle |
| nurse_station | room | medium | 14 | L-shaped station and three connections |
| operating_theatre | room | medium | 12 | Gurney, overhead operating lamp and scrub sink |
| recovery_annex | room | medium | 14 | L-shaped recovery wing |
| waiting_room | room | small | 9 | Facing institutional bench seats |
| isolation_room | room | closet | 4 | Narrow single-bed suite |
| sluice_utility | room | small | 6 | Scrub sink and cabinet |
| xray_room | room | small | 9 | Mobile screen and examination gurney |
| long_ward_corridor | hallway | medium | 14 | Two-cell-wide straight corridor, handrails, centred S/N end caps |
| corridor_bend | hallway | medium | 16 | Two-cell-wide L corridor |
| day_room | room | large | 24 | Bench seating and service counter |
| nightingale_landmark | room | hall | 42 | Long open ward with four bed bays |
| isolation_door_freeze | room | small | 9 | Freeze-tagged doorway setup; eligible from round 3 |
| gurney_maze | room | medium | 20 | Staggered traversal obstacles; eligible from round 3 |

## Geometry and socket conventions that integration must preserve

The owner contract places each 3.2 m opening at the centre of one 2 m cell edge. A four-metre frame centred there crosses three cell edges, and its ends fall on odd metre coordinates. To avoid overlap, the catalogue uses the full `wall_door_4m` once, removes all boundary wall spans beneath it, and uses `wall_1m` returns where needed. Floors, ceilings and footprints remain on the 2 m grid. Half-module boundary returns are explicit placements, never scaled meshes.

The run-3 owner clarification uses `span: 2` for both corridors: a 4m frame is centred on each short end cap, referenced by the lower tangent-axis cell (X for N/S, Z for E/W). The straight corridor has S/N sockets and the bend S/E sockets. The nurse station remains an L-shaped furnished room rather than a narrow-arm junction. The isolation suite is a room, not a hallway, and retains its long-side room socket.

`closedWith` is an array of ordinary placement records. Each socket's alternative contains one `wall_closed_4m`, replacing that socket's `wall_door_4m` AND `door_double_porthole_4m` instances. Do not add the closing wall over an existing door frame or leave the open leaves embedded in it. Match the placements by position and yaw. The leaves are a single static mesh posed open at 95 degrees; they are not a rig or an interactive door implementation. Their pivot stays on the wall plane rather than at the centre of their posed depth bounds.

Cardinal wall yaws face the room: N=0, E=90, S=180, W=270. The authoring conversion is Unity `(x,y,z)` to Blender `(x,-z,y)` with positive Blender Z rotation equal to Unity Y yaw. Floor pivots are at -0.16 m so the walking surface is y=0. Ceiling bottoms are at y=3.6. Light socket locations are below the flush fixture.

Wall nonoverlap validation rejects overlapping collinear spans. Perpendicular standard-module corner joins intentionally interpenetrate inside their 0.5 m wall thickness; this is not a general mesh collision validator. It also independently ray-tests imported wall triangles at three elevations on every boundary edge. Navigation, prop clearance for actual player/hunter capsules, interactive socket closure and all gameplay gimmicks remain Unity integration gates.

## Regeneration and validation

From the worktree root in Git Bash:

```sh
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/env_theme_hospital.py
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/validate_env_theme_hospital.py -- --record-baseline hospital-repeat-001
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/env_theme_hospital.py -- --skip-previews
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/validate_env_theme_hospital.py -- --compare-baseline hospital-repeat-001
```

Use a fresh baseline name for a new investigation; recording refuses to overwrite an existing baseline. Generation overwrites only named outputs; it does not clean directories. The two v1 wear PNGs are retained but not referenced by the new kit. Four active textures plus these two legacy textures still fit the six-texture export budget.

The validator checks all v1 contract categories: exact inventory containment, isolated FBX models, raw axes/metre units/identity transforms, finite triangulated nondegenerate geometry, dimensions, pivots, material names, UVs, winding, source/export agreement, straight and curved seams, unobstructed door/window openings, relative packed textures and nonblank renders. Added theme props use their newly authored measured dimensions, not the superseded v1 prop dimensions. It tests the complete room catalogue, source placement agreement and 12 rejection controls. Determinism compares both manifest hashes, measured geometry hashes and texture hashes; FBX timestamps and Blender session metadata are intentionally not byte-determinism targets.

Evidence directory: `Logs/AgentValidation/Art/EnvHospital/`.

The door-fix validator independently unposes each swing leaf, rasterizes its panel at 2.5mm, checks each face's hardware and porthole, and rejects wall materials. Gap and missing-hardware controls exercise rejection. `doors/` contains front/back close-ups with geometry/image hash receipts: double leaves are shown in an explicitly labelled closed inspection pose only; the exported 95-degree pose is unchanged. The shared studio/check helpers live in the owned Castle scripts, but use this theme's geometry exclusively.

Run 3 adds independent end-cap discovery and measured floor/back-face support validation. Signage and ceiling-height luminaires are seated against solid wall faces with an 8mm authored gap; curtain bays gain floor posts while retaining their 3.6m tops. No ceiling-hung prop exemption is used. Full prop containment, wall clearance and run-2 fixture-height checks remain. Offline validation does not establish Unity wiring or visual approval.

- `kit-sheet.png`: every piece, 2100 × 1650.
- `hospital_<template>-three-quarter.png`: each template, 1100 × 850, cutaway ceiling and clinical light sources.
- `in-darkness.png`: full ward shell from eye level, zero world illumination, clinical emission, one 40 W area light and a 65 W flashlight-like spot; no studio fill. Other direct clinical lights are zeroed for this render.
- `review-scenes.json`: cutaway indices and lighting provenance.
- `validation.txt`, `validation.json`: actual check results, hashes and preview statistics. Image-content checks establish nonblank output, not artistic acceptance.

## Provisional authoring values

These are offline art parameters, not new runtime Config fields. Fixed owner palette, module/opening sizes, wall height and mandatory ids are not provisional.

| Value | Default | Location in generator |
| --- | --- | --- |
| Texture / random seed | 260930 | `SEED`, `materials`, `render_setup` |
| Texture dimensions | 512 × 512, four active textures | `TEXTURE_SIZE`, `materials` |
| Dado height / tile pitch | 1.8 m / 0.2 m, approximately 5 mm joints | `DADO`, `wall`, `materials` |
| Additional surface colours | rubber #303a36, glass #455e58, curtain #8eafa0, linen #ddd9c8, acoustic #e4e1d4 | `PALETTE` |
| Surface roughness | tile .24, vinyl .42, stainless .32, other .78 | `materials` |
| Stainless metallic / tile bump | .8 / strength .18, distance .012 m | `materials` |
| Light emission strength | 3.5 | `materials` |
| Fluorescent preview power / area size | 105 W / .86 m square | `LIGHT_WATTS`, `lamp` |
| Light placement / dead fixtures | cell `(x + 2*z) % 4 == 1`; every fifth selected fixture dead | `assemble_template` |
| Darkness direct lights | first fluorescent 40 W, others 0 W; spot 65 W, 48° cone, .5 blend | `room_sources`, `lamp` |
| Preview sampling / exposure | 24 Cycles samples, denoising, AgX; world strength .28 or zero for darkness | `SAMPLES`, `render_setup`, `room_sources` |
| Camera | room orthographic scale `1.45*max(width,depth)+2`; darkness perspective 21 mm | `room_sources` |
| Kit review studio | 7000 W, 25 m area; orthographic scale 46 m | `kit_sheet` |
| Door leaf pose | 95° open | `door_leaves` |
| Handrail / signage base | .95 m / 2.05 m | `assemble_template` |
| Curtain bay placement | floor Y=0, 25mm-radius posts, top 3.6m | `curtain`, `catalogue.bed_bay` |
| Cake socket count | at least 2; ceiling(cells/5), distributed over furniture-free cell centres | `assemble_template` |
| Prop exclusion margin for cake candidates | .35 m around rotated furniture bounds | `assemble_template` |
| Room selection weights / rounds | ordinary 1.0 from round 1; gimmick .6 from round 3 | `assemble_template` |
| Furniture and layout dimensions | authored metre coordinates, measured sizes recorded per piece | `furniture`, `catalogue`, kit and room manifests |
| Run-3 light anchors | .12m inward from panel centre and .04m below panel bottom | `catalogue` |

## Requests to coordinator / other owners

1. `ArtSource/README.md`, Inventory: add these two rows (not edited by this worker):

   `| [Environment/Hospital/Kit/HospitalKit.blend](Environment/Hospital/Kit/HospitalKit.blend) | Assets/Art/Environment/Hospital/Kit/Hospital_*.fbx and four hospital_*_surface.png textures | Original 1970s ward; tools/blender/env_theme_hospital.py; validate_env_theme_hospital.py; see Environment/Hospital/README.md. |`

   `| [Environment/Hospital/Rooms/HospitalRooms.blend](Environment/Hospital/Rooms/HospitalRooms.blend) | Assets/Art/Environment/Hospital/Rooms/HospitalRooms.manifest.json (no room FBXs) | Fourteen kit-assembled room templates from env_theme_hospital.py; see Environment/Hospital/README.md. |`

2. `tools/blender/README.md`: add the line `Hospital restyle and room catalogue: see ../../ArtSource/Environment/Hospital/README.md; generate with env_theme_hospital.py and validate with validate_env_theme_hospital.py (Blender 5.2, --python-exit-code 1).` Add the four regenerate/validate commands above if keeping commands centrally.

3. `Assets/Editor/Procedural/ProceduralContentSetup.cs`, `ProceduralContentSetup`: coordinator-owned integration needs a deterministic kit/room importer (currently `WireSelected` only connects theme/challenge configs). Consume the two manifests, preserve FBX axis settings, create URP material mappings for every listed `hospital_*` slot, assign the four sRGB textures, and wire cyan-white emission only to `hospital_light`. Do not import the sources as runtime assets. Unity must create all new `.meta` files.

4. PLAN-026 procedural owner: consume `closedWith` as replacement placements and the approved `span: 2` end-cap centre; connect light sockets to fluorescent lighting/flicker, preserve missing-kit fallback, and bind gimmicks. Existing `ProceduralThemeUtilityTests` and `EnvironmentThemeConsumerTests` do not establish these manifest/import behaviours; add integration and navigation coverage in coordinator-owned scope.

5. Owner: judge the kit sheet, the complete room catalogue and `in-darkness.png`. No cross-theme visual comparison or human artistic approval is claimed by the validator.
