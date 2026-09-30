// ============================================================================
// ProgressionSessionController.cs
// ============================================================================
// PURPOSE:
//   Resolves expedition choices, generated-floor handshakes and wallet changes.
//   Every accepted action changes an explicit phase or revision so stale clicks
//   and callbacks cannot purchase twice or complete a replacement floor.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Consume revision-guarded selected item uses, arm one ward and spend one revival per run.
//   - Apply the current shrine yield to later Golden Cake credit through the shared remainder path.
//   - Retain current-floor health for reporting, but refill it after choices before each generation.
//   - Advance independent selection and shop clocks using completed combat floors.
//   - Debit the configured bail penalty once, inside generation-guarded completion.
//   - Commit catalogue purchases and deferred replacements with one wallet debit.
//   - Delegate shop draws, inventory and economy to the owned Shop subtree.
//   - Resolve generation-guarded shrines, temporary effects and paid shelter bargains.
//   - Keep temporary upgrades out of retained summaries and permanent inventory sizing.
//   - Leave catalogue health/sprint effects to Player; legacy baselines stay unmodified.
//   - Produce immutable snapshots and deterministic per-round generation inputs.
//   - Commit at most three eligible hunter/curse choices and skip exhausted menus.
// DEPENDENCIES:
//   - Own Config and BehaviorState; Core progression value contracts.
//   - Delegated Progression.Shop controller/state/config; no foreign system state.
//   - Injected System.Random; no scene or foreign gameplay systems.
// USAGE NOTES:
//   Construct with a fresh seeded random source when starting/restarting a run.
//   Generation and UI identities remain monotonic in the reused state. Exactly
//   one random draw occurs per round; purchases and health do not alter layouts.
//   Shrines use a separate seeded stream. A marked Bargain forces the next shelter;
//   that visit resets the shop clock and walking away adds no bargain-specific cost.
//   CompleteFloor defaults to a normal escape. The flagged overload is the only
//   bail entry; ApplyBailPenalty is the extension point for a future curse cost.
// ============================================================================
using System;
using System.Collections.Generic;
using Worsen.Core;
using Worsen.Session.Progression.Shop;

namespace Worsen.Session.Progression
{
    public sealed class ProgressionSessionController
    {
        private const int MaximumChoices = 3;
        private readonly ProgressionSessionBehaviorState state;
        private readonly ProgressionConfig config;
        private readonly System.Random random;
        private readonly EffectCatalogueConfig catalogue;
        private readonly IReadOnlyList<ProgressionEntryConfig> curses;
        private readonly EffectCatalogueConfig shopCatalogue;
        private readonly ShopRules shopRules;
        private ShopController shop;
        private ShrineProgressionController shrines;
        public IReadOnlyList<ShrineResolvedFact> ShrineHistory => shrines.History;
        public ActiveEffects FloorEffects => shrines.FloorEffects;
        public float ShrineYieldMultiplier => shrines.YieldMultiplier;

        public ProgressionSessionController(ProgressionSessionBehaviorState state, ProgressionConfig config, System.Random random,
            ShopConfig shopConfig = null, EffectCatalogueConfig shopCatalogue = null)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            ValidateConfig(config);
            catalogue = config.EffectCatalogue;
            this.shopCatalogue = catalogue ?? shopCatalogue;
            if (!(this.shopCatalogue is null) && catalogue is null)
            {
                var legacyHunters = new List<string>();
                foreach (var entry in config.Threats) legacyHunters.Add(entry.Id);
                EffectCatalogueUtility.Validate(this.shopCatalogue, legacyHunters);
            }
            shopRules = (config.ShopConfig ?? shopConfig)?.Rules ?? new ShopRules();
            shop = new ShopController(state.Shop, shopRules, this.shopCatalogue, new System.Random(0));
            shrines = new ShrineProgressionController(state.Shrines, config.ShrineConfig?.Rules ?? new ShrineProgressionRules(),
                this.shopCatalogue, new System.Random(0));
            var combined = new List<ProgressionEntryConfig>(config.Curses);
            if (!(catalogue is null))
            {
                var hunters = new List<string>();
                foreach (var entry in config.Threats) hunters.Add(entry.Id);
                EffectCatalogueUtility.Validate(catalogue, hunters);
                foreach (var entry in catalogue.Entries)
                {
                    var legacy = Find(config.Threats, entry.Id) ?? Find(config.Curses, entry.Id);
                    if (legacy != null && entry.Kind != LegacyKind(legacy))
                        throw new ArgumentException("Catalogue kind conflicts with legacy entry: " + entry.Id);
                    if (entry.Kind == EffectKind.Curse && Find(combined, entry.Id) == null)
                        combined.Add(new ProgressionEntryConfig(entry.Id, entry.Title, entry.CardCopy));
                }
            }
            curses = combined.AsReadOnly();
        }

        public void StartRun(int seed)
        {
            state.Seed = seed;
            state.Round = 0;
            state.CompletedCombatFloors = state.LastShopAtCombatCount = 0;
            state.Traits = ProgressionTraits.None;
            state.WaxWardCharges = 0;
            shop.Reset();
            state.ExtraLifeConsumed = false;
            shrines = new ShrineProgressionController(state.Shrines, config.ShrineConfig?.Rules ?? new ShrineProgressionRules(),
                shopCatalogue, new System.Random(seed));
            shrines.ResetRun();
            state.OfferedThreatIds.Clear();
            state.OfferedCurseIds.Clear();
            state.ActiveThreatIds.Clear();
            state.Wallet = state.ThreatCount = state.CurseCount = 0;
            state.Health = state.MaximumHealth = config.InitialMaximumHealth;
            state.MovementSpeedMultiplier = state.HunterSpeedMultiplier = 1f;
            state.FogDensityMultiplier = state.FlashlightRangeMultiplier = 1f;
            state.SelectionCounts.Clear();
            state.ActiveEffectEntries.Clear();
            state.CollectedGoldenAnchors.Clear();
            BeginNextRound();
        }

        public bool ChooseThreat(string id, int revision)
        {
            if (!Matches(ProgressionPhase.ChooseThreat, revision)) return false;
            ProgressionEntryConfig choice = Find(config.Threats, id);
            if (choice == null || !state.OfferedThreatIds.Contains(id) || !EligibleEntry(choice, EffectKind.Threat))
                return Reject("That hunter is unavailable.");
            ApplyEntry(choice, EffectKind.Threat);
            state.ActiveThreatIds.Add(id);
            state.ThreatCount = state.ActiveThreatIds.Count;
            BeginCurseSelection(id);
            return true;
        }

        public bool ChooseCurse(string id, int revision)
        {
            if (!Matches(ProgressionPhase.ChooseCurse, revision)) return false;
            ProgressionEntryConfig choice = Find(curses, id);
            if (choice == null || !EligibleCurse(choice) || !state.OfferedCurseIds.Contains(id))
                return Reject("That curse is unavailable or at its stack cap.");
            ApplyEntry(choice, EffectKind.Curse);
            state.CurseCount++;
            BeginGeneration();
            return true;
        }

        public bool ConfirmFloorReady(int generationId)
        {
            if (!MatchesGeneration(ProgressionPhase.Generating, generationId)) return false;
            state.Phase = state.IsShop ? ProgressionPhase.Shop : ProgressionPhase.Exploring;
            if (state.IsShop) shrines.OpenDeal(Active());
            state.Message = state.IsShop
                ? "A moment of safety. Spend Golden Cakes, or continue without buying."
                : "Collect the Cakes. Find the exit. Keep your light close.";
            state.Revision++;
            return true;
        }

        public bool FailGeneration(int generationId, string reason)
        {
            if (!MatchesGeneration(ProgressionPhase.Generating, generationId)) return false;
            state.Phase = ProgressionPhase.GenerationFailed;
            state.Message = "Floor generation failed (seed " + state.RoundSeed + "): " +
                (string.IsNullOrWhiteSpace(reason) ? "No valid layout was admitted." : reason);
            state.Revision++;
            return true;
        }

        public bool CompleteFloor(int generationId) => CompleteFloor(generationId, false);

        public bool CompleteFloor(int generationId, bool bailed)
        {
            if (!MatchesGeneration(ProgressionPhase.Exploring, generationId)) return false;
            if (bailed) ApplyBailPenalty();
            shrines.EndFloor();
            state.Wallet += shop.Interest(state.Wallet, Active());
            state.CompletedCombatFloors++;
            BeginNextRound();
            return true;
        }

        private void ApplyBailPenalty()
        {
            // Future permanent-curse costs belong here, inside the same completion guard.
            int debit = shop.BailDebit(state.Wallet, config.EarlyBailWalletFraction, Active());
            state.Wallet -= debit;
        }

        public bool ContinueShop(int revision)
        {
            if (!Matches(ProgressionPhase.Shop, revision)) return false;
            if (shop.HasPending) return Reject("Choose a replacement slot or cancel first.");
            shrines.CloseDeal();
            state.Wallet += shop.Interest(state.Wallet, Active());
            BeginNextRound();
            return true;
        }

        public bool RecordGoldenCollected(int generationId, int anchorId)
        {
            if (!MatchesGeneration(ProgressionPhase.Exploring, generationId) || anchorId < 0 ||
                state.CollectedGoldenAnchors.Contains(anchorId)) return false;
            if (!shop.GoldenCredit(state.Wallet, config.GoldenCakeValue, Active(), out int credit, shrines.YieldMultiplier)) return false;
            state.CollectedGoldenAnchors.Add(anchorId);
            state.Wallet += credit;
            state.Revision++;
            return true;
        }

        public bool Purchase(string id, int revision)
        {
            if (!Matches(ProgressionPhase.Shop, revision)) return false;
            if (shrines.BargainMarked) return Reject("Take a bargain or walk away before shopping.");
            return CommitPurchase(id);
        }

        public bool ReplaceInventorySlot(int slot, int revision)
        {
            if (!Matches(ProgressionPhase.Shop, revision)) return false;
            if (!shop.HasPending || slot < 0) return Reject("Choose an occupied inventory slot.");
            return CommitPurchase(shop.Pending.Id, slot);
        }

        private bool CommitPurchase(string id, int slot = -1)
        {
            if (!shop.Purchase(id, state.Wallet, Active(), out int wallet, out string removed, out string reason, slot))
                return Reject(reason);
            if (shop.HasPending) state.Message = "Inventory full. Choose a slot to replace, or cancel. Nothing has been charged.";
            else
            {
                state.Wallet = wallet;
                if (!string.IsNullOrEmpty(removed))
                {
                    int remaining = Active().Stacks(new EffectId(removed)) - 1;
                    if (remaining <= 0) state.ActiveEffectEntries.Remove(removed);
                    else state.ActiveEffectEntries[removed] = new ActiveEffect(new EffectId(removed), EffectKind.Consumable, remaining);
                }
                var entry = EffectCatalogueUtility.Find(shopCatalogue, id);
                int stacks = Active().Stacks(new EffectId(id));
                state.ActiveEffectEntries[id] = new ActiveEffect(new EffectId(id), entry.Kind, stacks + 1);
                state.SelectionCounts[id] = Count(id) + 1;
                shop.ExpandPedestals(Active());
                state.Message = entry.Title + " purchased.";
            }
            state.Revision++;
            return true;
        }

        public bool CancelReplacement(int revision)
        {
            if (!Matches(ProgressionPhase.Shop, revision) || !shop.CancelReplacement()) return false;
            state.Message = "Replacement cancelled. Wallet and inventory unchanged.";
            state.Revision++;
            return true;
        }

        public bool RerollShop(int revision)
        {
            if (!Matches(ProgressionPhase.Shop, revision)) return false;
            if (shrines.BargainMarked) return Reject("Bargain offers cannot be rerolled.");
            if (!shop.Reroll(state.Wallet, Active(), out int wallet)) return Reject(shop.RerollUnavailable(state.Wallet, Active()));
            state.Wallet = wallet;
            state.Message = "New pedestals drawn.";
            state.Revision++;
            return true;
        }

        public bool RerollSelection(int revision)
        {
            if (state.Revision != revision || (state.Phase != ProgressionPhase.ChooseThreat && state.Phase != ProgressionPhase.ChooseCurse)) return false;
            if (SelectionRerollsRemaining() <= 0) return Reject("No selection rerolls remain.");
            if (state.Phase == ProgressionPhase.ChooseThreat) { state.ThreatRerollsUsed++; BuildThreatChoices(); }
            else { state.CurseRerollsUsed++; BuildCurseChoices(state.ActiveThreatIds.Count == 0 ? null : state.ActiveThreatIds[state.ActiveThreatIds.Count - 1]); }
            state.Message = "New selection drawn.";
            state.Revision++;
            return true;
        }

        private int SelectionRerollsRemaining() => Math.Max(0, shop.SelectionRerolls(Active()) -
            (state.Phase == ProgressionPhase.ChooseThreat ? state.ThreatRerollsUsed : state.CurseRerollsUsed));

        private ActiveEffects Active() => shrines.Combined(new ActiveEffects(state.ActiveEffectEntries.Values));

        public bool ActivateShrine(int generationId, ShrineActivatedFact fact, float shieldCapacity,
            float collectedFraction, out ShrineResolvedFact resolution)
        {
            resolution = default;
            if (!MatchesGeneration(ProgressionPhase.Exploring, generationId)) return false;
            if (!shrines.Resolve(generationId, state.Round, fact, state.Wallet, shieldCapacity, collectedFraction,
                new ActiveEffects(state.ActiveEffectEntries.Values), out resolution)) return false;
            state.Wallet -= resolution.Cost;
            state.Revision++;
            return true;
        }

        public IReadOnlyList<NoiseEvent> TickShrines(int generationId, float dt, long tick) =>
            MatchesGeneration(ProgressionPhase.Exploring, generationId) ? shrines.Tick(dt, tick) : Array.Empty<NoiseEvent>();

        public bool TakeBargain(string id, int revision)
        {
            if (!Matches(ProgressionPhase.Shop, revision)) return false;
            if (!shrines.TakeDeal(id, state.Wallet, Active(), out var curse, out int payout))
                return Reject("That bargain is unavailable. You can walk away for free.");
            state.ActiveEffectEntries[id] = curse;
            state.SelectionCounts[id] = Count(id) + 1;
            state.CurseCount++;
            state.Wallet += payout;
            state.Message = "Bargain accepted. Paid " + payout + " Golden Cakes.";
            state.Revision++;
            return true;
        }

        public bool TryConsumeWaxWard(int generationId)
        {
            if (!MatchesGeneration(ProgressionPhase.Exploring, generationId) || state.WaxWardCharges != 1) return false;
            state.WaxWardCharges = 0;
            state.Message = "Your Wax Ward broke the shadow's grip.";
            state.Revision++;
            return true;
        }

        public ConsumableInventorySnapshot Consumables() => shop.Consumables(Active());

        public bool CycleConsumable(int generationId, int direction)
        {
            if (!MatchesGeneration(ProgressionPhase.Exploring, generationId) || !shop.Cycle(direction, Active())) return false;
            state.Revision++;
            return true;
        }

        public bool TryConsumeSelected(int generationId, int revision, string id)
        {
            if (!MatchesGeneration(ProgressionPhase.Exploring, generationId) || revision != state.Revision ||
                (id == "wax-ward" && state.WaxWardCharges > 0) || !shop.ConsumeSelected(id, out bool exhausted)) return false;
            if (exhausted && state.ActiveEffectEntries.TryGetValue(id, out var active))
            {
                if (active.StackCount <= 1) state.ActiveEffectEntries.Remove(id);
                else state.ActiveEffectEntries[id] = new ActiveEffect(active.Id, active.Kind, active.StackCount - 1);
            }
            if (id == "wax-ward") state.WaxWardCharges = 1;
            state.Revision++;
            return true;
        }

        public bool TryConsumeExtraLife(int generationId)
        {
            if (!MatchesGeneration(ProgressionPhase.Exploring, generationId) || state.ExtraLifeConsumed ||
                !Active().Has(new EffectId("extra-life"))) return false;
            state.ExtraLifeConsumed = true;
            state.Revision++;
            return true;
        }

        public bool RecordHealth(int generationId, float health)
        {
            if (!MatchesGeneration(ProgressionPhase.Exploring, generationId) || !Finite(health) || health < 0f) return false;
            float next = Math.Min(health, state.MaximumHealth);
            if (next == state.Health) return false;
            state.Health = next;
            if (state.Health <= 0f) return EndRun(generationId);
            state.Revision++;
            return true;
        }

        public bool EndRun(int generationId)
        {
            if (generationId != state.GenerationId || state.Phase != ProgressionPhase.Exploring) return false;
            state.Phase = ProgressionPhase.Ended;
            state.Health = 0f;
            state.Wallet = 0;
            shrines.EndFloor(); shrines.CloseDeal();
            state.Message = "The expedition ended on floor " + state.Round + ".";
            state.Revision++;
            return true;
        }

        public ProgressionGenerationRequest GenerationRequest() => new ProgressionGenerationRequest(
            state.GenerationId, state.RoundSeed, state.Round, state.IsShop, Effects());

        public ProgressionSnapshot Snapshot()
        {
            var choices = new List<ProgressionChoice>();
            IReadOnlyList<ProgressionEntryConfig> catalog = state.Phase == ProgressionPhase.ChooseThreat ? config.Threats :
                state.Phase == ProgressionPhase.ChooseCurse ? curses : null;
            if (catalog != null)
                foreach (ProgressionEntryConfig entry in catalog)
                {
                    if (state.Phase == ProgressionPhase.ChooseCurse && !state.OfferedCurseIds.Contains(entry.Id)) continue;
                    if (state.Phase == ProgressionPhase.ChooseThreat && !state.OfferedThreatIds.Contains(entry.Id)) continue;
                    var data = EffectCatalogueUtility.Find(catalogue, entry.Id);
                    choices.Add(new ProgressionChoice(entry.Id, data?.Title ?? entry.Title,
                        data?.CardCopy ?? entry.Description, Count(entry.Id)));
                }
            bool inShop = state.Phase == ProgressionPhase.Shop;
            bool selection = state.Phase == ProgressionPhase.ChooseThreat || state.Phase == ProgressionPhase.ChooseCurse;
            var active = Active();
            var permanent = new ActiveEffects(state.ActiveEffectEntries.Values);
            var offers = inShop ? shop.Offers(state.Wallet, active) : Array.AsReadOnly(Array.Empty<ProgressionOffer>());
            var retained = new List<ProgressionSelection>();
            AppendSelections(retained, config.Threats, ProgressionChoiceKind.Threat);
            AppendSelections(retained, curses, ProgressionChoiceKind.Curse);
            if (!(shopCatalogue is null))
                foreach (var entry in shopCatalogue.Entries)
                {
                    if (entry.Kind == EffectKind.Upgrade && permanent.Has(new EffectId(entry.Id)))
                        retained.Add(new ProgressionSelection(entry.Id, entry.Title, ProgressionChoiceKind.Upgrade, permanent.Stacks(new EffectId(entry.Id))));
                    if (entry.Kind == EffectKind.Curse && Find(curses, entry.Id) == null && Count(entry.Id) > 0)
                        retained.Add(new ProgressionSelection(entry.Id, entry.Title, ProgressionChoiceKind.Curse, Count(entry.Id)));
                }
            string rerollReason = inShop && shrines.BargainMarked ? "Bargain offers cannot be rerolled." : inShop ? shop.RerollUnavailable(state.Wallet, active) :
                selection && SelectionRerollsRemaining() <= 0 ? "No selection rerolls remain." : null;
            return new ProgressionSnapshot(state.Revision, state.GenerationId, state.Round, state.Seed, state.Wallet,
                state.ThreatCount, state.CurseCount, state.Phase, state.Health, state.MaximumHealth,
                Array.AsReadOnly(choices.ToArray()), offers, Array.AsReadOnly(retained.ToArray()),
                Effects(), state.Message, inShop && !shop.HasPending,
                state.Phase == ProgressionPhase.Ended || state.Phase == ProgressionPhase.GenerationFailed,
                shop.Inventory(permanent), inShop ? shop.Pending?.Id : null, inShop ? shop.Pending?.Title : null,
                inShop ? shop.Price(shop.Pending, active) : 0, (inShop || selection) && rerollReason == null,
                inShop ? shop.RerollPrice(active) : 0, inShop ? shop.FreeRerolls(active) : selection ? SelectionRerollsRemaining() : 0,
                rerollReason, inShop ? shrines.Deal() : default);
        }

        private void BeginNextRound()
        {
            if (state.Round == int.MaxValue)
            {
                state.Phase = ProgressionPhase.GenerationFailed;
                state.Message = "The supported floor index has been exhausted.";
                state.Revision++;
                return;
            }
            state.Round++;
            state.RoundSeed = random.Next();
            state.IsShop = shrines.BargainMarked || (state.CompletedCombatFloors > 0 &&
                state.CompletedCombatFloors - state.LastShopAtCombatCount >= config.ShopInterval);
            if (state.IsShop) state.LastShopAtCombatCount = state.CompletedCombatFloors;
            state.ThreatRerollsUsed = state.CurseRerollsUsed = 0;
            if (state.IsShop)
            {
                shop = new ShopController(state.Shop, shopRules, shopCatalogue, new System.Random(state.RoundSeed));
                shop.BeginVisit(state.Round, Active());
            }
            state.OfferedThreatIds.Clear();
            state.OfferedCurseIds.Clear();
            state.CollectedGoldenAnchors.Clear();
            if (state.IsShop || state.CompletedCombatFloors % config.SelectionInterval != 0) BeginGeneration();
            else if (!HasEligibleThreat()) BeginCurseSelection(null);
            else
            {
                BuildThreatChoices();
                state.Phase = ProgressionPhase.ChooseThreat;
                state.Message = "Choose what follows you onto floor " + state.Round + ".";
                state.Revision++;
            }
        }

        private void BeginGeneration()
        {
            if (state.GenerationId == int.MaxValue)
            {
                state.Phase = ProgressionPhase.GenerationFailed;
                state.Message = "The supported generation identity has been exhausted.";
            }
            else
            {
                state.Health = state.MaximumHealth;
                state.GenerationId++;
                state.Phase = ProgressionPhase.Generating;
                state.Message = state.IsShop ? "Finding a safe room..." : "The next floor is taking shape...";
            }
            state.Revision++;
        }

        public ProgressionEffectsSnapshot EffectsSnapshot() => new ProgressionEffectsSnapshot(Snapshot(),
            Active());

        private void ApplyEntry(ProgressionEntryConfig entry, EffectKind kind)
        {
            state.SelectionCounts[entry.Id] = Count(entry.Id) + 1;
            // Immediate legacy healing is spent on purchase, not a held consumable.
            if (kind != EffectKind.Consumable || entry.Healing == 0f)
            {
                int stacks = state.ActiveEffectEntries.TryGetValue(entry.Id, out var active) ? active.StackCount : 0;
                state.ActiveEffectEntries[entry.Id] = new ActiveEffect(new EffectId(entry.Id), kind, stacks + 1);
            }
            state.Traits |= entry.Traits;
            bool playerOwned = !(catalogue is null) && EffectCatalogueUtility.Find(catalogue, entry.Id) != null;
            state.MovementSpeedMultiplier = Clamp(state.MovementSpeedMultiplier * (playerOwned ? 1d : entry.MovementSpeedMultiplier),
                config.MinimumMultiplier, config.MaximumMovementMultiplier);
            state.HunterSpeedMultiplier = Clamp(state.HunterSpeedMultiplier * (double)entry.HunterSpeedMultiplier,
                config.MinimumMultiplier, config.MaximumHunterMultiplier);
            state.FogDensityMultiplier = Clamp(state.FogDensityMultiplier * (double)entry.FogDensityMultiplier,
                config.MinimumMultiplier, config.MaximumFogMultiplier);
            state.FlashlightRangeMultiplier = Clamp(state.FlashlightRangeMultiplier * (double)entry.FlashlightRangeMultiplier,
                config.MinimumMultiplier, config.MaximumFlashlightMultiplier);
            state.MaximumHealth = Clamp(state.MaximumHealth + (playerOwned ? 0d : entry.MaximumHealthDelta),
                config.MinimumMaximumHealth, config.MaximumMaximumHealth);
            state.Health = Clamp(state.Health + (double)entry.Healing, 0f, state.MaximumHealth);
        }

        private ProgressionEffects Effects() => new ProgressionEffects(state.MovementSpeedMultiplier,
            state.HunterSpeedMultiplier, state.FogDensityMultiplier, state.FlashlightRangeMultiplier,
            state.MaximumHealth, state.Health, state.IsShop ? 0 : state.ThreatCount,
            state.Traits, state.WaxWardCharges, Array.AsReadOnly(state.ActiveThreatIds.ToArray()));

        private void BuildThreatChoices()
        {
            state.OfferedThreatIds.Clear();
            // Derive offers from the committed round seed without consuming layout randomness.
            int offset = (int)(((long)(uint)state.RoundSeed + state.ThreatRerollsUsed) % config.Threats.Count);
            for (int index = 0; index < config.Threats.Count && state.OfferedThreatIds.Count < MaximumChoices; index++)
            {
                ProgressionEntryConfig entry = config.Threats[(offset + index) % config.Threats.Count];
                if (EligibleEntry(entry, EffectKind.Threat)) state.OfferedThreatIds.Add(entry.Id);
            }
        }

        private bool HasEligibleThreat()
        {
            foreach (ProgressionEntryConfig entry in config.Threats)
                if (EligibleEntry(entry, EffectKind.Threat)) return true;
            return false;
        }

        private void BeginCurseSelection(string preferredThreat)
        {
            BuildCurseChoices(preferredThreat);
            if (state.OfferedCurseIds.Count == 0)
            {
                BeginGeneration();
                state.Message = "All eligible curses are already carried. The next floor is taking shape...";
                return;
            }
            state.Phase = ProgressionPhase.ChooseCurse;
            state.Message = "Choose a curse to carry into the dark.";
            state.Revision++;
        }

        private void BuildCurseChoices(string preferredThreat)
        {
            state.OfferedCurseIds.Clear();
            // Reuse the committed floor seed; UI reads and purchases never draw layout randomness.
            int offset = (int)(((long)(uint)state.RoundSeed + state.CurseRerollsUsed) % curses.Count);
            // Preserve a real map choice beside hunter-specific choices while both pools remain.
            AppendCurseChoice(offset, null, true);
            if (!string.IsNullOrEmpty(preferredThreat)) AppendCurseChoice(offset, preferredThreat, false);
            for (int index = 0; index < curses.Count && state.OfferedCurseIds.Count < MaximumChoices; index++)
            {
                ProgressionEntryConfig entry = curses[(offset + index) % curses.Count];
                if (EligibleCurse(entry) && !state.OfferedCurseIds.Contains(entry.Id)) state.OfferedCurseIds.Add(entry.Id);
            }
        }

        private void AppendCurseChoice(int offset, string threatId, bool general)
        {
            for (int index = 0; index < curses.Count; index++)
            {
                ProgressionEntryConfig entry = curses[(offset + index) % curses.Count];
                var data = EffectCatalogueUtility.Find(catalogue, entry.Id);
                bool matches = general ? string.IsNullOrEmpty(entry.RequiredThreatId) && (data == null || data.RequiredHunterIds.Count == 0)
                    : entry.RequiredThreatId == threatId || (data != null && Contains(data.RequiredHunterIds, threatId));
                if (!matches || !EligibleCurse(entry)) continue;
                state.OfferedCurseIds.Add(entry.Id);
                return;
            }
        }

        private bool EligibleCurse(ProgressionEntryConfig entry) => EligibleEntry(entry, EffectKind.Curse);

        private bool EligibleEntry(ProgressionEntryConfig entry, EffectKind kind)
        {
            if (!string.IsNullOrEmpty(entry.RequiredThreatId) && !state.ActiveThreatIds.Contains(entry.RequiredThreatId)) return false;
            var data = EffectCatalogueUtility.Find(catalogue, entry.Id);
            return data == null ? kind != EffectKind.Curse || Count(entry.Id) == 0
                : EffectCatalogueUtility.Eligible(data, state.Round, new ActiveEffects(state.ActiveEffectEntries.Values));
        }

        private EffectKind LegacyKind(ProgressionEntryConfig entry) => Find(config.Threats, entry.Id) != null ? EffectKind.Threat
            : Find(config.Curses, entry.Id) != null ? EffectKind.Curse
            : EffectCatalogueUtility.Find(catalogue, entry.Id)?.Kind ?? (entry.Repeatable ? EffectKind.Consumable : EffectKind.Upgrade);


        private static bool Contains(IReadOnlyList<string> values, string id)
        { foreach (string value in values) if (value == id) return true; return false; }


        private bool Matches(ProgressionPhase phase, int revision) => state.Phase == phase && state.Revision == revision;
        private bool MatchesGeneration(ProgressionPhase phase, int generationId) => state.Phase == phase && state.GenerationId == generationId;
        private int Count(string id) => state.SelectionCounts.TryGetValue(id, out int value) ? value : 0;
        private bool Reject(string message) { state.Message = message; state.Revision++; return false; }
        private static float Clamp(double value, float minimum, float maximum) => (float)Math.Max(minimum, Math.Min(maximum, value));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void AppendSelections(List<ProgressionSelection> target, IReadOnlyList<ProgressionEntryConfig> catalog, ProgressionChoiceKind kind)
        {
            foreach (ProgressionEntryConfig entry in catalog)
                if (Count(entry.Id) > 0) target.Add(new ProgressionSelection(entry.Id, entry.Title, kind, Count(entry.Id)));
        }

        private static ProgressionEntryConfig Find(IReadOnlyList<ProgressionEntryConfig> catalog, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (ProgressionEntryConfig entry in catalog)
                if (string.Equals(entry.Id, id, StringComparison.Ordinal)) return entry;
            return null;
        }

        private static void ValidateConfig(ProgressionConfig value)
        {
            if (value.ShopInterval < 2 || value.SelectionInterval < 1 || value.GoldenCakeValue < 1 ||
                !Finite(value.EarlyBailWalletFraction) || value.EarlyBailWalletFraction < 0f || value.EarlyBailWalletFraction > 1f ||
                !Finite(value.InitialMaximumHealth) || !Finite(value.MinimumMaximumHealth) || !Finite(value.MaximumMaximumHealth) ||
                value.MinimumMaximumHealth <= 0f || value.InitialMaximumHealth < value.MinimumMaximumHealth ||
                value.InitialMaximumHealth > value.MaximumMaximumHealth || !Finite(value.MinimumMultiplier) ||
                value.MinimumMultiplier <= 0f || value.MinimumMultiplier > 1f)
                throw new ArgumentException("Progression health, cadence or wallet configuration is invalid.", nameof(value));
            foreach (float maximum in new[] { value.MaximumMovementMultiplier, value.MaximumHunterMultiplier,
                value.MaximumFogMultiplier, value.MaximumFlashlightMultiplier })
                if (!Finite(maximum) || maximum < 1f)
                    throw new ArgumentException("Progression multiplier limits must be finite and at least one.", nameof(value));
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            ValidateCatalog(value.Threats, identifiers, false);
            ValidateCatalog(value.Curses, identifiers, false);
            ValidateCatalog(value.Offers, identifiers, true);
            foreach (ProgressionEntryConfig curse in value.Curses)
                if (!string.IsNullOrEmpty(curse.RequiredThreatId) && Find(value.Threats, curse.RequiredThreatId) == null)
                    throw new ArgumentException("A curse requires a hunter missing from the threat catalog.", nameof(value));
        }

        private static void ValidateCatalog(IReadOnlyList<ProgressionEntryConfig> catalog, HashSet<string> identifiers, bool allowEmpty)
        {
            if (catalog == null || (!allowEmpty && catalog.Count == 0))
                throw new ArgumentException("Required progression catalog is empty.", nameof(catalog));
            foreach (ProgressionEntryConfig entry in catalog)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Title) ||
                    !identifiers.Add(entry.Id) || entry.Price < 0 || !Finite(entry.Healing) || entry.Healing < 0f ||
                    !Finite(entry.MaximumHealthDelta) || entry.StockPerVisit < 1 ||
                    (entry.GrantsWaxWard && (!entry.Repeatable || entry.Healing > 0f || entry.Traits != ProgressionTraits.None)))
                    throw new ArgumentException("Progression entries need unique identifiers and valid rewards.", nameof(catalog));
                foreach (float multiplier in new[] { entry.MovementSpeedMultiplier, entry.HunterSpeedMultiplier,
                    entry.FogDensityMultiplier, entry.FlashlightRangeMultiplier })
                    if (!Finite(multiplier) || multiplier <= 0f)
                        throw new ArgumentException("Progression entry multipliers must be finite and positive.", nameof(catalog));
            }
        }
    }
}
