// ============================================================================
// DirectorBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Holds the Director's clock and each player's independent pacing history.
//   The Controller owns changes to this passive data so scene teardown can clear
//   heat, relief, assignment, and intrusion episodes together without stale state.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Director.
// KEY RESPONSIBILITIES:
//   - Store accumulated evaluation time and bounded per-player position buffers.
//   - Keep pressure gaps separate from repeated hint and intrusion scheduling.
// DEPENDENCIES:
//   - Core identities and Director-local data definitions only.
// USAGE NOTES:
//   Pure scene-owned data; no events or engine access. No foreign system reads
//   this mutable state; observations leave through Core facts on DirectorManager.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Director
{
    public sealed class DirectorBehaviorState
    {
        public double ElapsedSeconds { get; set; }
        public double EvaluationAccumulatorSeconds { get; set; }
        public long LastTick { get; set; } = -1;
        public long EvaluationCount { get; set; }
        public Dictionary<EntityId, DirectorPlayerBehaviorState> Players { get; } = new Dictionary<EntityId, DirectorPlayerBehaviorState>();
    }

    public sealed class DirectorPlayerBehaviorState
    {
        public EntityId PlayerId { get; set; }
        public EntityId AssignedHunterId { get; set; }
        public double HeatSeconds { get; set; }
        public double ReliefSeconds { get; set; }
        public double LastHintDeliveredAtSeconds { get; set; }
        public bool HasIssuedHint { get; set; }
        public bool WasChasing { get; set; }
        public bool IsWithinProximity { get; set; }
        public double SlowSeconds { get; set; }
        public bool IntrusionIssuedThisEpisode { get; set; }
        public double IntrusionAvailableAtSeconds { get; set; }
        public DirectorPositionSample[] History { get; set; }
        public int HistoryCount { get; set; }
        public int HistoryNextIndex { get; set; }
    }
}
