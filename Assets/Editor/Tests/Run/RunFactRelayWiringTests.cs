// ============================================================================
// RunFactRelayWiringTests.cs
// ============================================================================
// PURPOSE:
//   Exercises all four fact groups through actual Manager event subscriptions.
//   Explicit lifecycle calls isolate routing from scene assembly and actor ticking.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Run.
// KEY RESPONSIBILITIES:
//   - Verify one input subscription and one upward delivery per group.
//   - Verify pause, repeated enable, disable, detach and owning Manager teardown.
//   - Keep typed channel identity stable across lifecycle boundaries.
// DEPENDENCIES:
//   - Run/Progression, Domain Player/Hunter/Floor, Core, NUnit and Unity objects.
// USAGE NOTES:
//   Native Edit Mode test, not headless coverage. Inactive disposable publishers
//   avoid registry registration; no assets, scenes or persistent initialization.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Hunter;
using Worsen.Domain.Floor;
using Worsen.Session.Run;
using Worsen.Session.Progression;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Run
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RunFactRelayWiringTests
    {
        [Test]
        public void EachGroupPairsOnceAndManagerTeardownClearsStableChannels()
        {
            var owned = new List<GameObject>();
            var run = Make<RunSessionManager>(owned);
            var player = Make<PlayerManager>(owned);
            var hunter = Make<HunterManager>(owned);
            var floor = Make<FloorManager>(owned);
            var progression = Make<ProgressionSessionManager>(owned);
            var state = new RunSessionBehaviorState(1);
            var clock = new RunSessionController(state, new System.Random(1));
            clock.StartScene(SceneKey.HorrorRun);
            Set(run, "state", state); Set(run, "controller", clock);
            object[] channels = { run.HunterFacts, run.FloorFacts, run.PlayerFacts, run.WorldFacts };
            object[] sources = { hunter, floor, player, progression };
            string[] inputs = { "OnHabit", "OnGuidanceChanged", "OnStumbled", "ShrineNoiseEmitted" };
            string[] outputs = { "HunterHabitPublished", "GuidanceChanged", "PlayerStumbled", "WorldNoisePublished" };
            var order = new List<int>();
            run.HunterFacts.HunterHabitPublished += _ => order.Add(0);
            run.FloorFacts.GuidanceChanged += _ => order.Add(1);
            run.PlayerFacts.PlayerStumbled += (_, __, ___) => order.Add(2);
            run.WorldFacts.WorldNoisePublished += _ => order.Add(3);
            void Bind()
            {
                ((List<PlayerManager>)Read(run, "players")).Add(player);
                ((List<HunterManager>)Read(run, "hunters")).Add(hunter);
                Set(run, "floor", floor);
                run.BindShrines(null, progression, 1, new EntityId(1), null);
                Call(run, "OnEnable"); Call(run, "OnEnable");
            }
            void Check(int subscriptions, bool deliver)
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    var input = Read(sources[i], inputs[i]) as Delegate;
                    Assert.That(input?.GetInvocationList().Length ?? 0, Is.EqualTo(subscriptions), inputs[i]);
                    if (input != null) Assert.That(input.Target, Is.SameAs(channels[i]));
                }
                order.Clear();
                Publish(hunter, inputs[0], default(HunterHabitFact));
                Publish(floor, inputs[1], (object)Array.Empty<GuidanceTarget>());
                Publish(player, inputs[2], new EntityId(1), 7L, .5f);
                Publish(progression, inputs[3], new NoiseEvent(new EntityId(1), Vector3.one, .8f, 7));
                Assert.That(order, Is.EqualTo(deliver ? new[] { 0, 1, 2, 3 } : Array.Empty<int>()));
            }
            try
            {
                Bind(); Check(1, true);
                run.SetPaused(true); Check(1, false);
                run.SetPaused(false); Check(1, true);
                Call(run, "OnDisable"); Check(0, false);
                Call(run, "OnEnable"); Check(1, true);
                run.DetachGameplay(); Check(0, false);
                Bind(); Check(1, true);
                Assert.That(run.HunterFacts, Is.SameAs(channels[0]));
                Assert.That(run.FloorFacts, Is.SameAs(channels[1]));
                Assert.That(run.PlayerFacts, Is.SameAs(channels[2]));
                Assert.That(run.WorldFacts, Is.SameAs(channels[3]));
                Call(run, "OnDestroy"); Check(0, false);
                for (int i = 0; i < channels.Length; i++) Assert.That(Read(channels[i], outputs[i]), Is.Null);
            }
            finally
            {
                run.DetachGameplay();
                for (int i = owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(owned[i]);
            }
        }

        private static T Make<T>(List<GameObject> owned) where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); owned.Add(go); return go.AddComponent<T>(); }
        private static object Read(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Publish(object target, string name, params object[] values) => (Read(target, name) as Delegate)?.DynamicInvoke(values);
    }
}
