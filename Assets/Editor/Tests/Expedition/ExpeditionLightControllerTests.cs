// ============================================================================
// ExpeditionLightControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Mannequin light override expiry, duplicate budgets and Wick precedence.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Preserve per-light baselines through temporary, permanent and Wick effects.
//   - Keep the lamp budget absolute across duplicate type instances.
// DEPENDENCIES:
//   - Expedition pure Controller/state, Core, NUnit and Unity value types.
// USAGE NOTES:
//   Pure managed tests; the coordinator checks actual light drivers in Unity.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Expedition;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Expedition
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ExpeditionLightControllerTests
    {
        private static InteractableState Lamp(int id, bool lit = true) => new InteractableState(id,
            InteractableKind.Light, 1, Vector3.zero, lit ? InteractableStateValue.Lit : InteractableStateValue.Inactive);
        private static void Apply(InteractableState[] lamps, IReadOnlyList<KeyValuePair<int, bool>> changes)
        {
            foreach (var change in changes)
                for (int i = 0; i < lamps.Length; i++) if (lamps[i].Id == change.Key) lamps[i] = Lamp(change.Key, change.Value);
        }
        [Test] public void TemporaryOverrideRestoresEachOriginalValueOnce()
        {
            var controller = new ExpeditionLightController(new ExpeditionLightBehaviorState());
            var lamps = new[] { Lamp(1), Lamp(2, false) };
            controller.Observe(new MannequinFact(new EntityId(-1), MannequinFactKind.RoomLightOverride, 1, 1, seconds: 2));
            Apply(lamps, controller.Tick(0, false, lamps));
            Assert.That(lamps[0].Value, Is.EqualTo(InteractableStateValue.Inactive));
            Apply(lamps, controller.Tick(2, false, lamps));
            Assert.That(lamps[0].Value, Is.EqualTo(InteractableStateValue.Lit));
            Assert.That(lamps[1].Value, Is.EqualTo(InteractableStateValue.Inactive));
            Assert.That(controller.Tick(1, false, lamps), Is.Empty);
        }
        [Test] public void DuplicateBudgetsAreAbsoluteAndUseStableLampOrder()
        {
            var controller = new ExpeditionLightController(new ExpeditionLightBehaviorState());
            var lamps = new[] { Lamp(4), Lamp(1), Lamp(3), Lamp(2) };
            controller.Observe(new MannequinFact(new EntityId(-1), MannequinFactKind.LampBudget, 1, value: .5f));
            controller.Observe(new MannequinFact(new EntityId(-2), MannequinFactKind.LampBudget, 1, value: .5f));
            Apply(lamps, controller.Tick(0, false, lamps));
            Assert.That(lamps[0].Value, Is.EqualTo(InteractableStateValue.Inactive));
            Assert.That(lamps[2].Value, Is.EqualTo(InteractableStateValue.Inactive));
            Assert.That(lamps[1].Value, Is.EqualTo(InteractableStateValue.Lit));
            Assert.That(lamps[3].Value, Is.EqualTo(InteractableStateValue.Lit));
            controller.Observe(new MannequinFact(new EntityId(-1), MannequinFactKind.LampBudget, 2, value: 1));
            Apply(lamps, controller.Tick(0, false, lamps));
            foreach (var lamp in lamps) Assert.That(lamp.Value, Is.EqualTo(InteractableStateValue.Lit));
        }
        [Test] public void ExpiryDuringWickDefersRestorationUntilWickEnds()
        {
            var controller = new ExpeditionLightController(new ExpeditionLightBehaviorState());
            var lamps = new[] { Lamp(1), Lamp(2, false) };
            controller.Observe(new MannequinFact(new EntityId(-1), MannequinFactKind.RoomLightOverride, 1, 1, seconds: 1));
            Apply(lamps, controller.Tick(0, false, lamps));
            // Wick's manager lights all lamps; its teardown would restore the dark snapshot.
            Apply(lamps, controller.Tick(2, true, lamps));
            foreach (var lamp in lamps) Assert.That(lamp.Value, Is.EqualTo(InteractableStateValue.Lit));
            lamps[0] = Lamp(1, false); lamps[1] = Lamp(2, false);
            Apply(lamps, controller.Tick(0, false, lamps));
            Assert.That(lamps[0].Value, Is.EqualTo(InteractableStateValue.Lit));
            Assert.That(lamps[1].Value, Is.EqualTo(InteractableStateValue.Inactive));
        }
        [Test] public void PermanentOverrideSurvivesWickAndCannotBeReplacedByTemporaryFact()
        {
            var controller = new ExpeditionLightController(new ExpeditionLightBehaviorState());
            var lamps = new[] { Lamp(1) };
            controller.Observe(new MannequinFact(new EntityId(-1), MannequinFactKind.RoomLightOverride, 1, 1, permanent: true));
            Apply(lamps, controller.Tick(0, false, lamps));
            controller.Observe(new MannequinFact(new EntityId(-2), MannequinFactKind.RoomLightOverride, 2, 1, lit: true, seconds: 5));
            Apply(lamps, controller.Tick(10, true, lamps)); Assert.That(lamps[0].Value, Is.EqualTo(InteractableStateValue.Lit));
            Apply(lamps, controller.Tick(10, false, lamps)); Assert.That(lamps[0].Value, Is.EqualTo(InteractableStateValue.Inactive));
        }
        [Test] public void NewBudgetDuringWickNeverCapturesWickForcedLightAsBaseline()
        {
            var controller = new ExpeditionLightController(new ExpeditionLightBehaviorState());
            var lamps = new[] { Lamp(1) };
            controller.Observe(new MannequinFact(new EntityId(-1), MannequinFactKind.LampBudget, 1, value: 0));
            Assert.That(controller.Tick(0, true, lamps), Is.Empty);
            lamps[0] = Lamp(1, false); // Wick restores the original unlit lamp.
            Apply(lamps, controller.Tick(0, false, lamps));
            controller.Observe(new MannequinFact(new EntityId(-1), MannequinFactKind.LampBudget, 2, value: 1));
            Apply(lamps, controller.Tick(0, false, lamps));
            Assert.That(lamps[0].Value, Is.EqualTo(InteractableStateValue.Inactive));
        }
    }
}
