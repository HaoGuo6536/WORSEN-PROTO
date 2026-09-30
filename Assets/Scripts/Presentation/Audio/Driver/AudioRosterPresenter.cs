// ============================================================================
// AudioRosterPresenter.cs
// ============================================================================
// PURPOSE:
//   Maps roster observations to the five existing hunter budget slots.
//   Authoritative replay and tick facts have already been delayed by gameplay;
//   this presenter neither adds delay nor synthesizes another cadence timer.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Deduplicate hunter facts and the shared TurnToFace/deliberation beat.
//   - Queue hidden-mutation tells by committed event identity, never display text.
//   - Resolve config-driven floor/room sound zones without scene queries.
// DEPENDENCIES:
//   - Core fact payloads and own Audio config/state/commands only.
// USAGE NOTES:
//   No engine calls or random clock. Unknown named cues remain silent placeholders.
//   Duplicate archetypes use EntityId, not archetype name, for voice ownership.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Presentation.Audio
{
    public sealed class AudioRosterPresenter
    {
        private bool Fresh(AudioRosterDriverState state, EntityId hunter, string kind, long tick)
        {
            var key = (hunter, kind);
            if (!hunter.IsValid || state.Ticks.TryGetValue(key, out long previous) && tick <= previous) return false;
            state.Ticks[key] = tick; return true;
        }
        private AudioRosterCommand Command(EntityId hunter, string id, HunterCueSlot slot, Vector3 position, bool exact = false)
            => new AudioRosterCommand { Hunter = hunter, Id = id, Slot = slot, Position = position, Gain = 1f, Pitch = 1f, Exact = exact };
        public bool Archetype(AudioRosterDriverState state, HunterArchetypeFact fact, out AudioRosterCommand command)
        {
            command = default;
            if (fact.Kind != HunterArchetypeFactKind.ReplayedFootstep || !Fresh(state, fact.Hunter, "echo-step:" + fact.RecordedTick, fact.Tick)) return false;
            state.Archetypes[fact.Hunter] = "echo";
            command = Command(fact.Hunter, "echo.footstep", HunterCueSlot.Presence, fact.Position, true);
            command.Gain = fact.Gain; command.Pitch = fact.Pitch; return true;
        }
        public bool Weaver(AudioRosterDriverState state, WeaverFact fact, out AudioRosterCommand command)
        {
            command = default; state.Archetypes[fact.Hunter] = "weaver";
            if (fact.Kind != WeaverFactKind.SkitteringAbove && fact.Kind != WeaverFactKind.WetClick) return false;
            if (!Fresh(state, fact.Hunter, "weaver:" + fact.Kind, fact.Tick)) return false;
            bool tell = fact.Kind == WeaverFactKind.WetClick;
            command = Command(fact.Hunter, tell ? "weaver.wet-click" : "weaver.skitter", tell ? HunterCueSlot.AttackTiming : HunterCueSlot.Presence, fact.Position, tell);
            return true;
        }
        public bool Ticking(AudioRosterDriverState state, TickingSoundFact fact, out AudioRosterCommand command)
        {
            command = default;
            if (!Fresh(state, fact.Hunter, "ticking:" + fact.Sound, fact.Tick)) return false;
            state.Archetypes[fact.Hunter] = "ticking";
            string id = fact.SoundId;
            if (string.IsNullOrEmpty(id))
                id = fact.Sound == TickingSound.KeyAppeared ? "ticking.key-appeared" : "ticking." + fact.Sound.ToString().ToLowerInvariant();
            command = Command(fact.Hunter, id, fact.Sound == TickingSound.Wake ? HunterCueSlot.Detection : HunterCueSlot.Presence, fact.Position, true);
            command.Stop = fact.Sound == TickingSound.Stop;
            command.Interval = fact.Sound == TickingSound.Tick && fact.Interval > 0f && !float.IsInfinity(fact.Interval) ? fact.Interval : 0f;
            state.TickIntervals[fact.Hunter] = command.Interval;
            return true;
        }
        public bool Habit(AudioRosterDriverState state, HunterHabitFact fact, out AudioRosterCommand command)
        {
            command = default;
            if (fact.Kind == HunterHabitKind.ThresholdPause) return false;
            string beat = fact.Kind == HunterHabitKind.TurnToFace ? "turn" : "cake";
            if (!Fresh(state, fact.Hunter, beat, fact.Tick)) return false;
            command = Command(fact.Hunter, "hunter." + (beat == "turn" ? "turn" : "cake-reaction"), HunterCueSlot.Presence, fact.Position);
            return true;
        }
        public bool Deliberation(AudioRosterDriverState state, EntityId hunter, Vector3 position, long tick, out AudioRosterCommand command)
            => Habit(state, new HunterHabitFact(hunter, HunterHabitKind.TurnToFace, position, tick), out command);
        public bool Feedback(AudioRosterDriverState state, HunterFeedbackEvent fact, out AudioRosterCommand command)
        {
            command = default; state.Archetypes[fact.Hunter] = fact.ArchetypeKey;
            string suffix; HunterCueSlot slot;
            switch (fact.Kind)
            {
                case HunterFeedbackKind.Detected: suffix = "detection"; slot = HunterCueSlot.Detection; break;
                case HunterFeedbackKind.AttackWindup: case HunterFeedbackKind.SpikeWarning: suffix = "attack"; slot = HunterCueSlot.AttackTiming; break;
                case HunterFeedbackKind.Scream: suffix = "chase"; slot = HunterCueSlot.ChaseLayer; break;
                case HunterFeedbackKind.LightReaction: suffix = "presence"; slot = HunterCueSlot.Presence; break;
                case HunterFeedbackKind.Footstep:
                    if (fact.ArchetypeKey == "echo" || fact.ArchetypeKey == "weaver" || fact.ArchetypeKey == "ticking") return false;
                    suffix = "presence"; slot = HunterCueSlot.Presence; break;
                default: return false;
            }
            if (!Fresh(state, fact.Hunter, "feedback:" + fact.Kind, fact.Tick)) return false;
            command = Command(fact.Hunter, (string.IsNullOrEmpty(fact.ArchetypeKey) ? "hunter" : fact.ArchetypeKey) + "." + suffix,
                slot, fact.Position, slot == HunterCueSlot.AttackTiming);
            return true;
        }
        public void Mutation(AudioRosterDriverState state, ProgressionEventFact fact)
        {
            if (fact.ResolvedKind != ProgressionEventKind.HiddenMutation || string.IsNullOrWhiteSpace(fact.TellId) || !state.MutationSequences.Add(fact.Sequence)) return;
            state.PendingTells.Add(Command(EntityId.None, fact.TellId, HunterCueSlot.Presence, Vector3.zero, true));
        }
        public AudioRosterBinding? Binding(AudioSoundscapeDriverConfig config, string id)
        {
            foreach (var binding in config.RosterBindings) if (binding.Id == id) return binding;
            int dot = id == null ? -1 : id.LastIndexOf('.');
            string suffix = dot >= 0 ? id.Substring(dot + 1) : "";
            if (suffix != "presence" && suffix != "detection" && suffix != "chase" && suffix != "attack" && suffix != "death") return null;
            foreach (var binding in config.RosterBindings) if (binding.Id == "hunter." + suffix) return binding;
            return null;
        }
        public void Theme(AudioRosterDriverState state, string zone) { state.DefaultZone = zone; state.RoomZones.Clear(); }
        public void RoomTheme(AudioRosterDriverState state, AudioSoundscapeDriverConfig config, int room, string theme, string family)
        {
            string zone = state.DefaultZone;
            foreach (var preset in config.SoundZones)
                if (preset.Theme == theme && (string.IsNullOrEmpty(preset.Family) || preset.Family == family))
                { zone = preset.Id; if (!string.IsNullOrEmpty(preset.Family)) break; }
            state.RoomZones[room] = zone;
        }
        public float ZoneCutoff(AudioRosterDriverState state, AudioSoundscapeDriverConfig config, int room)
        {
            string id = state.RoomZones.TryGetValue(room, out var zone) ? zone : state.DefaultZone;
            foreach (var preset in config.SoundZones) if (preset.Id == id) return Mathf.Clamp(preset.Cutoff, 10f, 22000f);
            return 22000f;
        }
        public void Reset(AudioRosterDriverState state, bool preserveTells, bool preserveZones = false)
        {
            state.Ticks.Clear(); state.Archetypes.Clear(); state.TickIntervals.Clear(); state.LastAttacker = EntityId.None;
            if (!preserveZones) { state.RoomZones.Clear(); state.DefaultZone = null; }
            if (!preserveTells) { state.MutationSequences.Clear(); state.PendingTells.Clear(); state.Missing.Clear(); }
        }
    }
}
