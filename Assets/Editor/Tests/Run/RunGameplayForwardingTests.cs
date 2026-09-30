// ============================================================================
// RunGameplayForwardingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises Session relays with native Unity lifecycle dispatch. Source facts
//   are injected at Manager event boundaries; real Hunter hearing consumes noise.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Run.
// KEY RESPONSIBILITIES:
//   - Verify payload preservation, single subscriptions, pause rejection and teardown.
//   - Verify replacement publishers detach and pickups never become hunter stimuli.
// DEPENDENCIES:
//   Core, Domain Player/Hunter/Level/Floor, Session Run, NUnit and Unity Test Framework.
// USAGE NOTES:
//   Enters Play Mode under the coordinator's lease. Does not load/save scenes or
//   assets. Injected controllers avoid navigation and independent gameplay ticks;
//   activation, disable and destruction callbacks are dispatched by Unity itself.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Run
{
    public sealed class RunGameplayForwardingTests
    {
        [UnityTest]
        public IEnumerator RelaysPairAcrossRebindPauseDisableDetachLoadAndDestroy()
        {
            yield return new EnterPlayMode();
            Assert.That(PlayerRegistry.Items, Is.Empty);
            Assert.That(HunterRegistry.Items, Is.Empty);
            Assert.That(RunSessionManager.Instance, Is.Null);
            using (var f = new Fixture())
            {
                f.Bind(); f.Check(true);
                f.Bind(); f.Check(true);
                f.Run.SetPaused(true); f.Check(false, subscribed: true);
                f.Run.SetPaused(false); f.Check(true);
                f.Run.enabled = false; f.Check(false);
                f.Bind(); f.Check(false);
                f.Run.enabled = true; f.Check(true);
                f.Run.gameObject.SetActive(false); f.Check(false);
                f.Bind(); f.Check(false);
                f.Run.gameObject.SetActive(true); f.Check(true);
                f.ReplacePublishers(); f.Check(true);
                f.Run.DetachGameplay(); f.Check(false);
                f.Bind(); f.Check(true);
                f.Run.SuspendForSceneLoad(); f.Check(false);
                f.Bind(); f.Check(true);
                Object.DestroyImmediate(f.Run.gameObject); f.Check(false);
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        private sealed class Fixture : IDisposable
        {
            private readonly List<Object> owned = new List<Object>();
            private readonly List<PlayerManager> players = new List<PlayerManager>();
            private readonly List<FloorManager> floors = new List<FloorManager>();
            private readonly List<HunterManager> hunters = new List<HunterManager>();
            private readonly List<object[]> traversal = new List<object[]>(), stumble = new List<object[]>(), lost = new List<object[]>();
            private readonly List<GraceWindowFact> graceStarts = new List<GraceWindowFact>(), graceEnds = new List<GraceWindowFact>();
            private int current, serial;
            public RunSessionManager Run { get; }
            public Fixture()
            {
                Run = Component<RunSessionManager>();
                var state = new RunSessionBehaviorState(13);
                var clock = new RunSessionController(state, new System.Random(13));
                clock.StartScene(SceneKey.HorrorRun);
                Set(Run, "state", state); Set(Run, "controller", clock);
                for (int i = 0; i < 2; i++)
                {
                    var player = Component<PlayerManager>();
                    Set(player, "_state", new PlayerBehaviorState { Id = new EntityId(i + 1), Health = 100f });
                    players.Add(player); floors.Add(Component<FloorManager>());
                }
                Registry(typeof(PlayerRegistry), "Register", players[0]);
                var profile = ScriptableObject.CreateInstance<HunterProfile>(); owned.Add(profile);
                for (int i = 0; i < 5; i++)
                {
                    var hunter = Component<HunterManager>();
                    var memory = new HunterBehaviorState();
                    var controller = new HunterController(memory, profile, new System.Random(13), players[0].ReadOnlyState, new LevelFixture());
                    controller.Reset(new EntityId(-i - 1), Vector3.zero, Vector3.forward);
                    Set(hunter, "_state", memory); Set(hunter, "_controller", controller);
                    if (i == 2) hunter.enabled = false;
                    if (i == 3) typeof(HunterBehaviorState).GetProperty("IsActive").SetValue(memory, false);
                    if (i < 4) Registry(typeof(HunterRegistry), "Register", hunter);
                    hunters.Add(hunter);
                }
                Run.TraversalProgressed += (id, tick, kind, progress, active) => traversal.Add(new object[] { id, tick, kind, progress, active });
                Run.PlayerStumbled += (id, tick, duration) => stumble.Add(new object[] { id, tick, duration });
                Run.CakeLost += (anchor, room, kind, tick) => lost.Add(new object[] { anchor, room, kind, tick });
                Run.OnGraceStarted += graceStarts.Add; Run.OnGraceEnded += graceEnds.Add;
            }
            public void Bind() => Run.BindGameplay(null, floors[current], null);
            public void ReplacePublishers()
            {
                Registry(typeof(PlayerRegistry), "Unregister", players[current]);
                current = 1;
                Registry(typeof(PlayerRegistry), "Register", players[current]);
                Bind();
            }
            public void Check(bool forwards, bool subscribed = false)
            {
                int before = traversal.Count;
                var noise = new NoiseEvent(players[current].Id, Vector3.zero, ++serial, 0, NoiseSourceKind.CakePickup);
                int[] heardBefore = hunters.Select(h => Heard(h).Count).ToArray();
                for (int i = 0; i < players.Count; i++)
                {
                    int expected = (forwards || subscribed) && i == current ? 1 : 0;
                    foreach (string name in new[] { "OnTraversalProgress", "OnStumbled", "OnGraceStarted", "OnGraceEnded" })
                        AssertSubscribers(players[i], name, expected);
                    foreach (string name in new[] { "OnCakeLost", "OnPickupNoise" }) AssertSubscribers(floors[i], name, expected);
                    Raise(players[i], "OnTraversalProgress", players[i].Id, 17L, TraversalKind.Mantle, .75f, false);
                    Raise(players[i], "OnStumbled", players[i].Id, 17L, .4f);
                    Raise(players[i], "OnGraceStarted", default(GraceWindowFact));
                    Raise(players[i], "OnGraceEnded", default(GraceWindowFact));
                    Raise(floors[i], "OnCakeLost", 7, 3, PickupKind.GoldenCake, 17L);
                    Raise(floors[i], "OnPickupNoise", noise);
                }
                int total = before + (forwards ? 1 : 0);
                Assert.That(traversal.Count, Is.EqualTo(total)); Assert.That(stumble.Count, Is.EqualTo(total));
                Assert.That(lost.Count, Is.EqualTo(total)); Assert.That(graceStarts.Count, Is.EqualTo(total));
                Assert.That(graceEnds.Count, Is.EqualTo(total));
                if (forwards)
                {
                    Assert.That(traversal.Last(), Is.EqualTo(new object[] { players[current].Id, 17L, TraversalKind.Mantle, .75f, false }));
                    Assert.That(stumble.Last(), Is.EqualTo(new object[] { players[current].Id, 17L, .4f }));
                    Assert.That(lost.Last(), Is.EqualTo(new object[] { 7, 3, PickupKind.GoldenCake, 17L }));
                }
                for (int i = 0; i < hunters.Count; i++)
                {
                    Assert.That(Heard(hunters[i]).Count, Is.EqualTo(heardBefore[i]), "Pickup leaked to hunter " + i);
                }
            }
            private static List<NoiseEvent> Heard(HunterManager hunter) => (List<NoiseEvent>)Field(hunter.ReadOnlyState, "HeardNoises").GetValue(hunter.ReadOnlyState);
            private void AssertSubscribers(object publisher, string name, int count)
            {
                var handlers = Field(publisher, name).GetValue(publisher) as Delegate;
                Assert.That(handlers?.GetInvocationList().Count(value => ReferenceEquals(value.Target, Run)) ?? 0, Is.EqualTo(count), name);
            }
            private T Component<T>() where T : Component
            { var go = new GameObject(typeof(T).Name + " forwarding test"); owned.Add(go); return go.AddComponent<T>(); }
            public void Dispose()
            {
                if (Run != null) Run.DetachGameplay();
                foreach (var player in players) Registry(typeof(PlayerRegistry), "Unregister", player);
                foreach (var hunter in hunters) Registry(typeof(HunterRegistry), "Unregister", hunter);
                for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            }
        }
        private sealed class LevelFixture : IReadOnlyLevelState
        {
            public bool IsReady => true;
            public LevelGraph Graph { get; } = new LevelGraph(new[] { new LevelRoom(1, Vector3.zero, Vector3.one * 20f) },
                Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, Vector3.zero);
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static void Raise(object target, string name, params object[] args) => (Field(target, name).GetValue(target) as Delegate)?.DynamicInvoke(args);
        private static void Registry(Type type, string method, object actor) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { actor });
    }
}
