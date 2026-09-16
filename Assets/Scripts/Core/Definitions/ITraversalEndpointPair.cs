// ============================================================================
// ITraversalEndpointPair.cs
// ============================================================================
// PURPOSE:
//   Describes optional authored landings on both sides of a traversal surface.
//   This lets physical probes choose the opposite landing without depending on
//   Level components or changing the existing single-target surface contract.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared traversal contracts.
// KEY RESPONSIBILITIES:
//   - Expose an explicit opt-in and two world-space landing positions.
// DEPENDENCIES:
//   - UnityEngine value types only; no project-layer or engine-object references.
// USAGE NOTES:
//   Implement alongside ITraversalSurface. HasEndpointPair=false retains that
//   surface's legacy Target. Enabled pairs must be finite and horizontally
//   distinct; consumers reject invalid pairs rather than guessing a landing.
//   Endpoints are metres, authored independently; neither is inferred by mirroring.
// ============================================================================
using UnityEngine;

namespace Worsen.Core
{
    public interface ITraversalEndpointPair
    {
        bool HasEndpointPair { get; }
        Vector3 EndpointA { get; }
        Vector3 EndpointB { get; }
    }
}
