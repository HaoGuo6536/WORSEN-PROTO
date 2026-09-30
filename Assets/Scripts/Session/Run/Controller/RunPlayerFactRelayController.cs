// ============================================================================
// RunPlayerFactRelayController.cs
// ============================================================================
// PURPOSE:
//   Exposes player recovery, traversal and inventory observations as a typed Run
//   channel. The Run Manager retains damage, revival and snapshot sequencing;
//   this dispatcher only delivers already decided facts to upward observers.
// ARCHITECTURAL ROLE:
//   Controller (§2, pure-C# dispatcher) · Session · Run.
// KEY RESPONSIBILITIES:
//   - Relay player observations synchronously through an injected pause gate.
//   - Publish explicit snapshots without adding a new admission rule.
//   - Release upward subscribers at Run teardown.
// DEPENDENCIES:
//   - Core payloads and an injected pause predicate only.
// USAGE NOTES:
//   Owned by the persistent Run Manager, which pairs input subscriptions on
//   enable/disable. Snapshot publication intentionally remains allowed during
//   pause, matching the previous Manager API. No actor reference is retained.
// ============================================================================
using System;
using Worsen.Core;

namespace Worsen.Session.Run
{
    public sealed class RunPlayerFactRelayController
    {
        private readonly Func<bool> isPaused;
        public RunPlayerFactRelayController(Func<bool> isPaused)
        { this.isPaused = isPaused ?? throw new ArgumentNullException(nameof(isPaused)); }

        public event Action<GraceWindowFact> OnGraceStarted;
        public event Action<GraceWindowFact> OnGraceEnded;
        public event Action<EntityId, long, TraversalKind, float, bool> TraversalProgressed;
        public event Action<EntityId, long, float> PlayerStumbled;
        public event Action<EntityId, float> ShieldChanged;
        public event Action<int> EmptyItemSlotsChanged;
        public event Action<float> SpeedNormalizedPublished;

        internal void HandleGraceStarted(GraceWindowFact fact) { if (!isPaused()) OnGraceStarted?.Invoke(fact); }
        internal void HandleGraceEnded(GraceWindowFact fact) { if (!isPaused()) OnGraceEnded?.Invoke(fact); }
        internal void HandleTraversalProgress(EntityId player, long tick, TraversalKind kind, float progress, bool active)
        { if (!isPaused()) TraversalProgressed?.Invoke(player, tick, kind, progress, active); }
        internal void HandleStumbled(EntityId player, long tick, float duration) { if (!isPaused()) PlayerStumbled?.Invoke(player, tick, duration); }
        internal void HandleShield(EntityId player, float shield) { if (!isPaused()) ShieldChanged?.Invoke(player, shield); }
        internal void PublishShield(EntityId player, float shield) => ShieldChanged?.Invoke(player, shield);
        internal void PublishEmptyItemSlots(int count) => EmptyItemSlotsChanged?.Invoke(count);
        internal void PublishSpeedNormalized(float speed) => SpeedNormalizedPublished?.Invoke(speed);

        internal void Teardown()
        {
            OnGraceStarted = null; OnGraceEnded = null; TraversalProgressed = null;
            PlayerStumbled = null; ShieldChanged = null; EmptyItemSlotsChanged = null;
            SpeedNormalizedPublished = null;
        }
    }
}
