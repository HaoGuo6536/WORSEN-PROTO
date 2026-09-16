// ============================================================================
// FloorLoopIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the built FloorLoop scene through real pickup and exit triggers,
//   Session outcome publication, Results presentation and routed scene restart.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Floor scene integration.
// KEY RESPONSIBILITIES:
//   - Verify physical trigger delivery through the ordinary Manager/router paths.
//   - Verify golden currency reaches Results and restart creates fresh scene state.
//   - Preserve persistent service identities and event subscription counts.
// DEPENDENCIES:
//   - Core, Domain Floor/Player/Hunter/Level, Session Run/SceneFlow and Presentation.
//   - Unity Test Framework, NUnit, UI Toolkit and the already generated FloorLoop.
// USAGE NOTES:
//   Coordinator holds the Unity lease. No scene, prefab or config asset is saved.
//   Captured locals and background policy are established after EnterPlayMode.
//   Player teleport plus Driver reinitialization is test arrangement only; the
//   following ordinary Session tick must commit the new pose before it is asserted.
//   Hunters are deactivated only for this runtime flow fixture. This does not prove
//   movement, chase, collapse timing, floor duration, replay or subjective acceptance.
//   UI submit dispatch verifies event routing, not physical pointer hit testing.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Audio;
using Worsen.Presentation.HUD;
using Worsen.Presentation.Input;
using Worsen.Presentation.Results;
using Worsen.Presentation.Telemetry;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Floor
{
    public sealed class FloorLoopIntegrationTests
    {
        private const string ScenePath = "Assets/Scenes/FloorLoop.unity";
        private bool _restoreBackground;
        private bool _previousBackground;

        [UnityTest]
        public IEnumerator BuiltFloorTriggersReachResultsAndRestartThroughSceneFlow()
        {
            yield return new EnterPlayMode();
            _previousBackground = Application.runInBackground;
            _restoreBackground = true;
            Application.runInBackground = true;
            try { yield return ExerciseBuiltFloor(); }
            finally { RestoreBackground(); }
        }

        private static IEnumerator ExerciseBuiltFloor()
        {
            Assert.That(Object.FindObjectsByType<FloorLoopSceneRoot>(FindObjectsSortMode.None), Is.Empty,
                "Run from the Test Framework isolated bootstrap scene.");
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "Generate and enable FloorLoop before running this fixture.");
            yield return Until(() => load.isDone && Ready(), "FloorLoop did not initialize and tick.");

            var run = One<RunSessionManager>();
            var input = One<InputManager>();
            var flow = One<SceneFlowManager>();
            var telemetry = One<TelemetryManager>();
            var audio = One<AudioManager>();
            var floor = One<FloorManager>();
            var player = One<PlayerManager>();
            var results = One<ResultsManager>();
            var level = One<LevelManager>();
            Assert.That(run, Is.SameAs(RunSessionManager.Instance));
            Assert.That(input, Is.SameAs(InputManager.Instance));
            Assert.That(flow, Is.SameAs(SceneFlowManager.Instance));
            Assert.That(telemetry, Is.SameAs(TelemetryManager.Instance));
            Assert.That(audio, Is.SameAs(AudioManager.Instance));
            Assert.That(HunterRegistry.Items.Count, Is.EqualTo(1));
            One<HunterManager>().gameObject.SetActive(false);
            Assert.That(HunterRegistry.Items.Count, Is.Zero, "Only chase interference is suppressed in this flow fixture.");
            Assert.That(floor.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Locked));
            Assert.That(floor.ReadOnlyState.CakeCount, Is.Zero);
            Assert.That(floor.ReadOnlyState.GoldenCakeCount, Is.Zero);
            var anchors = floor.ReadOnlyState.ActiveCakeAnchors.OrderBy(anchor => anchor.Id).ToArray();
            Assert.That(anchors.Length, Is.EqualTo(floor.ReadOnlyState.RequiredCakeCount).And.GreaterThanOrEqualTo(10));
            int[] persistentIds = PersistentIds();
            int oldFloorId = floor.GetInstanceID(), oldPlayerId = player.GetInstanceID(), oldResultsId = results.GetInstanceID();
            int seed = run.Seed;
            var firstRandom = run.RandomSource;
            string firstCapturePath = telemetry.LastOutputPath;
            var summaries = new List<RunSummary>();
            var terminalOrder = new List<string>();
            var phases = new List<RunPhase>();
            int captureEnds = 0, restarts = 0, sceneReady = 0;
            long restartTick = -1;
            bool captureComplete = false;
            Action neutralInput = () => run.ReceiveInput(default);
            Action<RunSummary> ended = summary => { summaries.Add(summary); terminalOrder.Add("ended"); };
            Action<long, bool> captureEnded = (_, complete) => { captureEnds++; captureComplete = complete; terminalOrder.Add("capture"); };
            Action<RunPhase> phaseChanged = phase => phases.Add(phase);
            Action<RunCaptureMetadata> captureStarted = _ => restarts++;
            Action<SceneKey> ready = scene => { if (scene == SceneKey.FloorLoop) { sceneReady++; restartTick = run.Tick; } };
            run.BeforeTick += neutralInput;
            run.RunEnded += ended;
            run.CaptureEnded += captureEnded;
            run.PhaseChanged += phaseChanged;
            run.CaptureStarted += captureStarted;
            FloorLoopSceneRoot.SceneReady += ready;
            int[] subscribers = SubscriberCounts(run, input, flow);
            try
            {
                for (int index = 0; index < anchors.Length; index++)
                {
                    var anchor = anchors[index];
                    var pickup = floor.GetComponentsInChildren<CakePickup>()
                        .Single(item => item.Kind == PickupKind.Cake && item.AnchorId == anchor.Id);
                    Assert.That(pickup.GetComponent<Collider>().isTrigger, Is.True);
                    long beforeTick = run.Tick;
                    ArrangePlayerAt(player, anchor.Position);
                    int expected = index + 1;
                    yield return Until(() => floor.ReadOnlyState.CakeCount >= expected && run.Tick > beforeTick,
                        "Physical cake trigger did not collect anchor " + anchor.Id + ".", 3f);
                    AssertCommittedPose(player, anchor.Position);
                    Assert.That(pickup.gameObject.activeSelf, Is.False);
                    Assert.That(floor.ReadOnlyState.CakeCount, Is.EqualTo(expected));
                }

                Assert.That(phases, Does.Contain(RunPhase.ExitOpen));
                Assert.That(floor.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Open));
                Assert.That(floor.ReadOnlyState.ActiveCakeAnchors, Is.Empty);
                var hud = One<HUDManager>().GetComponent<UIDocument>().rootVisualElement;
                Assert.That(hud.Q<Label>("cake-count").text,
                    Is.EqualTo("Cakes: " + anchors.Length + " / " + anchors.Length));
                Assert.That(hud.Q<Label>("exit-state").text, Is.EqualTo("Exit: OPEN"));

                // The last cake may immediately spawn a golden trigger around the
                // overlapping Player. Explicitly collect a different active anchor.
                var golden = floor.GetComponentsInChildren<CakePickup>()
                    .Where(item => item.Kind == PickupKind.GoldenCake && item.AnchorId != anchors.Last().Id)
                    .OrderByDescending(item => item.AnchorId).First();
                var goldenAnchor = anchors.Single(anchor => anchor.Id == golden.AnchorId);
                int beforeGolden = floor.ReadOnlyState.GoldenCakeCount;
                long beforeGoldenTick = run.Tick;
                ArrangePlayerAt(player, goldenAnchor.Position);
                yield return Until(() => !golden.gameObject.activeSelf && run.Tick > beforeGoldenTick,
                    "Physical golden trigger did not reach the Floor wallet.", 3f);
                AssertCommittedPose(player, goldenAnchor.Position);
                Assert.That(floor.ReadOnlyState.GoldenCakeCount, Is.GreaterThan(beforeGolden));
                Assert.That(floor.ReadOnlyState.CakeCount, Is.EqualTo(anchors.Length));
                int goldenTotal = floor.ReadOnlyState.GoldenCakeCount;

                var exit = One<FloorExitVolume>();
                Assert.That(exit.GetComponent<Collider>().isTrigger, Is.True);
                ArrangePlayerAt(player, level.ReadOnlyState.Graph.ExitPosition);
                yield return Until(() => summaries.Count > 0, "Physical exit trigger did not end the run.", 3f);
                AssertCommittedPose(player, level.ReadOnlyState.Graph.ExitPosition);
                Assert.That(run.Phase, Is.EqualTo(RunPhase.Ended));
                Assert.That(summaries, Has.Count.EqualTo(1));
                Assert.That(captureEnds, Is.EqualTo(1));
                Assert.That(captureComplete, Is.True);
                Assert.That(terminalOrder, Is.EqualTo(new[] { "capture", "ended" }));
                Assert.That(summaries[0].EndReason, Is.EqualTo(RunEndReason.Escaped));
                Assert.That(summaries[0].Scene, Is.EqualTo(SceneKey.FloorLoop));
                Assert.That(summaries[0].Seed, Is.EqualTo(seed));
                Assert.That(summaries[0].CakesCollected, Is.EqualTo(anchors.Length));
                Assert.That(summaries[0].GoldenCakesCollected, Is.EqualTo(goldenTotal));

                var resultsRoot = results.GetComponent<UIDocument>().rootVisualElement;
                Assert.That(resultsRoot.Q<VisualElement>("results").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(resultsRoot.Q<Label>("cake-total").text, Is.EqualTo(anchors.Length.ToString(CultureInfo.InvariantCulture)));
                Assert.That(resultsRoot.Q<Label>("golden-cake-total").text, Is.EqualTo(goldenTotal.ToString(CultureInfo.InvariantCulture)));
                Assert.That(resultsRoot.Q<Label>("end-reason").text, Is.EqualTo("Escaped through the exit"));
                long terminalTick = run.Tick;
                double fixedTimeBefore = Time.fixedTimeAsDouble;
                double twoFixedSteps = 2d * Time.fixedDeltaTime;
                yield return Until(() => Time.fixedTimeAsDouble - fixedTimeBefore >= twoFixedSteps,
                    "Physics did not advance while checking the ended run guard.", 3f);
                Assert.That(run.Tick, Is.EqualTo(terminalTick), "Ended run must not resume simulation.");
                Assert.That(summaries, Has.Count.EqualTo(1));

                var restart = resultsRoot.Q<Button>("restart-button");
                Assert.That(restart.panel, Is.Not.Null);
                Assert.That(restart.enabledInHierarchy, Is.True);
                using (var submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = restart;
                    restart.SendEvent(submit);
                }
                yield return Until(() => sceneReady == 1 && Ready(), "Results submit did not reload through SceneFlow.");
                Assert.That(restarts, Is.EqualTo(1));
                Assert.That(restartTick, Is.Zero);
                Assert.That(PersistentIds(), Is.EqualTo(persistentIds));
                Assert.That(run.Seed, Is.EqualTo(seed));
                Assert.That(run.RandomSource, Is.Not.SameAs(firstRandom));
                var freshFloor = One<FloorManager>();
                Assert.That(freshFloor.GetInstanceID(), Is.Not.EqualTo(oldFloorId));
                Assert.That(One<PlayerManager>().GetInstanceID(), Is.Not.EqualTo(oldPlayerId));
                Assert.That(One<ResultsManager>().GetInstanceID(), Is.Not.EqualTo(oldResultsId));
                Assert.That(freshFloor.ReadOnlyState.CakeCount, Is.Zero);
                Assert.That(freshFloor.ReadOnlyState.GoldenCakeCount, Is.Zero);
                Assert.That(freshFloor.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Locked));
                Assert.That(freshFloor.ReadOnlyState.ActiveCakeAnchors.Select(anchor => anchor.Id).OrderBy(id => id),
                    Is.EqualTo(anchors.Select(anchor => anchor.Id)));
                Assert.That(PlayerRegistry.Items.Count, Is.EqualTo(1));
                Assert.That(HunterRegistry.Items.Count, Is.EqualTo(1));
                Assert.That(SubscriberCounts(run, input, flow), Is.EqualTo(subscribers));
                Assert.That(telemetry.LastOutputPath, Is.Not.Empty.And.Not.EqualTo(firstCapturePath));
                Assert.That(telemetry.LastError, Is.Empty);
                Assert.That(One<ResultsManager>().GetComponent<UIDocument>().rootVisualElement
                    .Q<VisualElement>("results").style.display.value, Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                if (run != null)
                {
                    run.BeforeTick -= neutralInput;
                    run.RunEnded -= ended;
                    run.CaptureEnded -= captureEnded;
                    run.PhaseChanged -= phaseChanged;
                    run.CaptureStarted -= captureStarted;
                }
                FloorLoopSceneRoot.SceneReady -= ready;
            }
        }

        private static void ArrangePlayerAt(PlayerManager player, Vector3 feet)
        {
            Assert.That(player.ReadOnlyState.IsAlive, Is.True);
            Assert.That(player.ReadOnlyState.MovementState, Is.Not.EqualTo(MovementState.Vault));
            Assert.That(player.ReadOnlyState.MovementState, Is.Not.EqualTo(MovementState.Slide));
            player.transform.position = feet;
            player.GetComponent<Rigidbody>().position = feet;
            player.GetComponent<PlayerDriver>().Initialize();
            Physics.SyncTransforms();
        }

        private static void AssertCommittedPose(PlayerManager player, Vector3 expected)
        {
            Assert.That(Vector3.Distance(player.ReadOnlyState.Position, expected), Is.LessThan(0.4f),
                "The normal Player tick must commit the arranged position.");
            Assert.That(Vector3.Distance(player.GetComponent<PlayerDriver>().Position, player.ReadOnlyState.Position), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(player.LastMovementSample.Position, player.ReadOnlyState.Position), Is.LessThan(0.001f));
            Assert.That(player.ReadOnlyState.IsAlive, Is.True);
        }

        private static bool Ready() => SceneManager.GetActiveScene().path == ScenePath && RunSessionManager.Instance != null
            && RunSessionManager.Instance.Scene == SceneKey.FloorLoop && RunSessionManager.Instance.Phase == RunPhase.FirstSweep
            && RunSessionManager.Instance.Tick >= 3 && PlayerRegistry.Items.Count == 1;

        private static int[] PersistentIds() => new[] { One<RunSessionManager>().GetInstanceID(), One<InputManager>().GetInstanceID(),
            One<SceneFlowManager>().GetInstanceID(), One<TelemetryManager>().GetInstanceID(), One<AudioManager>().GetInstanceID() };

        private static T One<T>() where T : Component
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(found, Has.Length.EqualTo(1), typeof(T).Name + " must have one instance.");
            return found[0];
        }

        private static int[] SubscriberCounts(RunSessionManager run, InputManager input, SceneFlowManager flow) => new[]
        {
            Count(run, "BeforeTick"), Count(run, "FloorDisplayChanged"), Count(run, "PhaseChanged"), Count(run, "RunEnded"),
            Count(run, "CaptureStarted"), Count(run, "CaptureEnded"), Count(input, "FramePublished"), Count(flow, "SceneLoadStarted"),
            Count(typeof(FloorLoopSceneRoot), "SceneReady")
        };

        private static int Count(object source, string name)
        {
            Type type = source as Type ?? source.GetType();
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "Expected field-like event " + type.Name + "." + name);
            return (field.GetValue(source is Type ? null : source) as Delegate)?.GetInvocationList().Length ?? 0;
        }

        private static IEnumerator Until(Func<bool> condition, string failure, float seconds = 15f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            for (int frame = 0; frame < 3600 && !condition() && Time.realtimeSinceStartup < deadline; frame++) yield return null;
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
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
