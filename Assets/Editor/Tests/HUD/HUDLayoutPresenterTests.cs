// ============================================================================
// HUDLayoutPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Proves that default HUD rectangles fit common resolutions through ultrawide.
//   Uses the same pure layout as the runtime document, including selected-slot
//   overhang and number clearance rather than relying on screenshots alone.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · HUD.
// KEY RESPONSIBILITIES:
//   - Verify safe inset, count/inventory separation and persistent health placement.
// DEPENDENCIES:
//   NUnit, HUD pure layout/guidance/inventory presenters and Unity value types.
// USAGE NOTES:
//   Direct pixel-space tests are stricter than Expand's minimum 1920x1080 panel.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Presentation.HUD;
namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDLayoutPresenterTests
    {
        [TestCase(1280, 720)] [TestCase(1920, 1080)] [TestCase(2560, 1080)] [TestCase(3440, 1440)]
        public void AllHudRegionsFitAndInventoryNeverCoversArrowOrCount(float width, float height)
        {
            var p = new HUDLayoutPresenter(); var inventory = new HUDInventoryPresenter();
            Rect safe = p.SafeRect(width, height, .05f), arrow = p.Arrow(safe, 84f);
            float clearance = new HUDGuidancePresenter().CounterBottom(84f, 4f);
            float rowWidth = inventory.FlashlightLeft(112f, 14f, 32f) + 168f;
            Rect row = p.Inventory(safe, rowWidth, 72f, 84f, clearance, 26f, 14f);
            Rect caption = p.Caption(safe, rowWidth, 27f, 84f), health = p.Health(safe, 180f, 35f);
            foreach (var rect in new[] { row, caption, arrow, health })
            {
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(safe.xMin)); Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(safe.yMin));
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(safe.xMax)); Assert.That(rect.yMax, Is.LessThanOrEqualTo(safe.yMax));
            }
            Assert.That(arrow.center.x, Is.EqualTo(width * .5f).Within(.001f));
            Assert.That(row.yMax + 72f * .04f, Is.LessThan(safe.yMax - clearance - 26f));
            Assert.That(row.yMax + 18f, Is.LessThan(caption.yMin), "Overflow and caption have separate bands.");
            Assert.That(health.yMax, Is.LessThan(row.yMin));
            Assert.That(row.xMin - 112f * .04f, Is.GreaterThan(width * .04f));
            Assert.That(safe.xMin, Is.EqualTo(width * .05f));
        }
    }
}
