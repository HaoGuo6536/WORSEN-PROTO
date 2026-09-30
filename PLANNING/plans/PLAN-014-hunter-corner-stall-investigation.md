---
id: PLAN-014
type: plan
title: Hunter corner-stall investigation
status: LIVE
created: 2026-09-30
updated: 2026-09-30
owner: Hunter navigation worker (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001, SPEC-003]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-014 — Hunter corner-stall investigation

> Status: LIVE since 2026-09-30 (approved by Hao Guo). Implements [SPEC-004 §2.9](../specs/SPEC-004-horror-direction-content-proposals.md#29-playtest-notes-2026-09-30-movement-and-traversal), row "Hunter AI breaks on corners". Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Direction documentation is not present.

## 1. Objective

Find, with evidence, why hunters stall or jitter against corner obstacles in generated rooms, then fix the cause. Acceptance is zero stalls across a declared set of generated floors, with the replays retained. Two earlier attempts failed, so this is an investigation: each candidate cause is ruled in or out by a recorded experiment before any fix is kept.

## 2. Starting point

- Corner-clearance tests pass in the test scene, but hunters stall on generated door frames (SPEC-004 §2.9). Earlier fixes are not identified in the planning record [find them in git history and `Logs/AgentValidation/` before step 3].
- [`HunterSteeringPresenter`](../../Assets/Scripts/Domain/Hunter/Driver/HunterSteeringPresenter.cs) (281 lines) turns navigation-mesh (NavMesh) path corners into acceleration- and turn-limited steering. [`HunterRoutePresenter`](../../Assets/Scripts/Domain/Hunter/Driver/HunterRoutePresenter.cs) (41 lines) selects route points. [`HunterDriver`](../../Assets/Scripts/Domain/Hunter/Driver/HunterDriver.cs) owns the capsule and rigidbody.
- [`ProceduralDriver`](../../Assets/Scripts/Domain/Procedural/Driver/ProceduralDriver.cs) validates paths on the runtime-built NavMesh (around lines 195–217). Door openings come from [`ProceduralGeometryPresenter`](../../Assets/Scripts/Domain/Procedural/Driver/ProceduralGeometryPresenter.cs) and [`ProceduralCastlePresenter`](../../Assets/Scripts/Domain/Procedural/Driver/ProceduralCastlePresenter.cs).
- Telemetry has no stall record.

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Stall detector (fact only): no progress along a non-empty path for longer than a threshold | Hunter Controller or Driver state | — |
| 2 | Stall telemetry: position, room, nearest obstacle, path corners, agent radius versus capsule radius, current action, seed | Telemetry kind (Core, coordinator); Telemetry presenter | 1 |
| 3 | Automated sweep: drive each hunter archetype through every door frame and stair edge of a declared set of generated floors and assert no stall | `Assets/Editor/Tests/Hunter/**` (Play Mode), sweep tool under `Assets/Editor/Hunter/` | 1, 2 |
| 4 | Hypothesis experiments, one at a time (table below), each with a recorded result | Branch-local changes; evidence under `Logs/AgentValidation/PLAN-014/` | 3 |
| 5 | Fix the cause(s); add the corner margin in the route presenter and a stuck detector that replans after a short stall | Route/steering presenters; generation or bake settings via PLAN-026 | 4 |

| Candidate cause | Experiment |
|---|---|
| NavMesh agent radius smaller than the physical capsule | Match radii in the bake and capsule; rerun the sweep |
| Steering presenter overrides the agent's own avoidance at corners | Log the steering target against the path corner near a stall; test with a corner-aware lookahead |
| Missing off-mesh links at door thresholds | Inspect NavMesh at thresholds; add links in a test build |
| Bake settings for generated geometry (voxel size, step height, slope) | Vary one setting per run on the same seeds |

**Requests.** Stall telemetry is a Core change (coordinator). Bake or door-frame changes go to PLAN-026's owner. Until this plan completes, it owns `HunterSteeringPresenter`, `HunterRoutePresenter` and the stall path in `HunterDriver`; PLAN-015 waits on those files.

## 4. Sequence

1. Recover the history of the two earlier attempts and what each changed. Record it in §9.
2. **Before measuring**, declare the seed set, room-count range, archetypes and stall threshold. Proposal: 25 seeds spanning the smallest and largest configured floors, every current archetype, and a threshold of 0.75 s without progress. Hao Guo approves.
3. Build the detector, telemetry and sweep. Run a baseline and keep every failure and replay.
4. Run the hypothesis experiments one at a time on the same seeds.
5. Apply the confirmed fix, then add the margin and replan fallback. Re-run the full declared sweep.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

- Pure tests for the stall detector's threshold boundaries and for corner-margin geometry.
- Play Mode sweep over the declared set, held under the Unity lease, writing per-run CSV and replays.
- **The fallback must not hide the defect.** Every replan counts as a stall event in telemetry. Acceptance needs zero stalls **and** reports the replan count; a replan count above zero means the cause is not fully fixed, and Hao Guo decides whether that is acceptable.
- Live check: chases through generated door frames in HorrorRun, with replays retained.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Size of the declared sweep | Open value | Evidence strength | Declare before measuring; Hao Guo |
| Cause lies in generation, not hunter code | Ownership | Cross-plan fix | Request to PLAN-026 |
| New archetypes (PLAN-016/017) reintroduce stalls | Regression | Recurrence | Sweep becomes a regression test for every archetype |
| Earlier fix attempts unrecorded | Evidence gap | Repeated work | Step 1 before any change |

## 7. Deferred follow-ups

Partition-ignoring navigation (PLAN-016) and ceiling traversal for the Weaver (PLAN-017) need their own sweeps.

## 8. Definition of done

- [ ] Earlier attempts and each hypothesis result are recorded with evidence.
- [ ] The declared sweep passes with zero stalls; the replan count is reported; replays are retained.
- [ ] The sweep runs as a regression test for all current archetypes.
- [ ] Live HorrorRun chases through generated door frames show no stall.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-30 | Detector and tooling | Stall detector, corner sweep tool and stall telemetry landed. Sweep host adapter and the Unity sweep remain | Commits 37096f0, 1c02f4d |
