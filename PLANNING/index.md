# PLANNING registry — WORSEN

> Last audited: 2026-09-30 by $docs-plans audit
> Current floor-pacing scope: see [the 2026-09-15 user clarification](#floor-pacing-clarification-on-2026-09-15); the former first-sweep duration target is non-blocking.
> Companion: `../DOCUMENTATION/direction.md` is not present; direction linkage is unavailable until $codebase-documentation has been run.

## Live

| ID | Type | Title | Status | Created | Updated | Direction | Specs | Supersedes | Path |
|----|------|-------|--------|---------|---------|-----------|-------|------------|------|
| PLAN-001 | plan | WORSEN core boilerplate implementation | LIVE | 2026-09-14 | 2026-09-15 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-001-worsen-boilerplate.md](plans/PLAN-001-worsen-boilerplate.md) |
| PLAN-002 | plan | Parallel contracts and integration | LIVE | 2026-09-14 | 2026-09-15 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-002-parallel-coordination.md](plans/PLAN-002-parallel-coordination.md) |
| PLAN-003 | plan | Player movement and health | LIVE | 2026-09-14 | 2026-09-15 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-003-player-movement-health.md](plans/PLAN-003-player-movement-health.md) |
| PLAN-005 | plan | Hunter behavior and chase rules | LIVE | 2026-09-14 | 2026-09-15 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-005-hunter-chase.md](plans/PLAN-005-hunter-chase.md) |
| PLAN-006 | plan | Camera and post-processing communicate pursuit without obscuring movement | LIVE | 2026-09-14 | 2026-09-15 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-006-camera-postfx-feedback.md](plans/PLAN-006-camera-postfx-feedback.md) |
| PLAN-007 | plan | Audio and interface communicate chase and run outcomes | LIVE | 2026-09-14 | 2026-09-15 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-007-audio-hud-results.md](plans/PLAN-007-audio-hud-results.md) |
| PLAN-008 | plan | Floor collection and collapse | LIVE | 2026-09-14 | 2026-09-15 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-008-floor-collapse.md](plans/PLAN-008-floor-collapse.md) |
| PLAN-009 | plan | Director pressure and relief | LIVE | 2026-09-14 | 2026-09-15 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-009-director-pressure.md](plans/PLAN-009-director-pressure.md) |
| PLAN-010 | plan | Telemetry replay and tuning | LIVE | 2026-09-14 | 2026-09-15 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-010-telemetry-replay-tuning.md](plans/PLAN-010-telemetry-replay-tuning.md) |
| SPEC-001 | spec | Project architecture and modular design guidelines | LIVE | 2026-09-14 | 2026-09-15 | n/a | — | none | [specs/SPEC-001-project-architecture-guidelines.md](specs/SPEC-001-project-architecture-guidelines.md) |
| SPEC-002 | spec | WORSEN foundational game design revision 3 | LIVE | 2026-09-14 | 2026-09-14 | n/a | — | none | [specs/SPEC-002-worsen-game-design.md](specs/SPEC-002-worsen-game-design.md) |
| SPEC-003 | spec | Procedural maps and level progression | DRAFT | 2026-09-15 | 2026-09-15 | n/a | — | none | [specs/SPEC-003-procedural-maps-level-progression.md](specs/SPEC-003-procedural-maps-level-progression.md) |
| SPEC-004 | spec | Horror direction and content proposals | DRAFT | 2026-09-30 | 2026-09-30 | n/a | — | none | [specs/SPEC-004-horror-direction-content-proposals.md](specs/SPEC-004-horror-direction-content-proposals.md) |

## Archived

| ID | Type | Title | Final status | Created | Archived | Superseded by / reason | Evidence | Path |
|----|------|-------|--------------|---------|----------|------------------------|----------|------|
| PLAN-004 | plan | Level graph and tag arena | COMPLETED | 2026-09-14 | 2026-09-15 | Exit criteria met; separate umbrella acceptance remains | [025 checkpoint](../Logs/AgentValidation/GoalCompletion/current-evidence/checkpoint-025.md) | [archive/plans/PLAN-004-level-tag-arena.md](archive/plans/PLAN-004-level-tag-arena.md) |

## Initialization record

Initialized on 2026-09-14. Migration scope was `Assets/Scripts/`, with this registry at the repository root as requested. Migrated documents start `LIVE` under the skill's `init` defaults. This operation organizes existing intent; it does not execute PLAN-001 or establish that its exit criteria have been met. Creation dates record registration in this system; original document ownership was not supplied.

| Original path | Current location | Removed Unity metadata |
|---|---|---|
| `Assets/Scripts/PROJECT_ARCHITECTURE_GUIDELINES.md` | [SPEC-001](specs/SPEC-001-project-architecture-guidelines.md) | `Assets/Scripts/PROJECT_ARCHITECTURE_GUIDELINES.md.meta` |
| `Assets/Scripts/WORSEN_Boilerplate_Plan.md` | [PLAN-001](plans/PLAN-001-worsen-boilerplate.md) | `Assets/Scripts/WORSEN_Boilerplate_Plan.md.meta` |
| `Assets/Scripts/WORSEN_GDD_Rev3.docx` | [Unchanged binary source](specs/sources/WORSEN_GDD_Rev3.docx), summarized by [SPEC-002](specs/SPEC-002-worsen-game-design.md) | `Assets/Scripts/WORSEN_GDD_Rev3.docx.meta` |

The originals were untracked, so migration used filesystem moves instead of `git mv`; no files were staged. The Word source is retained byte for byte. The migrated Markdown bodies retain their existing sections, including the architecture's stable section numbers; metadata, navigation, and the plan's obsolete location note were updated. Existing lint and test labels that name `PROJECT_ARCHITECTURE_GUIDELINES.md` refer to SPEC-001.

No generated `docs/plans/*gitnexus-plan*.md` files existed to register. No direction document exists to reconcile. When $codebase-documentation is explicitly requested, use the registered plan as intent and verify implementation before assigning direction priorities or recording completion.

## Initialization audit on 2026-09-14

All three Markdown documents have exactly one registry row, matching front matter, valid locations, and matching dates. The binary source link and local navigation resolve. No terminal documents, generated plans, stale live plans, or direction references require correction. SPEC-002 records unresolved disagreements within its source without changing that source.

Validation covered planning structure, document references, preservation of the source binary, and removal of the three obsolete metadata files. This was a documentation migration: no code symbols were changed, so GitNexus symbol impact, compilation, ast-grep, and Unity tests were not run as engineering gates. Historical test claims in migrated documents were not reverified.

**Audit summary: 2 live specs, 1 live plan, 0 archived, 0 violations.**

## Parallel decomposition on 2026-09-14

The user requested physical plans that can be run in parallel and an explicit testing-admission gate. PLAN-001 remains the LIVE umbrella; PLAN-002 through PLAN-010 partition its approved execution scope. They inherit the prior instruction to begin boilerplate execution and add no feature scope. LIVE does not mean a dependency checkpoint has been met, an agent has been launched, or work is complete. No document was superseded, archived, copied into a second authority, or marked completed.

Start with [the execution map](plans/PLAN-001-worsen-boilerplate.md#parallel-execution-map), then [PLAN-002 C0/C1](plans/PLAN-002-parallel-coordination.md#3-changes). Workers use exact file ownership, agreed shared contracts and the [Unity lease](../tools/coordination/README.md). Physical child files contain nine structured sections, scope, entry gates, handoffs, verification, risks and completion checklists. C1 permits parallel implementation; real integration and human/measured acceptance have later gates.

The M0 evidence is [available locally](../Logs/AgentValidation/M0/verification.md); it is a baseline with recorded limits, not completion of the full boilerplate. No direction document exists. HAND-OFF → $codebase-documentation, when explicitly requested: reconcile the umbrella and child plans against implementation before assigning direction items; this operation did not edit DOCUMENTATION.

The decomposition audit checks one registry row per Markdown document, matching metadata/status/dates, valid locations, unique IDs, local file/anchor links, all nine sections in each new plan, no generated-plan mutation, and explicit coverage of M1–M8 plus shared coordination. The standalone lease tests verify concurrent admission and fail-closed behavior outside Unity; this documentation/tooling task does not claim new Unity gameplay test results.

**Audit summary: 2 live specs, 10 live plans, 0 archived, 0 structural/link violations; 157 local links and anchors checked.** [Audit evidence](../Logs/AgentValidation/ParallelPlans/planning-audit.json). The lease helper passed [11 standalone scenarios](../Logs/AgentValidation/ParallelPlans/lease-tests.json), including eight-process concurrent acquisition with exactly one winner, and a [Windows PowerShell 5.1 lifecycle check](../Logs/AgentValidation/ParallelPlans/lease-ps51.json). These tests did not operate Unity or acquire the real project lease.

## Boilerplate continuation audit on 2026-09-14

The user authorized continuation and invoked $docs-plans to archive plans where warranted. PLAN-002 coordinated named Player, Level, Camera/PostFX and Telemetry/Input owners plus independent compilation and review. Their 89-file implementation candidate remains outside imported paths while editor admission is unresolved. [The verification report](../Logs/AgentValidation/PLAN-002/27ec7913-08f9-4c5b-8184-2dfbbb1f94a7/verification.md) distinguishes offline compilation and 78 executed managed assertions from unexecuted Unity, scene, graph and measured/human gates. PLAN-002/003/004/006/010 execution logs now record that preparation.

**Archival decision: 2 LIVE specs, 10 LIVE plans, 0 archived.** No plan has satisfied its exit criteria, been superseded or been cancelled. Therefore no status transition or move is appropriate. [The current metadata/file-link audit](../Logs/AgentValidation/PLAN-002/planning-audit/audit-summary-final.json) records the checks and their scope; existing anchor validation from decomposition is historical rather than newly executed evidence.

Direction linkage remains unavailable because `DOCUMENTATION/direction.md` is absent. No DOCUMENTATION edits or implicit codebase-documentation pass occurred. A future explicit documentation reconciliation must verify published implementation before describing it as current behavior; source preparation is not implementation completion.

## Published implementation reconciliation on 2026-09-14

PLAN-001–010 implementation is published across TagArena and FloorLoop systems. Earlier preparation records remain history and no longer describe publication state. The [requirement matrix](../Logs/AgentValidation/GoalCompletion/requirements.md) maps implementation, tests, observed failures and remaining acceptance.

The published [source012 manifest](../Logs/AgentValidation/PLAN-002/offline-compile/hunter-level-log-final-012/source-manifest.json), 61DC0C1B32D70A37D28496911592C19488B744F000F21471992982D22DE05C33, contains 238 exact audited inputs: 231 C# files and seven assembly definitions. Seven assemblies compile with 0 errors and 60 visible warnings; 11 lint rules report 0 findings. The final Unity suite passes 548/548, including all 13 architecture cases. [Final source graph012](../Logs/AgentValidation/GoalCompletion/current-evidence/graph-final-012.md) matches all source and eight scene/NavMesh/meta inputs, with reviewed conformance and complete tracked/untracked change coverage. The accumulated baseline has fully reviewed CRITICAL impact.

[Final engineering and closeout](../Logs/AgentValidation/GoalCompletion/current-evidence/final-012-closeout.md) retains the observer interval, matching rebuilt scene source/config stamps, intended idle TagArena, no active Play/import/compile/build/test or dirty scenes, stopped observer and successful coordinator lease release. Existing dirty editor/vendor assets were not globally saved. The ambiguous console response contains two errors exactly matching passing fault-injection tests; no clean-console claim is made. The prior full 548 run with stale stamps, subsequent 547/548 log-assertion failure and all earlier route/replay/screenshot failures remain history. The corrected passing Level case still prints the same Synaptic informational message and expected Level Error.

The [final capture audit](../Logs/AgentValidation/GoalCompletion/telemetry-audit/20260915T0550228251317Z-85a6c6facab346bb8ca17fc74842622e/README.md) verifies 22 CSV and 23 input files, all 13 emitted kinds and five complete exact-session pairs against current source/config/scene stamps. All 22 catch/vault/denominator and inclusive-membership checks agree. Seventeen CSV and 17 input files remain incomplete, including seven incomplete input end mismatches. Complete Player input is unpaired; the 131-tick Hunter pair has two arranged catches and circumstantial fixture attribution. The directly bound 2672-tick Floor sweep is 32.283335017 seconds; fresh Player/Floor p90 values 8.819847346/7.999999 remain below 9 m/s. Incomplete look-back/failure captures and all historical numeric trials are preserved.

Bounded actual movement, forward/reverse vaults, seven Hunter gate/invalidation/detour cases, collection/cues/collapse, historical-hint investigation, presentation and recording have evidence. The [remaining acceptance matrix](../Logs/AgentValidation/GoalCompletion/current-evidence/remaining-acceptance.md) retains wider movement/lock combinations, measured speed/chase/floor/Director criteria, real devices, perception, natural look-back/vault use and comfort. Retained synthetic speed and first-sweep trials miss their thresholds; those cohorts remain declared and do not establish a sprint tuning defect or participant acceptance. No new acceptance threshold or denominator is substituted.

**Archival decision: no transition or move.** Ten plans remain LIVE because explicit exit criteria are unmet; none is superseded or cancelled. Two specs remain LIVE. Preserve old rows, scope, identities, dates, unchecked completion items and locations. Direction linkage is unavailable because `DOCUMENTATION/direction.md` is absent; no DOCUMENTATION pass or edit is implied. A separately invoked $codebase-documentation reconciliation remains the handoff for verified behavior and remaining gaps.

**Audit summary: 2 live specs, 10 live plans, 0 archived.** The [proposal004 audit](../Logs/AgentValidation/GoalCompletion/planning-update/planning-audit-004.json) checks metadata, locations and target-relative file links. Application is coordinator-owned and guarded by the reviewed final proposal hash and all 11 original target hashes.


## Current engineering and acceptance audit on 2026-09-15

The [checkpoint evidence](../Logs/AgentValidation/GoalCompletion/planning-update/proposal-005/evidence-matrix.md) verifies Source 021 engineering. The current full normal Unity suite reports **563 passed, 0 failed and 2 Explicit skips out of 565 cases**, including all **13 architecture cases**. The two skipped long cohort methods retain their separately executed earlier results. All 246 source inputs, native assemblies, graph identities and config/scene stamps agree. Seven assemblies compile with 0 errors and 60 retained warnings; 11 lint rules report 0 findings.

Independent critical-health and limb/replay audits pass. Real public damage commands reach living 25 health, with actual critical audio gain and injury vignette, then death/fresh life clear them. The coordinator and independent reviewer inspected all four original corrected limb PNGs. Recorded movement is unchanged while feet become visible in both Slide images and Vault hands remain visible. Intended tuning edit/persistence/Undo and actual movement-chain/cast/lock/replay requirements have evidence; no exhaustive permutation or changed-FOV experiment is added. A later actual pointer click on rendered RUN AGAIN restarts FloorLoop with fresh run state and a new capture, with its scope and second closeout independently audited.

The independent full-regression audit retains every leaf output, expected fault-injection errors, strict navigation sample failures and tool/vendor diagnostics. Reviewed graph coverage remains CRITICAL for the accumulated baseline, with no unresolved partial/truncated result. Final live probes confirm the correct project, idle editor, clean active scene and disabled observer callbacks; lease release succeeds and subsequent status is free. No clean-console claim is made.

**Acceptance remains open.** The declared 23-attempt cohort reaches 30 completed chases with 23 complete input/CSV pairs, but median chase length is 6.316666996 s, official loss is 20%, and 16 first sweeps have median 34.650001807 s. Those miss their targets; Lunge share is 24/24. All nine complete interior proximity gaps are below the 23-second base threshold, with 30 boundary-censored intervals retained. Earlier free-speed shortfalls remain. Physical-device and participant tag/readability, distance perception, natural look-back/vault rates and 15-minute comfort/no-wandering evidence are still missing. Historical failures and every denominator remain.

**Archival decision: no transition or move.** Ten plans and two specs remain LIVE. Preserve identities, creation dates, scope, unchecked criteria and locations. This is a verified engineering checkpoint and an honest acceptance handoff, not completed boilerplate. Direction linkage is unavailable; no DOCUMENTATION file was edited. A separately requested $codebase-documentation pass remains the handoff for verified implementation.

## Floor pacing clarification on 2026-09-15

The [user clarification](../Logs/AgentValidation/GoalCompletion/planning-update/proposal-006/user-clarification.md) makes the current greybox floor's former **2–4-minute first-sweep duration target NON-BLOCKING** for boilerplate completion. Procedural level generation, later floor growth and their duration calibration remain deferred. Existing first-sweep measurements still miss the historical target; the dated scope decision does not turn them into passes.

[PLAN-001 M5](plans/PLAN-001-worsen-boilerplate.md#m5--floor-loop--2-weeks) and [PLAN-008](plans/PLAN-008-floor-collapse.md) retain functional collection/cues, no-stop interaction, exit, optional Golden Cakes, collapse/telegraph/blocker/lethal behavior, warning presentation, death/exit summaries and restart. Hunter/chase numeric rules, movement metrics, Director behavior and unrelated participant/device acceptance are unchanged. Do not redesign or tune this hand-built floor solely to meet the deferred duration target.

Ten plans and two specs remain LIVE, with no checkbox completion, status transition or archive move. Historical execution/evidence records remain intact. This planning clarification does not edit DOCUMENTATION or implement procedural generation.

## Level child-plan completion on 2026-09-15

PLAN-004 is COMPLETED and moved once into archive/plans. Its twelve authored routes, original camera cue review, retained topology/navigation/rebuild evidence and current engineering checks satisfy the finite child scope. The full run retains 595 passes, one neutral-input precondition failure and four Explicit skips; the unchanged exact-method retry passes. See [025 evidence](../Logs/AgentValidation/GoalCompletion/current-evidence/checkpoint-025.md). Earlier status/count statements above are historical.

PLAN-001 and the other eight execution plans remain LIVE. SPEC-003 remains a separate DRAFT todo; working procedural generation is required, with enemy/curse/shop placeholders allowed. No implementation of that follow-up was performed.

> HAND-OFF → $codebase-documentation
> PLAN-004 is COMPLETED at PLANNING/archive/plans/PLAN-004-level-tag-arena.md. Reconcile its verified outcome when documentation is explicitly requested. DOCUMENTATION/direction.md is absent; this operation did not create or edit DOCUMENTATION.

Audit summary: 2 LIVE specs, 9 LIVE plans, 1 DRAFT spec, 1 archived plan. [Archive audit](../Logs/AgentValidation/GoalCompletion/planning-update/archive-025/audit.json): 13 unique documents, 269 local file links checked, zero violations.

## Horror direction spec registered on 2026-09-30

SPEC-004 records the owner's 2026-09-30 direction shift toward sustained horror, converted from the reviewed Claude Doc with its approved and deferred marks kept inline, and retains the two design-conversation transcripts under `specs/sources/`. It is DRAFT: it overrides named SPEC-002 lines where they conflict but is not yet approved authority, and no plan implements it. SPEC-002 and SPEC-003 are unchanged in status. This operation did not edit DOCUMENTATION.

Audit summary: 2 LIVE specs, 9 LIVE plans, 2 DRAFT specs, 1 archived plan; every `.md` under PLANNING except index.md has one registry row and every row's path exists.
