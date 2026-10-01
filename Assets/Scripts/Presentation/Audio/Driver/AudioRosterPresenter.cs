// ============================================================================
// AudioRosterPresenter.cs
// ============================================================================
// PURPOSE:
//   Maps roster observations to the five existing hunter budget slots.
//   Authoritative replay and tick facts have already been delayed by gameplay;
//   this presenter neither adds delay nor synthesizes another cadence timer.
//   New hunters resolve exact named clips only; shared banks belong to legacy ids.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Deduplicate hunter facts and the shared TurnToFace/deliberation beat.
//   - Queue hidden-mutation tells by committed event identity, never display text.
//   - Resolve config-driven floor/room sound zones without scene queries.
//   - Map expansion hunter tells without replaying discovery or unconfirmed death.
//   - Fail closed on missing roster identities instead of borrowing monster banks.
// DEPENDENCIES:
//   - Core fact payloads and own Audio config/state/commands only.
// USAGE NOTES:
//   No engine calls or random clock. Unknown named cues remain silent placeholders.
//   Duplicate archetypes use EntityId, not archetype name, for voice ownership.
// ============================================================================
using System;
using System.Collections.Generic;
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
            state.Archetypes.TryGetValue(fact.Hunter, out string key);
            if (key == "mannequin" || key == "mimic" || key == "skip") return false;
            if (fact.Kind == HunterHabitKind.ThresholdPause) return false;
            string beat = fact.Kind == HunterHabitKind.TurnToFace ? "turn" : "cake";
            if (!Fresh(state, fact.Hunter, beat, fact.Tick)) return false;
            command = Command(fact.Hunter, (IsLegacyHunter(key) ? "hunter" : string.IsNullOrEmpty(key) ? "unknown" : key) +
                "." + (beat == "turn" ? "turn" : "cake-reaction"), HunterCueSlot.Presence, fact.Position);
            return true;
        }
        public bool Deliberation(AudioRosterDriverState state, EntityId hunter, Vector3 position, long tick, out AudioRosterCommand command)
            => Habit(state, new HunterHabitFact(hunter, HunterHabitKind.TurnToFace, position, tick), out command);
        public bool Feedback(AudioRosterDriverState state, HunterFeedbackEvent fact, out AudioRosterCommand command)
        {
            command = default;
            if (!string.IsNullOrEmpty(fact.ArchetypeKey)) state.Archetypes[fact.Hunter] = fact.ArchetypeKey;
            state.Archetypes.TryGetValue(fact.Hunter, out string key);
            if (key == "mannequin" || key == "mimic" || key == "skip") return false;
            // Dedicated facts own these cues; generic feedback must not double-play them.
            if (key == "herald" || key == "blinder" || key == "stare") return false;
            string suffix; HunterCueSlot slot;
            switch (fact.Kind)
            {
                case HunterFeedbackKind.Detected: suffix = "detection"; slot = HunterCueSlot.Detection; break;
                case HunterFeedbackKind.AttackWindup: case HunterFeedbackKind.SpikeWarning: suffix = "attack"; slot = HunterCueSlot.AttackTiming; break;
                case HunterFeedbackKind.Scream: suffix = "chase"; slot = HunterCueSlot.ChaseLayer; break;
                case HunterFeedbackKind.LightReaction: suffix = "presence"; slot = HunterCueSlot.Presence; break;
                case HunterFeedbackKind.Footstep:
                    if (key == "echo" || key == "weaver" || key == "ticking") return false;
                    suffix = "presence"; slot = HunterCueSlot.Presence; break;
                default: return false;
            }
            if (!Fresh(state, fact.Hunter, "feedback:" + fact.Kind, fact.Tick)) return false;
            command = Command(fact.Hunter, (string.IsNullOrEmpty(key) ? "unknown" : key) + "." + suffix,
                slot, fact.Position, slot == HunterCueSlot.AttackTiming);
            return true;
        }
        public void Mutation(AudioRosterDriverState state, ProgressionEventFact fact)
        {
            if (fact.ResolvedKind != ProgressionEventKind.HiddenMutation || string.IsNullOrWhiteSpace(fact.TellId) || !state.MutationSequences.Add(fact.Sequence)) return;
            state.PendingTells.Add(Command(EntityId.None, fact.TellId, HunterCueSlot.Presence, Vector3.zero, true));
        }
        public bool Herald(AudioRosterDriverState state, HeraldScreamFact fact, out AudioRosterCommand command)
        {
            command = default; state.Archetypes[fact.Hunter] = "herald";
            // ObservedTick is the last player clue, NOT the scream's emission tick.
            if (!Fresh(state, fact.Hunter, "herald:" + fact.Sound, fact.Noise.Tick)) return false;
            var slot = fact.Sound == HeraldSound.Discovery ? HunterCueSlot.Detection :
                fact.Sound == HeraldSound.Attack || fact.Sound == HeraldSound.DrawnBreath ? HunterCueSlot.AttackTiming : HunterCueSlot.ChaseLayer;
            command = Command(fact.Hunter, fact.SoundId, slot, fact.Noise.Position, true);
            command.Pitch = fact.Sound == HeraldSound.Attack ? 1f : fact.Pitch;
            return true;
        }
        public bool HeraldBreath(AudioRosterDriverState state, HeraldBreathFact fact, out AudioRosterCommand command)
        {
            command = default; state.Archetypes[fact.Hunter] = "herald";
            if (!Fresh(state, fact.Hunter, "herald:breath", fact.Tick)) return false;
            command = Command(fact.Hunter, "herald.breath", HunterCueSlot.AttackTiming, fact.Position, true);
            command.Interval = fact.Duration; return true;
        }
        public bool Blinder(AudioRosterDriverState state, BlinderSoundFact fact, out AudioRosterCommand command)
        {
            command = default; state.Archetypes[fact.Hunter] = "blinder";
            if (fact.Sound == BlinderSound.Catch || !Fresh(state, fact.Hunter, "blinder:" + fact.Sound, fact.Tick)) return false;
            var slot = fact.Sound == BlinderSound.Detection ? HunterCueSlot.Detection : fact.Sound == BlinderSound.Chase ? HunterCueSlot.ChaseLayer :
                fact.Sound == BlinderSound.ThrowHiss || fact.Sound == BlinderSound.TrapTick ? HunterCueSlot.AttackTiming : HunterCueSlot.Presence;
            string suffix = fact.Sound == BlinderSound.ThrowHiss ? "throw-hiss" : fact.Sound == BlinderSound.TrapTick ? "trap-tick" : fact.Sound.ToString().ToLowerInvariant();
            command = Command(fact.Hunter, "blinder." + suffix, slot, fact.Position, slot == HunterCueSlot.AttackTiming); return true;
        }
        public bool Ram(AudioRosterDriverState state, RamFact fact, out AudioRosterCommand command)
        {
            command = default; state.Archetypes[fact.Hunter] = "ram";
            if (fact.Kind == RamFactKind.Won || !Fresh(state, fact.Hunter, "ram:" + fact.Kind, fact.Tick)) return false;
            var slot = fact.Kind == RamFactKind.Stamp || fact.Kind == RamFactKind.Bellow ? HunterCueSlot.AttackTiming : HunterCueSlot.Presence;
            command = Command(fact.Hunter, fact.SoundId, slot, fact.Position, slot == HunterCueSlot.AttackTiming); return true;
        }
        public bool Mimic(AudioRosterDriverState state, MimicFact fact, out AudioRosterCommand command)
        {
            command = default; state.Archetypes[fact.Hunter] = "mimic";
            if (fact.Kind != MimicFactKind.BiteStarted || !Fresh(state, fact.Hunter, "mimic:bite", fact.Tick)) return false;
            command = Command(fact.Hunter, fact.SoundId, HunterCueSlot.AttackTiming, fact.Position, true); return true;
        }
        public bool Stare(AudioRosterDriverState state, StareFact fact, out AudioRosterCommand command)
        {
            command = default; state.Archetypes[fact.Hunter] = "stare";
            if (fact.Kind != StareFactKind.Sound || fact.Slot == HunterCueSlot.DeathSting ||
                !Fresh(state, fact.Hunter, "stare:" + fact.SoundId, fact.Tick)) return false;
            command = Command(fact.Hunter, fact.SoundId, fact.Slot, fact.Position, true); command.Gain = fact.Volume; return true;
        }
        public void Mannequin(AudioRosterDriverState state, MannequinFact fact) => state.Archetypes[fact.Hunter] = "mannequin";
        public bool AcceptHeraldDeafen(AudioRosterDriverState state, HeraldDeafenFact fact) =>
            fact.Player.IsValid && Fresh(state, fact.Hunter, "herald:deafen:" + fact.Player.Value, fact.Tick);
        public bool AcceptBlinderHit(AudioRosterDriverState state, BlinderHitFact fact) =>
            fact.Player.IsValid && Fresh(state, fact.Hunter, "blinder:hit:" + fact.Player.Value + ":" + fact.Serial, fact.Tick);
        public AudioRosterBinding? Binding(AudioSoundscapeDriverConfig config, string id)
            => ResolveBinding(config.RosterBindings, id);
        public AudioRosterBinding? ResolveBinding(IReadOnlyList<AudioRosterBinding> bindings, string id)
        {
            if (string.IsNullOrEmpty(id) || bindings == null) return null;
            foreach (var binding in bindings) if (binding.Id == id) return binding;
            int dot = id == null ? -1 : id.LastIndexOf('.');
            if (dot < 0 || !IsLegacyHunter(id.Substring(0, dot))) return null;
            string suffix = dot >= 0 ? id.Substring(dot + 1) : "";
            if (suffix != "presence" && suffix != "detection" && suffix != "chase" && suffix != "attack" && suffix != "death") return null;
            foreach (var binding in bindings) if (binding.Id == "hunter." + suffix) return binding;
            return null;
        }
        public bool IsLegacyHunter(string key) => key == "rusher" || key == "hexer" || key == "lurker" || key == "thorncaller" || key == "watcher";
        public bool AllowsSharedBank(string id)
        {
            int dot = id == null ? -1 : id.IndexOf('.');
            return dot > 0 && (id.Substring(0, dot) == "hunter" || IsLegacyHunter(id.Substring(0, dot)));
        }
        public bool IsRosterCue(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (id == "ms_mangled_scream_03" || id == "sb_mangled_scream_01" || id == "sb_mangled_scream_02" || id == "sb_mangled_scream_03") return true;
            int separator = id.IndexOfAny(new[] { '.', '-' });
            string key = separator > 0 ? id.Substring(0, separator) : id;
            return key == "echo" || key == "weaver" || key == "ticking" || key == "ram" || key == "skip" ||
                key == "mimic" || key == "blinder" || key == "herald" || key == "mannequin" || key == "stare";
        }
        public string DeathId(AudioRosterDriverState state) => state.LastAttacker.IsValid &&
            state.Archetypes.TryGetValue(state.LastAttacker, out string key) && !string.IsNullOrEmpty(key) ? key + ".death" : "unknown.death";
        public bool AllowsSharedEmitter(AudioRosterDriverState state, int emitter) =>
            state.Archetypes.TryGetValue(new EntityId(emitter), out string key) && IsLegacyHunter(key);
        public bool OwnsCommand(AudioRosterDriverState state, AudioRosterCommand command)
        {
            if (string.IsNullOrEmpty(command.Id)) return false;
            // Progression tells deliberately carry no entity; they still cannot address legacy banks.
            if (!command.Hunter.IsValid) return IsRosterCue(command.Id);
            if (!state.Archetypes.TryGetValue(command.Hunter, out string key) || string.IsNullOrEmpty(key)) return false;
            if (IsLegacyHunter(key)) return command.Id.StartsWith(key + ".", StringComparison.Ordinal) ||
                command.Id.StartsWith("hunter.", StringComparison.Ordinal);
            if (!IsRosterCue(key)) return false;
            return command.Id.StartsWith(key + ".", StringComparison.Ordinal) ||
                command.Id.StartsWith(key + "-", StringComparison.Ordinal) || key == "herald" &&
                (command.Id == "ms_mangled_scream_03" || command.Id == "sb_mangled_scream_01" ||
                 command.Id == "sb_mangled_scream_02" || command.Id == "sb_mangled_scream_03");
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
