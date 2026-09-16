// ============================================================================
// CakePickup.cs
// ============================================================================
// PURPOSE:
//   Reports moving trigger contact without deciding who may collect a cake.
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
//   Scene-owned per pickup. FloorDriver configures the stable anchor identity; FloorManager resolves Collider to EntityId.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Floor
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class CakePickup : MonoBehaviour
    {
        private int _anchorId;
        private PickupKind _kind;
        public int AnchorId => _anchorId;
        public PickupKind Kind => _kind;
        public event Action<Collider, int, PickupKind> Contact;
        public void Configure(int anchorId, PickupKind kind) { _anchorId = anchorId; _kind = kind; }
        private void OnTriggerEnter(Collider other) => Contact?.Invoke(other, _anchorId, _kind);
        private void OnTriggerStay(Collider other) => Contact?.Invoke(other, _anchorId, _kind);
    }
}