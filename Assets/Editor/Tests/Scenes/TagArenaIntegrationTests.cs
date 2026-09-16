// ============================================================================
// TagArenaIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the built I1 arena through its real startup, fixed tick and reload.
//   Synthetic input verifies motor integration without claiming keyboard evidence.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Orchestrator · Scenes integration.
// KEY RESPONSIBILITIES:
//   - Check real movement, committed poses and canonical service lifetimes.
//   - Hold Sprint explicitly for the running-distance and end-speed checks.
//   - Check fresh run state and paired subscriptions after a SceneFlow reload.
// DEPENDENCIES:
//   - Core, Player, Run/SceneFlow, scene routers and Input/Telemetry/Camera/PostFX.
//   - Unity Test Framework, NUnit and the Level builder's read-only spawn value.
// USAGE NOTES:
//   Coordinator holds the Unity lease. Uses Test Framework's isolated scene and
//   restoration; never builds/saves assets. Runtime capture may write normal logs.
//   Initial loading is an Editor-test bootstrap; subsequent loading uses SceneFlow.
//   Background execution is enabled only within the fixture and restored afterward.
// ============================================================================
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Editor.Level;
using Worsen.Orchestrator;
using Worsen.Presentation.Camera;
using Worsen.Presentation.Input;
using Worsen.Presentation.PostFX;
using Worsen.Presentation.Telemetry;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;

namespace Worsen.Tests.Scenes
{
    public sealed class TagArenaIntegrationTests
    {
        private const string ArenaPath = "Assets/Scenes/TagArena.unity";

        [UnityTest]
        public IEnumerator BuiltArenaMovesWithSyntheticInputAndReloadsWithoutDuplicateServices()
        {
            yield return new EnterPlayMode();
            // Domain reload restores the iterator position, so create captured locals afterward.
            bool previousBackground = Application.runInBackground;
            try
            {
                Application.runInBackground = true;
                yield return RunArenaAssertions();
            }
            finally { Application.runInBackground = previousBackground; }
        }

        private static IEnumerator RunArenaAssertions()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<TagArenaSceneRoot>(FindObjectsSortMode.None), Is.Empty,
                "Run this Edit Mode fixture from the Test Framework isolated bootstrap scene.");
            AsyncOperation load = SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "Build TagArena before running this integration test.");
            yield return Until(() => load.isDone && Ready(), "Initial arena did not become ready.");
            RunSessionManager run = One<RunSessionManager>();
            InputManager input = One<InputManager>();
            SceneFlowManager flow = One<SceneFlowManager>();
            TelemetryManager telemetry = One<TelemetryManager>();
            Assert.That(run, Is.SameAs(RunSessionManager.Instance)); Assert.That(input, Is.SameAs(InputManager.Instance));
            Assert.That(flow, Is.SameAs(SceneFlowManager.Instance)); Assert.That(telemetry, Is.SameAs(TelemetryManager.Instance));
            input.SetInputEnabled(false);
            PlayerManager player = AssertArena();
            int[] persistentIds = { run.GetInstanceID(), input.GetInstanceID(), flow.GetInstanceID(), telemetry.GetInstanceID() };
            int[] subscribers = SubscriberCounts(run, input, flow);
            int oldPlayerId = player.GetInstanceID(), oldCameraId = One<CameraManager>().GetInstanceID(), seed = run.Seed;
            System.Random originalRandom = run.RandomSource;
            string firstCapture = telemetry.LastOutputPath;
            Assert.That(firstCapture, Is.Not.Empty);
            Assert.That(originalRandom, Is.Not.Null);
            yield return DriveSyntheticForward(run, input, player);
            long previousTick = run.Tick, readyTick = -1;
            int readyCount = 0;
            Action<SceneKey> ready = key => { if (key == SceneKey.TagArena) { readyCount++; readyTick = run.Tick; input.SetInputEnabled(false); } };
            TagArenaSceneRoot.SceneReady += ready;
            try
            {
                flow.RequestLoad(SceneKey.TagArena);
                yield return Until(() => readyCount > 0 && Ready(), "Reload did not publish readiness and resume ticking.");
            }
            finally { TagArenaSceneRoot.SceneReady -= ready; }
            Assert.That(readyCount, Is.EqualTo(1));
            Assert.That(readyTick, Is.Zero, "SceneReady must reset the tick before simulation resumes.");
            Assert.That(run.Tick, Is.LessThan(previousTick));
            Assert.That(run.Seed, Is.EqualTo(seed));
            Assert.That(run.RandomSource, Is.Not.SameAs(originalRandom));
            Assert.That(new[] { One<RunSessionManager>().GetInstanceID(), One<InputManager>().GetInstanceID(),
                One<SceneFlowManager>().GetInstanceID(), One<TelemetryManager>().GetInstanceID() }, Is.EqualTo(persistentIds));
            Assert.That(new[] { RunSessionManager.Instance.GetInstanceID(), InputManager.Instance.GetInstanceID(),
                SceneFlowManager.Instance.GetInstanceID(), TelemetryManager.Instance.GetInstanceID() }, Is.EqualTo(persistentIds));
            Assert.That(AssertArena().GetInstanceID(), Is.Not.EqualTo(oldPlayerId));
            Assert.That(One<CameraManager>().GetInstanceID(), Is.Not.EqualTo(oldCameraId));
            Assert.That(SubscriberCounts(run, input, flow), Is.EqualTo(subscribers), "Reload leaked or lost event subscribers.");
            Assert.That(telemetry.LastOutputPath, Is.Not.Empty.And.Not.EqualTo(firstCapture));
            Assert.That(telemetry.LastError, Is.Empty);
        }

        private static IEnumerator DriveSyntheticForward(RunSessionManager run, InputManager input, PlayerManager player)
        {
            int supplied = 0, ticks = 0, frames = 0, samples = 0, probes = 0;
            Vector3 start = player.ReadOnlyState.Position, finish = start;
            float endSpeed = 0f;
            var forward = new InputFrame(Vector2.up, Vector2.zero, InputButtons.Sprint, InputButtons.None, InputButtons.None);
            Action inject = () => { run.ReceiveInput(supplied < 60 ? forward : default); supplied++; };
            Action<InputFrame> frame = _ => frames++;
            Action<PlayerMovementSample> sample = _ => samples++;
            Action<InputProbeRecord> probe = _ => probes++;
            Action<InputFrame, float, long> tick = (_, __, ___) =>
            {
                ticks++;
                if (ticks != 60) return;
                finish = player.ReadOnlyState.Position;
                Vector3 velocity = player.LastMovementSample.Velocity;
                endSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            };
            // Added after the live InputOrchestrator; its neutral frame is replaced synchronously.
            run.BeforeTick += inject; input.FramePublished += frame;
            run.PlayerMovementPublished += sample; run.PlayerProbeRecorded += probe; run.TickAdvanced += tick;
            try
            {
                yield return Until(() => ticks >= 60, "The real fixed tick did not complete 60 synthetic movement steps.");
                Assert.That(frames, Is.EqualTo(ticks)); Assert.That(samples, Is.EqualTo(ticks)); Assert.That(probes, Is.EqualTo(ticks));
                Assert.That(supplied, Is.EqualTo(ticks));
                Assert.That(Vector3.ProjectOnPlane(finish - start, Vector3.up).magnitude, Is.InRange(5f, 9f));
                Assert.That(Mathf.Abs(finish.y - start.y), Is.LessThan(0.25f), "Player fell or climbed during the clear-ground segment.");
                Assert.That(endSpeed, Is.InRange(7f, 8.1f));
                Assert.That(player.LastProbeRecord.Resolution.Grounded, Is.True);
            }
            finally
            {
                run.BeforeTick -= inject; input.FramePublished -= frame;
                run.PlayerMovementPublished -= sample; run.PlayerProbeRecorded -= probe; run.TickAdvanced -= tick;
            }
        }

        private static bool Ready() => SceneManager.GetActiveScene().path == ArenaPath && RunSessionManager.Instance != null
            && RunSessionManager.Instance.Phase == RunPhase.FirstSweep && RunSessionManager.Instance.Tick >= 3 && PlayerRegistry.Items.Count == 1;
        private static PlayerManager AssertArena()
        {
            PlayerManager player = One<PlayerManager>();
            Assert.That(PlayerRegistry.Items.Count, Is.EqualTo(1)); Assert.That(PlayerRegistry.Items[0], Is.SameAs(player));
            Assert.That(player.ReadOnlyState.Health, Is.EqualTo(100f));
            Vector3 position = player.ReadOnlyState.Position;
            Assert.That(float.IsNaN(position.sqrMagnitude) || float.IsInfinity(position.sqrMagnitude), Is.False);
            Assert.That(Vector3.Distance(position, TagArenaLevelSetup.SpawnPosition), Is.LessThan(0.25f));
            Assert.That(player.LastProbeRecord.Resolution.Present && player.LastProbeRecord.Resolution.Grounded, Is.True);
            Assert.That(One<CameraManager>().IsReady && One<PostFXManager>().IsReady, Is.True);
            Assert.That(UnityEngine.Camera.main, Is.Not.Null);
            Assert.That(Vector3.Distance(UnityEngine.Camera.main.transform.position, player.LastMovementSample.EyePosition), Is.LessThan(0.5f));
            return player;
        }
        private static T One<T>() where T : Component
        {
            T[] found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(found.Length, Is.EqualTo(1), typeof(T).Name + " must have one live instance."); return found[0];
        }
        private static int[] SubscriberCounts(RunSessionManager run, InputManager input, SceneFlowManager flow) => new[]
        {
            Count(run, "BeforeTick"), Count(run, "PlayerMovementPublished"), Count(run, "PlayerProbeRecorded"),
            Count(run, "CaptureStarted"), Count(run, "CaptureEnded"), Count(input, "FramePublished"),
            Count(flow, "SceneLoadStarted"), Count(typeof(TagArenaSceneRoot), "SceneReady")
        };
        private static int Count(object source, string name)
        {
            Type type = source as Type ?? source.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "Expected field-like event: " + type.Name + "." + name);
            return (field.GetValue(source is Type ? null : source) as Delegate)?.GetInvocationList().Length ?? 0;
        }
        private static IEnumerator Until(Func<bool> condition, string failure)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            for (int frame = 0; frame < 3600 && !condition() && Time.realtimeSinceStartup < deadline; frame++) yield return null;
            Assert.That(condition(), Is.True, failure);
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
