// ============================================================================
// HUDCompassVisualTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the caption-free compass surface and its repeated document lifecycle.
//   Chrome can disappear without hiding guidance or introducing chase-state labels.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10) - test suite (section 11) - Presentation - HUD.
// KEY RESPONSIBILITIES:
//   - Ensure bind/unbind leaves no duplicate indicator or obsolete caption.
//   - Preserve guidance in chases; forbid obsolete chrome and hints.
//   - Keep three slots, empty selection and flashlight under the same chase gate.
//   - Keep one remaining number above its selected arrow, with no separate golden label.
// DEPENDENCIES:
//   NUnit, HUD presentation, Unity objects and UI Toolkit.
// USAGE NOTES:
//   Edit Mode engine tests; temporary objects are destroyed in finally blocks.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Presentation.HUD;
using Worsen.Core;
namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDCompassVisualTests
    {
        [Test]
        public void OneNumberFollowsWhiteThenGoldenArrowWithTintAndRotationClearance()
        {
            var owner = new GameObject("Counter attachment test");
            var config = ScriptableObject.CreateInstance<HUDDriverConfig>();
            var driver = owner.AddComponent<HUDVisualDriver>(); var root = new VisualElement();
            var state = new HUDDriverState(); var presenter = new HUDPresenter(); var guidance = new HUDGuidancePresenter();
            try
            {
                driver.Bind(root, config);
                presenter.SetCount(state, 3, 8); presenter.SetGoldenCount(state, 2, 6);
                var white = new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.forward, Vector3.forward, 1);
                var gold = new GuidanceTarget(GuidanceKind.GoldenSense, Vector3.right, Vector3.right, 2);
                presenter.SetGuidance(state, new[] { white, gold }); driver.Apply(state);
                var label = root.Q<Label>("cake-count"); var panel = root.Q("hud");
                Assert.That(label.text, Is.EqualTo("5"));
                Assert.That(label.style.color.value, Is.EqualTo(Color.white));
                Assert.That(panel.parent, Is.SameAs(root.Q("direction-group")));
                Assert.That(panel.style.bottom.value.value, Is.EqualTo(guidance.CounterBottom(config.CompassSize, config.CounterArrowGap)));
                Assert.That(panel.style.position.value, Is.EqualTo(Position.Absolute));
                Assert.That(panel.style.left.value.value, Is.Zero); Assert.That(panel.style.right.value.value, Is.Zero);
                presenter.SetGuidance(state, new[] { gold }); driver.Apply(state);
                Assert.That(root.Q<Label>("cake-count"), Is.SameAs(label));
                Assert.That(label.text, Is.EqualTo("4"));
                Assert.That(label.style.color.value, Is.EqualTo(config.GoldenSenseColor));
                Assert.That(panel.parent, Is.SameAs(root.Q("golden-direction-group")));
                Assert.That(root.Q("golden-count"), Is.Null);
                Assert.That(root.Query<Label>("cake-count").ToList().Count, Is.EqualTo(1));
                presenter.SetGuidance(state, new[] { new GuidanceTarget(GuidanceKind.GoldenSense, Vector3.left, Vector3.right, 2) });
                guidance.Tick(state, .1f, 90f); driver.Apply(state);
                Assert.That(root.Q("golden-direction-cue").style.rotate.value.angle.value, Is.EqualTo(state.DisplayGoldenArrowDegrees));
                Assert.That(state.DisplayGoldenArrowDegrees, Is.Not.EqualTo(state.GoldenSenseArrowDegrees));
                state.HiddenCount = true; driver.Apply(state);
                Assert.That(panel.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(root.Q("golden-direction-group").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                state.HiddenCount = false; presenter.SetGuidance(state, null); driver.Apply(state);
                Assert.That(panel.style.display.value, Is.EqualTo(DisplayStyle.None));
            }
            finally { driver.Unbind(); Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        }

        [Test]
        public void RepeatedBindUnbindOwnsOneNeedleAndNoCaption()
        {
            var owner = new GameObject("Compass lifecycle test");
            var config = ScriptableObject.CreateInstance<HUDDriverConfig>();
            var root = new VisualElement();
            try
            {
                var driver = owner.AddComponent<HUDVisualDriver>();
                for (int pass = 0; pass < 2; pass++)
                {
                    driver.Bind(root, config);
                    driver.Apply(new HUDDriverState { DirectionVisible = true, ViewDirection = Vector3.up });
                    Assert.That(root.Q("direction-caption"), Is.Null);
                    Assert.That(root.Query<VisualElement>("direction-cue").ToList().Count, Is.EqualTo(1));
                    Assert.That(root.Q("direction-cue").style.width.value.value, Is.EqualTo(config.CompassSize));
                    Assert.That(root.Q("direction-group").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                    driver.Unbind(); driver.Unbind();
                    Assert.That(root.childCount, Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        }

        [Test]
        public void TargetAndChaseSuppressionRemainAtTheViewBoundary()
        {
            var owner = new GameObject("Compass visibility test");
            var config = ScriptableObject.CreateInstance<HUDDriverConfig>();
            var root = new VisualElement();
            try
            {
                var driver = owner.AddComponent<HUDVisualDriver>(); driver.Bind(root, config);
                var state = new HUDDriverState(); driver.Apply(state);
                Assert.That(root.Q("direction-group").style.display.value, Is.EqualTo(DisplayStyle.None));
                var presenter = new HUDPresenter();
                presenter.SetCount(state, 0, 4);
                presenter.SetDirection(state, Vector3.forward, true);
                presenter.SetConsumables(state, new ConsumableInventorySnapshot(
                    new[] { new ProgressionInventorySlot("gauze", "Gauze", 4) }, new[] { 1 }, 0), 8);
                presenter.SetChaseMode(state, true); driver.Apply(state);
                Assert.That(root.Q("hud-extra").style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(root.Q("selected-consumable").parent, Is.SameAs(root.Q("hud-extra")));
                Assert.That(root.Q<Label>("selected-consumable").text, Is.EqualTo("1: Gauze ×1"));
                Assert.That(root.Q("shield"), Is.Null);
                Assert.That(root.Q("hud").style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(root.Q("direction-group").parent, Is.SameAs(root));
                Assert.That(root.Q("direction-group").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("chase-warning"), Is.Null);
                foreach (Label label in root.Query<Label>().ToList())
                    Assert.That(label.text, Does.Not.Contain("HUNTED"));
                Assert.That(root.Q("item-slots"), Is.Not.Null);
                Assert.That(root.Q("controls-hint"), Is.Null);
                Assert.That(root.Q("objective-title"), Is.Null);
                Assert.That(root.Q("cake-gauge"), Is.Null);
                Assert.That(root.Q("exit-state"), Is.Null);
                Assert.That(root.Q("item-slots").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                presenter.SetGoldenCount(state, 4); driver.Apply(state);
                Assert.That(root.Q<Label>("golden-count"), Is.Null);
                presenter.SetChaseMode(state, false);
                presenter.Tick(state, config.RestoreSeconds, config.RestoreSeconds); driver.Apply(state);
                Assert.That(root.Q("hud-extra").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("hud").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("hud").style.opacity.value, Is.EqualTo(1f));
                Assert.That(root.Q("hud-extra").style.opacity.value, Is.EqualTo(1f));
                presenter.SetConsumables(state, new ConsumableInventorySnapshot(
                    new[] { default(ProgressionInventorySlot) }, new[] { 0 }, 0), 8); driver.Apply(state);
                Assert.That(root.Q("item-slots").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("selected-consumable").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q<Label>("selected-consumable").text, Is.EqualTo("1: Empty"));
                Assert.That(root.Q("item-slot-1"), Is.Not.Null);
                Assert.That(root.Q("item-slot-2"), Is.Not.Null);
                Assert.That(root.Q("item-slot-3"), Is.Not.Null);
                Assert.That(root.Q("item-slot-4"), Is.Null);
                Assert.That(root.Q("flashlight-slot").parent, Is.SameAs(root.Q("inventory-panel")));
                new HUDInventoryPresenter().SetFlashlight(state, true, 1f, .5f); driver.Apply(state);
                Assert.That(root.Q("flashlight-aim").style.width.value.value, Is.EqualTo(50f));
                Assert.That(root.Q<Label>("flashlight-status").text, Is.EqualTo("Aim 50%"));
                // Edit Mode does not exercise ordinary runtime MonoBehaviour callbacks.
                driver.Unbind();
                Object.DestroyImmediate(owner);
                Assert.That(root.childCount, Is.Zero);
            }
            finally { if (owner != null) Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        }
    }
}
