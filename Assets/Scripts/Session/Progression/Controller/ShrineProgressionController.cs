// ============================================================================
// ShrineProgressionController.cs
// ============================================================================
// PURPOSE:
//   Resolves shrine effects against the catalogue without commanding world systems.
//   Progression commits returned wallet costs and Player grants before publishing facts.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Keep Chance floor-scoped, Bargain single-payment and history run-scoped.
//   - Schedule Pacification noise on injected time and describe external effect intents.
//   - Resolve Echo as an ordinary second use and expose Purgatory's scaled yield.
// DEPENDENCIES:
//   - Own state/rules/catalogue, Core values and a separate injected random stream.
// USAGE NOTES:
//   No layout randomness is consumed. Chance draws general curses and eligible upgrades;
//   Bargain draws general curses. Hunter requirements still apply to upgrades.
//   Failed activations consume their floor-local identity but do not become Echo history.
//   One deal per escaped floor; repeated Bargains coalesce at the highest payout multiplier.
// ============================================================================
using System;
using System.Collections.Generic;
using Worsen.Core;
namespace Worsen.Session.Progression
{
    public sealed class ShrineProgressionController
    {
        private readonly ShrineProgressionBehaviorState state;
        private readonly ShrineProgressionRules rules;
        private readonly EffectCatalogueConfig catalogue;
        private readonly System.Random random;
        public ShrineProgressionController(ShrineProgressionBehaviorState state, ShrineProgressionRules rules,
            EffectCatalogueConfig catalogue, System.Random random)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.catalogue = catalogue;
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (rules.ProtectionCost < 0 || rules.CostFloorInterval < 1 || rules.BargainOffers < 1 ||
                rules.BargainBase < 0 || rules.BargainFloorInterval < 1 || rules.MutationChance > 1f)
                throw new ArgumentException("Invalid shrine counts or mutation probability.");
            foreach (float value in new[] { rules.ShieldHitPoints, rules.PacificationDelay, rules.PacificationLoudness,
                rules.WickSeconds, rules.MutationChance })
                if (!float.IsFinite(value) || value < 0f) throw new ArgumentException("Invalid shrine tuning.");
            var kinds = new HashSet<ShrineKind>();
            foreach (var entry in rules.Echo)
                if (entry == null || !kinds.Add(entry.Kind) || entry.Kind == ShrineKind.Echo || entry.Magnitude < 1 ||
                    entry.CostMultiplier < 1 || !float.IsFinite(entry.DelayMultiplier) || entry.DelayMultiplier < 0f)
                    throw new ArgumentException("Invalid Echo table.");
        }
        public bool BargainMarked => state.BargainMarked;
        public float YieldMultiplier => state.YieldMultiplier;
        public IReadOnlyList<ShrineResolvedFact> History => Array.AsReadOnly(state.History.ToArray());
        public ActiveEffects FloorEffects => new ActiveEffects(state.FloorEffects.Values);
        public void ResetRun()
        { EndFloor(); state.History.Clear(); CloseDeal(); }
        public void EndFloor()
        {
            state.Seen.Clear(); state.FloorEffects.Clear(); state.Noises.Clear();
            state.Clock = 0d; state.LastTick = -1; state.YieldMultiplier = 1f;
        }
        public ActiveEffects Combined(IReadOnlyActiveEffects retained)
        {
            var result = new Dictionary<string, ActiveEffect>();
            foreach (var effect in retained) result.Add(effect.Id.Value, effect);
            foreach (var effect in state.FloorEffects.Values)
            {
                int count = result.TryGetValue(effect.Id.Value, out var existing) ? existing.StackCount : 0;
                result[effect.Id.Value] = new ActiveEffect(effect.Id, effect.Kind, count + effect.StackCount);
            }
            return new ActiveEffects(result.Values);
        }
        public bool Resolve(int generation, int floor, ShrineActivatedFact activation, int wallet,
            float shieldCapacity, float collectedFraction, IReadOnlyActiveEffects retained, out ShrineResolvedFact result,
            bool extraLifeConsumed = false)
        {
            result = default;
            if (activation.ShrineId < 0 || activation.Tick < 0 || activation.Kind < ShrineKind.Chance ||
                activation.Kind > ShrineKind.Purgatory || !state.Seen.Add(activation.ShrineId)) return false;
            var kind = activation.Kind;
            bool changed = kind == ShrineKind.Echo;
            ShrineEchoRule echo = null;
            if (changed)
            {
                if (state.History.Count == 0) return false;
                kind = state.History[state.History.Count - 1].ResolvedKind;
                foreach (var entry in rules.Echo) if (entry.Kind == kind) { echo = entry; break; }
                if (echo == null) return false;
            }
            int magnitude = echo?.Magnitude ?? 1, costMultiplier = echo?.CostMultiplier ?? 1;
            int cost = 0;
            float shield = 0f, wick = 0f, yield = 1f;
            bool drop = false, mutation = false;
            int hunters = 0;
            switch (kind)
            {
                case ShrineKind.Chance:
                    for (int i = 0; i < magnitude; i++)
                    {
                        var candidates = Candidates(floor, Combined(retained), false);
                        if (extraLifeConsumed) candidates.RemoveAll(entry => entry.Id == "extra-life");
                        if (candidates.Count == 0) { if (i == 0) return false; break; }
                        var entry = candidates[random.Next(candidates.Count)];
                        int count = state.FloorEffects.TryGetValue(entry.Id, out var active) ? active.StackCount : 0;
                        state.FloorEffects[entry.Id] = new ActiveEffect(new EffectId(entry.Id), entry.Kind, count + 1);
                    }
                    break;
                case ShrineKind.Bargain:
                    state.BargainMarked = true; state.BargainFloor = floor;
                    state.BargainMultiplier = Math.Max(state.BargainMultiplier, magnitude);
                    break;
                case ShrineKind.Protection:
                    long price = ((long)rules.ProtectionCost + Math.Max(0, floor) / rules.CostFloorInterval) * costMultiplier;
                    shield = rules.ShieldHitPoints * magnitude;
                    if (price > wallet || !float.IsFinite(shield) || shield <= 0f || float.IsNaN(shieldCapacity) || shieldCapacity < shield) return false;
                    cost = (int)price;
                    break;
                case ShrineKind.Pacification:
                    drop = true;
                    state.Noises.Add((activation, state.Clock + rules.PacificationDelay * (echo?.DelayMultiplier ?? 1f),
                        rules.PacificationLoudness * magnitude));
                    break;
                case ShrineKind.Wick: wick = rules.WickSeconds * magnitude; break;
                case ShrineKind.Passage: yield = magnitude; break;
                case ShrineKind.Purgatory:
                    if (!float.IsFinite(collectedFraction)) return false;
                    yield = PurgatoryYield(collectedFraction);
                    state.YieldMultiplier = Math.Max(state.YieldMultiplier, yield);
                    hunters = 1; mutation = random.NextDouble() < Math.Min(1d, rules.MutationChance * magnitude);
                    break;
            }
            result = new ShrineResolvedFact(generation, activation, kind, false, cost, shield, drop, wick, yield, hunters, mutation);
            state.History.Add(result);
            return true;
        }
        public static float PurgatoryYield(float collectedFraction)
        {
            if (!float.IsFinite(collectedFraction)) throw new ArgumentOutOfRangeException(nameof(collectedFraction));
            return Math.Min(2f, 1f + (1f - Math.Max(0f, Math.Min(1f, collectedFraction))));
        }
        private List<EffectCatalogueEntry> Candidates(int floor, IReadOnlyActiveEffects active, bool cursesOnly)
        {
            var result = new List<EffectCatalogueEntry>();
            if (!(catalogue is null)) foreach (var entry in catalogue.Entries)
                if (((entry.Kind == EffectKind.Curse && entry.RequiredHunterIds.Count == 0) ||
                    (!cursesOnly && entry.Kind == EffectKind.Upgrade)) &&
                    EffectCatalogueUtility.Eligible(entry, floor, active)) result.Add(entry);
            return result;
        }
        public void OpenDeal(IReadOnlyActiveEffects retained)
        {
            if (!state.BargainMarked || state.Offers.Count != 0) return;
            var candidates = Candidates(state.BargainFloor, retained, true);
            while (candidates.Count > 0 && state.Offers.Count < rules.BargainOffers)
            {
                int index = random.Next(candidates.Count);
                var entry = candidates[index]; candidates.RemoveAt(index);
                decimal payout = (decimal)Math.Max(1, entry.Value) * (rules.BargainBase + (long)state.BargainFloor / rules.BargainFloorInterval);
                int capped = (int)Math.Min(int.MaxValue, payout * state.BargainMultiplier);
                state.Offers.Add(new ShrineDealOffer(entry.Id, entry.Title, entry.CardCopy, capped));
            }
        }
        public ShrineDealSnapshot Deal() => new ShrineDealSnapshot(state.BargainMarked, state.BargainFloor,
            Array.AsReadOnly(state.Offers.ToArray()));
        public bool TakeDeal(string id, int wallet, IReadOnlyActiveEffects active, out ActiveEffect curse, out int payout)
        {
            curse = default; payout = 0;
            if (!state.BargainMarked) return false;
            foreach (var offer in state.Offers)
            {
                if (offer.Id != id || (long)wallet + offer.Payout > int.MaxValue) continue;
                var entry = EffectCatalogueUtility.Find(catalogue, id);
                if (!EffectCatalogueUtility.Eligible(entry, state.BargainFloor, active)) return false;
                curse = new ActiveEffect(new EffectId(id), EffectKind.Curse, active.Stacks(new EffectId(id)) + 1);
                payout = offer.Payout;
                CloseDeal(); return true;
            }
            return false;
        }
        public void CloseDeal()
        { state.BargainMarked = false; state.BargainFloor = 0; state.BargainMultiplier = 1; state.Offers.Clear(); }
        public IReadOnlyList<NoiseEvent> Tick(float dt, long tick)
        {
            var output = new List<NoiseEvent>();
            if (!float.IsFinite(dt) || dt <= 0f || tick <= state.LastTick) return output.AsReadOnly();
            state.Clock += dt; state.LastTick = tick;
            for (int i = 0; i < state.Noises.Count;)
            {
                var scheduled = state.Noises[i];
                if (state.Clock < scheduled.Due) { i++; continue; }
                output.Add(new NoiseEvent(EntityId.None, scheduled.Fact.Position, scheduled.Loudness, tick, NoiseSourceKind.Shrine));
                state.Noises.RemoveAt(i);
            }
            return output.AsReadOnly();
        }
    }
}
