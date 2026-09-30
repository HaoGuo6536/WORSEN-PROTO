// ============================================================================
// StareDriver.cs
// ============================================================================
// PURPOSE:
//   Proves nearby Stare placements on navigable, empty ground before teleporting.
//   Logical despawn hides the retained body while its Manager keeps the return
//   cadence alive; no scene entity or asset is created or destroyed here.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by HunterDriver · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Probe reachability, capsule clearance and camera-to-body occlusion.
//   - Hide/restore only the original placeholder renderers and colliders.
//   - Reuse complete pooled capsule queries, released symmetrically on teardown.
// DEPENDENCIES:
//   - Unity physics/navigation, parent placement port and HunterMotorDriverConfig.
// USAGE NOTES:
//   Scene-owned; owner calls Initialize/Teardown. No Update or global side effects.
// ============================================================================
using System.Buffers;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Stare
{
    public sealed class StareDriver : MonoBehaviour, IHunterPlacementDriver
    {
        private StareDriverState _state;
        public void Initialize()
        {
            Teardown();
            _state = new StareDriverState { Renderers = GetComponentsInChildren<Renderer>(true), Colliders = GetComponentsInChildren<Collider>(true) };
            _state.QueryOverlaps = ArrayPool<Collider>.Shared.Rent(64);
            _state.RenderEnabled = new bool[_state.Renderers.Length]; _state.ColliderEnabled = new bool[_state.Colliders.Length];
            for (int i = 0; i < _state.Renderers.Length; i++) _state.RenderEnabled[i] = _state.Renderers[i].enabled;
            for (int i = 0; i < _state.Colliders.Length; i++) _state.ColliderEnabled[i] = _state.Colliders[i].enabled;
            SetPresent(false);
        }
        public void SetPresent(bool present)
        {
            if (_state == null) return;
            for (int i = 0; i < _state.Renderers.Length; i++) if (_state.Renderers[i] != null) _state.Renderers[i].enabled = present && _state.RenderEnabled[i];
            for (int i = 0; i < _state.Colliders.Length; i++) if (_state.Colliders[i] != null) _state.Colliders[i].enabled = present && _state.ColliderEnabled[i];
        }
        public bool Probe(Vector3 candidate, Vector3 player, HunterPlayerView view, HunterMotorDriverConfig config, out Vector3 point)
        {
            point = default;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit end, config.GroundProbeDistance, config.NavigationAreaMask) ||
                !NavMesh.SamplePosition(player, out NavMeshHit start, config.PathSampleRadius, config.NavigationAreaMask)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, config.NavigationAreaMask, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            point = end.position;
            Vector3 low = point + Vector3.up * (config.Radius + config.SkinWidth);
            Vector3 high = point + Vector3.up * (config.Height - config.Radius);
            int count = CapsuleOverlap(low, high, Mathf.Max(.001f, config.Radius - config.SkinWidth), config.CollisionMask);
            for (int i = 0; i < count; i++)
            {
                Collider hit = _state.QueryOverlaps[i];
                if (!hit.transform.IsChildOf(transform)) return false;
            }
            return true;
        }
        private int CapsuleOverlap(Vector3 low, Vector3 high, float radius, int mask)
        {
            int count;
            while ((count = Physics.OverlapCapsuleNonAlloc(low, high, radius, _state.QueryOverlaps, mask, QueryTriggerInteraction.Ignore)) == _state.QueryOverlaps.Length)
            {
                int previous = _state.QueryOverlaps.Length;
                Collider[] larger = ArrayPool<Collider>.Shared.Rent(checked(previous * 2));
                ArrayPool<Collider>.Shared.Return(_state.QueryOverlaps, true); _state.QueryOverlaps = larger;
                Debug.LogWarning($"Stare physics query buffer saturated; grew from {previous} to {larger.Length} and retrying.", this);
            }
            return count;
        }
        public void Teardown()
        {
            if (_state == null) return;
            SetPresent(true);
            ArrayPool<Collider>.Shared.Return(_state.QueryOverlaps, true); _state.QueryOverlaps = null;
            _state = null;
        }
        private void OnDestroy() { Teardown(); }
    }
}
