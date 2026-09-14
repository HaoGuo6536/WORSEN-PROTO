---
id: SPEC-002
type: spec
title: WORSEN foundational game design revision 3
status: LIVE
created: 2026-09-14
updated: 2026-09-14
owner: UNKNOWN — owner input needed
supersedes: none
superseded_by: none
source: sources/WORSEN_GDD_Rev3.docx
archived: none
---

# SPEC-002 — WORSEN foundational game design revision 3

> Status: LIVE since 2026-09-14 by migration default. See the [registry](../index.md).
> Source: [Foundational Game Design Document (GDD), Revision 3](sources/WORSEN_GDD_Rev3.docx), migrated unchanged from `Assets/Scripts/WORSEN_GDD_Rev3.docx`.

## 1. Subject and scope

This is a summary stub for the retained design source, not a complete transcription. The source defines WORSEN's first-person action-horror game and solo prototype: survival through movement, pursuit, collection, and a collapsing floor. Future cooperative play and broader content are design targets beyond the initial prototype. These are intended behaviors, not verified implementation claims.

## 2. Intended behaviour

1. Evasion is combat. Players survive artificial intelligence (AI) hunters through movement; ordinary hunters cannot be permanently killed and there is no attack button (source §§1–3).
2. Keep players moving. The base kit includes sprint, jump, crouch/slide, vault/mantle, short wall rebound, air steering, momentum-preserving landings, and held look-back. Basic sprint has no stamina bar; default interactions should not lock movement for more than roughly one second (§§3–4.2).
3. Cakes collect instantly on contact. Collecting all Cakes opens the exit and starts collapse from distant regions toward the exit. Golden Cakes appear at former Cake positions for an optional second sweep that earns currency. The solo first sweep targets two to four minutes (§§4.3–4.5).
4. A confirmed chase produces an in-body audiovisual detection beat. Proximity effects and hunter audio communicate pursuit distance. Health values remain hidden, there is no permanent minimap, and Golden Cakes have no directional cue (§4.6).
5. The Director supplies delayed, imprecise hints and spaces pressure with relief; hunters establish confirmed pursuit through their own sensors and memory. Hunter decisions use goal-oriented action planning (GOAP). Chase tuning depends on inertia, committed attacks, speed ratio, and loss rules (§4.8).
6. Readable chase spaces offer standard, technical, and bailout routes. Required traversal does not depend on an item. The wider design uses procedural assemblies of handcrafted modules; PLAN-001 limits the first prototype to a hand-built cluster (§4.10 and PLAN-001 scope).

## 3. Constraints and non-negotiables

The source's ordered prototype scope is solo-only and separates simulation state from presentation for future networking (§6). It defers networking, classes and content catalogs, a full hunter roster, boss cadence, branching, extra biomes, and between-level shops. Engineering follows [SPEC-001](SPEC-001-project-architecture-guidelines.md); the build sequence and narrower prototype scope are in [PLAN-001](../plans/PLAN-001-worsen-boilerplate.md).

## 4. Rationale

The source centers play on legible pursuit and decisions made while moving. Instant collection maintains motion; collapse and optional Golden Cakes create a choice between leaving and taking further risk. Hunter limitations make movement skill useful, while the Director sustains pressure without directly steering hunters (§§3–4.8).

## 5. Open questions

These entries preserve source uncertainty; migration does not choose a design outcome.

| Question | Why it matters | Owner | Needed by |
|----------|----------------|-------|-----------|
| Which title, collectible name, chase speeds, health/injury values, and inventory values are final? | The source leaves these tunables and labels open (§7). | UNKNOWN | Relevant implementation or tuning milestone |
| What steering cost should look-back have? | §§4.6 and 6 specify reduced steering, while §7 still leaves that cost unresolved. | UNKNOWN | Look-back implementation |
| Is an optional third-person detection snap retained, and is any boss in prototype scope? | §5 still mentions a camera switch and objective-based boss prototype; the first-person rules and §6 sequence do not establish those as required prototype work. PLAN-001 treats the snap as an optional late experiment. | UNKNOWN | Any camera or boss scope change |
| Which networking authority, collapse stripping rules, map item, and detailed content lists are chosen? | The source explicitly leaves these decisions open (§7); several concern deferred work. | UNKNOWN | Plans covering those features |

## 6. Plans implementing this spec

- [PLAN-001 — WORSEN core boilerplate implementation](../plans/PLAN-001-worsen-boilerplate.md) — LIVE; implements the solo prototype subset under SPEC-001's architecture.

## 7. History

| Date | Change | By |
|------|--------|----|
| 2026-09-14 | Migrated the original binary unchanged and registered this summary stub. Source disagreements remain open. | $docs-plans init |
