// ============================================================================
// HorrorConsumptionOrderTests.cs
// ============================================================================
// PURPOSE:
//   Exercises a lethal floor-hand hit through real Player, HorrorEffects, Run,
//   Expedition and Progression managers. It protects the synchronous consumption
//   presentation window from an earlier terminal-health menu notification.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Session · Expedition integration test (§11).
// KEY RESPONSIBILITIES:
//   - Drive physical hand warning, grab and hit probes without fake lethal events.
//   - Require Consumed before death presentation and terminal progression state.
//   - Keep positive health updates immediate and final health accurate.
// DEPENDENCIES:
//   Core, Domain Floor/Player/Level and assembly service types, Session managers,
//   NUnit and Unity Test Framework. No production Presentation dependency.
// USAGE NOTES:
//   Requires the exclusive Unity lease. Runs in Test Framework isolated Play Mode
//   with transient objects/configs only; saves and modifies no authored scene.
//   Reflection sets fixture config references and obtains the real Expedition
//   controller to admit a hand-authored zero-hunter floor. It never invokes a
//   private event handler, hit resolver, death confirmation or Run finalizer.
//   Actual manager subscriptions, hand probes, ApplyHit and the next native fixed
//   tick establish the event order under test; procedural generation is excluded.
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
using Worsen.Domain.Chase;
using Worsen.Domain.Director;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Domain.Procedural;
using Worsen.Session.Expedition;
using Worsen.Session.HorrorEffects;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Expedition
{
    public sealed class HorrorConsumptionOrderTests
    {
        private bool previousBackground;
        private bool restoreBackground;

        [UnityTest]
        public IEnumerator LethalHandIsConsumedBeforeProgressionEndsOnTheNextNativeRunTick()
        {
            yield return new EnterPlayMode();
            previousBackground = Application.runInBackground;
            restoreBackground = true;
            Application.runInBackground = true;
            Assert.That(RunSessionManager.Instance, Is.Null, "Use the isolated Test Framework bootstrap.");
            Assert.That(ProgressionSessionManager.Instance, Is.Null);
            Assert.That(ExpeditionSessionManager.Instance, Is.Null);
            Assert.That(HorrorEffectsManager.Instance, Is.Null);
            Assert.That(PlayerRegistry.Items, Is.Empty);

            using (var fixture = new Fixture())
            {
                fixture.Player.ApplyHit(75f, fixture.Origin);
                Assert.That(fixture.Progression.Snapshot.Health, Is.EqualTo(25f),
                    "Positive health changes must still reach progression synchronously.");
                fixture.Floor.Collect(fixture.Player.Id, 101, PickupKind.Cake);
                fixture.Floor.Collect(fixture.Player.Id, 102, PickupKind.Cake);
                Assert.That(fixture.Floor.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Open));

                fixture.Floor.Tick(8f, 1);
                Assert.That(fixture.Order.Contains("Warning"), Is.True,
                    "The real room probe must acquire the player at the visible hand.");
                fixture.Floor.Tick(0.7f, 2);
                Assert.That(fixture.Order.Contains("Grabbed"), Is.True);
                Assert.That(fixture.Player.ReadOnlyState.Health, Is.EqualTo(25f));
                fixture.Floor.Tick(1.4f, 3);

                Assert.That(fixture.Player.ReadOnlyState.Health, Is.Zero,
                    "HorrorEffects must route the hand hit through Player.ApplyHit.");
                Assert.That(fixture.Order.Count(entry => entry == "Consumed"), Is.EqualTo(1));
                Assert.That(fixture.Order.Contains("ProgressionEnded"), Is.False,
                    "Terminal health cannot hide consumption before the next committed Run end.");
                Assert.That(fixture.Progression.Snapshot.Phase, Is.EqualTo(ProgressionPhase.Exploring));
                Assert.That(fixture.Summaries, Is.Empty);

                double deadline = Time.realtimeSinceStartupAsDouble + 3d;
                while (fixture.Progression.Snapshot.Phase != ProgressionPhase.Ended)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline),
                        "The next native Run fixed tick did not commit the lethal result.");
                    yield return null;
                }

                Assert.That(fixture.Progression.Snapshot.Health, Is.Zero);
                Assert.That(fixture.Run.Phase, Is.EqualTo(RunPhase.Ended));
                Assert.That(fixture.Summaries.Count, Is.EqualTo(1));
                Assert.That(fixture.Summaries[0].EndReason, Is.EqualTo(RunEndReason.Died));
                Assert.That(fixture.Order.Count(entry => entry == "ProgressionEnded"), Is.EqualTo(1));
                Assert.That(fixture.Order.IndexOf("Consumed"), Is.LessThan(fixture.Order.IndexOf("PlayerDied")));
                Assert.That(fixture.Order.IndexOf("PlayerDied"), Is.LessThan(fixture.Order.IndexOf("ProgressionEnded")));
                Assert.That(fixture.ProgressionObservedCommittedRunEnd, Is.True,
                    "Progression UI listeners may receive Ended only after Run committed its terminal phase.");
            }
            yield return null;
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<Object> owned = new List<Object>();
            public readonly Vector3 Origin = new Vector3(44000f, 0f, 44000f);
            public readonly RunSessionManager Run;
            public readonly ProgressionSessionManager Progression;
            public readonly FloorManager Floor;
            public readonly PlayerManager Player;
            public readonly List<string> Order = new List<string>();
            public readonly List<RunSummary> Summaries = new List<RunSummary>();
            public bool ProgressionObservedCommittedRunEnd;
            private readonly ExpeditionSessionManager expedition;
            private readonly HorrorEffectsManager effects;

            public Fixture()
            {
                var progressionConfig = Config<ProgressionConfig>();
                var floorConfig = Config<FloorConfig>();
                var floorVisual = Config<FloorDriverConfig>();
                var playerProfile = Config<PlayerProfile>();
                var moverConfig = Config<PlayerMoverDriverConfig>();
                Set(floorConfig, "_requiredCakeCount", 2);
                Run = Component<RunSessionManager>("Consumption Run").Initialize(901);
                Progression = Component<ProgressionSessionManager>("Consumption Progression").Initialize(progressionConfig, 901);
                expedition = Component<ExpeditionSessionManager>("Consumption Expedition").Initialize();
                effects = Component<HorrorEffectsManager>("Consumption Effects").Initialize(Config<HorrorEffectsConfig>());
                Floor = Component<FloorManager>("Consumption Floor");
                Set(Floor.GetComponent<FloorDriver>(), "_config", floorVisual);
                var level = Component<LevelManager>("Consumption Level");
                var factory = Component<PlayerFactory>("Consumption Player Factory");
                var procedural = Component<ProceduralManager>("Consumption Procedural Boundary");

                Progression.StartRun(901);
                Assert.That(Progression.ChooseThreat(Progression.Snapshot.Choices[0].Id, Progression.Snapshot.Revision), Is.True);
                Assert.That(Progression.ChooseCurse(Progression.Snapshot.Choices[0].Id, Progression.Snapshot.Revision), Is.True);
                int generation = Progression.Snapshot.GenerationId;
                expedition.ConfigureScene(Run, Progression, procedural, Config<ProceduralConfig>(), Config<ProceduralDriverConfig>(),
                    level, factory, playerProfile, Component<HunterFactory>("Consumption Hunter Factory"), Config<HunterProfile>(),
                    Component<ChaseManager>("Consumption Chase"), Config<ChaseConfig>(), Floor, floorConfig,
                    Component<DirectorManager>("Consumption Director"), Config<DirectorConfig>(), SceneKey.HorrorRun, effects: effects);

                // Admit a deterministic zero-hunter fixture through the real assembly controller.
                // Generation itself is outside this event-order regression's scope.
                var assembly = Get<ExpeditionSessionController>(expedition, "_controller");
                var combatEffects = new ProgressionEffects(1f, 1f, 1f, 1f, 100f, 100f, 0);
                var request = new ProgressionGenerationRequest(generation, 901, 1, false, combatEffects);
                Assert.That(assembly.Queue(request), Is.True);
                Assert.That(assembly.Begin(generation), Is.True);
                Run.PrepareScene(SceneKey.HorrorRun, 901);

                var room = new LevelRoom(1, Origin + Vector3.up * 3.5f, new Vector3(12f, 7f, 12f));
                Vector3 handPosition = new RoomCollapsePresenter().GridPoint(room.Bounds, 0, floorVisual.HandGridWidth, floorVisual.PortalInset);
                handPosition.y = room.Bounds.min.y + 0.015f;
                var template = new GameObject("Consumption Player Template");
                owned.Add(template);
                template.SetActive(false);
                var templatePlayer = template.AddComponent<PlayerManager>();
                Set(templatePlayer.GetComponent<PlayerDriver>(), "_config", moverConfig);
                Set(playerProfile, "_prefab", template);
                factory.Configure(playerProfile, Run.RandomSource);
                EntityId playerId = factory.Spawn(new SpawnRequest(playerProfile.ArchetypeKey, handPosition, Quaternion.identity));
                Assert.That(PlayerRegistry.TryGet(playerId, out PlayerManager spawned), Is.True);
                Player = spawned;
                Player.gameObject.SetActive(true);
                assembly.RecordPlayer(playerId);
                var graph = LevelGraphUtility.Build(new[] { room }, Array.Empty<LevelEdge>(),
                    new[] { new LevelAnchor(101, 1, CakeAnchorType.Flow, Origin),
                        new LevelAnchor(102, 1, CakeAnchorType.Flow, Origin + Vector3.right * 4f) }, 1, Origin + Vector3.forward * 4f);
                level.InitializeGenerated(graph);
                Floor.Initialize(floorConfig, level.ReadOnlyState, new[] { Player.ReadOnlyState }, Run.RandomSource, 2);
                Floor.OnCollapseHand += ObserveHand;
                Run.BindGameplay(null, Floor, null);
                effects.BeginFloor(generation, combatEffects);
                effects.ConfigureHazards(Progression, Floor);
                effects.BindActors();
                assembly.Ready();
                Assert.That(Progression.ConfirmFloorReady(generation), Is.True);
                Run.HandleSceneReady(SceneKey.HorrorRun);
                Run.PlayerDied += ObserveDeath;
                Run.RunEnded += Summaries.Add;
                Progression.SnapshotChanged += ObserveProgression;
                Physics.SyncTransforms();
            }

            private void ObserveHand(CollapseHandFact fact) => Order.Add(fact.Kind.ToString());
            private void ObserveDeath(EntityId player, Vector3 position) => Order.Add("PlayerDied");
            private void ObserveProgression(ProgressionSnapshot snapshot)
            {
                if (snapshot.Phase != ProgressionPhase.Ended) return;
                Order.Add("ProgressionEnded");
                ProgressionObservedCommittedRunEnd = Run.Phase == RunPhase.Ended;
            }
            private T Component<T>(string name) where T : Component
            {
                var root = new GameObject(name);
                owned.Add(root);
                return root.AddComponent<T>();
            }
            private T Config<T>() where T : ScriptableObject
            { T asset = ScriptableObject.CreateInstance<T>(); owned.Add(asset); return asset; }
            public void Dispose()
            {
                if (Progression != null) Progression.SnapshotChanged -= ObserveProgression;
                if (Run != null) { Run.PlayerDied -= ObserveDeath; Run.RunEnded -= Summaries.Add; }
                if (Floor != null) Floor.OnCollapseHand -= ObserveHand;
                if (effects != null) effects.ClearHazards();
                if (expedition != null) expedition.ClearScene();
                for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.Destroy(owned[i]);
            }
        }

        private static void Set(object owner, string field, object value)
        {
            Assert.That(owner, Is.Not.Null);
            FieldInfo member = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(member, Is.Not.Null, "Fixture binding field changed: " + field);
            member.SetValue(owner, value);
        }
        private static T Get<T>(object owner, string field)
        {
            FieldInfo member = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(member, Is.Not.Null, "Fixture observation field changed: " + field);
            return (T)member.GetValue(owner);
        }
        [UnityTearDown]
        public IEnumerator RestoreEditor()
        {
            if (restoreBackground) { Application.runInBackground = previousBackground; restoreBackground = false; }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
