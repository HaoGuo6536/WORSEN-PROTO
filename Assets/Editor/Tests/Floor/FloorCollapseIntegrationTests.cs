// ============================================================================
// FloorCollapseIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies default FloorLoop closure through live warning lights, collision,
//   navigation carving and the production lethal-contact route into Session.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Floor scene integration.
// KEY RESPONSIBILITIES:
//   - Compare an authored doorway and complete path before and after closure.
//   - Require HunterDriver to reject navigation into a genuinely carved room.
//   - Verify stationary closure overlap and later physical entry each end once.
// DEPENDENCIES:
//   - Core, Floor/Level/Player/Hunter, Run Session, Unity physics and navigation.
//   - Unity Test Framework and the existing generated FloorLoop scene/configs.
// USAGE NOTES:
//   Coordinator holds the Unity lease. Never builds, saves or bakes assets.
//   Test locals/background policy are established after EnterPlayMode and restored.
//   Teleports, disabling HunterManager and direct Manager collection arrange the
//   scenario; they are not first-sweep, movement, chase or pickup-trigger evidence.
//   Normal Session ticks advance unmodified 6-second/12-second collapse defaults.
//   Uses clock/event predicates because the Edit Mode runner ignores ordinary
//   coroutine wait instructions, including WaitForFixedUpdate after EnterPlayMode.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Session.Run;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Floor
{
    public sealed class FloorCollapseIntegrationTests
    {
        private const string ScenePath = "Assets/Scenes/FloorLoop.unity";
        private bool _restoreBackground;
        private bool _previousBackground;

        [UnityTest]
        public IEnumerator DefaultClosureBlocksDoorCarvesNavigationAndKillsLaterEntry()
        {
            yield return new EnterPlayMode();
            yield return WithBackground(false);
        }

        [UnityTest]
        public IEnumerator StationaryPlayerDiesDuringTheFirstDefaultClosureTick()
        {
            yield return new EnterPlayMode();
            yield return WithBackground(true);
        }

        private IEnumerator WithBackground(bool stationary)
        {
            _previousBackground = Application.runInBackground;
            _restoreBackground = true;
            Application.runInBackground = true;
            try { yield return ExerciseCollapse(stationary); }
            finally { RestoreBackground(); }
        }

        private static IEnumerator ExerciseCollapse(bool stationary)
        {
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "Generate and enable FloorLoop before this fixture.");
            yield return Until(() => load.isDone && Ready(), "FloorLoop did not become ready.");
            var run = One<RunSessionManager>();
            var floor = One<FloorManager>();
            var floorDriver = floor.GetComponent<FloorDriver>();
            var player = One<PlayerManager>();
            var hunter = One<HunterManager>();
            var hunterDriver = hunter.GetComponent<HunterDriver>();
            var graph = One<LevelManager>().ReadOnlyState.Graph;
            var config = Configured<FloorConfig>(floor, "_config");
            var driverConfig = Configured<FloorDriverConfig>(floorDriver, "_config");
            var hunterProfile = Configured<HunterProfile>(hunter, "_profile");
            Assert.That(config.TelegraphDuration, Is.EqualTo(6f));
            Assert.That(config.CollapseInterval, Is.EqualTo(12f));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "This fixture requires ordinary simulation time.");
            var selected = floor.ReadOnlyState.ActiveCakeAnchors.ToArray();
            var distances = LevelGraphUtility.DistancesTo(graph, graph.ExitRoomId, TraversalAccess.Player);
            var firstRoom = graph.Rooms.OrderByDescending(room => distances[room.Id]).ThenBy(room => room.Id).First();
            Assert.That(firstRoom.Id, Is.Not.EqualTo(graph.ExitRoomId));
            var room = floor.GetComponentsInChildren<RoomCollapseVolume>()
                .Single(volume => Vector3.Distance(volume.transform.position, firstRoom.Center) < 0.001f);
            var blockers = room.GetComponentsInChildren<BoxCollider>(true).Where(collider => !collider.isTrigger).ToArray();
            var warning = room.GetComponent<Light>();
            var obstacle = room.GetComponent<NavMeshObstacle>();
            Assert.That(blockers, Has.Length.EqualTo(4));
            Assert.That(blockers.All(blocker => !blocker.gameObject.activeInHierarchy), Is.True);
            Assert.That(warning.enabled, Is.False);
            Assert.That(obstacle.enabled, Is.False);

            // A Z-boundary doorway keeps both probe sides on the same authored
            // floor elevation. Capture its live marker rather than rebuilding geometry.
            var door = Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None)
                .Select(marker => marker.Capture())
                .Where(marker => marker.Kind == LevelMarkerKind.RoomLink && marker.Size.z < marker.Size.x &&
                    (marker.RoomId == firstRoom.Id || marker.TargetRoomId == firstRoom.Id))
                .OrderBy(marker => marker.Id).First();
            int neighborId = door.RoomId == firstRoom.Id ? door.TargetRoomId : door.RoomId;
            var neighbor = graph.Rooms.Single(candidate => candidate.Id == neighborId);
            Vector3 outward = Vector3.ProjectOnPlane(neighbor.Center - firstRoom.Center, Vector3.up).normalized;
            var inside = graph.Anchors.First(anchor => anchor.RoomId == firstRoom.Id).Position;
            var outside = graph.Anchors.First(anchor => anchor.RoomId == neighborId).Position;
            var outsideControl = graph.Anchors.Where(anchor => anchor.RoomId == neighborId).Skip(1).First().Position;
            var safe = graph.Anchors.First(anchor => anchor.RoomId == graph.ExitRoomId &&
                Vector3.Distance(anchor.Position, graph.ExitPosition) > 3f).Position;
            Assert.That(firstRoom.Bounds.Contains(inside + Vector3.up), Is.True);
            Assert.That(Mathf.Min(inside.x - firstRoom.Bounds.min.x, firstRoom.Bounds.max.x - inside.x,
                inside.z - firstRoom.Bounds.min.z, firstRoom.Bounds.max.z - inside.z), Is.GreaterThan(driverConfig.PathSampleRadius));

            // Keep the initialized Driver/body for its real navigation consumer probe,
            // but remove the Manager from Session's actor tick registry during arrangement.
            hunter.enabled = false;
            Assert.That(HunterRegistry.Items.Count, Is.Zero);
            var transitions = new List<RoomPhaseChangedFact>();
            var warningSamples = new List<float>();
            var summaries = new List<RunSummary>();
            var lethalFacts = new List<FloorLethalContactFact>();
            double telegraphElapsed = double.NaN, closedElapsed = double.NaN;
            long closedTick = -1;
            bool deadAtClosedTickPublication = false;
            Action neutral = () => run.ReceiveInput(default);
            Action<RoomPhaseChangedFact> phase = fact =>
            {
                transitions.Add(fact);
                if (fact.RoomId != firstRoom.Id) return;
                if (fact.Phase == RoomPhase.Telegraph) telegraphElapsed = run.ElapsedSeconds;
                if (fact.Phase == RoomPhase.Closed) { closedElapsed = run.ElapsedSeconds; closedTick = fact.Tick; }
            };
            Action<FloorLethalContactFact> lethal = fact => lethalFacts.Add(fact);
            Action<RunSummary> ended = summary => summaries.Add(summary);
            Action<InputFrame, float, long> tick = (_, __, stamp) =>
            {
                if (floor.ReadOnlyState.RoomPhases[firstRoom.Id] == RoomPhase.Telegraph) warningSamples.Add(warning.intensity);
                if (stamp == closedTick) deadAtClosedTickPublication = !player.ReadOnlyState.IsAlive && lethalFacts.Count == 1;
            };
            run.BeforeTick += neutral;
            run.TickAdvanced += tick;
            run.RunEnded += ended;
            floor.OnRoomPhaseChanged += phase;
            floor.OnLethalContact += lethal;
            try
            {
                long beforeArrange = run.Tick;
                ArrangePlayerAt(player, stationary ? inside : safe);
                yield return Until(() => run.Tick > beforeArrange, "The arranged Player pose did not reach a normal tick.", 3f);
                AssertPose(player, stationary ? inside : safe);
                Assert.That(player.ReadOnlyState.IsAlive, Is.True);
                Assert.That(DoorHits(door.Position, outward), Is.Empty, "The authored doorway must be physically open before collapse.");
                Assert.That(CompletePath(outside, inside), Is.True, "The same navigation route must exist before the room closes.");
                Assert.That(CompletePath(outside, outsideControl), Is.True, "The surviving-room control route must exist before closure.");
                ProbeHunter(hunterDriver, hunterProfile, outside, inside);
                Assert.That(hunterDriver.PathAvailable, Is.True, "The actual HunterDriver must accept the initial complete path.");

                // Collection is explicit arrangement; the separate FloorLoop fixture
                // proves real cake triggers. No Floor tick, config or time acceleration.
                foreach (var anchor in selected) floor.Collect(player.Id, anchor.Id, PickupKind.Cake);
                Assert.That(floor.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Open));
                Assert.That(transitions, Is.Not.Empty);
                Assert.That(transitions[0].RoomId, Is.EqualTo(firstRoom.Id));
                Assert.That(transitions[0].Phase, Is.EqualTo(RoomPhase.Telegraph));
                Assert.That(warning.enabled, Is.True);
                Assert.That(warning.color, Is.EqualTo(driverConfig.WarningColor));
                Assert.That(obstacle.enabled, Is.False);
                Assert.That(blockers.All(blocker => !blocker.gameObject.activeInHierarchy), Is.True);
                Assert.That(player.ReadOnlyState.IsAlive, Is.True, "Telegraph must not be lethal.");

                yield return Until(() => floor.ReadOnlyState.RoomPhases[firstRoom.Id] == RoomPhase.Closed,
                    "Normal Session ticks did not complete the six-second telegraph.", 20f);
                Assert.That(closedElapsed - telegraphElapsed,
                    Is.EqualTo(config.TelegraphDuration).Within(Time.fixedDeltaTime + 0.0001d));
                Assert.That(transitions.Count(fact => fact.RoomId == firstRoom.Id && fact.Phase == RoomPhase.Closed), Is.EqualTo(1));
                Assert.That(warningSamples.Count, Is.GreaterThan(1));
                Assert.That(warningSamples.Max() - warningSamples.Min(), Is.GreaterThan(0.1f), "The live telegraph light must pulse.");
                Assert.That(warning.enabled, Is.True);
                Assert.That(warning.color, Is.EqualTo(driverConfig.ClosedColor));
                Assert.That(blockers.All(blocker => blocker.enabled && blocker.gameObject.activeInHierarchy), Is.True);
                Assert.That(obstacle.enabled && obstacle.carving && !obstacle.carveOnlyStationary, Is.True);
                Assert.That(DoorHits(door.Position, outward).Any(hit => blockers.Contains(hit.collider)), Is.True,
                    "A raised Floor blocker must obstruct the previously clear doorway.");

                if (stationary)
                {
                    Assert.That(deadAtClosedTickPublication, Is.True,
                        "Closure must kill the stationary occupant before Session publishes that tick, without waiting for a later trigger callback.");
                }
                else
                {
                    Assert.That(player.ReadOnlyState.IsAlive, Is.True, "Player outside the closed room must remain safe.");
                    Assert.That(lethalFacts, Is.Empty);
                    // Carving affects navigation asynchronously. Compare the identical
                    // deep endpoint; the small radius cannot sample a neighboring room.
                    yield return Until(() => !NavMesh.SamplePosition(inside, out _, 0.5f, NavMesh.AllAreas) &&
                        !CompletePath(outside, inside), "The room obstacle did not remove its live navigation surface.", 5f);
                    Assert.That(NavMesh.SamplePosition(outside, out _, 0.5f, NavMesh.AllAreas), Is.True,
                        "The unclosed neighboring room must retain its navigation surface.");
                    Assert.That(CompletePath(outside, outsideControl), Is.True,
                        "A control route in the neighboring room must survive; losing the whole NavMesh is not valid carving.");
                    Assert.That(float.IsPositiveInfinity(floorDriver.PathLength(outside, inside)), Is.True);
                    Vector3 hunterBefore = outside;
                    ProbeHunter(hunterDriver, hunterProfile, hunterBefore, inside);
                    Assert.That(hunterDriver.PathAvailable, Is.False, "HunterDriver must reject the carved route, not merely increase its cost.");
                    Assert.That(Vector3.ProjectOnPlane(hunterDriver.Position - hunterBefore, Vector3.up).magnitude, Is.LessThan(0.0001f));
                    Assert.That(summaries, Is.Empty);
                    ArrangePlayerAt(player, inside);
                }

                yield return Until(() => summaries.Count > 0, "Closed-room contact did not reach Session's death outcome.", 3f);
                Assert.That(player.ReadOnlyState.Health, Is.Zero);
                Assert.That(run.Phase, Is.EqualTo(RunPhase.Ended));
                Assert.That(summaries, Has.Count.EqualTo(1));
                Assert.That(summaries[0].EndReason, Is.EqualTo(RunEndReason.Died));
                Assert.That(summaries[0].Scene, Is.EqualTo(SceneKey.FloorLoop));
                Assert.That(lethalFacts, Has.Count.EqualTo(1));
                Assert.That(lethalFacts[0].PlayerId, Is.EqualTo(player.Id));
                Assert.That(lethalFacts[0].RoomId, Is.EqualTo(firstRoom.Id));
                Assert.That(lethalFacts[0].Tick, Is.GreaterThanOrEqualTo(closedTick));
                long finalTick = run.Tick;
                double fixedBefore = Time.fixedTimeAsDouble, requiredAdvance = 2d * Time.fixedDeltaTime;
                yield return Until(() => Time.fixedTimeAsDouble - fixedBefore >= requiredAdvance,
                    "Physics did not continue for repeated closed-volume contacts.", 3f);
                Assert.That(run.Tick, Is.EqualTo(finalTick));
                Assert.That(lethalFacts, Has.Count.EqualTo(1));
                Assert.That(summaries, Has.Count.EqualTo(1));
            }
            finally
            {
                if (run != null) { run.BeforeTick -= neutral; run.TickAdvanced -= tick; run.RunEnded -= ended; }
                if (floor != null) { floor.OnRoomPhaseChanged -= phase; floor.OnLethalContact -= lethal; }
                // ExitPlayMode disposes the arranged scene. Re-enabling a factory-owned
                // Hunter Manager alone would not restore its registry membership.
            }
        }

        private static RaycastHit[] DoorHits(Vector3 doorway, Vector3 outward)
        {
            Vector3 feet = doorway + outward * 2f + Vector3.up * 0.1f;
            return Physics.CapsuleCastAll(feet + Vector3.up * 0.3f, feet + Vector3.up * 1.5f,
                0.3f, -outward, 4f, ~0, QueryTriggerInteraction.Ignore);
        }

        private static bool CompletePath(Vector3 from, Vector3 to)
        {
            return NavMesh.SamplePosition(from, out var start, 0.5f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(to, out var end, 0.5f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, SharedPath) &&
                SharedPath.status == NavMeshPathStatus.PathComplete;
        }

        // Allocated lazily after EnterPlayMode; this stores only a temporary engine
        // query result and is reset during fixture teardown.
        private static NavMeshPath _path;
        private static NavMeshPath SharedPath => _path ?? (_path = new NavMeshPath());

        private static void ProbeHunter(HunterDriver driver, HunterProfile profile, Vector3 from, Vector3 target)
        {
            driver.transform.position = from;
            driver.GetComponent<Rigidbody>().position = from;
            driver.Initialize();
            Physics.SyncTransforms();
            driver.Move(target, profile.PatrolSpeed, profile.Acceleration, profile.TurnRate,
                Time.fixedDeltaTime, false, false, Vector3.zero, profile.LungeSpeed, profile.LungeDistance);
        }

        private static void ArrangePlayerAt(PlayerManager player, Vector3 feet)
        {
            Assert.That(player.ReadOnlyState.IsAlive, Is.True);
            Assert.That(player.ReadOnlyState.MovementState, Is.Not.EqualTo(MovementState.Vault).And.Not.EqualTo(MovementState.Slide));
            player.transform.position = feet;
            player.GetComponent<Rigidbody>().position = feet;
            player.GetComponent<PlayerDriver>().Initialize();
            Physics.SyncTransforms();
        }

        private static void AssertPose(PlayerManager player, Vector3 expected)
        {
            Assert.That(Vector3.Distance(player.ReadOnlyState.Position, expected), Is.LessThan(0.4f));
            Assert.That(Vector3.Distance(player.GetComponent<PlayerDriver>().Position, player.ReadOnlyState.Position), Is.LessThan(0.001f));
        }

        private static T Configured<T>(object owner, string field) where T : class
        {
            var member = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(member, Is.Not.Null);
            var result = member.GetValue(owner) as T;
            Assert.That(result, Is.Not.Null, "Read the actual scene's configured dependency " + field);
            return result;
        }

        private static T One<T>() where T : Component
        {
            var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(found, Has.Length.EqualTo(1), typeof(T).Name);
            return found[0];
        }

        private static bool Ready() => SceneManager.GetActiveScene().path == ScenePath && RunSessionManager.Instance != null &&
            RunSessionManager.Instance.Phase == RunPhase.FirstSweep && RunSessionManager.Instance.Tick >= 3 && PlayerRegistry.Items.Count == 1;

        private static IEnumerator Until(Func<bool> condition, string failure, float seconds = 15f)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            for (int frames = 0; frames < 10000 && !condition() && Time.realtimeSinceStartupAsDouble < deadline; frames++) yield return null;
            Assert.That(condition(), Is.True, failure);
        }

        private void RestoreBackground()
        {
            if (!_restoreBackground) return;
            Application.runInBackground = _previousBackground;
            _restoreBackground = false;
        }

        [UnityTearDown]
        public IEnumerator RestoreEditor()
        {
            RestoreBackground();
            _path = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
