// ============================================================================
// PlayerLimbStandIn.cs
// ============================================================================
// PURPOSE:
//   Keeps the legacy first-person limb references hidden during all movement.
//   Hands and feet are temporarily removed from the horror prototype's view.
//   The serialized fields and command remain compatible with existing Player prefabs.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by PlayerDriver · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Disable all four legacy limb objects for every movement command.
//   - Keep game rules, passive state, and engine interactions in separate roles.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Scene-owned. Commanded only by PlayerDriver; generated renderers have no collision shapes.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Player
{
    public sealed class PlayerLimbStandIn : MonoBehaviour
    {
        [SerializeField] private GameObject _leftHand;
        [SerializeField] private GameObject _rightHand;
        [SerializeField] private GameObject _leftFoot;
        [SerializeField] private GameObject _rightFoot;
        public void Apply(MovementState movement, float eyeHeight, Vector3 handOffset, Vector3 footOffset)
        {
            if (_leftHand != null) _leftHand.SetActive(false);
            if (_rightHand != null) _rightHand.SetActive(false);
            if (_leftFoot != null) _leftFoot.SetActive(false);
            if (_rightFoot != null) _rightFoot.SetActive(false);
        }
    }
}