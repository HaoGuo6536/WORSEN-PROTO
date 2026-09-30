# Basement industrial kit and room catalogue

Original procedural geometry, authored for PLAN-026's 2026-09-30 industrial-boiler direction. No downloaded assets, textures, school-generator imports, Unity calls or manually authored metadata.

## Sources and exports

- `Kit/BasementKit.blend`: all 44 origin-aligned source meshes plus a labelled kit-sheet scene.
- `Rooms/BasementRooms.blend`: one scene per room template, plus the retained source meshes and kit sheet. Each template scene has every manifest placement, named by its placement index. `hide_render` makes review-only wall/ceiling cuts; it does not remove geometry from the manifest.
- `../../../Assets/` is not a source location. Runtime exports are at repository-root `Assets/Art/Environment/Basement/Kit/` (44 FBXs and the kit manifest) and `Assets/Art/Environment/Basement/Rooms/BasementRooms.manifest.json` (13 templates). There is no per-room FBX.
- Generators: `tools/blender/env_theme_basement.py` and `tools/blender/validate_env_theme_basement.py`, both repository-root relative. The older School/Basement generator remains unchanged and must not be used to regenerate this restyle.

The catalogue is boiler room (hall), pump room (medium), electrical room and storage cage (small), fuel bunker (closet), pipe tunnel and service bend (hallways), duct junction, catwalk hall (large), irregular sump room, L-shaped valve gallery, steam-vent traversal and flooding-pit freeze rooms. Gimmick rooms are authored staging and sockets, not an implementation of steam damage, timed traversal or dynamic flooding.

## Regeneration and verification

Use Blender 5.2 with `--background --factory-startup --python-exit-code 1 --python tools/blender/env_theme_basement.py` from this worktree. The optional `-- --skip-previews` regenerates the same assets without re-rendering existing previews.

Run the validator with the same Blender flags and `--python tools/blender/validate_env_theme_basement.py`. For two-run determinism, validate the first generation with `-- --record-baseline`, regenerate, then validate with `-- --compare-baseline`. A baseline already present is deliberately not overwritten: retain it under a historical name before intentionally accepting a changed generation. Reports and render receipts live under `Logs/AgentValidation/Art/EnvBasement/`.

The validator imports every FBX, checks applied raw FBX transforms and axis metadata, source parity, metre dimensions, material names, budgets, pivots, triangle degeneracy, seams and clear door aperture. It validates exact boundary interval coverage, full socket spans and closure alternatives, footprint connectivity, catalogue coverage and anchor clearance. Imported concrete cross-sections reject perpendicular slab intersections at re-entrant corners. Every manifest placement and transformed inward direction is compared with its Blender scene. Negative controls reject damaged templates and geometry. Preview checks establish file integrity, current manifest hashes, framing, dimensions and nonblank content, not artistic approval.

## Placement and integration details

- Unity XYZ metres; +Y up, +Z north, piece fronts face -Z. Blender `(X,-Z,Y)` conversion preserves signed yaw about the up axis. FBX forward -Z, up Y, applied scale/rotation, `bakeAxisConversion=false` in Unity.
- Wall height 3.2 m, concrete thickness .25 m. Ordinary modules have matched flat end profiles. The four `wall_miter_{left,right}_{1,2}m` variants cut only the exterior side of re-entrant corner ends, avoiding overlapping slabs.
- Every 3.2 x 2.8 m clear opening is centred on its stated 2 m cell edge, inside an actual 4 m door wall. Its full 4 m span is reserved, including adjacent cell-edge portions. One-metre infills complete the remaining boundary. Do not also instantiate default walls on those reserved portions.
- Each socket's `closedWith` is a list of two normal wall placements replacing the entire 4 m door wall. It is an alternative, not additional geometry. Remove the socket's `wall_door_4m` when closing it.
- A centred 3.2 m aperture cannot fit a 2 m or 4 m-wide hallway end edge at an odd-metre cell-edge centre. Both hallway templates therefore use lateral entrances near opposite ends, on long boundary runs. The straight passage remains four metres wide. Do not recenter the manifest sockets.
- Floor origins sit at the bottom: concrete placed at -.1001 m and grate at -.12 m make their walking surfaces Y=0. Pit liners are at -1.2 m. The catwalk rail defines a central route over visibly perforated maintenance decking. Author solid walkable colliders separately; do not derive navigation from the thin grate bars.
- Live lamp bulbs are separate from their light socket, which is just below the cage to avoid self-shadowing. Dead fixtures have no light anchor. FBXs contain mesh geometry only; actual lights must be supplied by setup/runtime owners.
- Bulkhead leaf, extra joists, isolated I-beam, valves, elbows and narrow catwalk are available kit pieces; not every optional piece is placed in each room.
- Origin-aligned source meshes are not themselves an assembled room. Select the named kit-sheet or room scene for review.

## Palette and provisional art values

The owner palette is fixed sRGB, converted to linear when assigned in Blender: concrete `#5e6061`, damp `#3a3c3d`, rust `#8a4b22`, steel `#161514`, galvanised `#8d9396`, insulation/efflorescence `#bdb6a4`, sodium emission `#ff9a3c`, sparse hazard accents `#c8a62a`. `basement_water` uses oily-black base colour. No orange paint, tile surface or painted dado band exists.

The following are provisional authoring/review defaults, not new Unity runtime configuration:

- `materials()`: roughness .38 for steel/galvanised, .86 for other dry surfaces; metallic .75 for steel/galvanised/rust and 0 otherwise. Water roughness .065, metallic .35. Sodium emission strength 4, used only on the live cage bulb.
- `BasementMesh.__init__()`: stable per-piece random seed string `basement/industrial/<piece_id>`. Only stain outlines and fuel lumps use this private random source; there is no global randomness.
- `concrete()`, `beam()`, `grate()`, `build()`: .25 m wall thickness; .1 m concrete floor; .12 m grate; .30 m exposed beam depth plus .04 m ceiling slab. Tie-hole radius .029 m at Y=.55/1.9/2.95 m. Other literal dimensions in these functions are editable asset geometry, not gameplay tunables.
- `catalogue()`: authored cell sets and prop placements are the template design. Cake count is one for closets, otherwise at least two and `ceil(cells/6)`; normal weight 1, gimmick weight .6; minRound 1 or 3. Pit depth 1.2 m; overhead services start at 2.28 m; lamp base 2.18 m and light socket 2.12 m. Ceiling bottom 2.86 m, top 3.2 m.
- `setup_scene()`: Cycles 32 samples, seed 260930, denoising, AgX; room previews 1200x800, kit sheet 1800x1400. Review ambient colour (.12,.14,.16), strength .18; darkness ambient 0.
- `lighting()`, `rooms_source()`: sodium point lights 95 W, .13 m radius; review-only area fill 1900 W, colour (.8,.86,1), size 9 m. Flashlight-only spot 190 W, colour (.84,.9,1), 52-degree cone, .55 blend, .04 m radius. The darkness image restores the full room envelope and uses no area fill.
- `sheet()`: review softboxes 9000 W / 6500 W, sizes 18 / 14 m, colours (1,.92,.8) / (.72,.83,1), orthographic scale 48 m. Room orthographic scale `max(width,depth)*1.5+4`; darkness lens 23 mm; camera near clip .03 m. Camera positions are authored review framing, not player-camera settings.

## Requests to other owners

1. `ArtSource/README.md`, Inventory: add these two rows (do not replace the older School entry):

   `| [Environment/Basement/Kit/BasementKit.blend](Environment/Basement/Kit/BasementKit.blend) | Assets/Art/Environment/Basement/Kit/Basement_*.fbx; BasementKit.manifest.json | Original industrial boiler kit; tools/blender/env_theme_basement.py; validate_env_theme_basement.py. |`

   `| [Environment/Basement/Rooms/BasementRooms.blend](Environment/Basement/Rooms/BasementRooms.blend) | Assets/Art/Environment/Basement/Rooms/BasementRooms.manifest.json | Thirteen assembled templates using the Basement kit; no per-room FBX. |`

2. `tools/blender/README.md`, add a Basement subsection:

   `Industrial Basement: regenerate with Blender 5.2 --background --factory-startup --python-exit-code 1 --python tools/blender/env_theme_basement.py. Validate with the same flags and tools/blender/validate_env_theme_basement.py. Use -- --record-baseline on the first validation and -- --compare-baseline after regeneration. Sources and integration notes: ArtSource/Environment/Basement/README.md. Previews: Logs/AgentValidation/Art/EnvBasement/. Do not use env_kit_schoolbasement.py to regenerate the restyled Basement.`

3. `Assets/Editor/Horror/HorrorWorldAssetSetup.cs`, `Configure` / `BuildEnvironmentConfig`: route Basement asset/material/template setup through the coordinator's theme importer rather than castle materials, arches and torches. Preserve existing identities; generate all new `.meta` files in Unity. Map all nine `basement_*` surface slots to URP and wire cage lights from the explicit anchors. Actual emission/lighting parity needs live review.

4. Procedural/template owner: consume the exact placements and `closedWith` alternatives, honour 4 m aperture reservations and authored inward yaw, provide grate walking colliders, and implement the two gameplay gimmicks. Preserve missing-piece and missing-kit primitive fallback. No consumer of these new room manifests exists in this worktree's inspected C# files; the consumer's final file/symbol is coordinator-owned, so no guessed runtime edit was made.

5. `Assets/Editor/Tests/Procedural/ProceduralThemeUtilityTests.cs` and `Assets/Editor/Tests/CastleEnvironment/EnvironmentThemeConsumerTests.cs`: extend the current castle/hospital-only coverage to Basement after importing; add template-closure, fallback, collider/navigation, low-headroom and sodium/dead-fixture checks. Existing expectations were not edited by this art task.

## Remaining acceptance

No Unity process, lease, scene, prefab, script, assembly, package, project setting or shared source README was changed. Offline Roslyn compilation and structural lint do not prove runtime wiring. Owner judgement of the darkness render, style differentiation, fog readability and live importer/material appearance remains necessary. No vision-capable review tool was exposed in this worker session; image-integrity measurements are not presented as human visual acceptance.
