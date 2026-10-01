// ============================================================================
// AudioWorldMixPresenter.cs
// ============================================================================
// PURPOSE:
//   Applies the shared hearing model to every environmental voice using a supplied level view.
//   It also computes bodily masking, timed sensory effects and rare presentation-only misdirection.
//   Native spatial sources request portal-only gain; pure audibility queries retain distance loss.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Resolve room cells, portals and closed doors without engine queries or duplicate distance rolloff.
//   - Protect hunter presence and attack timing from masking and timed deafening.
//   - Apply Ear Plugs and Mirror Skin once at raw hunter-fact ingress.
//   - Schedule false positives and accelerating collapse pulses using injected time/randomness.
// DEPENDENCIES:
//   - Core AcousticOcclusionUtility, effects and topology; Audio config/state only.
// USAGE NOTES:
//   Missing topology fails closed for world voices. Silent Presence deliberately
//   mutes presence despite no-duck protection. False positives never publish noise.
//   Timing tells and protected voices retain their unfiltered mix except occlusion.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Audio
{
    public sealed class AudioWorldMixPresenter
    {
        public void SetWorld(AudioWorldMixDriverState state, LevelGraph graph, IReadOnlyDictionary<int, bool> doors)
        {
            state.Graph = graph; state.ClosedDoors.Clear();
            if (doors != null) foreach (var pair in doors) state.ClosedDoors[pair.Key] = pair.Value;
        }
        public void SetEffects(AudioWorldMixDriverState state, IReadOnlyActiveEffects effects)
        {
            state.SilentPresence = effects != null && effects.Has(new EffectId("silent-presence"));
            state.KeenEars = effects != null && effects.Has(new EffectId("keen-ears"));
            state.EarPlugs = effects != null && effects.Has(new EffectId("ear-plugs"));
            state.MirrorSkin = effects != null && effects.Has(new EffectId("mirror-skin"));
        }
        public void HeraldDeafen(AudioWorldMixDriverState state, HeraldDeafenFact fact, float durationMultiplier)
            => Deafening(state, Positive(fact.Duration) * (state.EarPlugs ? Mathf.Clamp01(Positive(durationMultiplier)) : 1f));
        public void BlinderHit(AudioWorldMixDriverState state, BlinderHitFact fact, float durationMultiplier)
        {
            if (fact.MuffledDark) MuffledDark(state, Positive(fact.Duration) * (state.MirrorSkin ? Mathf.Clamp01(Positive(durationMultiplier)) : 1f));
        }
        public float Gain(AudioWorldMixDriverState state, AudioCueCatalogueEntry entry, Vector3 source, AudioSoundscapeDriverConfig config, bool distanceAttenuatedBySource = false)
        {
            bool presence = entry.Category == CueCategory.Hunter && entry.Slot == (int)HunterCueSlot.Presence;
            if (presence && state.SilentPresence) return 0f;
            float gain = 1f;
            if (entry.Noise.HasValue)
            {
                HearingModelSettings hearing = config.Hearing;
                if (presence && state.KeenEars)
                    hearing = new HearingModelSettings(hearing.ReferenceDistance * config.KeenEarsRangeMultiplier,
                        hearing.Rolloff, hearing.PerPortalAttenuation, hearing.ClosedDoorAttenuation, hearing.AudibleThreshold);
                gain = AcousticGain(state, source, hearing, distanceAttenuatedBySource);
            }
            return gain * Mask(state, entry.Protected, config);
        }
        public float AcousticGain(AudioWorldMixDriverState state, Vector3 source, HearingModelSettings hearing, bool distanceAttenuatedBySource)
        {
            if (state.Graph == null) return 0f;
            if (distanceAttenuatedBySource)
                hearing = new HearingModelSettings(hearing.ReferenceDistance, 0f,
                    hearing.PerPortalAttenuation, hearing.ClosedDoorAttenuation, hearing.AudibleThreshold);
            var sample = AcousticOcclusionUtility.Sample(state.Graph, Room(state.Graph, source), source,
                Room(state.Graph, state.Listener), state.Listener, 1f, hearing, state.ClosedDoors);
            return sample.Audible ? sample.PerceivedLoudness : 0f;
        }
        public float Mask(AudioWorldMixDriverState state, bool protectedVoice, AudioSoundscapeDriverConfig config) =>
            protectedVoice ? 1f : state.MaskGain * (state.DeafenedRemaining > 0f ? config.DeafenedGain : 1f);
        public float Cutoff(AudioWorldMixDriverState state, bool protectedVoice, AudioSoundscapeDriverConfig config) =>
            !protectedVoice && state.MuffledRemaining > 0f ? config.MuffledCutoff : 22000f;
        public void Deafening(AudioWorldMixDriverState state, float seconds) => state.DeafenedRemaining = Mathf.Max(state.DeafenedRemaining, Positive(seconds));
        public void MuffledDark(AudioWorldMixDriverState state, float seconds) => state.MuffledRemaining = Mathf.Max(state.MuffledRemaining, Positive(seconds));
        public void Grace(AudioWorldMixDriverState state, AudioSoundscapeDriverConfig config)
        { state.GraceRemaining = config.GraceSpikeSeconds; state.HeartbeatDue = state.Time; }
        public void Tick(AudioWorldMixDriverState state, AudioSoundscapeDriverConfig config, bool active, bool alive, float injuryBreath, float dt)
        {
            dt = Positive(dt); state.Commands.Clear(); state.Heartbeat = false;
            if (dt <= 0f) return;
            state.Time += dt;
            state.DeafenedRemaining = Mathf.Max(0f, state.DeafenedRemaining - dt);
            state.MuffledRemaining = Mathf.Max(0f, state.MuffledRemaining - dt);
            float closeness = Mathf.Clamp01(state.Closeness);
            state.HeartbeatStrength = active && alive ? Mathf.Max(closeness, state.GraceRemaining > 0f ? 1f : 0f) : 0f;
            state.BreathGain = active && alive ? Mathf.Max(injuryBreath, state.InChase ? config.PursuitBreathGain : 0f) : 0f;
            state.HeartbeatEnvelope = Mathf.Max(0f, state.HeartbeatEnvelope - dt / Mathf.Max(.01f, config.HeartbeatPulseSeconds));
            if (state.HeartbeatStrength > 0f && state.Time >= state.HeartbeatDue)
            {
                state.Heartbeat = true; state.HeartbeatEnvelope = state.HeartbeatStrength;
                state.HeartbeatDue = state.Time + Mathf.Lerp(config.HeartbeatSlowSeconds, config.HeartbeatFastSeconds, state.HeartbeatStrength);
            }
            state.MaskGain = 1f - config.HeartbeatMask * state.HeartbeatEnvelope;
            state.GraceRemaining = Mathf.Max(0f, state.GraceRemaining - dt);
            if (!active || !alive) { state.MaskGain = 1f; return; }
            foreach (var pair in state.Rooms)
            {
                var room = pair.Value;
                if (room.Phase == RoomPhase.Open || room.Phase == RoomPhase.Closed) continue;
                if (state.PulseDue.TryGetValue(room.RoomId, out float due) && due > state.Time) continue;
                state.PulseDue[room.RoomId] = state.Time + CollapseInterval(room.Progress, config);
                if (state.Graph != null) foreach (var location in state.Graph.Rooms)
                    if (location.Id == room.RoomId) state.Commands.Add(new AudioFeedbackCommand
                    { Cue = CueId.RoomTelegraph, Position = location.Center, Emitter = -10000 - room.RoomId, Gain = 1f });
            }
        }
        public float CollapseInterval(float progress, AudioSoundscapeDriverConfig config) =>
            Mathf.Lerp(config.CollapseSlowSeconds, config.CollapseFastSeconds, Mathf.Clamp01(progress));
        public bool FalsePositive(AudioWorldMixDriverState state, AudioSoundscapeDriverConfig config, bool active, bool alive, System.Random random, out AudioFeedbackCommand command)
        {
            command = default;
            if (state.FalsePositiveDue < 0f) state.FalsePositiveDue = state.Time + Interval(config, random);
            if (!active || !alive || state.InChase || state.Time < state.FalsePositiveDue || state.Graph == null) return false;
            // Choose only an actual distant room, in stable graph order. No hunter identity is invented.
            int candidates = 0;
            foreach (var room in state.Graph.Rooms) if (FalsePositiveRoom(state, config, room.Center)) candidates++;
            if (candidates == 0) return false;
            int pick = random.Next(candidates);
            foreach (var room in state.Graph.Rooms)
            {
                if (!FalsePositiveRoom(state, config, room.Center) || pick-- != 0) continue;
                command = new AudioFeedbackCommand { Cue = random.Next(2) == 0 ? CueId.Footstep : CueId.ChainCreak,
                    Position = room.Center, Emitter = int.MinValue, Gain = config.FalsePositiveGain };
                state.FalsePositiveDue = state.Time + Interval(config, random); return true;
            }
            return false;
        }
        public int Room(LevelGraph graph, Vector3 position)
        {
            if (graph != null) foreach (var room in graph.Rooms)
                if (room.ContainsXZ(position)) return room.Id;
            return -1;
        }
        private bool FalsePositiveRoom(AudioWorldMixDriverState state, AudioSoundscapeDriverConfig config, Vector3 position) =>
            (position - state.Listener).magnitude >= config.FalsePositiveDistance &&
            Gain(state, new AudioCueCatalogueEntry(CueCategory.World, (int)WorldCueSlot.HunterClosedDoor, NoiseSourceKind.Door), position, config) > 0f;
        private float Interval(AudioSoundscapeDriverConfig config, System.Random random) =>
            config.FalsePositiveMinimumSeconds + Positive(config.FalsePositiveExtraSeconds) * (float)random.NextDouble();
        private float Positive(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }
}
