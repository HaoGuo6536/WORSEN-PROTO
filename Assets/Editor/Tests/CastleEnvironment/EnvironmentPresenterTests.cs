// ============================================================================
// EnvironmentPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks that decorative lighting cannot block a portal or exceed its runtime budget.
//   Tests also cover finite repeated-room placement and minimum visibility under flame curses.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Verify portal clearance, elevation, selection, local dimming and threshold chalk.
// DEPENDENCIES:
//   - NUnit and EnvironmentPresenter; no scene objects required.
// USAGE NOTES:
//   Edit Mode tests with explicit time and positions.
// ============================================================================
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Presentation.Environment;

namespace Worsen.Tests.CastleEnvironment
{
    public sealed class EnvironmentPresenterTests
    {
        [Test]
        public void SlotsKeepPortalClearanceAndRespectElevatedFloor()
        {
            var bounds = new Bounds(new Vector3(0f, 7.5f, 0f), new Vector3(12f, 7f, 12f));
            var portals = new[] { new Vector3(-3.36f, 4f, -6f), new Vector3(6f, 4f, 3.36f) };
            EnvironmentSlot[] slots = EnvironmentPresenter.BuildSlots(2, bounds, portals);
            Assert.That(slots.Length, Is.InRange(1, 4));
            foreach (var slot in slots)
            {
                Assert.That(slot.Position.y, Is.EqualTo(6.75f).Within(0.001f));
                Assert.That(slot.Position.x, Is.InRange(bounds.min.x, bounds.max.x));
                Assert.That(slot.Position.y, Is.InRange(bounds.min.y, bounds.max.y));
                Assert.That(slot.Position.z, Is.InRange(bounds.min.z, bounds.max.z));
                Assert.That(EnvironmentPresenter.ClearsPortals(slot.Position, portals, 2.1f), Is.True);
            }
            Assert.That(slots.Count(s => s.Torch), Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void CrowdedPortalsProduceFewerDecorationsInsteadOfBlocking()
        {
            var bounds = new Bounds(new Vector3(0f, 3.5f, 0f), new Vector3(12f, 7f, 12f));
            var all = Enumerable.Range(0, 8).SelectMany(id => EnvironmentPresenter.BuildSlots(id, bounds, null)).Select(s => s.Position).ToArray();
            Assert.That(EnvironmentPresenter.BuildSlots(0, bounds, all), Is.Empty);
            Assert.That(EnvironmentPresenter.BuildSlots(0, new Bounds(Vector3.zero, Vector3.one), null), Is.Empty);
        }

        [Test]
        public void EffectBudgetChoosesNearestAvailableAndStableTies()
        {
            var points = new[] { new Vector3(2, 0, 0), new Vector3(-2, 0, 0), new Vector3(1, 0, 0), new Vector3(20, 0, 0) };
            var available = new[] { true, true, false, true };
            Assert.That(EnvironmentPresenter.Nearest(Vector3.zero, points, available, 2, 10f), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(EnvironmentPresenter.Nearest(Vector3.zero, points, available, 0, 10f), Is.Empty);
            Assert.That(EnvironmentPresenter.Nearest(Vector3.zero, points, available, 20, 1f), Is.Empty);
        }

        [Test]
        public void FlameCursePreservesMinimumUntilRoomActuallyConsumed()
        {
            for (int i = 0; i < 300; i++)
            {
                float value = EnvironmentPresenter.FlameBrightness(i * 0.037f, 9, 1f, 0f);
                Assert.That(value, Is.InRange(0.239f, 0.301f));
                Assert.That(EnvironmentPresenter.FlameBrightness(i * 0.037f, 9, 1f, 1f), Is.Zero);
            }
            Assert.That(EnvironmentPresenter.FlameBrightness(0.5f, 2, 0f, 0f), Is.Not.EqualTo(EnvironmentPresenter.FlameBrightness(0.5f, 7, 0f, 0f)));
        }

        [Test]
        public void MedievalDressingHasSixPropBudgetAndCoherentRoomRoles()
        {
            var bounds = new Bounds(new Vector3(0f, 3.5f, 0f), new Vector3(12f, 7f, 12f));
            var doors = new[] { new Vector3(0f, 0f, -6f), new Vector3(6f, 0f, 0f) };
            for (int id = 1; id < 24; id++)
            {
                var ordinary = EnvironmentPresenter.BuildDressing(id, bounds, false, false, doors);
                Assert.That(ordinary.Length, Is.LessThanOrEqualTo(6));
                Assert.That(ordinary.Count(s => s.Kind == EnvironmentDecorationKind.Arch), Is.EqualTo(1));
                Assert.That(ordinary.Count(s => s.Kind == EnvironmentDecorationKind.Column), Is.GreaterThanOrEqualTo(1));
                Assert.That(ordinary.Count(s => s.Kind == EnvironmentDecorationKind.FloorProp), Is.EqualTo(1));
                var refuge = EnvironmentPresenter.BuildDressing(id, bounds, false, true, doors);
                Assert.That(refuge.Count(s => s.Kind == EnvironmentDecorationKind.MerchantDisplay), Is.EqualTo(1));
                var cloister = EnvironmentPresenter.BuildDressing(id, bounds, true, false, doors);
                Assert.That(cloister.Count(s => s.Kind == EnvironmentDecorationKind.Column), Is.EqualTo(2));
                Assert.That(cloister.Length, Is.LessThanOrEqualTo(6));
            }
        }

        [Test]
        public void FloorPropsStayInCornerAlcovesAndRespectReservedTraversalSpace()
        {
            var bounds = new Bounds(new Vector3(0f, 3.5f, 0f), new Vector3(12f, 7f, 12f));
            var central = new Bounds(new Vector3(0f, 3f, 0f), new Vector3(9.7f, 6f, 9.7f));
            var doors = new[] { new Vector3(0f, 0f, -6f), new Vector3(6f, 0f, 0f) };
            var slots = EnvironmentPresenter.BuildDressing(4, bounds, false, false, doors, new[] { central });
            foreach (var slot in slots.Where(s => s.Kind == EnvironmentDecorationKind.FloorProp))
            {
                Assert.That(Mathf.Abs(slot.Position.x) - slot.Envelope.x * .5f, Is.GreaterThan(4.85f));
                Assert.That(Mathf.Abs(slot.Position.z) - slot.Envelope.z * .5f, Is.GreaterThan(4.85f));
                Assert.That(EnvironmentPresenter.ClearsFloorRoutes(slot.Position, slot.Envelope, bounds, doors, new[] { central }), Is.True);
            }
            var forbiddenAll = EnvironmentPresenter.BuildDressing(4, bounds, false, false, doors, new[] { bounds });
            Assert.That(forbiddenAll.Any(s => s.Kind == EnvironmentDecorationKind.FloorProp || s.Kind == EnvironmentDecorationKind.Column), Is.False);
        }

        [Test]
        public void DecorativeArchesNeverLowerTheStandingDoorwayClearance()
        {
            var bounds = new Bounds(new Vector3(0f, 7.5f, 0f), new Vector3(12f, 7f, 12f));
            var slots = EnvironmentPresenter.BuildDressing(5, bounds, false, false, new[] { new Vector3(-6f, 4f, 0f) });
            var arch = slots.Single(s => s.Kind == EnvironmentDecorationKind.Arch);
            Assert.That(arch.Position.y - arch.Envelope.y * .5f - bounds.min.y, Is.GreaterThanOrEqualTo(2.9f));
            Assert.That(arch.Yaw, Is.EqualTo(90f));
        }

        [Test]
        public void LocalFlameDimOnlyAffectsNearbyTorchesAndFeathersItsEdge()
        {
            Assert.That(EnvironmentPresenter.LocalFlameMultiplier(Vector3.zero, Vector3.zero, 5f, .4f), Is.EqualTo(.4f).Within(.001f));
            Assert.That(EnvironmentPresenter.LocalFlameMultiplier(Vector3.right * 6f, Vector3.zero, 5f, .4f), Is.EqualTo(1f));
            Assert.That(EnvironmentPresenter.LocalFlameMultiplier(Vector3.up * 6f, Vector3.zero, 5f, .4f), Is.EqualTo(1f));
            Assert.That(EnvironmentPresenter.LocalFlameMultiplier(Vector3.right * 4.5f, Vector3.zero, 5f, .4f), Is.EqualTo(.7f).Within(.001f));
            Assert.That(EnvironmentPresenter.LocalFlameMultiplier(Vector3.zero, Vector3.zero, 5f, -1f), Is.EqualTo(.3f).Within(.001f));
        }

        [Test]
        public void FlameRestorationAndGlobalCompatibilityDoNotStackBelowReadableMinimum()
        {
            Assert.That(EnvironmentPresenter.LocalFlameMultiplier(Vector3.zero, Vector3.zero, 5f, 1f), Is.EqualTo(1f));
            Assert.That(EnvironmentPresenter.LocalFlameMultiplier(Vector3.zero, Vector3.zero, 0f, .3f), Is.EqualTo(1f));
            Assert.That(EnvironmentPresenter.LocalFlameMultiplier(Vector3.zero, Vector3.zero, float.NaN, .3f), Is.EqualTo(1f));
            float combined = EnvironmentPresenter.CombinedFlameGutter(1f, Vector3.zero, Vector3.zero, 5f, .3f);
            Assert.That(combined, Is.EqualTo(1f).Within(.001f));
            Assert.That(EnvironmentPresenter.FlameBrightness(0f, 1, combined, 0f), Is.GreaterThanOrEqualTo(.239f));
            Assert.That(EnvironmentPresenter.CombinedFlameGutter(.2f, Vector3.right * 10f, Vector3.zero, 5f, .3f), Is.EqualTo(.2f));
        }

        [Test]
        public void ChalkCrossIsSmallAndRaisedOffThePushedThreshold()
        {
            Vector3 position = new Vector3(8f, 4f, -6f);
            Vector3[][] strokes = EnvironmentPresenter.ChalkCross(position);
            Assert.That(strokes.Length, Is.EqualTo(2));
            foreach (Vector3[] stroke in strokes)
            foreach (Vector3 point in stroke)
            {
                Assert.That(point.y, Is.EqualTo(4.035f).Within(.001f));
                Assert.That(Mathf.Abs(point.x - position.x), Is.LessThanOrEqualTo(.2f));
                Assert.That(Mathf.Abs(point.z - position.z), Is.LessThanOrEqualTo(.2f));
            }
        }

        [Test]
        public void ChalkOwnershipIncludesBothPortalRoomsWithoutConfusingFloors()
        {
            var rooms = new Dictionary<int, Bounds>
            {
                { 2, new Bounds(new Vector3(12f, 3.5f, 0f), new Vector3(12f, 7f, 12f)) },
                { 1, new Bounds(new Vector3(0f, 3.5f, 0f), new Vector3(12f, 7f, 12f)) },
                { 3, new Bounds(new Vector3(0f, 7.5f, 0f), new Vector3(12f, 7f, 12f)) }
            };
            Assert.That(EnvironmentPresenter.ChalkRooms(new Vector3(6f, 0f, 1f), rooms), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(EnvironmentPresenter.ChalkRooms(new Vector3(6f, 4f, 1f), rooms), Is.EqualTo(new[] { 3 }));
            Assert.That(EnvironmentPresenter.ChalkRooms(new Vector3(60f, 0f, 1f), rooms), Is.Empty);
        }

        [Test]
        public void DecorationFitNeverUpscalesOrExceedsEnvelope()
        {
            Assert.That(EnvironmentPresenter.FitScale(new Vector3(2, 4, 1), new Vector3(1, 1, 1)), Is.EqualTo(0.25f));
            Assert.That(EnvironmentPresenter.FitScale(Vector3.one * 0.1f, Vector3.one), Is.EqualTo(1f));
            Assert.That(EnvironmentPresenter.FitScale(new Vector3(0.3f, 1.3f, 1.2f), new Vector3(1.3f, 1.4f, 0.35f), 90f), Is.EqualTo(1f));
        }
    }
}
