// ============================================================================
// EnvironmentPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks that decorative lighting cannot block a portal or exceed its runtime budget.
//   Tests also cover finite repeated-room placement and minimum visibility under flame curses.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Verify density budgets exclude unavailable torches and preserve other light types.
//   - Verify portal clearance, elevation, selection, local dimming and threshold chalk.
//   - Verify default-off Wick/Darker Floors composition without exceeding the light cap.
//   - Bind exact Core light sockets without lighting other rooms, moons or destroyed torches.
//   - Keep footprint dressing out of notches and off internal walls; preserve rectangle placement.
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
using Worsen.Core;

namespace Worsen.Tests.CastleEnvironment
{
    public sealed class EnvironmentPresenterTests
    {
        [TestCase(1f, 3)]
        [TestCase(.5f, 1)]
        [TestCase(0f, 0)]
        [TestCase(float.NaN, 3)]
        public void TorchBudgetKeepsUnavailableSocketsOffAndDoesNotScaleMoons(float multiplier, int torches)
        {
            var state = new EnvironmentDriverState { TorchCountMultiplier = multiplier };
            for (int i = 0; i < 6; i++)
            {
                state.Positions.Add(Vector3.right * i);
                state.Available.Add(i != 0 && i != 1);
                state.Flames.Add(new EnvironmentFlameDriverState { Moon = i == 5, Lit = i != 0, Destruction = i == 1 ? 1f : 0f });
            }
            var visible = EnvironmentPresenter.BudgetedLights(state, 6, 20f);
            Assert.That(visible.Count(i => !state.Flames[i].Moon), Is.EqualTo(torches));
            Assert.That(visible, Does.Contain(5));
            Assert.That(visible, Has.No.Member(0));
            Assert.That(visible, Has.No.Member(1));
            Assert.That(EnvironmentPresenter.BudgetedLights(state, 1, 20f).Length, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void FootprintDressingAndLightSlotsAvoidNotchesAndInternalWalls()
        {
            var cells = new[] { new Bounds(new Vector3(0f, 3.5f, 0f), new Vector3(12f, 7f, 12f)),
                new Bounds(new Vector3(12f, 3.5f, 0f), new Vector3(12f, 7f, 12f)),
                new Bounds(new Vector3(0f, 3.5f, 12f), new Vector3(12f, 7f, 12f)) };
            var bounds = new Bounds(new Vector3(6f, 3.5f, 6f), new Vector3(24f, 7f, 24f));
            var room = new LevelRoom(1, bounds.center, bounds.size, cells);
            for (int id = 0; id < 8; id++)
            {
                var dressing = EnvironmentPresenter.BuildDressing(id, bounds, false, false, null, cells: cells);
                var lights = EnvironmentPresenter.BuildSlots(id, bounds, null, cells);
                Assert.That(dressing, Is.Not.Empty); Assert.That(lights, Is.Not.Empty);
                foreach (var slot in dressing.Concat(lights))
                {
                    Assert.That(room.ContainsXZ(slot.Position), Is.True);
                    var rotation = Quaternion.Euler(0f, slot.Yaw, 0f);
                    foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                        Assert.That(room.ContainsXZ(slot.Position + rotation * new Vector3(x * slot.Envelope.x * .5f, 0f,
                            z * slot.Envelope.z * .5f)), Is.True, "The entire dressing envelope must stay on occupied cells.");
                    if (slot.Kind != EnvironmentDecorationKind.Torch && slot.Kind != EnvironmentDecorationKind.Banner) continue;
                    var outside = slot.Position - Quaternion.Euler(0f, slot.Yaw, 0f) * Vector3.forward * .31f;
                    Assert.That(room.ContainsXZ(outside), Is.False, "A wall-backed item cannot attach to a cell seam.");
                }
            }
        }

        [Test]
        public void ExplicitSingleCellPreservesEveryRectangleSlot()
        {
            var bounds = new Bounds(new Vector3(0f, 3.5f, 0f), new Vector3(12f, 7f, 12f));
            var portals = new[] { Vector3.back * 6f };
            Assert.That(EnvironmentPresenter.BuildSlots(3, bounds, portals, new[] { bounds }),
                Is.EqualTo(EnvironmentPresenter.BuildSlots(3, bounds, portals)));
            Assert.That(EnvironmentPresenter.BuildDressing(3, bounds, false, true, portals, cells: new[] { bounds }),
                Is.EqualTo(EnvironmentPresenter.BuildDressing(3, bounds, false, true, portals)));
        }

        [Test]
        public void LightFactsMatchOnlyTheirRoomSocketAndRespectDestruction()
        {
            var state = new EnvironmentDriverState();
            state.Flames.Add(new EnvironmentFlameDriverState { RoomId = 1, SocketPosition = Vector3.one, Moon = true });
            state.Flames.Add(new EnvironmentFlameDriverState { RoomId = 2, SocketPosition = Vector3.one });
            state.Flames.Add(new EnvironmentFlameDriverState { RoomId = 1, SocketPosition = Vector3.one });
            state.Available.AddRange(new[] { true, true, true });
            var off = new InteractableState(42, InteractableKind.Light, 1, Vector3.one, InteractableStateValue.Inactive);
            var on = new InteractableState(42, InteractableKind.Light, 1, Vector3.one, InteractableStateValue.Lit);
            Assert.That(EnvironmentPresenter.ApplyLight(state, off), Is.True);
            Assert.That(state.Available, Is.EqualTo(new[] { true, true, false }));
            Assert.That(EnvironmentPresenter.ApplyLight(state, on), Is.True);
            Assert.That(state.Available[2], Is.True);
            state.Flames[2].Destruction = 1f;
            EnvironmentPresenter.ApplyLight(state, on);
            Assert.That(state.Available[2], Is.False);
            Assert.That(EnvironmentPresenter.ApplyLight(state,
                new InteractableState(43, InteractableKind.Light, 1, Vector3.zero, InteractableStateValue.Lit)), Is.False);
            Assert.That(EnvironmentPresenter.ApplyLight(state,
                new InteractableState(42, InteractableKind.Door, 1, Vector3.one, InteractableStateValue.Open)), Is.False);
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
        public void LampHooksRestoreLitStateAndScaleDarknessWithoutRevivingDestroyedRooms(bool wick, bool darker)
        {
            float expected = EnvironmentPresenter.FlameBrightness(2f, 3, wick ? 0f : 1f, 0f) * (darker ? .65f : 1f);
            Assert.That(EnvironmentPresenter.LampBrightness(2f, 3, 1f, 0f, wick, darker, .65f), Is.EqualTo(expected));
            Assert.That(EnvironmentPresenter.LampBrightness(2f, 3, 1f, 1f, wick, darker, .65f), Is.Zero);
            var positions = Enumerable.Range(0, 40).Select(i => Vector3.right * i).ToArray();
            var available = Enumerable.Repeat(true, 40).ToArray();
            Assert.That(EnvironmentPresenter.Nearest(Vector3.zero, positions, available, 12, 60f),
                Is.EqualTo(Enumerable.Range(0, 12).ToArray()));
        }

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
