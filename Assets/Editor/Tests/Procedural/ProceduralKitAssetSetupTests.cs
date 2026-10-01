// ============================================================================
// ProceduralKitAssetSetupTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the room-preview block builder without importing or saving assets.
//   Compound visuals must appear once while every collision command stays present,
//   including ordinary solids whose imported meshes have no colliders.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check art-present and missing-art compound fallback ownership.
//   - Check solid and render-only block collision independence.
// DEPENDENCIES:
//   - NUnit, Domain.Procedural, Editor.Procedural and temporary Unity objects.
// USAGE NOTES:
//   Native Edit Mode only; no prefab or scene assets are saved by these tests.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Procedural;
using Worsen.Editor.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralKitAssetSetupTests
    {
        [TestCase(false)] [TestCase(true)]
        public void CompoundArtIsInstantiatedOnceAndPartsKeepIndependentCollision(bool art)
        {
            var root = new GameObject("Preview test"); var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var visual = new ProceduralBlock(1, ProceduralSurfaceKind.Wall, Vector3.up, new Vector3(4f, 3f, .4f),
                    role: ProceduralBlockRole.KitVisual, pieceId: "arch", piecePosition: new Vector3(10f, 0f, 0f));
                ProceduralKitAssetSetup.CreateBlock(visual, art ? source : null, root.transform);
                for (int i = 0; i < 3; i++)
                    ProceduralKitAssetSetup.CreateBlock(new ProceduralBlock(1, ProceduralSurfaceKind.Wall,
                        new Vector3(i * 2f, 1f, 0f), new Vector3(.4f, 2f, .4f), role: ProceduralBlockRole.KitCollision,
                        pieceId: "arch"), art ? source : null, root.transform);
                Assert.That(root.GetComponentsInChildren<Transform>().Count(t => t.name == "Kit arch"), Is.EqualTo(art ? 1 : 0));
                Assert.That(root.GetComponentsInChildren<Collider>().Length, Is.EqualTo(3));
                Assert.That(root.GetComponentsInChildren<Renderer>().Count(r => r.enabled), Is.EqualTo(art ? 1 : 3));
                foreach (var collider in root.GetComponentsInChildren<Collider>())
                    Assert.That(collider.transform.localScale, Is.EqualTo(new Vector3(.4f, 2f, .4f)));
                if (art) Assert.That(root.transform.Find("Kit arch").position, Is.EqualTo(visual.PiecePosition));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(source); }
        }

        [TestCase(ProceduralBlockRole.Solid, true)] [TestCase(ProceduralBlockRole.VisualOnly, false)]
        [TestCase(ProceduralBlockRole.CollisionOnly, true)]
        public void ArtNeverReplacesOrAddsCollision(ProceduralBlockRole role, bool collision)
        {
            var root = new GameObject("Preview solid test"); var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var block = new ProceduralBlock(1, ProceduralSurfaceKind.Wall, Vector3.up, Vector3.one * 2f, role: role, pieceId: "fixture");
                ProceduralKitAssetSetup.CreateBlock(block, source, root.transform);
                Assert.That(root.GetComponentsInChildren<Collider>().Length, Is.EqualTo(collision ? 1 : 0));
                Assert.That(root.GetComponentsInChildren<Renderer>().Count(r => r.enabled), Is.EqualTo(block.HasRenderer ? 1 : 0));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(source); }
        }
    }
}
