// ============================================================================
// ProceduralNavigationPresenter.cs
// ============================================================================
// PURPOSE:
//   Separates walkable hunter surfaces from physical player-only shortcuts. Future
//   partition-ignoring hunters receive explicit horizontal link plans on a distinct
//   area, never an accidental hole in the ordinary collision/navigation shell.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Classify box sources and reject masks that grant ordinary hunters shortcuts.
//   - Describe optional door, thin partition and vault-window links without engine calls.
// DEPENDENCIES:
//   - Own layout/config and Core interactable/traversal values only.
// USAGE NOTES:
//   Area 1 is Unity's non-walkable area. Player-only collision stays in the bake as
//   an obstacle. No vertical off-mesh links are emitted; hunters must walk ramps.
//   Link installation is fail-closed until the coordinator enables it after masks
//   are routed to Hunter. The future Weaver must also ignore partition collision.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralNavigationPresenter
    {
        public int Area(ProceduralBlock block)
            => block.Kind == ProceduralSurfaceKind.Floor && block.Role != ProceduralBlockRole.PlayerOnly ? 0 : 1;

        public void Validate(ProceduralDriverConfig config)
        {
            if (config.PartitionIgnoringArea < 3 || config.PartitionIgnoringArea > 31 ||
                (config.HunterAreaMask & 1) == 0 || (config.HunterAreaMask & (2 | (1 << config.PartitionIgnoringArea))) != 0 ||
                config.PartitionIgnoringAreaMask != (config.HunterAreaMask | (1 << config.PartitionIgnoringArea)))
                throw new ArgumentException("Navigation masks must keep partition-ignoring traversal out of ordinary hunter routes.");
        }

        public IReadOnlyList<ProceduralNavigationLink> Links(ProceduralLayout layout,
            IReadOnlyList<ProceduralBlock> blocks, ProceduralDriverConfig config)
        {
            Validate(config);
            var links = new List<ProceduralNavigationLink>();
            foreach (var block in blocks.Where(b => b.TraversalKind == TraversalSurfaceKind.Vault))
            {
                // A vault-down balcony must never become a Weaver/hunter level-change link.
                if (Mathf.Abs(block.EndpointA.y - block.EndpointB.y) > 0.001f) continue;
                links.Add(new ProceduralNavigationLink(block.EndpointA, block.EndpointB, config.PartitionIgnoringArea));
            }
            foreach (var door in layout.Doors.Where(d => !d.IsOptional && layout.Interactables.Any(p =>
                p.State.Kind == InteractableKind.Door && p.State.EdgeId == 1001 + layout.Doors.TakeWhile(d2 => !d2.Equals(d)).Count())))
            {
                var offset = (door.AlongX ? Vector3.forward : Vector3.right) * config.LandingOffset;
                links.Add(new ProceduralNavigationLink(door.Center - offset, door.Center + offset, config.PartitionIgnoringArea));
            }
            return links.AsReadOnly();
        }
    }
}
