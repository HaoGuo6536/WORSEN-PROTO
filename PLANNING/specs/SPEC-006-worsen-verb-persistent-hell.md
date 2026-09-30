---
id: SPEC-006
type: spec
title: In-run worsen verb and persistent hell
status: DRAFT
created: 2026-09-30
updated: 2026-09-30
owner: Hao Guo
supersedes: none
superseded_by: none
source: none
archived: none
---

# SPEC-006 — In-run worsen verb and persistent hell

> Status: DRAFT since 2026-09-30. See [../index.md](../index.md).

## 1. Subject and scope

Two short design choices for [SPEC-004 §2.4][s4] and [PLAN-023][p23]. **Not built until approved.** This DRAFT neither approves implementation nor introduces config fields, catalogue entries, save migrations or input bindings. All proposed numbers below are **provisional design values**, located only in this document, not existing runtime defaults. Working option labels are not lore names.

Baseline: [catalogue][catalogue], [shop][shop] and [shrine][shrines] rules exist. **Interact is now used by shrines**, coordinated by [ExpeditionSessionManager][expedition], contrary to older “unused” claims. [RunHistoryRecord][persistence] holds lifetime runs, best depth and unlocked ids, not persistent curses/debt. Existing bail and floor-start revival copy is superseded by §3.

## 2. Intended behaviour

### 2.1 In-run worsen verb — choose one option

For either option: a **new rebindable Worsen action**, held **0.75 s**; release cancels before commitment. Audit physical bindings; never overload Interact. Movement continues without a menu. Preview exact cost/reward; disable commitment during shrine interaction, catch/revival immunity, pause and transitions. One acceptance per floor, protected against input/reload duplication. Declining changes nothing.

| | A — Give up the look-back (recommended) | B — Shorten the escape clock |
|---|---|---|
| Player-facing rule | “Lose look-back for this floor. Earn 25% extra Golden Cake base value after this choice, paid when you escape.” | “Your collapse escape time shrinks by 20%. Earn 50% extra Golden Cake base value after this choice, paid when you escape.” |
| Timing/input | Commit during the sweep, before exit opening, using the dedicated action above. | Same action and window; never shorten an already-running collapse or remove an existing warning. |
| Pressure | Temporarily apply `no-look-back`; preserve free forward steering, arrow accuracy and attack warnings. | Multiply the otherwise-applicable collapse duration by **0.8** when collapse begins; no damage/speed inflation. |
| Curses | Unavailable if look-back is already removed, including by persistent Option P1. No free reward for an absent sacrifice. Hunter-specific curses, including Faithless Arrow, remain unchanged. | Unavailable with `faster-collapse`; do not multiply two timing penalties. With `shuffled-collapse`, retain the guaranteed escape route. Greedy Door and other exit requirements stay in force. |
| Shrines | Bargain/Chance cannot pay a second reward for this temporary effect. No Echo replay, Purgatory removal or substitution of this contract. Other shrine outcomes resolve normally. | Shrines cannot reset the clock modifier, duplicate its reward or buy back time. Wick remains fully effective; no room lighting is consumed as the cost. |
| Economy | Bonus = floor(0.25 × eligible base value). Eligible value is only real Golden Cakes collected after commitment; exclude shrine payouts, refunds, Interest, Golden Touch and other yield bonuses. | Same isolated bonus calculation, with **0.50** replacing 0.25; never multiply Business License/Gilded Greed bonuses again. |
| Extra Life | Same-floor contract and pending payout survive the in-place revive. No fresh activation, refund or second payout. Terminal death loses wallet and pending bonus. | Same; collapse continues through revival, but collision grace/immunity cannot be shortened by this contract. |
| Risk / playtest | Removal may feel redundant for players who seldom look back; a rounded bonus may be worthless on a poor floor. Do players knowingly accept, change routes, and regret greed rather than the input? | A short route may become impossible or a long route trivial. Can every validated escape route still be completed with movement mistakes? Does the extra haul swamp shop prices? |

Preview rounding and remaining reward opportunity; don't promise a coin. On a valid exit, pay once and clear pressure; terminal death clears without payment. Collection requirements, roster and selection cadence stay unchanged. No wallet wager, early exit or debit.

**Recommendation:** A first: information loss without collapse rebalancing. Compare matched seeds, including no later Golden Cakes, Stare/Mimic and Extra Life. Retune trivial sacrifices/rewards before adding tiers. B is an alternative, not a simultaneous button.

### 2.2 Persistent hell — choose one option

Offer an explicitly confirmed **shelter contract**, never a surprise death penalty. Use menu selection/confirm, not Interact. Show clearance terms before acceptance and remaining burden at each run start. One contract at a time; declining adds nothing. No equipment, wallet or Extra Life carries across runs: bring-in stakes remain deferred.

**P1 — Carry a known curse until you climb out (recommended).** Accept `no-look-back` across runs; earn a one-time **4 Golden Cake wallet credit** in the accepting run. Clear the contract after **3 successful floor exits**, cumulative across runs. Death preserves earned exit progress, adds no stacks, and never grants the acceptance credit again. A normal death is still terminal; the next run starts with this one explicit burden. A player who already lacks look-back cannot accept it; while contracted, exclude duplicate no-look-back offers. Do not count it as the mandatory curse pick.

- Input: shelter preview then confirm; results show progress, with no in-run action to erase it.
- Curses/shrines: separate persistent ownership from ordinary run effects. Purgatory, Bargain and Echo cannot remove, trade or replay this contract; preview that exception. Other cures, deals and Wick operate normally. Hunter gates and stack caps still apply.
- Economy/Extra Life: the credit is not Golden Cake collection and earns no yield multipliers. It may fund ordinary purchases, never carried gear. Extra Life does not clear the curse or count as an exit/death; only a completed floor advances clearance. No credit on restart, death or revival.
- Risk/playtest: the restriction may become tedious, and conflicts with in-run A by design. Can a struggling player clear it over several short runs without seeking save deletion? Is the small up-front benefit worth explicit loss of a learned tool? Persistent P1 disables A until cleared; do not silently substitute another sacrifice.

**P2 — Bounded debt, paid by survival.** Confirm a one-time **6 Golden Cake wallet advance** and a persistent **8-unit debt**. Each terminal run death adds **4 units**, capped at **16**. Each successful floor exit clears **2 units**, even with an empty wallet. For each such exit while indebted, withhold up to **2 Golden Cakes** from that floor's earned haul, never from an already-spent wallet; a shortfall is forgiven, not capitalised. Clear debt at zero. No new loan while indebted.

- Input: shelter preview/confirm; show current debt and maximum before every run. It is an obligation, not a button that pays to escape.
- Curses/shrines: no hunter-stat penalty, no new curse stacks. Shrine money, refunds and Interest are excluded from the withholding basis; shrine operations cannot refinance or erase debt. Other effects retain their rules.
- Economy/Extra Life: if combined later with in-run worsening, settle normal haul plus its bonus, then the bounded withholding, then between-floor Interest. A revive neither compounds nor repays debt. Terminal death compounds once; reload must not repeat it. Empty-wallet exits still provide recovery, so failure cannot create an unpayable bill.
- Risk/playtest: even capped debt punishes struggling players and may encourage farming easy exits; it also makes shop affordability harder to predict. Does the advance buy meaningful agency, or merely delay insolvency? Can players explain the next repayment without opening a ledger?

**Recommendation:** P1, trialled separately from A: readable burden, progress preserved through failure. P2 is comparison only. Both need atomic, versioned acceptance/credit/progress; failed writes reject acceptance without granting/charging. Old saves start contract-free. Resume/abandon policy needs approval: neither duplicate credit nor inferred crash deaths is acceptable.

## 3. Constraints and non-negotiables

Owner decisions of 2026-09-30 override older plans: no early bail; every retained hunter remains active every floor; catch ends the run except once-per-run Extra Life, which revives **in place with collision grace and temporary immunity**. No option refunds or refreshes that use. Mannequin moves only in darkness, lit rooms are refuges and Wick always freezes it. Faithless Arrow is an ordinary Mimic curse. Neither false-positive sounds nor world noises reach hunters; worsening never converts presentation audio into a stimulus. Lore names/backstory remain out of implementation. Maintain [SPEC-001][architecture] layering and save ownership; no implementation or migration is authorised by this DRAFT.

## 4. Rationale

A/P1 remove information with bounded recovery, not hidden damage inflation. Separate mid-floor and shelter consent. Explicit settlement prevents shrine laundering, multiplied yield and revival refunds. Approve a small trial, not every option.

## 5. Open questions

| Question | Why it matters | Owner | Needed by |
|---|---|---|---|
| Approve A and/or P1, or choose their alternatives? | These recommendations are not execution approval. | Hao Guo | Before implementation |
| Which free, accessible binding should Worsen use? | Interact belongs to shrines; accidental acceptance is unacceptable. | Input/UI owners + Hao Guo | Input contract |
| Are rewards worth the sacrifice across generated floors? | Small base yields and rounding may produce no bonus. | PLAN-023/024 + Hao Guo | Seeded playtest |
| Permit shrine exceptions and cumulative P1 clearance? | These must be understandable before acceptance. | PLAN-025 + Hao Guo | Contract approval |
| What happens on abandon, crash or unsupported save version? | Must prevent duplicate rewards without punishing failed writes. | Persistence/coordinator + Hao Guo | Save schema approval |

## 6. Plans implementing this spec

[PLAN-023][p23] is the prospective owner; [PLAN-024][p24] economy and [PLAN-025][p25] shrines are dependencies, with shared input/persistence routing assigned by [PLAN-011][p11]. Their LIVE status does not approve these proposals. The hunter rules remain in the separate [SPEC-005 draft][s5].

### Requests

Coordinator: add this proposed row to [PLANNING/index.md](../index.md), with the path interpreted relative to that registry; do not promote the status. The registry was not edited.

| ID | Type | Title | Status | Created | Updated | Direction | Specs | Supersedes | Path |
|---|---|---|---|---|---|---|---|---|---|
| SPEC-006 | spec | In-run worsen verb and persistent hell | DRAFT | 2026-09-30 | 2026-09-30 | n/a | — | none | `specs/SPEC-006-worsen-verb-persistent-hell.md` |

Coordinator hand-offs: [SPEC-004 §2.4][s4] and [PLAN-023 §§1–3, 5–8][p23] need the settled no-bail/shrine-Interact decisions reconciled. [EffectCatalogueConfig.cs][catalogue] `_entries` retains `bail-bond` and old `extra-life` wording: retire obsolete bail offers and align revival copy through their owners, not this draft. After approval only, scope [ProgressionSessionController][selection] contract settlement and [RunHistoryRecord][persistence] versioned persistence, with exact shared input fields assigned by the coordinator. No speculative API name or source edit is proposed as already available.

## 7. History

| Date | Change | By |
|---|---|---|
| 2026-09-30 | Drafted two options per idea, recommended A/P1, recorded owner constraints and deferred all implementation pending approval. | Hermes, for Hao Guo |

[s4]: SPEC-004-horror-direction-content-proposals.md#24-stakes-and-the-make-it-worse-ladder
[s5]: SPEC-005-hunter-briefs.md
[architecture]: SPEC-001-project-architecture-guidelines.md
[p11]: ../plans/PLAN-011-horror-direction-coordination.md
[p23]: ../plans/PLAN-023-selection-curses-stakes.md
[p24]: ../plans/PLAN-024-shop-upgrades-consumables.md
[p25]: ../plans/PLAN-025-shrines.md
[catalogue]: ../../Assets/Scripts/Session/Progression/Config/EffectCatalogueConfig.cs
[shop]: ../../Assets/Scripts/Session/Progression/Shop/Config/ShopConfig.cs
[shrines]: ../../Assets/Scripts/Session/Progression/Config/ShrineProgressionConfig.cs
[expedition]: ../../Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs
[persistence]: ../../Assets/Scripts/Core/Definitions/PersistenceDefinitions.cs
[selection]: ../../Assets/Scripts/Session/Progression/Controller/ProgressionSessionController.cs
| 2026-09-30 | Owner: not approved; persistent hell dropped (run-scoped worsening only); worsen verb under review | Hao Guo / coordinator |
