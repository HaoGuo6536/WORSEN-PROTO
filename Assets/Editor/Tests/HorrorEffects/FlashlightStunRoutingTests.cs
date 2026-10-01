// ============================================================================
// FlashlightStunRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Proves the real Session publication path reaches the registered hunter Manager.
//   No Orchestrator subscriber is installed: applying gameplay reactions must not
//   depend on a presentation route being present in the scene.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · HorrorEffects.
// KEY RESPONSIBILITIES:
//   - Route one held-face stun, clear real motor momentum and retain recharge.
//   - Verify duplicate publication and uncharged aim cannot repeat the fact.
// DEPENDENCIES:
//   Core, HorrorEffects/Hunter Managers and logic, managed test configs and NUnit.
// USAGE NOTES:
//   Native Edit Mode case. Inactive components use injected state, not Initialize;
//   no NavMesh or physics queries are created. Hunter Teardown is explicit in finally.
// ============================================================================
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Session.HorrorEffects;
using Worsen.Tests.Hunter;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.HorrorEffects
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FlashlightStunRoutingTests
    {
        [Test, Category("Native")]
        public void HeldFaceFactRoutesOnceToHunterManagerAndStopsMotorWithoutSubscriber()
        {
            var hunterObject = new GameObject("stun receiver"); hunterObject.SetActive(false);
            var effectsObject = new GameObject("stun publisher"); effectsObject.SetActive(false);
            var hunter = hunterObject.AddComponent<HunterManager>();
            var motor = hunterObject.GetComponent<HunterDriver>();
            var effects = effectsObject.AddComponent<HorrorEffectsManager>();
            var id = new EntityId(-8123); var playerId = new EntityId(8123);
            var state = new HunterBehaviorState(); var motorState = new HunterDriverState();
            var receiver = new HunterController(state, HunterAttackControllerTests.Profile(), new System.Random(7),
                new PlayerBehaviorState { Id = playerId, Health = 100, SprintSpeed = 8 }, new EchoControllerTests.World());
            receiver.Reset(id, Vector3.zero, Vector3.forward);
            Set(hunter, "_state", state); Set(hunter, "_controller", receiver); Set(hunter, "_driver", motor);
            Set(motor, "_state", motorState); motorState.Steering.Velocity = Vector3.forward * 8f; motorState.VerticalSpeed = 2f;
            var tuning = ConsumableStunTests.Config();
            var items = new ConsumableController(new ConsumableBehaviorState(), tuning); items.BeginFloor();
            Set(effects, "consumables", items);
            var light = new FlashlightSample(playerId, 1, true, Vector3.zero, Vector3.forward, 18f, 52f);
            var faces = new[] { new HunterFaceSample(id, Vector3.forward * 3f, Vector3.zero, true) };
            int published = 0;
            Action<HunterStunFact> observe = fact => { Assert.That(fact.HunterId, Is.EqualTo(id)); published++; };
            effects.HunterStunned += observe;
            try
            {
                typeof(HunterRegistry).GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { hunter });
                items.Tick(.8f, 1, light, faces, Vector3.zero, true, 0, out _);
                Publish(effects); Publish(effects);
                Assert.That(published, Is.EqualTo(1)); Assert.That(items.Charged, Is.False);
                Assert.That(motor.Velocity, Is.EqualTo(Vector3.zero)); Assert.That(motorState.VerticalSpeed, Is.Zero);
                var held = receiver.Tick(default, .1f, 1);
                Assert.That(held.HoldPosition, Is.True); Assert.That(held.Speed, Is.Zero);
                items.Tick(24.9f, 2, light, faces, Vector3.zero, true, 0, out _); Publish(effects);
                Assert.That(published, Is.EqualTo(1)); Assert.That(items.Charged, Is.False);
            }
            finally
            {
                effects.HunterStunned -= observe;
                effects.Suspend(); effects.ClearHazards();
                hunter.Teardown(); Object.DestroyImmediate(effectsObject); Object.DestroyImmediate(hunterObject);
            }
        }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Publish(HorrorEffectsManager effects) => typeof(HorrorEffectsManager)
            .GetMethod("PublishConsumables", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effects, null);
    }
}
