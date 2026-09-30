// ============================================================================
// HorrorEffectsDriver.cs
// ============================================================================
// PURPOSE:
//   Observes the physical world for consumable throws and hunter face aiming.
//   It reports positions and visibility without deciding item use or stun rules.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Session · HorrorEffects.
// KEY RESPONSIBILITIES:
//   - Sweep throw segments against non-trigger colliders, excluding the thrower.
//   - Sample an animated humanoid head, or an explicit provisional root offset.
//   - Reuse complete pooled ray queries stored in the passive DriverState below.
// DEPENDENCIES:
//   - Unity physics/animation only; no foreign Controllers, Managers or Registries.
// USAGE NOTES:
//   Persistent through HorrorEffectsManager; holds no scene objects across calls.
//   No independent tunables or global side effects; values arrive as parameters (§7d).
// ============================================================================
using System.Buffers;
using UnityEngine;

namespace Worsen.Session.HorrorEffects
{
    public sealed class HorrorEffectsDriver : MonoBehaviour
    {
        private readonly HorrorEffectsDriverState _state = new HorrorEffectsDriverState();
        public Vector3 Head(GameObject actor, float fallbackHeight)
        {
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null) return head.position;
            }
            return actor.transform.position + Vector3.up * fallbackHeight;
        }
        public bool Visible(Vector3 from, Vector3 head, GameObject observer, GameObject target)
        {
            if (!Sweep(from, head, observer, out _, out Collider hit)) return true;
            return hit.transform.IsChildOf(target.transform);
        }
        public bool Impact(Vector3 from, Vector3 to, GameObject thrower, out Vector3 point)
            => Sweep(from, to, thrower, out point, out _);
        private bool Sweep(Vector3 from, Vector3 to, GameObject ignored, out Vector3 point, out Collider collider)
        {
            point = to; collider = null;
            Vector3 delta = to - from;
            float nearest = delta.magnitude;
            if (nearest <= 0f) return false;
            int count = RayQuery(from, delta.normalized, nearest);
            int nearestId = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _state.QueryHits[i];
                if (ignored != null && hit.collider.transform.IsChildOf(ignored.transform)) continue;
                int id = hit.collider.GetInstanceID();
                if (hit.distance > nearest || (hit.distance == nearest && id >= nearestId)) continue;
                nearestId = id;
                nearest = hit.distance; point = hit.point; collider = hit.collider;
            }
            return collider != null;
        }
        private int RayQuery(Vector3 origin, Vector3 direction, float distance)
        {
            if (_state.QueryHits == null) _state.QueryHits = ArrayPool<RaycastHit>.Shared.Rent(64);
            int count;
            while ((count = Physics.RaycastNonAlloc(origin, direction, _state.QueryHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) == _state.QueryHits.Length)
            {
                int previous = _state.QueryHits.Length;
                RaycastHit[] larger = ArrayPool<RaycastHit>.Shared.Rent(checked(previous * 2));
                ArrayPool<RaycastHit>.Shared.Return(_state.QueryHits, true); _state.QueryHits = larger;
                Debug.LogWarning($"Horror effects physics query buffer saturated; grew from {previous} to {larger.Length} and retrying.", this);
            }
            return count;
        }
        private void OnDestroy()
        {
            if (_state.QueryHits != null) ArrayPool<RaycastHit>.Shared.Return(_state.QueryHits, true);
            _state.QueryHits = null;
        }
    }
    // DriverState (§7c) · Session · HorrorEffects. Passive ray-hit storage (no scene references).
    internal sealed class HorrorEffectsDriverState
    {
        public RaycastHit[] QueryHits;
    }
}
