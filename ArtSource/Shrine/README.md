# Eight shrine models — PLAN-025 handoff

Original procedural art; no downloads, vendor assets, textures or fonts. All models share
one stepped, bevelled altar, a front offering step/seal and two used material slots.
Sources are in this directory; runtime exports are under `Assets/Art/Shrine/`.

## Inventory row requests for the coordinator

`ArtSource/README.md` is outside this worker's ownership. Add these rows to its inventory
(paths below are relative to that file); do not mistake this request for an applied edit.

| Source | Unity exports | Provenance |
| --- | --- | --- |
| [Shrine/Chance/WORSEN_ShrineChance.blend](Chance/WORSEN_ShrineChance.blend) | `Assets/Art/Shrine/Chance/WORSEN_ShrineChance.fbx` | Original tilted die in a grasping cradle; `tools/blender/shrine_kinds.py`, checked by `validate_shrine_kinds.py`. |
| [Shrine/Bargain/WORSEN_ShrineBargain.blend](Bargain/WORSEN_ShrineBargain.blend) | `Assets/Art/Shrine/Bargain/WORSEN_ShrineBargain.fbx` | Original unequal balance: coins versus a spiked curse weight; same generator/validator. |
| [Shrine/Pacification/WORSEN_ShrinePacification.blend](Pacification/WORSEN_ShrinePacification.blend) | `Assets/Art/Shrine/Pacification/WORSEN_ShrinePacification.fbx` | Original lullaby bell with closed-eye incisions; same generator/validator. |
| [Shrine/Wick/WORSEN_ShrineWick.blend](Wick/WORSEN_ShrineWick.blend) | `Assets/Art/Shrine/Wick/WORSEN_ShrineWick.fbx` | Original dripping candle and faceted frozen flame; same generator/validator. |
| [Shrine/Passage/WORSEN_ShrinePassage.blend](Passage/WORSEN_ShrinePassage.blend) | `Assets/Art/Shrine/Passage/WORSEN_ShrinePassage.fbx` | Original pointed portal and supported miniature bridge steps; same generator/validator. |
| [Shrine/Protection/WORSEN_ShrineProtection.blend](Protection/WORSEN_ShrineProtection.blend) | `Assets/Art/Shrine/Protection/WORSEN_ShrineProtection.fbx` | Original spiked kite shield with an eye ward; same generator/validator. |
| [Shrine/Echo/WORSEN_ShrineEcho.blend](Echo/WORSEN_ShrineEcho.blend) | `Assets/Art/Shrine/Echo/WORSEN_ShrineEcho.fbx` | Original paired coffin-shaped mirrors repeating a face incision; same generator/validator. |
| [Shrine/Purgatory/WORSEN_ShrinePurgatory.blend](Purgatory/WORSEN_ShrinePurgatory.blend) | `Assets/Art/Shrine/Purgatory/WORSEN_ShrinePurgatory.fbx` | Original crowned cage with a bound, grasping captive; same generator/validator. |

Each source has a sibling `WORSEN_Shrine<Kind>.manifest.json`: measured footprint, height,
triangle count, source/export paths, material palette, bounds and interaction point.
No manifest is required at runtime.

## Reproduce (Blender 5.2, no Unity)

Run from this worktree in Git Bash:

```sh
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/shrine_kinds.py
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/validate_shrine_kinds.py
```

Generator options after `--`: `--only Passage`, `--no-render`. Normal generation renders
front, three-quarter and side views of each kind plus a true-scale lineup. Sources are
saved before preview floors/lights/cameras are created, so only a mesh and the
`InteractionPoint` empty reach source/export. Both slots are geometry-backed; flat normals
and explicit triangles survive export. The validator independently opens each source,
reimports each FBX, checks metre scale, ground pivot, bounds, material coverage and the
interaction point, then compares triangle/material/position semantic hashes. It requires
all 25 PNGs and rejects duplicate geometry. Hashes normalise signed zero and quantise to
0.1 mm; bounds and marker comparisons have a separate 0.1 mm tolerance. Binary timestamps
are not determinism evidence. This is not a Unity importer test.

## Coordinator-only import and wiring

1. Integrate through the normal gate; acquire the lease in the open checkout. This worker
   did not run Unity, touch the shared Library, write `.meta` files or edit the main project.
2. Allow Unity to generate metas for new source and art folders/files.
3. Invoke `Worsen/Shrine/Import and Wire Shrine Models`, or
   `Worsen.Editor.Shrine.ShrineModelSetup.ImportAndWire()`.
   This preflights all exports/manifests, configures static FBX import at scale 1,
   `bakeAxisConversion = false`, no animations, lights, cameras or colliders, imports
   normals, remaps materials to URP/Lit, and assigns all eight references on
   `Assets/Resources/ScriptableObjects/Domain/Shrine/ShrineDriverConfig.asset`.
   Missing per-kind materials are created under `Assets/Art/Shrine/<Kind>/Materials/`.
   Existing config and material identities/tuning survive; model references are explicitly
   rewired to this family. No scenes or prefabs are changed and no setup runs automatically.
4. Run setup a second time. Confirm material/config GUIDs, unchanged tunables and exactly
   eight model references. If Expedition uses an explicit noncanonical `_shrineDriverConfig`
   override, the coordinator must wire its corresponding eight references too; this tool
   deliberately does not search/mutate every config in the project.
5. Run `ShrineDriverModelTests` (13 native cases), `ShrineDriverTests`,
   `ShrineDriverPresenterTests`, `ShrineManagerTests`, `ExpeditionShrineWiringTests`,
   `ShrineWorldRouteTests`, `RunShrineWiringTests`, and architecture conformance.
   `ImportedEightModelsAreWiredAtMetreScaleWithTwoSlotsAndCorrectAxes` intentionally fails
   when setup has not been run. It measures actual Unity bounds and interaction coordinates.
6. Capture an eight-shrine lineup in HorrorRun game lighting, active and spent. Check
   front-facing seals, not just height; each active emission must dim independently on use.
   Confirm moving interaction remains unchanged, colliders stay disabled, rebuild leaves
   no old roots/materials, and unloading clears everything. This visual/live check is pending.

The Driver instantiates at authored scale, not `ShrineDriverConfig.Size` (fallback only).
Authored bodies retain their imported stone colour; `Color` still tunes fallback bodies.
Kind tint, emission and spent settings apply to both paths. Each instance owns exactly two
private materials; shared meshes/materials/models are not changed or destroyed. Missing
models use the existing primitive recipe. Malformed configured models fail closed with
complete cleanup, rather than silently hiding a setup defect. Only Transform, MeshFilter,
MeshRenderer and Collider components are allowed, checked before instantiation; colliders
are disabled below an inactive owned parent before activation. No scripts/lights/Animators/
Rigidbody/audio or other components can introduce prefab side effects.

The interaction marker is a visual/authoring point, not new gameplay. Source front is -Y,
Z up; export settings are -Z forward/Y up with `bake_space_transform=True`. Manifest Unity
interaction is `(0, .30, -.405)` m, relative to the grounded altar origin. The existing
Controller still samples the site pose/envelope. Site rotation is absent from the Core
contract; designated biome rooms and room-facing procedural placement remain separate work.

## Visual review

All 24 source views and the lineup were inspected with the vision tool. The family reads
without text: die/pips, tipped paid scales, quiet bell, tall candle/flame, open portal,
closed shield/ward, doubled face mirrors and captive cage. Common stepped bases and front
seals make the group coherent. The candle drips and cage captive remain visible in dim
studio lighting; the shield and mirrors are deliberately much narrower in side view.
The first side review found unsupported Passage stair treads; stringers and rear feet
were added, regenerated and reviewed. These are Blender Cycles reviews, not game screenshots.
Owner judgement of the horror tone and in-game low-light readability is still required.

Evidence root:
`C:/Users/Hao Guo/Documents/UnityProjects/WORSEN-wt/shrine-art/Logs/AgentValidation/Art/Shrine/`

`lineup.png`, `<Kind>-front.png`, `<Kind>-three-quarter.png`, `<Kind>-side.png`,
`review-1.png` through `review-4.png` (contact sheets), and `validation.json`.

## Provisional authored values

- New serialized references in `ShrineDriverConfig.cs`: `_chanceModel`, `_bargainModel`,
  `_pacificationModel`, `_wickModel`, `_passageModel`, `_protectionModel`, `_echoModel`,
  `_purgatoryModel`, all default null. Setup assigns corresponding FBXs. No new numeric
  runtime tunable, changes to game rules or alterations of existing silhouette values.
- `shrine_kinds.py`: body sRGB `(0.24, 0.27, 0.29)`, material roughness `.82`
  (Unity smoothness `.18`); interaction seal `(0, -.405, .30)` in Blender metres.
- Generator accent colours reuse the eight existing silhouette defaults, in enum order:
  `(.65,.8,1)`, `(1,.65,.25)`, `(.4,.85,.65)`, `(1,.45,.15)`, `(.3,.75,1)`,
  `(.5,.6,1)`, `(.85,.5,1)`, `(1,.25,.3)`; surface emission `.65`, unchanged runtime
  spent multiplier `.12`. Only the DriverConfig tunes runtime tint/emission/dimming.
- Exact geometry dimensions/angles/radii live in the generator's `base` and eight named
  builders; these are authored mesh coordinates, not hidden runtime tunables. Measured
  heights: Chance 1.246421, Bargain 1.48, Pacification 1.125, Wick 1.53, Passage 1.56,
  Protection 1.33, Echo 1.336923, Purgatory 1.47 m (manifest is authoritative).
- Preview-only settings: Cycles 24 samples, AgX, world strength `.20`, area lights
  380/500/65 W at the positions/colours in `studio`; 640x720 per view, 2400x650 lineup.
  Individual orthographic span 1.85 m; lineup spacing 1.17 m and span 10.1 m. No preview
  lights or floor are exported. These light levels are not claims about HorrorRun lighting.

## Verification and known gaps

Blender 5.2.1 validator: all eight source/FBX pairs and 25 previews pass. Full regeneration
repeated with identical semantic geometry hashes, material triangle counts, bounds and
interaction markers for every kind. Meshes range from 668 to 1,360 triangles.

Offline compile `shrine-art-001`: all seven assemblies, 0 errors, baseline warning counts
0/41/0/34/70/0/0. `ast-grep scan`: 0 findings. Pure run `shrine-art-pure-001`:
`PURE_RESULT passed=16 failed=0 environment=30 skipped=0`; no infrastructure errors.
Filter:
`^Worsen[.]Tests[.](Shrine[.](ShrineDriverModelTests|ShrineDriverTests|ShrineDriverPresenterTests|ShrineManagerTests)|Expedition[.](ExpeditionShrineWiringTests|ShrineWorldRouteTests)|Run[.]RunShrineWiringTests)[.]`

The new model fixture has no pure coverage: all 13 cases require native Unity. The other
17 environment cases are the existing Driver (5), Manager (2), Expedition wiring (3),
world-route (4) and Run wiring (3) checks. Existing test expectations were not changed.
Existing dependent fixtures omit explicit ShrineManager.Teardown on assertion-failure
paths. Out-of-scope requests before a broad native suite:

- `Assets/Editor/Tests/Shrine/ShrineManagerTests.cs`,
  `MovingActivationPublishesExactlyOnceWithoutBlocking`: call `manager.Teardown()` in
  `finally` before destroying `root` (the success-path call does not cover failed assertions).
- `Assets/Editor/Tests/Expedition/ExpeditionShrineWiringTests.cs`,
  `PlacementStreamReplaysAndWorldTeardownRemovesObjects`: explicitly tear down both
  constructed ShrineManagers in `finally`, before destroying their owners.
- `Assets/Editor/Tests/Run/RunShrineWiringTests.cs`, `TearDown`: call
  `shrine.Teardown()` if present before the owned-object destruction loop.
- `Assets/Editor/Tests/Expedition/ShrineWorldRouteTests.cs`, `TearDown`: explicitly
  tear down Expedition's retained `_shrines` before destroying the owning objects;
  the individual Passage test cleanup does not cover all fixture cases.

GitNexus: ShrineDriver and ShrineDriverConfig LOW but empty upstream results (not accepted
as no callers); code search confirms ShrineManager.Assemble/Sample/Teardown commands the
Driver, and ExpeditionSessionManager.AssembleShrines/ShrineSetup consume the config.
The unchanged state has LOW risk with ShrineDriver as direct caller. Index refresh is
coordinator-only; no commit, stage or branch operation was performed here.
