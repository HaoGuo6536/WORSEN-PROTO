// ============================================================================
// EchoController.cs
// ============================================================================
// PURPOSE:
//   Mirrors the player's recorded position and heading after an injected delay.
//   The first pose is the player's spawn, never the ordinary Hunter spawn point.
//   Playback is kinematic: doors, traversal restrictions and hits cannot divert it.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Record a bounded pose ring and advance a monotonic playback cursor.
//   - Publish delayed footsteps and copied Trail Reader segments.
//   - Read capped active-effect multipliers without changing shared assets.
//   - Admit contact once per tick without lunge windup or recovery.
// DEPENDENCIES:
//   - Parent Hunter replay contracts, injected Player pose and Core effects.
// USAGE NOTES:
//   Initialize at round start and tick after Player commits its pose. Faster
//   Playback may consume backlog, never future data. Removing it holds the cursor
//   until the fixed delay catches up rather than rewinding. Undersized recordings
//   publish an overrun and hide until the requested timestamp is retained again.
//   Exactness is to tick samples; sub-tick positions/headings interpolate between them.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Hunter.Archetypes.Echo
{
    public sealed class EchoController : HunterArchetypeController, IHunterKinematicReplayRules, IHunterAttackRules
    {
        public static readonly EffectId ShorterDelay = new EffectId("echo-shorter-delay");
        public static readonly EffectId FasterPlayback = new EffectId("echo-faster-playback");
        public static readonly EffectId SilentSteps = new EffectId("echo-silent-steps");
        public static readonly EffectId TrailReader = new EffectId("trail-reader");
        private readonly EchoBehaviorState _state;
        private readonly EchoConfig _config;
        public EchoController(EchoConfig config) : this(new EchoBehaviorState(), config) { }
        public EchoController(EchoBehaviorState state, EchoConfig config)
        { _state = state ?? throw new ArgumentNullException(nameof(state)); _config = config ?? throw new ArgumentNullException(nameof(config)); }
        public override bool OwnsPursuit => true;
        public override bool NeverLoses => true;
        public bool UsesSharedAttacks => false;
        public bool ReplayActive => _state.Active;
        public HunterReplayPose ReplayPose => _state.Pose;
        public IReadOnlyList<HunterReplayPose> ReplayPoses => _state.Motion;
        public bool ContactReady => _state.Active && _state.Context.Player.IsAlive && _state.LastContactTick != _state.LastTick;
        public void CommitContact() { _state.LastContactTick = _state.LastTick; }
        public float EffectiveDelay => _config.DelaySeconds * Multiplier(ShorterDelay, _config.ShorterDelayMultiplier);
        public float EffectivePlayback => Multiplier(FasterPlayback, _config.FasterPlaybackMultiplier);
        private float Multiplier(EffectId effect, float multiplier)
            => Mathf.Pow(multiplier, Mathf.Clamp(_state.Context.Effects?.Stacks(effect) ?? 0, 0, _config.CurseStackCap));
        public override void Reset(HunterArchetypeContext context)
        {
            _state.Samples = new EchoSample[_config.SampleCapacity];
            _state.First = 0; _state.Next = 0; _state.Cursor = 0;
            _state.Now = 0; _state.PlaybackTime = 0;
            _state.LastTick = _state.LastNoiseTick = _state.LastContactTick = -1;
            _state.Context = context;
            _state.Pose = new HunterReplayPose(context.Player.Position, context.Player.HeadingDegrees);
            _state.Active = context.Player.IsAlive && EffectiveDelay == 0f;
            _state.Started = _state.Active; _state.Overrun = false;
            _state.LookBack = context.Player.LookBack;
            _state.Motion.Clear(); _state.Facts.Clear();
            Append(-1, 0);
        }
        public override void Tick(HunterArchetypeContext context)
        {
            _state.Motion.Clear();
            if (!(context.DeltaTime > 0f) || float.IsNaN(context.DeltaTime) || float.IsInfinity(context.DeltaTime) ||
                context.Tick <= _state.LastTick) return;
            _state.Context = context; _state.LastTick = context.Tick;

            _state.Now += context.DeltaTime;
            int footsteps = 0;
            long latestNoise = _state.LastNoiseTick;
            if (context.Player.RecentNoises != null)
                foreach (NoiseEvent noise in context.Player.RecentNoises)
                    if (noise.Source == context.Player.Id && noise.SourceKind == NoiseSourceKind.Footstep &&
                        noise.Tick > _state.LastNoiseTick && noise.Tick <= context.Tick)
                    { footsteps++; latestNoise = Math.Max(latestNoise, noise.Tick); }
            _state.LastNoiseTick = latestNoise;
            Append(context.Tick, footsteps);
            PrepareMotion();
            if (_state.Active && context.Player.LookBack && !_state.LookBack &&
                (context.Effects?.Has(TrailReader) ?? false)) RevealTrail();
            _state.LookBack = context.Player.LookBack;
        }
        private void Append(long tick, int footsteps)
        {
            long sequence = _state.Next++;
            _state.Samples[sequence % _state.Samples.Length] = new EchoSample(sequence, _state.Now,
                _state.Context.Player.Position, _state.Context.Player.HeadingDegrees, tick, footsteps);
            if (_state.Next - _state.First > _state.Samples.Length) _state.First++;
        }
        private EchoSample Sample(long sequence) => _state.Samples[sequence % _state.Samples.Length];
        private void PrepareMotion()
        {
            // Inputs are floats even though the accumulated clock is double. Treat one
            // float rounding unit across the delay as a sample boundary, not motion.
            double timeTolerance = EffectiveDelay * (1.0 / (1 << 23));
            double delayedTime = _state.Now - EffectiveDelay;
            _state.Active = _state.Context.Player.IsAlive && (_state.Started || delayedTime >= -timeTolerance);
            if (!_state.Active) return;
            double until = Math.Max(_state.PlaybackTime, delayedTime);
            if (_state.Started && EffectivePlayback > 1f)
                until = Math.Max(until, _state.PlaybackTime + _state.Context.DeltaTime * EffectivePlayback);
            until = Math.Min(_state.Now, until);
            EchoSample oldest = Sample(_state.First);
            if (until < oldest.Time - timeTolerance)
            {
                if (!_state.Overrun) Emit(HunterArchetypeFactKind.RecordingOverrun, _state.Pose.Position);
                _state.Overrun = true; _state.Active = false; return;
            }
            _state.Overrun = false;
            if (!_state.Started || _state.Cursor < _state.First)
            {
                _state.Cursor = _state.First;
                _state.Pose = new HunterReplayPose(oldest.Position, oldest.HeadingDegrees);
                _state.Motion.Add(_state.Pose);
            }
            _state.Started = true;
            for (long i = _state.Cursor + 1; i < _state.Next; i++)
            {
                EchoSample a = Sample(i - 1), b = Sample(i);
                if (until <= a.Time + timeTolerance) break;
                bool complete = b.Time <= until + timeTolerance;
                float fraction = (float)((until - a.Time) / (b.Time - a.Time));
                _state.Pose = complete ? new HunterReplayPose(b.Position, b.HeadingDegrees) :
                    new HunterReplayPose(Vector3.Lerp(a.Position, b.Position, fraction),
                        Mathf.LerpAngle(a.HeadingDegrees, b.HeadingDegrees, fraction));
                _state.Motion.Add(_state.Pose);
                if (!complete) break;
                _state.Cursor = i;
                for (int step = 0; step < b.Footsteps; step++)
                    Emit(HunterArchetypeFactKind.ReplayedFootstep, b.Position, b.Tick,
                        gain: _config.FootstepGain * Multiplier(SilentSteps, _config.SilentStepsMultiplier), pitch: _config.FootstepPitch);
            }
            _state.PlaybackTime = until;
        }
        private void RevealTrail()
        {
            var points = new List<Vector3> { _state.Pose.Position };
            for (long i = _state.Cursor + 1; i < _state.Next && points.Count < _config.TrailPointLimit; i++)
                points.Add(Sample(i).Position);
            Emit(HunterArchetypeFactKind.TrailRevealed, _state.Pose.Position, duration: _config.TrailSeconds, path: points);
        }
        private void Emit(HunterArchetypeFactKind kind, Vector3 position, long recordedTick = -1, int objectId = -1,
            float gain = 1f, float pitch = 1f, float duration = 0f, IReadOnlyList<Vector3> path = null)
        {
            if (_state.Facts.Count >= _config.SampleCapacity) _state.Facts.Dequeue();
            _state.Facts.Enqueue(new HunterArchetypeFact(_state.Context.Hunter.Id, kind, position, _state.Context.Tick,
                recordedTick, objectId, gain, pitch, duration, path));
        }
        public override bool FilterVisibility(bool visible, SightProbe probe, HunterArchetypeContext context)
            => false;
        public override bool TryTakeFact(out HunterArchetypeFact fact)
        { fact = default; if (_state.Facts.Count == 0) return false; fact = _state.Facts.Dequeue(); return true; }
    }
}
