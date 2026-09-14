# PLANNING registry — WORSEN

> Last audited: 2026-09-14 by $docs-plans audit
> Companion: `../DOCUMENTATION/direction.md` is not present; direction linkage is unavailable until $codebase-documentation has been run.

## Live

| ID | Type | Title | Status | Created | Updated | Direction | Specs | Supersedes | Path |
|----|------|-------|--------|---------|---------|-----------|-------|------------|------|
| PLAN-001 | plan | WORSEN core boilerplate implementation | LIVE | 2026-09-14 | 2026-09-14 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-001-worsen-boilerplate.md](plans/PLAN-001-worsen-boilerplate.md) |
| PLAN-002 | plan | Parallel contracts and integration | LIVE | 2026-09-14 | 2026-09-14 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-002-parallel-coordination.md](plans/PLAN-002-parallel-coordination.md) |
| PLAN-003 | plan | Player movement and health | LIVE | 2026-09-14 | 2026-09-14 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-003-player-movement-health.md](plans/PLAN-003-player-movement-health.md) |
| PLAN-004 | plan | Level graph and tag arena | LIVE | 2026-09-14 | 2026-09-14 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-004-level-tag-arena.md](plans/PLAN-004-level-tag-arena.md) |
| PLAN-005 | plan | Hunter behavior and chase rules | LIVE | 2026-09-14 | 2026-09-14 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-005-hunter-chase.md](plans/PLAN-005-hunter-chase.md) |
| PLAN-006 | plan | Camera and post-processing communicate pursuit without obscuring movement | LIVE | 2026-09-14 | 2026-09-14 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-006-camera-postfx-feedback.md](plans/PLAN-006-camera-postfx-feedback.md) |
| PLAN-007 | plan | Audio and interface communicate chase and run outcomes | LIVE | 2026-09-14 | 2026-09-14 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-007-audio-hud-results.md](plans/PLAN-007-audio-hud-results.md) |
| PLAN-008 | plan | Floor collection and collapse | LIVE | 2026-09-14 | 2026-09-14 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-008-floor-collapse.md](plans/PLAN-008-floor-collapse.md) |
| PLAN-009 | plan | Director pressure and relief | LIVE | 2026-09-14 | 2026-09-14 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-009-director-pressure.md](plans/PLAN-009-director-pressure.md) |
| PLAN-010 | plan | Telemetry replay and tuning | LIVE | 2026-09-14 | 2026-09-14 | n/a | SPEC-001, SPEC-002 | none | [plans/PLAN-010-telemetry-replay-tuning.md](plans/PLAN-010-telemetry-replay-tuning.md) |
| SPEC-001 | spec | Project architecture and modular design guidelines | LIVE | 2026-09-14 | 2026-09-14 | n/a | — | none | [specs/SPEC-001-project-architecture-guidelines.md](specs/SPEC-001-project-architecture-guidelines.md) |
| SPEC-002 | spec | WORSEN foundational game design revision 3 | LIVE | 2026-09-14 | 2026-09-14 | n/a | — | none | [specs/SPEC-002-worsen-game-design.md](specs/SPEC-002-worsen-game-design.md) |

## Archived

| ID | Type | Title | Final status | Created | Archived | Superseded by / reason | Evidence | Path |
|----|------|-------|--------------|---------|----------|------------------------|----------|------|

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
