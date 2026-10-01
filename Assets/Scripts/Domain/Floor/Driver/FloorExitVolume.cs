// ============================================================================
// FloorExitVolume.cs
// ============================================================================
// PURPOSE:
//   Reports legacy exit contacts; FloorManager admits them only when the exit is open.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FloorDriver · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Forward valid active trigger contacts without retaining hold state.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Scene-owned. FloorManager resolves collider identities and owns exit admission.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System;

using UnityEngine;


namespace Worsen.Domain.Floor
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FloorExitVolume : MonoBehaviour
    {
        private readonly FloorExitVolumeDriverState _state = new FloorExitVolumeDriverState();
        public event Action<Collider> Contact;
        public void Configure()
        {
            _state.Trigger = GetComponent<BoxCollider>();
        }
        private void Observe(Collider other)
        {
            if (!isActiveAndEnabled || _state.Trigger == null || !_state.Trigger.enabled ||
                other == null || !other.enabled || !other.gameObject.activeInHierarchy) return;
            Contact?.Invoke(other);
        }
        private void OnTriggerEnter(Collider other) => Observe(other);
        private void OnTriggerStay(Collider other) => Observe(other);
    }
}