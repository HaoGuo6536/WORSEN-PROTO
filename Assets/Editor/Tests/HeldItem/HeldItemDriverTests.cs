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
// DEPENDENCIES:
//   NUnit, Unity Test Framework, Core and HeldItem presentation components.
// USAGE NOTES:
//   Coordinator-only Play Mode test. Teardown is explicit before destroying owners.
//   Does not save assets, load project scenes or depend on window focus.
// ============================================================================
using System.Collections;
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
        [UnityTest]
        public IEnumerator SelectedItemAppearsWithoutArmsAndExhaustedSnapshotLowersIt()
        {
            yield return new EnterPlayMode();
            var owner = new GameObject("Held item service test");
            var viewOwner = new GameObject("Held item camera test");
            var view = viewOwner.AddComponent<UnityEngine.Camera>();
            var manager = owner.AddComponent<HeldItemManager>();
            var config = ScriptableObject.CreateInstance<HeldItemDriverConfig>();
            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                Assert.That(shader, Is.Not.Null);
                typeof(HeldItemDriverConfig).GetField("_shader", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, shader);
                manager.Initialize(view, config);
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
                yield return new WaitForSeconds(.4f);
                Assert.That(view.transform.childCount, Is.Zero);
                manager.Teardown(); manager.Teardown();
                Assert.That(view.transform.childCount, Is.Zero);
            }
            finally
            {
                manager.Teardown();
                Object.DestroyImmediate(owner); Object.DestroyImmediate(viewOwner); Object.DestroyImmediate(config);
            }
            yield return new ExitPlayMode();
        }
    }
}
