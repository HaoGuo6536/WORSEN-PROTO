// ============================================================================
// DirectorDefinitions.cs
// ============================================================================
//
// PURPOSE:
//   Describes delayed hints, pressure observations and presentation intrusions.
//   Values cross system boundaries without exposing mutable runtime state.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Director shared contracts.
//
// KEY RESPONSIBILITIES:
//   - Carry tick-stamped identity and immutable values between owning systems.
//   - Keep event payloads independent of Domain and Presentation implementations.
//
// DEPENDENCIES:
//   - Core definitions and pure UnityEngine value types only.
//
// USAGE NOTES:
//   Distances are metres and durations are seconds; ticks identify committed steps.
//   Constructors carry supplied values and perform no engine or gameplay operations.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    public readonly struct HintPayload
    {
        public HintPayload(EntityId hunter, EntityId player, long observedTick, long deliveredTick, Vector3 position, float ageSeconds, float radius, float confidence = 0.5f)
        {
            Hunter = hunter;
            Player = player;
            ObservedTick = observedTick;
            DeliveredTick = deliveredTick;
            Position = position;
            AgeSeconds = ageSeconds;
            Radius = radius;
            Confidence = confidence;
        }
        public EntityId Hunter { get; }
        public EntityId Player { get; }
        public long ObservedTick { get; }
        public long DeliveredTick { get; }
        public Vector3 Position { get; }
        public float AgeSeconds { get; }
        public float Radius { get; }
        public float Confidence { get; }
    }
    public readonly struct IntrusionSample
    {
        public IntrusionSample(EntityId player, long tick, float durationSeconds)
        {
            Player = player;
            Tick = tick;
            DurationSeconds = durationSeconds;
        }
        public EntityId Player { get; }
        public long Tick { get; }
        public float DurationSeconds { get; }
    }
    public readonly struct DirectorPressureSample
    {
        public DirectorPressureSample(EntityId player, long tick, float heatSeconds, float reliefSeconds, bool isChasing, bool isWithinProximity, bool hintIssued)
        {
            Player = player;
            Tick = tick;
            HeatSeconds = heatSeconds;
            ReliefSeconds = reliefSeconds;
            IsChasing = isChasing;
            IsWithinProximity = isWithinProximity;
            HintIssued = hintIssued;
        }
        public EntityId Player { get; }
        public long Tick { get; }
        public float HeatSeconds { get; }
        public float ReliefSeconds { get; }
        public bool IsChasing { get; }
        public bool IsWithinProximity { get; }
        public bool HintIssued { get; }
    }
}
