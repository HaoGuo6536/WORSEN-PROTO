// ============================================================================
// CriticalHealthFeedbackIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the real Player health publisher and Session/presentation routing at
//   living 25 health, which unchanged 50-damage Hunter lunges cannot naturally reach.
//   Explicit public damage commands arrange 100 -> 50 -> 25 -> 0; actual source and
//   volume observations verify critical breathing, injury and fresh-life restoration.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Camera/PostFX and Audio integration.
// KEY RESPONSIBILITIES:
//   - Preserve default configs and observe Player -> Run -> Audio/PostFX facts.
//   - Distinguish critical breathing from proximity and inspect real AudioSources.
//   - Verify death fades and canonical persistent Audio reset on one fresh scene.
// DEPENDENCIES:
//   Core; Domain Player/Hunter/Chase; Session Run/SceneFlow; Audio/PostFX/Input;
//   TagArena SceneRoot; existing CaptureGateTrace focus admission; NUnit and Unity
//   Test Framework. Rendering-package components are inspected through reflection.
// USAGE NOTES:
//   Root owns the Unity lease. This is one API-arranged health fixture, not natural
//   Hunter damage, accepted-hit telemetry, replay from input, or audible-device proof.
//   Scene Hunters are disabled before tick one; only the gameplay device filter is
//   isolated and restored. No health-field writes, fake events, Presenter calls,
//   config changes, scene saves, time-scale changes or direct gameplay ticks occur.
//   The first capture ends normally but requires the retained damage-command ledger
//   to reproduce this arrangement; the second capture is incomplete at teardown.
//   Scene-owned observer order 32000 samples after Audio Update and PostFX LateUpdate;
//   frame intervals bound settling observations, not an exact audio-device timeline.
//   Each life records startup frames, then admits timing after two consecutive
//   <=80 ms frames strictly beyond readiness. Measurement begins on the next frame;
//   all subsequent frames and all damage transitions retain the same 80 ms limit.
//   Raw startup and measured frame samples remain in the report, including failures.
//   Canonical subscriptions survive reload; scene observers retire before new Start.
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
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Audio;
using Worsen.Presentation.Input;
using Worsen.Presentation.PostFX;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;
using Worsen.Tests.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Camera
{
    public sealed class CriticalHealthFeedbackIntegrationTests
    {
        private const string Arena = "Assets/Scenes/TagArena.unity";

        [UnityTest]
        public IEnumerator ArrangedPublicDamageRoutesCriticalBreathingAndFreshLifeClearsIt()
        {
            yield return new EnterPlayMode();
            yield return Exercise();
        }

        private static IEnumerator Exercise()
        {
            bool background = Application.runInBackground;
            var trace = new CaptureGateTrace("CriticalHealthFeedback");
            var trial = new Trial();
            try
            {
                Application.runInBackground = true;
                Assert.That(UnityEngine.Object.FindObjectsByType<TagArenaSceneRoot>(FindObjectsSortMode.None), Is.Empty);
                yield return trace.AdmitStableGameViewFocus();
                SceneManager.sceneLoaded += trial.Loaded;
                Assert.That(SceneManager.LoadSceneAsync(Arena, LoadSceneMode.Single), Is.Not.Null);
                yield return Until(() => trial.SceneCount == 1 && trial.Settled, trial, "Healthy baseline");
                trial.Damage(50f, 50f);
                yield return Until(() => trial.Settled, trial, "50 health stays below critical breathing threshold");
                trial.Damage(25f, 25f);
                yield return Until(() => trial.Settled, trial, "25 health reaches actual critical breath source and vignette");
                trial.AssertCritical();
                trial.Damage(25f, 0f);
                yield return Until(() => trial.Settled && trial.Ended, trial, "Death reaches zero loop gains and Run end");
                trial.AssertFirstLife();
                trace.Mark("critical life ended; request fresh TagArena through canonical SceneFlow");
                trial.ReloadRequested = true;
                Assert.That(SceneFlowManager.Instance, Is.Not.Null);
                SceneFlowManager.Instance.RequestLoad(SceneKey.TagArena);
                yield return Until(() => trial.SceneCount == 2 && trial.Settled, trial, "Fresh life resets canonical audio and scene volume");
                trial.AssertFreshLife();
                trial.AssertConfigs();
            }
            finally
            {
                SceneManager.sceneLoaded -= trial.Loaded;
                try { trial.Dispose(); }
                finally
                {
                    try { TestContext.WriteLine(trial.Describe()); trace.Dispose(); }
                    finally { Application.runInBackground = background; }
                }
            }
        }

        private sealed class Life
        {
            public RunCaptureMetadata Capture;
            public EntityId PlayerId;
            public readonly List<float> RunHealth = new List<float>();
            public readonly List<float> PlayerHealth = new List<float>();
            public int ReadyFrame, SettledFrame, NeutralFrames, Ticks, Deaths;
            public int TimingAdmissionFrame = -1, StableStartupFrames;
            public bool Unique, CaptureComplete;
            public string InputPath = "";
        }

        private sealed class Trial : IDisposable
        {
            private RunSessionManager run;
            private InputManager input;
            private PlayerManager player;
            private AudioManager audioOwner;
            private AudioDriver audio;
            private AudioDriverConfig audioConfig;
            private PostFXDriver post;
            private PostFXDriverConfig postConfig;
            private AudioSource breath, hunterLayer;
            private InputActionMap gameplay;
            private ReadOnlyArray<InputDevice>? previousDevices;
            private CriticalHealthFrameObserver observer;
            private readonly List<Life> lives = new List<Life>();
            private readonly List<string> events = new List<string>();
            private readonly List<string> frameSamples = new List<string>();
            private readonly Dictionary<ScriptableObject, string> configs = new Dictionary<ScriptableObject, string>();
            private Life current;
            private bool ready, awaitingDamage, sawCriticalMidpoint;
            private float health = 100f, elapsed, maxFrame, lastBreath, peakBreath;
            private float proximity;
            private int stageFrames, firstAudioId, firstRunId, firstInputId, firstPostId, firstPlayerObjectId;
            public bool Settled, ReloadRequested;
            public string Failure = "";
            public int SceneCount => lives.Count;
            public bool Ended => run != null && run.Phase == RunPhase.Ended && current.Deaths == 1;

            public void Loaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != Arena) return;
                Guard(() =>
                {
                    DetachScene();
                    var root = One<TagArenaSceneRoot>();
                    TagArenaSceneRoot.SceneReady -= Ready;
                    TagArenaSceneRoot.SceneReady += Ready;
                    if (run != null) return;
                    run = RunSessionManager.Instance ?? (RunSessionManager)Read(root, "_run");
                    Assert.That(run, Is.Not.Null);
                    run.CaptureStarted += CaptureStarted;
                    run.CaptureEnded += CaptureEnded;
                    run.HealthChanged += HealthChanged;
                    run.PlayerDied += Died;
                    run.ChaseStarted += UnexpectedChase;
                    run.ProximityPublished += Proximity;
                });
            }

            private void CaptureStarted(RunCaptureMetadata metadata) => Guard(() =>
            {
                Assert.That(lives.Count, Is.LessThan(2));
                Assert.That(lives.Count == 0 || ReloadRequested, Is.True);
                Assert.That(metadata.StartTick, Is.Zero);
                Assert.That(metadata.FixedDeltaTime, Is.EqualTo(1f / 60f).Within(0.000001f));
                Assert.That(metadata.SessionId, Is.Not.Empty);
                Assert.That(metadata.SourceRevision, Does.StartWith("sha256:"));
                Assert.That(metadata.ConfigSnapshotHash, Does.StartWith("sha256:"));
                current = new Life { Capture = metadata }; lives.Add(current);
                proximity = 0f;
                events.Add("capture=" + metadata.SessionId + " scene=" + SceneCount + " seed=" + metadata.Seed +
                    " startTick=" + metadata.StartTick + " startFrame=" + Time.frameCount +
                    " source=" + metadata.SourceRevision + " config=" + metadata.ConfigSnapshotHash +
                    " focused=" + Application.isFocused);
            });

            private void HealthChanged(EntityId id, float value, float maximum) => Guard(() =>
            {
                Assert.That(current, Is.Not.Null);
                Assert.That(PlayerRegistry.TryGet(id, out var actual), Is.True);
                Assert.That(actual.ReadOnlyState.Health, Is.EqualTo(value));
                Assert.That(maximum, Is.EqualTo(100f));
                if (current.RunHealth.Count == 0)
                { Assert.That(value, Is.EqualTo(100f)); current.PlayerId = id; }
                else Assert.That(awaitingDamage, Is.True, "Health changed outside the disclosed public damage command.");
                Assert.That(id, Is.EqualTo(current.PlayerId));
                current.RunHealth.Add(value);
                health = value; elapsed = maxFrame = 0f; stageFrames = 0; Settled = false;
                lastBreath = breath == null ? 0f : breath.volume;
                events.Add("Run.HealthChanged=" + value + " entity=" + id + " tick=" + run.Tick +
                    " frame=" + Time.frameCount + " focused=" + Application.isFocused);
            });

            private void Ready(SceneKey scene)
            {
                if (scene != SceneKey.TagArena) return;
                Guard(() =>
                {
                    Assert.That(current, Is.Not.Null);
                    Assert.That(run, Is.SameAs(RunSessionManager.Instance));
                    Assert.That(run.Tick, Is.Zero);
                    var root = One<TagArenaSceneRoot>();
                    input = InputManager.Instance; audioOwner = AudioManager.Instance;
                    Assert.That(input != null && audioOwner != null, Is.True);
                    Assert.That(Read(root, "_run"), Is.SameAs(run));
                    Assert.That(Read(root, "_input"), Is.SameAs(input));
                    Assert.That(Read(root, "_audio"), Is.SameAs(audioOwner));
                    player = One<PlayerManager>();
                    Assert.That(player.Id, Is.EqualTo(current.PlayerId));
                    Assert.That(current.RunHealth, Is.EqualTo(new[] { 100f }));
                    foreach (HunterManager hunter in UnityEngine.Object.FindObjectsByType<HunterManager>(FindObjectsSortMode.None))
                    { Assert.That(hunter.gameObject.scene.path, Is.EqualTo(Arena)); hunter.gameObject.SetActive(false); }
                    Assert.That(HunterRegistry.Items.Count, Is.Zero);
                    Assert.That(One<ChaseManager>().ReadOnlyState.HasActiveChase, Is.False);
                    audio = audioOwner.GetComponent<AudioDriver>();
                    Assert.That(Read(audioOwner, "_driver"), Is.SameAs(audio));
                    Assert.That(audio.IsInitialized && audioOwner.isActiveAndEnabled, Is.True);
                    audioConfig = (AudioDriverConfig)Read(audio, "_config");
                    post = One<PostFXManager>().GetComponent<PostFXDriver>();
                    postConfig = (PostFXDriverConfig)Read(post, "_config");
                    Assert.That(post.IsReady, Is.True);
                    Snapshot(audioConfig); Snapshot(postConfig); Snapshot((PlayerProfile)Read(root, "_playerProfile"));
                    Assert.That(audioConfig.MasterGain, Is.EqualTo(0.8f));
                    Assert.That(audioConfig.MixSettings.CriticalHealthFraction, Is.EqualTo(0.25f));
                    Assert.That(audioConfig.MixSettings.CriticalBreathGain, Is.EqualTo(0.55f));
                    Assert.That(audioConfig.MixSettings.LayerFadeSeconds, Is.EqualTo(0.3f));
                    Assert.That(postConfig.InjuryVignette, Is.EqualTo(0.45f));
                    breath = (AudioSource)Read(audio, "_breath"); hunterLayer = (AudioSource)Read(audio, "_hunter");
                    Assert.That(breath.clip, Is.SameAs(audioConfig.BreathLoop));
                    Assert.That(breath.clip != null && breath.clip.samples > 0, Is.True);
                    Assert.That(breath.loop && breath.enabled && breath.gameObject.activeInHierarchy && !breath.mute, Is.True);
                    AssertVolumeWiring();
                    if (SceneCount == 1)
                    {
                        firstRunId = run.GetInstanceID(); firstInputId = input.GetInstanceID(); firstAudioId = audio.GetInstanceID();
                        firstPostId = post.GetInstanceID(); firstPlayerObjectId = player.GetInstanceID();
                    }
                    else
                    {
                        Assert.That(run.GetInstanceID(), Is.EqualTo(firstRunId));
                        Assert.That(input.GetInstanceID(), Is.EqualTo(firstInputId));
                        Assert.That(audio.GetInstanceID(), Is.EqualTo(firstAudioId));
                        Assert.That(post.GetInstanceID(), Is.Not.EqualTo(firstPostId));
                        Assert.That(player.GetInstanceID(), Is.Not.EqualTo(firstPlayerObjectId));
                    }
                    gameplay = (InputActionMap)Read(input.GetComponent<PlayerInputDriver>(), "_actions");
                    previousDevices = gameplay.devices.HasValue
                        ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray()) : (ReadOnlyArray<InputDevice>?)null;
                    gameplay.devices = Array.Empty<InputDevice>();
                    Assert.That(gameplay.actions.All(action => action.controls.Count == 0), Is.True);
                    Assert.That(input.SetSource(InputSource.Live), Is.True);
                    input.FramePublished += Produced;
                    run.TickAdvanced += Tick;
                    player.OnHealthChanged += PlayerHealthChanged;
                    current.ReadyFrame = Time.frameCount;
                    events.Add("ready scene=" + SceneCount + " frame=" + Time.frameCount + " tick=" + run.Tick +
                        " canonicalRun/Input/Audio=" + firstRunId + "/" + firstInputId + "/" + firstAudioId +
                        " audioConfig=" + AssetDatabase.GetAssetPath(audioConfig) + " postConfig=" + AssetDatabase.GetAssetPath(postConfig) +
                        " breathClip=" + AssetDatabase.GetAssetPath(breath.clip));
                    observer = new GameObject("Critical health frame observation").AddComponent<CriticalHealthFrameObserver>();
                    observer.Sample = () => Guard(Observe);
                    ready = true;
                });
            }

            private void Produced(InputFrame frame) => Guard(() =>
            { Assert.That(frame.Equals(default(InputFrame)), Is.True); current.NeutralFrames++; });
            private void Tick(InputFrame frame, float dt, long tick) => Guard(() =>
            {
                current.Ticks++;
                Assert.That(current.NeutralFrames, Is.EqualTo(current.Ticks));
                Assert.That(tick, Is.EqualTo(current.Ticks));
                Assert.That(dt, Is.EqualTo(1f / 60f).Within(0.000001f));
                Assert.That(frame.Equals(default(InputFrame)), Is.True);
                Assert.That(HunterRegistry.Items.Count, Is.Zero);
            });
            private void PlayerHealthChanged(EntityId id, float value, float maximum) => Guard(() =>
            {
                Assert.That(id, Is.EqualTo(current.PlayerId)); Assert.That(maximum, Is.EqualTo(100f));
                current.PlayerHealth.Add(value);
            });
            private void Died(EntityId id, Vector3 position) => Guard(() =>
            {
                Assert.That(id, Is.EqualTo(current.PlayerId)); Assert.That(health, Is.Zero);
                current.Deaths++;
                events.Add("Run.PlayerDied tick=" + run.Tick + " frame=" + Time.frameCount);
            });
            private void CaptureEnded(long tick, bool complete)
            {
                if (current == null) return;
                current.CaptureComplete = complete;
                current.InputPath = input == null ? "" : input.LastRecordingPath;
                events.Add("capture ended tick=" + tick + " frame=" + Time.frameCount + " complete=" + complete +
                    " input=" + current.InputPath + "; public damage commands are external arrangement, not replayed input");
            }
            private void UnexpectedChase(ChaseFact fact) => Guard(() => Assert.Fail("Disabled-Hunter health fixture unexpectedly started a chase."));
            private void Proximity(ProximitySample sample) { proximity = sample.Closeness; }

            public void Damage(float amount, float expectedHealth)
            {
                Assert.That(Failure, Is.Empty);
                Assert.That(SceneCount == 1 && Settled && current.Unique, Is.True);
                Assert.That(player.ReadOnlyState.IsAlive && run.Phase == RunPhase.FirstSweep, Is.True);
                int runEvents = current.RunHealth.Count, playerEvents = current.PlayerHealth.Count;
                events.Add("ARRANGED PlayerManager.ApplyHit(" + amount + ") beforeHealth=" + health +
                    " expectedHealth=" + expectedHealth + " tick=" + run.Tick + " frame=" + Time.frameCount +
                    " killerPosition=self " + player.ReadOnlyState.Position.ToString("F4") + " focused=" + Application.isFocused);
                awaitingDamage = true;
                try { player.ApplyHit(amount, player.ReadOnlyState.Position); }
                finally { awaitingDamage = false; }
                Assert.That(Failure, Is.Empty);
                Assert.That(player.ReadOnlyState.Health, Is.EqualTo(expectedHealth));
                Assert.That(health, Is.EqualTo(expectedHealth));
                Assert.That(current.RunHealth.Count, Is.EqualTo(runEvents + 1));
                Assert.That(current.PlayerHealth.Count, Is.EqualTo(playerEvents + 1));
                Assert.That(current.PlayerHealth.Last(), Is.EqualTo(expectedHealth));
            }

            private void Observe()
            {
                if (!ready) return;
                float dt = Time.unscaledDeltaTime;
                float gain = breath.volume;
                frameSamples.Add(FormattableString.Invariant($"phase={(current.TimingAdmissionFrame < 0 ? "startup" : "measured")} scene={SceneCount} frame={Time.frameCount} tick={run.Tick} health={health:R} dt={dt:R} breath={gain:R} focused={Application.isFocused}"));
                Assert.That(Application.isFocused, Is.True, "Actual focus is required for this native observation.");
                if (!current.Unique && Time.frameCount > current.ReadyFrame)
                {
                    Assert.That(One<RunSessionManager>(), Is.SameAs(run));
                    Assert.That(One<InputManager>(), Is.SameAs(input));
                    Assert.That(One<AudioManager>(), Is.SameAs(audioOwner));
                    Assert.That(One<AudioDriver>(), Is.SameAs(audio));
                    Assert.That(One<PlayerInputDriver>(), Is.SameAs(input.GetComponent<PlayerInputDriver>()));
                    current.Unique = true; current.SettledFrame = Time.frameCount;
                }
                Assert.That(proximity, Is.Zero, "Critical breathing must not be supplied by Hunter proximity.");
                Assert.That(Vignette(), Is.EqualTo((1f - health / 100f) * postConfig.InjuryVignette).Within(0.0001f));
                Assert.That(hunterLayer.volume, Is.Zero.Within(0.0001f));
                Assert.That(dt > 0f && !float.IsNaN(dt) && !float.IsInfinity(dt), Is.True);
                Assert.That(gain, Is.InRange(0f, 0.4401f));
                if (current.TimingAdmissionFrame < 0)
                {
                    // The readiness frame's dt may include scene loading before this
                    // observer existed. Admit a pre-command baseline once; never
                    // restart admission or discard a frame after any damage command.
                    Assert.That(health, Is.EqualTo(100f));
                    Assert.That(gain, Is.Zero.Within(0.0001f));
                    current.StableStartupFrames = current.Unique && Time.frameCount > current.ReadyFrame && dt <= 0.08f
                        ? current.StableStartupFrames + 1 : 0;
                    if (current.StableStartupFrames >= 2)
                    {
                        current.TimingAdmissionFrame = Time.frameCount;
                        events.Add("timing admitted scene=" + SceneCount + " readyFrame=" + current.ReadyFrame +
                            " frame=" + Time.frameCount + " stableStartupFrames=" + current.StableStartupFrames +
                            "; measured baseline begins next frame; no damage issued");
                    }
                    return;
                }
                Assert.That(Time.frameCount, Is.GreaterThan(current.TimingAdmissionFrame));
                elapsed += dt; maxFrame = Mathf.Max(maxFrame, dt); stageFrames++;
                Assert.That(maxFrame, Is.LessThanOrEqualTo(0.08f), "Insufficient frame resolution for critical gain observation.");
                if (health == 25f)
                {
                    Assert.That(breath.isPlaying, Is.True);
                    Assert.That(breath.clip, Is.SameAs(audioConfig.BreathLoop));
                    Assert.That(gain + 0.0001f, Is.GreaterThanOrEqualTo(lastBreath));
                    if (gain > 0.0001f && gain < 0.4399f) sawCriticalMidpoint = true;
                    peakBreath = Mathf.Max(peakBreath, gain);
                }
                else if (health == 0f) Assert.That(gain, Is.LessThanOrEqualTo(lastBreath + 0.0001f));
                else Assert.That(gain, Is.Zero.Within(0.0001f));
                lastBreath = gain;
                // LayerFadeSeconds bounds a full-unit gain change, not an exact
                // 0.55-gain rise duration. Permit one measured frame of update phase.
                if (Settled || !current.Unique || elapsed < audioConfig.MixSettings.LayerFadeSeconds + maxFrame) return;
                float expected = health == 25f ? 0.44f : 0f;
                Assert.That(gain, Is.EqualTo(expected).Within(0.0001f));
                Assert.That(stageFrames, Is.GreaterThan(1));
                Settled = true;
                events.Add("settled scene=" + SceneCount + " health=" + health + " tick=" + run.Tick +
                    " frame=" + Time.frameCount + " observedSeconds=" + elapsed + " maxFrame=" + maxFrame +
                    " samples=" + stageFrames + " breathVolume=" + gain + " playing=" + breath.isPlaying +
                    " clip=" + breath.clip.name + " vignette=" + Vignette() + " focused=" + Application.isFocused);
            }

            public void AssertCritical()
            {
                Assert.That(current.RunHealth, Is.EqualTo(new[] { 100f, 50f, 25f }));
                Assert.That(sawCriticalMidpoint, Is.True, "Observe a real source fade, not only its eventual property value.");
                Assert.That(peakBreath, Is.EqualTo(0.44f).Within(0.0001f));
                Assert.That(player.ReadOnlyState.IsAlive, Is.True);
                Assert.That(Vignette(), Is.EqualTo(0.3375f).Within(0.0001f));
            }
            public void AssertFirstLife()
            {
                Assert.That(current.RunHealth, Is.EqualTo(new[] { 100f, 50f, 25f, 0f }));
                Assert.That(current.PlayerHealth, Is.EqualTo(new[] { 50f, 25f, 0f }));
                Assert.That(current.Deaths, Is.EqualTo(1));
                Assert.That(current.CaptureComplete, Is.True);
                Assert.That(current.InputPath, Is.Not.Empty);
                Assert.That(player.ReadOnlyState.IsAlive, Is.False);
                Assert.That(breath.volume == 0f && hunterLayer.volume == 0f, Is.True);
            }
            public void AssertFreshLife()
            {
                Assert.That(lives.Count, Is.EqualTo(2));
                Assert.That(current.Unique && current.SettledFrame > current.ReadyFrame, Is.True);
                Assert.That(current.Capture.SessionId, Is.Not.EqualTo(lives[0].Capture.SessionId));
                Assert.That(current.Capture.SourceRevision, Is.EqualTo(lives[0].Capture.SourceRevision));
                Assert.That(current.Capture.ConfigSnapshotHash, Is.EqualTo(lives[0].Capture.ConfigSnapshotHash));
                Assert.That(current.RunHealth, Is.EqualTo(new[] { 100f }));
                Assert.That(current.PlayerHealth, Is.Empty);
                Assert.That(current.Deaths, Is.Zero);
                Assert.That(player.ReadOnlyState.Health, Is.EqualTo(100f));
                Assert.That(run.Phase, Is.EqualTo(RunPhase.FirstSweep));
                Assert.That(breath.isPlaying && breath.clip == audioConfig.BreathLoop, Is.True);
                Assert.That(breath.volume == 0f && hunterLayer.volume == 0f && Vignette() == 0f, Is.True);
            }
            private void Snapshot(ScriptableObject config)
            {
                Assert.That(config, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(config), Does.StartWith("Assets/Resources/ScriptableObjects/"));
                if (!configs.ContainsKey(config)) configs.Add(config, JsonUtility.ToJson(config));
            }
            public void AssertConfigs()
            { foreach (var item in configs) Assert.That(JsonUtility.ToJson(item.Key), Is.EqualTo(item.Value), item.Key.name); }
            private float Vignette() => (float)Read(Read(Read(post, "_vignette"), "intensity"), "value");
            private void AssertVolumeWiring()
            {
                var volume = (Behaviour)Read(post, "_volume");
                Assert.That(volume.isActiveAndEnabled, Is.True);
                Assert.That(Read(volume, "m_InternalProfile"), Is.SameAs(Read(post, "_profile")));
                UnityEngine.Camera output = UnityEngine.Camera.main;
                Assert.That(output, Is.Not.Null);
                var data = output.GetComponents<Component>().Single(component => component != null &&
                    component.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
                Assert.That(Read(data, "renderPostProcessing"), Is.EqualTo(true));
                Assert.That(((LayerMask)Read(data, "volumeLayerMask")).value & (1 << volume.gameObject.layer), Is.Not.Zero);
            }
            private void Guard(Action action)
            { if (Failure.Length > 0) return; try { action(); } catch (Exception error) { Failure = error.ToString(); } }
            public string Describe() => "Critical health API arrangement; scenes=" + SceneCount + " failure=" + Failure +
                "; events=[" + string.Join("; ", events) + "]; frameSamples=[" + string.Join("; ", frameSamples) +
                "]; actual source/volume routing only; no natural Hunter-hit, " +
                "input-only replay, pixel-visibility, device-audibility or participant claim; second capture incomplete at teardown.";
            private void DetachScene()
            {
                ready = false;
                if (observer != null) { observer.Sample = null; UnityEngine.Object.Destroy(observer.gameObject); observer = null; }
                if (input != null) input.FramePublished -= Produced;
                if (run != null) run.TickAdvanced -= Tick;
                if (player != null) player.OnHealthChanged -= PlayerHealthChanged;
                if (gameplay != null) { gameplay.devices = previousDevices; gameplay = null; }
                player = null; breath = null; hunterLayer = null;
            }
            public void Dispose()
            {
                DetachScene(); TagArenaSceneRoot.SceneReady -= Ready;
                if (run == null) return;
                run.CaptureStarted -= CaptureStarted; run.CaptureEnded -= CaptureEnded;
                run.HealthChanged -= HealthChanged; run.PlayerDied -= Died;
                run.ChaseStarted -= UnexpectedChase; run.ProximityPublished -= Proximity;
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
        private static T One<T>() where T : Component
        {
            T[] values = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(values.Length, Is.EqualTo(1), typeof(T).Name + " must have one active instance."); return values[0];
        }
        private static IEnumerator Until(Func<bool> condition, Trial trial, string label)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20.0;
            for (int frame = 0; frame < 12000 && !condition() && trial.Failure.Length == 0 && Time.realtimeSinceStartupAsDouble < deadline; frame++) yield return null;
            Assert.That(trial.Failure, Is.Empty, label + ": " + trial.Describe());
            Assert.That(condition(), Is.True, label + ": " + trial.Describe());
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }

    [DefaultExecutionOrder(32000)]
    public sealed class CriticalHealthFrameObserver : MonoBehaviour
    {
        public Action Sample;
        private void LateUpdate() => Sample?.Invoke();
    }
}
