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
// DEPENDENCIES:
//   - Unity physics/navigation and HunterMotorDriverConfig passed by the owner.
// USAGE NOTES:
//   Scene-owned; owner calls Initialize/Teardown. No Update or global side effects.
// ============================================================================
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Stare
{
    public sealed class StareDriver : MonoBehaviour
    {
        private StareDriverState _state;
        public void Initialize()
        {
            Teardown();
            _state = new StareDriverState { Renderers = GetComponentsInChildren<Renderer>(true), Colliders = GetComponentsInChildren<Collider>(true) };
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
            foreach (Collider hit in Physics.OverlapCapsule(low, high, Mathf.Max(.001f, config.Radius - config.SkinWidth), config.CollisionMask, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) return false;
            return true;
        }
        public void Teardown() { SetPresent(true); _state = null; }
        private void OnDestroy() { Teardown(); }
    }
}
