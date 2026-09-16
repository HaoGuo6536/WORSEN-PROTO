// ============================================================================
// ChaseLossIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Observes actual Hunter sight rays and normal Run ticks across occlusion,
//   loss, grace reacquisition and one final escaped-chase event in TagArena.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Chase integration.
// KEY RESPONSIBILITIES:
//   - Verify physical occlusion with range/cone still eligible and distance >14 m.
//   - Measure confirmation/loss/grace in completed Session ticks and preserve identity.
//   - Record real poses, sight samples and phase facts without changing game rules.
//   - Observe exactly one mapped pooled or legacy Lose source and the half-second HUD fade.
// DEPENDENCIES:
//   - Core facts; Domain Player/Hunter/Chase; Session.Run; Input; TagArenaSceneRoot.
//   - Audio/HUD presentation and UI Toolkit; read-only observation of actual sources.
//   - Unity physics, UnityEditor serialized config reads, NUnit and Test Framework.
// USAGE NOTES:
//   Coordinator holds the Unity lease. Runtime-only initial spawn wiring places
//   Player=(-18,0,-2), Hunter=(8,0,-2) on authored TagArena floors. A temporary
//   one-metre containment fence prevents closing the gap; a temporary three-metre
//   screen toggles actual ray occlusion. Original configs/navigation are unchanged.
//   This arranged sensor/Session integration is not route skill, default geometry,
//   movement balance, a complete input recording or a 30-chase tuning dataset.
//   No direct simulation ticks, fake probes, runtime-state overrides or teleports.
//   Only the owned gameplay action map is isolated from devices; its prior nullable
//   filter is copied and restored. Live source reset clears buffered hardware edges.
//   Real input/focus/owner gates and fully neutral publication assertions remain.
//   runInBackground is restored in finally; teardown unloads temporary geometry.
//   A fresh nested iterator creates captured locals only after EnterPlayMode reload.
//   Lost grace must retain the reduced HUD and cannot emit the final Lose cue.
//   A late-frame observer samples after the real HUD Driver; frame-integrated timing
//   records its sample width and proves component behavior, not device audibility.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Input;
using Worsen.Presentation.Audio;
using Worsen.Presentation.HUD;
using Worsen.Session.Run;

namespace Worsen.Tests.Chase
{
    public sealed class ChaseLossIntegrationTests
    {
        private const string ArenaPath = "Assets/Scenes/TagArena.unity";
        private const float Dt = 1f / 60f;
        private static readonly Vector3 PlayerSpawn = new Vector3(-18f, 0f, -2f);
        private static readonly Vector3 HunterSpawn = new Vector3(8f, 0f, -2f);

        [UnityTest]
        public IEnumerator PhysicalOcclusionReacquiresWithinGraceThenEndsOnceAfterGraceExpires()
        {
            yield return new EnterPlayMode();
            // The runner restores the outer program counter after domain reload,
            // not captured local objects. Allocate the trial closure in a new iterator.
            yield return ExerciseArena();
        }

        private static IEnumerator ExerciseArena()
        {
            bool previousBackground = Application.runInBackground;
            var trial = new Trial();
            try
            {
                Application.runInBackground = true;
                Assert.That(UnityEngine.Object.FindObjectsByType<TagArenaSceneRoot>(FindObjectsSortMode.None), Is.Empty,
                    "Start from the Test Framework isolated bootstrap scene.");
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(Dt).Within(0.000001f));
                SceneManager.sceneLoaded += trial.Arrange;
                TestContext.Progress.WriteLine("Arranged physical LOS test: Player=(-18,0,-2), Hunter=(8,0,-2); " +
                    "authored floors, temporary 1 m containment fence, temporary 3 m occlusion screen, " +
                    "neutral Run input, original profiles. Not a movement/route/balance trial.");
                AsyncOperation load = SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, "Build TagArena before running this fixture.");
                yield return Until(() => trial.Ready || trial.Failure.Length > 0, 15f);
                Assert.That(trial.Failure, Is.Empty);
                Assert.That(load.isDone && trial.Ready, Is.True, "Arena readiness failed: " + trial.Describe());
                yield return Until(() => trial.Done || trial.Failure.Length > 0, 30f);
                Assert.That(trial.Failure, Is.Empty, trial.Describe());
                trial.AssertOutcome();
            }
            finally
            {
                SceneManager.sceneLoaded -= trial.Arrange;
                try { trial.Dispose(); }
                finally { Application.runInBackground = previousBackground; }
            }
        }

        private enum Stage { Establish, FirstLoss, Reacquire, FinalLoss, Grace, EndStability, Complete }

        private sealed class Trial : IDisposable
        {
            private RunSessionManager run;
            private InputManager inputService;
            private InputActionMap gameplay;
            private ReadOnlyArray<InputDevice>? previousDevices;
            private bool deviceFilterSaved;
            private PlayerManager player;
            private HunterManager hunter;
            private HunterDriver driver;
            private ChaseManager chase;
            private HunterProfile profile;
            private ChaseConfig config;
            private HunterMotorDriverConfig motor;
            private GameObject geometry;
            private BoxCollider screen;
            private readonly Dictionary<ScriptableObject, string> configSnapshots = new Dictionary<ScriptableObject, string>();
            private readonly List<ChaseFact> starts = new List<ChaseFact>();
            private readonly List<ChaseFact> phases = new List<ChaseFact>();
            private readonly List<ChaseFact> losses = new List<ChaseFact>();
            private readonly List<ChaseFact> ends = new List<ChaseFact>();
            private readonly List<HunterSighting> sightings = new List<HunterSighting>();
            private Stage stage;
            private long previousTick, noSightTick, reacquireSightTick, finalLossTick, endTick;
            private long firstLossTick, reacquiredTick;
            private int chaseId, readyCount, completedTicks, hits, initialFrame, neutralFrames;
            private double initialFixedTime;
            private float minimumDistance = float.PositiveInfinity;
            private bool pathAndMotionObserved;
            private RunSessionManager captureRun;
            private RunCaptureMetadata captureMetadata;
            private bool capturedMetadata;
            private AudioDriver audio;
            private AudioDriverConfig audioConfig;
            private HUDDriver hud;
            private HUDDriverConfig hudConfig;
            private VisualElement hudExtra;
            private Label hudCount, hudExit;
            private string initialCount, initialExit;
            private AudioSource[] cueSources;
            private AudioSoundscapeDriver soundscape;
            private AudioClip loseClip;
            private ChaseLossFeedbackObserver feedbackObserver;
            private int lossEndFrame, restoreFrame, feedbackSamples, graceFeedbackSamples;
            private float feedbackElapsed, maxFeedbackFrame, previousOpacity, restoreElapsed;
            private bool loseSourceObserved, restoreMidpoint, feedbackRestored;
            public bool Ready;
            public string Failure = "";
            public bool Done => stage == Stage.Complete;

            public void Arrange(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != ArenaPath) return;
                try
                {
                    TagArenaSceneRoot root = One<TagArenaSceneRoot>();
                    // sceneLoaded precedes Start: real factories initialize physics and
                    // state from these runtime-only poses. No entity state is overwritten.
                    SpawnField("_spawnPosition").SetValue(root, PlayerSpawn);
                    SpawnField("_hunterSpawnPosition").SetValue(root, HunterSpawn);
                    captureRun = RunSessionManager.Instance ?? (RunSessionManager)Read(root, "_run");
                    Assert.That(captureRun, Is.Not.Null);
                    captureRun.CaptureStarted += CaptureStarted;
                    var wiring = new SerializedObject(root);
                    profile = (HunterProfile)wiring.FindProperty("_hunterProfile").objectReferenceValue;
                    config = (ChaseConfig)wiring.FindProperty("_chaseConfig").objectReferenceValue;
                    Snapshot(profile); Snapshot(config);
                    Snapshot((PlayerProfile)wiring.FindProperty("_playerProfile").objectReferenceValue);
                    geometry = new GameObject("Chase Loss Test - Temporary Physical Arrangement");
                    SceneManager.MoveGameObjectToScene(geometry, scene);
                    // These low rails block the capsule but leave eye-to-target rays
                    // clear. They are physical test geometry, not navigation/config edits.
                    Box("West Low Fence", new Vector3(5f, 0.5f, -2f), new Vector3(0.4f, 1f, 7f));
                    Box("East Low Fence", new Vector3(11f, 0.5f, -2f), new Vector3(0.4f, 1f, 7f));
                    Box("North Low Fence", new Vector3(8f, 0.5f, 1.5f), new Vector3(6.4f, 1f, 0.4f));
                    Box("South Low Fence", new Vector3(8f, 0.5f, -5.5f), new Vector3(6.4f, 1f, 0.4f));
                    screen = Box("LOS Screen", new Vector3(-11f, 1.5f, -2f), new Vector3(0.4f, 3f, 4f));
                    screen.enabled = false;
                    Physics.SyncTransforms();
                    TagArenaSceneRoot.SceneReady += ObserveReady;
                }
                catch (Exception error) { Fail("Runtime arrangement: " + error); }
            }

            private void ObserveReady(SceneKey scene)
            {
                if (scene != SceneKey.TagArena) return;
                try
                {
                    readyCount++;
                    run = One<RunSessionManager>(); inputService = One<InputManager>(); player = One<PlayerManager>();
                    hunter = One<HunterManager>(); chase = One<ChaseManager>();
                    driver = hunter.GetComponent<HunterDriver>();
                    motor = (HunterMotorDriverConfig)new SerializedObject(driver).FindProperty("_config").objectReferenceValue;
                    Snapshot(motor);
                    AudioManager audioOwner = AudioManager.Instance;
                    Assert.That(audioOwner, Is.Not.Null);
                    audio = audioOwner.GetComponent<AudioDriver>();
                    Assert.That(audio, Is.Not.Null);
                    Assert.That(audio.IsInitialized, Is.True);
                    audioConfig = (AudioDriverConfig)Read(audio, "_config");
                    if (audioConfig.Soundscape != null)
                    {
                        soundscape = (AudioSoundscapeDriver)Read(audio, "_soundscape");
                        Assert.That(soundscape, Is.Not.Null);
                        cueSources = (AudioSource[])Read(soundscape, "_voices");
                        var bank = audioConfig.Soundscape.Sounds.Single(cue => cue.Cue == CueId.Lose);
                        loseClip = bank.Clips.FirstOrDefault(clip => clip != null);
                        Assert.That(cueSources.Length, Is.EqualTo(audioConfig.Soundscape.VoiceCount));
                        Snapshot(audioConfig.Soundscape);
                    }
                    else
                    {
                        cueSources = (AudioSource[])Read(audio, "_cueSources");
                        loseClip = audioConfig.Cues.Single(cue => cue.Cue == CueId.Lose).Clip;
                        Assert.That(cueSources.Length, Is.EqualTo(2));
                    }
                    Assert.That(loseClip, Is.Not.Null);
                    hud = One<HUDDriver>();
                    hudConfig = (HUDDriverConfig)Read(hud, "_config");
                    Snapshot(audioConfig); Snapshot(hudConfig);
                    Assert.That(hudConfig.RestoreSeconds, Is.EqualTo(0.5f));
                    VisualElement document = hud.GetComponent<UIDocument>().rootVisualElement;
                    hudExtra = document.Q<VisualElement>("hud-extra");
                    hudCount = document.Q<Label>("cake-count"); hudExit = document.Q<Label>("exit-state");
                    Assert.That(hudExtra != null && hudCount != null && hudExit != null, Is.True);
                    initialCount = hudCount.text; initialExit = hudExit.text;
                    Assert.That(run.Tick, Is.Zero, "Observe readiness before the first real tick.");
                    Assert.That(PlayerRegistry.Items.Count, Is.EqualTo(1));
                    Assert.That(HunterRegistry.Items.Count, Is.EqualTo(1));
                    Assert.That(hunter.ReadOnlyState.TargetId, Is.EqualTo(player.Id));
                    Assert.That(player.ReadOnlyState.Position, Is.EqualTo(PlayerSpawn));
                    Assert.That(hunter.ReadOnlyState.Position, Is.EqualTo(HunterSpawn));
                    Assert.That(config.ConfirmationSeconds, Is.EqualTo(0.3f));
                    Assert.That(config.LossSeconds, Is.EqualTo(2.5f));
                    Assert.That(config.LossDistance, Is.EqualTo(14f));
                    Assert.That(config.LostGraceSeconds, Is.EqualTo(1.5f));
                    Assert.That(profile.SensorIntervalTicks, Is.EqualTo(4));
                    Assert.That(profile.SightRange, Is.EqualTo(30f));
                    Assert.That(profile.SightConeDegrees, Is.EqualTo(110f));
                    gameplay = (InputActionMap)typeof(PlayerInputDriver)
                        .GetField("_actions", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(inputService.GetComponent<PlayerInputDriver>());
                    Assert.That(gameplay, Is.Not.Null);
                    previousDevices = gameplay.devices.HasValue
                        ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray())
                        : (ReadOnlyArray<InputDevice>?)null;
                    deviceFilterSaved = true;
                    gameplay.devices = Array.Empty<InputDevice>();
                    foreach (InputAction action in gameplay.actions)
                        Assert.That(action.controls.Count, Is.Zero, "Synthetic trial must bind no hardware controls.");
                    // Clear buffered hardware/cancelled edges before tick one. This
                    // retains real owner/focus/input gates and the active recording.
                    Assert.That(inputService.SetSource(InputSource.Live), Is.True);
                    inputService.FramePublished += NeutralInput; run.TickAdvanced += OnTick;
                    run.ChaseStarted += OnStarted; run.ChasePhaseChanged += OnPhase;
                    run.ChaseEnded += OnEnded; chase.OnChaseLost += OnLost;
                    hunter.OnSighting += OnSighting; hunter.OnLungeHit += OnHit;
                    initialFrame = Time.frameCount; initialFixedTime = Time.fixedTimeAsDouble;
                    feedbackObserver = new GameObject("Chase loss feedback observation").AddComponent<ChaseLossFeedbackObserver>();
                    feedbackObserver.Sample = ObserveFeedback;
                    Ready = true;
                }
                catch (Exception error) { Fail("Readiness: " + error); }
            }

            private void NeutralInput(InputFrame produced)
            {
                if (Done || Failure.Length > 0 || run.Phase == RunPhase.Ended) return;
                neutralFrames++;
                if (!produced.Equals(default(InputFrame)))
                    Fail("Isolated gameplay producer emitted non-neutral input: " + DescribeInput(produced));
                run.ReceiveInput(default);
            }
            private void OnStarted(ChaseFact fact) => starts.Add(fact);
            private void OnPhase(ChaseFact fact) => phases.Add(fact);
            private void OnLost(ChaseFact fact) => losses.Add(fact);
            private void OnEnded(ChaseFact fact)
            {
                ends.Add(fact);
                try
                {
                    Assert.That(fact.EndReason, Is.EqualTo(ChaseEndReason.Lost));
                    Assert.That(ends.Count, Is.EqualTo(1));
                    AudioSource[] playing = PlayingLoseSources();
                    Assert.That(playing.Length, Is.EqualTo(1), "Final loss must start one real Lose cue source.");
                    if (soundscape != null)
                    {
                        var config = audioConfig.Soundscape;
                        var bank = config.Sounds.Single(cue => cue.Cue == CueId.Lose);
                        Assert.That(playing[0].clip, Is.Not.Null);
                        Assert.That(bank.Clips, Does.Contain(playing[0].clip));
                        float maximum = Mathf.Clamp01(bank.Gain) * audioConfig.MasterGain * (bank.Ambience ? config.AmbienceGain : config.EffectsGain);
                        Assert.That(playing[0].volume, Is.GreaterThan(0f));
                        Assert.That(playing[0].volume, Is.InRange(maximum * (1f - Mathf.Clamp01(bank.GainVariation)) - .0001f, maximum + .0001f));
                        Assert.That(playing[0].pitch, Is.InRange(bank.PitchMinimum - .0001f, bank.PitchMaximum + .0001f));
                        Assert.That(playing[0].loop, Is.EqualTo(bank.Loop));
                        Assert.That(playing[0].spatialBlend, Is.EqualTo(bank.Spatial ? 1f : 0f));
                        Assert.That(playing[0].outputAudioMixerGroup, Is.EqualTo(bank.Ambience ? config.AmbienceGroup : config.EffectsGroup));
                        loseClip = playing[0].clip;
                    }
                    else
                    {
                        var definition = audioConfig.Cues.Single(cue => cue.Cue == CueId.Lose);
                        Assert.That(playing[0].volume, Is.EqualTo(definition.Gain * audioConfig.MasterGain).Within(0.0001f));
                    }
                    Assert.That(hudExtra.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                    Assert.That(hudExtra.style.opacity.value, Is.Zero, "HUD restoration must start hidden, not snap to full opacity.");
                    loseSourceObserved = true; lossEndFrame = Time.frameCount;
                }
                catch (Exception error) { Fail("Final loss feedback routing: " + error); }
            }
            private AudioSource[] PlayingLoseSources()
            {
                if (soundscape == null) return cueSources.Where(source => source.clip == loseClip && source.isPlaying).ToArray();
                var voices = ((AudioSoundscapeDriverState)Read(soundscape, "_state")).Voices;
                Assert.That(voices.Length, Is.EqualTo(cueSources.Length));
                return Enumerable.Range(0, voices.Length).Where(index => voices[index].Cue == (int)CueId.Lose &&
                    voices[index].Remaining > 0f && cueSources[index].isPlaying).Select(index => cueSources[index]).ToArray();
            }
            private void CaptureStarted(RunCaptureMetadata metadata)
            { captureMetadata = metadata; capturedMetadata = true; }
            private void OnSighting(HunterSighting fact) => sightings.Add(fact);
            private void OnHit(HunterHit hit) { hits++; Fail("Unexpected contact invalidates the distant LOS arrangement."); }

            private void OnTick(InputFrame input, float dt, long tick)
            {
                if (Failure.Length > 0 || Done) return;
                try
                {
                    completedTicks++;
                    Assert.That(neutralFrames, Is.EqualTo(completedTicks),
                        "Each real tick must follow one isolated neutral gameplay publication.");
                    Assert.That(tick, Is.EqualTo(previousTick + 1)); previousTick = tick;
                    Assert.That(dt, Is.EqualTo(Dt).Within(0.000001f));
                    Assert.That(input.Equals(default(InputFrame)), Is.True, DescribeInput(input));
                    Assert.That(tick, Is.LessThan(900), "Bounded integration exceeded 15 simulated seconds.");
                    Assert.That(run.Phase, Is.EqualTo(RunPhase.FirstSweep));
                    Assert.That(player.ReadOnlyState.IsAlive && hunter.ReadOnlyState.IsActive, Is.True);
                    Assert.That(player.LastProbeRecord.Resolution.Grounded, Is.True);
                    Assert.That(Vector3.Distance(player.ReadOnlyState.Position, PlayerSpawn), Is.LessThan(0.1f));
                    Assert.That(Vector3.Distance(hunter.transform.position, hunter.ReadOnlyState.Position), Is.LessThan(0.001f));
                    float distance = Vector3.Distance(player.ReadOnlyState.Position, hunter.ReadOnlyState.Position);
                    minimumDistance = Mathf.Min(minimumDistance, distance);
                    Assert.That(distance, Is.GreaterThan(config.LossDistance), "Distance condition must remain true throughout.");
                    Assert.That(distance, Is.LessThan(profile.SightRange), "Loss must not come from exceeding sight range.");
                    Vector3 direction = player.ReadOnlyState.Position - hunter.ReadOnlyState.Position; direction.y = 0f;
                    Assert.That(Vector3.Dot(hunter.ReadOnlyState.Forward.normalized, direction.normalized),
                        Is.GreaterThan(Mathf.Cos(profile.SightConeDegrees * 0.5f * Mathf.Deg2Rad)),
                        "Loss must not come from leaving the sight cone.");
                    pathAndMotionObserved |= driver.PathAvailable && Vector3.Distance(hunter.ReadOnlyState.Position, HunterSpawn) > 0.2f;
                    if (screen.enabled) AssertPhysicalOcclusion();

                    if (stage == Stage.Establish)
                    {
                        if (tick < 60 || chase.ReadOnlyState.Phase != ChasePhase.Confirmed) return;
                        Assert.That(starts.Count, Is.EqualTo(1));
                        Assert.That(pathAndMotionObserved, Is.True, "Hunter must advance through its real motor before containment.");
                        Assert.That(hunter.ReadOnlyState.PlayerVisible, Is.True);
                        chaseId = chase.ReadOnlyState.ChaseId;
                        Mark("confirmed with screen open"); SetScreen(true); stage = Stage.FirstLoss;
                    }
                    else if (stage == Stage.FirstLoss || stage == Stage.FinalLoss)
                    {
                        if (hunter.ReadOnlyState.PlayerVisible)
                        {
                            Assert.That(noSightTick, Is.Zero, "Sight returned while the physical screen remained closed.");
                            Assert.That(chase.ReadOnlyState.Phase, Is.EqualTo(ChasePhase.Confirmed));
                            return; // Sensor cadence may retain the old observation for up to three ticks.
                        }
                        if (noSightTick == 0) { noSightTick = tick; Mark("first committed no-sight"); }
                        int unseenTicks = (int)(tick - noSightTick + 1);
                        if (unseenTicks < 150)
                            Assert.That(chase.ReadOnlyState.Phase, Is.EqualTo(ChasePhase.Confirmed), "Loss happened before 2.5 s of no sight.");
                        else
                        {
                            Assert.That(unseenTicks, Is.EqualTo(150));
                            Assert.That(chase.ReadOnlyState.Phase, Is.EqualTo(ChasePhase.Lost));
                            Assert.That(ends.Count, Is.Zero, "Lost is a grace phase, not an escaped event.");
                            Mark("lost after 150 no-sight ticks");
                            if (stage == Stage.FirstLoss)
                            { firstLossTick = tick; SetScreen(false); stage = Stage.Reacquire; }
                            else { finalLossTick = tick; stage = Stage.Grace; }
                        }
                    }
                    else if (stage == Stage.Reacquire)
                    {
                        Assert.That(Any(RawSight()), Is.True, "Low fence must leave actual target rays visible with screen open.");
                        if (hunter.ReadOnlyState.PlayerVisible && reacquireSightTick == 0)
                        { reacquireSightTick = tick; Mark("first committed reacquisition sight"); }
                        int seenTicks = reacquireSightTick == 0 ? 0 : (int)(tick - reacquireSightTick + 1);
                        if (seenTicks < 18)
                            Assert.That(chase.ReadOnlyState.Phase, Is.EqualTo(ChasePhase.Lost));
                        else
                        {
                            Assert.That(seenTicks, Is.EqualTo(18));
                            Assert.That(chase.ReadOnlyState.Phase, Is.EqualTo(ChasePhase.Confirmed));
                            Assert.That(chase.ReadOnlyState.ChaseId, Is.EqualTo(chaseId));
                            Assert.That(starts.Count, Is.EqualTo(1)); Assert.That(ends.Count, Is.Zero);
                            Assert.That((tick - firstLossTick) * dt, Is.LessThan(config.LostGraceSeconds));
                            reacquiredTick = tick; Mark("same chase reacquired after 18 visible ticks");
                            noSightTick = 0; SetScreen(true); stage = Stage.FinalLoss;
                        }
                    }
                    else if (stage == Stage.Grace)
                    {
                        Assert.That(hunter.ReadOnlyState.PlayerVisible, Is.False);
                        long elapsed = tick - finalLossTick;
                        if (elapsed < 90)
                        {
                            Assert.That(chase.ReadOnlyState.Phase, Is.EqualTo(ChasePhase.Lost));
                            Assert.That(ends.Count, Is.Zero, "No escaped event before all 90 grace ticks.");
                        }
                        else
                        {
                            Assert.That(elapsed, Is.EqualTo(90));
                            Assert.That(chase.ReadOnlyState.Phase, Is.EqualTo(ChasePhase.None));
                            Assert.That(ends.Count, Is.EqualTo(1));
                            endTick = tick; Mark("escaped once after 90 grace ticks"); stage = Stage.EndStability;
                        }
                    }
                    else if (stage == Stage.EndStability)
                    {
                        Assert.That(chase.ReadOnlyState.HasActiveChase, Is.False);
                        Assert.That(starts.Count, Is.EqualTo(1)); Assert.That(ends.Count, Is.EqualTo(1));
                        if (tick - endTick >= 12 && feedbackRestored) stage = Stage.Complete;
                    }
                }
                catch (Exception error) { Fail("Tick " + tick + " in " + stage + ": " + error); }
            }

            private void SetScreen(bool closed)
            {
                screen.enabled = closed;
                Physics.SyncTransforms();
                if (closed) AssertPhysicalOcclusion();
                else Assert.That(Any(RawSight()), Is.True, "Open screen must restore a real target ray past the low fence.");
                Mark(closed ? "physical screen closed" : "physical screen opened");
            }
            private SightProbe RawSight() => driver.ProbeSight(player.ReadOnlyState.Position,
                collider => collider.GetComponentInParent<PlayerManager>() == player);
            private static bool Any(SightProbe sight) => sight.HeadVisible || sight.ChestVisible || sight.HipsVisible;
            private void AssertPhysicalOcclusion()
            {
                Assert.That(Any(RawSight()), Is.False, "All three physical target rays must be blocked.");
                Vector3 origin = driver.Position + Vector3.up * motor.EyeHeight;
                Vector3 heights = motor.TargetSampleHeights;
                foreach (float height in new[] { heights.x, heights.y, heights.z })
                {
                    Vector3 delta = player.ReadOnlyState.Position + Vector3.up * height - origin;
                    Assert.That(screen.Raycast(new Ray(origin, delta.normalized), out _, delta.magnitude), Is.True,
                        "The temporary screen must intersect each target ray; absence of a target alone is insufficient.");
                }
            }
            private void Mark(string reason)
            {
                SightProbe sight = RawSight();
                TestContext.Progress.WriteLine("LOS " + reason + "; " + Describe() +
                    "; raw head/chest/hips=" + sight.HeadVisible + "/" + sight.ChestVisible + "/" + sight.HipsVisible);
            }
            public string Describe() => "stage=" + stage + "; tick=" + (run == null ? 0 : run.Tick) +
                "; fixedTime=" + Time.fixedTimeAsDouble + "; frame=" + Time.frameCount +
                "; neutralFrames=" + neutralFrames +
                "; starts/lost/ends=" + starts.Count + "/" + losses.Count + "/" + ends.Count +
                "; player=" + (player == null ? "absent" : player.ReadOnlyState.Position.ToString("F4")) +
                "; hunter=" + (hunter == null ? "absent" : hunter.ReadOnlyState.Position.ToString("F4")) +
                "; visible=" + (hunter != null && hunter.ReadOnlyState.PlayerVisible) + "; minDistance=" + minimumDistance;
            public void AssertOutcome()
            {
                Assert.That(Done, Is.True, "No completed live result: " + Describe());
                Assert.That(readyCount, Is.EqualTo(1)); Assert.That(hits, Is.Zero);
                Assert.That(completedTicks, Is.InRange(400, 899));
                Assert.That(neutralFrames, Is.EqualTo(completedTicks));
                Assert.That(Time.frameCount, Is.GreaterThan(initialFrame));
                Assert.That(Time.fixedTimeAsDouble - initialFixedTime, Is.GreaterThan(6.5));
                Assert.That(losses.Count, Is.EqualTo(2));
                Assert.That(phases.Select(fact => fact.Phase), Is.EqualTo(new[] {
                    ChasePhase.Confirmed, ChasePhase.Lost, ChasePhase.Confirmed, ChasePhase.Lost, ChasePhase.None }));
                Assert.That(phases.All(fact => fact.ChaseId == chaseId && fact.Player == player.Id && fact.Hunter == hunter.Id), Is.True);
                Assert.That(phases[2].Tick, Is.EqualTo(reacquiredTick));
                Assert.That(ends.Single().EndReason, Is.EqualTo(ChaseEndReason.Lost));
                Assert.That(ends.Single().ChaseId, Is.EqualTo(chaseId));
                Assert.That(ends.Single().Tick - finalLossTick, Is.EqualTo(90));
                Assert.That(sightings.Count(fact => fact.Visible), Is.GreaterThan(5));
                Assert.That(sightings.Count(fact => !fact.Visible), Is.GreaterThan(50));
                Assert.That(sightings.All(fact => fact.Hunter == hunter.Id && fact.Target == player.Id), Is.True);
                long firstSight = sightings.First(fact => fact.Visible).Tick;
                Assert.That(starts.Single().Tick - firstSight + 1, Is.EqualTo(18));
                Assert.That(player.ReadOnlyState.Health, Is.EqualTo(100f));
                Assert.That(minimumDistance, Is.GreaterThan(config.LossDistance));
                Assert.That(capturedMetadata, Is.True);
                Assert.That(captureMetadata.StartTick, Is.Zero);
                Assert.That(loseSourceObserved && restoreMidpoint && feedbackRestored, Is.True);
                Assert.That(graceFeedbackSamples, Is.GreaterThan(0), "Lost grace needs actual UI/audio observations.");
                Assert.That(feedbackSamples, Is.GreaterThan(1));
                Assert.That(maxFeedbackFrame, Is.LessThanOrEqualTo(0.1f), "Insufficient frame resolution for half-second UI timing.");
                Assert.That(restoreElapsed, Is.InRange(hudConfig.RestoreSeconds - 0.0001f,
                    hudConfig.RestoreSeconds + maxFeedbackFrame + 0.0001f));
                Assert.That(restoreFrame, Is.GreaterThan(lossEndFrame));
                Assert.That(hudExtra.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(hudExtra.style.opacity.value, Is.EqualTo(1f));
                TestContext.WriteLine("Loss feedback: session=" + captureMetadata.SessionId + "; seed=" + captureMetadata.Seed +
                    "; source=" + captureMetadata.SourceRevision + "; config=" + captureMetadata.ConfigSnapshotHash +
                    "; endTick=" + endTick + "; endFrame=" + lossEndFrame + "; restoredFrame=" + restoreFrame +
                    "; actualLoseClip=" + loseClip.name + "; restoreSeconds=" + restoreElapsed + "; maxFrame=" + maxFeedbackFrame +
                    "; samples=" + feedbackSamples + "; graceSamples=" + graceFeedbackSamples +
                    "; actual source/UI routing only, no audio-device or participant claim; input capture remains incomplete at teardown.");
                foreach (var item in configSnapshots) Assert.That(JsonUtility.ToJson(item.Key), Is.EqualTo(item.Value), item.Key.name);
                TestContext.Progress.WriteLine("Arranged physical loss/grace integration passed: " + Describe() +
                    ". Near-distance AND threshold equality, route skill, tuning statistics and human feel remain separate.");
            }
            private void Snapshot(ScriptableObject asset)
            { Assert.That(asset, Is.Not.Null); configSnapshots.Add(asset, JsonUtility.ToJson(asset)); }
            private BoxCollider Box(string name, Vector3 center, Vector3 size)
            {
                var value = new GameObject(name);
                value.transform.SetParent(geometry.transform, false); value.transform.position = center;
                BoxCollider collider = value.AddComponent<BoxCollider>(); collider.size = size;
                return collider;
            }
            private void Fail(string message) { if (Failure.Length == 0) Failure = message; }
            private static string DescribeInput(InputFrame input) => "move=" + input.Move +
                "; look=" + input.LookDelta + "; held=" + input.Held +
                "; pressed=" + input.Pressed + "; released=" + input.Released;
            public void Dispose()
            {
                if (feedbackObserver != null)
                { feedbackObserver.Sample = null; UnityEngine.Object.Destroy(feedbackObserver.gameObject); }
                if (captureRun != null) captureRun.CaptureStarted -= CaptureStarted;
                TagArenaSceneRoot.SceneReady -= ObserveReady;
                if (inputService != null) inputService.FramePublished -= NeutralInput;
                if (gameplay != null && deviceFilterSaved) gameplay.devices = previousDevices;
                if (run != null)
                {
                    run.TickAdvanced -= OnTick;
                    run.ChaseStarted -= OnStarted; run.ChasePhaseChanged -= OnPhase; run.ChaseEnded -= OnEnded;
                }
                if (chase != null) chase.OnChaseLost -= OnLost;
                if (hunter != null) { hunter.OnSighting -= OnSighting; hunter.OnLungeHit -= OnHit; }
                if (geometry != null) UnityEngine.Object.Destroy(geometry);
            }

            private void ObserveFeedback()
            {
                if (!Ready || Done || Failure.Length > 0 || starts.Count == 0) return;
                try
                {
                    Assert.That(hudCount.text, Is.EqualTo(initialCount));
                    Assert.That(hudExit.text, Is.EqualTo(initialExit));
                    Assert.That(hudCount.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
                    Assert.That(hudExit.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
                    if (ends.Count == 0)
                    {
                        Assert.That(hudExtra.style.display.value, Is.EqualTo(DisplayStyle.None));
                        Assert.That(hudExtra.style.opacity.value, Is.Zero);
                        Assert.That(PlayingLoseSources(), Is.Empty,
                            "Temporary Lost grace cannot emit the final Lose cue.");
                        if (chase.ReadOnlyState.Phase == ChasePhase.Lost) graceFeedbackSamples++;
                        return;
                    }
                    Assert.That(hudExtra.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                    if (feedbackRestored)
                    { Assert.That(hudExtra.style.opacity.value, Is.EqualTo(1f)); return; }
                    float dt = Time.unscaledDeltaTime;
                    Assert.That(dt > 0f && !float.IsNaN(dt) && !float.IsInfinity(dt), Is.True);
                    feedbackElapsed += dt; maxFeedbackFrame = Mathf.Max(maxFeedbackFrame, dt); feedbackSamples++;
                    float opacity = hudExtra.style.opacity.value;
                    Assert.That(opacity, Is.EqualTo(Mathf.Clamp01(feedbackElapsed / hudConfig.RestoreSeconds)).Within(0.0001f),
                        "Actual HUD opacity must follow the configured duration using observed frame time.");
                    Assert.That(opacity, Is.GreaterThanOrEqualTo(previousOpacity)); previousOpacity = opacity;
                    if (opacity > 0f && opacity < 1f) restoreMidpoint = true;
                    if (opacity == 1f)
                    { feedbackRestored = true; restoreElapsed = feedbackElapsed; restoreFrame = Time.frameCount; }
                }
                catch (Exception error) { Fail("Late-frame loss feedback: " + error); }
            }
        }
        private static object Read(object target, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, flags); if (field != null) return field.GetValue(target);
                PropertyInfo property = type.GetProperty(name, flags); if (property != null) return property.GetValue(target);
            }
            throw new InvalidOperationException("Missing inspected member " + target.GetType().Name + "." + name);
        }
        private static FieldInfo SpawnField(string name) => typeof(TagArenaSceneRoot).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Missing initial spawn wiring: " + name);
        private static T One<T>() where T : Component
        {
            T[] values = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(values.Length, Is.EqualTo(1), typeof(T).Name + " must have one active instance.");
            return values[0];
        }
        private static IEnumerator Until(Func<bool> predicate, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }

    [DefaultExecutionOrder(32000)]
    public sealed class ChaseLossFeedbackObserver : MonoBehaviour
    {
        public Action Sample;
        private void LateUpdate() => Sample?.Invoke();
    }
}
