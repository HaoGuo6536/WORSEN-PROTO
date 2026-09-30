---
id: SPEC-003
type: spec
title: Procedural maps and level progression
status: DRAFT
created: 2026-09-15
updated: 2026-09-30
owner: UNKNOWN — owner input needed
supersedes: none
superseded_by: none
source: sources/WORSEN_GDD_Rev3.docx
archived: none
---

# SPEC-003 — Procedural maps and level progression

> Status: DRAFT since 2026-09-15. See the [registry](../index.md).
> Sources: the user's 2026-09-15 request and the unchanged [Game Design Document (GDD), Revision 3](sources/WORSEN_GDD_Rev3.docx), summarized by [SPEC-002](SPEC-002-worsen-game-design.md). This records intended follow-up work, not an implemented feature or settled balance choices.

## 1. Subject and scope

Working procedural maps, growth across successive rounds, and progression involving enemies, curses and shops. **Procedural generation must produce playable maps in the running game. Enemy, curse, shop and visual content may be placeholders.** For this draft, a round means one playable floor followed by a between-floor progression interval; exact choice cadence remains open.

This is a separate follow-up to the hand-built boilerplate. [PLAN-001](../plans/PLAN-001-worsen-boilerplate.md#4-what-is-deliberately-not-in-the-boilerplate), [PLAN-004](../archive/plans/PLAN-004-level-tag-arena.md) and [PLAN-008](../plans/PLAN-008-floor-collapse.md) retain their current scope. Procedural generation and progression do not become boilerplate completion criteria. Networking, cooperative resolution, final content catalogs, boss cadence, branching, extra biomes, permanent unlocks and save/load are outside this initial follow-up.

## 2. Intended behaviour

1. **Generate physical maps at runtime.** Starting a run or advancing rounds assembles reusable handcrafted room modules, connections, collision geometry, movement markers, objective anchors, player/enemy entry points and an exit. A small module catalog and greybox art are sufficient. A fixed scene selector, an abstract graph without playable geometry or an editor-only assembly cannot satisfy this requirement.

2. **Make generated maps reproducible.** The run seed, round index and generator/content/progression configuration determine module selection, transforms, connections, stable generated identities, objective/spawn placement and collapse ordering. Record these inputs and a comparable layout manifest. Repeating them in a fresh run must reproduce the layout independently of frame timing. This promises layout reproducibility, not identical physics or navigation-mesh serialization across engine versions.

3. **Vary layouts and support larger later rounds.** Different seeds produce meaningful module arrangement or route variation. Configurable progression controls physical map size and content budgets, supporting measurably larger later floors within a declared range. Growth can be stepped or capped; no percentage increase, room-count formula, size cap or mandatory increase on every round is prescribed. The GDD describes player-count scaling; growth with round depth is the user's additional direction.

4. **Preserve movement and chase routes.** Module connections describe direction, traversal capability, height, collectible anchors and player/hunter permissions. Generated spaces provide standard routes and technical alternatives using the base movement kit, with readable bailout opportunities and deliberate sightline breaks. Required traversal and escape must never depend on buying an item. One-way drops need valid landings and onward routes; required Cakes use valid reachable anchors.

5. **Validate the assembled floor before play.** Every required objective is reachable from spawn, and the exit is reachable from each required objective, accounting for directional traversal. Validate navigation, movement-flow and hunter-route graphs against actual geometry and supported capabilities. The configured hunter stand-in has usable navigation before pursuit starts; player-only shortcuts do not become hunter walkways. Reject overlaps, blocked connections, invalid anchors/spawns and unsupported mandatory traversal. Generation attempts are bounded, and failure reports the seed/reason without admitting a half-built floor. A fallback must be declared and reported separately; loading a fixed map cannot be counted as successful procedural generation.

6. **Support the existing floor loop on generated maps.** Preserve the GDD §§4.3–4.5 defaults: contact collection of required Cakes opens the exit and starts telegraphed collapse; Golden Cakes appear at former Cake positions for an optional second sweep. The player may leave without taking an optional reward or buying anything. Choose the exit early enough to calculate room distances and farthest-first collapse. Validate connections and collapse order so required escape does not depend on crossing an already removed route without warning. Delaying or making a routing mistake may still cause death. Floor duration and pacing are evaluated with the later size/content configuration; this spec introduces no new timing gate for the hand-built boilerplate.

7. **Advance through an actual round lifecycle.** Successful solo escape resolves the floor once, retains run-scoped currency and progression state, presents the between-floor interval and generates the next round. Death ends the run. Restart resets the wallet, selections, round state and generated content; an explicitly selected seed reproduces its initial layout. Teardown removes old geometry, pickups, navigation data/links, hazards, hunters and subscriptions before the next floor becomes active. Repeated input cannot duplicate rewards, purchases, actors or completion events.

8. **Provide replaceable enemy, curse and shop placeholders.** A player can progress through placeholder choices/shop presentation, with selected identifiers and lifetimes visible in run state and passed to the next round. A simple existing hunter can represent an enemy entry. Curse effects and shop content may be clearly labeled stubs; a completed roster, item catalog or new artificial intelligence system is unnecessary. Preserve the GDD distinction between threats, curses and rewards: retained hunters and curses persist within the run by default, while activation budgets, concrete effects and cadence remain open. Retention does not imply that every hunter must be active simultaneously.

9. **Keep wallet and shop behavior consistent.** Under GDD §4.9, Golden Cakes fund the shared run wallet, retained after escape and reduced by purchases, explicit enemy effects or run end. Solo uses this same run wallet. A placeholder shop may display stub offers and allow continuing without buying. Any enabled purchase must check affordability, apply its debit/reward exactly once and prevent a negative balance. Prices, offer counts, rewards and shop frequency remain configurable and unbalanced; permanent currency and unlocks are deferred.

Acceptance evidence for a future implementing plan must include:

- Runtime generation followed by actual movement, collection and escape on the generated map, with navigation queries or movement by the configured hunter stand-in.
- Fresh runs with matching seed/round/configuration producing matching full layout manifests, plus a prospectively declared seed sample demonstrating meaningful variation. Retain failed generation attempts and their reasons.
- Smaller and larger progression configurations with measured physical room/usable-area and route differences, reachable required anchors and usable navigation. Configuration labels alone do not establish growth.
- Validation of every required anchor and exit, directional traversal, hunter permissions and collapse sequencing; invalid module/configuration cases fail visibly before play.
- Consecutive generated rounds exercising enemy/curse/shop placeholder selections, retained run state and wallet semantics, then death/restart with reset state and no stale content or duplicate events.
- A declared supported catalog, round/size range, seed sample, runtime generation budget and acceptance method before measurement. Numerical budgets remain open until the implementing plan records them.

## 3. Constraints and non-negotiables

Follow [SPEC-001](SPEC-001-project-architecture-guidelines.md): injected randomness and permitted layer dependencies, designer configuration separated from runtime state, paired lifecycle/events, repeatable asset wiring and engineering verification. Pure generation/progression decisions must not depend on scene searches or engine objects. Physical assembly and navigation belong to engine-facing components. Generated state has explicit ownership and teardown; generation must not mutate source modules or configuration assets.

[SPEC-002](SPEC-002-worsen-game-design.md) and its GDD remain the broader design source. This draft preserves first-person evasion, base-kit reachability, readable chase spaces, optional second sweep and run-scoped progression. It records the requested follow-up without approving unresolved balance choices or expanding the existing boilerplate plans. A separate implementation plan must define owned paths, integration contracts and supported budgets. No DOCUMENTATION/HIGHLEVEL folder exists to link; this operation does not initialize or edit DOCUMENTATION.

## 4. Rationale

Reusable modules allow varied maps while preserving deliberate local traversal. Seeds and layout manifests make bad maps reproducible; physical and navigation validation ensure a connected graph is also playable. A working round transition puts generation into the game loop. Placeholder content concentrates the initial work on generation, reachability and lifecycle while leaving room for later enemies, curses and shops.

## 5. Open questions

| Question | Why it matters | Owner | Needed by |
|----------|----------------|-------|-----------|
| What module catalog, connection rules, room/area ranges and size cap define the first supported generator? | Determines feasibility, validation coverage and generation cost. | UNKNOWN | Implementation plan |
| How do map size, required Cakes, threats and rewards change with round depth? | Establishes the growth curve and later pacing; none is approved here. | UNKNOWN | Progression configuration |
| Does every round present an enemy, a curse and a shop, or do these occur at different intervals or as alternatives? | The user names all three; their exact cadence is unresolved. | UNKNOWN | Round-flow design |
| Which retained hunters are active, and what lifetimes/effects do placeholder curses exercise? | Preserves run progression without requiring all retained threats to be active or fully implemented. | UNKNOWN | Content integration |
| Which shop offers and purchase effects are functional in the first slice? | Placeholders are allowed; a final economy is unnecessary. | UNKNOWN | Shop integration |
| What generation-time/memory budgets, retry policy and seed/size sample apply? | Bounds runtime work and establishes meaningful acceptance without arbitrary gates. | UNKNOWN | Implementation plan |
| What route, sightline, collapse-escape and readability checks operationalize GDD §4.10? | Turns its qualitative module rules into checkable content constraints. | UNKNOWN | Module acceptance |

## 6. Plans implementing this spec

The unchecked follow-up is recorded in [PLAN-001's deferred-work list](../plans/PLAN-001-worsen-boilerplate.md#4-what-is-deliberately-not-in-the-boilerplate). PLAN-001, PLAN-004 and PLAN-008 retain their hand-built boilerplate scope. This DRAFT is not an execution plan.

- PLAN-026 — DRAFT — [plans/PLAN-026-level-diversity-gaps-multifloor.md](../plans/PLAN-026-level-diversity-gaps-multifloor.md). Registered 2026-09-30 from SPEC-004. It continues this spec's generation work, and its first step records which of section 2's requirements the existing `Domain/Procedural` code meets. PLAN-014 (hunter corner stalls on generated floors) and PLAN-023 (selection cadence) also touch this spec's subject.

## 7. History

| Date | Change | By |
|------|--------|----|
| 2026-09-15 | Recorded the requested separate procedural-map and level-progression follow-up. Working generation is required; enemy/curse/shop content may be placeholders. Preserved GDD floor/wallet/progression semantics and left growth, cadence and balance choices open. | $docs-plans |
| 2026-09-30 | Section 6 lists PLAN-026 (DRAFT), registered during the SPEC-004 decomposition. Body and status unchanged. | $docs-plans new plan |
