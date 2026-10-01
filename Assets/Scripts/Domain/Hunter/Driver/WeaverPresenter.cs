// ============================================================================
// WeaverPresenter.cs
// ============================================================================
// PURPOSE:
//   Calculates ceiling offset and deterministic firing candidate geometry.
//   Keeping these calculations pure lets room height and probe ordering be
//   checked without a running physics world.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Hunter shared swept-shot presentation stack.
// KEY RESPONSIBILITIES:
//   - Clamp/invert the body below a ceiling and generate an ordered candidate ring.
// DEPENDENCIES:
//   - UnityEngine value types only.
// USAGE NOTES:
//   Stateless. The Driver supplies measured body height and all configuration.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    public sealed class WeaverPresenter
    {
        public float CeilingOffset(float floor, float ceiling, float bodyTop, float clearance, bool drop)
            => drop ? 0f : Mathf.Max(0f, ceiling - floor - bodyTop - clearance);
        public Quaternion CeilingRotation(bool ceiling) => ceiling ? new Quaternion(0f, 0f, 1f, 0f) : Quaternion.identity;
        public Vector3 CeilingPosition(Vector3 original, Vector3 pivot, Vector3 offset, Quaternion rotation)
            => pivot + offset + rotation * (original - pivot);
        public Vector3 Candidate(Vector3 origin, int index, int count, float distance)
        {
            float angle = index * Mathf.PI * 2f / count;
            return origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
        }
    }
}
