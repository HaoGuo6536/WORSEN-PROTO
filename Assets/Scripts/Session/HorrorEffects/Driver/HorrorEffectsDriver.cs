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
// DEPENDENCIES:
//   - Unity physics/animation only; no foreign Controllers, Managers or Registries.
// USAGE NOTES:
//   Persistent through HorrorEffectsManager; holds no scene objects across calls.
//   No independent tunables or global side effects; values arrive as parameters (§7d).
// ============================================================================
using UnityEngine;

namespace Worsen.Session.HorrorEffects
{
    public sealed class HorrorEffectsDriver : MonoBehaviour
    {
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
            foreach (var hit in Physics.RaycastAll(from, delta.normalized, nearest, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (ignored != null && hit.collider.transform.IsChildOf(ignored.transform)) continue;
                if (hit.distance > nearest) continue;
                nearest = hit.distance; point = hit.point; collider = hit.collider;
            }
            return collider != null;
        }
    }
}
