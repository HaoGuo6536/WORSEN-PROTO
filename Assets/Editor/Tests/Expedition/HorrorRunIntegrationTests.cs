// ============================================================================
// HorrorRunIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the saved HorrorRun scene through generated combat floors, its first
//   safe shop, death and restart using the actual Session and Domain managers.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Expedition scene integration.
// KEY RESPONSIBILITIES:
//   - Verify native geometry/navigation admission and growing in-place floors.
//   - Exercise the two-combat shop cadence, unique choices and per-visit consumables.
//   - Verify a central four-route exit hub and distinct ready animated hunter models.
//   - Verify canonical input routing and immediate capture-boundary HUD reset.
// DEPENDENCIES:
//   - Core; Domain Procedural/Level/Floor/Player/Hunter; Session Run/Progression/
//     Expedition; Presentation Input/HUD; NUnit and Unity Test Framework.
// USAGE NOTES:
//   Requires the coordinator's Unity lease and generated HorrorRun build entry.
//   Enter/ExitPlayMode use the Test Framework's isolated bootstrap and restore
//   the original editor scene setup; this fixture saves no scene or asset.
//   Neutral input is supplied after the ordinary input router on BeforeTick.
//   Choices, pickup/exit contact and damage enter public manager boundaries;
//   this does not claim physical locomotion, device input or subjective quality.
//   Reflection only observes input/HUD presentation state, never changes it.
//   Retains Unity's SceneHandle type for identity assertions without int coercion.
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
using Worsen.Domain.Procedural;
using Worsen.Orchestrator;
using Worsen.Presentation.HUD;
using Worsen.Presentation.Input;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Expedition
{
    public sealed class HorrorRunIntegrationTests
    {
        private const string ScenePath = "Assets/Scenes/HorrorRun.unity";
        private bool _restoreBackground;
        private bool _previousBackground;

        [UnityTest]
        public IEnumerator SavedHorrorRunGeneratesGrowingFloorsShopAndRestartThroughGameplayCallbacks()
        {
            yield return new EnterPlayMode();
            _previousBackground = Application.runInBackground;
            _restoreBackground = true;
            Application.runInBackground = true;
            try { yield return ExerciseScene(); }
            finally { RestoreBackground(); }
        }

        private static IEnumerator ExerciseScene()
        {
            Assert.That(RunSessionManager.Instance, Is.Null, "Use the Test Framework isolated bootstrap scene.");
            Assert.That(ProgressionSessionManager.Instance, Is.Null);
            Assert.That(ExpeditionSessionManager.Instance, Is.Null);
            Assert.That(InputManager.Instance, Is.Null);
            yield return LoadToChoices();
            var input = One<InputManager>();
            var run = One<RunSessionManager>();
            var progression = One<ProgressionSessionManager>();
            var expedition = One<ExpeditionSessionManager>();
            AssertInputGate(input, false);

            // A second scene has duplicate authored services, but must bind the
            // previously initialized input router rather than a destroyed duplicate.
            yield return LoadToChoices();
            Assert.That(One<InputManager>(), Is.SameAs(input));
            Assert.That(One<RunSessionManager>(), Is.SameAs(run));
            Assert.That(One<ProgressionSessionManager>(), Is.SameAs(progression));
            Assert.That(One<ExpeditionSessionManager>(), Is.SameAs(expedition));
            Assert.That(input.GetComponent<InputOrchestrator>(), Is.Not.Null);
            AssertInputGate(input, false);

            var procedural = One<ProceduralManager>();
            var floor = One<FloorManager>();
            var hud = One<HUDManager>();
            SceneHandle sceneHandle = SceneManager.GetActiveScene().handle;
            var captures = new List<int>();
            var summaries = new List<RunSummary>();
            Action neutralInput = () => run.ReceiveInput(default);
            Action<RunCaptureMetadata> captured = _ =>
            {
                captures.Add(progression.Snapshot.GenerationId);
                var state = Observe<HUDDriverState>(hud.GetComponent<HUDDriver>(), "_state");
                Assert.That(state.ChaseMode, Is.False, "CaptureStarted must clear previous floor chase suppression synchronously.");
                Assert.That(state.ExtraOpacity, Is.EqualTo(1f), "New floor HUD must not wait for its restore fade.");
            };
            run.BeforeTick += neutralInput;
            run.CaptureStarted += captured;
            run.RunEnded += summaries.Add;
            string firstManifest = null;
            int previousRooms = 0;
            try
            {
                for (int round = 1; round <= 2; round++)
                {
                    hud.SetChaseMode(true);
                    ChooseCombatFloor(progression, input, round);
                    yield return AwaitFloor(progression, expedition, run, ProgressionPhase.Exploring);
                    AssertFloor(progression, expedition, run, procedural, floor, sceneHandle, false, captures);
                    Assert.That(procedural.Graph.Rooms.Count, Is.GreaterThan(previousRooms));
                    previousRooms = procedural.Graph.Rooms.Count;
                    if (round == 1) firstManifest = procedural.LayoutManifest;
                    var player = One<PlayerManager>();
                    if (round == 1)
                    {
                        player.ApplyHit(40f, player.transform.position + Vector3.forward);
                        Assert.That(progression.Snapshot.Health, Is.EqualTo(60f));
                    }
                    else Assert.That(player.ReadOnlyState.Health, Is.EqualTo(60f), "Health must persist into the generated player.");

                    int beforeWallet = progression.Snapshot.Wallet;
                    var anchors = floor.ReadOnlyState.ActiveCakeAnchors.ToArray();
                    foreach (var anchor in anchors) floor.Collect(player.Id, anchor.Id, PickupKind.Cake);
                    Assert.That(floor.ReadOnlyState.CakeCount, Is.EqualTo(anchors.Length));
                    Assert.That(floor.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Open));
                    foreach (var anchor in anchors.Take(3)) floor.Collect(player.Id, anchor.Id, PickupKind.GoldenCake);
                    Assert.That(progression.Snapshot.Wallet, Is.EqualTo(beforeWallet + 3));
                    Assert.That(floor.ReadOnlyState.GoldenCakeCount, Is.EqualTo(3));
                    hud.SetChaseMode(true); // Also covers automatic generation of the shop after two combat floors.
                    floor.ContactExit(player.Id);
                    yield return Until(() => progression.Snapshot.Round == round + 1,
                        "Floor contact did not reach RunEnded and advance progression.");
                    Assert.That(summaries.Count, Is.EqualTo(round));
                    Assert.That(summaries.Last().EndReason, Is.EqualTo(RunEndReason.Escaped));
                    Assert.That(summaries.Last().Scene, Is.EqualTo(SceneKey.HorrorRun));
                    AssertInputGate(input, false);
                }

                yield return AwaitFloor(progression, expedition, run, ProgressionPhase.Shop);
                AssertFloor(progression, expedition, run, procedural, floor, sceneHandle, true, captures);
                Assert.That(procedural.Graph.Rooms.Count, Is.GreaterThan(previousRooms));
                previousRooms = procedural.Graph.Rooms.Count;
                Assert.That(progression.Snapshot.Round, Is.EqualTo(3));
                Assert.That(progression.Snapshot.Wallet, Is.EqualTo(6));
                var shopPlayer = One<PlayerManager>();
                int shopGeneration = expedition.GenerationId;
                Assert.That(progression.Snapshot.Offers.Select(offer => offer.Id), Is.EquivalentTo(new[] {
                    "shuttered-lens", "felt-soles", "climber-wraps", "pilgrim-chalk", "field-dressing", "wax-ward" }));
                var medkit = progression.Snapshot.Offers.Single(offer => offer.Id == "field-dressing");
                Assert.That(medkit.Repeatable, Is.True);
                Assert.That(medkit.StockRemaining, Is.EqualTo(1));
                Assert.That(medkit.CanAfford, Is.True);
                Assert.That(progression.Purchase(medkit.Id, progression.Snapshot.Revision), Is.True);
                Assert.That(progression.Snapshot.Wallet, Is.EqualTo(6 - medkit.Price));
                Assert.That(progression.Snapshot.Health, Is.EqualTo(95f));
                Assert.That(shopPlayer.ReadOnlyState.Health, Is.EqualTo(95f), "Shop healing must reach the live player immediately.");
                Assert.That(expedition.GenerationId, Is.EqualTo(shopGeneration), "Purchasing must not regenerate the shop.");
                Assert.That(progression.Purchase(medkit.Id, progression.Snapshot.Revision), Is.False);
                Assert.That(progression.Snapshot.Wallet, Is.EqualTo(6 - medkit.Price));
                var soldDressing = progression.Snapshot.Offers.Single(offer => offer.Id == medkit.Id);
                Assert.That(soldDressing.StockRemaining, Is.Zero);
                Assert.That(soldDressing.UnavailableReason, Does.Contain("Sold out"));
                var soles = progression.Snapshot.Offers.Single(offer => offer.Id == "felt-soles");
                Assert.That(soles.Repeatable, Is.False);
                Assert.That(progression.Purchase(soles.Id, progression.Snapshot.Revision), Is.True);
                int equippedWallet = progression.Snapshot.Wallet;
                Assert.That(progression.Snapshot.Effects.Traits.HasFlag(ProgressionTraits.FeltSoles), Is.True);
                Assert.That(progression.Purchase(soles.Id, progression.Snapshot.Revision), Is.False);
                Assert.That(progression.Snapshot.Wallet, Is.EqualTo(equippedWallet));
                AssertInputGate(input, false);
                Assert.That(progression.ContinueShop(progression.Snapshot.Revision), Is.True);

                hud.SetChaseMode(true);
                ChooseCombatFloor(progression, input, 4);
                yield return AwaitFloor(progression, expedition, run, ProgressionPhase.Exploring);
                AssertFloor(progression, expedition, run, procedural, floor, sceneHandle, false, captures);
                Assert.That(procedural.Graph.Rooms.Count, Is.GreaterThan(previousRooms));
                var doomedPlayer = One<PlayerManager>();
                Assert.That(doomedPlayer.ReadOnlyState.Health, Is.EqualTo(95f));
                Assert.That(progression.Snapshot.ThreatCount, Is.EqualTo(3));
                Assert.That(progression.Snapshot.CurseCount, Is.EqualTo(3));
                Assert.That(progression.Snapshot.Effects.Traits.HasFlag(ProgressionTraits.FeltSoles), Is.True);
                int previousGeneration = expedition.GenerationId;
                doomedPlayer.ApplyHit(doomedPlayer.ReadOnlyState.MaxHealth + 1f, doomedPlayer.transform.position + Vector3.forward);
                // Health can end progression synchronously; Run finalizes its summary on its next fixed tick.
                yield return Until(() => progression.Snapshot.Phase == ProgressionPhase.Ended && summaries.Count == 3,
                    "Player death did not reach both the expedition end and committed RunEnded summary.");
                Assert.That(summaries.Count, Is.EqualTo(3));
                Assert.That(summaries.Last().EndReason, Is.EqualTo(RunEndReason.Died));
                Assert.That(run.Phase, Is.EqualTo(RunPhase.Ended));
                Assert.That(progression.Snapshot.Health, Is.Zero);
                AssertInputGate(input, false);

                int endRevision = progression.Snapshot.Revision;
                Assert.That(progression.RestartRun(endRevision), Is.True);
                Assert.That(progression.RestartRun(endRevision), Is.False, "Old result actions must not restart a second time.");
                Assert.That(progression.Snapshot.Wallet, Is.Zero);
                Assert.That(progression.Snapshot.ThreatCount, Is.Zero);
                Assert.That(progression.Snapshot.CurseCount, Is.Zero);
                Assert.That(progression.Snapshot.Health, Is.EqualTo(100f));
                hud.SetChaseMode(true);
                ChooseCombatFloor(progression, input, 1);
                yield return AwaitFloor(progression, expedition, run, ProgressionPhase.Exploring);
                AssertFloor(progression, expedition, run, procedural, floor, sceneHandle, false, captures);
                Assert.That(expedition.GenerationId, Is.GreaterThan(previousGeneration));
                Assert.That(procedural.LayoutManifest, Is.EqualTo(firstManifest), "Restart must regenerate the same first floor seed.");
                Assert.That(doomedPlayer == null, Is.True, "Old actors must be destroyed before the replacement map is admitted.");
                Assert.That(One<PlayerManager>().ReadOnlyState.Health, Is.EqualTo(100f));
                Assert.That(captures.Count, Is.EqualTo(5), "One capture per admitted floor: 1, 2, shop 3, 4 and restarted 1.");
                Assert.That(captures.Distinct().Count(), Is.EqualTo(5));
                Assert.That(summaries.Count, Is.EqualTo(3));
            }
            finally
            {
                if (run != null)
                {
                    run.BeforeTick -= neutralInput;
                    run.CaptureStarted -= captured;
                    run.RunEnded -= summaries.Add;
                }
            }
        }

        private static void ChooseCombatFloor(ProgressionSessionManager progression, InputManager input, int round)
        {
            Assert.That(progression.Snapshot.Round, Is.EqualTo(round));
            Assert.That(progression.Snapshot.Phase, Is.EqualTo(ProgressionPhase.ChooseThreat));
            AssertInputGate(input, false);
            string[] roster = { "watcher", "rusher", "lurker", "hexer", "thorncaller" };
            var retainedThreats = progression.Snapshot.Effects.ActiveThreatIds.ToArray();
            string[] eligible = roster.Except(retainedThreats).ToArray();
            string[] offered = progression.Snapshot.Choices.Select(choice => choice.Id).ToArray();
            Assert.That(offered.Length, Is.EqualTo(Math.Min(3, eligible.Length)), "Offer three hunters while enough remain.");
            Assert.That(offered.Distinct().Count(), Is.EqualTo(offered.Length));
            Assert.That(offered.All(id => eligible.Contains(id)), Is.True, "Only unselected members of the full five-hunter roster are eligible.");
            string nextThreat = progression.Snapshot.Choices.First().Id;
            if (retainedThreats.Length > 0)
            {
                Assert.That(progression.ChooseThreat(retainedThreats[0], progression.Snapshot.Revision), Is.False);
                Assert.That(progression.Snapshot.Phase, Is.EqualTo(ProgressionPhase.ChooseThreat));
            }
            Assert.That(progression.ChooseThreat(nextThreat, progression.Snapshot.Revision), Is.True);
            Assert.That(progression.Snapshot.Effects.ActiveThreatIds, Is.EquivalentTo(retainedThreats.Concat(new[] { nextThreat })));
            Assert.That(progression.Snapshot.Phase, Is.EqualTo(ProgressionPhase.ChooseCurse));
            AssertInputGate(input, false);
            var retainedCurses = progression.Snapshot.Retained.Where(item => item.Kind == ProgressionChoiceKind.Curse).ToArray();
            Assert.That(progression.Snapshot.Choices.Count, Is.InRange(1, 3));
            Assert.That(progression.Snapshot.Choices.All(choice => choice.SelectedCount == 0
                && !retainedCurses.Any(item => item.Id == choice.Id)), Is.True);
            string nextCurse = progression.Snapshot.Choices.First().Id;
            if (retainedCurses.Length > 0)
                Assert.That(progression.ChooseCurse(retainedCurses[0].Id, progression.Snapshot.Revision), Is.False);
            Assert.That(progression.ChooseCurse(nextCurse, progression.Snapshot.Revision), Is.True);
            Assert.That(progression.Snapshot.Retained.Where(item => item.Kind == ProgressionChoiceKind.Curse)
                .All(item => item.Count == 1), Is.True, "Curses remain unique for the expedition.");
            Assert.That(progression.Snapshot.Phase, Is.EqualTo(ProgressionPhase.Generating));
            AssertInputGate(input, false);
        }

        private static void AssertFloor(ProgressionSessionManager progression, ExpeditionSessionManager expedition,
            RunSessionManager run, ProceduralManager procedural, FloorManager floor, SceneHandle sceneHandle,
            bool shop, List<int> captures)
        {
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneHandle), "Floors replace geometry in the same scene.");
            Assert.That(One<ProceduralManager>(), Is.SameAs(procedural));
            Assert.That(One<FloorManager>(), Is.SameAs(floor));
            Assert.That(procedural.IsReady, Is.True);
            Assert.That(procedural.Graph, Is.Not.Null);
            Assert.That(One<LevelManager>().ReadOnlyState.IsReady, Is.True);
            Assert.That(One<LevelManager>().ReadOnlyState.Graph.Rooms.Count, Is.EqualTo(procedural.Graph.Rooms.Count));
            Assert.That(procedural.Graph.Rooms.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(procedural.Graph.Anchors.Count, Is.GreaterThanOrEqualTo(procedural.Graph.Rooms.Count * 10));
            Assert.That(procedural.GetComponentsInChildren<Collider>().Length, Is.GreaterThan(procedural.Graph.Rooms.Count));
            Assert.That(expedition.GenerationId, Is.EqualTo(progression.Snapshot.GenerationId));
            Assert.That(expedition.AssemblyPhase, Is.EqualTo(ExpeditionAssemblyPhase.Ready));
            Assert.That(PlayerRegistry.Items.Count, Is.EqualTo(1));
            var player = One<PlayerManager>();
            Assert.That(player.Id, Is.EqualTo(expedition.ActivePlayerId));
            Assert.That(player.ReadOnlyState.IsAlive, Is.True);
            Assert.That(HunterRegistry.Items.Count, Is.EqualTo(progression.Snapshot.Effects.ActiveThreatBudget));
            Assert.That(expedition.ActiveHunterCount, Is.EqualTo(HunterRegistry.Items.Count));
            Assert.That(run.Scene, Is.EqualTo(SceneKey.HorrorRun));
            Assert.That(run.Tick, Is.GreaterThan(0), "Native fixed ticks require the real SceneReady route.");
            Assert.That(captures.Count(id => id == expedition.GenerationId), Is.EqualTo(1));
            AssertInputGate(One<InputManager>(), !shop);
            Assert.That(NavMesh.SamplePosition(procedural.PlayerSpawnPosition, out var start, 2f, NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.SamplePosition(procedural.Graph.ExitPosition, out var exit, 2f, NavMesh.AllAreas), Is.True);
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(start.position, exit.position, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "Generated spawn and exit must have native walking connectivity.");
            AssertCentralExitHub(procedural.Graph);
            if (shop)
            {
                Assert.That(HunterRegistry.Items, Is.Empty);
                Assert.That(floor.ReadOnlyState, Is.Null, "Safe shop must not run collection or collapse.");
                Assert.That(floor.GetComponentsInChildren<CakePickup>(), Is.Empty);
            }
            else
            {
                Assert.That(HunterRegistry.Items.Count, Is.GreaterThan(0));
                AssertActiveRoster(progression.Snapshot.Effects);
                Assert.That(floor.ReadOnlyState.IsReady, Is.True);
                Assert.That(floor.ReadOnlyState.RequiredCakeCount, Is.EqualTo(procedural.Graph.Anchors.Count));
                Assert.That(floor.ReadOnlyState.CakeCount, Is.Zero);
                Assert.That(floor.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Locked));
            }
        }

        private static void AssertCentralExitHub(LevelGraph graph)
        {
            var hub = graph.Rooms.Single(room => room.Id == graph.ExitRoomId);
            Assert.That(new Vector2(graph.ExitPosition.x, graph.ExitPosition.z),
                Is.EqualTo(new Vector2(hub.Center.x, hub.Center.z)), "Exit must sit in the hub centre.");
            var connections = graph.Edges.Where(edge => edge.FromRoomId == hub.Id || edge.ToRoomId == hub.Id).ToArray();
            Assert.That(connections.Length, Is.EqualTo(4), "The central exit hub needs four walking entrances.");
            Assert.That(connections.All(edge => edge.Bidirectional && edge.Access == TraversalAccess.All), Is.True);
            var directions = new HashSet<Vector2Int>();
            Assert.That(NavMesh.SamplePosition(graph.ExitPosition, out var destination, 1f, NavMesh.AllAreas), Is.True);
            foreach (var edge in connections)
            {
                int neighborId = edge.FromRoomId == hub.Id ? edge.ToRoomId : edge.FromRoomId;
                var neighbor = graph.Rooms.Single(room => room.Id == neighborId);
                var delta = neighbor.Center - hub.Center;
                directions.Add(Mathf.Abs(delta.x) > Mathf.Abs(delta.z)
                    ? new Vector2Int(delta.x > 0f ? 1 : -1, 0) : new Vector2Int(0, delta.z > 0f ? 1 : -1));
                var ground = new Vector3(neighbor.Center.x, graph.ExitPosition.y, neighbor.Center.z);
                Assert.That(NavMesh.SamplePosition(ground, out var origin, 2f, NavMesh.AllAreas), Is.True);
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(origin.position, destination.position, NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "Every hub neighbor needs native walking access to the exit.");
            }
            Assert.That(directions.Count, Is.EqualTo(4), "Entrances must reach all four sides of the hub.");
        }

        private static void AssertActiveRoster(ProgressionEffects effects)
        {
            Assert.That(effects.ActiveThreatIds.Count, Is.EqualTo(effects.ActiveThreatBudget));
            Assert.That(effects.ActiveThreatIds.Distinct().Count(), Is.EqualTo(effects.ActiveThreatBudget));
            Assert.That(HunterRegistry.Items.Select(hunter => hunter.Id).Distinct().Count(), Is.EqualTo(effects.ActiveThreatBudget));
            var modelSignatures = new HashSet<string>();
            foreach (var hunter in HunterRegistry.Items)
            {
                Assert.That(hunter.ReadOnlyState.IsActive, Is.True);
                Assert.That(hunter.ReadOnlyState.Id, Is.EqualTo(hunter.Id));
                var animation = hunter.GetComponentInChildren<HunterAnimationDriver>(true);
                Assert.That(animation, Is.Not.Null);
                Assert.That(animation.IsReady, Is.True, "The imported animation graph must be running on every admitted hunter.");
                var skins = hunter.GetComponentsInChildren<SkinnedMeshRenderer>();
                Assert.That(skins.Any(skin => skin.sharedMesh != null), Is.True, "A capsule without its imported creature is not an admitted model.");
                modelSignatures.Add(string.Join("|", skins.Where(skin => skin.sharedMesh != null)
                    .Select(skin => skin.sharedMesh.name).OrderBy(name => name)));
            }
            Assert.That(modelSignatures.Count, Is.EqualTo(effects.ActiveThreatBudget),
                "Selecting distinct hunters must not spawn repeated copies of the default creature.");
        }

        private static IEnumerator LoadToChoices()
        {
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "Run Worsen/Scenes/3 — Build HorrorRun before this fixture.");
            yield return Until(() => load.isDone && ProgressionSessionManager.Instance != null &&
                ProgressionSessionManager.Instance.Snapshot.Phase == ProgressionPhase.ChooseThreat &&
                Object.FindObjectsByType<HorrorRunSceneRoot>(FindObjectsSortMode.None).Length == 1,
                "Saved HorrorRun did not assemble its choice screen.");
            yield return null; // Allow duplicate persistent service Destroy calls to finish.
        }

        private static IEnumerator AwaitFloor(ProgressionSessionManager progression, ExpeditionSessionManager expedition,
            RunSessionManager run, ProgressionPhase phase)
        {
            yield return Until(() =>
            {
                Assert.That(progression.Snapshot.Phase, Is.Not.EqualTo(ProgressionPhase.GenerationFailed), expedition.LastError);
                return progression.Snapshot.Phase == phase && expedition.AssemblyPhase == ExpeditionAssemblyPhase.Ready && run.Tick > 0;
            }, "Native floor did not become ready and tick: " + phase + ".");
        }

        private static IEnumerator Until(Func<bool> condition, string failure)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 8d;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), failure);
                yield return null;
            }
        }

        private static void AssertInputGate(InputManager input, bool expected)
        {
            Assert.That(input, Is.SameAs(InputManager.Instance));
            Assert.That(Observe<InputDriverState>(input.GetComponent<PlayerInputDriver>(), "_state").InputEnabled,
                Is.EqualTo(expected), "Only admitted combat floors may enable movement input.");
        }

        private static T Observe<T>(object owner, string field)
        {
            Assert.That(owner, Is.Not.Null);
            var member = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(member, Is.Not.Null, "Presentation observation field changed: " + field);
            return (T)member.GetValue(owner);
        }

        private static T One<T>() where T : Object
        {
            var matches = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(matches.Length, Is.EqualTo(1), typeof(T).Name + " must have one active instance.");
            return matches[0];
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
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
