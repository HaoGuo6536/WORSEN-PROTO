// ============================================================================
// FloorExitVolume.cs
// ============================================================================
// PURPOSE:
//   Reports exit contact and last-collider departure so locked-exit holds cannot linger.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FloorDriver · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Cache resolved identities and prune disabled, destroyed or inactive colliders before hold timing.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Scene-owned. Manager-injected identity resolution keeps foreign components out
//   of this sub-driver. Disable/reconfigure reports departures before clearing state.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Floor
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FloorExitVolume : MonoBehaviour
    {
        private readonly FloorExitVolumeDriverState _state = new FloorExitVolumeDriverState();
        private Func<Collider, EntityId> _resolveIdentity;
        public event Action<Collider> Contact;
        public event Action<EntityId> Departed;
        public void Configure(Func<Collider, EntityId> resolveIdentity)
        {
            ClearContacts();
            _resolveIdentity = resolveIdentity;
            _state.Trigger = GetComponent<BoxCollider>();
        }
        public void RefreshContacts()
        {
            foreach (var id in new List<EntityId>(_state.Contacts.Keys))
            {
                if (!_state.Contacts.TryGetValue(id, out var colliders)) continue;
                colliders.RemoveWhere(other => other == null || !other.enabled || !other.gameObject.activeInHierarchy);
                if (colliders.Count > 0 && isActiveAndEnabled && _state.Trigger != null && _state.Trigger.enabled) continue;
                _state.Contacts.Remove(id);
                Departed?.Invoke(id);
            }
        }
        private void Observe(Collider other)
        {
            if (!isActiveAndEnabled || _state.Trigger == null || !_state.Trigger.enabled ||
                other == null || !other.enabled || !other.gameObject.activeInHierarchy) return;
            EntityId id = _resolveIdentity?.Invoke(other) ?? EntityId.None;
            if (id.IsValid)
            {
                if (!_state.Contacts.TryGetValue(id, out var colliders))
                { colliders = new HashSet<Collider>(); _state.Contacts.Add(id, colliders); }
                colliders.Add(other);
            }
            Contact?.Invoke(other);
        }
        private void ClearContacts()
        {
            var ids = new List<EntityId>(_state.Contacts.Keys);
            _state.Contacts.Clear();
            foreach (var id in ids) Departed?.Invoke(id);
        }
        private void OnTriggerEnter(Collider other) => Observe(other);
        private void OnTriggerStay(Collider other) => Observe(other);
        private void OnTriggerExit(Collider other)
        {
            foreach (var colliders in _state.Contacts.Values) colliders.Remove(other);
            RefreshContacts();
        }
        private void OnDisable() => ClearContacts();
    }
}