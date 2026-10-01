// ============================================================================
// HorrorRunIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the saved HorrorRun scene through generated combat floors, its first
//   safe shop, death and restart using the actual Session and Domain managers.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Expedition scene integration.
// KEY RESPONSIBILITIES:
//   - Wait for the title and activate its Start interaction before expecting choices.
//   - Reject inter-floor health carry-over, including shop entry and return to combat.
//   - Verify native geometry/navigation admission, growing floors and canonical input/HUD reset.
//   - Exercise independent cadence, catalogue pedestals and held inventory across floors.
//   - Verify clear hub doorways with blocker diagnostics and distinct ready hunter models.
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
//   Reflection observes presentation and procedural diagnostics, never changes live state.
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
using UnityEngine.UIElements;
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
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
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
                    // PLAN-026 §2 growth; ProceduralConfig budgets optional pockets separately.
                    Assert.That(procedural.Graph.Rooms.Count(room => !room.Pocket), Is.GreaterThan(previousRooms),
                        "Connected rooms must grow on combat round " + round + ".");
                    previousRooms = procedural.Graph.Rooms.Count(room => !room.Pocket);
                    if (round == 1) firstManifest = procedural.LayoutManifest;
                    var player = One<PlayerManager>();
                    if (round == 1)
                    {
                        player.ApplyHit(40f, player.transform.position + Vector3.forward);
                        Assert.That(progression.Snapshot.Health, Is.EqualTo(60f));
                    }
                    else Assert.That(player.ReadOnlyState.Health, Is.EqualTo(player.ReadOnlyState.MaxHealth),
                        "Each generated player starts at its effective maximum, not the prior floor's health.");

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
                Assert.That(procedural.Graph.Rooms.Count(room => !room.Pocket), Is.GreaterThan(previousRooms),
                    "Shop round 3 grows the connected budget even when round 2 had optional pockets.");
                previousRooms = procedural.Graph.Rooms.Count(room => !room.Pocket);
                Assert.That(progression.Snapshot.Round, Is.EqualTo(3));
                Assert.That(progression.Snapshot.Wallet, Is.EqualTo(6));
                var shopPlayer = One<PlayerManager>();
                int shopGeneration = expedition.GenerationId;
                Assert.That(progression.Snapshot.Offers.Count, Is.EqualTo(4));
                Assert.That(progression.Snapshot.Offers.Select(offer => offer.Id), Does.Not.Contain("field-dressing"));
                Assert.That(progression.Purchase("field-dressing", progression.Snapshot.Revision), Is.False);
                Assert.That(progression.Snapshot.Wallet, Is.EqualTo(6));
                Assert.That(progression.Snapshot.Health, Is.EqualTo(progression.Snapshot.MaxHealth));
                Assert.That(shopPlayer.ReadOnlyState.Health, Is.EqualTo(shopPlayer.ReadOnlyState.MaxHealth));
                var bought = progression.Snapshot.Offers.First(offer => offer.CanAfford && offer.Kind == EffectKind.Consumable);
                Assert.That(progression.Purchase(bought.Id, progression.Snapshot.Revision), Is.True);
                Assert.That(progression.Snapshot.Wallet, Is.EqualTo(6 - bought.Price));
                Assert.That(progression.Purchase(bought.Id, progression.Snapshot.Revision), Is.False);
                Assert.That(expedition.GenerationId, Is.EqualTo(shopGeneration), "Purchasing must not regenerate the shop.");
                Assert.That(progression.Snapshot.Inventory.Count, Is.EqualTo(3));
                Assert.That(progression.Snapshot.Inventory[0].Id, Is.EqualTo(bought.Id));
                AssertInputGate(input, false);
                Assert.That(progression.ContinueShop(progression.Snapshot.Revision), Is.True);

                hud.SetChaseMode(true);
                ChooseCombatFloor(progression, input, 4);
                yield return AwaitFloor(progression, expedition, run, ProgressionPhase.Exploring);
                AssertFloor(progression, expedition, run, procedural, floor, sceneHandle, false, captures);
                Assert.That(procedural.Graph.Rooms.Count(room => !room.Pocket), Is.GreaterThan(previousRooms),
                    "Connected rooms must grow when returning from the shop on round 4.");
                var doomedPlayer = One<PlayerManager>();
                Assert.That(doomedPlayer.ReadOnlyState.Health, Is.EqualTo(doomedPlayer.ReadOnlyState.MaxHealth));
                Assert.That(progression.Snapshot.ThreatCount, Is.EqualTo(2));
                Assert.That(progression.Snapshot.CurseCount, Is.EqualTo(2));
                Assert.That(progression.EffectsSnapshot.ActiveEffects.Has(new EffectId(bought.Id)), Is.True);
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
            if (round == 2)
            {
                Assert.That(progression.Snapshot.Phase, Is.EqualTo(ProgressionPhase.Generating).Or.EqualTo(ProgressionPhase.Exploring));
                return;
            }
            Assert.That(progression.Snapshot.Phase, Is.EqualTo(ProgressionPhase.ChooseThreat));
            AssertInputGate(input, false);
            string[] roster = { "echo", "weaver", "ticking", "ram", "mannequin", "mimic", "blinder", "herald", "skip", "stare" };
            var retainedThreats = progression.Snapshot.Effects.ActiveThreatIds.ToArray();
            string[] eligible = roster.Where(id => ProgressionRosterUtility.Admits(id, round)).ToArray();
            string[] offered = progression.Snapshot.Choices.Select(choice => choice.Id).ToArray();
            Assert.That(offered.Length, Is.EqualTo(Math.Min(3, eligible.Length)), "Offer three hunters while enough remain.");
            Assert.That(offered.Distinct().Count(), Is.EqualTo(offered.Length));
            Assert.That(offered.All(id => eligible.Contains(id)), Is.True, "Only round-admitted hunters may be offered.");
            Assert.That(retainedThreats.All(id => eligible.Contains(id)), Is.True, "Previously selected hunters remain eligible.");
            // Deliberately select distinct bodies for this fixture's distinct-model assertions.
            string nextThreat = progression.Snapshot.Choices.First(choice => !retainedThreats.Contains(choice.Id)).Id;
            Assert.That(progression.ChooseThreat(nextThreat, progression.Snapshot.Revision), Is.True);
            Assert.That(progression.Snapshot.Effects.ActiveThreatIds, Is.EquivalentTo(retainedThreats.Concat(new[] { nextThreat })));
            Assert.That(progression.Snapshot.Phase, Is.EqualTo(ProgressionPhase.ChooseCurse));
            AssertInputGate(input, false);
            var retainedCurses = progression.Snapshot.Retained.Where(item => item.Kind == ProgressionChoiceKind.Curse).ToArray();
            Assert.That(progression.Snapshot.Choices.Count, Is.InRange(1, 3));
            var catalogue = Observe<ProgressionConfig>(progression, "config").EffectCatalogue;
            Assert.That(catalogue, Is.Not.Null);
            foreach (var choice in progression.Snapshot.Choices)
            {
                var entry = EffectCatalogueUtility.Find(catalogue, choice.Id);
                Assert.That(entry, Is.Not.Null, choice.Id);
                Assert.That(choice.SelectedCount, Is.EqualTo(retainedCurses.Where(item => item.Id == choice.Id).Sum(item => item.Count)), choice.Id);
                Assert.That(EffectCatalogueUtility.Eligible(entry, round, progression.EffectsSnapshot.ActiveEffects), Is.True, choice.Id);
            }
            // Keep this geometry/factory fixture independent of new Player health curse tuning.
            string nextCurse = progression.Snapshot.Choices.First(choice => choice.Id.StartsWith(nextThreat + "-", StringComparison.Ordinal)).Id;
            var capped = retainedCurses.FirstOrDefault(item => item.Count >= EffectCatalogueUtility.Find(catalogue, item.Id).StackCap);
            if (!string.IsNullOrEmpty(capped.Id))
                Assert.That(progression.ChooseCurse(capped.Id, progression.Snapshot.Revision), Is.False);
            Assert.That(progression.ChooseCurse(nextCurse, progression.Snapshot.Revision), Is.True);
            Assert.That(progression.Snapshot.Retained.Where(item => item.Kind == ProgressionChoiceKind.Curse)
                .All(item => item.Count == 1), Is.True, "This fixture selects one distinct curse per newly selected hunter.");
            Assert.That(progression.Snapshot.Retained.Where(item => item.Kind == ProgressionChoiceKind.Curse).Select(item => item.Id),
                Is.EquivalentTo(retainedCurses.Select(item => item.Id).Concat(new[] { nextCurse })));
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
            // SPEC-004 §2.5 / PLAN-026 change 2: a few typed candidates per room (default 3-5), not 10-cake lines.
            Assert.That(procedural.Graph.Anchors.Count, Is.GreaterThan(0));
            Assert.That(procedural.Graph.Anchors.Count, Is.LessThanOrEqualTo(procedural.Graph.Rooms.Count * 5));
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
            AssertCentralExitHub(procedural);
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
                // Room density places a subset of the typed candidates; a required subset gates the exit.
                int placed = floor.GetComponentsInChildren<CakePickup>().Length;
                Assert.That(floor.ReadOnlyState.RequiredCakeCount, Is.GreaterThanOrEqualTo(1).And.LessThanOrEqualTo(placed));
                Assert.That(placed, Is.LessThanOrEqualTo(procedural.Graph.Anchors.Count));
                Assert.That(floor.ReadOnlyState.CakeCount, Is.Zero);
                Assert.That(floor.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Locked));
            }
        }

        private static void AssertCentralExitHub(ProceduralManager procedural)
        {
            var graph = procedural.Graph;
            var playerSpawn = procedural.PlayerSpawnPosition;
            var doors = procedural.Doors;
            var hub = graph.Rooms.Single(room => room.Id == graph.ExitRoomId);
            // SPEC-004 §2.2 "Reveals, not spawns": player starts in the central
            // exit ROOM. PLAN-026 §2's four-door hub is the starting implementation,
            // not an acceptance requirement; §1/§5 preserve walking reachability.
            // The owner sets a minimum of two entrances, not four cardinal sides.
            Assert.That(hub.Pocket, Is.False, "The exit hub cannot be an optional pocket.");
            Assert.That(hub.ContainsXZ(graph.ExitPosition), Is.True, "Exit must be on the hub's actual footprint.");
            Assert.That(hub.ContainsXZ(playerSpawn), Is.True, "Player must start in the central exit room.");
            var connections = graph.Edges.Where(edge => edge.FromRoomId == hub.Id || edge.ToRoomId == hub.Id).ToArray();
            Assert.That(connections.Length, Is.GreaterThanOrEqualTo(2), "The exit hub needs at least two walking entrances.");
            Assert.That(connections.All(edge => edge.Bidirectional && edge.Access == TraversalAccess.All), Is.True,
                "Every hub edge must allow both actors in both directions.");
            const int walkingArea = 1; // Walkable only: no player/partition-ignoring shortcuts.
            Assert.That(NavMesh.SamplePosition(graph.ExitPosition, out var destination, 1f, walkingArea), Is.True,
                "Exit position must sample onto ordinary walkable NavMesh.");
            Assert.That(hub.ContainsXZ(destination.position), Is.True, "Exit sample must stay inside the hub.");
            foreach (var edge in connections)
            {
                int neighborId = edge.FromRoomId == hub.Id ? edge.ToRoomId : edge.FromRoomId;
                var neighbor = graph.Rooms.Single(room => room.Id == neighborId);
                // Never enlarge the radius around an empty AABB centre: that can
                // silently sample a different room across a gap. Probe actual cells.
                var candidates = graph.Anchors.Where(a => a.RoomId == neighborId).Select(a => a.Position)
                    .Concat(neighbor.Cells.SelectMany(cell =>
                        Enumerable.Range(0, Mathf.Max(1, Mathf.FloorToInt(cell.size.x / 2f))).SelectMany(x =>
                        Enumerable.Range(0, Mathf.Max(1, Mathf.FloorToInt(cell.size.z / 2f))).Select(z =>
                            new Vector3(cell.min.x + 1f + x * 2f, cell.min.y, cell.min.z + 1f + z * 2f)))))
                    .OrderBy(p => (p - neighbor.Center).sqrMagnitude);
                bool sampled = false;
                NavMeshHit origin = default;
                foreach (var point in candidates)
                    if (NavMesh.SamplePosition(point, out origin, .5f, walkingArea) && neighbor.ContainsXZ(origin.position))
                    { sampled = true; break; }
                Assert.That(sampled, Is.True, "No supported walking point in hub neighbour " + neighborId);
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(origin.position, destination.position, walkingArea, path), Is.True,
                    "Could not calculate hub neighbour " + neighborId + " path to exit.");
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete),
                    "Hub neighbour " + neighborId + " needs native walking access to the exit.");
                var portals = doors.Where(d => (d.FromRoomId == edge.FromRoomId && d.ToRoomId == edge.ToRoomId) ||
                    (d.ToRoomId == edge.FromRoomId && d.FromRoomId == edge.ToRoomId)).ToArray();
                Assert.That(portals, Is.Not.Empty, "Hub edge must have a physical doorway.");
                foreach (var door in portals)
                {
                    Assert.That(door.IsOptional, Is.False, "Hub doorway cannot require traversal.");
                    var across = door.AlongX ? Vector3.forward : Vector3.right;
                    Assert.That(NavMesh.SamplePosition(door.Center - across, out var sideA, .5f, walkingArea), Is.True,
                        "Hub door to " + neighborId + " lacks its first walking approach.");
                    Assert.That(NavMesh.SamplePosition(door.Center + across, out var sideB, .5f, walkingArea), Is.True,
                        "Hub door to " + neighborId + " lacks its second walking approach.");
                    Assert.That((hub.ContainsXZ(sideA.position) && neighbor.ContainsXZ(sideB.position)) ||
                        (neighbor.ContainsXZ(sideA.position) && hub.ContainsXZ(sideB.position)), Is.True,
                        "Door samples must lie on opposite sides in the two connected rooms.");
                    if (NavMesh.Raycast(sideA.position, sideB.position, out var hit, walkingArea))
                        Assert.Fail("Hub doorway to " + neighborId + " is blocked; an alternate path is not sufficient. " +
                            DoorwayDiagnostics(procedural, door, sideA.position, sideB.position, hit.position));
                }
            }
        }

        private static string DoorwayDiagnostics(ProceduralManager procedural, ProceduralDoorPlan door,
            Vector3 sideA, Vector3 sideB, Vector3 hit)
        {
            var layout = Observe<ProceduralBehaviorState>(procedural, "_state").Layout;
            var config = Observe<ProceduralConfig>(procedural, "_config");
            var driver = Observe<ProceduralDriverConfig>(procedural, "_driverConfig");
            var native = Observe<ProceduralDriverState>(procedural.GetComponent<ProceduralDriver>(), "_state");
            var nearby = Physics.OverlapBox(hit + Vector3.up, new Vector3(1f, 1f, 1f), Quaternion.identity,
                ~0, QueryTriggerInteraction.Ignore).Select(c => HierarchyPath(c.transform) + " bounds=" + c.bounds);
            var obstacles = Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None)
                .Where(o => o.enabled && o.carving && (o.transform.position - hit).sqrMagnitude < 16f)
                .Select(o => HierarchyPath(o.transform) + " size=" + o.size);
            var geometry = new ProceduralGeometryPresenter().Build(layout, config, driver);
            string blocks = string.Join("; ", geometry.Where(b => b.HasCollision && b.Kind == ProceduralSurfaceKind.Wall &&
                    (b.Center - hit).sqrMagnitude <= (b.Size.magnitude + 2f) * (b.Size.magnitude + 2f))
                    .Select(b => "room=" + b.RoomId + " piece=" + b.PieceId + " role=" + b.Role +
                        " center=" + b.Center + " size=" + b.Size + " rotation=" + b.Rotation));
            var sources = native.NavigationSources.Where(s =>
                (s.transform.MultiplyPoint3x4(Vector3.zero) - hit).sqrMagnitude <= (s.size.magnitude + 2f) * (s.size.magnitude + 2f))
                .Select(s => "shape=" + s.shape + " area=" + s.area + " size=" + s.size + " transform=" + s.transform);
            return "theme=" + procedural.ThemeId + " door=" + door.Center + " alongX=" + door.AlongX +
                " samples=" + sideA + " -> " + sideB + " hit=" + hit +
                " seed=" + layout.Seed + " round=" + layout.RoundIndex + " radius=" + native.NavigationSettings.agentRadius +
                " height=" + native.NavigationSettings.agentHeight + " voxel=" + native.NavigationSettings.voxelSize +
                " templates=" + string.Join("; ", layout.TemplateRooms.Where(r => r.RoomId == door.FromRoomId || r.RoomId == door.ToRoomId)
                    .Select(r => r.RoomId + ":" + r.Template.Id + " turns=" + r.Turns + " offset=" + r.Offset +
                        " open=" + string.Join(",", r.OpenDoors))) +
                " baked blocks=[" + blocks + "] colliders=[" + string.Join("; ", nearby) + "] carving=[" +
                string.Join("; ", obstacles) + "] native sources=[" + string.Join("; ", sources) + "] manifest=" + procedural.LayoutManifest;
        }

        private static string HierarchyPath(Transform item)
            => item.parent == null ? item.name : HierarchyPath(item.parent) + "/" + item.name;

        private static void AssertActiveRoster(ProgressionEffects effects)
        {
            Assert.That(effects.ActiveThreatIds.Count, Is.EqualTo(effects.ActiveThreatBudget));
            Assert.That(effects.ActiveThreatIds.Distinct().Count(), Is.EqualTo(effects.ActiveThreatBudget));
            Assert.That(HunterRegistry.Items.Select(hunter => hunter.Id).Distinct().Count(), Is.EqualTo(effects.ActiveThreatBudget));
            var modelSignatures = new HashSet<string>();
            foreach (var hunter in HunterRegistry.Items)
            {
                // Echo is deliberately inactive until its replay delay elapses (owner rule).
                Assert.That(hunter.ReadOnlyState.IsActive || hunter.ArchetypeKey == "echo", Is.True);
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
            yield return Until(() => load.isDone && Object.FindObjectsByType<Worsen.Presentation.Menu.MenuDriver>(FindObjectsSortMode.None)
                .Any(menu => Observe<Worsen.Presentation.Menu.MenuDriverState>(menu, "_state").TitleVisible),
                "Saved HorrorRun did not show its title.");
            var title = One<Worsen.Presentation.Menu.MenuDriver>().GetComponent<UIDocument>().rootVisualElement.Q<Button>("start-run");
            Assert.That(title, Is.Not.Null);
            using (var click = NavigationSubmitEvent.GetPooled()) { click.target = title; title.SendEvent(click); }
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
