# Exit art inventory and coordinator hand-off

## Inventory

| Editable source | Runtime exports | Provenance |
|---|---|---|
| `WeatheredDoor/WORSEN_WeatheredExitDoor.blend` | `Assets/Art/Exit/WeatheredDoor/WORSEN_WeatheredExitDoor.fbx`, `ExitPaint.png` | Original geometry and seeded painted-wood texture from `tools/blender/exit_door.py`; no third-party content. Packed source texture. |

The parent `ArtSource/README.md` inventory is outside this worker's ownership. Coordinator: add this row there.

## Import and wiring required (not performed by this worker)

1. Import FBX with `bakeAxisConversion = false`, metre scale, no generated mesh colliders, no animation. Preserve names and origins. The runtime contract is an XY aperture 2 x 3 m; local -Z is the front. `DoorLeaf` must be at (-1,0,0), with a local bounding centre of (1,1.5,0). Verify these in Unity; Blender round-trip is not Unity import proof.
2. In `Assets/Editor/Horror/HorrorWorldAssetSetup.cs`, replace `BuildExitLeaf`'s castle leaf output with a deterministic prefab containing the complete exported assembly. Keep `DoorFrame`, `DoorLeaf`, `Threshold`, `EscapeSurface`. No facade, second leaf or surrounding backdrop. Do not use the old 90-degree prefab yaw.
3. Map `Exit_EscapeSurface` to a persistent material using `Worsen/ExitPortal`. Map other slots to URP Lit; `Exit_PatinatedPaint` uses `ExitPaint.png` with white tint, roughness .78 (smoothness .22). Preserve exported colors for edges, brass and threshold; brass metallic .65. This serialized material reference retains the shader in builds. Never add runtime Shader.Find.
4. `HorrorWorldAssetSetup.Configure`: `_usePhysicalExitDoor = true`, `_exitDoorPrefab = <whole new assembly>`, `_exitDoorPrefabYaw = 0`. Runtime deliberately ignores legacy prefab yaw for this canonical assembly. Existing opening defaults remain 1.2 s, 100 degrees, crossing hysteresis .35 m. Suggested `_exitOpenColor = (1,.64,.32,1)` to match the warm escape instead of existing green; retain the existing Lumen prefab and brightness policy. No new DriverConfig schema is needed.
5. `Assets/Scripts/Domain/Procedural/Controller/ProceduralExitHubUtility.cs`, `TrySelect`: reserve the full single-leaf swing envelope (canonical x [-1.48,1.2], z [-1.97,.2], height 3.21 m) plus player clearance. Arrange approach on the door's -Z side using `FloorDriverConfig.ExitDoorYaw` in the floor assembly; never orient the front into an inaccessible route.
6. `FloorController.Initialize/Collect/UpdateExitLock` remains owned by the floor worker: every real cake, not a subset or Blind Faith double-credit shortcut, must precede the existing exit-open fact; last cake starts the fast collapse and the exit room stays protected. This checkout still has old subset/Greedy Door rules. This worker does not change those facts or contracts.
7. Keep the existing audio route: opening progress -> Floor display -> `AudioOrchestrator.OnFloorDisplay` -> `AudioFeedbackPresenter.Exit` -> `CueId.DoorOpen`. The existing bank includes `Assets/Audio/Horror/Expansion/WORSEN_door_01.wav` and `WORSEN_door_02.wav`; no duplicate AudioSource is created. Preferred named clip is `WORSEN_door_01.wav`; the current bank intentionally varies between both. Do not claim audible verification until the native run.

Missing assembly wiring intentionally leaves the detailed paneled compatibility fallback, without an escape shader. An authored assembly with a wrong portal material logs an error and keeps that surface disabled, not a magenta or two-sided replacement. Integration is not complete until steps 1-4 run. The old castle leaf is never instantiated by the new physical exit. Legacy `FloorExitVolume` and `FloorDriver`'s nonphysical marker branch remain unchanged for FloorLoop compatibility; target HorrorRun uses the physical branch.

## Technique

One opaque single-sided quad exactly bounds the aperture. Backface culling plus an explicit camera half-space clip reject rear views; depth testing/writing retain foreground occlusion and hide the local room inside the aperture. The leaf swings toward the room so it stays in front of this surface. A procedural view-ray sky, sun and distant field produce the escape without a texture, second camera, render target, stencil allocation or scene render. The rectangle itself is the mask; using a shared stencil bit adds cost and conflicts without improving this rectangular opening. There is no 3D destination to walk around. The escape uses no fog or shadow caster pass.

The shader is URP-only. Offline C# compile does not compile HLSL. Native forward/deferred and fog/post-process interactions remain unverified.

## Provisional authoring values

- `ExitPortal.shader`: zenith (.18,.38,.62), horizon (1,.64,.32), field (.18,.24,.095), sun HDR (3,2.2,1.1), sun direction (.22,.12,1), angular radius .035, exposure 1.25. Material properties, not runtime magic tunables.
- `exit_door.py`: 2 x 3 m aperture; 2.4 x 3.21 m total frame; hinge (-1,0,0); slab 1.98 x 2.96 m; .16 m runtime slab collision (handles extend to .325 m); .04 m threshold; portal Z=.11. These are the versioned geometry contract used by fallback/collision/validator, scaled by existing ExitSize.
- Generator palette: paint (.49,.60,.53), bare wood (.19,.125,.071), edge (.28,.20,.12), brass (.28,.17,.055), stone (.22,.235,.225); roughness .78, brass metallic .65. Seed 1901; 256 x 512 paint image, 45 chip ellipses, .012 m default two-segment bevel. Timeless style shared by all themes.
- Existing 1.2 s/100-degree opening, .35 m crossing distance and Lumen .3 -> 2.5 brightness are retained, not new tunables.

## Evidence and native gates

`Logs/AgentValidation/Art/ExitDoor/validation.json` contains independent source/reimport measurements and SHA-256 hashes. Both have 7,670 triangles, one leaf and one aperture quad. Generator creates `closed-front.png`, `open-front.png`, `open-back.png` and `swing-strip.png`; inspected with vision. The strip exposed rear-swing occlusion by the portal; the delivered forward swing fixes it. Final images show a chipped pale-green single door, bright dusk/hill/sun only in the opening and no backdrop from behind. Blender's CPU shader reference is art evidence, not native URP proof.

Coordinator Unity checks:
- Open-door screenshot from the front (visible escape, leaf occlusion, warm Lumen spill).
- Open-door screenshot from behind and grazing side (ordinary room, no sky card, no shadows cast by aperture).
- Closed screenshot, five-frame native opening strip, same in castle/hospital/school/basement.
- `FloorFreestandingExitTests.ImportedSingleLeafKeepsItsHingeFitAndRevealsAFixedAperture`: imported orientation, hinge/collider fit, portal enable/reset and teardown.
- `FloorExitDoorIntegrationTests`: closed collision, 1.2-second swing, one-shot contact, reverse/around rejection and regeneration.
- `FloorLumenIntegrationTests`, `FloorLifecycleTests`, `FloorLoopIntegrationTests`, `SetupReferenceAuditTests`; run audio physical-opening case in `AudioWorldMixPresenterTests` (its setup needs ScriptableObject native support).
- Race at maximum sprint speed through the opening, around both jambs, from behind, while locked and while opening; confirm only fully-open front-to-back crossing completes. Trigger sampling is discrete, not a swept teleport detector.
- Collect every cake in live HorrorRun: exactly one open fact and opening sound, immediate fast collapse outside the exit, no collapse inside it. The dependent floor-worker changes are required.

No Unity process, lease, HTTP control, scene/prefab/config edit, Library write, staging or commit was performed here.
