// ============================================================================
// FloorExitVolume.cs
// ============================================================================
// PURPOSE:
//   Reports a player walking into or standing within the authored exit trigger.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FloorDriver · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Scene-owned. Contacts while locked are rejected by the Controller, and stay contacts allow an already overlapping player to exit as soon as it opens.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System;
using UnityEngine;

namespace Worsen.Domain.Floor
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FloorExitVolume : MonoBehaviour
    {
        public event Action<Collider> Contact;
        private void OnTriggerEnter(Collider other) => Contact?.Invoke(other);
        private void OnTriggerStay(Collider other) => Contact?.Invoke(other);
    }
}