// ============================================================================
// AudioSoundscapePresenter.cs
// ============================================================================
//
// PURPOSE:
//   Chooses nonrepeating sound variations and bounded voices from supplied data.
//   It also aggregates all pursuers into one continuous chase envelope, so another enemy never restarts music.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Identify enemy-owned voices for death cleanup without muting player death/UI/world sounds.
//   - Respect cue cooldowns and priorities without stealing equally important voices.
//   - Refresh loop gain while retaining its initially chosen voice and random variation.
//   - Fade synchronized music layers after a short lost-contact hold.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Time and cosmetic randomness are injected; no engine properties are read.
//   Pitch changes affect playback lifetime only, never the authoritative gameplay attack phase.
//
// ============================================================================

using System;
using UnityEngine;

namespace Worsen.Presentation.Audio
{
    public sealed class AudioSoundscapePresenter
    {
        public bool IsEnemyCue(Worsen.Core.CueId cue)
        {
            switch (cue)
            {
                case Worsen.Core.CueId.Presence: case Worsen.Core.CueId.Detection: case Worsen.Core.CueId.Chase:
                case Worsen.Core.CueId.EnemyWindup: case Worsen.Core.CueId.EnemyAttack: case Worsen.Core.CueId.EnemyMiss:
                case Worsen.Core.CueId.EnemyHit: case Worsen.Core.CueId.EnemyRecovery: case Worsen.Core.CueId.EnemyLost:
                case Worsen.Core.CueId.EnemyScream: case Worsen.Core.CueId.EnemyFootstep:
                case Worsen.Core.CueId.ProjectileLaunch: case Worsen.Core.CueId.ProjectileTravel: case Worsen.Core.CueId.ProjectileImpact:
                case Worsen.Core.CueId.SpikeWarning: case Worsen.Core.CueId.SpikeErupt: return true;
                default: return false;
            }
        }
        public Worsen.Core.CueId FootstepSurface(string description)
        {
            string text = (description ?? string.Empty).ToLowerInvariant();
            if (text.Contains("wood") || text.Contains("timber") || text.Contains("plank")) return Worsen.Core.CueId.FootstepWood;
            if (text.Contains("metal") || text.Contains("iron") || text.Contains("grate")) return Worsen.Core.CueId.FootstepMetal;
            if (text.Contains("soil") || text.Contains("dirt") || text.Contains("grass") || text.Contains("mud")) return Worsen.Core.CueId.FootstepSoil;
            return Worsen.Core.CueId.Footstep;
        }
        public void Reset(AudioSoundscapeDriverState state, int voices, System.Random random)
        {
            state.Threats.Clear(); state.Cooldowns.Clear(); state.LastClips.Clear(); state.MissingWarnings.Clear();
            state.ExpiredKeys.Clear(); state.Voices = new AudioVoiceSample[Mathf.Clamp(voices, 8, 48)];
            state.CosmeticRandom = random; state.Time = state.ChaseHold = state.TensionGain = state.ChaseGain = state.DangerGain = 0f;
            state.InteriorGain = state.ExteriorGain = state.Openness = state.Collapse = state.Duck = 0f;
            state.FootstepGain = 1f; state.MusicStarted = false; state.Alive = true;
        }

        public bool TryPlay(AudioSoundscapeDriverState state, AudioSoundDefinition sound, int emitter,
            float[] clipDurations, float requestedGain, out AudioPlaybackSample result)
        {
            result = default;
            if (clipDurations == null || clipDurations.Length == 0 || state.Voices == null) return false;
            int cue = (int)sound.Cue;
            long key = ((long)cue << 32) | (uint)emitter;
            int count = 0;
            for (int i = 0; i < state.Voices.Length; i++)
            {
                AudioVoiceSample voice = state.Voices[i];
                if (voice.Remaining <= 0f || voice.Cue != cue) continue;
                if (sound.Loop && voice.Loop && voice.Emitter == emitter)
                {
                    result = new AudioPlaybackSample { Voice = i, ReuseLoop = true, Gain = voice.BaseGain * Unit(requestedGain) };
                    return true;
                }
                count++;
            }
            if (count >= Mathf.Max(1, sound.MaxConcurrent)) return false;
            if (state.Cooldowns.TryGetValue(key, out float until) && until > state.Time) return false;
            int index = -1;
            for (int i = 0; i < state.Voices.Length; i++)
            {
                if (state.Voices[i].Remaining <= 0f) { index = i; break; }
                if (state.Voices[i].Priority >= sound.Priority) continue;
                if (index < 0 || state.Voices[i].Priority < state.Voices[index].Priority ||
                    (state.Voices[i].Priority == state.Voices[index].Priority && state.Voices[i].Remaining < state.Voices[index].Remaining)) index = i;
            }
            if (index < 0) return false;
            int last = state.LastClips.TryGetValue(cue, out int previous) ? previous : -1;
            int playable = 0;
            for (int i = 0; i < clipDurations.Length; i++) if (Positive(clipDurations[i]) > 0f) playable++;
            if (playable == 0) return false;
            bool excludeLast = playable > 1 && last >= 0 && last < clipDurations.Length && Positive(clipDurations[last]) > 0f;
            int choose = state.CosmeticRandom.Next(playable - (excludeLast ? 1 : 0));
            int selected = -1;
            for (int i = 0; i < clipDurations.Length; i++)
            {
                if (Positive(clipDurations[i]) <= 0f || (excludeLast && i == last)) continue;
                if (choose-- == 0) { selected = i; break; }
            }
            float minimum = Mathf.Clamp(Positive(sound.PitchMinimum), 0.5f, 2f);
            float maximum = Mathf.Clamp(Positive(sound.PitchMaximum), minimum, 2f);
            float pitch = Mathf.Lerp(minimum, maximum, (float)state.CosmeticRandom.NextDouble());
            float variation = Unit(sound.GainVariation);
            float baseGain = Unit(sound.Gain) * Mathf.Lerp(1f - variation, 1f, (float)state.CosmeticRandom.NextDouble());
            float gain = baseGain * Unit(requestedGain);
            state.Voices[index] = new AudioVoiceSample { Cue = cue, Emitter = emitter, Priority = sound.Priority,
                Remaining = sound.Loop ? float.MaxValue : clipDurations[selected] / pitch, Loop = sound.Loop, BaseGain = baseGain };
            state.LastClips[cue] = selected;
            state.Cooldowns[key] = state.Time + Positive(sound.Cooldown);
            if (sound.Priority >= 75) state.Duck = Mathf.Max(state.Duck, 0.45f);
            result = new AudioPlaybackSample { Voice = index, Clip = selected, Gain = gain, Pitch = pitch };
            return true;
        }

        public void Tick(AudioSoundscapeDriverState state, float dt, float attack, float release, float hold)
        {
            dt = Positive(dt); if (dt <= 0f) return;
            state.Time += dt;
            for (int i = 0; i < state.Voices.Length; i++)
            {
                AudioVoiceSample voice = state.Voices[i];
                if (!voice.Loop) voice.Remaining = Mathf.Max(0f, voice.Remaining - dt);
                state.Voices[i] = voice;
            }
            state.ExpiredKeys.Clear();
            foreach (var pair in state.Cooldowns) if (pair.Value <= state.Time) state.ExpiredKeys.Add(pair.Key);
            foreach (long key in state.ExpiredKeys) state.Cooldowns.Remove(key);
            bool chasing = false; float proximity = 0f;
            foreach (AudioThreatSample threat in state.Threats.Values)
            { chasing |= threat.Chasing; proximity = Mathf.Max(proximity, Unit(threat.Closeness)); }
            if (chasing && state.Alive) state.ChaseHold = Positive(hold);
            else state.ChaseHold = Mathf.Max(0f, state.ChaseHold - dt);
            float chase = state.Alive && (chasing || state.ChaseHold > 0f) ? 1f : 0f;
            float tension = state.Alive ? Mathf.Max(proximity * 0.55f, chase * 0.35f) : 0f;
            float danger = state.Alive && chase > 0f ? Mathf.InverseLerp(0.45f, 1f, proximity) : 0f;
            state.TensionGain = Envelope(state.TensionGain, tension, dt, attack, release);
            state.ChaseGain = Envelope(state.ChaseGain, chase, dt, attack, release);
            state.DangerGain = Envelope(state.DangerGain, danger, dt, attack, release);
            state.InteriorGain = Envelope(state.InteriorGain, (1f - Unit(state.Openness)) * (1f - Unit(state.Collapse) * 0.4f), dt, 1.5f, 1.5f);
            state.ExteriorGain = Envelope(state.ExteriorGain, Unit(state.Openness), dt, 1.5f, 1.5f);
            state.Duck = Mathf.MoveTowards(state.Duck, 0f, dt * 0.5f);
        }

        private float Envelope(float current, float target, float dt, float attack, float release) =>
            Mathf.MoveTowards(Unit(current), Unit(target), dt / Mathf.Max(0.05f, Positive(target > current ? attack : release)));
        private float Positive(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        private float Unit(float value) => Mathf.Clamp01(Positive(value));
    }
}
