// ============================================================================
// PlayerInputDriverTests.cs
// ============================================================================
// PURPOSE:
//   Verifies cursor ownership and input gates through real component lifecycles.
//   Virtual devices exercise Input System callbacks without claiming hardware input
//   or depending on the Unity window or Game view having focus.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Input.
// KEY RESPONSIBILITIES:
//   - Check lifecycle gates and prove settings restoration across Play Mode exit.
//   - Protect canonical-service cursor ownership and live/playback transitions.
//   - Verify independent UI click actions survive the gameplay gate closing.
//   - Verify C produces one slide press while Left Ctrl no longer does.
//   - Preserve physical Shift press/hold/release through the Sprint input contract.
// DEPENDENCIES:
//   - Input presentation components, Core records, Unity Input System and Test Framework.
// USAGE NOTES:
//   Coordinator runs under the Unity lease. Test Framework supplies/restores its
//   isolated scene. Creates only temporary objects/devices, scopes the gameplay map
//   to those devices, pairs events, and restores cursor state after every test.
//   Focus signals are synthetic; actual focus and rendered Results clicks remain
//   integration checks. Focus overrides exist only inside synchronous calls, never
//   across a yield. SessionState retains the pre-entry identity/policies across
//   reloads; teardown checks before/after exit and again after the whole fixture.
//   No assets are modified; projects without a settings asset use Unity's default object.
// ============================================================================
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Presentation.Input;

namespace Worsen.Tests.Input
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class PlayerInputDriverTests
    {
        private GameObject _root;
        private InputManager _manager;
        private PlayerInputDriver _driver;
        private InputDriverConfig _config;
        private InputActionMap _gameplay;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private const string SettingsSnapshotKey = "Worsen.PlayerInputDriverTests.InputSettings";
        private CursorLockMode _previousLock;
        private bool _previousVisible;
        private bool _cursorSnapshotTaken;
        private InputFrame _lastFrame;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            CaptureProjectInputSettings();
            yield return new EnterPlayMode();
            AssertProjectInputSettings("after EnterPlayMode/domain reload");
            Assert.That(InputManager.Instance, Is.Null, "Requires the Test Framework isolated scene.");
            try
            {
                _previousLock = Cursor.lockState;
                _previousVisible = Cursor.visible;
                _cursorSnapshotTaken = true;
                _lastFrame = default;
                // A hidden, unlocked cursor makes restoration distinct from the UI state.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = false;
                FocusIndependentInputScope.Run(() =>
                {
                    _keyboard = InputSystem.AddDevice<Keyboard>();
                    _mouse = InputSystem.AddDevice<Mouse>();
                });
                _root = new GameObject("Input cursor lifecycle test");
                _root.SetActive(false);
                _manager = _root.AddComponent<InputManager>();
                _driver = _root.GetComponent<PlayerInputDriver>();
                _config = ScriptableObject.CreateInstance<InputDriverConfig>();
                var serialized = new SerializedObject(_driver);
                serialized.FindProperty("_config").objectReferenceValue = _config;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                _root.SetActive(true);
                Assert.That(_manager.Initialize(), Is.SameAs(_manager));
                _gameplay = (InputActionMap)typeof(PlayerInputDriver)
                    .GetField("_actions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_driver);
                _gameplay.devices = new InputDevice[] { _keyboard, _mouse };
                _driver.SendMessage("OnApplicationFocus", true);
                _manager.FramePublished += CaptureFrame;
            }
            catch
            {
                // A failed UnitySetUp may prevent the normal teardown from running.
                CleanupFixture();
                throw;
            }
            AssertProjectInputSettings("completed setup, before yielding to test");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CleanupFixture();
            AssertProjectInputSettings("teardown before ExitPlayMode");
            if (Application.isPlaying) yield return new ExitPlayMode();
            AssertProjectInputSettings("teardown after ExitPlayMode/domain reload");
        }

        private void CleanupFixture()
        {
            try
            {
                if (_manager != null) _manager.FramePublished -= CaptureFrame;
                if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
                if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
                if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
                if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
                if (_cursorSnapshotTaken)
                {
                    Cursor.lockState = _previousLock;
                    Cursor.visible = _previousVisible;
                    _cursorSnapshotTaken = false;
                }
            }
            finally { AssertProjectInputSettings("after fixture cleanup"); }
        }

        private static void CaptureProjectInputSettings()
        {
            Assert.That(Application.isPlaying, Is.False, "Capture the project object before Play Mode entry.");
            if (SessionState.GetString(SettingsSnapshotKey, "").Length > 0)
            {
                AssertProjectInputSettings("before the next test enters Play Mode");
                return;
            }
            InputSettings current = InputSystem.settings;
            if (EditorBuildSettings.TryGetConfigObject("com.unity.input.settings", out InputSettings asset))
                Assert.That(current, Is.SameAs(asset), "A previous fixture left a settings override installed.");
            else
            {
                // This checkout has project-wide actions but no saved InputSettings asset.
                InputSettings defaults = ScriptableObject.CreateInstance<InputSettings>();
                try
                {
                    Assert.That(current.backgroundBehavior, Is.EqualTo(defaults.backgroundBehavior));
                    Assert.That(current.editorInputBehaviorInPlayMode, Is.EqualTo(defaults.editorInputBehaviorInPlayMode));
                }
                finally { UnityEngine.Object.DestroyImmediate(defaults); }
            }
            SessionState.SetString(SettingsSnapshotKey, SettingsIdentityAndPolicy(current));
        }

        private static string SettingsIdentityAndPolicy(InputSettings settings) =>
            settings.GetEntityId() + ";" + settings.backgroundBehavior + ";" + settings.editorInputBehaviorInPlayMode;

        private static void AssertProjectInputSettings(string phase)
        {
            string expected = SessionState.GetString(SettingsSnapshotKey, "");
            Assert.That(expected, Is.Not.Empty, "Missing pre-Play Mode settings snapshot: " + phase);
            string actual = SettingsIdentityAndPolicy(InputSystem.settings);
            Assert.That(actual, Is.EqualTo(expected), phase + ": original settings object and focus policies must survive.");
            TestContext.Progress.WriteLine("Input settings " + phase + ": " + actual);
        }

        [OneTimeTearDown]
        public void AfterFixtureProjectInputSettingsAreUnchanged()
        {
            try
            {
                Assert.That(Application.isPlaying, Is.False);
                AssertProjectInputSettings("after PlayerInputDriverTests finished");
            }
            finally { SessionState.EraseString(SettingsSnapshotKey); }
        }

        [UnityTest]
        public IEnumerator AfterFixtureTeardownAndPlayModeExitRestoresProjectInputSettings()
        {
            _manager.SetInputEnabled(true);
            PushKeys(Key.W);
            _manager.PublishFrame();
            Assert.That(_lastFrame.Move, Is.EqualTo(Vector2.up), "Exercise the focus-independent path first.");
            CleanupFixture();
            // Exit must be yielded directly: Unity cannot restore nested iterators
            // across a domain reload. No fixture fields are read after this point.
            yield return new ExitPlayMode();
            Assert.That(Application.isPlaying, Is.False);
            AssertProjectInputSettings("post-lifecycle regression");
        }

        [Test]
        public void FocusOverrideRestoresOriginalObjectAndPoliciesWhenWorkThrows()
        {
            InputSettings original = InputSystem.settings;
            InputSettings clone = null;
            string before = JsonUtility.ToJson(original);
            bool previousBackground = Application.runInBackground;
            Assert.Throws<InvalidOperationException>(() => FocusIndependentInputScope.Run(() =>
            {
                clone = InputSystem.settings;
                Assert.That(clone, Is.Not.SameAs(original));
                Assert.That(clone.backgroundBehavior, Is.EqualTo(InputSettings.BackgroundBehavior.IgnoreFocus));
                Assert.That(clone.editorInputBehaviorInPlayMode,
                    Is.EqualTo(InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView));
                throw new InvalidOperationException("Deliberate synchronous input failure.");
            }));
            Assert.That(InputSystem.settings, Is.SameAs(original));
            Assert.That(JsonUtility.ToJson(original), Is.EqualTo(before));
            Assert.That(clone == null, Is.True, "Destroy the unsaved clone even when input work fails.");
            Assert.That(Application.runInBackground, Is.EqualTo(previousBackground));
            AssertProjectInputSettings("after throwing input work");
        }

        [Test]
        public void ShiftPublishesSprintWhileUnmodifiedMovementDoesNot()
        {
            _manager.SetInputEnabled(true);
            PushKeys(Key.W);
            _manager.PublishFrame();
            Assert.That(_lastFrame.Move, Is.EqualTo(Vector2.up));
            Assert.That(_lastFrame.Held & InputButtons.Sprint, Is.EqualTo(InputButtons.None));
            PushKeys(Key.W, Key.LeftShift);
            _manager.PublishFrame();
            Assert.That(_lastFrame.Held & InputButtons.Sprint, Is.EqualTo(InputButtons.Sprint));
            Assert.That(_lastFrame.Pressed & InputButtons.Sprint, Is.EqualTo(InputButtons.Sprint));
            _manager.PublishFrame();
            Assert.That(_lastFrame.Held & InputButtons.Sprint, Is.EqualTo(InputButtons.Sprint));
            Assert.That(_lastFrame.Pressed & InputButtons.Sprint, Is.EqualTo(InputButtons.None));
            PushKeys(Key.W);
            _manager.PublishFrame();
            Assert.That(_lastFrame.Held & InputButtons.Sprint, Is.EqualTo(InputButtons.None));
            Assert.That(_lastFrame.Released & InputButtons.Sprint, Is.EqualTo(InputButtons.Sprint));
        }

        [Test]
        public void CPublishesOneSlidePressUsingLegacyBitAndControlDoesNot()
        {
            // Owner playtest 2026-09-30: C slides; the Core bit is retained for recordings.
            Assert.That(_gameplay.FindAction("Slide"), Is.Not.Null);
            Assert.That(_gameplay.FindAction("Crouch"), Is.Null);
            _manager.SetInputEnabled(true);
            PushKeys(Key.LeftCtrl);
            _manager.PublishFrame();
            Assert.That(_lastFrame.Held & InputButtons.Crouch, Is.EqualTo(InputButtons.None));
            PushKeys(Key.C);
            _manager.PublishFrame();
            Assert.That(_lastFrame.Pressed & InputButtons.Crouch, Is.EqualTo(InputButtons.Crouch));
            _manager.PublishFrame();
            Assert.That(_lastFrame.Pressed & InputButtons.Crouch, Is.EqualTo(InputButtons.None));
            PushKeys();
            _manager.PublishFrame();
            Assert.That(_lastFrame.Released & InputButtons.Crouch, Is.EqualTo(InputButtons.Crouch));
        }

        [Test]
        public void EndGateReleasesCursorAndRestartDropsBufferedGameplayInput()
        {
            AssertCursor(false);
            Assert.That(_gameplay.enabled, Is.False);
            _manager.SetInputEnabled(true);
            AssertCursor(true);
            PushKeys(Key.W, Key.Space);
            _manager.SetInputEnabled(false);
            AssertCursor(false);
            Assert.That(_gameplay.enabled, Is.False);
            _manager.PublishFrame();
            AssertNeutral();
            PushKeys();
            _manager.SetInputEnabled(true);
            _manager.PublishFrame();
            AssertNeutral();
            PushKeys(Key.W, Key.Space);
            _manager.PublishFrame();
            Assert.That(_lastFrame.Move, Is.EqualTo(Vector2.up));
            Assert.That(_lastFrame.Pressed, Is.EqualTo(InputButtons.Jump));
            AssertCursor(true);
        }

        [Test]
        public void SyntheticFocusAndOwnerGatesReleaseAndReacquireWithoutStaleInput()
        {
            _manager.SetInputEnabled(true);
            PushKeys(Key.Space);
            _driver.SendMessage("OnApplicationFocus", false);
            AssertCursor(false);
            Assert.That(_gameplay.enabled, Is.False);
            _manager.PublishFrame();
            AssertNeutral();
            PushKeys();
            _driver.SendMessage("OnApplicationFocus", true);
            AssertCursor(true);
            _manager.PublishFrame();
            AssertNeutral();
            _manager.enabled = false;
            AssertCursor(false);
            Assert.That(_gameplay.enabled, Is.False);
            _manager.enabled = true;
            AssertCursor(true);
            _manager.SetInputEnabled(false);
            _driver.SendMessage("OnApplicationFocus", false);
            _driver.SendMessage("OnApplicationFocus", true);
            AssertCursor(false);
            Assert.That(_gameplay.enabled, Is.False);
        }

        [Test]
        public void DisabledDriverCannotCaptureEvenWhenRunIsEnabled()
        {
            _manager.SetInputEnabled(true);
            _driver.enabled = false;
            AssertCursor(false);
            _manager.SetInputEnabled(true);
            AssertCursor(false);
            Assert.That(_gameplay.enabled, Is.False);
            PushKeys(Key.Space);
            _manager.PublishFrame();
            AssertNeutral();
            PushKeys();
            _driver.enabled = true;
            _driver.SendMessage("OnApplicationFocus", true);
            AssertCursor(true);
            _manager.PublishFrame();
            AssertNeutral();
        }

        [Test]
        public void UninitializedDuplicateDoesNotReleaseCanonicalCursor()
        {
            _manager.SetInputEnabled(true);
            var duplicate = new GameObject("Duplicate input service test");
            try
            {
                InputManager candidate = duplicate.AddComponent<InputManager>();
                Assert.That(candidate.Initialize(), Is.SameAs(_manager));
                Assert.That(InputManager.Instance, Is.SameAs(_manager));
                AssertCursor(true);
            }
            finally { if (duplicate != null) UnityEngine.Object.DestroyImmediate(duplicate); }
            AssertCursor(true);
            Assert.That(_gameplay.enabled, Is.True);
        }

        [Test]
        public void TeardownRestoresPriorCursorOnceAndDoesNotOverwriteLaterOwner()
        {
            _manager.SetInputEnabled(true);
            _driver.Teardown();
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.False, "Restore the pre-initialization visibility.");
            Cursor.visible = true;
            _driver.Teardown();
            _driver.enabled = false;
            AssertCursor(false);
        }

        [Test]
        public void PlaybackReleasesCursorAndCompletionRecapturesOnlyWhenLiveReady()
        {
            _manager.SetInputEnabled(true);
            var metadata = new RunCaptureMetadata("cursor-test", 42, 0.1f, "source", "config", "Player", 0);
            var records = new[] { new InputProbeRecord(1, 1, default, default, 0.1f) };
            Assert.That(_manager.StartPlayback(metadata, records), Is.True);
            AssertCursor(false);
            Assert.That(_gameplay.enabled, Is.False);
            _manager.PublishFrame();
            Assert.That(_manager.Source, Is.EqualTo(InputSource.Live));
            AssertCursor(true);
            Assert.That(_manager.StartPlayback(metadata, records), Is.True);
            _manager.SetInputEnabled(false);
            Assert.That(_manager.Source, Is.EqualTo(InputSource.Live));
            AssertCursor(false);
            _manager.PublishFrame();
            AssertNeutral();
        }

        [Test]
        public void ReleasedCursorAndIndependentUiClickActionSurviveGameplayDisable()
        {
            using (var ui = new InputActionMap("Cursor lifecycle test UI"))
            {
                ui.devices = new InputDevice[] { _mouse };
                InputAction click = ui.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton");
                int clicks = 0;
                Action<InputAction.CallbackContext> clicked = context => { if (context.ReadValue<float>() > 0.5f) clicks++; };
                click.performed += clicked;
                try
                {
                    ui.Enable();
                    _manager.SetInputEnabled(true);
                    _manager.SetInputEnabled(false);
                    FocusIndependentInputScope.Run(() =>
                    {
                        InputSystem.QueueStateEvent(_mouse, new MouseState().WithButton(MouseButton.Left));
                        InputSystem.Update();
                    });
                    AssertProjectInputSettings("after UI input update");
                    AssertCursor(false);
                    Assert.That(ui.enabled, Is.True);
                    Assert.That(clicks, Is.EqualTo(1));
                    _manager.PublishFrame();
                    AssertNeutral();
                }
                finally { click.performed -= clicked; ui.Disable(); }
            }
        }

        private void PushKeys(params Key[] keys)
        {
            FocusIndependentInputScope.Run(() =>
            {
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
                InputSystem.Update();
            });
            AssertProjectInputSettings("after keyboard input update");
        }

        private void CaptureFrame(InputFrame frame) => _lastFrame = frame;
        private static void AssertCursor(bool captured)
        {
            Assert.That(Cursor.lockState, Is.EqualTo(captured ? CursorLockMode.Locked : CursorLockMode.None));
            Assert.That(Cursor.visible, Is.EqualTo(!captured));
        }
        private void AssertNeutral()
        {
            Assert.That(_lastFrame.Move, Is.EqualTo(Vector2.zero));
            Assert.That(_lastFrame.LookDelta, Is.EqualTo(Vector2.zero));
            Assert.That(_lastFrame.Held | _lastFrame.Pressed | _lastFrame.Released, Is.EqualTo(InputButtons.None));
        }
    }
}
