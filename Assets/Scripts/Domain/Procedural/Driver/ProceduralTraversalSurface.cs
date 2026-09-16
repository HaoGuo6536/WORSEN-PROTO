// ============================================================================
// ProceduralTraversalSurface.cs
// ============================================================================
// PURPOSE:
//   Exposes neutral traversal metadata on one generated collision surface.
//   Player probes can recognize a purposeful vault, slide aperture or rebound
//   corner without depending on the procedural system or a Level component.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by ProceduralDriver · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Carry explicit surface identity and bidirectional vault landing positions.
// DEPENDENCIES:
//   - Core ITraversalSurface and ITraversalEndpointPair only.
// USAGE NOTES:
//   Scene-owned per generated block. Configured while its parent is inactive;
//   destroyed with that geometry. No subscriptions or global engine effects.
// ============================================================================
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralTraversalSurface : MonoBehaviour, ITraversalSurface, ITraversalEndpointPair
    {
        public int SurfaceId { get; private set; }
        public TraversalSurfaceKind Kind { get; private set; }
        public Vector3 Target => EndpointB;
        public bool HasEndpointPair => Kind == TraversalSurfaceKind.Vault;
        public Vector3 EndpointA { get; private set; }
        public Vector3 EndpointB { get; private set; }
        public void Configure(ProceduralBlock block)
        { SurfaceId = block.SurfaceId; Kind = block.TraversalKind; EndpointA = block.EndpointA; EndpointB = block.EndpointB; }
    }
}
