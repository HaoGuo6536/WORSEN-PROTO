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
// DEPENDENCIES:
//   - Own DriverConfig, Core entity identity, Unity physics and navigation.
// USAGE NOTES:
//   Scene-scoped probe owned by ExpeditionSessionManager; no update loop or global changes.
//   Dynamic bodies and actors never count as cover. No navigation bake is performed.
// ============================================================================
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
namespace Worsen.Session.Expedition
{
    public sealed class ExpeditionSpawnDriver : MonoBehaviour
    {
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
            foreach (float height in new[] { config.Radius, config.Height * .5f, config.Height })
            {
                Vector3 offset = candidate + Vector3.up * height - eye;
                bool covered = false;
                foreach (var hit in Physics.RaycastAll(eye, offset.normalized, offset.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (hit.collider.attachedRigidbody == null && hit.collider.GetComponentInParent<IEntityHandle>() == null)
                    { covered = true; break; }
                if (!covered) return false;
            }
            return true;
        }
    }
}
