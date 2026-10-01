# Furnished room expansion — PLAN-026, 2026-10-01

## Activation boundary

This is an explicit content expansion, not a claim that the new rooms already appear in-game. Each theme retains its active `Rooms/<Theme>Rooms.manifest.json`, and gains `Rooms/<Theme>Rooms.expansion.manifest.json`. The existing admission tests require Castle/Hospital/School/Basement counts of 15/14/12/13. `ProceduralTemplateValidationUtility.ValidateRoom` currently rejects kinds `shrine` and `puzzle`, and the import DTO does not retain the new socket fields. Do not rename these rooms to `room` to bypass admission. The coordinator must activate the expansion together with the contract changes below and fresh consumer tests.

The active kit manifests include the new furniture, so the additional FBXs are importable independently of room activation. All exports use the existing theme materials; no new material slots, downloaded content, shrine models, prefabs or metadata files are authored here. Existing active room layouts and vault contracts remain unchanged.

## Source inventory

These are additions to the existing eight editable sources, not a second set of monolithic room FBXs:

| Editable source | Export / manifest |
| --- | --- |
| Castle/Kit/CastleKit.blend | Assets/Art/Environment/Castle/Kit/Castle_*.fbx; CastleKit.manifest.json |
| Castle/Rooms/CastleRooms.blend | Assets/Art/Environment/Castle/Rooms/CastleRooms.expansion.manifest.json, plus retained active assemblies |
| Hospital/Kit/HospitalKit.blend | Assets/Art/Environment/Hospital/Kit/Hospital_*.fbx; HospitalKit.manifest.json |
| Hospital/Rooms/HospitalRooms.blend | Assets/Art/Environment/Hospital/Rooms/HospitalRooms.expansion.manifest.json, plus retained active assemblies |
| School/Kit/SchoolKit.blend | Assets/Art/Environment/School/Kit/School_*.fbx; SchoolKit.manifest.json |
| School/Rooms/SchoolRooms.blend | Assets/Art/Environment/School/Rooms/SchoolRooms.expansion.manifest.json, plus retained active assemblies |
| Basement/Kit/BasementKit.blend | Assets/Art/Environment/Basement/Kit/Basement_*.fbx; BasementKit.manifest.json |
| Basement/Rooms/BasementRooms.blend | Assets/Art/Environment/Basement/Rooms/BasementRooms.expansion.manifest.json, plus retained active assemblies |

New rooms use scenes named `Furnished_<template-id>`. Their mesh instances carry `piece_id` and `placement_index`; the validator checks poses and imported-FBX bounds against the saved source. Roof/near-wall render visibility is only a review cutaway. The blue plan outlines are review-only curves describing reserved space; they are not exported shrine/puzzle models.

## Room inventory and intent

Each theme has six functional rooms, two transitions, one shrine room and one puzzle room. Positions use Unity XYZ metres; footprint cells remain 2m modules. Furniture itself is human-scale rather than stretched to fill a module.

- Castle: `household_refectory` (paired trestles/benches), `household_kitchen` (dresser, preparation table, hearth), `armoury_long` (wall racks and fitting bench), `scriptorium` (bookcases and reading groups), `guard_mess` (cots and mess table), `pew_chapel` (pews facing altar). Transitions: `service_gate`, `infirmary_bend`. Shrine: `reliquary_nook`. Puzzle: `trial_gallery`.
- Hospital: `ward_long` (two wall bed rows with retracted privacy screens), `nurses_office` (L-shaped station with service storage), `surgery_suite` (central operating gurney and wash/storage perimeter), `family_waiting` (wall benches and visitor table/chairs), `sluice_laundry` (washer/sink/linen sequence), `gurney_gallery` (long service corridor). Transitions: `admissions_lobby`, `service_airlock`. Shrine: `ward_memorial`. Puzzle: `rehabilitation_lane`.
- School: `classroom_double` (desk/chair rows toward chalkboard), `reading_library` (bookcase perimeter and reading tables), `refectory` (integrated bench tables), `staff_common_room` (piano, meeting table, bookcase), `gym_equipment_hall` (bleachers, hoop and stacked mats), `cloakroom` (lockers/coats and benches). Transitions: `administration_lobby`, `plant_annex`. Shrine: `remembrance_room`. Puzzle: `movement_hall`.
- Basement: `twin_boilers` (boiler/pump trains), `pump_gallery` (pumps, gauges and risers), `caged_stores` (cages/supply shelves), `laundry` (washer row, sorting bench, cart), `coal_store` (fuel bins with servicing fittings), `repair_workshop` (workbenches paired with toolboards). Transitions: `utility_lobby`, `loading_bend`. Shrine: `boiler_shrine_nook`. Puzzle: `pressure_lane`.

Transition rooms retain their own biome shell and furnish a service/public threshold or old-wing/plant bend. Each offers a standard 3.2m-wide, 2.8m-high opening at Y=0. No cross-theme wall is embedded: neighbouring placement owns its shell and height. This is authored seam compatibility, not implemented mixed-biome generation.

## Authoring and validation

Run the existing four `env_theme_<theme>.py` entry points with Blender 5.2 headless, `--factory-startup --python-exit-code 1`. They now call `env_kit_furnishings.py` for eight new pieces per theme, then `env_theme_furnished.py` / `env_theme_room_layouts.py` for the expansion. Do not use the obsolete combined kit generators to overwrite these restyles.

Each `validate_env_theme_<theme>.py` entry point additionally calls `validate_env_theme_furnished.validate_expansion` with independently reimported FBX vertices. Checks include measured floor bottoms, explicit ceiling/wall support, actual prop and wall separation, door-frame/throat clearance, 1.2m-wide connected routes, cake/hunter and selected player/exit clearance, functional facing, reserved interaction areas and saved-source agreement. Negative controls target floating furniture, wall gaps, duplicate props, blocked doors, obstructed anchors, duplicate shrine sockets, displaced puzzle steps and occupied reservations.

The existing catalogue/kit validators and existing vault rejection controls remain enabled. Review images and hash receipts are under `Logs/AgentValidation/Art/Rooms/<theme>/`, one plan and one perspective per expansion template. Legacy previews are regenerated under `Logs/AgentValidation/Art/Env<Theme>/`. Render hashes establish provenance, not artistic or Unity acceptance.

## Contract extension requests — not implemented here

1. `Assets/Scripts/Domain/Procedural/Definitions/ProceduralTemplateDefinitions.cs`, `ProceduralRoomTemplate`: retain `shrineSockets`, `passageGap`, `puzzleSockets`, `reservedAreas` and transition metadata as typed serializable data. `Assets/Editor/Procedural/ProceduralRoomManifestSetup.cs`, `Parse`: parse them strictly, including finite XYZ vectors, cardinal facing, positive envelopes and exact singular shrine count. Merge each expansion's `templates` after admission, rather than silently discarding the additional file.
2. `Assets/Scripts/Domain/Procedural/Controller/ProceduralTemplateValidationUtility.cs`, `ValidateRoom`/`Validate`: admit `shrine` and `puzzle` as dedicated room kinds, apply ordinary room support/anchor/enclosure requirements to both, enforce exactly one shrine socket and exactly one puzzle lane, preserve existing catalogue coverage and gimmick-budget rules. `Assets/Editor/Tests/Procedural/ProceduralTemplateValidationUtilityTests.cs`, `CheckedInThemeManifestsPassProductionAdmission`: update counts only when all ten expansions per theme are activated; add malformed socket/clearance and round-gate cases, without weakening existing assertions.
3. `Assets/Scripts/Domain/Procedural/Controller/ProceduralTemplateController.cs`, `TryGenerate`/`Attach`: classify dedicated rooms as rooms rather than the current `Kind != "room"` hallway preference; exclude them from exit hubs and ordinary pockets. Respect their reservations. For a Passage shrine, reserve the authored east gap and target pocket using `passageGap`, rotate/translate both with the room, and publish the corresponding `ProceduralGapSite`. Do not turn this into a required graph edge or an ordinary open door.
4. `Assets/Scripts/Domain/Procedural/Driver/ProceduralShrineSitePresenter.cs`, `Build`: in template mode consume the single authored socket and facing in designated rooms instead of scattering generic candidates into every room. Keep existing support, collision, graph and native-navigation admission. Supply `GapEdge=true` only when the declared destination pocket actually exists. `ProceduralPassagePresenter.Corridor` already matches a collinear gap/landing and pocket identity; the runtime must preserve that relationship, not just the gap's visual appearance.
5. `Assets/Scripts/Domain/Procedural/Driver/ProceduralPuzzleLayoutPresenter.cs`, `Build`: consume authored origin/axis with the room transform, require `Kind == "puzzle"` and `Gimmick == "puzzle"`, and retain the existing budget/clearance/collision checks. The current code derives origin from the single golden-cake socket and searches both axes; the extension pins the declared axis instead of accepting a different fit. `ProceduralTemplateUtility.Volumes` merges adjacent subcells into rectangular volumes, so these rectangular puzzle rooms already fit the existing support test; no support relaxation is needed. `Point` already gives the three steps and reward at offsets -3/-1/+1/+3m for the default 8m lane. `GoldenCake` retains exactly that one reward position. Required cake/hunter anchors lie outside the lane.
6. Shrine art owner: fit the original unique model into the socket's 1.2 x 2.4 x 1.2m envelope and agree front-facing orientation with the +X room-facing vector. Art comes from `Assets/Art/Shrine/<Kind>/`; no placeholder is embedded. If a shrine kind needs more space, change its declared envelope and rerun the room checks instead of scaling or clipping it silently.
7. Coordinator: Unity-generated metadata for new exports/manifests, catalogue import and material mapping with `bakeAxisConversion=false`; one generated-floor screenshot per theme in actual game lighting; mixed-theme seam/closed-door tests, shrine singular placement/interaction, Passage opening/landing, puzzle bypass, native player/hunter navigation and owner playtest.
8. `ArtSource/README.md`, Inventory: link this expansion inventory and the four additional room manifests under the existing eight source rows. That root file is outside this worker's owned paths and was not edited.

## Provisional art values

No C# Config or assembly reference changed. The following are offline design defaults:

- `env_theme_furnished.py`: floor Y=0, authored wall gap .008m, minimum walk width 1.2m, gameplay-anchor radius .6m; cake spacing 1.4m with existing density max(2, ceil(2*cells/9)); ordinary weight/minRound inherited as 1/1. Wall-face offsets Castle/Hospital/School/Basement 0/.25/.028/.006m reflect the existing kit geometry.
- `env_theme_room_layouts.py`: explicit furniture poses, rectangular/L footprints, room dimensions and group membership are editable art data. Shrine model 1.2 x 2.4 x 1.2m at (1.8,0,7), interaction 2 x 2.4 x 2m centred at (3.6,0,7), facing +X. Passage edge (10,0,7), landing (14,0,7), width 2.4m, length 4m; pocket template uses the first transition room with offset (7,2) in 2m cells, zero turns. The east-facing slot is sealed until activation and optional-only.
- Puzzle default origin (7,0,7), axis +Z; lane 8 x 1.6m, cage height 2.5m, panel .1m, margin .8m, reserved 3.2 x 2.5 x 9.6m centred at Y=1.35m. Existing challenge defaults are copied, not new runtime tunables. Puzzle minRound=3 and weight=.65.
- `env_kit_furnishings.py`: eight original pieces per theme; dimensions and detailing are local literals in `build_furnishing`, human-scale furniture fitting a 2m bay. Theme material names and existing palettes are reused unchanged. Back planes are local +Z.
- `validate_env_theme_furnished.py`: geometry overlap/ground tolerance .002m, maximum back gap .025m, walk lattice .25m from the existing vault checker; width 1.2m, standing height 2m. Imported/source bounds tolerance .0001m, position .00001m, rotation .0001 degrees. These are test tolerances, not runtime navigation promises.
- Expansion review (`env_theme_furnished.render_expansion`): Cycles 16 samples, 1200 x 1000px, seed 261001, AgX, world strength .45 with colour (.32,.36,.42); key/fill 2600/1800W, sizes 9/8m, perspective 39mm. Plans cut walls at 1.1m and frame the room plus a 2m allowance with an 8% margin; perspectives target 4% image-edge margins, allowing twelve distance adjustments at factor 1.12. Reservation lines have .025m radius and colour (.13,.65,.75). These are neutral review lights, not game lighting. Castle sconces are seated at 3.3m with light sockets at 4.2m; other fixtures derive their top from the theme height.

## Evidence and visual review

Final execution and per-template visual observations are recorded in [FurnishedRoomsReview.md](FurnishedRoomsReview.md). Unity checks remain coordinator-owned; no Unity process, lease, HTTP request, scene/prefab edit, package change or shared Library write was performed in this task.
