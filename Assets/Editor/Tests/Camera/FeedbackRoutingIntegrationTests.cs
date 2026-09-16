// ============================================================================
// FeedbackRoutingIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Follows real TagArena pursuit and damage into the first-person camera, actual
//   rendering volume, audio sources and HUD, then verifies fresh-run restoration.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Camera integration.
// KEY RESPONSIBILITIES:
//   - Drive held free-look explicitly with mouse degrees; only release is an automatic ease.
//   - Observe confirmed sight and two native lunge contacts through Session routing.
//   - Measure actual LookBack camera endpoints with frame-width timing uncertainty.
//   - Verify scene-local comfort settings without modifying shared designer assets.
//   - Isolate music routing with disposable test stems; production Pursuit/Danger may be empty.
//   - Inspect actual pooled cue identity, configured clips, gain/pitch and playback; retain legacy coverage.
// DEPENDENCIES:
//   Core; Player/Hunter; Session.Run; Camera/PostFX/Audio/HUD/Results/Input;
//   TagArena assembly, UI Toolkit, NUnit and Unity Test Framework. Rendering package
//   components are inspected read-only through reflection, preserving assemblies.
// USAGE NOTES:
//   Coordinator owns the exclusive Unity lease. Runtime-only initial spawn fields
//   arrange a stationary two-catch trial; the second scene disables Hunters before
//   its first tick. Hardware is excluded by the gameplay map device filter, which
//   is restored in finally. No simulated sightings, hits, direct gameplay ticks,
//   Presenter calls, scene saves or global clock changes are used. The late observer
//   samples after production Drivers; insufficient frame resolution is a failure
//   to measure, not a timing pass. AudioSource data is not device audibility or human
//   perception. The second free scene recording remains incomplete at teardown.
//   Detach old scene observers in sceneLoaded before fresh initial health is
//   published by Start; retain the independent canonical capture subscription.
//   The actual comfort slide explicitly holds Sprint to reach its unchanged speed gate.
//   Bind canonical persistent services at readiness for tick-zero input isolation;
//   enforce exact global uniqueness after their deferred-destruction frame.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Audio;
using Worsen.Presentation.Camera;
using Worsen.Presentation.HUD;
using Worsen.Presentation.Input;
using Worsen.Presentation.PostFX;
using Worsen.Presentation.Results;
using Worsen.Session.Run;
using Worsen.Tests.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Camera
{
    public sealed class FeedbackRoutingIntegrationTests
    {
        private const string Arena = "Assets/Scenes/TagArena.unity";

        [UnityTest]
        public IEnumerator NativePursuitRoutesFeedbackAndRestartRestoresDefaultsBeforeComfortChanges()
        {
            yield return new EnterPlayMode();
            yield return Exercise();
        }

        private static IEnumerator Exercise()
        {
            bool background = Application.runInBackground;
            var trace = new CaptureGateTrace("FeedbackRouting");
            var trial = new Trial();
            Application.runInBackground = true;
            try
            {
                Assert.That(UnityEngine.Object.FindObjectsByType<TagArenaSceneRoot>(FindObjectsSortMode.None), Is.Empty);
                yield return trace.AdmitStableGameViewFocus();
                SceneManager.sceneLoaded += trial.Loaded;
                var load = SceneManager.LoadSceneAsync(Arena, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null);
                yield return Until(() => trial.SceneCount == 1 && trial.Run != null, trial, "First scene readiness");
                yield return Until(() => trial.Deaths == 1, trial, "Two native lunge contacts and death");
                yield return Until(() => trial.DeathRendered && trial.LoopsSilent, trial, "Death camera and layer fade");
                trial.AssertChase();
                trial.RestartRequested = true;
                var button = One<ResultsManager>().GetComponent<UIDocument>().rootVisualElement.Q<Button>("restart-button");
                Assert.That(button, Is.Not.Null);
                Assert.That(button.panel != null && button.enabledInHierarchy, Is.True);
                using (var submit = NavigationSubmitEvent.GetPooled())
                { submit.target = button; button.SendEvent(submit); }
                yield return Until(() => trial.SceneCount == 2 && trial.FreshFrames >= 4, trial, "Results routed restart");
                trial.AssertReset();
                trial.BeginLookCycle(false);
                yield return Until(() => trial.CycleDone, trial, "Default LookBack easing and return blur");
                trial.AssertLookCycle();
                trial.InstallComfortClones();
                yield return Until(() => trial.CloneFrames >= 4, trial, "Comfort settings reach the actual camera");
                trial.BeginLookCycle(true);
                yield return Until(() => trial.CycleDone, trial, "Comfort LookBack easing with blur disabled");
                trial.AssertLookCycle();
                trial.BeginSlide();
                yield return Until(() => trial.SlideFrames >= 4, trial, "Actual slide with tilt disabled");
                trial.AssertComfort();
                Assert.That(trial.Failure, Is.Empty);
                TestContext.WriteLine(trial.Describe());
                trace.Mark("feedback integration complete; first recording=" + trial.FirstRecording);
            }
            finally
            {
                SceneManager.sceneLoaded -= trial.Loaded;
                TagArenaSceneRoot.SceneReady -= trial.Ready;
                trial.Dispose();
                trace.Dispose();
                Application.runInBackground = background;
            }
        }

        private sealed class Trial : IDisposable
        {
            public RunSessionManager Run;
            private InputManager input;
            private PlayerManager player;
            private HunterManager hunter;
            private CameraManager camera;
            private CameraDriver cameraDriver;
            private PostFXManager postFX;
            private PostFXDriver postDriver;
            private AudioDriver audio;
            private HUDDriver hud;
            private UnityEngine.Camera output;
            private CameraDriverConfig cameraConfig, cameraOriginal, cameraClone;
            private PostFXDriverConfig postConfig, postOriginal, postClone;
            private AudioDriverConfig audioConfig, audioOriginal, audioClone;
            private AudioSoundscapeDriverConfig soundscapeOriginal, soundscapeClone;
            private readonly List<AudioClip> musicClips = new List<AudioClip>();
            private string audioJson, soundscapeJson;
            private InputActionMap gameplay;
            private ReadOnlyArray<InputDevice>? previousDevices;
            private FeedbackFrameObserver observer;
            private string cameraJson, postJson;
            private int inputId, readyFrame;
            private bool persistentUnique;
            private int oldCameraId, oldPostId, audioId, starts, contacts, hits;
            private int proximityFrames, injuryFrames, detectionFrames, impulseFrames, detectionCleared;
            private int attackFrames, decayFrames;
            private float elapsedFromChase = -1f, peakFov, maxFrame;
            private float detectionMaxFrame, detectionMaximumError;
            private float proximity, health = 100f;
            private Vector3 killer;
            private bool returnedCapture;
            private readonly List<float> healthChanges = new List<float>();
            private readonly List<string> events = new List<string>();
            private RunSessionManager captureRun;
            private readonly List<float> lookTimes = new List<float>();
            private InputButtons previousHeld;
            private bool requestedBack, committedBack, lookActive, comfortCycle, returning, sawMidpoint, sawBlur;
            private bool sliding, slideCommitted;
            private bool cloneReceivedMovement;
            private float lookElapsed, lookMaxFrame, lookPreviousYaw;
            private long lookStartTick;
            private int lookStartFrame;
            public int SceneCount, Deaths, FreshFrames, CloneFrames, SlideFrames;
            public bool RestartRequested, DeathRendered, LoopsSilent, CycleDone;
            public string FirstRecording = "", Failure = "";

            public void Loaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != Arena) return;
                Guard(() =>
                {
                    // The previous Player has already been destroyed. Its observers
                    // must retire before the new scene publishes initial health.
                    Detach();
                    var root = One<TagArenaSceneRoot>();
                    Write(root, "_spawnPosition", new Vector3(-20f, 0f, 0f));
                    Write(root, "_hunterSpawnPosition", new Vector3(-13f, 0f, 0f));
                    // Register after scene router OnEnable subscriptions, so capture
                    // and initial health publication precede our first-tick observer.
                    TagArenaSceneRoot.SceneReady -= Ready;
                    TagArenaSceneRoot.SceneReady += Ready;
                    if (captureRun == null)
                    {
                        captureRun = RunSessionManager.Instance ?? (RunSessionManager)Read(root, "_run");
                        Assert.That(captureRun, Is.Not.Null);
                        captureRun.CaptureStarted += CaptureStarted;
                    }
                });
            }

            public void Ready(SceneKey scene)
            {
                if (scene != SceneKey.TagArena) return;
                Guard(() =>
                {
                    Detach();
                    SceneCount++;
                    Run = RunSessionManager.Instance; input = InputManager.Instance;
                    var audioOwner = AudioManager.Instance;
                    Assert.That(Run != null && input != null && audioOwner != null, Is.True);
                    Assert.That(Run.isActiveAndEnabled && input.isActiveAndEnabled && audioOwner.isActiveAndEnabled, Is.True);
                    var root = One<TagArenaSceneRoot>();
                    Assert.That(Read(root, "_run"), Is.SameAs(Run));
                    Assert.That(Read(root, "_input"), Is.SameAs(input));
                    Assert.That(Read(root, "_audio"), Is.SameAs(audioOwner));
                    Assert.That(Run, Is.SameAs(captureRun));
                    player = One<PlayerManager>(); hunter = One<HunterManager>();
                    camera = One<CameraManager>(); cameraDriver = camera.GetComponent<CameraDriver>();
                    postFX = One<PostFXManager>(); postDriver = postFX.GetComponent<PostFXDriver>();
                    audio = audioOwner.GetComponent<AudioDriver>(); hud = One<HUDDriver>(); output = UnityEngine.Camera.main;
                    Assert.That(audio, Is.Not.Null);
                    Assert.That(Read(audioOwner, "_driver"), Is.SameAs(audio));
                    readyFrame = Time.frameCount; persistentUnique = false;
                    events.Add("ready scene=" + SceneCount + " frame=" + readyFrame + " Run/Input/Audio counts=" +
                        UnityEngine.Object.FindObjectsByType<RunSessionManager>(FindObjectsSortMode.None).Length + "/" +
                        UnityEngine.Object.FindObjectsByType<InputManager>(FindObjectsSortMode.None).Length + "/" +
                        UnityEngine.Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None).Length);
                    cameraConfig = (CameraDriverConfig)Read(camera, "_config");
                    postConfig = (PostFXDriverConfig)Read(postFX, "_config");
                    audioConfig = (AudioDriverConfig)Read(audio, "_config");
                    if (SceneCount == 1) InstallMusicFixture();
                    Assert.That(Run.Tick, Is.Zero);
                    Assert.That(camera.IsReady && postFX.IsReady && audio.IsInitialized, Is.True);
                    Assert.That(output, Is.Not.Null);
                    Assert.That(cameraConfig.HorizontalFieldOfView, Is.EqualTo(95f));
                    Assert.That(cameraConfig.DetectionFieldOfView, Is.EqualTo(12f));
                    Assert.That(cameraConfig.DetectionAttackSeconds, Is.EqualTo(0.08f));
                    Assert.That(cameraConfig.DetectionDecaySeconds, Is.EqualTo(0.4f));
                    Assert.That(cameraConfig.LookBackSeconds, Is.EqualTo(0.12f));
                    Assert.That(cameraConfig.LookForwardSeconds, Is.EqualTo(0.15f));
                    AssertVolumeWiring();
                    if (SceneCount == 1)
                    {
                        oldCameraId = camera.GetInstanceID(); oldPostId = postFX.GetInstanceID(); audioId = audio.GetInstanceID();
                        inputId = input.GetInstanceID();
                        cameraOriginal = cameraConfig; postOriginal = postConfig;
                        cameraJson = JsonUtility.ToJson(cameraOriginal); postJson = JsonUtility.ToJson(postOriginal);
                    }
                    else
                    {
                        Assert.That(RestartRequested && SceneCount == 2, Is.True);
                        Assert.That(camera.GetInstanceID(), Is.Not.EqualTo(oldCameraId));
                        Assert.That(postFX.GetInstanceID(), Is.Not.EqualTo(oldPostId));
                        Assert.That(audio.GetInstanceID(), Is.EqualTo(audioId));
                        Assert.That(input.GetInstanceID(), Is.EqualTo(inputId));
                        hunter.gameObject.SetActive(false);
                        Assert.That(HunterRegistry.Items.Count, Is.Zero);
                    }
                    gameplay = (InputActionMap)Read(input.GetComponent<PlayerInputDriver>(), "_actions");
                    previousDevices = gameplay.devices.HasValue
                        ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray()) : (ReadOnlyArray<InputDevice>?)null;
                    gameplay.devices = Array.Empty<InputDevice>();
                    Assert.That(gameplay.actions.All(action => action.controls.Count == 0), Is.True);
                    Assert.That(input.SetSource(InputSource.Live), Is.True);
                    input.FramePublished += Produce;
                    Run.ChaseStarted += Started; Run.HealthChanged += Injured; Run.PlayerDied += Died;
                    Run.ProximityPublished += Proximity; Run.PlayerMovementPublished += Movement;
                    Run.CaptureEnded += CaptureEnded;
                    hunter.OnLungeHit += Hit;
                    hunter.GetComponent<HunterDriver>().OnLungeContact += Contact;
                    observer = new GameObject("Feedback late-frame observation").AddComponent<FeedbackFrameObserver>();
                    observer.Sample = () => Guard(Observe);
                });
            }

            private void Produce(InputFrame produced)
            {
                Guard(() =>
                {
                    Assert.That(produced.Equals(default(InputFrame)), Is.True, "Hardware filter must produce neutral input.");
                    InputButtons held = requestedBack ? InputButtons.LookBack : InputButtons.None;
                    if (sliding) held |= InputButtons.Sprint;
                    if (sliding && player.ReadOnlyState.Velocity.magnitude >= 7.9f) held |= InputButtons.Crouch;
                    if (slideCommitted) held |= InputButtons.Crouch;
                    Vector2 move = sliding ? Vector2.up : Vector2.zero;
                    Vector2 look = requestedBack && (previousHeld & InputButtons.LookBack) == 0 ? new Vector2(160f, 0f) : Vector2.zero;
                    var frame = new InputFrame(move, look, held, held & ~previousHeld, previousHeld & ~held);
                    previousHeld = held;
                    Run.ReceiveInput(frame);
                });
            }

            private void Started(ChaseFact fact) => Guard(() =>
            {
                starts++; elapsedFromChase = 0f;
                Assert.That(fact.Phase, Is.EqualTo(ChasePhase.Confirmed));
                Assert.That(fact.Player, Is.EqualTo(player.Id));
                Assert.That(fact.Hunter, Is.EqualTo(hunter.Id));
                var extra = hud.GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("hud-extra");
                Assert.That(extra.style.display.value, Is.EqualTo(DisplayStyle.None));
                AssertCue(CueId.Detection);
                events.Add("detection tick=" + fact.Tick + " frame=" + Time.frameCount);
            });
            private void Proximity(ProximitySample sample) { proximity = sample.Closeness; }
            private void Injured(EntityId id, float value, float maximum) => Guard(() =>
            {
                Assert.That(id, Is.EqualTo(player.Id)); Assert.That(maximum, Is.EqualTo(100f));
                health = value; healthChanges.Add(value);
                events.Add("health=" + value + " tick=" + Run.Tick + " frame=" + Time.frameCount);
            });
            private void Hit(HunterHit hit) { hits++; }
            private void Contact(Collider collider) { if (collider.GetComponentInParent<PlayerManager>() == player) contacts++; }
            private void Died(EntityId id, Vector3 position) => Guard(() =>
            {
                Deaths++; killer = position;
                Assert.That(id, Is.EqualTo(player.Id)); Assert.That(hits, Is.EqualTo(2));
                AssertCue(CueId.Death);
                events.Add("death tick=" + Run.Tick + " frame=" + Time.frameCount);
            });
            private void CaptureEnded(long tick, bool complete)
            { if (SceneCount == 1) { returnedCapture = complete; FirstRecording = input.LastRecordingPath; } }
            private void CaptureStarted(RunCaptureMetadata metadata)
            { events.Add("capture=" + metadata.SessionId + " seed=" + metadata.Seed + " startTick=" + metadata.StartTick +
                " fixedDt=" + metadata.FixedDeltaTime + " source=" + metadata.SourceRevision + " config=" + metadata.ConfigSnapshotHash +
                " frame=" + Time.frameCount); }
            private void Movement(PlayerMovementSample sample)
            {
                if (SceneCount != 2) return;
                if (cameraClone != null) cloneReceivedMovement = true;
                if (sliding && sample.MovementState == MovementState.Slide) slideCommitted = true;
                if (!lookActive || committedBack == sample.LookBack) return;
                committedBack = sample.LookBack;
                lookElapsed = lookMaxFrame = 0f; lookPreviousYaw = committedBack ? 0f : 160f;
                sawMidpoint = false; returning = !committedBack;
                lookStartTick = sample.Tick; lookStartFrame = Time.frameCount;
            }

            private void Observe()
            {
                if (output == null || Failure.Length > 0) return;
                if (!persistentUnique && Time.frameCount > readyFrame)
                {
                    // This frame follows Start's deferred Destroy boundary. Pending
                    // duplicates were never accepted as a settled singleton state.
                    Assert.That(One<RunSessionManager>(), Is.SameAs(Run));
                    Assert.That(One<InputManager>(), Is.SameAs(input));
                    Assert.That(One<AudioManager>(), Is.SameAs(AudioManager.Instance));
                    Assert.That(One<AudioDriver>(), Is.SameAs(audio));
                    Assert.That(One<PlayerInputDriver>(), Is.SameAs(input.GetComponent<PlayerInputDriver>()));
                    persistentUnique = true;
                    events.Add("settled scene=" + SceneCount + " frame=" + Time.frameCount +
                        " Run/Input/Audio/AudioDriver/PlayerInputDriver counts=1/1/1/1/1 canonicalIDs=" +
                        Run.GetInstanceID() + "/" + input.GetInstanceID() + "/" + AudioManager.Instance.GetInstanceID());
                }
                Assert.That(Application.isFocused, Is.True, "Actual focus interrupted presentation observation.");
                float dt = Time.deltaTime;
                maxFrame = Mathf.Max(maxFrame, dt);
                Assert.That(dt > 0f && !float.IsNaN(dt), Is.True);
                float horizontal = HorizontalFov();
                if (SceneCount == 1)
                {
                    if (elapsedFromChase >= 0f)
                    {
                        elapsedFromChase += dt;
                        peakFov = Mathf.Max(peakFov, horizontal);
                        Assert.That(horizontal, Is.InRange(94.99f, 107.01f));
                        if (Deaths == 0)
                        {
                            // Normative +12 degrees over 0.08 s, then 0.4 s decay.
                            // Elapsed is independently accumulated from actual frames
                            // starting with the late frame containing the real event.
                            float kick = elapsedFromChase <= 0.08f ? 12f * elapsedFromChase / 0.08f
                                : 12f * Mathf.Clamp01(1f - (elapsedFromChase - 0.08f) / 0.4f);
                            float error = Mathf.Abs(horizontal - (95f + kick));
                            detectionMaximumError = Mathf.Max(detectionMaximumError, error);
                            Assert.That(error, Is.LessThan(0.03f), "Actual camera lens differs from the declared detection envelope.");
                            if (elapsedFromChase <= 0.48f)
                            {
                                detectionMaxFrame = Mathf.Max(detectionMaxFrame, dt);
                                if (elapsedFromChase <= 0.08f) attackFrames++; else decayFrames++;
                            }
                        }
                        if (horizontal > 96f) detectionFrames++;
                        if (elapsedFromChase > 0.48f + 2f * maxFrame && Deaths == 0)
                        { Assert.That(horizontal, Is.EqualTo(95f).Within(0.02f)); detectionCleared++; }
                        object rigState = Read(Read(cameraDriver, "_rig"), "State");
                        if (((Vector3)Read(rigState, "PositionCorrection")).sqrMagnitude > 0.00000001f) impulseFrames++;
                    }
                    if (proximity > 0f && Deaths == 0)
                    {
                        Assert.That(Effect("_chromatic", "intensity"), Is.EqualTo(proximity * postConfig.PeripheralChromatic).Within(0.0001f));
                        Assert.That(Effect("_distortion", "intensity"), Is.EqualTo(-proximity * postConfig.PeripheralDistortion).Within(0.0001f));
                        if (ThreatLayerPlaying() && ((AudioSource)Read(audio, "_breath")).isPlaying && ((AudioSource)Read(audio, "_breath")).volume > 0f)
                            proximityFrames++;
                    }
                    Assert.That(Effect("_vignette", "intensity"), Is.EqualTo((1f - health / 100f) * postConfig.InjuryVignette).Within(0.0001f));
                    if (health == 50f) injuryFrames++;
                    if (Deaths == 1)
                    {
                        Assert.That(Vector3.Angle(output.transform.forward, killer - player.LastMovementSample.EyePosition), Is.LessThan(0.1f));
                        Assert.That((float)Read(Read(cameraDriver, "_listener"), "Gain"), Is.Zero);
                        DeathRendered = true;
                        LoopsSilent = ThreatAndGameplayLoopsSilent() && ((AudioSource)Read(audio, "_breath")).volume == 0f;
                    }
                    return;
                }
                if (player.LastMovementSample.Tick <= 0) return;
                FreshFrames++;
                if (cameraClone != null && cloneReceivedMovement)
                {
                    CloneFrames++;
                    float speed = new Vector2(player.ReadOnlyState.Velocity.x, player.ReadOnlyState.Velocity.z).magnitude;
                    Assert.That(horizontal, Is.EqualTo(80f + cameraClone.SpeedFieldOfView * Mathf.Clamp01(speed / cameraClone.MaxDesignSpeed)).Within(0.03f));
                    Assert.That((float)Read(Read(cameraDriver, "_listener"), "Gain"), Is.Zero);
                    Assert.That((bool)Read(Read(postDriver, "_blur"), "active"), Is.False);
                    if (slideCommitted && player.ReadOnlyState.MovementState == MovementState.Slide)
                    { Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, output.transform.eulerAngles.z)), Is.LessThan(0.1f)); SlideFrames++; }
                }
                if (!lookActive || committedBack != requestedBack) return;
                lookElapsed += dt; lookMaxFrame = Mathf.Max(lookMaxFrame, dt);
                float yaw = Mathf.DeltaAngle(player.ReadOnlyState.HeadingDegrees,
                    Mathf.Atan2(output.transform.forward.x, output.transform.forward.z) * Mathf.Rad2Deg);
                Assert.That(yaw, Is.InRange(-0.05f, 160.05f));
                Assert.That(returning ? yaw <= lookPreviousYaw + 0.05f : yaw >= lookPreviousYaw - 0.05f, Is.True);
                if (yaw > 10f && yaw < 150f) sawMidpoint = true;
                lookPreviousYaw = yaw;
                bool blur = (bool)Read(Read(postDriver, "_blur"), "active");
                if (returning && blur) sawBlur = true;
                bool endpoint = returning ? yaw < 0.02f : Mathf.Abs(yaw - 160f) < 0.02f;
                if (!endpoint) return;
                float duration = returning ? 0.15f : 0.12f;
                Assert.That(lookMaxFrame, Is.LessThanOrEqualTo(0.08f), "Insufficient frame resolution for easing measurement.");
                if (returning) Assert.That(sawMidpoint, Is.True, "Release must ease from the mouse-directed view.");
                if (returning) Assert.That(lookElapsed, Is.InRange(duration - 0.01f, duration + lookMaxFrame + 0.001f));
                events.Add((returning ? "return" : "lookback") + " comfort=" + comfortCycle + " startTick=" + lookStartTick +
                    " startFrame=" + lookStartFrame + " endFrame=" + Time.frameCount + " sampledDt=" + lookElapsed + " maxFrame=" + lookMaxFrame);
                lookTimes.Add(lookElapsed);
                if (!returning) requestedBack = false;
                else { lookActive = false; CycleDone = true; }
            }

            public void AssertChase()
            {
                Assert.That(persistentUnique, Is.True, "First scene must pass post-destruction singleton checks.");
                Assert.That(starts, Is.EqualTo(2)); Assert.That(hits, Is.EqualTo(2));
                Assert.That(contacts, Is.GreaterThanOrEqualTo(2));
                Assert.That(healthChanges, Is.EqualTo(new[] { 50f, 0f }));
                Assert.That(detectionFrames > 0 && impulseFrames > 0 && detectionCleared > 0, Is.True,
                    "Detection must move the actual lens, apply a Cinemachine correction and decay before death. " + Describe());
                Assert.That(attackFrames > 0 && decayFrames > 0, Is.True, "Both detection timing phases need actual rendered samples.");
                Assert.That(detectionMaxFrame, Is.LessThanOrEqualTo(0.08f), "Insufficient frame resolution for detection timing.");
                Assert.That(proximityFrames, Is.GreaterThan(0)); Assert.That(injuryFrames, Is.GreaterThan(0));
                Assert.That(returnedCapture, Is.True); Assert.That(input.LastRecordingError, Is.Empty);
                FirstRecording = input.LastRecordingPath;
                Assert.That(Run.Phase, Is.EqualTo(RunPhase.Ended));
                AssertUnchanged();
            }
            public void AssertReset()
            {
                Assert.That(persistentUnique, Is.True, "Restart must pass post-destruction singleton checks.");
                Assert.That(SceneCount, Is.EqualTo(2)); Assert.That(player.ReadOnlyState.Health, Is.EqualTo(100f));
                Assert.That(HorizontalFov(), Is.EqualTo(95f).Within(0.02f));
                Assert.That(Vector3.Angle(output.transform.forward, Vector3.right), Is.LessThan(0.1f));
                foreach (string field in new[] { "_chromatic", "_distortion", "_vignette", "_grain" })
                    Assert.That(Effect(field, "intensity"), Is.Zero);
                Assert.That(Effect("_color", "saturation"), Is.Zero);
                Assert.That(ThreatAndGameplayLoopsSilent(), Is.True, "Restart must clear threat music and gameplay loop playback.");
                if (audioConfig.Soundscape != null)
                {
                    var soundscape = (AudioSoundscapeDriver)Read(audio, "_soundscape");
                    Assert.That(((AudioSource[])Read(soundscape, "_voices")).All(source => !source.isPlaying), Is.True, "Restart must stop every pooled effect.");
                    Assert.That(((AudioSoundscapeDriverState)Read(soundscape, "_state")).Voices.All(voice => voice.Remaining == 0f), Is.True, "Restart must release every effect voice lease.");
                }
                Assert.That(((AudioSource)Read(audio, "_breath")).volume, Is.Zero);
                Assert.That(((AudioSource[])Read(audio, "_cueSources")).All(source => !source.isPlaying && source.volume == 0f), Is.True);
                Assert.That(hud.GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("hud-extra").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(((CameraDriverState)Read(cameraDriver, "_state")).DeathSnapped, Is.False);
            }
            public void BeginLookCycle(bool comfort)
            {
                comfortCycle = comfort; CycleDone = false; lookActive = true;
                requestedBack = true; committedBack = false; returning = false; sawBlur = false;
            }
            public void AssertLookCycle()
            {
                Assert.That(lookTimes.Count, Is.EqualTo(comfortCycle ? 4 : 2));
                Assert.That(sawBlur, Is.EqualTo(!comfortCycle));
                Assert.That(player.ReadOnlyState.LookBack, Is.False);
                Assert.That((bool)Read(Read(postDriver, "_blur"), "active"), Is.False);
            }
            public void InstallComfortClones()
            {
                cameraClone = UnityEngine.Object.Instantiate(cameraOriginal);
                postClone = UnityEngine.Object.Instantiate(postOriginal);
                cameraClone.name = "Feedback test comfort settings"; postClone.name = "Feedback test blur disabled";
                Write(cameraClone, "_horizontalFieldOfView", 80f);
                Write(cameraClone, "_punchIntensity", 0f); Write(cameraClone, "_tiltEnabled", false);
                Write(postClone, "_reacquireBlurEnabled", false);
                camera.Teardown(); postFX.Teardown();
                Write(camera, "_config", cameraClone); Write(postFX, "_config", postClone);
                camera.Initialize(); postFX.Initialize(); cameraConfig = cameraClone; postConfig = postClone;
                Assert.That(camera.IsReady && postFX.IsReady, Is.True);
            }
            public void BeginSlide() { sliding = true; }
            public void AssertComfort()
            {
                Assert.That(slideCommitted && SlideFrames >= 4, Is.True);
                Assert.That(player.LastTraversalFacts.Any(fact => fact.Kind == TraversalKind.Slide && fact.Succeeded) ||
                    player.ReadOnlyState.MovementState == MovementState.Slide, Is.True);
                AssertUnchanged();
            }
            private void AssertUnchanged()
            { Assert.That(JsonUtility.ToJson(cameraOriginal), Is.EqualTo(cameraJson)); Assert.That(JsonUtility.ToJson(postOriginal), Is.EqualTo(postJson)); }
            private void InstallMusicFixture()
            {
                // Keep scene wiring and production cue banks; only fixture-owned music
                // slots use generated non-silent clips for gain/playback assertions.
                if (audioConfig.Soundscape == null) return;
                var originalDriver = (AudioSoundscapeDriver)Read(audio, "_soundscape");
                var originalLayers = (AudioSource[])Read(originalDriver, "_layers");
                AudioClip[] productionMusic = { audioConfig.Soundscape.TensionStem, audioConfig.Soundscape.ChaseStem, audioConfig.Soundscape.DangerStem };
                for (int i = 0; i < productionMusic.Length; i++)
                    if (productionMusic[i] == null)
                    {
                        Assert.That(originalLayers[i].clip, Is.Null, "Empty production slot must not receive a fallback clip.");
                        Assert.That(originalLayers[i].isPlaying, Is.False, "Empty production slot must remain silent.");
                    }
                audioOriginal = audioConfig; soundscapeOriginal = audioConfig.Soundscape;
                audioJson = JsonUtility.ToJson(audioOriginal);
                soundscapeJson = JsonUtility.ToJson(soundscapeOriginal);
                audioClone = UnityEngine.Object.Instantiate(audioOriginal);
                soundscapeClone = UnityEngine.Object.Instantiate(soundscapeOriginal);
                audioClone.name = "Feedback test audio config";
                soundscapeClone.name = "Feedback test soundscape";
                string[] slots = { "_tensionStem", "_chaseStem", "_dangerStem" };
                int[] frequencies = { 64, 83, 107 };
                const int sampleRate = 48000;
                for (int i = 0; i < slots.Length; i++)
                {
                    var clip = AudioClip.Create("Feedback test music " + i, sampleRate, 1, sampleRate, false);
                    musicClips.Add(clip);
                    var samples = new float[sampleRate];
                    for (int frame = 0; frame < samples.Length; frame++)
                        samples[frame] = .01f * Mathf.Sin(2f * Mathf.PI * frequencies[i] * frame / sampleRate);
                    Assert.That(clip.SetData(samples, 0), Is.True);
                    Write(soundscapeClone, slots[i], clip);
                }
                Write(audioClone, "_soundscape", soundscapeClone);
                audio.Teardown(); audio.Initialize(audioClone);
                audio.SetOwnerEnabled(AudioManager.Instance.isActiveAndEnabled);
                audioConfig = audioClone;
            }
            private AudioSource[] RichLayers()
            {
                var driver = (AudioSoundscapeDriver)Read(audio, "_soundscape");
                Assert.That(driver, Is.Not.Null);
                var layers = (AudioSource[])Read(driver, "_layers");
                Assert.That(layers.Length, Is.EqualTo(5));
                AudioClip[] expected = { audioConfig.Soundscape.TensionStem, audioConfig.Soundscape.ChaseStem, audioConfig.Soundscape.DangerStem };
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(expected[i], Is.Not.Null, "Trial requires its three temporary music stems.");
                    Assert.That(layers[i].clip, Is.SameAs(expected[i]));
                    Assert.That(layers[i].loop, Is.True);
                    Assert.That(layers[i].outputAudioMixerGroup, Is.EqualTo(audioConfig.Soundscape.MusicGroup));
                }
                return layers;
            }
            private bool ThreatLayerPlaying()
            {
                if (audioConfig.Soundscape == null)
                { var legacy = (AudioSource)Read(audio, "_hunter"); return legacy.isPlaying && legacy.volume > 0f; }
                var layers = RichLayers();
                return layers[1].isPlaying && layers[1].volume > 0f;
            }
            private bool ThreatAndGameplayLoopsSilent()
            {
                if (audioConfig.Soundscape == null) return ((AudioSource)Read(audio, "_hunter")).volume == 0f;
                var driver = (AudioSoundscapeDriver)Read(audio, "_soundscape");
                var sources = (AudioSource[])Read(driver, "_voices");
                var voices = ((AudioSoundscapeDriverState)Read(driver, "_state")).Voices;
                for (int i = 0; i < voices.Length; i++)
                {
                    if (!voices[i].Loop) continue;
                    var bank = audioConfig.Soundscape.Sounds.Single(entry => (int)entry.Cue == voices[i].Cue);
                    if (!bank.Ambience && (voices[i].Remaining > 0f || sources[i].isPlaying)) return false;
                }
                return RichLayers().Take(3).All(source => source.volume == 0f);
            }
            private void AssertCue(CueId cue)
            {
                if (audioConfig.Soundscape != null)
                {
                    var config = audioConfig.Soundscape;
                    var bank = config.Sounds.Single(entry => entry.Cue == cue);
                    var driver = (AudioSoundscapeDriver)Read(audio, "_soundscape");
                    Assert.That(driver, Is.Not.Null, "Configured soundscape driver is missing.");
                    var voices = ((AudioSoundscapeDriverState)Read(driver, "_state")).Voices;
                    var pooledSources = (AudioSource[])Read(driver, "_voices");
                    Assert.That(pooledSources.Length, Is.EqualTo(voices.Length));
                    float maximumGain = Mathf.Clamp01(bank.Gain) * audioConfig.MasterGain *
                        (bank.Ambience ? config.AmbienceGain : config.EffectsGain);
                    float minimumGain = maximumGain * (1f - Mathf.Clamp01(bank.GainVariation));
                    var active = Enumerable.Range(0, voices.Length).Where(index => voices[index].Cue == (int)cue &&
                        voices[index].Remaining > 0f && pooledSources[index].isPlaying).ToArray();
                    Assert.That(active, Is.Not.Empty, "Actual pooled cue source did not start: " + cue);
                    foreach (int index in active)
                    {
                        var source = pooledSources[index];
                        Assert.That(source.clip, Is.Not.Null);
                        Assert.That(bank.Clips, Does.Contain(source.clip), "Playing clip must belong to the requested cue bank.");
                        Assert.That(source.volume, Is.GreaterThan(0f));
                        Assert.That(source.volume, Is.InRange(minimumGain - 0.0001f, maximumGain + 0.0001f));
                        Assert.That(source.pitch, Is.InRange(bank.PitchMinimum - 0.0001f, bank.PitchMaximum + 0.0001f));
                        Assert.That(source.loop, Is.EqualTo(bank.Loop));
                        Assert.That(source.spatialBlend, Is.EqualTo(bank.Spatial ? 1f : 0f));
                        Assert.That(source.outputAudioMixerGroup, Is.EqualTo(bank.Ambience ? config.AmbienceGroup : config.EffectsGroup));
                    }
                    return;
                }
                var definition = audioConfig.Cues.Single(entry => entry.Cue == cue);
                var sources = (AudioSource[])Read(audio, "_cueSources");
                Assert.That(sources.Any(source => source.clip == definition.Clip && source.isPlaying &&
                    Mathf.Abs(source.volume - definition.Gain * audioConfig.MasterGain) < 0.0001f), Is.True, "Actual cue source did not start: " + cue);
            }
            private void AssertVolumeWiring()
            {
                var volume = (Behaviour)Read(postDriver, "_volume");
                Assert.That(volume.isActiveAndEnabled, Is.True);
                Assert.That(Read(volume, "m_InternalProfile"), Is.SameAs(Read(postDriver, "_profile")));
                var data = output.GetComponents<Component>().Single(component => component != null &&
                    component.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
                Assert.That(Read(data, "renderPostProcessing"), Is.EqualTo(true));
                Assert.That(((LayerMask)Read(data, "volumeLayerMask")).value & (1 << volume.gameObject.layer), Is.Not.Zero);
            }
            private float Effect(string component, string parameter) => (float)Read(Read(Read(postDriver, component), parameter), "value");
            private float HorizontalFov() => 2f * Mathf.Atan(Mathf.Tan(output.fieldOfView * Mathf.Deg2Rad / 2f) * output.aspect) * Mathf.Rad2Deg;
            private void Guard(Action action) { try { action(); } catch (Exception error) { if (Failure.Length == 0) Failure = error.ToString(); } }
            public string Describe() => "Feedback sceneCount=" + SceneCount + " starts/hits/contacts=" + starts + "/" + hits + "/" + contacts +
                " detection/impulse/cleared=" + detectionFrames + "/" + impulseFrames + "/" + detectionCleared + " peakHorizontalFov=" + peakFov +
                " attack/decaySamples=" + attackFrames + "/" + decayFrames + " detectionMaxFrame/error=" + detectionMaxFrame + "/" + detectionMaximumError +
                " maxFrame=" + maxFrame + " proximity/injuryFrames=" + proximityFrames + "/" + injuryFrames + " events=[" + string.Join("; ", events) +
                "]; complete first input=" + FirstRecording + "; actual source/component integration only; no audio-device, perception or human-comfort claim.";
            private void Detach()
            {
                if (observer != null) { observer.Sample = null; UnityEngine.Object.Destroy(observer.gameObject); observer = null; }
                if (input != null) input.FramePublished -= Produce;
                if (gameplay != null) { gameplay.devices = previousDevices; gameplay = null; }
                if (Run != null)
                {
                    Run.ChaseStarted -= Started; Run.HealthChanged -= Injured; Run.PlayerDied -= Died;
                    Run.ProximityPublished -= Proximity; Run.PlayerMovementPublished -= Movement; Run.CaptureEnded -= CaptureEnded;
                }
                if (hunter != null)
                { hunter.OnLungeHit -= Hit; hunter.GetComponent<HunterDriver>().OnLungeContact -= Contact; }
            }
            public void Dispose()
            {
                Detach();
                if (captureRun != null) captureRun.CaptureStarted -= CaptureStarted;
                if (audioClone != null)
                {
                    try
                    {
                        Assert.That(JsonUtility.ToJson(audioOriginal), Is.EqualTo(audioJson));
                        Assert.That(JsonUtility.ToJson(soundscapeOriginal), Is.EqualTo(soundscapeJson));
                    }
                    finally
                    {
                        if (audio != null)
                        {
                            audio.Teardown(); audio.Initialize(audioOriginal);
                            audio.SetOwnerEnabled(AudioManager.Instance != null && AudioManager.Instance.isActiveAndEnabled);
                        }
                        UnityEngine.Object.Destroy(audioClone);
                        UnityEngine.Object.Destroy(soundscapeClone);
                        foreach (AudioClip clip in musicClips) UnityEngine.Object.Destroy(clip);
                        musicClips.Clear();
                    }
                }
                if (cameraClone != null)
                {
                    if (camera != null) { camera.Teardown(); Write(camera, "_config", cameraOriginal); camera.Initialize(); }
                    UnityEngine.Object.Destroy(cameraClone);
                }
                if (postClone != null)
                {
                    if (postFX != null) { postFX.Teardown(); Write(postFX, "_config", postOriginal); postFX.Initialize(); }
                    UnityEngine.Object.Destroy(postClone);
                }
            }
        }

        private static object Read(object target, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, flags); if (field != null) return field.GetValue(target);
                var property = type.GetProperty(name, flags); if (property != null) return property.GetValue(target);
            }
            throw new InvalidOperationException("Missing inspected member " + target.GetType().Name + "." + name);
        }
        private static void Write(object target, string field, object value)
        {
            var member = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(member, Is.Not.Null, "Missing runtime setup member " + field); member.SetValue(target, value);
        }
        private static T One<T>() where T : Component
        {
            T[] values = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(values.Length, Is.EqualTo(1), typeof(T).Name + " must have one active instance."); return values[0];
        }
        private static IEnumerator Until(Func<bool> condition, Trial trial, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30.0;
            for (int frame = 0; frame < 12000 && !condition() && trial.Failure.Length == 0 && Time.realtimeSinceStartupAsDouble < deadline; frame++) yield return null;
            Assert.That(trial.Failure, Is.Empty, message + ": " + trial.Describe());
            Assert.That(condition(), Is.True, message + ": " + trial.Describe());
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }

    [DefaultExecutionOrder(32000)]
    public sealed class FeedbackFrameObserver : MonoBehaviour
    {
        public Action Sample;
        private void LateUpdate() => Sample?.Invoke();
    }
}
