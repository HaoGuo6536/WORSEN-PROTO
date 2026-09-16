// ============================================================================
// PlayerRegistry.cs
// ============================================================================
// PURPOSE:
//   Exposes registered player instances to longer-lived consumers without scene searches.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Registry (§8) · Domain · Player (Service system).
// KEY RESPONSIBILITIES:
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Scene-owned registrants; static collection resets for domain-reload-disabled Play Mode. Factory registers and unregisters each owned identity.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Player
{
    public static class PlayerRegistry
    {
        private static readonly List<PlayerManager> Players = new List<PlayerManager>();
        private static readonly IReadOnlyList<PlayerManager> View = Players.AsReadOnly();
        public static IReadOnlyList<PlayerManager> Items => View;
        public static bool TryGet(EntityId id, out PlayerManager player)
        {
            foreach (PlayerManager candidate in Players)
                if (candidate != null && candidate.Id == id) { player = candidate; return true; }
            player = null;
            return false;
        }
        internal static void Register(PlayerManager player) { if (!Players.Contains(player)) Players.Add(player); }
        internal static void Unregister(PlayerManager player) { Players.Remove(player); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Players.Clear(); }
    }
}