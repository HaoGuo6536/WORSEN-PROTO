// ============================================================================
// HUDCompassVisualTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the caption-free compass surface and its repeated document lifecycle.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10) - test suite (section 11) - Presentation - HUD.
// KEY RESPONSIBILITIES:
//   - Ensure bind/unbind leaves no duplicate indicator or obsolete caption.
//   - Preserve unavailable-target and chase visibility decisions at the view boundary.
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
                state.DirectionVisible = true; state.ChaseMode = true; driver.Apply(state);
                Assert.That(root.Q("hud-extra").style.display.value, Is.EqualTo(DisplayStyle.None));
                state.ChaseMode = false; driver.Apply(state);
                Assert.That(root.Q("hud-extra").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                // Edit Mode does not exercise ordinary runtime MonoBehaviour callbacks.
                driver.Unbind();
                Object.DestroyImmediate(owner);
                Assert.That(root.childCount, Is.Zero);
            }
            finally { if (owner != null) Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        }
    }
}
