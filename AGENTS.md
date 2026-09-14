# WORSEN project agent guide

Read this file before working in the repository. It connects project policy, planning, documentation, and verification; keep the detailed rules in their owning documents. User instructions take precedence over this guide. Read any more specific `AGENTS.md` in the area being changed as well.

## Read the right authority

| Source | Responsibility | When to read |
| --- | --- | --- |
| [SPEC-001 — Project architecture guidelines](PLANNING/specs/SPEC-001-project-architecture-guidelines.md) | Normative script taxonomy, layer dependencies, lifecycle, placement, and engineering gates. Stable section numbers are referenced by enforcement tools. | Before adding or changing project code, assembly references, or asset wiring; read the relevant sections in full. |
| `DOCUMENTATION/direction.md`, when present | Owner-established goals, priorities, Now/Next/Later, and evidence of completion. | At task start when the work relates to project direction. |
| [PLANNING registry](PLANNING/index.md) and its linked `LIVE` specs/plans | Design decisions, executable plans, their status, and archived history. | Before planning or implementing the affected feature. |
| `DOCUMENTATION/HIGHLEVEL/`, `LOWLEVEL/`, and `SOURCE/`, when present | Descriptions of current implementation, dependencies, integration gaps, and fallbacks. | Read the relevant overview, then follow links to the affected source. Verify claims against code and Unity. |
| GitNexus index and generated block below | Descriptive navigation and dependency evidence. | For code exploration, impact analysis, and verification. A generated map does not override architecture policy. |

Do not treat a draft, archived plan, old audit note, or a generated symbol count as proof of current authority or a passing check. Code and observed Unity state establish what works today; plans establish intent. Record disagreements instead of silently rewriting either to fit the other. Resolve a conflict with the user's existing decisions where possible; ask only when an unresolved decision changes the implementation.

The architecture authority is SPEC-001 at the linked path, migrated from `Assets/Scripts/PROJECT_ARCHITECTURE_GUIDELINES.md`. Existing enforcement labels using that filename refer to SPEC-001; its section numbers are unchanged. Follow the registry to any future live successor and keep one authoritative copy. The [boilerplate plan (PLAN-001)](PLANNING/plans/PLAN-001-worsen-boilerplate.md) and [game design summary (SPEC-002)](PLANNING/specs/SPEC-002-worsen-game-design.md) were registered `LIVE` by the migration defaults. Initialization did not execute the plan or verify its completion; verify execution approval and remaining work before implementing it. SPEC-002 links the unchanged Word source and records its unresolved design questions.

## Compose the documentation skills

Discover skills from the active session catalog and read their `SKILL.md` before using them. The personal installations are under `$CODEX_HOME/skills/<name>/SKILL.md`, defaulting to `~/.codex/skills/<name>/SKILL.md`. Use the installed files and their referenced templates rather than copying their full instructions here. If a required skill is unavailable, report it instead of silently inventing its workflow.

Both skills are **manual-only**. Reading existing project documents for context is normal task work. Running either skill requires the current user request to invoke it or explicitly ask to apply it. This guide does not automatically start documentation passes, initialize folders, migrate documents, change plan status, or archive work. An invocation does not carry into later turns.

| Skill | Owns | How it fits with the other skill |
| --- | --- | --- |
| `$docs-plans` | Root-level `PLANNING/`: the registry, specs, plans, and archives. Records intended work and its lifecycle. | Link live plans to the direction items they serve. It reads direction but does not edit `DOCUMENTATION/`; status changes produce a hand-off to `$codebase-documentation`. |
| `$codebase-documentation` | Root-level `DOCUMENTATION/`: direction, HIGHLEVEL, LOWLEVEL, SOURCE, glossary, and attribution. Records verified implementation and alignment. | Reconcile direction against implementation evidence and registered plans. Update existing documentation in place; flag unwired features and fallback behavior explicitly. |

Keep `PLANNING/` and `DOCUMENTATION/` as siblings at the project root, outside `Assets/`. Do not initialize or migrate them merely to satisfy a missing link in this guide.

For a request invoking both skills, read existing direction and the plan registry first. Use `$docs-plans` for the requested planning operation, then `$codebase-documentation` for the requested documentation reconciliation and direction hand-off. Each skill edits its own artifacts. If only one is invoked, report the hand-off needed by the other instead of running it implicitly. These operations do not themselves authorize implementation of an unapproved plan.

Preserve the planning lifecycle: `DRAFT` is not approved execution; `LIVE` identifies current authority or approved work; `COMPLETED` requires evidence that a plan's exit criteria were met; `SUPERSEDED` names a successor; `CANCELLED` records a reason. Specs are never `COMPLETED`. Terminal documents move to the appropriate archive and retain one registry entry. Follow the skill's exact transitions and templates.

GitNexus-generated plans are an explicit exception to normal placement: keep `docs/plans/*gitnexus-plan*.md` unchanged and at the original path while draft or live. `$docs-plans` registers them in place; identity and status live in the registry. Do not edit their body, add front matter, or rename them. Archive only after terminal status, preserving the filename. When using `gitnexus-work`, select the exact approved plan instead of relying on the newest-file default.

The documentation skill excludes test-only material by default. That controls what gets documented, **not whether engineering tests run**. Keep its required integration/fallback callouts, relative links, attribution, and rendered visual checks for changed PlantUML diagrams. Use `explain-clearly`, when available, for concise explanations and consistent acronym expansion; preserve the artifact structures required by the owning skills.

## Parallel work and Unity testing admission

For boilerplate execution, start with the [parallel map in PLAN-001](PLANNING/plans/PLAN-001-worsen-boilerplate.md#parallel-execution-map) and [PLAN-002 coordination checkpoints](PLANNING/plans/PLAN-002-parallel-coordination.md). Use the assigned physical child plan and its exact owned paths. LIVE means approved scope, not that its dependencies are ready. One worker owns each system/file; only the coordinator edits shared Core/Session contracts, Orchestrators, scene setup, assemblies, package/project settings, architecture or registry. Submit shared changes to that owner. Do not switch branches or stage unrelated work in a shared checkout. A new worktree from HEAD does not include this repository's uncommitted/untracked M0 baseline; verify the baseline before delegating.

**Before Unity testing or any operation that could interrupt another agent's tests, acquire the [exclusive Unity lease](tools/coordination/README.md) with [UnityTestLease.ps1](tools/coordination/UnityTestLease.ps1).** This includes Play Mode, test/build runs, scene/prefab/setup changes, navigation baking, package changes, refresh/imports, and saving files under Assets, Packages or ProjectSettings in a checkout currently open in Unity. Hold publication leases until the resulting imports/compilation settle. An idle snapshot or a free Status response is not ownership; Acquire must succeed and return your token.

After acquiring, verify the intended editor/project, no foreign Play Mode/test/build operation, no import/compilation in progress, and any dirty scene state. If busy or unreadable, do not begin: retain the lease and coordinate with the active operator until actual state is known and idle. Assert the token before each mutation; heartbeat between operations. Use plan/token-specific logs, wait for actual completed test results and inspect failures/counts. Release only once the owned operation is finished and the editor is idle; a tool timeout does not prove it stopped. Never steal an old lease, clear another owner's record, or force-stop their editor merely to gain access. Follow the guide's verified manual recovery process if an owner is unavailable.

While another owner holds the lease, continue read-only work, prepare patches outside Unity-imported paths, or edit an isolated checkout that is not open in Unity. Do not publish to the tested checkout: automatic import can invalidate the run even when paths have different feature owners. Before the first test window, the coordinator obtains pause/protocol acknowledgments from already-active writers. The lease is shared through Git's common directory across worktrees; Unity operations for this repository are serialized. Manual user actions and noncompliant tools remain outside the lock, so inspect actual state and report interference. Read-only document checks and lint may run independently against a stable snapshot.

## Apply the architecture before implementation

Classify each changed script by taxonomy role, system, and layer before editing. The architecture document is authoritative for details and exceptions. The dependency graph is not a linear stack: `Core` has no project-layer dependencies; `Domain` references Core; `Session` references Core and Domain; `Presentation` references Core only; `Orchestrator` may reference all runtime layers. Domain-to-Domain dependencies must follow a declared acyclic order.

Within a system, the Manager owns and connects the logic and presentation stacks. Controllers and Presenters compute; BehaviorState and DriverState hold runtime data; Config and content ScriptableObjects hold designer data; Drivers perform engine interactions and report events to their owning Manager. A Domain system may have its own Driver stack without depending on the separate Presentation layer. Upward cross-layer communication uses events routed by Orchestrators, not forbidden assembly references.

Follow the mandatory script header (§0), time/randomness injection (§2), lifecycle and event pairing (§8–§9), and folder/namespace rules (§12). Put project editor tools in `Assets/Editor/[System]/`, tests in `Assets/Editor/Tests/[System]/`, and editor menu entries under `Worsen/[System or Group]/...`. Preserve Unity `.meta` identities and serialized references when moving assets. Prefer Unity asset operations for moves and scene/prefab wiring. Where wiring must survive re-imports, provide the deterministic setup tool required by §10.

Do not relax assembly references, delete lint rules, ignore conformance tests, or hide warnings to make a change pass. A needed policy amendment belongs in the architecture document and the corresponding enforcement checks, with a stated reason. Vendor code, including `Assets/Synaptic AI Pro/`, is outside the project's architecture conformance scope.

## Select tools by the evidence needed

Model Context Protocol (MCP) tools operate connected services or the running editor; command-line interface (CLI) tools operate files, indexes, or separate processes. Check which tools are actually available before choosing a path.

| Need | Tool | Required practice |
| --- | --- | --- |
| Locate files, exact identifiers, or configuration | `rg --files`, `rg -n` | Scope searches to the relevant project paths. Exclude generated caches and vendor trees unless investigating them. Text matches supplement graph analysis. |
| Find C# syntax patterns or enforce local structural rules | ast-grep | Use `ast-grep run` for structural search and `ast-grep scan` with [sgconfig.yml](sgconfig.yml) and [project rules](tools/ast-grep/rules). It does not establish cross-file callers or scene wiring. |
| Trace execution and dependencies | GitNexus MCP | Start with `query`, then `context` on relevant symbols. Run upstream `impact` before editing existing functions, classes, methods, or affected indexed files. Include the repository name `WORSEN-PROTO`. |
| Inspect or edit live Unity objects, assets, console, or tests | Synaptic Unity MCP (`unity-synaptic`) | Verify the connected project and inspect the target first. Discover the actual tools and parameters before execution. |
| Reproduce compilation, tests, or an existing build/setup method | Unity Editor CLI | Use the Editor version in [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt), explicit project/output paths, and inspect logs plus test results. |

### GitNexus workflow

Use the relevant project [GitNexus skills](.claude/skills) for exploration, impact analysis, debugging, refactoring, and CLI work. Review direct callers, affected execution flows, and risk before editing. Warn before proceeding on `HIGH` or `CRITICAL`. `UNKNOWN`, missing targets, partial results, or truncated results are unresolved evidence, never proof of no impact. Disambiguate targets, page results, check text/structural references, and inspect Unity serialized or runtime wiring that a static graph may not capture. For instruction-only Markdown that has no graph target, report that limit and check document references directly; do not invent a code symbol to obtain a clean verdict.

Prefer MCP for queries. The local runner is the CLI fallback; run from the repository root and check `--help` for the installed version:

```powershell
node .gitnexus/run.cjs status
node .gitnexus/run.cjs query 'scene initialization' --repo WORSEN-PROTO
node .gitnexus/run.cjs context 'ActualSymbolName' --repo WORSEN-PROTO
node .gitnexus/run.cjs impact 'ActualSymbolName' --direction upstream --repo WORSEN-PROTO
node .gitnexus/run.cjs analyze --index-only
node .gitnexus/run.cjs detect-changes --scope all --repo WORSEN-PROTO
```

Replace placeholder symbols with actual targets. If the runner is absent, follow the [CLI skill](.claude/skills/gitnexus-cli/SKILL.md) to bootstrap it. Refresh the index after changes as required by architecture §13c; `--index-only` preserves manually maintained agent instructions and skips skill-file injection. Request `--pdg` only when control/data-dependence analysis needs it; an index without that layer cannot answer those questions.

Before a commit, use `detect_changes` and resolve incomplete results. For architecture verification, read the live graph schema, then run the saved checks in [tools/gitnexus/queries](tools/gitnexus/queries/README.md). Query 4 requires human/agent review of the reported method, as its README explains. Do not interpret an empty query against the wrong schema as a passing check. Use GitNexus rename support for code symbols, then verify serialized Unity references separately.

### ast-grep workflow

The rule files cite the architecture sections they enforce. These are PowerShell examples; single quotes keep pattern variables literal:

```powershell
ast-grep --version
ast-grep run --lang csharp --pattern '$OWNER.Instance' Assets/Scripts
ast-grep scan
ast-grep scan --json
```

Check syntax-tree node names with `ast-grep run --debug-query` when updating rules or parsers. Review structural rewrites before applying them; they do not replace GitNexus impact or coordinated symbol renaming. The [pre-commit hook](tools/hooks/pre-commit) currently skips lint when ast-grep is absent: report that as a skipped gate, not a clean result. A committed hook file also does not prove `core.hooksPath` is configured; check with `git config --get core.hooksPath` when relying on it.

### Synaptic Unity MCP workflow

This project uses Synaptic's `unity-synaptic` server. Discover its exposed tools rather than assuming a generic `unityMCP` HTTP endpoint. A server listing tools proves server connectivity only; a successful read from the intended Unity project proves the editor connection. Check the project identity (the Assets path if necessary) and scene before mutations, especially with multiple editors open.

Use `inspect` for scene, hierarchy, object, component, or project information. Use `list_categories`, `search_tools`, `list_tools`, or a scoped `get_tools_reference` to discover operations, then call `execute` with a verified tool name and parameters. Dedicated `create`/`modify` tools are available for supported object work. A read-only probe is `inspect({target: "scene", depth: 1})`; it must return scene data before being reported as successful.

Use `run_csharp` only when no dedicated operation covers the task, and return explicit evidence from the snippet. Runtime snippets are not persistent project implementation. Durable behavior belongs in source or deterministic setup tools under the architecture rules. Preserve user edits, inspect dirty scene state, save only intended changes, and re-read objects/assets after mutations. Allow compilation and domain reload to settle before checking the console or running tests.

Synaptic exposes `unity_run_tests` in the `Debug` category; inspect its current schema for operations and filters. Wait for completed results and report failures/counts, not just that a run started. If a tool times out, reports disconnected Unity, or returns a failure in its content, state that failure and retry only after checking connection state. Do not treat a tool-discovery response as a passed Unity test.

### Unity CLI workflow

Resolve the installed `Unity.exe` matching `ProjectSettings/ProjectVersion.txt`; on this machine the standard Hub location is `C:/Program Files/Unity/Hub/Editor/<version>/Editor/Unity.exe`. Verify it exists. Prefer the connected editor for live work. Do not launch a second editor against an already-open project or close an editor with unsaved work to free the project lock. Use an authorized isolated checkout for separate batch work when appropriate.

For Edit Mode tests, set `$unityExe` to that verified executable and run from the project root. This example waits for Unity and retains results under `Logs/`:

```powershell
$projectRoot = (Get-Location).Path
$testOutput = Join-Path $projectRoot 'Logs/AgentValidation'
New-Item -ItemType Directory -Force -Path $testOutput | Out-Null
$unityArgs = @(
    '-batchmode', '-projectPath', ('"{0}"' -f $projectRoot),
    '-runTests', '-testPlatform', 'EditMode',
    '-testResults', ('"{0}"' -f (Join-Path $testOutput 'editmode-results.xml')),
    '-logFile', ('"{0}"' -f (Join-Path $testOutput 'editmode.log'))
)
$unityProcess = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -WindowStyle Hidden -Wait -PassThru
$unityProcess.ExitCode
```

Let the Test Framework finish the run; do not add `-quit` to this test command. Use fresh result paths or check timestamps so old results cannot masquerade as a new pass. Inspect the result file for test totals, failures, and skipped tests as well as the process exit code and log. Zero tests or missing results are not a pass. See Unity's [Test Framework command-line reference](https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/manual/reference-command-line.html) for filters supported by the installed package.

For compilation-only batch checks, use `-batchmode -quit -projectPath ... -logFile ...`. For builds or setup use `-executeMethod` only with an existing, inspected static editor method; do not invent a build entry point. Add `-nographics` only when the validation does not require rendering. A command that merely opens Unity is not compilation, build, or test evidence.

## Finish with evidence

For implementation changes, complete architecture §13: update touched script headers and relevant pure-layer tests, verify assembly compilation, run ast-grep and graph conformance checks, refresh GitNexus, and run `ArchitectureConformanceTests` plus the project unit tests. Add live scene/prefab checks when the change affects wiring. A green static check alone cannot establish that a feature is connected in Unity.

For instruction-only changes, validate links, skill invocation/ownership rules, and consistency with architecture and tool schemas; report checks that have no applicable code target. Keep unrelated user changes intact. Summarize what changed, evidence obtained, checks skipped or blocked, and any documentation hand-off. Never claim all engineering gates passed merely because a command exited successfully.

## Generated GitNexus context

Keep the maintained guide above outside the `gitnexus:start` / `gitnexus:end` markers. The block below is generated navigation and may be refreshed by GitNexus; do not put project policy only inside it.

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **WORSEN-PROTO** (3564 symbols, 7459 relationships, 267 execution flows).

> Index stale? Run `node .gitnexus/run.cjs analyze --index-only` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? Bootstrap with `npx`, `bunx`, or `pnpm dlx` — e.g. `bunx gitnexus@latest analyze` (npm 11 npx crash; #1939).

## Always Do

- **MUST run impact analysis before editing.** Use `impact({target: "symbolName", direction: "upstream"})` (MCP) or `node .gitnexus/run.cjs impact "symbolName" --direction upstream --repo .` (CLI fallback); report callers, processes, and risk. Never substitute grep for graph analysis.
- **MUST analyze graph changes before committing.** Use `detect_changes({scope: "all"})` (MCP) or `node .gitnexus/run.cjs detect-changes --scope all --repo .` (CLI fallback). `partial: true` or `truncated: true` is not a clean check — a zero means unseen, not unaffected; re-run it. For regression review: `detect_changes({scope: "compare", base_ref: "main"})` or `node .gitnexus/run.cjs detect-changes --scope compare --base-ref "main" --repo .`.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- **MUST treat `risk: UNKNOWN` as unresolved, not as low.** An empty caller set is not evidence the symbol is unused — it can also mean the callers are not resolvable by the index (plain-object property access, dynamic dispatch, cross-language calls). `impact` pairs `UNKNOWN` with a `riskNote` saying so. Confirm with a text search before treating the symbol as safe to change or delete; do not proceed on the strength of a zero.
- When exploring unfamiliar code, use `query({search_query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `context({name: "symbolName"})`.
- For security review, `explain({target: "fileOrSymbol"})` lists taint findings (source→sink flows; needs `analyze --pdg`).

## Never Do

- NEVER edit a function, class, or method before MCP/CLI impact analysis.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis, and never read `UNKNOWN` as an all-clear — it means the walk could not answer, which is the one verdict that requires confirming by other means.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit before MCP/CLI graph change analysis.

## Resources

| Resource | Use for |
| --- | --- |
| `gitnexus://repo/WORSEN-PROTO/context` | Codebase overview, check index freshness |
| `gitnexus://repo/WORSEN-PROTO/clusters` | All functional areas |
| `gitnexus://repo/WORSEN-PROTO/processes` | All execution flows |
| `gitnexus://repo/WORSEN-PROTO/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
| --- | --- |
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
