// ============================================================================
// DirectorDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries committed observations into the Director's pure pacing rules.
//   These snapshots keep the Controller independent of live registries and engine
//   objects, while preserving separate identities for every player and hunter.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Director.
// KEY RESPONSIBILITIES:
//   - Describe sampled player movement/chase state and active hunter positions.
//   - Preserve timestamps on history and separate decision data from publication.
// DEPENDENCIES:
//   - Core identities and value-only UnityEngine vectors.
// USAGE NOTES:
//   Director-local inputs only; cross-layer output facts use Core payloads.
//   Position snapshots describe the end of the supplied Session tick.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Director
{
    public readonly struct DirectorPlayerSample
    {
        public DirectorPlayerSample(EntityId playerId, Vector3 position, Vector3 velocity, bool isAlive, bool isChasing)
        { PlayerId = playerId; Position = position; Velocity = velocity; IsAlive = isAlive; IsChasing = isChasing; }
        public EntityId PlayerId { get; }
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }
        public bool IsAlive { get; }
        public bool IsChasing { get; }
    }

    public readonly struct DirectorHunterSample
    {
        public DirectorHunterSample(EntityId hunterId, EntityId targetId, Vector3 position, bool isActive)
        { HunterId = hunterId; TargetId = targetId; Position = position; IsActive = isActive; }
        public EntityId HunterId { get; }
        public EntityId TargetId { get; }
        public Vector3 Position { get; }
        public bool IsActive { get; }
    }

    public readonly struct DirectorPositionSample
    {
        public DirectorPositionSample(long tick, double seconds, Vector3 position)
        { Tick = tick; Seconds = seconds; Position = position; }
        public long Tick { get; }
        public double Seconds { get; }
        public Vector3 Position { get; }
    }

    public readonly struct DirectorTickResult
    {
        public DirectorTickResult(IReadOnlyList<HintPayload> hints, IReadOnlyList<IntrusionSample> intrusions,
            IReadOnlyList<DirectorPressureSample> pressure)
        { Hints = hints; Intrusions = intrusions; Pressure = pressure; }
        public IReadOnlyList<HintPayload> Hints { get; }
        public IReadOnlyList<IntrusionSample> Intrusions { get; }
        public IReadOnlyList<DirectorPressureSample> Pressure { get; }
    }
}
