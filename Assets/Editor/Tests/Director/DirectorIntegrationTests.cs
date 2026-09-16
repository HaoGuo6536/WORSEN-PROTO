// ============================================================================
// DirectorIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the built FloorLoop's movement-pressure intrusion with its original
//   actors, configuration and ordered Session ticks. The fixture observes the
//   Director event, its Session relay and the real rendering volume's effect
//   parameters, then waits for the effect to expire through normal frame updates.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Director integration.
// KEY RESPONSIBILITIES:
//   - Verify live intrusion identity, duration, two-Hertz evaluation and one-shot behavior.
//   - Check real PostFX volume activation, camera inclusion and natural effect expiry.
// DEPENDENCIES:
//   - Core facts; Director/Player/Hunter/Chase; Session.Run; PostFX; FloorLoopSceneRoot.
//   - Unity Test Framework and reflection for read-only rendering-package inspection.
// USAGE NOTES:
//   Coordinator runs under the Unity lease. Uses unchanged FloorLoop spawns and
//   profiles; synthetic neutral input is the only gameplay arrangement. No asset,
//   scene, configuration or private gameplay state is written. Timing assertions
//   use Run ticks; the wall-clock bound only detects a stalled editor. Background
//   execution is restored in finally and UnityTearDown exits Play Mode. This is
//   a short intrusion smoke, not delayed-hint travel, relief/exit cadence, full-floor
//   pressure-gap, human perception or no-wandering acceptance evidence.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Director;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Editor.Level;
using Worsen.Orchestrator;
using Worsen.Presentation.PostFX;
using Worsen.Session.Run;

namespace Worsen.Tests.Director
{
    public sealed class DirectorIntegrationTests
    {
        private const string ScenePath = "Assets/Scenes/FloorLoop.unity";
        private const long ObservationTicks = 450;

        [UnityTest]
        public IEnumerator DefaultFloorStationaryPlayerRoutesOneIntrusionToActualVolumeAndExpires()
        {
            yield return new EnterPlayMode();
            // Create captured state after EnterPlayMode's domain reload.
            bool previousBackground = Application.runInBackground;
            try
            {
                Application.runInBackground = true;
                yield return ObserveFloor();
            }
            finally { Application.runInBackground = previousBackground; }
        }

        private static IEnumerator ObserveFloor()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<FloorLoopSceneRoot>(FindObjectsSortMode.None), Is.Empty,
                "Start from the Test Framework isolated bootstrap scene.");
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 60f).Within(0.000001f));
            var trial = new Trial();
            SceneManager.sceneLoaded += trial.OnLoaded;
            try
            {
                TestContext.Progress.WriteLine("Director intrusion smoke: default FloorLoop spawns/profiles, " +
                    "stationary synthetic Run input, 450 observed fixed ticks. This is not a full-floor " +
                    "pressure-gap or no-wandering acceptance measurement.");
                AsyncOperation load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, "Build FloorLoop before running this fixture.");
                yield return Until(() => trial.Ready || trial.Failure.Length > 0);
                Assert.That(trial.Failure, Is.Empty);
                Assert.That(load.isDone && trial.Ready, Is.True, "FloorLoop did not assemble.");
                float watchdog = Time.realtimeSinceStartup + 45f;
                for (int frame = 0; trial.Ticks < ObservationTicks && trial.Failure.Length == 0 &&
                    frame < 36000 && Time.realtimeSinceStartup < watchdog; frame++)
                {
                    trial.ObserveVolume();
                    yield return null;
                }
                trial.ObserveVolume();
                Assert.That(trial.Failure, Is.Empty, trial.Describe());
                Assert.That(trial.Ticks, Is.GreaterThanOrEqualTo(ObservationTicks), "Session stalled: " + trial.Describe());
                trial.AssertOutcome();
                TestContext.Progress.WriteLine("Director live volume smoke passed: " + trial.Describe() +
                    "; desaturation/grain routed and naturally cleared; delayed hints, travel, relief, " +
                    "exit cadence and human acceptance remain separate gates.");
            }
            finally
            {
                SceneManager.sceneLoaded -= trial.OnLoaded;
                trial.Dispose();
            }
        }

        private sealed class Trial : IDisposable
        {
            private RunSessionManager run;
            private DirectorManager director;
            private PlayerManager player;
            private HunterManager hunter;
            private ChaseManager chase;
            private PostFXDriver driver;
            private DirectorConfig config;
            private PostFXDriverConfig effects;
            private string directorBefore, effectsBefore;
            private object color, grain;
            private int readyCount, hints;
            private bool sawEffect, sawExpiry;
            private long effectTick, expiryTick;
            private float activeSaturation, activeGrain;
            public bool Ready;
            public long Ticks;
            public string Failure = "";
            private readonly List<IntrusionSample> emitted = new List<IntrusionSample>();
            private readonly List<IntrusionSample> relayed = new List<IntrusionSample>();
            private readonly List<DirectorPressureSample> pressure = new List<DirectorPressureSample>();

            public void OnLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != ScenePath) return;
                // OnEnable has wired the normal scene routers before sceneLoaded.
                FloorLoopSceneRoot.SceneReady += OnReady;
            }

            private void OnReady(SceneKey scene)
            {
                if (scene != SceneKey.FloorLoop) return;
                try
                {
                    readyCount++;
                    run = One<RunSessionManager>(); director = One<DirectorManager>();
                    player = One<PlayerManager>(); hunter = One<HunterManager>(); chase = One<ChaseManager>();
                    driver = One<PostFXDriver>();
                    var root = One<FloorLoopSceneRoot>();
                    var manager = One<PostFXManager>();
                    config = (DirectorConfig)Read(root, "_directorConfig");
                    effects = (PostFXDriverConfig)Read(manager, "_config");
                    Assert.That(config, Is.Not.Null); Assert.That(effects, Is.Not.Null);
                    Assert.That(Read(root, "_director"), Is.SameAs(director));
                    Assert.That(Read(root, "_postFX"), Is.SameAs(manager));
                    Assert.That(run.Tick, Is.Zero);
                    Assert.That(run.Phase, Is.EqualTo(RunPhase.FirstSweep));
                    Assert.That(PlayerRegistry.Items.Count, Is.EqualTo(1));
                    Assert.That(HunterRegistry.Items.Count, Is.EqualTo(1));
                    Assert.That(player.ReadOnlyState.Position, Is.EqualTo(FloorLoopLevelSetup.SpawnPosition));
                    Assert.That(hunter.ReadOnlyState.Position, Is.EqualTo(FloorLoopLevelSetup.HunterSpawnPosition));
                    Assert.That(config.EvaluationIntervalSeconds, Is.EqualTo(0.5f));
                    Assert.That(config.SlowThresholdSeconds, Is.EqualTo(3f));
                    Assert.That(config.IntrusionDurationSeconds, Is.EqualTo(2f));
                    Assert.That(config.IntrusionCooldownSeconds, Is.EqualTo(10f));
                    Assert.That(manager.IsReady && driver.IsReady, Is.True);
                    directorBefore = JsonUtility.ToJson(config);
                    effectsBefore = JsonUtility.ToJson(effects);
                    var volume = (Behaviour)Read(driver, "_volume");
                    object profile = Read(driver, "_profile");
                    Assert.That(volume.isActiveAndEnabled, Is.True);
                    Assert.That(Read(volume, "isGlobal"), Is.EqualTo(true));
                    Assert.That(Read(volume, "m_InternalProfile"), Is.SameAs(profile),
                        "The effects must belong to the actual volume's runtime profile.");
                    color = Read(driver, "_color"); grain = Read(driver, "_grain");
                    var components = ((IEnumerable)Read(profile, "components")).Cast<object>().ToArray();
                    Assert.That(components, Does.Contain(color)); Assert.That(components, Does.Contain(grain));
                    Assert.That(color.GetType().FullName, Is.EqualTo("UnityEngine.Rendering.Universal.ColorAdjustments"));
                    Assert.That(grain.GetType().FullName, Is.EqualTo("UnityEngine.Rendering.Universal.FilmGrain"));
                    Assert.That(Parameter(color, "saturation"), Is.Zero);
                    Assert.That(Parameter(grain, "intensity"), Is.Zero);
                    UnityEngine.Camera camera = UnityEngine.Camera.main;
                    Assert.That(camera, Is.Not.Null);
                    Component cameraData = camera.GetComponents<Component>().Single(component => component != null &&
                        component.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
                    Assert.That(Read(cameraData, "renderPostProcessing"), Is.EqualTo(true));
                    LayerMask mask = (LayerMask)Read(cameraData, "volumeLayerMask");
                    Assert.That(mask.value & (1 << volume.gameObject.layer), Is.Not.Zero);
                    director.OnIntrusion += OnEmitted; director.OnPressureSampled += OnPressure;
                    director.OnHintIssued += OnHint;
                    run.IntrusionPublished += OnRelayed; run.BeforeTick += NeutralInput;
                    run.TickAdvanced += OnTick;
                    Ready = true;
                }
                catch (Exception error) { Fail("Readiness inspection failed: " + error); }
            }

            private void NeutralInput() => run.ReceiveInput(default);
            private void OnEmitted(IntrusionSample sample) => emitted.Add(sample);
            private void OnRelayed(IntrusionSample sample) => relayed.Add(sample);
            private void OnPressure(DirectorPressureSample sample) => pressure.Add(sample);
            private void OnHint(HintPayload hint) => hints++;
            private void OnTick(InputFrame input, float dt, long tick)
            {
                if (tick != Ticks + 1 || Mathf.Abs(dt - 1f / 60f) > 0.000001f)
                    Fail("Session did not produce consecutive 60 Hz ticks.");
                Ticks = tick;
                if (!input.Equals(default(InputFrame))) Fail("The stationary fixture received non-neutral input.");
                if (player.ReadOnlyState.Health != 100f || chase.ReadOnlyState.HasActiveChase)
                    Fail("Default-spawn intrusion window unexpectedly entered combat.");
                if (Vector3.ProjectOnPlane(player.ReadOnlyState.Position - FloorLoopLevelSetup.SpawnPosition,
                    Vector3.up).magnitude > 0.05f)
                    Fail("The stationary Player moved horizontally.");
            }

            public void ObserveVolume()
            {
                if (!Ready || emitted.Count == 0) return;
                float saturation = Parameter(color, "saturation"), intensity = Parameter(grain, "intensity");
                if (saturation == -effects.IntrusionDesaturation && intensity == effects.IntrusionGrain)
                {
                    if (!sawEffect) effectTick = run.Tick;
                    sawEffect = true; activeSaturation = saturation; activeGrain = intensity;
                }
                if (sawEffect && !sawExpiry && saturation == 0f && intensity == 0f)
                { sawExpiry = true; expiryTick = run.Tick; }
            }

            public void AssertOutcome()
            {
                Assert.That(readyCount, Is.EqualTo(1));
                Assert.That(run.Phase, Is.EqualTo(RunPhase.FirstSweep));
                Assert.That(emitted.Count, Is.EqualTo(1), "One stationary episode must not stack intrusions.");
                Assert.That(relayed, Is.EqualTo(emitted), "Session lost, duplicated or changed the actual Director event.");
                Assert.That(emitted[0].Player, Is.EqualTo(player.Id));
                Assert.That(emitted[0].DurationSeconds, Is.EqualTo(config.IntrusionDurationSeconds));
                double eventSeconds = emitted[0].Tick * (double)Time.fixedDeltaTime;
                Assert.That(eventSeconds, Is.InRange(config.SlowThresholdSeconds - 0.00001,
                    config.SlowThresholdSeconds + config.EvaluationIntervalSeconds + Time.fixedDeltaTime));
                Assert.That(sawEffect, Is.True, "Real volume parameters never received the intrusion.");
                Assert.That(sawExpiry, Is.True, "Real volume parameters did not naturally return to neutral.");
                Assert.That(expiryTick, Is.GreaterThan(effectTick));
                Assert.That(Parameter(color, "saturation"), Is.Zero);
                Assert.That(Parameter(grain, "intensity"), Is.Zero);
                Assert.That(hints, Is.Zero, "This short smoke does not reach the default delayed-hint threshold.");
                int evaluationTicks = Mathf.RoundToInt(config.EvaluationIntervalSeconds / Time.fixedDeltaTime);
                Assert.That(pressure.Count, Is.EqualTo((int)(Ticks / evaluationTicks)));
                for (int i = 0; i < pressure.Count; i++)
                {
                    Assert.That(pressure[i].Tick, Is.EqualTo((i + 1L) * evaluationTicks));
                    Assert.That(pressure[i].Player, Is.EqualTo(player.Id));
                    Assert.That(pressure[i].IsChasing || pressure[i].HintIssued, Is.False);
                }
                Assert.That(JsonUtility.ToJson(config), Is.EqualTo(directorBefore));
                Assert.That(JsonUtility.ToJson(effects), Is.EqualTo(effectsBefore));
            }

            public string Describe() => "ticks=" + Ticks + "; Director/Session events=" + emitted.Count + "/" + relayed.Count +
                "; pressure samples=" + pressure.Count + "; active saturation/grain=" + activeSaturation + "/" + activeGrain +
                "; first active/expired Run ticks=" + effectTick + "/" + expiryTick;
            private void Fail(string message) { if (Failure.Length == 0) Failure = message; }
            public void Dispose()
            {
                FloorLoopSceneRoot.SceneReady -= OnReady;
                if (director != null)
                {
                    director.OnIntrusion -= OnEmitted; director.OnPressureSampled -= OnPressure;
                    director.OnHintIssued -= OnHint;
                }
                if (run != null)
                {
                    run.IntrusionPublished -= OnRelayed; run.BeforeTick -= NeutralInput;
                    run.TickAdvanced -= OnTick;
                }
            }
        }

        private static float Parameter(object component, string name) => Convert.ToSingle(Read(Read(component, name), "value"));
        private static object Read(object target, string name)
        {
            if (target == null) throw new InvalidOperationException("Missing object while reading " + name);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(target);
                PropertyInfo property = type.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (property != null) return property.GetValue(target);
            }
            throw new InvalidOperationException("Missing inspected member " + target.GetType().Name + "." + name);
        }
        private static T One<T>() where T : Component
        {
            T[] values = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(values.Length, Is.EqualTo(1), typeof(T).Name + " must have one active instance.");
            return values[0];
        }
        private static IEnumerator Until(Func<bool> condition)
        {
            float watchdog = Time.realtimeSinceStartup + 30f;
            for (int frame = 0; frame < 36000 && !condition() && Time.realtimeSinceStartup < watchdog; frame++) yield return null;
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
