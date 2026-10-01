// ============================================================================
// BlinderProjectileDriverTests.cs
// ============================================================================
// PURPOSE:
//   Verifies native projectile rendering objects and symmetric cleanup.
//   It exercises the component and the owning Manager/Driver command boundary.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Horror.
// KEY RESPONSIBILITIES:
//   - Check visible collider-free body/trail, duplicate identity and cleanup.
// DEPENDENCIES:
//   Horror sub-driver/config, Core facts, Unity objects, reflection and NUnit.
// USAGE NOTES:
//   Coordinator-run Edit Mode. Always teardown before destroying native owners.
// ============================================================================
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Horror;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Horror
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class BlinderProjectileDriverTests
    {
        [Test] public void BodiesAndTrailsAreVisibleNonCollidingAndReleased()
        {
            var owner = new GameObject("Blinder visual test");
            var driver = owner.AddComponent<BlinderProjectileDriver>();
            var config = ScriptableObject.CreateInstance<HorrorDriverConfig>();
            try
            {
                var shader = Shader.Find("Sprites/Default"); Assert.That(shader, Is.Not.Null);
                typeof(HorrorDriverConfig).GetField("_webShader", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, shader);
                driver.Initialize(config);
                var shot = new BlinderThrowFact(new EntityId(7), 1, 2, Vector3.zero, Vector3.forward, .06f, 12f, 16f);
                driver.Observe(shot); driver.Observe(shot);
                driver.Observe(new BlinderThrowFact(new EntityId(8), 1, 2, Vector3.right, Vector3.one, .06f, 12f, 16f));
                Assert.That(driver.VisualCount, Is.EqualTo(2));
                Assert.That(owner.GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(2));
                Assert.That(owner.GetComponentsInChildren<Collider>(), Is.Empty);
                driver.Tick(.1f);
                var trail = owner.GetComponentsInChildren<LineRenderer>()[0];
                Assert.That(trail.sharedMaterial, Is.Not.Null); Assert.That(trail.widthMultiplier, Is.GreaterThan(0f));
                Assert.That(Vector3.Distance(trail.GetPosition(0), trail.GetPosition(1)), Is.EqualTo(1.2f).Within(.001f));
                driver.ObserveHit(new BlinderHitFact(new EntityId(7), new EntityId(1), 2, 2, 3f, false));
                Assert.That(driver.VisualCount, Is.EqualTo(1));
                driver.Tick(100f); Assert.That(driver.VisualCount, Is.Zero);
                driver.Observe(shot); driver.Reset(); Assert.That(owner.transform.childCount, Is.Zero);
                driver.Observe(shot); driver.enabled = false;
                Assert.That(driver.VisualCount, Is.Zero);
            }
            finally { driver.Teardown(); Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        }
        [Test] public void HorrorOwnerForwardsLaunchHitTickClearAndReleasesItsProjectileChild()
        {
            var owner = new GameObject("Blinder owner routing fixture"); owner.SetActive(false);
            var manager = owner.AddComponent<HorrorManager>(); var driver = owner.GetComponent<HorrorDriver>();
            var child = new GameObject("Owned test Blinder"); child.transform.SetParent(owner.transform);
            var projectile = child.AddComponent<BlinderProjectileDriver>();
            var atmosphereObject = new GameObject("Uninitialized atmosphere boundary"); atmosphereObject.transform.SetParent(owner.transform);
            var atmosphere = atmosphereObject.AddComponent<HorrorAtmosphereDriver>();
            var config = ScriptableObject.CreateInstance<HorrorDriverConfig>();
            try
            {
                Set(config, "_webShader", Shader.Find("Sprites/Default"));
                projectile.Initialize(config);
                owner.SetActive(true);
                Set(manager, "_driver", driver); Set(driver, "_state", new HorrorDriverState { OwnerEnabled = true });
                Set(driver, "_blinder", projectile); Set(driver, "_atmosphere", atmosphere);
                Set(driver, "_config", config); Set(driver, "_presenter", new HorrorPresenter());
                var shot = new BlinderThrowFact(new EntityId(7), 1, 2, Vector3.zero, Vector3.forward, .06f, 12f, 16f);
                manager.LaunchProjectile(shot); Assert.That(projectile.VisualCount, Is.EqualTo(1));
                manager.TickProjectiles(.1f); Assert.That(child.GetComponentInChildren<MeshRenderer>().transform.position.z, Is.EqualTo(1.2f).Within(.001f));
                manager.HitProjectile(new BlinderHitFact(new EntityId(7), new EntityId(1), 2, 2, 3f, false));
                Assert.That(projectile.VisualCount, Is.Zero);
                manager.LaunchProjectile(shot); manager.ClearProjectiles(); Assert.That(projectile.VisualCount, Is.Zero);
                manager.LaunchProjectile(shot); manager.enabled = false; Assert.That(projectile.VisualCount, Is.Zero);
                manager.Teardown(); Assert.That(child == null, Is.True);
            }
            finally { manager.Teardown(); Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        }
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
