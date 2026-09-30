// ============================================================================
// TickingDriver.cs
// ============================================================================
// PURPOSE:
//   Owns the Ticking's single key placeholder and its navigation admission probes.
//   A candidate must have continuous walkable ground and physical clearance, so
//   a nearby point across a gap or closed door is never silently accepted.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Domain · Hunter Ticking facet.
// KEY RESPONSIBILITIES:
//   - Sample reachable rear pockets and keys, instantiate one trigger and relay raw contacts.
// DEPENDENCIES:
//   - Unity navigation/physics, own DriverConfig/DriverState and key sub-driver.
// USAGE NOTES:
//   Scene-owned, commanded only by TickingManager; no Update or global side effects.
//   Direct-ground admission is deliberately conservative in narrow/bent corridors.
//   The default navigation agent and Walkable area match the shared Hunter motor.
// ============================================================================
using System;
using UnityEngine;
using UnityEngine.AI;
namespace Worsen.Domain.Hunter.Archetypes.Ticking
{
    public sealed class TickingDriver : MonoBehaviour
    {
        private TickingDriverConfig _config;
        private readonly TickingDriverState _state = new TickingDriverState();
        public event Action<Collider, int> OnKeyContact;
        private void OnEnable() { TickingKeyContact.OnContact += HandleContact; }
        private void OnDisable() { TickingKeyContact.OnContact -= HandleContact; ClearKey(); }
        public void Initialize(TickingDriverConfig config)
        {
            ClearKey();
            if (config == null || config.KeyPrefab == null) throw new InvalidOperationException("Build and wire Ticking key assets before spawning.");
            _config = config;
            _state.Path = new NavMeshPath();
        }
        public bool TrySampleFollow(Vector3 hunter, Vector3 candidate, out Vector3 position)
        {
            position = default;
            if (_config == null || !NavMesh.SamplePosition(hunter, out NavMeshHit start, _config.SampleRadius, 1) ||
                !NavMesh.SamplePosition(candidate, out NavMeshHit end, _config.SampleRadius, 1) ||
                Mathf.Abs(end.position.y - candidate.y) > _config.MaximumElevation ||
                !NavMesh.CalculatePath(start.position, end.position, 1, _state.Path) ||
                _state.Path.status != NavMeshPathStatus.PathComplete) return false;
            position = end.position; return true;
        }
        public bool TrySampleKey(Vector3 player, Vector3 candidate, Func<Collider, bool> isPlayer, out Vector3 position)
        {
            position = default;
            const int walkable = 1;
            if (_config == null || !NavMesh.SamplePosition(player, out NavMeshHit start, _config.SampleRadius, walkable) ||
                !NavMesh.SamplePosition(candidate, out NavMeshHit end, _config.SampleRadius, walkable) ||
                Mathf.Abs(start.position.y - player.y) > _config.MaximumElevation ||
                Mathf.Abs(end.position.y - candidate.y) > _config.MaximumElevation ||
                NavMesh.Raycast(start.position, end.position, out _, walkable)) return false;
            Vector3 origin = start.position + Vector3.up * _config.ClearanceHeight;
            Vector3 destination = end.position + Vector3.up * _config.ClearanceHeight;
            Vector3 delta = destination - origin;
            foreach (RaycastHit hit in Physics.SphereCastAll(origin, _config.ClearanceRadius, delta.normalized,
                delta.magnitude, _config.ObstacleMask, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && (isPlayer == null || !isPlayer(hit.collider))) return false;
            foreach (Collider hit in Physics.OverlapSphere(destination, _config.ClearanceRadius, _config.ObstacleMask, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && (isPlayer == null || !isPlayer(hit))) return false;
            position = end.position; return true;
        }
        public void ShowKey(Vector3 position, int serial)
        {
            if (_state.Key != null && _state.Serial == serial) return;
            ClearKey();
            _state.Key = Instantiate(_config.KeyPrefab, position, Quaternion.identity);
            _state.Serial = serial;
            var trigger = _state.Key.GetComponent<SphereCollider>();
            if (trigger == null) trigger = _state.Key.AddComponent<SphereCollider>();
            trigger.isTrigger = true; trigger.radius = _config.ContactRadius; trigger.center = Vector3.up * _config.ContactRadius;
            var body = _state.Key.GetComponent<Rigidbody>();
            if (body == null) body = _state.Key.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            if (_state.Key.GetComponent<TickingKeyContact>() == null) _state.Key.AddComponent<TickingKeyContact>();
            _state.Key.SetActive(true);
        }
        private void HandleContact(GameObject key, Collider other)
        { if (key == _state.Key) OnKeyContact?.Invoke(other, _state.Serial); }
        public void ClearKey()
        {
            if (_state.Key == null) return;
            _state.Key.SetActive(false);
            if (Application.isPlaying) Destroy(_state.Key); else DestroyImmediate(_state.Key);
            _state.Key = null;
        }
        private void OnDestroy() { ClearKey(); }
    }
}
