// ============================================================================
// ProgressionEventController.cs
// ============================================================================
// PURPOSE:
//   Schedules infrequent seeded worsenings before the next floor is assembled.
//   It commits run-owned effects and mutation overrides, leaving spawning and
//   engine work to consumers of Progression's immutable views and facts.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Defer due events past shops, select eligible outcomes and commit each round once.
//   - Retain hazards for combat floors and mutations by archetype without editing profiles.
//   - Queue tell-only facts and one generic message for the next selection or shelter.
// DEPENDENCIES:
//   - Own Progression state/config/catalogue, Core values and Domain Hunter profile data.
//   - Injected System.Random, independent of layout, shop and shrine random streams.
// USAGE NOTES:
//   Reset with a fresh stream per run. Hazard duration includes the event's combat floor;
//   shops suspend it. Empty pools defer rather than announce an unapplied event.
//   Mutation validation mirrors Hunter's static rules and bounds speed overrides;
//   live speed admission remains Hunter-owned. Each tunable mutates once per archetype per run.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Worsen.Core;
using Worsen.Domain.Hunter;
namespace Worsen.Session.Progression
{
    public sealed class ProgressionEventController
    {
        private readonly ProgressionSessionBehaviorState state;
        private readonly ProgressionConfig config;
        private readonly EffectCatalogueConfig catalogue;
        private readonly System.Random random;
        public ProgressionEventController(ProgressionSessionBehaviorState state, ProgressionConfig config,
            EffectCatalogueConfig catalogue, System.Random random)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.catalogue = catalogue;
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (config.FirstEventRound < 8 || config.EventInterval < 2 || config.EventJitter < 0 ||
                config.EventJitter >= config.EventInterval || (long)config.EventInterval + config.EventJitter >= int.MaxValue ||
                config.EventHazardFloors < 1 || !float.IsFinite(config.MaximumMutationSpeedMultiplier) ||
                config.MaximumMutationSpeedMultiplier <= 0f) throw new ArgumentException("Invalid progression event tuning.");
            foreach (var kind in config.EventPool)
                if (!Enum.IsDefined(typeof(ProgressionEventKind), kind)) throw new ArgumentException("Invalid progression event kind.");
        }
        public IReadOnlyList<ProgressionEventFact> History => Array.AsReadOnly(state.EventHistory.ToArray());
        public IReadOnlyDictionary<string, IReadOnlyList<HunterMutation>> RetainedMutations
        {
            get
            {
                var copy = new Dictionary<string, IReadOnlyList<HunterMutation>>(StringComparer.Ordinal);
                foreach (var pair in state.Mutations) copy.Add(pair.Key, Array.AsReadOnly(pair.Value.ToArray()));
                return new ReadOnlyDictionary<string, IReadOnlyList<HunterMutation>>(copy);
            }
        }
        public void ResetRun()
        {
            state.NextEventRound = config.FirstEventRound; state.LastEventCheckRound = 0;
            state.EventFearAxis = FearAxis.None; state.MutationMessagePending = false;
            state.EventHistory.Clear(); state.PendingEvents.Clear(); state.EventHazards.Clear(); state.Mutations.Clear();
        }
        public bool TryTakeFact(out ProgressionEventFact fact)
        {
            fact = default;
            if (state.PendingEvents.Count == 0) return false;
            fact = state.PendingEvents.Dequeue(); return true;
        }
        public void ShowPendingMessage()
        {
            if (!state.MutationMessagePending || (state.Phase != ProgressionPhase.ChooseThreat &&
                state.Phase != ProgressionPhase.ChooseCurse && state.Phase != ProgressionPhase.Shop)) return;
            state.Message = "something is different";
            state.MutationMessagePending = false;
        }
        public void EndCombatFloor()
        {
            foreach (string id in new List<string>(state.EventHazards.Keys))
                if (--state.EventHazards[id] <= 0) state.EventHazards.Remove(id);
        }
        public ActiveEffects Combined(IReadOnlyActiveEffects source)
        {
            var values = new Dictionary<string, ActiveEffect>(StringComparer.Ordinal);
            foreach (var effect in source) values.Add(effect.Id.Value, effect);
            if (!state.IsShop) foreach (string id in state.EventHazards.Keys)
            {
                int count = values.TryGetValue(id, out var old) ? old.StackCount : 0;
                int cap = EffectCatalogueUtility.Find(catalogue, id).StackCap;
                values[id] = new ActiveEffect(new EffectId(id), EffectKind.Curse, (int)Math.Min(cap, (long)count + 1));
            }
            return new ActiveEffects(values.Values);
        }
        public bool BeginRound(IReadOnlyActiveEffects active)
        {
            if (state.Round <= state.LastEventCheckRound) return false;
            state.LastEventCheckRound = state.Round; state.EventFearAxis = FearAxis.None;
            if (state.IsShop || state.Round < state.NextEventRound || state.Round < config.FirstEventRound) return false;
            var hazards = new List<EffectCatalogueEntry>();
            var upgrades = new List<EffectCatalogueEntry>();
            if (!(catalogue is null)) foreach (var entry in catalogue.Entries)
            {
                // Nothing??? compounds at shops, so it is not a reversible floor hazard.
                if (entry.Kind != EffectKind.Curse || entry.Id == "nothing" ||
                    !EffectCatalogueUtility.Eligible(entry, state.Round, active)) continue;
                if (entry.RequiredHunterIds.Count == 0 && !state.EventHazards.ContainsKey(entry.Id)) hazards.Add(entry);
                else if (entry.RequiredHunterIds.Count > 0) upgrades.Add(entry);
            }
            var mutations = MutationCandidates();
            var available = new List<ProgressionEventKind>();
            if (hazards.Count > 0) available.Add(ProgressionEventKind.EnvironmentalHazard);
            if (upgrades.Count > 0) available.Add(ProgressionEventKind.HunterUpgrade);
            if (state.ActiveThreatIds.Count > 0) available.Add(ProgressionEventKind.ExtraHunter);
            if (mutations.Count > 0) available.Add(ProgressionEventKind.HiddenMutation);
            var pool = new List<ProgressionEventKind>();
            foreach (var entry in config.EventPool)
                if (available.Contains(entry) || (entry == ProgressionEventKind.Random && available.Count > 0)) pool.Add(entry);
            if (pool.Count == 0) return false;
            var kind = pool[random.Next(pool.Count)];
            var resolved = kind == ProgressionEventKind.Random ? available[random.Next(available.Count)] : kind;
            string effectId = null, archetype = null, tell = null;
            FearAxis axis = FearAxis.Unpredictability;
            if (resolved == ProgressionEventKind.EnvironmentalHazard || resolved == ProgressionEventKind.HunterUpgrade)
            {
                var candidates = resolved == ProgressionEventKind.EnvironmentalHazard ? hazards : upgrades;
                var entry = candidates[random.Next(candidates.Count)];
                effectId = entry.Id; axis = entry.Axis;
                if (resolved == ProgressionEventKind.EnvironmentalHazard) state.EventHazards.Add(entry.Id, config.EventHazardFloors);
                else
                {
                    var targets = new List<string>();
                    foreach (string id in state.ActiveThreatIds)
                        if (Contains(entry.RequiredHunterIds, id) && !targets.Contains(id)) targets.Add(id);
                    archetype = targets[random.Next(targets.Count)];
                    AddRetained(entry.Id, EffectKind.Curse); state.CurseCount++;
                }
            }
            else if (resolved == ProgressionEventKind.ExtraHunter)
            {
                archetype = state.ActiveThreatIds[random.Next(state.ActiveThreatIds.Count)];
                effectId = archetype;
                axis = EffectCatalogueUtility.Find(catalogue, archetype)?.Axis ?? FearAxis.Unpredictability;
                state.ActiveThreatIds.Add(archetype); state.ThreatCount = state.ActiveThreatIds.Count;
                AddRetained(archetype, EffectKind.Threat);
            }
            else
            {
                var candidate = mutations[random.Next(mutations.Count)];
                archetype = candidate.Key; tell = candidate.Mutation.TellId;
                if (!state.Mutations.TryGetValue(archetype, out var retained))
                { retained = new List<HunterMutation>(); state.Mutations.Add(archetype, retained); }
                retained.Add(candidate.Mutation); state.MutationMessagePending = true;
            }
            var fact = new ProgressionEventFact(state.EventHistory.Count + 1, state.Round, kind, resolved, axis, effectId, archetype, tell);
            state.EventFearAxis = axis; state.EventHistory.Add(fact); state.PendingEvents.Enqueue(fact);
            state.NextEventRound = (long)state.Round + random.Next(config.EventInterval - config.EventJitter,
                config.EventInterval + config.EventJitter + 1);
            return true;
        }
        private void AddRetained(string id, EffectKind kind)
        {
            int count = state.ActiveEffectEntries.TryGetValue(id, out var old) ? old.StackCount : 0;
            state.ActiveEffectEntries[id] = new ActiveEffect(new EffectId(id), kind, count + 1);
            state.SelectionCounts[id] = state.SelectionCounts.TryGetValue(id, out int selected) ? selected + 1 : 1;
        }
        private List<(string Key, HunterMutation Mutation)> MutationCandidates()
        {
            var result = new List<(string, HunterMutation)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var profile in config.MutationProfiles)
            {
                if (profile is null || !state.ActiveThreatIds.Contains(profile.ArchetypeKey) ||
                    !seen.Add(profile.ArchetypeKey) || profile.MutationPool == null) continue;
                foreach (var entry in profile.MutationPool)
                {
                    if (entry == null || !ValidMutation(profile, entry.Mutation)) continue;
                    bool used = false;
                    if (state.Mutations.TryGetValue(profile.ArchetypeKey, out var retained))
                        foreach (var old in retained) used |= old.Tunable == entry.Mutation.Tunable;
                    if (!used) result.Add((profile.ArchetypeKey, entry.Mutation));
                }
            }
            return result;
        }
        private bool ValidMutation(HunterProfile profile, HunterMutation mutation)
        {
            if (!Enum.IsDefined(typeof(HunterTunable), mutation.Tunable) || !float.IsFinite(mutation.Value) ||
                mutation.Value < 0f || string.IsNullOrWhiteSpace(mutation.TellId)) return false;
            if (mutation.Tunable == HunterTunable.ChaseSpeedMultiplier && mutation.Value > config.MaximumMutationSpeedMultiplier) return false;
            if (profile.NeverLoses && (mutation.Tunable == HunterTunable.LossSeconds || mutation.Tunable == HunterTunable.LossDistance)) return false;
            float original;
            switch (mutation.Tunable)
            {
                case HunterTunable.Acceleration: original = profile.Acceleration; break;
                case HunterTunable.TurnRate: original = profile.TurnRate; break;
                case HunterTunable.ChaseSpeedMultiplier: original = profile.ChaseSpeedMultiplier; break;
                case HunterTunable.ActionCommitmentSeconds: original = profile.ActionCommitmentSeconds; break;
                case HunterTunable.LossSeconds: original = profile.LossSeconds; break;
                case HunterTunable.LossDistance: original = profile.LossDistance; break;
                default:
                    if (mutation.Value != 0f && mutation.Value != 1f) return false;
                    var kind = (HunterHabitKind)((int)mutation.Tunable - (int)HunterTunable.ThresholdPauseEnabled);
                    if (profile.Habits != null) foreach (var habit in profile.Habits)
                        if (habit != null && habit.Kind == kind) return mutation.Value != (habit.Enabled ? 1f : 0f);
                    return false;
            }
            if (mutation.Tunable <= HunterTunable.ActionCommitmentSeconds && mutation.Value <= 0f) return false;
            if (mutation.Tunable == HunterTunable.ActionCommitmentSeconds && mutation.Value < 0.1f) return false;
            return mutation.Value != original;
        }
        private static bool Contains(IReadOnlyList<string> values, string id)
        { foreach (string value in values) if (value == id) return true; return false; }
    }
}
