// ============================================================================
// InputPauseActionTests.cs
// ============================================================================
// PURPOSE:
//   Checks the pause action lifetime independently of gameplay action enablement.
//   Gate transitions must discard stale frames without losing input on repeated acknowledgements.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Input boundary.
// KEY RESPONSIBILITIES:
//   - Verify Escape/Start bindings, focus/owner admission and input-buffer clearing.
// DEPENDENCIES:
//   NUnit, Input System, Core, Input Driver/Presenter state and pause cleanup support.
// USAGE NOTES:
//   Edit Mode transient objects only. No persistent Manager, asset mutation or IO.
//   Native device presses and cursor behavior in a build remain coordinator checks.
//   Reflection invokes lifecycle without SendMessage; teardown clears pause globals.
// ============================================================================
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Worsen.Core;
using Worsen.Presentation.Input;
using Worsen.Tests.Menu;
namespace Worsen.Tests.Input
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class InputPauseActionTests
    {
        private GameObject _owner;
        private PlayerInputDriver _driver;
        private InputDriverConfig _config;
        private InputDriverState _state;
        private InputAction _pause;
        private InputActionMap _gameplay;
        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Independent pause action test");
            _driver = _owner.AddComponent<PlayerInputDriver>();
            _config = ScriptableObject.CreateInstance<InputDriverConfig>();
            Field("_config").SetValue(_driver, _config);
            _driver.Initialize();
            _state = (InputDriverState)Field("_state").GetValue(_driver);
            _pause = (InputAction)Field("_pause").GetValue(_driver);
            _gameplay = (InputActionMap)Field("_actions").GetValue(_driver);
            _driver.SetOwnerEnabled(true);
            Focus(true);
        }
        [TearDown]
        public void TearDown()
        {
            try
            {
                if (_driver != null) _driver.Teardown();
                Object.DestroyImmediate(_owner); Object.DestroyImmediate(_config);
            }
            finally { PauseFixtureCleanup.Restore(); }
        }
        [Test]
        public void PauseIsSeparateAndAvailableWithGameplayDisabledButRespectsFocusAndOwner()
        {
            Assert.That(_pause.actionMap, Is.Not.SameAs(_gameplay));
            Assert.That(_pause.bindings.Count, Is.EqualTo(2));
            Assert.That(_pause.bindings[0].path, Is.EqualTo("<Keyboard>/escape"));
            Assert.That(_pause.bindings[1].path, Is.EqualTo("<Gamepad>/start"));
            _driver.SetInputEnabled(false);
            Assert.That(_gameplay.enabled, Is.False); Assert.That(_pause.enabled, Is.True);
            Focus(false);
            Assert.That(_pause.enabled, Is.False);
            Focus(true);
            Assert.That(_pause.enabled, Is.True);
            _driver.SetOwnerEnabled(false);
            Assert.That(_pause.enabled, Is.False);
        }
        [Test]
        public void ClosingAndOpeningGameplayGateClearInputButRepeatedOpenPreservesHeldValues()
        {
            _driver.SetInputEnabled(true);
            _state.Move = Vector2.up; _state.Pressed = InputButtons.Jump;
            _driver.SetInputEnabled(true);
            Assert.That(_state.Move, Is.EqualTo(Vector2.up));
            _driver.SetInputEnabled(false);
            Assert.That(_state.Move, Is.EqualTo(Vector2.zero)); Assert.That(_state.Pressed, Is.EqualTo(InputButtons.None));
            _state.LookDelta = Vector2.one; _state.Held = InputButtons.Sprint;
            _driver.SetInputEnabled(true);
            Assert.That(_state.LookDelta, Is.EqualTo(Vector2.zero)); Assert.That(_state.Held, Is.EqualTo(InputButtons.None));
        }
        private static FieldInfo Field(string name) => typeof(PlayerInputDriver).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private void Focus(bool focused) => typeof(PlayerInputDriver).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_driver, new object[] { focused });
    }
}
