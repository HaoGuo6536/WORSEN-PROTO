// ============================================================================
// HunterRegistry.cs
// ============================================================================
// PURPOSE:
//   Exposes spawned hunters without requiring persistent systems to retain scene objects.
//   Factory and entity lifecycle remove registrations before they can become stale.
// ARCHITECTURAL ROLE:
//   Registry (§8) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Hold registered identities and provide lookup and read-only enumeration.
// DEPENDENCIES:
//   - HunterManager and Core identities only.
// USAGE NOTES:
//   Scene-owned registrants; SubsystemRegistration clears no-domain-reload sessions.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public static class HunterRegistry
    {
        private static readonly List<HunterManager> Hunters = new List<HunterManager>();
        private static readonly IReadOnlyList<HunterManager> View = Hunters.AsReadOnly();
        public static IReadOnlyList<HunterManager> Items => View;
        public static bool TryGet(EntityId id, out HunterManager hunter)
        {
            foreach (HunterManager candidate in Hunters)
                if (candidate != null && candidate.Id == id) { hunter = candidate; return true; }
            hunter = null; return false;
        }
        internal static void Register(HunterManager hunter) { if (!Hunters.Contains(hunter)) Hunters.Add(hunter); }
        internal static void Unregister(HunterManager hunter) { Hunters.Remove(hunter); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Hunters.Clear(); }
    }
}
