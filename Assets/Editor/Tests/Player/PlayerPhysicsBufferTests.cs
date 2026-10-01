// ============================================================================
// PlayerPhysicsBufferTests.cs
// ============================================================================
// PURPOSE:
//   Guard pooled physics queries against exact-capacity truncation and stale hits.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Player.
// KEY RESPONSIBILITIES:
//   - Compare nearest ray/capsule contacts with the previous allocating queries.
//   - Check saturated overlaps, buffer reuse and idempotent teardown/reinitialization.
// DEPENDENCIES:
//   NUnit, Unity physics/TestTools, Player driver/config and read-only reflection.
// USAGE NOTES:
//   Native Edit Mode fixtures for the coordinator; never executed by a Unity CLI here.
//   Geometry is temporary and distant from ordinary scenes; no project settings change.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Domain.Player;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Player
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PlayerPhysicsBufferTests
    {
        private GameObject root, actor;
        private PlayerDriver driver;
        private PlayerDriverState state;
        private PlayerMoverDriverConfig config;
        private readonly Vector3 origin = new Vector3(7000f, 100f, 0f);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("[Test] Query buffer geometry");
            actor = new GameObject("[Test] Query buffer player"); actor.transform.position = origin;
            config = ScriptableObject.CreateInstance<PlayerMoverDriverConfig>();
            driver = actor.AddComponent<PlayerDriver>();
            typeof(PlayerDriver).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(driver, config);
            driver.Initialize();
            state = (PlayerDriverState)typeof(PlayerDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
        }
        [TearDown]
        public void TearDown()
        {
            if (driver != null) driver.Teardown();
            if (actor != null) Object.DestroyImmediate(actor);
            if (root != null) Object.DestroyImmediate(root);
            if (config != null) Object.DestroyImmediate(config);
        }

        [TestCase(false)] [TestCase(true)]
        public void ExactlyFullCastsGrowRetryAndMatchLegacyNearest(bool capsule)
        {
            int capacity = state.QueryHits.Length;
            for (int i = 0; i < capacity; i++) Box(origin + new Vector3(0f, 1f, 2f + i));
            Physics.SyncTransforms();
            var from = origin + Vector3.up;
            float distance = capacity + 4f;
            var presenter = new PlayerMoverPresenter();
            presenter.Capsule(origin, config.Height, config.Radius, out var bottom, out var top);
            var legacy = capsule ? Physics.CapsuleCastAll(bottom, top, Mathf.Max(.001f, config.Radius - config.SkinWidth),
                Vector3.forward, distance, config.CollisionMask, QueryTriggerInteraction.Ignore) :
                Physics.RaycastAll(from, Vector3.forward, distance, config.CollisionMask, QueryTriggerInteraction.Ignore);
            var expected = legacy.Where(hit => !hit.collider.transform.IsChildOf(actor.transform)).OrderBy(hit => hit.distance).First();
            LogAssert.Expect(LogType.Warning, new Regex("^Player physics query buffer saturated; grew from " + capacity + " to [0-9]+ and retrying[.]$"));
            // Ordinary movement casts retain all contacts; wall-only filtering is opt-in.
            object[] args = capsule ? new object[] { origin, config.Height, Vector3.forward, distance, default(RaycastHit), false } :
                new object[] { from, Vector3.forward, distance, default(RaycastHit) };
            var method = typeof(PlayerDriver).GetMethod(capsule ? "Cast" : "Ray", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method.Invoke(driver, args), Is.True);
            Assert.That(state.QueryHits.Length, Is.GreaterThan(capacity));
            var actual = (RaycastHit)args[capsule ? 4 : 3];
            Assert.That(actual.collider, Is.SameAs(expected.collider));
            Assert.That(actual.distance, Is.EqualTo(expected.distance).Within(.0001f));
            var buffer = state.QueryHits;
            Assert.That(method.Invoke(driver, args), Is.True); Assert.That(state.QueryHits, Is.SameAs(buffer));
            root.SetActive(false); Physics.SyncTransforms();
            Assert.That(method.Invoke(driver, args), Is.False, "Stale pooled entries must not become contacts.");
            LogAssert.NoUnexpectedReceived();
            CheckLifecycle();
        }

        [Test]
        public void FullOverlapsGrowAndReturnEveryColliderWithoutStaleEntries()
        {
            int capacity = state.QueryOverlaps.Length;
            var bottom = origin + Vector3.right * 10f;
            for (int i = 0; i < capacity; i++) Box(bottom + Vector3.up * .5f);
            Physics.SyncTransforms();
            var top = bottom + Vector3.up;
            var expected = Physics.OverlapCapsule(bottom, top, Mathf.Max(.001f, config.Radius - config.SkinWidth),
                config.CollisionMask, QueryTriggerInteraction.Ignore);
            Assert.That(expected.Length, Is.EqualTo(capacity));
            LogAssert.Expect(LogType.Warning, new Regex("^Player physics query buffer saturated; grew from " + capacity + " to [0-9]+ and retrying[.]$"));
            var method = typeof(PlayerDriver).GetMethod("OverlapCapsule", BindingFlags.Instance | BindingFlags.NonPublic);
            var args = new object[] { bottom, top };
            int count = (int)method.Invoke(driver, args);
            Assert.That(state.QueryOverlaps.Take(count), Is.EquivalentTo(expected));
            var buffer = state.QueryOverlaps;
            Assert.That(method.Invoke(driver, args), Is.EqualTo(count)); Assert.That(state.QueryOverlaps, Is.SameAs(buffer));
            root.SetActive(false); Physics.SyncTransforms();
            Assert.That(method.Invoke(driver, args), Is.EqualTo(0));
            LogAssert.NoUnexpectedReceived();
            CheckLifecycle();
        }
        private void CheckLifecycle()
        {
            driver.Teardown(); driver.Teardown();
            Assert.That(state.QueryHits, Is.Null); Assert.That(state.QueryOverlaps, Is.Null);
            driver.Initialize();
            Assert.That(state.QueryHits, Is.Not.Null); Assert.That(state.QueryOverlaps, Is.Not.Null);
        }
        private void Box(Vector3 position)
        {
            var box = new GameObject("[Test] Query obstacle"); box.transform.SetParent(root.transform);
            box.transform.position = position; box.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, .2f);
        }
    }
}
