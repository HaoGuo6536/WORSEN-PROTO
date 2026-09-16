// ============================================================================
// TelemetryDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains a single capture and the previous player facts needed for edge conversion. It stores data only; the Presenter owns interpretation and the Driver owns files.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Store raw samples, capture metadata, prior movement and local output status.
// DEPENDENCIES:
//   - Core immutable values, local report and System collections.
// USAGE NOTES:
//   - Persistent with TelemetryDriver; cleared explicitly for each run.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Presentation.Telemetry
{
    public sealed class TelemetryDriverState
    {
        public RunCaptureMetadata Metadata;
        public readonly List<TelemetrySample> Samples = new List<TelemetrySample>();
        public readonly Dictionary<EntityId, List<TelemetrySample>> ChaseTransitions = new Dictionary<EntityId, List<TelemetrySample>>();
        public readonly HashSet<long> RecordedEventIds = new HashSet<long>();
        public readonly Dictionary<EntityId, long> LastTaggedTicks = new Dictionary<EntityId, long>();
        public readonly Dictionary<EntityId, int> LastTaggedChases = new Dictionary<EntityId, int>();
        public int LateChaseTransitions;
        public readonly Dictionary<EntityId, PlayerMovementSample> PreviousMovement = new Dictionary<EntityId, PlayerMovementSample>();
        public readonly Dictionary<EntityId, string> LockReasons = new Dictionary<EntityId, string>();
        public readonly Dictionary<EntityId, long> ActiveVaults = new Dictionary<EntityId, long>();
        public int CensoredTraversals;
        public readonly HashSet<TelemetrySampleKind> AvailableStreams = new HashSet<TelemetrySampleKind>();
        public bool Active;
        public long EndTick, LastTick;
        public string LastError = "", OutputPath = "";
        public TelemetryReport Report;
    }
}
