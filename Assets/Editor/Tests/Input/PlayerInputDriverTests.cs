// ============================================================================
// PlayerInputDriverTests.cs
// ============================================================================
// PURPOSE:
//   Verifies cursor ownership and input gates through real component lifecycles.
//   Virtual devices exercise Input System callbacks without claiming hardware input.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Input.
// KEY RESPONSIBILITIES:
//   - Check readiness, end/restart, focus, owner/component disable and teardown.
//   - Protect canonical-service cursor ownership and live/playback transitions.
//   - Verify independent UI click actions survive the gameplay gate closing.
//   - Preserve physical Shift press/hold/release through the Sprint input contract.
// DEPENDENCIES:
//   - Input presentation components, Core records, Unity Input System and Test Framework.
// USAGE NOTES:
//   Coordinator runs under the Unity lease. Test Framework supplies/restores its
//   isolated scene. Creates only temporary objects/devices, scopes the gameplay map
//   to those devices, pairs events, and restores cursor state after every test.
//   Focus signals are synthetic; actual focus and rendered Results clicks remain
//   integration checks. No assets or global Input System settings are modified.
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
    public sealed class PlayerInputDriverTests
    {
        private GameObject _root;
        private InputManager _manager;
        private PlayerInputDriver _driver;
        private InputDriverConfig _config;
        private InputActionMap _gameplay;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private CursorLockMode _previousLock;
        private bool _previousVisible;
        private bool _cursorSnapshotTaken;
        private InputFrame _lastFrame;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            Assert.That(InputManager.Instance, Is.Null, "Requires the Test Framework isolated scene.");
            _previousLock = Cursor.lockState;
            _previousVisible = Cursor.visible;
            _cursorSnapshotTaken = true;
            _lastFrame = default;
            // A hidden, unlocked cursor makes restoration distinct from the UI state.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
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

        [UnityTearDown]
        public IEnumerator TearDown()
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
            if (Application.isPlaying) yield return new ExitPlayMode();
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
                    InputSystem.QueueStateEvent(_mouse, new MouseState().WithButton(MouseButton.Left));
                    InputSystem.Update();
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
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            InputSystem.Update();
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
