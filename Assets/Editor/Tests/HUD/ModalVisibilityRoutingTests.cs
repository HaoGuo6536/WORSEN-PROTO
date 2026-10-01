// ============================================================================
// ModalVisibilityRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Checks the applied document visibility exposed to the HUD's modal getter.
//   Native UI Toolkit checks complement the existing health-preserving route test.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · HUD.
// KEY RESPONSIBILITIES:
//   - Verify Hide, catch deferral, document disable and teardown are not modal.
// DEPENDENCIES:
//   Core snapshots, ProgressionUI, UI Toolkit and NUnit.
// USAGE NOTES:
//   Coordinator-run Edit Mode; no scene or asset writes. Teardown precedes disposal.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Core;
using Worsen.Presentation.ProgressionUI;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ModalVisibilityRoutingTests
    {
        [Test] public void AppliedDocumentVisibilityTracksHideCatchDeferralAndDisable()
        {
            var owner = new GameObject("Modal getter test"); owner.SetActive(false);
            var document = owner.AddComponent<UIDocument>();
            var panel = ScriptableObject.CreateInstance<PanelSettings>(); document.panelSettings = panel;
            var config = ScriptableObject.CreateInstance<ProgressionUIDriverConfig>();
            var driver = owner.AddComponent<ProgressionUIDriver>();
            var manager = owner.AddComponent<ProgressionUIManager>();
            try
            {
                owner.SetActive(true); manager.Initialize(config);
                manager.SetSnapshot(Snapshot(1, ProgressionPhase.Shop));
                Assert.That(driver.IsModalVisible && manager.IsModalVisible, Is.True);
                manager.Hide(); Assert.That(manager.IsModalVisible, Is.False);
                manager.SetSnapshot(Snapshot(2, ProgressionPhase.Exploring));
                var player = new EntityId(1); manager.PrepareCatch(player);
                manager.SetSnapshot(Snapshot(3, ProgressionPhase.Ended));
                Assert.That(manager.IsModalVisible, Is.False, "A deferred terminal snapshot is not applied.");
                manager.EndCatch(player); Assert.That(manager.IsModalVisible, Is.True);
                document.enabled = false; Assert.That(manager.IsModalVisible, Is.False);
                document.enabled = true; manager.enabled = false; Assert.That(manager.IsModalVisible, Is.False);
                manager.Teardown(); Assert.That(manager.IsModalVisible, Is.False);
            }
            finally { manager.Teardown(); Object.DestroyImmediate(owner); Object.DestroyImmediate(config); Object.DestroyImmediate(panel); }
        }
        private static ProgressionSnapshot Snapshot(int revision, ProgressionPhase phase) =>
            new ProgressionSnapshot(revision, 1, 1, 123, 0, 0, 0, phase, 0, 100,
                null, null, null, default, "", phase == ProgressionPhase.Shop, phase == ProgressionPhase.Ended);
    }
}
