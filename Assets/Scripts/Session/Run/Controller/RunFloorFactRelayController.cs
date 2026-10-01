// ============================================================================
// RunFloorFactRelayController.cs
// ============================================================================
// PURPOSE:
//   Groups floor presentation facts separately from the canonical Run tick.
//   The Manager still records collection and phase transitions before publishing
//   their committed facts; this dispatcher neither changes nor buffers payloads.
// ARCHITECTURAL ROLE:
//   Controller (§2, pure-C# dispatcher) · Session · Run.
// KEY RESPONSIBILITIES:
//   - Relay observational floor facts through the injected pause gate.
//   - Publish collection and phase facts after the Manager commits their rules.
//   - Clear upward subscriptions at Run teardown without replacing the channel.
// DEPENDENCIES:
//   - Core payloads, Unity value types and an injected pause predicate only.
// USAGE NOTES:
//   Owned by the persistent Run Manager. It pairs all source subscriptions on
//   enable/disable; observers access events only. No scene publisher is retained.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Session.Run
{
    public sealed class RunFloorFactRelayController
    {
        private readonly Func<bool> isPaused;
        public RunFloorFactRelayController(Func<bool> isPaused)
        { this.isPaused = isPaused ?? throw new ArgumentNullException(nameof(isPaused)); }

        public event Action<PickupCollectedFact, Vector3> PickupCollected;
        public event Action<RoomDestructionSample> RoomDestructionPublished;
        public event Action<IReadOnlyList<GuidanceTarget>> GuidanceChanged;
        public event Action<int, int, PickupKind, long> CakeLost;
        public event Action<RoomPhaseChangedFact> RoomPhaseChanged;
        public event Action<FloorBoundaryImpulseFact> BoundaryImpulsePublished;
        internal void PublishBoundaryImpulse(FloorBoundaryImpulseFact fact)
        { if (!isPaused()) BoundaryImpulsePublished?.Invoke(fact); }

        internal void HandleRoomDestruction(RoomDestructionSample sample) { if (!isPaused()) RoomDestructionPublished?.Invoke(sample); }
        internal void HandleGuidance(IReadOnlyList<GuidanceTarget> targets) { if (!isPaused()) GuidanceChanged?.Invoke(targets); }
        internal void HandleCakeLost(int anchorId, int roomId, PickupKind kind, long tick)
        { if (!isPaused()) CakeLost?.Invoke(anchorId, roomId, kind, tick); }
        internal void PublishPickup(PickupCollectedFact fact, Vector3 position) => PickupCollected?.Invoke(fact, position);
        internal void PublishRoomPhase(RoomPhaseChangedFact fact) => RoomPhaseChanged?.Invoke(fact);

        internal void Teardown()
        {
            PickupCollected = null; RoomDestructionPublished = null;
            GuidanceChanged = null; CakeLost = null; RoomPhaseChanged = null;
            BoundaryImpulsePublished = null;
        }
    }
}
