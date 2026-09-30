// ============================================================================
// TickingKeyContact.cs
// ============================================================================
// PURPOSE:
//   Reports physical key touches without applying winding or recognizing players.
//   The owning driver filters the object; its Manager resolves the collector id.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by TickingDriver · Domain · Hunter Ticking.
// KEY RESPONSIBILITIES:
//   - Relay trigger enter/stay so a rejected or paused contact is not lost forever.
// DEPENDENCIES:
//   - UnityEngine physics and System events only.
// USAGE NOTES:
//   Scene-owned. Static trigger handshake resets with subsystem registration;
//   each TickingDriver pairs its subscription in OnEnable and OnDisable.
// ============================================================================
using System;
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Ticking
{
    public sealed class TickingKeyContact : MonoBehaviour
    {
        public static event Action<GameObject, Collider> OnContact;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents() { OnContact = null; }
        private void OnTriggerEnter(Collider other) { OnContact?.Invoke(gameObject, other); }
        private void OnTriggerStay(Collider other) { OnContact?.Invoke(gameObject, other); }
    }
}
