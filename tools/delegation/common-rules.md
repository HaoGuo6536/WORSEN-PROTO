## Working environment

- **Execution root:** the isolated Git worktree named above (`WORKTREE`), a checkout of the WORSEN-PROTO Unity 6.3 project (C#) on its own branch. Edit files only inside it.
- The main project `C:/Users/Hao Guo/Documents/UnityProjects/WORSEN-PROTO` is open in the Unity Editor and shared. **Never edit, create or delete anything there**; only the read-only GitNexus commands below run there.
- `WORKTREE/Library` is a junction to the main project's Unity `Library`. **Never modify, clean or delete anything under `Library`.**
- **Do not run Unity** in any form: no Unity.exe or Unity CLI, no Synaptic HTTP (localhost:8086), no Unity MCP tools, no Unity test lease. The coordinator runs Unity after integration.
- Ignore `Assets/TutorialInfo/Icons/URP.png` (a known Git LFS pointer mismatch). Never hand-write `.meta` files.
- Do not edit `ProjectSettings/`, `Packages/`, scenes (`*.unity`), prefabs, `PLANNING/`, `CLAUDE.md`, `AGENTS.md`, `tools/hooks/`, `tools/integration/` or `tools/delegation/` unless the task's **Owned files** list names them.
- **Scope is enforced.** The coordinator's accept step rejects any changed path outside the task's Owned files. If you need a change elsewhere (for example a `using` line or one-line call in another system), do not make it; list it under Requests with the exact file, symbol and change, and finish the rest.
- **Git boundary:** do not commit, stage, branch, switch, stash, reset, push, or add or remove worktrees. **No recursive delegation:** do not run `hermes`, spawn agents or call other models.
- No network access beyond the listed commands. Do not download packages or assets.

## Project rules

- Architecture authority: `WORKTREE/PLANNING/specs/SPEC-001-project-architecture-guidelines.md`. Read §0 (mandatory script header), §2 (inject time and randomness; no `Time.*`/`UnityEngine.Random` in logic), §6–§9 (layers, routing, lifecycle, subscription pairing), §12 (folders and namespaces) and §13 (gates) before editing.
- Layers: `Core` references nothing in the project; `Domain` references Core (plus Lumen runtime); `Session` references Core and Domain; `Presentation` references Core only; `Orchestrator` may reference all. Never add assembly references or relax a rule to make code compile.
- Every new or changed script keeps the §0 header. Keep KEY RESPONSIBILITIES truthful and focused (at most five bullets; more means the script needs splitting or a debt entry).
- Match surrounding style: immutable `readonly struct` payloads in Core; Controllers and Presenters compute; BehaviorState/DriverState hold data; Config ScriptableObjects hold designer values as serialized fields with provisional defaults. List every provisional value in the report.
- Visual output: you have an image-viewing tool (vision). Whenever you change something visual (a Blender model or animation, a shader, a material, a layout), render it headlessly (Blender, or a script that writes a PNG) and **look at the image** before reporting. For animation, check a strip of frames across each clip. Report what you saw. A validator passing is not visual evidence.
- Tests: add or extend NUnit Edit Mode fixtures under `WORKTREE/Assets/Editor/Tests/<System>/`, preferring pure-logic tests. Each new pure-layer script (Controller, Presenter, Utility) ships a `<Name>Tests.cs`. Every test fixture class carries `[Worsen.Tests.Infrastructure.FixtureTimeGuard]` (a test enforces it). Tag tests that need the Unity window in focus with `[Category("RequiresFocus")]`. A fixture that enters Play Mode (`EnterPlayMode`, `[UnityTest]` scene loads) needs `Timeout(300000)` or more: each Play Mode entry costs about 100 s of domain reload here, and batch 13 lost 23 tests to a 30 s timeout. Edit Mode never calls `OnDestroy` (or `Awake`/`OnEnable`) on runtime MonoBehaviours, so a fixture that builds through a Manager or Driver must call its `Teardown()` in `finally` before destroying the owner; otherwise its NavMesh, links and native objects stay registered and break later fixtures (batch 19 lost seven procedural tests to a leaked bake at a shared origin).
- Owner decisions override older spec text when the task quotes them.
- Name requested Unity coverage as full fixture names: `Worsen.Tests.<System>.<FixtureClass>` (for example `Worsen.Tests.Player.PlayerControllerTests`), one per line or as a JSON string array. Do not give filenames, namespace prefixes, regexes or individual method names; the coordinator's selector/native runner consumes fixture names. Workers still never run Unity themselves.

## Required checks before you finish

1. **Impact analysis** before editing any existing class, method or function (read-only, from the main project root):
   `cd "C:/Users/Hao Guo/Documents/UnityProjects/WORSEN-PROTO" && node .gitnexus/run.cjs impact "<SymbolName>" --direction upstream --repo WORSEN-PROTO`
   Record risk and direct callers. HIGH or CRITICAL: stay strictly in scope and say so. UNKNOWN or empty is not proof of no callers; confirm with `rg`. The index may lag your base; `rg` is the tie-breaker.
2. **Dependent tests (you cannot run Unity, so read them):** for every public symbol whose behaviour you changed, run `rg -l "<Symbol>" WORKTREE/Assets/Editor/Tests` and read those fixtures. Update expectations only when your in-scope change makes them wrong; list every such test and why. Name the tests most likely to fail in Unity under Open issues.
3. **Compile (must pass):**
   `powershell -NoProfile -File "C:/Users/Hao Guo/Documents/UnityProjects/WORSEN-PROTO/tools/integration/compile.ps1" -Worktree "WORKTREE" -RunName <task>-<nnn>`
   New run name each time. Required: 0 errors in all seven assemblies and no new warnings (the script compares with the baseline).
4. **Headless pure tests** (when `WORKTREE/tools/offline-compile/Run-PureTests.ps1` exists): after a clean compile, run
   `powershell -NoProfile -File "WORKTREE/tools/offline-compile/Run-PureTests.ps1" -Worktree "WORKTREE" -CompileRun <your-compile-run> -RunName <task>-pure-<nnn> -Filter '<anchored regex of the fixtures you added, touched or depend on>'`
   (example: `^Worsen[.]Tests[.]Player[.](PlayerControllerTests|PlayerMoverPresenterTests)[.]`).
   - It never starts Unity. Report the `PURE_RESULT` line and the filter.
   - Any pure failure, timeout or harness error blocks hand-off: fix it.
   - Environment and skipped cases are not passes. List them for the coordinator's Unity run, and report a selection with no pure cases as "no pure coverage".
   - Never weaken assertions or add skips to get a green headless result.
5. **Lint:** `ast-grep scan` from `WORKTREE`. Required: 0 findings.

## Final message format

Keep it short; the coordinator reads the diff itself. Sections, in order:

1. `## Summary` — a few sentences.
2. `## Files changed` — each file with a one-line reason.
3. `## Impact analysis` — symbol, risk, direct callers.
4. `## Tests` — tests added; existing tests changed and why; tests likely to fail in Unity.
5. `## Provisional values` — each new tunable, default, location.
6. `## Requests to other owners` — exact file, symbol and change you needed but did not make.
7. `## Verification` — the compile summary lines and the ast-grep result.
8. `## Open issues` — unfinished, uncertain, or needing Unity or owner judgement.
9. `## Diff Report` — output of `git status --short` and `git diff --stat` only, plus the line count of each new file (`wc -l`). Do not paste diffs or file contents.

## Art sources (owner rule)

Never save a `.blend` under `Assets/` or the repository root. Editable Blender sources live in `WORKTREE/ArtSource/<Area>/<Asset>/<AssetName>.blend` (see `WORKTREE/ArtSource/README.md`; add each new source to its inventory); only exported FBX and textures go to `Assets/Art/<Area>/<Asset>/`. Use Blender 5.2 headless (`"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1`). Export FBX with -Z forward, Y up and apply transform; Unity importers keep `bakeAxisConversion = false` for these exports.
