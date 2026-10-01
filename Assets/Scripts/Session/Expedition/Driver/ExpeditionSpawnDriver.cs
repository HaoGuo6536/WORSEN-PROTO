// ============================================================================
// ExpeditionSpawnDriver.cs
// ============================================================================
// PURPOSE:
//   Probes the current physical floor before Session adds a Purgatory hunter.
//   Original generator admission is not enough once the player and collapse have moved.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Use the admitted archetype's navigation mask, defaulting to ordinary walkable areas.
//   - Require a free body, complete live navigation and cover from the current player.
//   - Reuse complete pooled cover queries held in the passive DriverState below.
// DEPENDENCIES:
//   - Own DriverConfig, Core entity identity, Unity physics and navigation.
// USAGE NOTES:
//   Scene-scoped probe owned by ExpeditionSessionManager; no update loop or global changes.
//   Dynamic bodies and actors never count as cover. No navigation bake is performed.
// ============================================================================
using System.Buffers;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
namespace Worsen.Session.Expedition
{
    public sealed class ExpeditionSpawnDriver : MonoBehaviour
    {
        private readonly ExpeditionSpawnDriverState _state = new ExpeditionSpawnDriverState();
        public bool Validate(Vector3 candidate, Vector3 player, ExpeditionSpawnDriverConfig config, int navigationAreaMask = 1)
        {
            if (config == null || !float.IsFinite(config.Radius) || config.Radius <= 0f ||
                !float.IsFinite(config.Height) || config.Height < config.Radius * 2f ||
                !float.IsFinite(config.Skin) || config.Skin <= 0f ||
                !float.IsFinite(config.NavigationTolerance) || config.NavigationTolerance <= 0f ||
                !float.IsFinite(config.EyeHeight) || config.EyeHeight <= 0f) return false;
            if (!NavMesh.SamplePosition(candidate, out var start, config.NavigationTolerance, navigationAreaMask) ||
                !NavMesh.SamplePosition(player, out var end, config.NavigationTolerance, navigationAreaMask)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, navigationAreaMask, path) ||
                path.status != NavMeshPathStatus.PathComplete) return false;
            if (Physics.CheckCapsule(candidate + Vector3.up * (config.Radius + config.Skin),
                candidate + Vector3.up * (config.Height - config.Radius + config.Skin), config.Radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            var eye = player + Vector3.up * config.EyeHeight;
            for (int sample = 0; sample < 3; sample++)
            {
                float height = sample == 0 ? config.Radius : sample == 1 ? config.Height * .5f : config.Height;
                Vector3 offset = candidate + Vector3.up * height - eye;
                bool covered = false;
                int count = RayQuery(eye, offset.normalized, offset.magnitude);
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = _state.QueryHits[i];
                    if (hit.collider.attachedRigidbody == null && hit.collider.GetComponentInParent<IEntityHandle>() == null)
                    { covered = true; break; }
                }
                if (!covered) return false;
            }
            return true;
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
                Debug.LogWarning($"Expedition spawn physics query buffer saturated; grew from {previous} to {larger.Length} and retrying.", this);
            }
            return count;
        }
        private void OnDestroy()
        {
            if (_state.QueryHits != null) ArrayPool<RaycastHit>.Shared.Return(_state.QueryHits, true);
            _state.QueryHits = null;
        }
    }
    // DriverState (§7c) · Session · Expedition. Passive query storage owned by the Driver.
    internal sealed class ExpeditionSpawnDriverState
    {
        public RaycastHit[] QueryHits;
    }
}
