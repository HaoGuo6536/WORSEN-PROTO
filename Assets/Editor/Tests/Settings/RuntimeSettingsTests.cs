// ============================================================================
// RuntimeSettingsTests.cs
// ============================================================================
// PURPOSE:
//   Checks runtime comfort and volume overrides without mutating shared configs.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Settings consumers.
// KEY RESPONSIBILITIES:
//   - Exercise Settings fan-out, input pause buffering, lens and blur calculators.
//   - Verify existing cue, loop, ambience and scheduled-music sources change gain.
// DEPENDENCIES:
//   NUnit, Core, Settings Session, Settings Orchestrator and Presentation stacks.
// USAGE NOTES:
//   Transient Edit Mode fixtures; no disk writes, scene assets or audio-quality claims.
//   Native volume components are injected so teardown need not defer asset destruction.
// ============================================================================
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Session.Settings;
using Worsen.Presentation.Input;
using Worsen.Presentation.Camera;
using Worsen.Presentation.PostFX;
using Worsen.Presentation.Audio;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Settings
{
    public sealed class RuntimeSettingsTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private static PlayerSettingsRecord Preferences => new PlayerSettingsRecord(1, .27f, true, 107, false, false, false, .5f, .25f, .4f);
        [TearDown] public void TearDown()
        {
            // Destroy behaviours before their injected configs/components.
            foreach (Object item in _owned) if (item is GameObject) Object.DestroyImmediate(item);
            foreach (Object item in _owned) if (item != null) Object.DestroyImmediate(item);
            _owned.Clear();
        }
        [Test]
        public void FanOutAppliesOnceAndDisableUnpairsWithoutChangingConfigs()
        {
            var input = Component<InputManager>(); var inputDriver = input.GetComponent<PlayerInputDriver>();
            var inputConfig = Config<InputDriverConfig>(); Set(inputDriver, "_config", inputConfig); inputDriver.Initialize();
            Ready(input, inputDriver);
            var camera = Component<CameraManager>(); var cameraDriver = camera.gameObject.AddComponent<CameraDriver>();
            var cameraState = new CameraDriverState { Movement = MovementState.Slide, SlideTurnRateDegrees = 90, Roll = 4, DetectionElapsed = .01f };
            var cameraPresenter = new CameraFeedbackPresenter();
            var cameraConfig = Config<CameraDriverConfig>();
            Set(cameraDriver, "_state", cameraState); Set(cameraDriver, "_presenter", cameraPresenter); Ready(camera, cameraDriver);
            var postFX = Component<PostFXManager>(); var postDriver = postFX.gameObject.AddComponent<PostFXDriver>();
            var postState = new PostFXDriverState { Blur = 1, BlurRemaining = 1 }; var postPresenter = new PostFXPresenter();
            var postConfig = Config<PostFXDriverConfig>();
            Set(postDriver, "_state", postState); Set(postDriver, "_presenter", postPresenter);
            Volume(postDriver, "_chromatic"); Volume(postDriver, "_distortion");
            Volume(postDriver, "_vignette"); Volume(postDriver, "_color");
            Volume(postDriver, "_grain"); var blur = Volume(postDriver, "_blur"); Ready(postFX, postDriver);
            var audio = Component<AudioManager>(); var audioDriver = audio.GetComponent<AudioDriver>();
            var audioConfig = Config<AudioDriverConfig>(); var clip = AudioClip.Create("Settings cue", 48000, 1, 48000, false); _owned.Add(clip);
            Set(audioConfig, "_breathLoop", clip); Set(audioConfig, "_hunterLoop", clip); audioDriver.Initialize(audioConfig); Ready(audio, audioDriver);
            var settings = Component<SettingsManager>(); var defaults = Config<SettingsConfig>(); var state = new SettingsBehaviorState();
            Set(settings, "_state", state); Set(settings, "_controller", new SettingsController(state, defaults.Defaults));
            Set(settings, "_driver", settings.gameObject.AddComponent<FakeSettingsFileDriver>());
            Object[] configs = { inputConfig, cameraConfig, postConfig, audioConfig, defaults };
            var before = new List<string>(); foreach (Object config in configs) before.Add(JsonUtility.ToJson(config));
            var route = Component<SettingsOrchestrator>(); route.Configure(settings, input, camera, postFX, audio);
            Invoke(route, "OnEnable"); Invoke(route, "OnEnable");
            Assert.That(((System.Delegate)Get(settings, "SettingsChanged")).GetInvocationList().Length, Is.EqualTo(1));
            settings.ApplySettings(Preferences);
            var inputState = (InputDriverState)Get(inputDriver, "_state");
            Assert.That(inputState.MouseSensitivity, Is.EqualTo(Preferences.MouseSensitivity)); Assert.That(inputState.InvertY, Is.True);
            cameraPresenter.PlayDetectionBeat(cameraState); cameraPresenter.Tick(cameraState, cameraConfig, .1f, 1);
            Assert.That(cameraState.HorizontalFieldOfView, Is.EqualTo(107)); Assert.That(cameraState.Roll, Is.Zero);
            Assert.That(cameraState.DetectionElapsed, Is.LessThan(0));
            Assert.That(postState.Blur, Is.Zero); Assert.That(blur.active, Is.False);
            postPresenter.PlayReacquireBlur(postState, postConfig); Assert.That(postState.BlurRemaining, Is.Zero);
            cameraPresenter.Reset(cameraState); postPresenter.Reset(postState); audioDriver.ResetRun();
            Assert.That(cameraState.BaseFieldOfView, Is.EqualTo(107)); Assert.That(postState.ReacquireBlurEnabled, Is.False);
            Assert.That(((AudioDriverState)Get(audioDriver, "_state")).RuntimeEffects, Is.EqualTo(.4f));
            for (int i = 0; i < configs.Length; i++) Assert.That(JsonUtility.ToJson(configs[i]), Is.EqualTo(before[i]));
            Invoke(route, "OnDisable"); Assert.That(Get(settings, "SettingsChanged"), Is.Null);
        }
        [Test]
        public void InputPauseClearsBothEdgesAndRuntimeLookUsesSensitivityAndInvert()
        {
            var state = new InputDriverState { OwnerEnabled = true, InputEnabled = true, HasFocus = true };
            var presenter = new InputFramePresenter(); presenter.SetMove(state, Vector2.up);
            presenter.SetPaused(state, true); presenter.AccumulateMouseLook(state, Vector2.one, .27f, true);
            Assert.That(presenter.Flush(state).LookDelta, Is.EqualTo(Vector2.zero));
            state.Held = state.Pressed = InputButtons.Jump; state.LookDelta = Vector2.one;
            presenter.SetPaused(state, false); Assert.That(presenter.Flush(state).Held, Is.EqualTo(InputButtons.None));
            presenter.AccumulateMouseLook(state, Vector2.one, .27f, true);
            Assert.That(presenter.Flush(state).LookDelta, Is.EqualTo(new Vector2(.27f, -.27f)));
        }
        [Test]
        public void SoundscapeGainsReachActiveVoicesAmbienceAndScheduledMusicWithoutCompounding()
        {
            var config = Config<AudioSoundscapeDriverConfig>(); var clip = AudioClip.Create("Settings soundscape", 48000, 1, 48000, false); _owned.Add(clip);
            Set(config, "_runIntro", clip);
            Set(config, "_sounds", new[] { new AudioSoundDefinition { Cue = CueId.SlideLoop, Clips = new[] { clip }, Gain = .5f, Loop = true, MaxConcurrent = 1, PitchMinimum = 1, PitchMaximum = 1 } });
            string before = JsonUtility.ToJson(config);
            var driver = Component<AudioSoundscapeDriver>(); driver.Initialize(config); driver.gameObject.SetActive(true); driver.SetOwnerEnabled(true);
            Assert.That(driver.Play(CueId.SlideLoop, Vector3.zero, 1, 7), Is.True);
            var state = (AudioSoundscapeDriverState)Get(driver, "_state"); var music = (AudioChaseMusicDriverState)Get(driver, "_music");
            state.InteriorGain = .5f; music.StressGain = .5f;
            var voices = (AudioSource[])Get(driver, "_voices"); var layers = (AudioSource[])Get(driver, "_layers");
            var run = (AudioSource[])Get(driver, "_runSources"); float original = voices[0].volume;
            driver.SetRuntimeGains(.5f, .25f, .4f); driver.SetRuntimeGains(.5f, .25f, .4f);
            Assert.That(voices[0].volume, Is.EqualTo(original * .5f * .4f).Within(.00001f));
            Assert.That(layers[1].volume, Is.EqualTo(.5f * .5f * .25f * config.MusicGain).Within(.00001f));
            Assert.That(layers[3].volume, Is.EqualTo(.5f * .5f * .4f * config.AmbienceGain).Within(.00001f));
            Assert.That(run[0].volume, Is.EqualTo(.5f * .25f * config.MusicGain * config.RunGain).Within(.00001f));
            driver.SetRuntimeGains(0, 1, 1); Assert.That(voices[0].volume + layers[1].volume + layers[3].volume + run[0].volume, Is.Zero);
            Assert.That(JsonUtility.ToJson(config), Is.EqualTo(before));
        }
        private T Component<T>() where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); _owned.Add(go); return go.AddComponent<T>(); }
        private T Config<T>() where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); _owned.Add(value); return value; }
        private VolumeComponent Volume(PostFXDriver driver, string name)
        {
            var field = typeof(PostFXDriver).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            var value = (VolumeComponent)ScriptableObject.CreateInstance(field.FieldType);
            _owned.Add(value); field.SetValue(driver, value); return value;
        }
        private static void Ready(object manager, object driver) { Set(manager, "_driver", driver); Set(manager, "_initialized", true); }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
