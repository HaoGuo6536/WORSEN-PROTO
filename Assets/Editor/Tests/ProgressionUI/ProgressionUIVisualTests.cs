// ============================================================================
// ProgressionUIVisualTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies that the visual surface applies the presenter's health visibility.
//   Both the number and gauge belong to the shelter modal, so neither can leak
//   into the floor status panel or survive a generation transition or rebind.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · ProgressionUI.
//
// KEY RESPONSIBILITIES:
//   - Verify health text and gauge display for every progression phase.
//   - Verify repeated binding preserves suppression without duplicate readouts.
//
// DEPENDENCIES:
//   Core progression snapshots, ProgressionUI, NUnit, Unity objects and UI Toolkit.
//
// USAGE NOTES:
//   Edit Mode engine tests; temporary objects are destroyed in finally blocks.
//   These tests inspect the UI tree; live layout and rendering require scene checks.
//
// ============================================================================

using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Core;
using Worsen.Presentation.ProgressionUI;

namespace Worsen.Tests.ProgressionUI
{
    public sealed class ProgressionUIVisualTests
    {
        [TestCase(ProgressionPhase.Dormant, false)]
        [TestCase(ProgressionPhase.ChooseThreat, true)]
        [TestCase(ProgressionPhase.ChooseCurse, true)]
        [TestCase(ProgressionPhase.Generating, false)]
        [TestCase(ProgressionPhase.Exploring, false)]
        [TestCase(ProgressionPhase.Shop, true)]
        [TestCase(ProgressionPhase.Ended, true)]
        [TestCase(ProgressionPhase.GenerationFailed, true)]
        public void HealthNumberAndGaugeFollowPresenterVisibilityAcrossRebinding(ProgressionPhase phase, bool visible)
        {
            var owner = new GameObject("Progression health visibility test");
            var config = ScriptableObject.CreateInstance<ProgressionUIDriverConfig>();
            var root = new VisualElement();
            try
            {
                var driver = owner.AddComponent<ProgressionUIVisualDriver>();
                var presenter = new ProgressionUIPresenter();
                var state = new ProgressionUIDriverState();
                for (int pass = 0; pass < 2; pass++)
                {
                    driver.Bind(root, config);
                    presenter.Present(state, Snapshot(pass * 2 + 1, ProgressionPhase.Shop));
                    driver.Apply(state);
                    presenter.Present(state, Snapshot(pass * 2 + 2, phase));
                    driver.Apply(state);
                    var health = root.Q<Label>("health");
                    var gauge = root.Q("health-gauge");
                    var panel = root.Q("progression-panel");
                    Assert.That(health.parent, Is.SameAs(panel));
                    Assert.That(gauge.parent, Is.SameAs(panel));
                    Assert.That(health.style.display.value, Is.EqualTo(visible ? DisplayStyle.Flex : DisplayStyle.None));
                    Assert.That(gauge.style.display.value, Is.EqualTo(visible ? DisplayStyle.Flex : DisplayStyle.None));
                    Assert.That(health.text, Is.EqualTo(visible ? "HEALTH  75 / 100" : ""));
                    Assert.That(root.Q<Label>("retained-counts").text, Does.Not.Contain("HEALTH"));
                    Assert.That(root.Q("progression-status").Q("health"), Is.Null);
                    Assert.That(root.Query<VisualElement>("health-gauge").ToList().Count, Is.EqualTo(1));
                    driver.Unbind(); driver.Unbind();
                    Assert.That(root.childCount, Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        }

        private static ProgressionSnapshot Snapshot(int revision, ProgressionPhase phase)
            => new ProgressionSnapshot(revision, 1, 3, 123, 3, 2, 1, phase, 75f, 100f,
                null, null, null, default, "", phase == ProgressionPhase.Shop,
                phase == ProgressionPhase.Ended || phase == ProgressionPhase.GenerationFailed);
    }
}
