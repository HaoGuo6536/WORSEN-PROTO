// ============================================================================
// TelemetryDefinitions.cs
// ============================================================================
//
// PURPOSE:
//   Carries versioned input/probe pairs and tick-stamped observational measurements.
//   It keeps cooperating systems on one contract without sharing mutable state.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared boilerplate contracts.
//
// KEY RESPONSIBILITIES:
//   - Distinguish accepted projectile and ground-spike damage while preserving prior values.
//   - Carry explicit values across system and layer boundaries.
//   - Preserve replay and measurement identity without engine object references.
//
// DEPENDENCIES:
//   - Core definitions and pure UnityEngine value types only.
//
// USAGE NOTES:
//   EventId zero has no deduplication identity. Unknown outcomes remain unclassified.
//   These values contain no engine operations or gameplay decision logic.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    public enum InputSource { Live, Playback }
    public enum ChaseEndReason { Unknown, Lunge, Cornered, Lost, Projectile, GroundSpike }
    public enum TelemetrySampleKind
    {
        HorizontalSpeed, ChaseStarted, ChaseEnded, InputLockStarted, InputLockEnded,
        LookBackStarted, LookBackEnded, VaultAttempt, VaultFailed, Proximity, Heat, FloorTime, AcceptedHit
    }
    public readonly struct MovementResolution
    {
        public MovementResolution(Vector3 position, Vector3 velocity, bool grounded, bool ceiling, Vector3 eyePosition)
        {
            Present = true;
            Position = position;
            Velocity = velocity;
            Grounded = grounded;
            Ceiling = ceiling;
            EyePosition = eyePosition;
        }
        public bool Present { get; }
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }
        public bool Grounded { get; }
        public bool Ceiling { get; }
        public Vector3 EyePosition { get; }
    }
    public readonly struct InputProbeRecord
    {
        public const int CurrentSchemaVersion = 1;
        public InputProbeRecord(int schemaVersion, long tick, InputFrame input, MovementProbe probe, float deltaTime = 1f / 60f, MovementResolution resolution = default)
        {
            SchemaVersion = schemaVersion;
            Tick = tick;
            Input = input;
            Probe = probe;
            DeltaTime = deltaTime;
            Resolution = resolution;
        }
        public int SchemaVersion { get; }
        public long Tick { get; }
        public InputFrame Input { get; }
        public MovementProbe Probe { get; }
        public float DeltaTime { get; }
        public MovementResolution Resolution { get; }
    }
    public readonly struct RunCaptureMetadata
    {
        public const int CurrentSchemaVersion = 1;
        public int SchemaVersion => CurrentSchemaVersion;
        public RunCaptureMetadata(string sessionId, int seed, float fixedDeltaTime, string sourceRevision, string configSnapshotHash, string randomConsumptionOrder, long startTick)
        {
            SessionId = sessionId;
            Seed = seed;
            FixedDeltaTime = fixedDeltaTime;
            SourceRevision = sourceRevision;
            ConfigSnapshotHash = configSnapshotHash;
            RandomConsumptionOrder = randomConsumptionOrder;
            StartTick = startTick;
        }
        public string SessionId { get; }
        public int Seed { get; }
        public float FixedDeltaTime { get; }
        public string SourceRevision { get; }
        public string ConfigSnapshotHash { get; }
        public string RandomConsumptionOrder { get; }
        public long StartTick { get; }
    }
    public readonly struct TelemetrySample
    {
        public TelemetrySample(long tick, EntityId player, int chaseId, TelemetrySampleKind kind, float value = 0f, string detail = "", bool inChase = false, ChaseEndReason outcome = ChaseEndReason.Unknown, long eventId = 0)
        {
            Tick = tick;
            Player = player;
            ChaseId = chaseId;
            Kind = kind;
            Value = value;
            Detail = detail;
            InChase = inChase;
            Outcome = outcome;
            EventId = eventId;
        }
        public long Tick { get; }
        public EntityId Player { get; }
        public int ChaseId { get; }
        public TelemetrySampleKind Kind { get; }
        public float Value { get; }
        public string Detail { get; }
        public bool InChase { get; }
        public ChaseEndReason Outcome { get; }
        public long EventId { get; }
    }
}
