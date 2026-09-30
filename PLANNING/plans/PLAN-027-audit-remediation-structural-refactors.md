---
id: PLAN-027
type: plan
title: "Audit remediation: gates, delegation workflow and structural refactors"
status: LIVE
created: 2026-09-30
updated: 2026-09-30
owner: Coordinator; design and approval authority Hao Guo
specs: [SPEC-001]
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-027 — Audit remediation: gates, delegation workflow and structural refactors

> Status: LIVE since 2026-09-30, explicitly owner-approved. Implements [SPEC-001](../specs/SPEC-001-project-architecture-guidelines.md). Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Direction linkage is unavailable until $codebase-documentation has been run.

## 1. Objective

Make audit remediation an owned, evidence-gated workstream: fail-closed integration and bounded delegation stay in force while structural debt is removed without changing approved gameplay or relaxing architecture. Retire the applicable SPEC-001 Appendix A rows only after their actual exit evidence exists. This LIVE plan records work already delivered and remaining work; it does not claim completion or authorise SPEC-006 implementation.

## 2. Starting point

The owner approved audit fixes and the workflow work on 2026-09-30. The coordinator tracker and accepted branches distinguish delivery from promotion. Documentation is absent; code, [gate ledger](../../evidence/gate-ledger.jsonl) and exact candidate evidence are the behavioural record.

Delivered work (done within the stated scope, not a claim that all candidates pass):

| Delivered item | Evidence and boundary |
|---|---|
| Fail-closed gate, delegation tooling/run records/scope checks, hooks including Git Large File Storage (LFS), vendor exclusions and VENDOR.md | `7e7c4bd`; [integration](../../tools/integration/README.md), [delegation](../../tools/delegation/README.md), [VENDOR.md](../../VENDOR.md) |
| Gate/delegation fixes and Unity-free continuous integration (CI) | `b1b83c5`; coordinator reports CI green; future candidates must still run it |
| Instruction slimming and on-demand skills | `fa7d1a1`; owner snapshot remains history, not replaced evidence |
| Architecture policy, declared Session order, responsibility/debt rules | `4ce8a81`; [SPEC-001 §13f and Appendix A](../specs/SPEC-001-project-architecture-guidelines.md#appendix-a--known-debt-migrate-when-touched) |
| Enforcement implementation and calibration | Accepted `wt/enforcement`: `6eb1d4d`, `b6368bb`, `a0be1c5`; expanded checks must run on the integrated hash |
| Headless pure-test tier | Accepted `wt/puretest-runner`, including project-root correction `721af64`; pure results exclude environment/skipped cases |
| Test infrastructure, focus categorisation and time-state guard | Accepted `wt/test-infra`, including `8e56aee`; guard-fixture integration `c7a4d85`; no claim of full native-suite acceptance |
| LFS prune | Owner-approved operation reported done in this WP-D brief; operation has no source commit; no reclaimed-byte claim or repeat prune requested |
| Unused High Definition Render Pipeline (HDRP) removal; Steam Audio import recorded | `947395e`; Steam Audio 4.8.1 imported, routing still pending in PLAN-021; Universal Render Pipeline remains |

Batch 12 is retained as `fail-setup`, not promoted: blocking 25 versus baseline 28 with 6 new failures. A smaller failure total is not acceptance. The ledger preserves failed batch13 prechecks too; branch acceptance/headless results are not a substitute for promotion.

## 3. Changes

All paths are repository-relative. These are ownership reservations for future coordinator-assigned batches, not concurrent permission to edit every listed path. New helper/type names below are proposed, not asserted to exist. The coordinator owns shared Core, Session contracts, Orchestrators, assembly definitions, packages/settings, SPEC-001 and its Appendix A. Delegate disjoint system subtrees and serialize overlapping requests.

| # | Remaining change | Owned paths / symbols and responsible owner | Depends on / exit evidence |
|---|---|---|---|
| 1 | Hunter archetype plug-ins and HunterController split; retire A-02/A-03 | Hunter worker: `Assets/Scripts/Domain/Hunter/**`, `Assets/Editor/Tests/Hunter/**`; `HunterManager`, `HunterController`, archetype definitions/modules. Coordinator approves shared contracts | Upstream impact first; replace config type-switch and Manager-created Managers with a factory/registry seam. Preserve each archetype's behaviour. Pure tests for extracted logic, conformance, native mixed/duplicate-roster and lifecycle results; checker no longer needs A-02/A-03 |
| 2 | Run fact-relay split; retire A-01; finish the related Expedition creation boundary A-04 | Coordinator: `Assets/Scripts/Session/Run/**`, `Assets/Scripts/Session/Expedition/**`, `Assets/Scripts/Core/**`, `Assets/Scripts/Orchestrator/**`; tests under `Assets/Editor/Tests/Run/**`, `Expedition/**`, `Scenes/**`, `Core/**` | Keep one Run tick owner, explicit fact channels/relay ownership and symmetric subscriptions; SceneRoot/factory creates ShrineManager. Rebind/disable/teardown, once-only routing and tick-order tests; no extra tick or duplicate event; checker retires A-01/A-04 |
| 3 | Replace string operation dispatch with a proposed `ProgressionOperation` enum | Progression worker: `Assets/Scripts/Session/Progression/**`, `Assets/Editor/Tests/Progression/**`; coordinator reserves `Assets/Scripts/Core/Definitions/ProgressionDefinitions.cs`, `Assets/Scripts/Orchestrator/ProgressionUIOrchestrator.cs` and corresponding routing tests if contract crossing requires them | Discover every producer/consumer before editing; no invented operation values. Exhaustive operation tests and invalid-operation rejection, compatibility/serialization decision recorded, unchanged purchase/choice/continue outcomes |
| 4 | Non-allocating (NonAlloc) physics and per-tick allocation removal | Domain owners serially: `Assets/Scripts/Domain/Player/Driver/**`, `Hunter/Driver/**`, `Procedural/Driver/**` and matching `Assets/Editor/Tests/Player/**`, `Hunter/**`, `Procedural/**`; coordinator Run tick path as needed | Freeze measured call-site inventory before allocation work; reusable query buffers need explicit overflow handling, not truncated collisions. Before/after garbage-collection allocation captures at matched seeds/rosters, native collision parity and buffer-saturation tests |
| 5 | Serialized shader references instead of `Shader.Find` | Presentation/Domain owners: shader-using `Assets/Scripts/Presentation/**` and `Assets/Scripts/Domain/**` config/driver files selected by inventory; coordinator: corresponding `Assets/Editor/**` setup and `Assets/Resources/**` wiring; tests follow selected systems | Record exact file assignment before edits; build-safe serialized references, deterministic setup and explicit missing-reference failure. Player build rendering capture with no missing/pink shader or hidden runtime lookup fallback; source scan |
| 6 | Remove Presentation→Presentation edge: EnvironmentDriver → HorrorLumenPresenter | Environment/Horror worker: `Assets/Scripts/Presentation/Environment/**`, `Assets/Scripts/Presentation/Horror/**`, tests `Assets/Editor/Tests/CastleEnvironment/**`, `Horror/**`; coordinator Core utility only if truly shared pure logic | Move computation to its actual owner or Core pure utility without adding assembly references. Dependency check, pure presenter tests and matched lighting capture show unchanged output |
| 7 | SceneRoot wiring deduplication; retire A-05 | Coordinator: `Assets/Scripts/Orchestrator/Scenes/**`, `Assets/Editor/Scenes/**`, `Assets/Editor/Tests/Scenes/**` | One shared assembly helper, no gameplay logic; TagArena/FloorLoop compatibility stays explicit. All three scenes assemble/rebind/teardown, equivalent references and once-only readiness; checker retires A-05 |
| 8 | Editor SetupKit, grouped “Rebuild All” command and scene-stamp normalisation | Coordinator/editor owner: `Assets/Editor/**` setup tools (excluding other assigned tests), proposed shared utilities in `Assets/Editor/Shared/`, `Assets/Editor/Tests/Scenes/**`, `Architecture/**`; stamp contracts under `Assets/Scripts/Core/**` only through coordinator | Inventory current stamp producer/consumer paths first. Rebuild twice from controlled inputs; equal normalised stamps and no second-pass diff; dirty/unrelated scenes protected, missing vendor dependencies fail explicitly. Publish exact setup version with native scene/build results |
| 9 | Layer assembly definitions set `autoReferenced: false` | Coordinator only: `Assets/Scripts/{Core,Domain,Session,Presentation,Orchestrator}/Worsen.*.asmdef`, `Assets/Editor/Worsen.Editor.asmdef`, `Assets/Editor/Tests/Worsen.Tests.asmdef`; architecture tests/checks | Inspect external/predefined-assembly consumers first. Seven-assembly compile, no new warnings, explicit-reference conformance, editor import and build proof; never relax the layer graph to compile |
| 10 | Approved Final IK vendor asmdef and working IK backend | Coordinator/vendor owner: `Assets/External/Plugins/RootMotion/FinalIK/**` assembly boundary (exact vendor path verified before action); Hunter worker: `Assets/Scripts/Domain/Hunter/Driver/**`, `Config/**`, `Assets/Editor/Tests/Hunter/**`; coordinator deterministic setup/assembly wiring | Permission to add vendor asmdef is already approved, not approval to publish vendor content. Inverse kinematics (IK) vendor types stay behind the engine-facing Driver seam. Shipped-rig head/spine/catch/feet evidence on stairs and split levels; generic no-op is failure; compile/import/build proof and reproducible private vendor setup |
| 11 | Unity Pipeline official Model Context Protocol (MCP)/command-line interface (CLI) pilot | Coordinator only: `Packages/manifest.json`, `Packages/packages-lock.json`, `tools/integration/**`, `.claude/skills/worsen-unity/**`; pilot evidence under `Logs/AgentValidation/PLAN-027/` | Approved pilot `com.unity.pipeline` 0.8.0-exp / CLI beta.9→beta.11. Do not import in a worktree sharing Library. Back up manifest; lease-protected isolated pilot or coordinator-controlled main; owner performs authentication. Predeclare five checks: connection/discovery, read-only scene inspection, supported setup/import, test-result retrieval, build/smoke. Keep rollback evidence; Synaptic HTTP remains the only supported project control path until pilot acceptance is recorded |
| 12 | Remove unused TutorialInfo template | Coordinator/editor owner: `Assets/TutorialInfo/**` and generated metadata through Unity; exact referencing editor/build files only after inventory | Verify no live scene/build/config references, remove template without touching unrelated LFS content, then import/compile/build and missing-reference scan; do not hand-write metadata |
| 13 | GitNexus re-index per batch and worktree retirement | Coordinator: `.gitnexus/**` index output, `tools/integration/**`, `tools/delegation/retire-worktrees.sh`; read-only worktree inventory and archived evidence under `Logs/AgentValidation/worktrees/<slug>/` | Re-index exact promoted hash; retain index identity/query checks. Retire only clean, merged, pushed trees after archiving Logs and unlinking—not recursively deleting—the Library junction. Record before/after inventory and archive readability; preserve dirty/unmerged/unpushed trees |

## 4. Sequence

1. Coordinator records the exact baseline, active owners and path manifest per batch. Re-prove each debt against current code; existing accepted branches must not be reimplemented. Run GitNexus upstream impact before every existing-symbol edit; warn on HIGH/CRITICAL and confirm UNKNOWN/empty/partial results by text search.
2. Freeze contracts and ownership, then deliver items 1–3 and 6–7 as small responsibility-preserving refactors. Serialize Hunter overlaps (1/4/10), SceneRoot overlaps (2/7/8) and coordinator contracts; every new pure Controller/Presenter/Utility ships matching tests and full §0 headers.
3. Profile before item 4; deliver shader/setup/stamp and assembly work (5/8/9) with deterministic setup and build proof. No test assertion weakening, warning suppression or architecture exceptions merely to pass.
4. Coordinator performs item 10 and the bounded item 11 pilot under the exclusive Unity lease, using the approved control-path policy. This document-writing task runs neither. Remove TutorialInfo only after dependency inspection (12).
5. Integrate every implementation item through the fail-closed gate, retain its ledger row and failed attempts, re-index each promoted batch, then retire only eligible worktrees (13). Retire Appendix A rows in the matching verified change, never on branch acceptance alone.

## 5. Verification

Apply [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification) and SPEC-001 §13. For each item retain exact integrated hash, owned-path manifest, upstream impact/direct callers, source/config/setup version, seven-assembly offline compile with zero errors/no new warnings, zero ast-grep findings, architecture checks and selected headless result counts/filter. New test fixtures carry `FixtureTimeGuard`; focus-dependent cases carry `RequiresFocus`.

Native integration additionally needs the gate's setup, Unity tests with nonzero counts, build/smoke seed and relevant recordings/profiler captures; environment/skipped cases are not passes. Owner-facing feel/visual/audio results include an owner date. A stub host, no-op rig, missing shader, overflow-truncated physics query, duplicate relay, changed setup stamp on an unchanged rebuild, or fallback scene is a failure, not a successful substitute. The pilot cannot silently replace Synaptic.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Shared contracts and large relay surfaces | Integration | Lost/duplicated facts or ordering | Coordinator exact ownership and lifecycle regression before promotion |
| Vendor assembly/public repository | Licensing | Leaking third-party content | Permission covers local asmdef integration only; private reproducible vendor setup, never commit the pack |
| Pipeline pilot shares Library if run carelessly | Environment | Disturbs active editor | No shared-Library import; exclusive lease and rollback; owner authentication |
| Allocation improvements alter collision completeness | Correctness | Missed contacts | Measured inventory, saturation tests and explicit overflow policy |
| Historical branch results mistaken for completion | Evidence | Invalid acceptance | Exact-hash ledger and native evidence; keep failed candidates |
| Appendix A waiver deadline | Governance | Expired waiver blocks work | Coordinator resolves before 2026-12-31; no silent extension |

## 7. Deferred follow-ups

Gameplay/content acceptance stays with PLAN-012–026. SPEC-006 run-scoped worsen remains unapproved and persistent hell dropped. No general documentation build, package replacement, gameplay rebalance or broader vendor refactor is authorised. IL2CPP release-backend installation/validation remains a separately scoped follow-up; the current build/smoke baseline uses Mono.

## 8. Definition of done

- [ ] Every remaining §3 item is implemented and merged through the fail-closed gate, with its item-specific exit evidence and ledger entry; external local operations also have read-back receipts.
- [ ] Compile, lint, architecture, headless and native/build results bind to exact integrated hashes; no failed setup, zero-case run, skipped/environment case or fallback is counted as a pass.
- [ ] Appendix A A-01–A-05 rows are retired only on the corresponding verified refactors; no unresolved alarm hidden by a relaxed rule.
- [ ] Every promoted batch has a matching GitNexus index; retired worktrees have archived evidence and satisfy clean/merged/pushed/junction-safety checks.
- [ ] §9 records each remaining item with durable evidence; owner acceptance or explicit pilot disposition is recorded where required. No COMPLETED transition until every exit holds.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-30 | Owner admission | New audit-remediation plan approved LIVE; documentation only in this WP-D operation | Owner request; [PLAN-011](PLAN-011-horror-direction-coordination.md) |
| 2026-09-30 | Delivered workflow/policy | Gate/delegation/hooks/VENDOR, CI, instruction slimming and architecture amendments delivered | `7e7c4bd`, `b1b83c5`, `fa7d1a1`, `4ce8a81`; §2 links |
| 2026-09-30 | Accepted enforcement and test tooling | Enforcement, puretest-runner and test-infra branches accepted; exact integrated gate still required | `wt/enforcement`, `wt/puretest-runner`, `wt/test-infra`; §2 commits; [ledger](../../evidence/gate-ledger.jsonl) |
| 2026-09-30 | Disk/package remediation | LFS prune reported done; HDRP removed; Steam Audio imported, routing pending | Owner/coordinator WP-D brief; `947395e`, [VENDOR.md](../../VENDOR.md) |
| 2026-09-30 | Batch 12 failure preserved | fail-setup; not promoted; blocking 25 versus baseline 28; 6 new failures | [Gate ledger](../../evidence/gate-ledger.jsonl), `batch12`, final candidate `5ee90d132f21e9242b17679b15c3cf83f35c9613` |
