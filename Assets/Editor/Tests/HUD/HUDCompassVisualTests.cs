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
//   - Preserve guidance in chases; forbid chrome, hints and empty slot outlines.
// DEPENDENCIES:
//   NUnit, HUD presentation, Unity objects and UI Toolkit.
// USAGE NOTES:
//   Edit Mode engine tests; temporary objects are destroyed in finally blocks.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Presentation.HUD;
namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDCompassVisualTests
    {
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
                presenter.SetDirection(state, Vector3.forward, true);
                presenter.SetItemSlots(state, 2, 8);
                presenter.SetChaseMode(state, true); driver.Apply(state);
                Assert.That(root.Q("hud-extra").style.display.value, Is.EqualTo(DisplayStyle.None));
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
                Assert.That(root.Q("item-slots").style.display.value, Is.EqualTo(DisplayStyle.None));
                presenter.SetGoldenCount(state, 4); driver.Apply(state);
                Assert.That(root.Q<Label>("golden-count").text, Is.EqualTo("Golden: 4"));
                presenter.SetChaseMode(state, false);
                presenter.Tick(state, config.RestoreSeconds, config.RestoreSeconds); driver.Apply(state);
                Assert.That(root.Q("hud-extra").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("hud").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("hud").style.opacity.value, Is.EqualTo(1f));
                Assert.That(root.Q("hud-extra").style.opacity.value, Is.EqualTo(1f));
                // Edit Mode does not exercise ordinary runtime MonoBehaviour callbacks.
                driver.Unbind();
                Object.DestroyImmediate(owner);
                Assert.That(root.childCount, Is.Zero);
            }
            finally { if (owner != null) Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        }
    }
}
