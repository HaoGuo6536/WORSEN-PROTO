// ============================================================================
// HorrorWebDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains copied Weaver visuals and their remaining lifetimes, not collision rules.
//   The rendering boundary owns the associated objects and clears them on floor reset.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Separate warning, projectile glow and doorway identity by hunter and serial.
// DEPENDENCIES:
//   - Core Weaver facts and passive Unity renderer references only.
// USAGE NOTES:
//   Scene-owned through HorrorDriver; never publishes events or operates on objects.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Presentation.Horror
{
    public sealed class HorrorWebDriverState
    {
        public readonly Dictionary<(EntityId, WeaverFactKind, int), HorrorWebVisualDriverState> Visuals = new Dictionary<(EntityId, WeaverFactKind, int), HorrorWebVisualDriverState>();
        public readonly Dictionary<(EntityId, WeaverFactKind, int), long> Ticks = new Dictionary<(EntityId, WeaverFactKind, int), long>();
        public readonly Dictionary<(EntityId, WeaverFactKind, int), GameObject> Objects = new Dictionary<(EntityId, WeaverFactKind, int), GameObject>();
    }
    public sealed class HorrorWebVisualDriverState
    {
        public WeaverFact Fact;
        public float Remaining;
    }
}
