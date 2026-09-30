// ============================================================================
// EchoController.cs
// ============================================================================
// PURPOSE:
//   Replays the player's tick-sampled polyline without planning a different route.
//   A monotonic cursor preserves order and recorded pace; doors pause it, while
//   forbidden transitions discard the leg and wait for a later trail intersection.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Record a bounded ring and acknowledge only physically completed motion.
//   - Publish delayed footsteps, door passages and copied Trail Reader segments.
//   - Read capped active-effect multipliers without changing shared assets.
// DEPENDENCIES:
//   - Hunter definitions/default rules, injected Player/Level/Floor views and Core effects.
// USAGE NOTES:
//   Never backtracks means recording order never rewinds, even if the player loops.
//   Off-trail spawns, post-lunge displacement and overwritten history wait for a
//   later recorded segment through the held position; they never teleport or reroute.
//   Exactness is to the injected tick samples, not unobserved sub-tick movement.
//   Level edges lack portal geometry: ambiguous parallel edges fail closed.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter.Archetypes.Default;
namespace Worsen.Domain.Hunter.Archetypes.Echo
{
    public sealed class EchoController : DefaultHunterController
    {
        public static readonly EffectId ShorterDelay = new EffectId("echo.shorter-delay");
        public static readonly EffectId FasterPlayback = new EffectId("echo.faster-playback");
        public static readonly EffectId SilentSteps = new EffectId("echo.silent-steps");
        public static readonly EffectId TrailReader = new EffectId("trail-reader");
        private readonly EchoBehaviorState _state;
        private readonly EchoConfig _config;
        public EchoController(EchoConfig config) : this(new EchoBehaviorState(), config) { }
        public EchoController(EchoBehaviorState state, EchoConfig config)
        { _state = state ?? throw new ArgumentNullException(nameof(state)); _config = config ?? throw new ArgumentNullException(nameof(config)); }
        public override bool OwnsPursuit => true;
        public override bool NeverLoses => true;
        public override IReadOnlyList<Vector3> ReplayPath => _state.Motion;
        public float EffectiveDelay => _config.DelaySeconds * Multiplier(ShorterDelay, _config.ShorterDelayMultiplier);
        public float EffectivePlayback => _state.Context.SpeedRatio * Multiplier(FasterPlayback, _config.FasterPlaybackMultiplier);
        private float Multiplier(EffectId effect, float multiplier)
            => Mathf.Pow(multiplier, Mathf.Clamp(_state.Context.Effects?.Stacks(effect) ?? 0, 0, _config.CurseStackCap));
        public override void Reset(HunterArchetypeContext context)
        {
            _state.Samples = new EchoSample[_config.SampleCapacity];
            _state.First = 0; _state.Next = 0; _state.Cursor = 0;
            _state.Now = 0; _state.PlaybackTime = 0; _state.LastTick = -1; _state.LastNoiseTick = -1;
            _state.Position = context.Hunter.Position; _state.Context = context;
            _state.PreviousDelay = EffectiveDelay; _state.DelayReduction = 0f;
            _state.Attached = Near(_state.Position, context.Player.Position);
            _state.LookBack = context.Player.LookBack;
            _state.Motion.Clear(); _state.Pending.Clear(); _state.Facts.Clear();
            Append(context.Player.Position, 0, -1, 0, -1);
        }
        public override void Tick(HunterArchetypeContext context)
        {
            _state.Motion.Clear(); _state.Pending.Clear();
            if (!(context.DeltaTime > 0f) || float.IsNaN(context.DeltaTime) || float.IsInfinity(context.DeltaTime) ||
                context.Tick <= _state.LastTick) return;
            _state.Context = context; _state.LastTick = context.Tick;
            _state.DelayReduction = Mathf.Max(0f, _state.PreviousDelay - EffectiveDelay);
            _state.PreviousDelay = EffectiveDelay;
            _state.Now += context.DeltaTime;
            int footsteps = 0;
            long latestNoise = _state.LastNoiseTick;
            if (context.Player.RecentNoises != null)
                foreach (NoiseEvent noise in context.Player.RecentNoises)
                    if (noise.Source == context.Player.Id && noise.SourceKind == NoiseSourceKind.Footstep &&
                        noise.Tick > _state.LastNoiseTick && noise.Tick <= context.Tick)
                    { footsteps++; latestNoise = Math.Max(latestNoise, noise.Tick); }
            _state.LastNoiseTick = latestNoise;
            int room = HunterNavigationUtility.RoomAt(context.Level.Graph, context.Player.Position);
            int previousRoom = Sample(_state.Next - 1).Room;
            int door = OpenDoor(previousRoom, room);
            Append(context.Player.Position, room, context.Tick, footsteps, door);
            if (!Near(_state.Position, context.Hunter.Position)) _state.Attached = false;
            _state.Position = context.Hunter.Position;
            if (context.CanReplay && context.Player.IsAlive) PrepareMotion();
            if (context.CanReplay && context.Player.IsAlive && context.Player.LookBack && !_state.LookBack &&
                (context.Effects?.Has(TrailReader) ?? false)) RevealTrail();
            _state.LookBack = context.Player.LookBack;
        }
        private void Append(Vector3 position, int room, long tick, int footsteps, int door)
        {
            if (_state.Next == 0) room = HunterNavigationUtility.RoomAt(_state.Context.Level.Graph, position);
            if (room == 0 && _state.Next > 0) room = Sample(_state.Next - 1).Room;
            long sequence = _state.Next++;
            _state.Samples[sequence % _state.Samples.Length] = new EchoSample(sequence, _state.Now, position, room, tick, footsteps, door);
            if (_state.Next - _state.First <= _state.Samples.Length) return;
            _state.First++;
            if (_state.Cursor >= _state.First) return;
            _state.Cursor = _state.First - 1; _state.Attached = false;
            Emit(HunterArchetypeFactKind.RecordingOverrun, position);
        }
        private EchoSample Sample(long sequence) => _state.Samples[sequence % _state.Samples.Length];
        private bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= _config.PathTolerance * _config.PathTolerance;
        private void PrepareMotion()
        {
            double delayedTime = _state.Now - EffectiveDelay;
            if (delayedTime <= 0) return;
            if (!_state.Attached)
            {
                for (long i = Math.Max(_state.First + 1, _state.Cursor + 1); i < _state.Next; i++)
                {
                    EchoSample a = Sample(i - 1), b = Sample(i);
                    if (b.Time > delayedTime + 0.000001) break;
                    Vector3 segment = b.Position - a.Position;
                    float t = segment.sqrMagnitude > 0f ? Vector3.Dot(_state.Position - a.Position, segment) / segment.sqrMagnitude : 0f;
                    if (t < 0f || t > 1f || !Near(a.Position + segment * t, _state.Position) || !Allowed(a, b, out _)) continue;
                    _state.Cursor = i - 1; _state.PlaybackTime = a.Time + (b.Time - a.Time) * t;
                    _state.Attached = true; break;
                }
                if (!_state.Attached) return;
            }
            // A neutral replay cannot get ahead of its fixed delay. Faster playback
            // may consume the backlog but can never read unrecorded future positions.
            double ceiling = EffectivePlayback > 1f ? _state.Now : delayedTime;
            double until = Math.Min(ceiling, _state.PlaybackTime +
                _state.Context.DeltaTime * EffectivePlayback + _state.DelayReduction);
            Vector3 position = _state.Position;
            for (long i = _state.Cursor + 1; i < _state.Next; i++)
            {
                EchoSample a = Sample(i - 1), b = Sample(i);
                if (until <= a.Time || until <= _state.PlaybackTime) break;
                if (!Allowed(a, b, out bool truncate))
                {
                    if (truncate && _state.Motion.Count == 0)
                    {
                        _state.Cursor = i; _state.Attached = false;
                        Emit(HunterArchetypeFactKind.ReplayTruncated, position, b.Tick);
                    }
                    break;
                }
                bool complete = b.Time <= until + 0.000001;
                double time = complete ? b.Time : until;
                Vector3 point = complete ? b.Position : Vector3.Lerp(a.Position, b.Position, (float)((time - a.Time) / (b.Time - a.Time)));
                _state.Motion.Add(point);
                _state.Pending.Add(new EchoSample(i, time, point, b.Room, b.Tick, b.Footsteps, b.Door, complete));
                position = point;
                if (!complete) break;
            }
        }
        private bool Allowed(EchoSample a, EchoSample b, out bool truncate)
        {
            truncate = false;
            LevelGraph graph = _state.Context.Level.Graph;
            if (!_state.Context.Level.IsReady || graph == null) return false;
            if (_state.Context.UnavailableRooms != null)
                foreach (Bounds room in _state.Context.UnavailableRooms)
                    if (Intersects(room, a.Position, b.Position)) return false;
            if (_state.Context.Floor != null)
                foreach (LevelRoom room in graph.Rooms)
                    if (_state.Context.Floor.RoomPhases.TryGetValue(room.Id, out RoomPhase phase) && phase == RoomPhase.Closed)
                    {
                        if (Intersects(room.Bounds, a.Position, b.Position)) return false;
                    }
            if (a.Room == b.Room) return true;
            int matches = 0;
            foreach (LevelEdge edge in graph.Edges)
            {
                bool forward = edge.FromRoomId == a.Room && edge.ToRoomId == b.Room;
                bool reverse = edge.ToRoomId == a.Room && edge.FromRoomId == b.Room;
                if (!forward && !reverse) continue;
                matches++;
                if ((!forward && !edge.Bidirectional) || (edge.Access & TraversalAccess.Hunter) == 0) truncate = true;
                if ((_state.Context.ClosedDoors?.TryGetValue(edge.Id, out bool closed) ?? false) && closed)
                { truncate = false; return false; }
            }
            if (matches != 1) return false; // No inferred portal when topology is ambiguous.
            return !truncate;
        }
        private static bool Intersects(Bounds room, Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            return room.Contains(a) || room.Contains(b) || (delta.sqrMagnitude > 0f &&
                room.IntersectRay(new Ray(a, delta.normalized), out float distance) && distance <= delta.magnitude);
        }
        private int OpenDoor(int from, int to)
        {
            if (from == to || to == 0 || _state.Context.Interactables == null || _state.Context.Level.Graph == null) return -1;
            var doors = new List<InteractableState>(_state.Context.Interactables.InRoom(from));
            doors.AddRange(_state.Context.Interactables.InRoom(to));
            foreach (InteractableState door in doors)
                if (door.Kind == InteractableKind.Door && door.Value == InteractableStateValue.Open)
                    foreach (LevelEdge edge in _state.Context.Level.Graph.Edges)
                        if (edge.Id == door.EdgeId && ((edge.FromRoomId == from && edge.ToRoomId == to) ||
                            (edge.Bidirectional && edge.ToRoomId == from && edge.FromRoomId == to))) return door.Id;
            return -1;
        }
        public override void CommitReplay(int reachedPoints, bool unreachable = false)
        {
            if (reachedPoints < 0 || reachedPoints > _state.Pending.Count) throw new ArgumentOutOfRangeException(nameof(reachedPoints));
            for (int i = 0; i < reachedPoints; i++)
            {
                EchoSample sample = _state.Pending[i];
                _state.Position = sample.Position; _state.PlaybackTime = sample.Time;
                if (!sample.Complete) continue;
                _state.Cursor = sample.Sequence;
                for (int step = 0; step < sample.Footsteps; step++)
                    Emit(HunterArchetypeFactKind.ReplayedFootstep, sample.Position, sample.Tick,
                        gain: _config.FootstepGain * Multiplier(SilentSteps, _config.SilentStepsMultiplier), pitch: _config.FootstepPitch);
                if (sample.Door >= 0) Emit(HunterArchetypeFactKind.ReplayedDoorPassage, sample.Position, sample.Tick, sample.Door);
            }
            if (unreachable && reachedPoints < _state.Pending.Count)
            {
                EchoSample rejected = _state.Pending[reachedPoints];
                _state.Cursor = rejected.Sequence; _state.Attached = false;
                Emit(HunterArchetypeFactKind.ReplayTruncated, _state.Position, rejected.Tick);
            }
            _state.Pending.Clear(); _state.Motion.Clear();
        }
        private void RevealTrail()
        {
            var points = new List<Vector3> { _state.Position };
            if (!_state.Attached) return;
            for (long i = Math.Max(_state.Cursor + 1, _state.First + 1); i < _state.Next && points.Count < _config.TrailPointLimit; i++)
            {
                if (!Allowed(Sample(i - 1), Sample(i), out _)) break;
                points.Add(Sample(i).Position);
            }
            Emit(HunterArchetypeFactKind.TrailRevealed, _state.Position, duration: _config.TrailSeconds, path: points);
        }
        private void Emit(HunterArchetypeFactKind kind, Vector3 position, long recordedTick = -1, int objectId = -1,
            float gain = 1f, float pitch = 1f, float duration = 0f, IReadOnlyList<Vector3> path = null)
        {
            if (_state.Facts.Count >= _config.SampleCapacity) _state.Facts.Dequeue();
            _state.Facts.Enqueue(new HunterArchetypeFact(_state.Context.Hunter.Id, kind, position, _state.Context.Tick,
                recordedTick, objectId, gain, pitch, duration, path));
        }
        public override bool FilterVisibility(bool visible, SightProbe probe, HunterArchetypeContext context)
            => _state.Now >= EffectiveDelay && (probe.HeadVisible || probe.ChestVisible || probe.HipsVisible) &&
                Mathf.Abs(context.Player.Position.y - context.Hunter.Position.y) <= _config.ContactElevation &&
                Vector3.Distance(context.Player.Position, context.Hunter.Position) <= _config.ContactRadius;
        public override float GoalUtility(HunterGoal goal, float utility) => goal == HunterGoal.LocatePrey ? utility : 0f;
        public override bool TryMovement(out Vector3 target, out float speed)
        { target = _state.Motion.Count > 0 ? _state.Motion[_state.Motion.Count - 1] : _state.Position; speed = 0f; return true; }
        public override bool TryTakeFact(out HunterArchetypeFact fact)
        { fact = default; if (_state.Facts.Count == 0) return false; fact = _state.Facts.Dequeue(); return true; }
    }
}
