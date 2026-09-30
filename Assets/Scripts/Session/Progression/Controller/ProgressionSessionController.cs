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
//   - Retain current-floor health for reporting, but refill it after choices before each generation.
//   - Advance independent selection and shop clocks using completed combat floors.
//   - Debit the configured bail penalty once, inside generation-guarded completion.
//   - Commit catalogue stacks alongside legacy traits, stock and one-charge wards.
//   - Leave catalogue health/sprint effects to Player; legacy baselines stay unmodified.
//   - Produce immutable snapshots and deterministic per-round generation inputs.
//   - Commit at most three eligible hunter/curse choices and skip exhausted menus.
// DEPENDENCIES:
//   - Own Config and BehaviorState; Core progression value contracts.
//   - Injected System.Random; no scene or foreign gameplay systems.
// USAGE NOTES:
//   Construct with a fresh seeded random source when starting/restarting a run.
//   Generation and UI identities remain monotonic in the reused state. Exactly
//   one random draw occurs per round; purchases and health do not alter layouts.
//   CompleteFloor defaults to a normal escape. The flagged overload is the only
//   bail entry; ApplyBailPenalty is the extension point for a future curse cost.
// ============================================================================
using System;
using System.Collections.Generic;
using Worsen.Core;

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

        public ProgressionSessionController(ProgressionSessionBehaviorState state, ProgressionConfig config, System.Random random)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            ValidateConfig(config);
            catalogue = config.EffectCatalogue;
            var combined = new List<ProgressionEntryConfig>(config.Curses);
            if (!(catalogue is null))
            {
                var hunters = new List<string>();
                foreach (var entry in config.Threats) hunters.Add(entry.Id);
                EffectCatalogueUtility.Validate(catalogue, hunters);
                foreach (var entry in catalogue.Entries)
                {
                    var legacy = Find(config.Threats, entry.Id) ?? Find(config.Curses, entry.Id) ?? Find(config.Offers, entry.Id);
                    if (legacy != null && (entry.Kind != LegacyKind(legacy) ||
                        (Find(config.Offers, entry.Id) != null && entry.Kind < EffectKind.Upgrade)))
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
            state.VisitPurchaseCounts.Clear();
            state.OfferedThreatIds.Clear();
            state.OfferedCurseIds.Clear();
            state.ActiveThreatIds.Clear();
            state.Wallet = state.ThreatCount = state.CurseCount = 0;
            state.Health = state.MaximumHealth = config.InitialMaximumHealth;
            state.MovementSpeedMultiplier = state.HunterSpeedMultiplier = 1f;
            state.FogDensityMultiplier = state.FlashlightRangeMultiplier = 1f;
            state.SelectionCounts.Clear();
            state.ActiveEffectEntries.Clear();
            state.PurchasedOfferIds.Clear();
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
            state.CompletedCombatFloors++;
            BeginNextRound();
            return true;
        }

        private void ApplyBailPenalty()
        {
            // Future permanent-curse costs belong here, inside the same completion guard.
            int debit = (int)Math.Floor(state.Wallet * (double)config.EarlyBailWalletFraction);
            state.Wallet -= debit;
        }

        public bool ContinueShop(int revision)
        {
            if (!Matches(ProgressionPhase.Shop, revision)) return false;
            BeginNextRound();
            return true;
        }

        public bool RecordGoldenCollected(int generationId, int anchorId)
        {
            if (!MatchesGeneration(ProgressionPhase.Exploring, generationId) || anchorId < 0 ||
                state.CollectedGoldenAnchors.Contains(anchorId)) return false;
            if (state.Wallet > int.MaxValue - config.GoldenCakeValue) return false;
            state.CollectedGoldenAnchors.Add(anchorId);
            state.Wallet += config.GoldenCakeValue;
            state.Revision++;
            return true;
        }

        public bool Purchase(string id, int revision)
        {
            if (!Matches(ProgressionPhase.Shop, revision)) return false;
            ProgressionEntryConfig offer = Find(config.Offers, id);
            if (offer == null) return Reject("That offer is unavailable.");
            string unavailable = OfferUnavailableReason(offer);
            if (unavailable != null) return Reject(unavailable);
            state.Wallet -= offer.Price;
            state.VisitPurchaseCounts[id] = VisitCount(id) + 1;
            if (!Repeatable(offer)) state.PurchasedOfferIds.Add(id);
            if (offer.GrantsWaxWard) state.WaxWardCharges = 1;
            ApplyEntry(offer, LegacyKind(offer));
            state.Message = offer.Title + " purchased.";
            state.Revision++;
            return true;
        }

        public bool TryConsumeWaxWard(int generationId)
        {
            if (!MatchesGeneration(ProgressionPhase.Exploring, generationId) || state.WaxWardCharges != 1) return false;
            state.WaxWardCharges = 0;
            state.Message = "Your Wax Ward broke the shadow's grip.";
            foreach (var offer in config.Offers)
                if (offer.GrantsWaxWard) state.ActiveEffectEntries.Remove(offer.Id);
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
            var offers = new List<ProgressionOffer>();
            if (state.Phase == ProgressionPhase.Shop)
                foreach (ProgressionEntryConfig entry in config.Offers)
                {
                    int stock = RemainingStock(entry);
                    bool purchased = (!Repeatable(entry) && state.PurchasedOfferIds.Contains(entry.Id)) || stock == 0;
                    string reason = OfferUnavailableReason(entry);
                    offers.Add(new ProgressionOffer(entry.Id, entry.Title, entry.Description, entry.Price,
                        purchased, reason == null, stock, Repeatable(entry), reason));
                }
            var retained = new List<ProgressionSelection>();
            AppendSelections(retained, config.Threats, ProgressionChoiceKind.Threat);
            AppendSelections(retained, curses, ProgressionChoiceKind.Curse);
            AppendSelections(retained, config.Offers, ProgressionChoiceKind.Upgrade);
            return new ProgressionSnapshot(state.Revision, state.GenerationId, state.Round, state.Seed, state.Wallet,
                state.ThreatCount, state.CurseCount, state.Phase, state.Health, state.MaximumHealth,
                Array.AsReadOnly(choices.ToArray()), Array.AsReadOnly(offers.ToArray()), Array.AsReadOnly(retained.ToArray()),
                Effects(), state.Message, state.Phase == ProgressionPhase.Shop,
                state.Phase == ProgressionPhase.Ended || state.Phase == ProgressionPhase.GenerationFailed);
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
            state.IsShop = state.CompletedCombatFloors > 0 &&
                state.CompletedCombatFloors - state.LastShopAtCombatCount >= config.ShopInterval;
            if (state.IsShop) state.LastShopAtCombatCount = state.CompletedCombatFloors;
            state.VisitPurchaseCounts.Clear();
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
            new ActiveEffects(state.ActiveEffectEntries.Values));

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
            int offset = (int)((uint)state.RoundSeed % (uint)config.Threats.Count);
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
            int offset = (int)((uint)state.RoundSeed % (uint)curses.Count);
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

        private bool Repeatable(ProgressionEntryConfig entry)
        {
            var data = EffectCatalogueUtility.Find(catalogue, entry.Id);
            return data == null ? entry.Repeatable : data.StackCap > 1 || data.Kind == EffectKind.Consumable;
        }

        private static bool Contains(IReadOnlyList<string> values, string id)
        { foreach (string value in values) if (value == id) return true; return false; }

        private int VisitCount(string id) => state.VisitPurchaseCounts.TryGetValue(id, out int value) ? value : 0;
        private int RemainingStock(ProgressionEntryConfig entry) => !Repeatable(entry) && state.PurchasedOfferIds.Contains(entry.Id)
            ? 0 : Math.Max(0, entry.StockPerVisit - VisitCount(entry.Id));

        private string OfferUnavailableReason(ProgressionEntryConfig entry)
        {
            if (!Repeatable(entry) && state.PurchasedOfferIds.Contains(entry.Id)) return "Already owned for this expedition.";
            if (RemainingStock(entry) == 0) return "Sold out for this visit.";
            if (entry.GrantsWaxWard && state.WaxWardCharges > 0) return "You already carry a Wax Ward.";
            if (!EligibleEntry(entry, LegacyKind(entry))) return "Requirements, availability or stack cap not met.";
            if (entry.Healing > 0f && state.Health >= state.MaximumHealth) return "Your health is already full.";
            if (state.Wallet < entry.Price) return "Not enough Golden Cakes.";
            return null;
        }

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
