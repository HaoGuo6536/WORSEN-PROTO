---
id: PLAN-024
type: plan
title: Shop, upgrades and consumables
status: LIVE
created: 2026-09-30
updated: 2026-09-30
owner: Shop worker, delegated shop and inventory subtree (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-024 — Shop, upgrades and consumables

> Status: LIVE since 2026-09-30 (approved by Hao Guo). Implements [SPEC-004 §2.12](../specs/SPEC-004-horror-direction-content-proposals.md#212-relics-consumables-shop-and-selection-cadence) (relics, flashlight, expensive shop); the §2.13 upgrades, consumables and shop presentation; the §2.18 flashlight row; and filling the §2.8 inventory slots. Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Direction documentation is not present.

## 1. Objective

Make buying something a decision:

- **Upgrades:** each a rule rather than a number where possible, available from a floor and sometimes only while a given hunter is in the run.
- **Shop:** draws a small subset per visit with scarce rerolls, priced against a floor's golden haul.
- **Consumables:** three slots by default, used in motion; a purchase with full slots forces a replacement.
- **Flashlight:** a free light with a slowly recharging stun, the first class-style item.

## 2. Starting point

- [`ProgressionConfig`](../../Assets/Scripts/Session/Progression/Config/ProgressionConfig.cs) entries carry price, multipliers, traits, repeatable, stock per visit, Wax Ward and `_requiredThreatId`. Current offers: four unique (narrower cone, quieter footsteps, shorter rebound cooldown, door marks) and two repeatable (heal, one-charge grab break). Prices are 2–4 and `_goldenCakeValue = 1`.
- [`InventorySnapshot`](../../Assets/Scripts/Core/Definitions/PlayerMovementDefinitions.cs) holds two strings, always empty.
- Flashlight: a UseItem toggle ([`HorrorOrchestrator`](../../Assets/Scripts/Orchestrator/HorrorOrchestrator.cs), [`HorrorEffectsController`](../../Assets/Scripts/Session/HorrorEffects/Controller/HorrorEffectsController.cs)). Hunters react to the beam through light-response actions. No stun exists.
- The shop screen is in [`ProgressionUI`](../../Assets/Scripts/Presentation/ProgressionUI/Driver/ProgressionUIPresenter.cs).

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Shop and inventory location: a new Session shop system or a delegated Progression subtree, decided at H1 | Architecture decision (coordinator) | H1 |
| 2 | Upgrade catalogue: all 49 §2.13 entries as data with availability floor, hunter requirement, stack and cap; no rarity | Catalogue from PLAN-023 change 1 | PLAN-023 |
| 3 | Upgrade effects, each implemented by its owner (table below) | Requests | 2 |
| 4 | Shop visit: draw a subset from allowed entries; rerolls scarce; prices scale with round; later floors unlock expensive upgrades | Shop controller, config | 1, 2 |
| 5 | Consumables: Firecracker, Gauze, Smelling Salts, Wax Ward, Doorstop, Oil Flask, Glass Vial, Adrenaline, with uses; used in motion | Inventory state; effects via owners | H1 shared hearing (Firecracker), interactables (Doorstop) |
| 6 | Inventory: three slots by default, Bigger Pockets adds slots; buying with full slots forces a choice; the replaced item is gone | Inventory state; Core snapshot for more slots (coordinator) | 5 |
| 7 | Input: consumable selection and use without clashing with the flashlight | Input binding request (coordinator) | §6 |
| 8 | Flashlight as a class item: the light stays free; aim and hold on a hunter's face to stun, spending a slowly recharging charge; Steady Hand | HorrorEffects or flashlight system; Hunter stun reaction request to PLAN-015 | PLAN-012 UseItem fix |
| 9 | Shop screen: pedestals, reroll, price, availability reason, replacement choice | ProgressionUI shop presenter | 4, 6 |
| 10 | Retire old offers; map Wax Ward to the consumable; drop the heal offer because health resets per floor | Config | PLAN-023 change 12 |

| Upgrade group | Effect owner |
|---|---|
| Movement and grace: Stored Momentum, Soft Landing, Quiet Slide, Thick Skin, Low Profile, Longer Slide, Higher Jump, Speed Boost, Quick Start, Air Control, Fast Hands, Long Boost, Field Kit | PLAN-013 (Low Profile also PLAN-018) |
| Collapse: Wax Heart | PLAN-018 |
| Flashlight: Steady Hand | This plan |
| Hunter-gated: Second Bounce, Glimpse, Latch, Echo Boots, Blind Faith, Loud Heart, Trail Reader, Sure Footing, Stone Nerves, Web Cutter, Marked Doors, Afterglow, Spare Key, Mirror Skin, Ear Plugs | PLAN-016/017 with the named hunter; Glimpse also PLAN-022 |
| Guidance and cakes: Sweet Tooth, Exit Sense, Golden Sense, Sticky Fingers, Gilded Greed | PLAN-019 |
| Senses: Cat Eyes, Keen Ears | PLAN-022, PLAN-021 |
| Economy: Lucky Reroll, Bargain Hunter, Bail Bond, Golden Touch, Shop Reroll, Loyalty Card, Interest, Refund, Extra Pedestal, Business License, Bigger Pockets | This plan (Bail Bond with PLAN-023) |
| Extra Life | This plan with PLAN-020/022 (death does not end the run once) |

## 4. Sequence

1. Run GitNexus upstream impact on `ProgressionConfig`, `ProgressionSessionController` shop paths, `InventorySnapshot` and the flashlight path.
2. **After PLAN-023 change 1:** 1, 2, 4, 6, 9 and 10 (catalogue and shop working with economy upgrades).
3. 8 (flashlight stun), then 5 and 7.
4. Movement and guidance upgrades as their owners publish hooks; hunter-gated upgrades as each hunter ships.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

- Shop tests: the subset honours floor and hunter requirements; the reroll count is consumed; prices scale by round; affordability is checked and the debit applied exactly once; the wallet never goes negative (SPEC-003 §2.9).
- Inventory tests: default three slots; Bigger Pockets stacks; replacement flow; uses decrement; Spent Pockets clears at exit.
- Flashlight tests: stun needs aim and hold; charge spent and recharged at the configured rate; the light works with no charge.
- Live HorrorRun: buy, replace, use a Firecracker (hunters investigate) and stun a hunter. Owner judges whether a purchase feels like a decision.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| "No relic is a percentage" vs catalogue multipliers | Spec conflict | Catalogue | PLAN-011 §6 item 3; Hao Guo |
| Input for consumables vs flashlight vs stun | Design | Controls | Hao Guo; Input owner |
| Prices, floors, reroll counts | Open values | Economy | Config; Hao Guo (SPEC-004 §5) |
| Extra Life conflicts with "the catch ends the run" | Design | Death flow | Hao Guo |
| 49 entries is a large surface | Scope | Delivery | Ship economy and movement first; add the rest as hooks land |

## 7. Deferred follow-ups

Survivor classes beyond the flashlight; More Shrines upgrade (PLAN-025); bring-in items (co-op).

## 8. Definition of done

- [ ] Shop visits draw gated subsets with rerolls and scaled prices, and the wallet invariants hold.
- [ ] All eight consumables work in motion with the slot and replacement rules.
- [ ] The flashlight stun works with recharge; the light stays free.
- [ ] Every upgrade is implemented by its owner, or explicitly deferred by the owner with a reason.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
