# M0 skeleton execution checkpoint

Implemented on 2026-09-14 on branch `codex/boilerplate-m0`. PLAN-001 remains LIVE; later milestones have not been executed.

## Implemented

- Core input, movement probe, noise, run phase and scene-key value types.
- Persistent Input, Run Session, Scene Flow and Debug Overlay systems; two Orchestrators and TagArenaSceneRoot.
- Eight gameplay actions; pure input buffering and overlay formatting; seeded run state and a fixed timestep of 1/60.
- Cinemachine 3.1.7 and Unity-resolved Splines/dependencies.
- TagArena scene, mirrored config/UI assets, and deterministic `Worsen/Scenes/1 — Build TagArena` setup.
- Architecture adoption/roster/lifecycle/reference updates in SPEC-001. Its 11 local Markdown links resolve.
- The obsolete empty test assembly was moved out of Assets into RemovedScaffold here with its original meta.

## Verification evidence

- Unity 6000.3.12f1 connected to this exact project; source compilation completed.
- 70 Edit Mode leaf tests passed, 0 failed, 0 skipped, including 13 ArchitectureConformanceTests. The runner's 93 passed entries include suites and parameterized parents and are not the test count. See [results](editmode-final.json).
- ast-grep: 0 findings. Graph queries and cycle review are detailed in [structural validation](structural-validation.md).
- Current disk index: 4,066 nodes, 8,560 edges, 312 reported flows. All 635 indexed file SHA-256 hashes matched disk; see [hash audit](index-content-validation.json).
- Rebuilding TagArena twice retained its GUID `2d54427b0f84b974f94fabe24c0ea735` and Input config GUID `dbab9bce21b9a474d976ccc58dfc282a`. Existing Scene1 stayed in Build Settings and its scene file was not rewritten.
- Live runtime tick advanced at fixedDeltaTime 0.0166666675. Two repeated loads through SceneFlowManager reset the run and left exactly one Run Session, Scene Flow, Input and Debug Overlay Manager. Both routing components also remained single.
- Live readiness and reload checks exposed and fixed pre-readiness input leakage and surviving duplicate Session components. The input regression has a pure-data test; repeated real scene loads verified the bootstrap fix.
- Injecting a short tap at the live input buffer boundary, publishing through Driver -> Manager -> InputOrchestrator -> Run, yielded pressed=Jump, released=Jump, held=None. One fixed tick cleared both transient edges.
- Disabling and re-enabling the actual UIDocument recreated its root and rebound the labels. The tick label exactly matched the run (6678 during the final read), phase FirstSweep. Speed/movement explicitly await M1.
- [Final rendered screenshot](tagarena-rendered-final.png) was visually inspected: scene and UI render correctly. Synaptic's immediate texture capture returned a black image; Unity's deferred CaptureScreenshot produced the actual rendered view.
- Final filtered project console: 0 errors and warnings (Synaptic/tool messages excluded).

## Limits and restored state

- Hardware keyboard/gamepad input was not manually exercised. Attempts to inject device events from the editor tool did not establish gameplay action delivery: the tool executed in Editor input/focus context. This is not counted as a passed device test. Pure buffer tests and live buffer-to-run wiring passed.
- Saved graph query 5 falsely reports RunSessionController because it only accepts references directly to Class nodes. The graph contains 24 test calls to its owned Constructor/Methods; a member-aware follow-up returns zero untested types. The original query remains unchanged.
- GitNexus MCP retained an old snapshot; final queries used a fresh CLI process. Its status command labels every dirty tree stale; direct content hashes establish index freshness here.
- Process extraction reports bounded/truncated flow enumeration in this vendor-heavy repository. An absent process is not proof of no dependency.
- detect_changes does not include the untracked M0 source files; its low-risk/zero-symbol result is not accepted as impact evidence.
- Unity is back in Edit Mode, unpaused, with TagArena loaded and clean. Temporary background execution was restored to false. Input settings were restored to ResetAndDisableNonBackgroundDevices and PointersAndKeyboardsRespectGameViewFocus. The temporary keyboard device was removed.
- No commit/staging was performed. The repository already contained extensive unrelated dirty/untracked work; it was preserved.
- No docs-plans lifecycle operation or codebase-documentation pass was run in this execution turn. A later documentation reconciliation can use this checkpoint as implementation evidence.

Next implementation milestone: M1 Player movement; no player or chase behavior is claimed by M0.
