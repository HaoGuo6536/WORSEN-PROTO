// ============================================================================
// PlayerLimbPresenter.cs
// ============================================================================
// PURPOSE:
//   Places hand roots in the lower first-person view without intersecting its
//   near plane. Projected bounds account for long, off-centre skinned arms as
//   well as capsule hands when the view pitches, rolls or snaps behind the player.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Mirror the configured hand offset and bound its depth by the supplied lens.
//   - Project renderer bounds relative to the root, not an assumed capsule centre.
// DEPENDENCIES:
//   - UnityEngine value math only; PlayerLimbStandIn supplies renderer/lens facts.
// USAGE NOTES:
//   Stateless pure math. Offsets come from PlayerMoverDriverConfig; the tiny
//   depth epsilon is numerical clearance, not a gameplay or art tuning value.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Player
{
    public sealed class PlayerLimbPresenter
    {
        public float RearExtent(Vector3 centerFromRoot, Vector3 worldExtents, Vector3 viewForward)
        {
            return Mathf.Abs(viewForward.x) * worldExtents.x
                + Mathf.Abs(viewForward.y) * worldExtents.y
                + Mathf.Abs(viewForward.z) * worldExtents.z
                - Vector3.Dot(centerFromRoot, viewForward);
        }

        public Vector3 HandOffset(Vector3 offset, bool left, float nearClip, float boundsRadius)
        {
            return new Vector3(left ? -Mathf.Abs(offset.x) : Mathf.Abs(offset.x), offset.y,
                Mathf.Max(offset.z, nearClip + boundsRadius + 0.0001f));
        }
    }
}
