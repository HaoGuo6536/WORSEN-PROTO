// ============================================================================
// SharedSceneRootWiringTests.cs
// ============================================================================
// PURPOSE:
//   Exercises shared assembly wiring against real inactive Unity components.
//   Each supported scene supplies its own catch policy, so compatibility routing
//   remains optional while HorrorRun keeps its required route and scoped expansion.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Scenes.
// KEY RESPONSIBILITIES:
//   - Exercise catch binding, rebinding and cleanup for all three parameter sets.
//   - Preserve optional legacy routes and strict HorrorRun route failures.
//   - Check unwired compatibility bootstrap diagnostics without publishing readiness.
// DEPENDENCIES:
//   - SharedSceneRoot, Audio/Results/Camera and Expedition component references.
//   - NUnit, Unity test logging and reflection for observing private route bindings.
// USAGE NOTES:
//   Coordinator-run Edit Mode tests; native components are required, not emulated.
//   Inactive disposable objects avoid service initialization and asset writes.
//   No singleton is initialized or changed and no scene is loaded by this fixture.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Presentation.Audio;
using Worsen.Presentation.Camera;
using Worsen.Presentation.DebugOverlay;
using Worsen.Presentation.Input;
using Worsen.Presentation.Results;
using Worsen.Presentation.Telemetry;
using Worsen.Session.Expedition;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Scenes
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class SharedSceneRootWiringTests
    {
        private readonly List<GameObject> _owned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        [TestCase(SceneKey.TagArena, false, false)]
        [TestCase(SceneKey.FloorLoop, false, false)]
        [TestCase(SceneKey.HorrorRun, true, true)]
        public void SceneCatchPoliciesRebindAndReleaseWithoutEnablingExpeditionForLegacyScenes(
            SceneKey scene, bool requireAudioRoute, bool clearExpansion)
        {
            var audio = Component<AudioManager>();
            var audioRoute = audio.gameObject.AddComponent<AudioOrchestrator>();
            var results = scene == SceneKey.HorrorRun ? null : Component<ResultsManager>();
            var resultsRoute = results != null ? results.gameObject.AddComponent<ResultsOrchestrator>() : null;
            var firstCamera = Component<CameraManager>();
            var replacementCamera = Component<CameraManager>();
            ExpeditionSessionManager expedition = null;
            if (scene == SceneKey.HorrorRun)
            {
                expedition = Component<ExpeditionSessionManager>();
                audioRoute.ConfigureExpansion(null, null, expedition, null);
            }
            SharedSceneRoot.ConfigureCatch(audio, firstCamera, results, requireAudioRoute);
            Assert.That(Reference(audioRoute, "_camera"), Is.SameAs(firstCamera));
            if (resultsRoute != null) Assert.That(Reference(resultsRoute, "_camera"), Is.SameAs(firstCamera));
            SharedSceneRoot.ConfigureCatch(audio, replacementCamera, results, requireAudioRoute);
            Assert.That(Reference(audioRoute, "_camera"), Is.SameAs(replacementCamera));
            if (resultsRoute != null) Assert.That(Reference(resultsRoute, "_camera"), Is.SameAs(replacementCamera));
            Assert.That(Reference(audioRoute, "_expedition"), Is.SameAs(expedition));
            SharedSceneRoot.ClearCatch(results, audio, clearExpansion);
            Assert.That(Reference(audioRoute, "_camera"), Is.Null);
            Assert.That(Reference(audioRoute, "_expedition"), Is.Null);
            if (resultsRoute != null) Assert.That(Reference(resultsRoute, "_camera"), Is.Null);
            Assert.DoesNotThrow(() => SharedSceneRoot.ClearCatch(results, audio, clearExpansion));
        }

        [TestCase(SceneKey.TagArena, false)]
        [TestCase(SceneKey.FloorLoop, false)]
        [TestCase(SceneKey.HorrorRun, true)]
        public void MissingAudioRouteRetainsEachScenesRequiredOrOptionalPolicy(SceneKey scene, bool required)
        {
            var audio = Component<AudioManager>();
            var camera = Component<CameraManager>();
            if (required)
                Assert.Throws<NullReferenceException>(() => SharedSceneRoot.ConfigureCatch(audio, camera, null, required));
            else
                Assert.DoesNotThrow(() => SharedSceneRoot.ConfigureCatch(audio, camera, null, required));
            Assert.DoesNotThrow(() => SharedSceneRoot.ClearCatch(null, audio, clearExpansion: required));
        }

        [TestCase(SceneKey.TagArena, "Worsen/Scenes/1 — Build TagArena")]
        [TestCase(SceneKey.FloorLoop, "Worsen/Scenes/2 — Build FloorLoop")]
        public void UnwiredCompatibilitySceneReturnsFalseWithItsOriginalDiagnostic(SceneKey scene, string command)
        {
            Object context = scene == SceneKey.TagArena ? (Object)Component<TagArenaSceneRoot>() : Component<FloorLoopSceneRoot>();
            Assert.That(RunSessionManager.Instance, Is.Null, "Use an isolated Edit Mode fixture, not live services.");
            Assert.That(SceneFlowManager.Instance, Is.Null);
            Assert.That(InputManager.Instance, Is.Null);
            Assert.That(DebugOverlayManager.Instance, Is.Null);
            RunSessionManager run = null;
            SceneFlowManager flow = null;
            InputManager input = null;
            DebugOverlayManager overlay = null;
            TelemetryManager telemetry = null;
            AudioManager audio = null;
            LogAssert.Expect(LogType.Error, $"{scene} bootstrap is unwired. Run {command}.");
            bool assembled = SharedSceneRoot.TryAssembleCompatibility(scene, command, context, 1,
                ref run, ref flow, ref input, ref overlay, ref telemetry, ref audio,
                null, null, null, Vector3.zero, null, null, null, null, Vector3.zero,
                null, null, null, null, "revision", "config-hash", out var player);
            Assert.That(assembled, Is.False);
            Assert.That(player, Is.Null);
            Assert.That(run, Is.Null);
            Assert.That(telemetry, Is.Null);
            Assert.That(audio, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TeardownToleratesMissingServices(bool clearExpansion)
            => Assert.DoesNotThrow(() => SharedSceneRoot.ClearCatch(null, null, clearExpansion));

        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " shared wiring test");
            _owned.Add(owner);
            owner.SetActive(false);
            return owner.AddComponent<T>();
        }

        private static object Reference(object owner, string field)
            => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
}
