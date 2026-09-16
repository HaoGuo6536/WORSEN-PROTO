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
//   - Advance combat floors and every-fourth-floor shops from explicit facts.
//   - Apply retained threat, curse and upgrade effects without engine calls.
//   - Produce immutable snapshots and deterministic per-round generation inputs.
//   - Show truthful choice descriptions when the active hunter limit is reached.
// DEPENDENCIES:
//   - Own Config and BehaviorState; Core progression value contracts.
//   - Injected System.Random; no scene or foreign gameplay systems.
// USAGE NOTES:
//   Construct with a fresh seeded random source when starting/restarting a run.
//   Generation and UI identities remain monotonic in the reused state. Exactly
//   one random draw occurs per round; purchases and health do not alter layouts.
// ============================================================================
using System;
using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Session.Progression
{
    public sealed class ProgressionSessionController
    {
        private readonly ProgressionSessionBehaviorState state;
        private readonly ProgressionConfig config;
        private readonly System.Random random;

        public ProgressionSessionController(ProgressionSessionBehaviorState state, ProgressionConfig config, System.Random random)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            ValidateConfig(config);
        }

        public void StartRun(int seed)
        {
            state.Seed = seed;
            state.Round = 0;
            state.Wallet = state.ThreatCount = state.CurseCount = 0;
            state.Health = state.MaximumHealth = config.InitialMaximumHealth;
            state.MovementSpeedMultiplier = state.HunterSpeedMultiplier = 1f;
            state.FogDensityMultiplier = state.FlashlightRangeMultiplier = 1f;
            state.SelectionCounts.Clear();
            state.PurchasedOfferIds.Clear();
            state.CollectedGoldenAnchors.Clear();
            BeginNextRound();
        }

        public bool ChooseThreat(string id, int revision)
        {
            if (!Matches(ProgressionPhase.ChooseThreat, revision)) return false;
            ProgressionEntryConfig choice = Find(config.Threats, id);
            if (choice == null) return Reject("That threat is unavailable.");
            ApplyEntry(choice);
            state.ThreatCount++;
            state.Phase = ProgressionPhase.ChooseCurse;
            state.Message = "Choose a curse to carry into the dark.";
            state.Revision++;
            return true;
        }

        public bool ChooseCurse(string id, int revision)
        {
            if (!Matches(ProgressionPhase.ChooseCurse, revision)) return false;
            ProgressionEntryConfig choice = Find(config.Curses, id);
            if (choice == null) return Reject("That curse is unavailable.");
            ApplyEntry(choice);
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

        public bool CompleteFloor(int generationId)
        {
            if (!MatchesGeneration(ProgressionPhase.Exploring, generationId)) return false;
            BeginNextRound();
            return true;
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
            if (state.PurchasedOfferIds.Contains(id)) return Reject("Already purchased on this floor.");
            if (state.Wallet < offer.Price) return Reject("Not enough Golden Cakes.");
            state.Wallet -= offer.Price;
            state.PurchasedOfferIds.Add(id);
            ApplyEntry(offer);
            state.Message = offer.Title + " purchased.";
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
                state.Phase == ProgressionPhase.ChooseCurse ? config.Curses : null;
            if (catalog != null)
                foreach (ProgressionEntryConfig entry in catalog)
                {
                    string description = entry.Description;
                    if (state.Phase == ProgressionPhase.ChooseThreat && state.ThreatCount >= config.MaximumActiveThreats)
                        description = string.IsNullOrWhiteSpace(entry.DescriptionAtThreatCap)
                            ? "Hunter limit reached. No additional hunter joins."
                            : entry.DescriptionAtThreatCap;
                    choices.Add(new ProgressionChoice(entry.Id, entry.Title, description, Count(entry.Id)));
                }
            var offers = new List<ProgressionOffer>();
            if (state.Phase == ProgressionPhase.Shop)
                foreach (ProgressionEntryConfig entry in config.Offers)
                {
                    bool purchased = state.PurchasedOfferIds.Contains(entry.Id);
                    offers.Add(new ProgressionOffer(entry.Id, entry.Title, entry.Description, entry.Price,
                        purchased, !purchased && state.Wallet >= entry.Price));
                }
            var retained = new List<ProgressionSelection>();
            AppendSelections(retained, config.Threats, ProgressionChoiceKind.Threat);
            AppendSelections(retained, config.Curses, ProgressionChoiceKind.Curse);
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
            state.IsShop = state.Round % config.ShopInterval == 0;
            state.PurchasedOfferIds.Clear();
            state.CollectedGoldenAnchors.Clear();
            if (state.IsShop) BeginGeneration();
            else
            {
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
                state.GenerationId++;
                state.Phase = ProgressionPhase.Generating;
                state.Message = state.IsShop ? "Finding a safe room..." : "The next floor is taking shape...";
            }
            state.Revision++;
        }

        private void ApplyEntry(ProgressionEntryConfig entry)
        {
            state.SelectionCounts[entry.Id] = Count(entry.Id) + 1;
            state.MovementSpeedMultiplier = Clamp(state.MovementSpeedMultiplier * (double)entry.MovementSpeedMultiplier,
                config.MinimumMultiplier, config.MaximumMovementMultiplier);
            state.HunterSpeedMultiplier = Clamp(state.HunterSpeedMultiplier * (double)entry.HunterSpeedMultiplier,
                config.MinimumMultiplier, config.MaximumHunterMultiplier);
            state.FogDensityMultiplier = Clamp(state.FogDensityMultiplier * (double)entry.FogDensityMultiplier,
                config.MinimumMultiplier, config.MaximumFogMultiplier);
            state.FlashlightRangeMultiplier = Clamp(state.FlashlightRangeMultiplier * (double)entry.FlashlightRangeMultiplier,
                config.MinimumMultiplier, config.MaximumFlashlightMultiplier);
            state.MaximumHealth = Clamp(state.MaximumHealth + (double)entry.MaximumHealthDelta,
                config.MinimumMaximumHealth, config.MaximumMaximumHealth);
            state.Health = Clamp(state.Health + (double)entry.Healing, 0f, state.MaximumHealth);
        }

        private ProgressionEffects Effects() => new ProgressionEffects(state.MovementSpeedMultiplier,
            state.HunterSpeedMultiplier, state.FogDensityMultiplier, state.FlashlightRangeMultiplier,
            state.MaximumHealth, state.Health, state.IsShop ? 0 : Math.Min(state.ThreatCount, config.MaximumActiveThreats));

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
            if (value.ShopInterval < 2 || value.MaximumActiveThreats < 1 || value.GoldenCakeValue < 1 ||
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
        }

        private static void ValidateCatalog(IReadOnlyList<ProgressionEntryConfig> catalog, HashSet<string> identifiers, bool allowEmpty)
        {
            if (catalog == null || (!allowEmpty && catalog.Count == 0))
                throw new ArgumentException("Required progression catalog is empty.", nameof(catalog));
            foreach (ProgressionEntryConfig entry in catalog)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Title) ||
                    !identifiers.Add(entry.Id) || entry.Price < 0 || !Finite(entry.Healing) || entry.Healing < 0f ||
                    !Finite(entry.MaximumHealthDelta))
                    throw new ArgumentException("Progression entries need unique identifiers and valid rewards.", nameof(catalog));
                foreach (float multiplier in new[] { entry.MovementSpeedMultiplier, entry.HunterSpeedMultiplier,
                    entry.FogDensityMultiplier, entry.FlashlightRangeMultiplier })
                    if (!Finite(multiplier) || multiplier <= 0f)
                        throw new ArgumentException("Progression entry multipliers must be finite and positive.", nameof(catalog));
            }
        }
    }
}
