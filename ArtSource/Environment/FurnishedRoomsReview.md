# Furnished rooms: visual review and handoff evidence

Companion contract/inventory: [FurnishedRooms.md](FurnishedRooms.md).

## Visual method and limits

Blender 5.2 headless generates both a plan and a perspective for every expansion template. Plans cut architectural meshes at 1.1m to reveal door apertures; perspectives remove the roof and camera-side walls. Furniture against a removed wall remains in its actual position, so the plan is the reference for wall contact. Neutral review lighting is deliberately not presented as in-game lighting. Pale cyan floor outlines mark reservations and do not depict shrine/puzzle art.

The vision tool was used to inspect paired plan/perspective sheets and individual close-ups. The first review led to twelve classroom desk/chair pairs instead of six, four reading groups instead of two in the school library and castle scriptorium, corrected shrine pew facing, and improved plan/perimeter and perspective framing. Geometry checks separately caught a basement locker at a door jamb, a shelf on a nonexistent L-room wall, an overlapping sconce/rack, a board on a projecting window and a lamp inside the reserved puzzle envelope. The fixes change placement or review geometry; assertions were not weakened.

## Observations by template

All IDs below have the theme prefix. These are observations of the renders, not claims of Unity navigation or owner approval.

### Gothic keep

- `household_refectory`: four trestle/bench groups form two ranks and a broad aisle; a hearth and sideboard identify a household dining function. Benches meet the floor and are paired with tables rather than scattered.
- `household_kitchen`: dresser shelving and hearth occupy the service wall, with a central preparation table and grounded barrels. The room reads as a working kitchen rather than an empty chamber with one decoration.
- `armoury_long`: weapon racks line adjacent walls; the centre bench provides a fitting/maintenance station. Raised sconces no longer intersect the tall racks.
- `scriptorium`: four reading-table/bench groups sit inside an L-shaped bookcase perimeter. The central and doorway approaches remain open; the full-size perspective shows floor contact and shelf backs clearly.
- `guard_mess`: paired cots against the back wall and a shared mess table provide a compact living function; the rack remains peripheral.
- `pew_chapel`: two banks of pews face the altar, with a clear central aisle and a side lectern. The high masonry shell retains the existing keep scale.
- `service_gate`: sideboard and bench make a restrained service vestibule; furniture is concentrated at the back rather than the door throats.
- `infirmary_bend`: the L-shaped shell gives a genuine turn and separate furniture pockets. The exposed far portal makes the change of wing legible.
- `reliquary_nook`: a framed wall panel and pews establish the shrine side of the room. The shrine-model, interaction and Passage reservations are distinct in plan; no fake shrine model has been inserted.
- `trial_gallery`: perimeter weapon racks and a bookcase frame a deliberately clear challenge lane. The broad empty area is reserved gameplay space, not a finished puzzle visual.

### 1970s hospital

- `ward_long`: two ranks of three beds face into the central aisle. Freestanding retracted privacy screens have visible feet and rails; the bed heads meet their walls.
- `nurses_office`: an L-shaped service room separates counters/storage from circulation. It reads as a standing service-counter arrangement, not a furnished private office; runtime signage and activity remain outside this art task.
- `surgery_suite`: the operating gurney and IV stand form the central treatment group, with wash/storage and an X-ray screen around the perimeter. Service access is left around the gurney.
- `family_waiting`: perimeter bench seats and a paired visitor-chair/side-table group make the waiting function clear. The radiator is wall-backed, not hovering in a corner.
- `sluice_laundry`: washers, scrub sink and linen shelving form a continuous service sequence against the wall. The open floor supports equipment handling rather than arbitrary decoration.
- `gurney_gallery`: the long narrow footprint is visibly different from a square ward; parked gurneys and opposite waiting benches leave a longitudinal walking route.
- `admissions_lobby`: a linen/service shelf and bench occupy the back wall, keeping both entries unobstructed. This is intentionally a sparse transition rather than another ward.
- `service_airlock`: the L-shaped room has storage at the end of one arm and seating in the other; the turn supplies a plausible ward/service seam.
- `ward_memorial`: seating and the nurses'-counter backdrop frame the single shrine reservation; the Passage approach remains clear. The empty memorial panel needs the separate shrine asset to complete its focal point.
- `rehabilitation_lane`: a wall row of waiting benches and end storage face the reserved challenge area. It reads as an activity/treatment lane, but the puzzle mechanism is runtime-owned.

### Post-war school

- `classroom_double`: twelve yellow desk/chair pairs face the chalkboard; the teacher's desk is separate at the teaching wall, with two wall-backed radiators. The revised density reads as a classroom, with a broad rear cross-route for the doors.
- `reading_library`: perimeter bookcases surround four two-chair reading tables. The additional groups replace the first revision's excessively empty centre.
- `refectory`: two ranks of integrated table/bench assemblies provide repeated dining groups; the fountain and radiator remain peripheral. Each bench belongs visibly to a table.
- `staff_common_room`: a piano, bookcase, meeting table/chairs and noticeboard create a recognisable staff room. The piano's keys and freestanding legs are visible in the kit review.
- `gym_equipment_hall`: bleachers, wall hoop and stacked mats leave a large central activity floor. This room is open by function, unlike the furnished classroom.
- `cloakroom`: lockers/coats along both sides and central changing benches form a storage/changing corridor. Coat rails, bench legs and bases are visibly supported.
- `administration_lobby`: a wall coat rack and bench define a public-to-staff threshold without obstructing the entries.
- `plant_annex`: the L-shaped shell and far doorway make a distinct service turn; the coat/bench placement remains at the ends rather than in the walking bend.
- `remembrance_room`: bookcase, bulletin board, bench seating and a framed memorial panel provide the backdrop for exactly one future shrine. The plan preserves its interaction and Passage zones.
- `movement_hall`: a locker bank rhythm and end chalkboard identify a school activity room around the empty puzzle reservation.

### Industrial basement

- `twin_boilers`: the name denotes a boiler-house type; the authored layout actually has three boiler/pump trains. The pumps align with their wall boilers, with electrical service equipment and risers at the sides. This is a visible industrial equipment arrangement, not a domestic room.
- `pump_gallery`: wall pump/gauge pairs and vertical risers make service relationships clear. The forward maintenance space remains open.
- `caged_stores`: cage fronts along one wall and stocked supply shelves on the other form an industrial storage perimeter. Shelves and cage feet meet the floor.
- `laundry`: a washer row, supply shelves, cart and sorting/work bench identify the room's purpose. The cart is deliberately freestanding beside the shelves, not wall-type furniture.
- `coal_store`: three filled bins share the back wall, with risers and a service locker. The locker was moved clear of the entry frame before acceptance.
- `repair_workshop`: toolboards sit over aligned workbenches, with nearby stocked shelves and a locker. Tools are attached to boards rather than suspended in space.
- `utility_lobby`: the locker/workbench pair is restrained service dressing at a clear threshold.
- `loading_bend`: the L-shaped shell and bulkhead opening supply the transition; the workbench and locker occupy different ends rather than the turn.
- `boiler_shrine_nook`: boiler, locker, workbench and a framed industrial back panel distinguish this shrine room from a chapel. Its single socket and Passage reservations stay open for the other owner's model and runtime crossing.
- `pressure_lane`: risers along the side and gauges on the end wall frame a dedicated clear challenge lane, with a walking bypass separate from the future mechanism.

## Checks and handoff limits

- Final render generation: `*-publish-09.log`; all four exit 0. All 80 final images were checked with the vision tool via `review-09-1.png` through `review-09-5.png` per theme, with individual classroom/scriptorium and plan-framing close-ups. Image SHA-256, 1200 x 1000 dimensions and nonblank checks also pass.
- Final full validators: `castle-validate-03.log`, `hospital-validate-03.log`, `school-validate-03.log`, `basement-validate-03.log`, all exit 0. Each admits ten expansion rooms and rejects all ten new mutations, in addition to the existing kit/active-room/vault/preview gates. Export inventories are Castle 54, Hospital 44, School 53 and Basement 54 pieces; each includes eight new furnishings.
- `ast-grep scan`: exit 0, empty output (zero findings), `ast-grep-final.txt`.
- Offline compile run `furnished-rooms-001`: all seven assemblies compile with zero errors and no warning-baseline growth.
- Headless pure run `furnished-rooms-pure-002`: `PURE_RESULT passed=152 failed=0 environment=9 skipped=0`.
- Filter: `^Worsen[.]Tests[.]Procedural[.](ProceduralTemplateValidationUtilityTests|ProceduralTemplateAnchorTests|ProceduralTemplateConsumerTests|ProceduralPuzzleLayoutPresenterTests)[.]`.
- Pure coverage comprises 59 template-validation, 55 anchor/reachability and 38 consumer cases. No NUnit fixture or expectation was changed. These tests exercise the preserved active catalogues, not activation of the expansion schema.
- Unity-only cases: the eight parameterizations of `ProceduralTemplateValidationUtilityTests.SegmentMatchesNativeEulerInUnity` and `ProceduralPuzzleLayoutPresenterTests.SeedSampleHasFourOptionalModulesWithoutChangingRequiredGraph`. The puzzle fixture therefore has no pure coverage.
- Shared furnished validation adds ten destructive controls per theme: floating, wall gap, overlap, blocked door, blocked anchor, duplicate shrine, misplaced puzzle step, occupied reservation, missing wall classification and incorrect functional facing. It also checks the declared Passage target's supported landing and walking routes.
- All 198 existing environment `.meta` files match the pre-generation SHA-256 inventory. Unity must create metadata for the new exports and expansion manifests. No metadata was hand-written.
- No Unity checks ran. Required coordinator checks are catalogue import and one generated-floor screenshot per theme in game lighting, followed by native navigation, cross-theme seams, socket closure, singular shrine/interaction, Passage and puzzle checks.
- The expansion is not active in the current runtime. Consumer/schema changes and fixed-count test updates are precisely listed in `FurnishedRooms.md`. Shrine models remain another owner's work. Final taste, lighting and playability acceptance belongs to the owner.

Machine evidence lives under `Logs/AgentValidation/Art/Rooms/`: per-theme render receipts, imported-FBX validation JSON, generation/validation logs, metadata comparison, lint and the raw Git diff report. Failed generation logs are retained separately from successful successors.
