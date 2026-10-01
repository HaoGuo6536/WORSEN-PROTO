// ============================================================================
// HeldItemDriverTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the real view-model object lifetime on a temporary output camera.
//   Inventory snapshots drive selection and exhaustion; this does not pretend to
//   test Session consumption, which is covered by ConsumableRoutingTests.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · HeldItem.
// KEY RESPONSIBILITIES:
//   - Verify arm-free camera parenting, collider removal, suppression and teardown.
//   - Diagnose remaining child identities and pose state after real rendered frames.
// DEPENDENCIES:
//   NUnit, Unity Test Framework, Core and HeldItem presentation components.
// USAGE NOTES:
//   Native Edit Mode timing plus a coordinator-only Play Mode frame-order smoke test.
//   Teardown is explicit before destroying owners.
//   Does not save assets, load project scenes or depend on window focus.
// ============================================================================
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Presentation.HeldItem;
namespace Worsen.Tests.HeldItem
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class HeldItemDriverTests
    {
        [Test, Category("NativeEditMode")]
        public void ExhaustionFinishesLoweringAndTeardownDestroysOwnedObjects()
        {
            var owner = new GameObject("Held item deterministic test");
            var viewOwner = new GameObject("Held item deterministic camera");
            var driver = owner.AddComponent<HeldItemDriver>();
            var view = viewOwner.AddComponent<UnityEngine.Camera>();
            var config = ScriptableObject.CreateInstance<HeldItemDriverConfig>();
            try
            {
                Assert.That(Application.isPlaying, Is.False);
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                Assert.That(shader, Is.Not.Null);
                typeof(HeldItemDriverConfig).GetField("_shader", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, shader);
                driver.Initialize(view, config);
                var snapshot = new ConsumableInventorySnapshot(new[] { new ProgressionInventorySlot("gauze", "Gauze", 0) }, new[] { 1 }, 0);
                driver.SetConsumables(snapshot); driver.Tick(config.TransitionSeconds);
                var item = view.transform.Find("Held gauze");
                Assert.That(item, Is.Not.Null);
                var body = item.GetComponentInChildren<Renderer>().sharedMaterial;
                Assert.That(item.GetComponentsInChildren<Collider>(), Is.Empty);
                float raisedY = item.localPosition.y;
                driver.SetConsumables(new ConsumableInventorySnapshot(snapshot.Inventory, new[] { 0 }, 0));
                driver.Tick(config.TransitionSeconds * .5f);
                Assert.That(item != null && item.gameObject.activeSelf, Is.True, "The exhausted item must lower before removal.");
                Assert.That(item.localPosition.y, Is.LessThan(raisedY));
                driver.Tick(config.TransitionSeconds);
                Assert.That(item == null, Is.True);
                Assert.That(view.transform.childCount, Is.Zero);
                driver.SetConsumables(snapshot); driver.Tick(config.TransitionSeconds);
                var replacement = view.transform.Find("Held gauze");
                Assert.That(replacement, Is.Not.Null);
                driver.Teardown(); driver.Teardown();
                Assert.That(view.transform.childCount, Is.Zero);
                Assert.That(replacement == null && body == null, Is.True);
            }
            finally
            {
                driver.Teardown();
                Object.DestroyImmediate(owner); Object.DestroyImmediate(viewOwner); Object.DestroyImmediate(config);
            }
        }

        [UnityTest]
        public IEnumerator SelectedItemAppearsWithoutArmsAndExhaustedSnapshotLowersIt()
        {
            yield return new EnterPlayMode();
            var owner = new GameObject("Held item service test");
            var viewOwner = new GameObject("Held item camera test");
            var view = viewOwner.AddComponent<UnityEngine.Camera>();
            var manager = owner.AddComponent<HeldItemManager>();
            var config = ScriptableObject.CreateInstance<HeldItemDriverConfig>();
            bool previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                Assert.That(shader, Is.Not.Null);
                typeof(HeldItemDriverConfig).GetField("_shader", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, shader);
                manager.Initialize(view, config);
                Assert.That(view.transform.childCount, Is.Zero, "Initialization creates materials, not a persistent camera child.");
                manager.SetConsumables(new ConsumableInventorySnapshot(new[] { new ProgressionInventorySlot("gauze", "Gauze", 0) }, new[] { 1 }, 0));
                yield return new WaitForSeconds(.4f);
                var item = view.transform.Find("Held gauze"); Assert.That(item, Is.Not.Null);
                Assert.That(item.gameObject.activeSelf, Is.True); Assert.That(item.localPosition.x, Is.GreaterThan(0f));
                Assert.That(item.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(3));
                Assert.That(item.GetComponentsInChildren<Collider>(), Is.Empty);
                Assert.That(item.GetComponentsInChildren<Animator>(), Is.Empty);
                manager.SetSuppressed(true); Assert.That(item.gameObject.activeSelf, Is.False);
                manager.SetSuppressed(false); yield return new WaitForSeconds(.4f);
                manager.SetConsumables(new ConsumableInventorySnapshot(new[] { default(ProgressionInventorySlot) }, new[] { 0 }, 0));
                // Observe the actual hierarchy, not a fixed wait which can resume
                // before LateUpdate. Keep a wall-clock bound and report precisely
                // what remains; do not accept a hidden or pooled item as removal.
                double deadline = Time.realtimeSinceStartupAsDouble + 5d;
                while (view.transform.childCount != 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(view.transform.childCount, Is.Zero, DescribeRemaining(view, owner.GetComponent<HeldItemDriver>()));
                yield return null; // Native destruction follows synchronous detachment.
                Assert.That(item == null, Is.True, "Exhaustion destroys, rather than merely hiding, the view model.");
                manager.SetConsumables(new ConsumableInventorySnapshot(new[] { new ProgressionInventorySlot("gauze", "Gauze", 0) }, new[] { 1 }, 0));
                yield return new WaitForSeconds(.4f); yield return null;
                var replacement = view.transform.Find("Held gauze"); Assert.That(replacement, Is.Not.Null);
                var ownedMaterial = replacement.GetComponentInChildren<Renderer>().sharedMaterial;
                manager.Teardown(); manager.Teardown();
                Assert.That(view.transform.childCount, Is.Zero);
                yield return null;
                Assert.That(replacement == null && ownedMaterial == null, Is.True);
            }
            finally
            {
                manager.Teardown();
                Object.DestroyImmediate(owner); Object.DestroyImmediate(viewOwner); Object.DestroyImmediate(config);
                Application.runInBackground = previousBackground;
            }
            yield return new ExitPlayMode();
        }

        private static string DescribeRemaining(UnityEngine.Camera view, HeldItemDriver driver)
        {
            var state = (HeldItemDriverState)typeof(HeldItemDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
            return "Remaining hierarchy: " + string.Join("; ", view.GetComponentsInChildren<Transform>(true)
                .Where(child => child != view.transform).Select(child => child.name + " active=" + child.gameObject.activeSelf +
                    " local=" + child.localPosition)) + "; selected=" + state.SelectedId + "; displayed=" + state.DisplayedId +
                "; raise=" + state.Raise + "; suppressed=" + state.Suppressed + "; driverEnabled=" + driver.isActiveAndEnabled +
                "; timeScale=" + Time.timeScale;
        }

        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
