// ============================================================================
// ProceduralWorldObjectTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that committed object states have matching physical and visual effects.
//   These small EditMode fixtures do not build a floor or bake navigation.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check door collision/carving, terminal barrier visuals and threshold markers.
// DEPENDENCIES:
//   - Domain.Procedural, Core, Unity components and NUnit.
// USAGE NOTES:
//   Coordinator runs these tests in Unity; all temporary primitives are destroyed.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralWorldObjectTests
    {
        [TestCase(InteractableKind.Door, InteractableStateValue.Open, false, false)]
        [TestCase(InteractableKind.Door, InteractableStateValue.Inactive, true, true)]
        [TestCase(InteractableKind.Partition, InteractableStateValue.Broken, false, false)]
        [TestCase(InteractableKind.ThresholdMark, InteractableStateValue.Marked, false, true)]
        [TestCase(InteractableKind.ThresholdMark, InteractableStateValue.Inactive, false, false)]
        public void StateDrivesCollisionAndVisibility(InteractableKind kind, InteractableStateValue value, bool solid, bool visible)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var driver = item.AddComponent<ProceduralWorldObject>();
                driver.Configure(new InteractableState(1, kind, 1, Vector3.zero, InteractableStateValue.Inactive));
                driver.Apply(new InteractableState(1, kind, 1, Vector3.zero, value));
                Assert.That(item.GetComponent<Collider>().enabled, Is.EqualTo(solid));
                Assert.That(item.GetComponent<Renderer>().enabled, Is.EqualTo(visible));
                if (kind == InteractableKind.Door)
                { Assert.That(item.GetComponent<NavMeshObstacle>().enabled, Is.EqualTo(solid)); Assert.That(item.GetComponent<NavMeshObstacle>().carving, Is.True); }
            }
            finally { Object.DestroyImmediate(item); }
        }
    }
}
