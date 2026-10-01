// ============================================================================
// HUDGuidancePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies target retention, bounded turning and the single remaining-cake number.
//   Exercises published snapshots and explicit frame times without native UI or scenes.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · HUD.
// KEY RESPONSIBILITIES:
//   - Cover reordered channels, equal-rank identities and genuinely replaced targets.
//   - Verify smooth shortest-path turning, invalid time and reset/reacquisition.
//   - Verify white/golden remaining counts, tint, Hidden Count and zero.
//   - Prove number clearance for every arrow rotation.
// DEPENDENCIES:
//   NUnit, Core guidance, HUD presenters and Unity value types only.
// USAGE NOTES:
//   Pure Edit Mode tests; no engine clock, ScriptableObject or rendered document.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.HUD;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDGuidancePresenterTests
    {
        private static GuidanceTarget White(int id, Vector3 direction) => new GuidanceTarget(
            GuidanceKind.WhiteArrow, direction, Vector3.forward * id, id);
        private static GuidanceTarget Gold(int id, Vector3 direction) => new GuidanceTarget(
            GuidanceKind.GoldenSense, direction, Vector3.right * id, id);

        [Test]
        public void SingleArrowPrioritizesExitThenGoldenThenWhiteAndUsesMatchingTint()
        {
            var p = new HUDPresenter(); var g = new HUDGuidancePresenter(); var s = new HUDDriverState();
            var white = White(1, Vector3.forward); var gold = Gold(2, Vector3.right);
            var exit = new GuidanceTarget(GuidanceKind.ExitThroughWalls, Vector3.left, Vector3.left);
            p.SetCount(s, 1, 4); p.SetGoldenCount(s, 1, 2);
            p.SetGuidance(s, new[] { white, gold, exit });
            Assert.That(g.ActiveDegrees(s), Is.EqualTo(-90f).Within(.001f));
            Assert.That(g.ArrowTint(s, Color.yellow, Color.cyan), Is.EqualTo(Color.cyan)); Assert.That(g.CountText(s), Is.EqualTo("3"));
            p.SetGuidance(s, new[] { white, gold }); Assert.That(g.ActiveDegrees(s), Is.EqualTo(90f).Within(.001f));
            Assert.That(g.ArrowTint(s, Color.yellow, Color.cyan), Is.EqualTo(Color.yellow)); Assert.That(g.CountText(s), Is.EqualTo("1"));
            p.SetGuidance(s, new[] { white }); Assert.That(g.ActiveDegrees(s), Is.Zero);
            Assert.That(g.ArrowTint(s, Color.yellow, Color.cyan), Is.EqualTo(Color.white));
            p.SetGuidance(s, null); Assert.That(g.ArrowVisible(s), Is.False);
        }

        [Test]
        public void AlternatingSnapshotOrderRetainsEachChannelAndItsCurrentSample()
        {
            var p = new HUDPresenter(); var s = new HUDDriverState();
            var a = White(2, Vector3.forward); var b = White(3, Vector3.back); var gold = Gold(4, Vector3.right);
            p.SetGuidance(s, new[] { b, gold, a });
            for (int i = 0; i < 60; i++)
            {
                p.SetGuidance(s, i % 2 == 0 ? new[] { a, b, gold } : new[] { gold, b, a });
                Assert.That(s.WhiteTarget.Value.AnchorId, Is.EqualTo(2));
                Assert.That(s.GoldenTarget.Value.AnchorId, Is.EqualTo(4));
                Assert.That(s.WorldDirection, Is.EqualTo(Vector3.forward));
                Assert.That(s.DisplayArrowDegrees, Is.Zero);
            }
            // Even a lower identity arriving cannot steal an already published target.
            p.SetGuidance(s, new[] { White(1, Vector3.back), White(2, Vector3.left), gold });
            Assert.That(s.WhiteTarget.Value.AnchorId, Is.EqualTo(2));
            Assert.That(s.WorldDirection, Is.EqualTo(Vector3.left), "Identity retention must not freeze stale direction data.");
            Assert.That(s.DisplayArrowDegrees, Is.Zero, "Repeated snapshot callbacks must not snap the display.");
        }

        [Test]
        public void RemovedTargetsAreReplacedAndEmptySnapshotNeverInventsFallback()
        {
            var p = new HUDPresenter(); var s = new HUDDriverState(); var g = new HUDGuidancePresenter();
            p.SetGuidance(s, new[] { White(1, Vector3.forward) });
            p.SetGuidance(s, new[] { White(2, Vector3.back) });
            Assert.That(s.WhiteTarget.Value.AnchorId, Is.EqualTo(2));
            Assert.That(s.ArrowDegrees, Is.EqualTo(180f));
            Assert.That(s.DisplayArrowDegrees, Is.Zero);
            g.Tick(s, .1f, 90f);
            Assert.That(Mathf.Abs(s.DisplayArrowDegrees), Is.EqualTo(9f).Within(.001f));
            p.SetGuidance(s, null);
            Assert.That(s.WhiteTarget, Is.Null); Assert.That(s.DirectionVisible, Is.False);
            Assert.That(s.ArrowInitialized, Is.False); Assert.That(g.CountVisible(s), Is.False);
            p.SetGuidance(s, new[] { White(3, Vector3.left) });
            Assert.That(s.DisplayArrowDegrees, Is.EqualTo(-90f).Within(.001f));
        }

        [Test]
        public void IdentityDomainsAndAnonymousPositionsDoNotCollide()
        {
            var g = new HUDGuidancePresenter();
            var anchor = White(7, Vector3.forward);
            var entity = new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.back, Vector3.back, entityId: new EntityId(7));
            Assert.That(g.Select(new[] { entity, anchor }, GuidanceKind.WhiteArrow, entity).Value.EntityId, Is.EqualTo(new EntityId(7)));
            var anonymous = new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.left, Vector3.left);
            var other = new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.right, Vector3.right);
            Assert.That(g.Select(new[] { other, anonymous }, GuidanceKind.WhiteArrow, anonymous).Value.TargetPosition, Is.EqualTo(Vector3.left));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(0f)]
        public void InvalidGuidanceDoesNotBecomeAForwardArrow(float x)
        {
            var p = new HUDPresenter(); var s = new HUDDriverState();
            p.SetGuidance(s, new[] { White(1, Vector3.forward), Gold(2, Vector3.right) });
            p.SetGuidance(s, new[] { White(1, new Vector3(x, 0f, 0f)), Gold(2, new Vector3(x, 0f, 0f)) });
            Assert.That(s.DirectionVisible || s.GoldenSenseVisible, Is.False);
        }

        [Test]
        public void PublishedFallbackIsUsedWithoutRecomputingDirectionToTargetPosition()
        {
            var s = new HUDDriverState(); var p = new HUDPresenter();
            var target = new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.left, Vector3.forward * 20, 5, isFallback: true);
            p.SetGuidance(s, new[] { target });
            Assert.That(s.WorldDirection, Is.EqualTo(Vector3.left));
            Assert.That(s.WhiteTarget.Value.IsFallback, Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void SameNumberUsesRemainingValueAndTintOfItsArrow(bool golden)
        {
            var s = new HUDDriverState(); var p = new HUDPresenter(); var g = new HUDGuidancePresenter();
            var tint = new Color(1f, .75f, .15f, 1f);
            p.SetFloorCounters(s, new FloorDisplaySnapshot(3, 4, 2, ExitState.Locked, false, Vector3.zero,
                totalCakes: 8, totalGoldenCakes: 6));
            p.SetGuidance(s, golden ? new[] { White(2, Vector3.forward), Gold(4, Vector3.right) } : new[] { White(2, Vector3.forward) });
            Assert.That(g.CountText(s), Is.EqualTo(golden ? "4" : "5"));
            Assert.That(g.CountTint(s, tint), Is.EqualTo(golden ? tint : Color.white));
            Assert.That(g.CountVisible(s), Is.True);
            if (golden) Assert.That(p.TryShowPhantomCake(s, 1f), Is.False, "A normal phantom must not affect the golden counter.");
            p.SetFloorCounters(s, new FloorDisplaySnapshot(3, 4, 2, ExitState.Locked, false, Vector3.zero,
                totalCakes: 8, totalGoldenCakes: 6, hiddenCount: true));
            Assert.That(g.CountVisible(s), Is.False);
            Assert.That(s.DirectionVisible || s.GoldenSenseVisible, Is.True);
            Assert.That(p.TryShowPhantomCake(s, 1f), Is.False);
        }

        [Test]
        public void WhiteExitTargetYieldsTheSingleArrowAndNumberToGoldenSense()
        {
            var s = new HUDDriverState(); var p = new HUDPresenter(); var g = new HUDGuidancePresenter();
            p.SetCount(s, 8, 8); p.SetGoldenCount(s, 2, 6);
            p.SetGuidance(s, new[] { White(0, Vector3.forward), Gold(4, Vector3.right) });
            Assert.That(g.UsesGoldenCount(s), Is.True); Assert.That(g.CountText(s), Is.EqualTo("4"));
            Assert.That(s.DirectionVisible && s.GoldenSenseVisible, Is.True);
            p.SetGuidance(s, new[] { White(0, Vector3.forward) });
            Assert.That(g.CountText(s), Is.EqualTo("0")); Assert.That(g.CountVisible(s), Is.True);
        }

        [Test]
        public void CountsReachZeroClampOverCollectionAndNeverGuessUnknownTotals()
        {
            var s = new HUDDriverState(); var p = new HUDPresenter(); var g = new HUDGuidancePresenter();
            p.SetGuidance(s, new[] { Gold(4, Vector3.right) });
            p.SetGoldenCount(s, 4); Assert.That(g.CountVisible(s), Is.False);
            p.SetGoldenCount(s, 4, 4); Assert.That(g.CountText(s), Is.EqualTo("0")); Assert.That(g.CountVisible(s), Is.True);
            p.SetGoldenCount(s, int.MaxValue, 0); Assert.That(g.CountText(s), Is.EqualTo("0"));
            p.SetGuidance(s, new[] { White(2, Vector3.forward) });
            p.SetCount(s, 0, 0); Assert.That(g.CountText(s), Is.EqualTo("0")); Assert.That(g.CountVisible(s), Is.True);
            Assert.That(p.TryShowPhantomCake(s, 1f), Is.False);
            p.SetCount(s, int.MaxValue, 0); Assert.That(g.CountText(s), Is.EqualTo("0"));
            p.SetCount(s, -1, 4); Assert.That(g.CountVisible(s), Is.False);
        }

        [Test]
        public void TurningIsShortestPathBoundedAndFramePartitionIndependent()
        {
            var g = new HUDGuidancePresenter(); var p = new HUDPresenter();
            var a = new HUDDriverState(); var b = new HUDDriverState();
            foreach (var s in new[] { a, b })
            {
                p.SetDirection(s, Vector3.forward, true);
                p.SetDirection(s, Vector3.right, true);
            }
            g.Tick(a, .5f, 90f);
            for (int i = 0; i < 5; i++) g.Tick(b, .1f, 90f);
            Assert.That(a.DisplayArrowDegrees, Is.EqualTo(45f).Within(.001f));
            Assert.That(b.DisplayArrowDegrees, Is.EqualTo(a.DisplayArrowDegrees).Within(.001f));
            g.Tick(a, 2f, 90f); Assert.That(a.DisplayArrowDegrees, Is.EqualTo(90f).Within(.001f));
            a.DisplayArrowDegrees = 179f; a.ArrowDegrees = -179f;
            g.Tick(a, .01f, 100f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(179f, a.DisplayArrowDegrees)), Is.EqualTo(1f).Within(.001f));
            g.Tick(a, .01f, 100f); Assert.That(a.DisplayArrowDegrees, Is.EqualTo(-179f).Within(.001f));
        }

        [Test]
        public void AlternatingPublishedBearingsCannotSnapOrAccumulateCallbackTime()
        {
            var g = new HUDGuidancePresenter(); var p = new HUDPresenter(); var s = new HUDDriverState();
            p.SetGuidance(s, new[] { White(1, Vector3.forward), Gold(2, Vector3.forward) });
            p.SetChaseMode(s, true);
            for (int i = 0; i < 60; i++)
            {
                float before = s.DisplayArrowDegrees;
                var direction = i % 2 == 0 ? Vector3.right : Vector3.left;
                for (int callback = 0; callback < 3; callback++) p.SetGuidance(s, new[] { White(1, direction), Gold(2, direction) });
                Assert.That(s.DisplayArrowDegrees, Is.EqualTo(before));
                g.Tick(s, .01f, 100f);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(before, s.DisplayArrowDegrees)), Is.LessThanOrEqualTo(1.001f));
                Assert.That(s.DisplayGoldenArrowDegrees, Is.EqualTo(s.DisplayArrowDegrees));
                Assert.That(s.WhiteTarget.Value.AnchorId, Is.EqualTo(1));
            }
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)] [TestCase(0f)]
        public void InvalidTimeOrTurnRateCannotCorruptOrSnapBearing(float invalid)
        {
            var p = new HUDPresenter(); var s = new HUDDriverState(); var g = new HUDGuidancePresenter();
            p.SetDirection(s, Vector3.forward, true); p.SetDirection(s, Vector3.right, true);
            g.Tick(s, invalid, 100f); g.Tick(s, .1f, invalid);
            Assert.That(s.DisplayArrowDegrees, Is.Zero);
        }

        [Test]
        public void LatestCameraAimIsUsedWithoutMovementHeadingOverridingIt()
        {
            var p = new HUDPresenter(); var s = new HUDDriverState(); var g = new HUDGuidancePresenter();
            p.SetGuidance(s, new[] { White(1, Vector3.forward) });
            p.SetViewRotation(s, new Quaternion(0f, .70710678f, 0f, .70710678f));
            p.SetHeading(s, 180f);
            g.Tick(s, .1f, 100f);
            Assert.That(s.ArrowDegrees, Is.EqualTo(-90f).Within(.001f));
            Assert.That(s.DisplayArrowDegrees, Is.EqualTo(-10f).Within(.001f));
        }

        [Test]
        public void CounterClearsTheRotatedArrowEnvelopeAtEveryBearing()
        {
            const float size = 84f, gap = 4f;
            float bottom = new HUDGuidancePresenter().CounterBottom(size, gap);
            var vertices = new HUDGeometryPresenter().Arrow(new Rect(0, 0, size, size));
            for (int degrees = 0; degrees < 360; degrees++)
            {
                double angle = degrees * Math.PI / 180d;
                foreach (var vertex in vertices)
                {
                    double y = (vertex.x - size * .5f) * Math.Sin(angle) + (vertex.y - size * .5f) * Math.Cos(angle);
                    Assert.That(bottom, Is.GreaterThanOrEqualTo(size * .5f - y + gap));
                }
            }
        }
    }
}
