// ============================================================================
// ConsumableInputTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the actual code-defined bindings and one-shot input buffering.
//   Consumable controls must not also activate look-back or toggle the flashlight.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Input.
// KEY RESPONSIBILITIES:
//   - Check direct keys, existing keyboard/gamepad/wheel paths and one-shot buffering.
// DEPENDENCIES:
//   - Core, Input presentation, Run/HorrorEffects controllers, NUnit and Input System.
// USAGE NOTES:
//   Edit Mode; temporary action maps are disposed and no devices or assets are modified.
// ============================================================================
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Worsen.Core;
using Worsen.Presentation.Input;
using Worsen.Session.HorrorEffects;
using Worsen.Session.Run;

namespace Worsen.Tests.Input
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ConsumableInputTests
    {
        [Test]
        public void CodeDefinedBindingsDoNotShareConsumableAndLookBackControls()
        {
            var owner = new GameObject("Consumable binding fixture");
            InputActionMap map = null; InputAction pause = null;
            try
            {
                var driver = owner.AddComponent<PlayerInputDriver>();
                typeof(PlayerInputDriver).GetMethod("BuildActionMap", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(driver, null);
                map = (InputActionMap)typeof(PlayerInputDriver).GetField("_actions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(driver);
                pause = (InputAction)typeof(PlayerInputDriver).GetField("_pause", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(driver);
                Assert.That(map["UseConsumable"].bindings.Select(b => b.path), Is.EquivalentTo(new[] { "<Keyboard>/q", "<Gamepad>/rightShoulder" }));
                Assert.That(map["CycleConsumable"].bindings.Select(b => b.path), Is.EquivalentTo(new[] { "<Mouse>/scroll/up", "<Gamepad>/dpad/right" }));
                Assert.That(map["CycleConsumablePrevious"].bindings.Select(b => b.path), Is.EquivalentTo(new[] { "<Mouse>/scroll/down", "<Gamepad>/dpad/left" }));
                Assert.That(map["LookBack"].bindings.Select(b => b.path), Is.EquivalentTo(new[] { "<Keyboard>/tab", "<Gamepad>/rightStickPress" }));
                Assert.That(map["UseItem"].bindings.Select(b => b.path), Is.EquivalentTo(new[] { "<Keyboard>/f", "<Gamepad>/leftShoulder" }));
                for (int i = 1; i <= 3; i++)
                    Assert.That(map["SelectSlot" + i].bindings.Select(b => b.path), Is.EquivalentTo(new[] { "<Keyboard>/" + i }));
                Assert.That(map["Slide"].bindings.Select(b => b.path), Does.Contain("<Keyboard>/c"));
            }
            finally { map?.Dispose(); pause?.Dispose(); Object.DestroyImmediate(owner); }
        }
        [TestCase(InputButtons.UseConsumable, 0)]
        [TestCase(InputButtons.CycleConsumable, 1)]
        [TestCase(InputButtons.CycleConsumablePrevious, -1)]
        public void TapReachesRunOnceAndDoesNotBecomeFlashlightOrLookBack(InputButtons button, int direction)
        {
            var state = new InputDriverState(); var input = new InputFramePresenter();
            input.SetInputEnabled(state, true); input.SetOwnerEnabled(state, true); input.SetFocus(state, true);
            input.SetMove(state, Vector2.up); input.SetButton(state, button, true); input.SetButton(state, button, false);
            var run = new RunSessionController(new RunSessionBehaviorState(1), new System.Random(1)); run.StartScene(SceneKey.HorrorRun);
            run.ReceiveInput(input.Flush(state)); run.TryTick(.02f, out var frame);
            Assert.That(frame.Move, Is.EqualTo(Vector2.up)); Assert.That(frame.Pressed, Is.EqualTo(button));
            Assert.That(frame.Released, Is.EqualTo(button)); Assert.That(ConsumableController.CycleDirection(frame), Is.EqualTo(direction));
            run.ReceiveInput(input.Flush(state)); run.TryTick(.02f, out frame); Assert.That(frame.Pressed, Is.EqualTo(InputButtons.None));
        }
    }
}
