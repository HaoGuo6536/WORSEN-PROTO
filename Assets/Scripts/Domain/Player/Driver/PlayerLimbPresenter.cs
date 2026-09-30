// ============================================================================
// PlayerLimbPresenter.cs
// ============================================================================
// PURPOSE:
//   Places placeholder hands in the lower first-person view without intersecting
//   its near plane. A conservative renderer radius keeps the whole hand clear
//   even when the view pitches, rolls or snaps behind the player.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Mirror the configured hand offset and bound its depth by the supplied lens.
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
        public Vector3 HandOffset(Vector3 offset, bool left, float nearClip, float boundsRadius)
        {
            return new Vector3(left ? -Mathf.Abs(offset.x) : Mathf.Abs(offset.x), offset.y,
                Mathf.Max(offset.z, nearClip + boundsRadius + 0.0001f));
        }
    }
}
