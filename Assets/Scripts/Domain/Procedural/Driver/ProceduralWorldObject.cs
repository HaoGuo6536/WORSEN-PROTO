// ============================================================================
// ProceduralWorldObject.cs
// ============================================================================
// PURPOSE:
//   Applies committed world-object state to one generated physical object.
//   It never chooses interactions: the Level registry decides state and the owning
//   ProceduralDriver pushes the resulting immutable snapshot into this component.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by ProceduralDriver · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Retract open doors and toggle their collider and navigation carving obstacle.
//   - Hide broken barriers, topple knocked props and reveal marked thresholds.
// DEPENDENCIES:
//   - Core interactable values and Unity engine components only.
// USAGE NOTES:
//   Scene-owned, destroyed with the generated geometry root; no global effects.
//   Dimensions/materials are already supplied by the owner. Existing Environment
//   lights are deliberately not duplicated here and need their owner's fact binding.
//   Partition destruction removes collision immediately; static navigation remains
//   conservative until its owner rebuilds it, so this does not grant a hunter shortcut.
// ============================================================================
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralWorldObject : MonoBehaviour
    {
        private InteractableState _initial;
        private Vector3 _size;
        private Renderer _renderer;
        private Collider _collider;
        private NavMeshObstacle _obstacle;

        public void Configure(InteractableState initial)
        {
            _initial = initial; _size = transform.localScale;
            _renderer = GetComponent<Renderer>(); _collider = GetComponent<Collider>();
            if (initial.Kind == InteractableKind.Door)
            {
                _obstacle = gameObject.AddComponent<NavMeshObstacle>();
                _obstacle.shape = NavMeshObstacleShape.Box; _obstacle.size = Vector3.one;
                _obstacle.carving = true;
            }
            Apply(initial);
        }

        public void Apply(InteractableState state)
        {
            if (state.Id != _initial.Id || state.Kind != _initial.Kind) return;
            bool broken = state.Value == InteractableStateValue.Broken;
            bool closedDoor = state.Kind == InteractableKind.Door && state.Value == InteractableStateValue.Inactive;
            bool visible = !broken && (state.Kind == InteractableKind.Door ? closedDoor :
                state.Kind != InteractableKind.ThresholdMark || state.Value == InteractableStateValue.Marked);
            if (_renderer != null) _renderer.enabled = visible;
            if (_collider != null) _collider.enabled = !broken && state.Kind != InteractableKind.ThresholdMark &&
                (state.Kind != InteractableKind.Door || closedDoor);
            if (_obstacle != null) _obstacle.enabled = closedDoor;
            if (state.Kind == InteractableKind.KnockableProp)
            {
                bool knocked = state.Value == InteractableStateValue.Marked;
                transform.rotation = knocked ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
                transform.position = _initial.Position + Vector3.up * (knocked ? (_size.x - _size.y) * 0.5f : 0f);
            }
        }
    }
}
