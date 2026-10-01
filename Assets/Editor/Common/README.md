# Shared editor SetupKit

Coordinator entry point: `Worsen/Setup/Rebuild All`, or
`Worsen.Editor.Setup.RebuildAllSetup.RebuildAll()` for a structured `SetupReport`.
Explicit invocation only, under the coordinator's Unity lease. No worker starts
Unity. The aggregate does not run inside an active test run.

## Rebuild order and dependency reasons

The executable manifest is `../Setup/RebuildAllSetup.cs:CreateManifest`.

1. Admission: reject busy editor, active tests, missing HunterBody, dirty scenes or assets before writes.
2. Base configs: create missing Progression/Procedural assets needed by migrations.
3. Player effects: supply the PlayerManager Resources fallback.
4. Player art and prefab: import character/arms before prefab consumers.
5. Hunter base: restore the base prefab on the coordinator-provisioned HunterBody layer.
6. Chase: supply chase tuning to both legacy and horror scenes.
7. Floor: create the base Floor visuals copied by HorrorRun.
8. Director: supply pacing tuning to FloorLoop/HorrorRun.
9. Camera: supply tuning and retained hand material.
10. PostFX: supply view tuning before scene service assembly.
11. Telemetry: supply capture config before scene service assembly.
12. Audio base: supply original samples and the legacy config before soundscape wiring.
13. HUD: restore UI panel/tree references before scene assembly.
14. Results: restore UI panel/tree references before scene assembly.
15. Procedural content: publish catalogue/kit data using an explicit config, never editor selection.
16. Shop and catalogue: bind economy and catalogue after Progression exists.
17. Shrine: bind placement, appearance, progression and spawn configuration.
18. TagArena: build legacy scene and shared Input/DebugOverlay prerequisites.
19. FloorLoop: build the legacy floor fixture from base assets.
20. HorrorRun: existing builder composes expansion, hunter roster, soundscape/mixer, world, renderer features, fog and menu/settings; promotes the title scene last.
21. Hunter roster audio: apply reviewed selections after soundscape generation.
22. Final provenance: update all three scenes after the last shared config mutation.

Nested existing setup calls are retained intentionally: standalone scene menus
must still be self-healing. Diagnostic capture/build tools, the FogSpike demo,
windows and obsolete standalone light-template authoring are not production
rebuild steps. Missing required vendor/art inputs fail through existing builders;
there is no download, substitute content or suppressed error.

`SetupSequence` validates the manifest before executing it and records every step
as Succeeded, Failed or NotRun. The aggregate promotes Unity error/assert/exception
logs to step failure. It stops after the failing step, but does not roll back
already saved assets. The report preserves the exception and the completed prefix.

## Shared helpers and compatibility

`SetupKit` owns folder creation and checked scalar/array object-reference wiring.
Thin existing adapters preserve their signatures, dirty/save policies and callers.
Accessible serialized fields use `nameof`; private runtime fields use checked
string paths. No runtime fields were renamed, so no FormerlySerializedAs migration
is needed. This extraction does not rewrite every unrelated scalar FindProperty
call in the editor tree.

`SceneFingerprint` uses normalized runtime source, not git HEAD: only `.cs` files
beneath `Assets/Scripts`, slash-normalized paths in ordinal order, LF line endings,
and trimmed trailing whitespace on each line. Stamps remain `sha256:` followed by
64 uppercase hex digits. Editor/test changes do not enter the source hash, while
uncommitted runtime edits still do. Configuration hashing deliberately retains the
legacy contract. All three builders, legacy play gates and byte-only provenance
refresh call the shared implementation. The first coordinator refresh is necessary
after this algorithm change; no scene bytes are edited in the worker checkout.

## Verification boundaries

`SceneFingerprintTests` and `SetupSequenceTests` are pure. `SetupKitTests` compares
legacy versus migrated serialized JSON on the same in-memory objects for every
migrated Wire adapter, checks array replacement, optional-null compatibility,
missing/wrong fields and folder GUID retention. This is helper-level equivalence,
not a captured pre/post full-project rebuild comparison. Full produced-asset parity
still needs coordinator Unity evidence.

`SetupReferenceAuditTests` is read-only after setup. It walks generated config/UI/
kit/prefab roots for dangling references and missing scripts, including inactive
children and nested serialized entries. Every authored Worsen reference must be
non-null; null native-engine bookkeeping (such as prefab ancestry) is not an
authored binding. A negative-control test proves a cleared PlayerProfile binding
is rejected and its resolved counterpart accepted. Required Player/Floor outputs
and the Player effect Resources fallback must also exist.

This strict test is expected to expose pre-existing intentional nulls: world setup
explicitly clears EnvironmentDriverConfig._lightTemplate, PlayerManager permits
an unassigned _effectConfig with a Resources fallback, and absent optional kit art
can produce null prefab slots. No exceptions or skips hide those findings. Making
all such references non-null would change generated output, so asset-parity and
strict-audit acceptance remain blocked on a coordinator/owner decision. The worker
has not run Unity and does not claim these audits pass.

## Out-of-scope follow-up

- `Assets/Editor/EnvironmentLighting/HorrorEnvironmentLightSetup.cs`,
  `EnsureFolder(string)`: delegate to `Worsen.Editor.Common.SetupKit.EnsureFolder`.
  EnvironmentLighting is not the owned Environment directory.
- `Assets/Editor/Hunter/**`: migrate the Hunter worker's Wire/folder implementations
  later, as requested. Their current APIs remain callable by the aggregate.
- Coordinator: generate .meta files via normal Unity import; run aggregate,
  reference/parity fixtures and legacy play-entry checks; record full asset parity.
