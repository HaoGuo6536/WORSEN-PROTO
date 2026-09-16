// ============================================================================
// ProceduralFracturePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks destruction texture visibility and physical displacement limits.
//   The playable cracking interval must not silently become an unescapable fall
//   and repeated samples must not accumulate fragment drift across frames.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Verify bounded phase progression, reset and deterministic crack masks.
// DEPENDENCIES:
//   - Core collapse values, Domain.Procedural and NUnit only.
// USAGE NOTES:
//   Pure calculations; native collider synchronization remains an integration gate.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;
namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralFracturePresenterTests
    {
        [Test]
        public void CrackingLeavesCollisionIntactAndOpeningResetsDisplacement()
        {
            var presenter = new ProceduralFracturePresenter();
            var block = new ProceduralBlock(1, ProceduralSurfaceKind.Floor, new Vector3(3f, -0.15f, 0f), new Vector3(4f, 0.3f, 4f));
            var bounds = new Bounds(new Vector3(0f, 3.5f, 0f), new Vector3(12f, 7f, 12f));
            Assert.That(presenter.Offset(block, bounds, new RoomDestructionSample(1, RoomPhase.Telegraph, 1f), 1), Is.EqualTo(Vector3.zero));
            Assert.That(presenter.Offset(block, bounds, new RoomDestructionSample(1, RoomPhase.Open, 1f), 1), Is.EqualTo(Vector3.zero));
            var consumed = presenter.Offset(block, bounds, new RoomDestructionSample(1, RoomPhase.Closed, 1f), 1);
            Assert.That(new Vector2(consumed.x, consumed.z).magnitude, Is.LessThanOrEqualTo(0.08001f));
            Assert.That(consumed.y, Is.GreaterThanOrEqualTo(-0.12001f));
        }
        [Test]
        public void CrackMaskHasTransparentBackgroundAndRepeatableFissures()
        {
            var presenter = new ProceduralFracturePresenter();
            var pixels = presenter.CrackPixels(128);
            Assert.That(pixels, Is.EqualTo(presenter.CrackPixels(128)));
            Assert.That(pixels.Count(p => p.a == 0), Is.GreaterThan(pixels.Length * 0.8f));
            Assert.That(pixels.Count(p => p.a > 0), Is.GreaterThan(100));
            Assert.That(presenter.CrackOpacity(new RoomDestructionSample(1, RoomPhase.Open, 1f)), Is.Zero);
            Assert.That(presenter.CrackOpacity(new RoomDestructionSample(1, RoomPhase.Telegraph, 1f)), Is.GreaterThan(0.9f));
        }
    }
}
