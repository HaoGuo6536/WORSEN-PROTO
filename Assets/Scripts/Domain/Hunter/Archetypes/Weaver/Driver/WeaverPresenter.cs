// ============================================================================
// WeaverPresenter.cs
// ============================================================================
// PURPOSE:
//   Calculates ceiling offset and deterministic firing candidate geometry.
//   Keeping these calculations pure lets room height and probe ordering be
//   checked without a running physics world.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Hunter archetype presentation stack.
// KEY RESPONSIBILITIES:
//   - Clamp the body below a ceiling and generate an ordered candidate ring.
// DEPENDENCIES:
//   - UnityEngine value types only.
// USAGE NOTES:
//   Stateless. The Driver supplies measured body height and all configuration.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Weaver
{
    public sealed class WeaverPresenter
    {
        public float CeilingOffset(float floor, float ceiling, float bodyTop, float clearance, bool drop)
            => drop ? 0f : Mathf.Max(0f, ceiling - floor - bodyTop - clearance);
        public Vector3 Candidate(Vector3 origin, int index, int count, float distance)
        {
            float angle = index * Mathf.PI * 2f / count;
            return origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
        }
    }
}
