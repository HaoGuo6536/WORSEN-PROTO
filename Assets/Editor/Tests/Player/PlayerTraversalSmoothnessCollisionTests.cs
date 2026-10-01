// ============================================================================
// PlayerTraversalSmoothnessCollisionTests.cs
// ============================================================================
// PURPOSE:
//   Tests traversal collision isolation against actual temporary Unity colliders.
//   The admitted waist obstacle determines the arc phases, while a second
//   collider must still stop it and must never produce a delayed catch-up jump.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Player integration.
// KEY RESPONSIBILITIES:
//   - Compare clear traversal, independent blockers and blocked landing admission.
//   - Require collider isolation to end on ordinary movement, teleport and teardown.
//   - Verify admitted geometry is latched per vault and missing geometry uses fallbacks.
// DEPENDENCIES:
//   PlayerDriver, LevelMarker, Core contracts, NUnit, UnityEditor and Unity physics.
// USAGE NOTES:
//   Unity-bound Edit Mode tests, not headless coverage. Temporary geometry mirrors
//   PlayerDriverTests; no scene assets, collision matrices or config assets are changed.
// ============================================================================
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Domain.Player;

namespace Worsen.Tests.Player
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PlayerTraversalSmoothnessCollisionTests
    {
        private const float Dt = 1f / 60f;
        private GameObject root, actor;
        private PlayerDriver driver;
        private PlayerMoverDriverConfig config;
        private BoxCollider vault;
        private Vector3 start, target;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("[Test] Vault isolation"); root.transform.position = new Vector3(2000f, 0f, 0f);
            config = ScriptableObject.CreateInstance<PlayerMoverDriverConfig>();
            Box("Floor", new Vector3(3f, -.25f, 0f), new Vector3(12f, .5f, 4f));
            vault = Box("Admitted obstacle", new Vector3(2.5f, .5f, 0f), new Vector3(1f, 1f, 2f));
            target = root.transform.position + Vector3.right * 3.6f;
            var marker = vault.gameObject.AddComponent<LevelMarker>();
            using (var fields = new SerializedObject(marker))
            {
                fields.FindProperty("_id").intValue = 92503;
                fields.FindProperty("_kind").enumValueIndex = (int)LevelMarkerKind.VaultSurface;
                fields.FindProperty("_targetPosition").vector3Value = target;
                fields.ApplyModifiedPropertiesWithoutUndo();
            }
            actor = new GameObject("[Test] Player");
            start = root.transform.position + Vector3.right * 1.4f;
            actor.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, 90f, 0f));
            driver = actor.AddComponent<PlayerDriver>();
            using (var fields = new SerializedObject(driver))
            { fields.FindProperty("_config").objectReferenceValue = config; fields.ApplyModifiedPropertiesWithoutUndo(); }
            driver.Initialize(); Physics.SyncTransforms();
            var probe = driver.Probe();
            Assert.That(probe.VaultCandidate && probe.VaultClearance > 0f, Is.True);
            Assert.That(probe.VaultTarget, Is.EqualTo(target));
        }

        [Test]
        public void ClearArcFollowsEveryCommandWithoutClampingOrObstacleDepenetration()
        {
            var presenter = new PlayerMoverPresenter();
            presenter.TraversalPhases(start, target, vault.bounds, config.Radius, config.SkinWidth,
                config.TraversalRisePortion, config.TraversalTraverseEnd, out float rise, out float end);
            for (int tick = 1; tick <= 15; tick++)
            {
                float progress = tick / 15f;
                var actual = Step(progress);
                Vector3 expected = presenter.TraversalPosition(start, target, progress, 1f,
                    config.TraversalLift, rise, end);
                Assert.That(Vector3.Distance(actual.Position, expected), Is.LessThan(.002f), "tick=" + tick);
            }
            AssertCleared();
            Assert.That(Vector3.Distance(driver.Position, target), Is.LessThan(.002f));
            Assert.That(Physics.GetIgnoreCollision(actor.GetComponent<CapsuleCollider>(), vault), Is.False);
        }

        [Test]
        public void ArcPhasesAreCapturedOnceAndRecomputedAfterTeleport()
        {
            Step(.1f);
            float rise = State.TraversalRisePortion, end = State.TraversalTraverseEnd;
            Assert.That(rise, Is.Not.EqualTo(config.TraversalRisePortion));
            Assert.That(end, Is.LessThan(config.TraversalTraverseEnd));
            vault.size = new Vector3(.2f, 1f, 2f); Physics.SyncTransforms();
            Step(.2f);
            Assert.That(State.TraversalRisePortion, Is.EqualTo(rise));
            Assert.That(State.TraversalTraverseEnd, Is.EqualTo(end));
            driver.Teleport(start, 90f); Physics.SyncTransforms();
            Assert.That(State.TraversalRisePortion, Is.Zero);
            Assert.That(State.TraversalTraverseEnd, Is.Zero);
            Assert.That(driver.Probe().VaultCandidate, Is.True);
            Step(.1f);
            Assert.That(State.TraversalRisePortion, Is.GreaterThan(rise));
            Assert.That(State.TraversalTraverseEnd, Is.LessThan(end));
        }

        [Test]
        public void TraversalWithoutAnAdmittedColliderUsesFallbackPhases()
        {
            driver.Teleport(start, 90f); // Clears the admitted probe; do not probe again.
            Object.DestroyImmediate(vault.gameObject); Physics.SyncTransforms();
            Vector3 actual = Step(.1f).Position;
            Assert.That(State.TraversalCollider, Is.Null);
            Assert.That(State.TraversalRisePortion, Is.EqualTo(config.TraversalRisePortion));
            Assert.That(State.TraversalTraverseEnd, Is.EqualTo(config.TraversalTraverseEnd));
            Vector3 expected = new PlayerMoverPresenter().TraversalPosition(start, target, .1f, 1f,
                config.TraversalLift, config.TraversalRisePortion, config.TraversalTraverseEnd);
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.002f));
        }

        [Test]
        public void IndependentColliderUnderTheSameMarkerStillBlocksAndCannotCatchUpAfterRemoval()
        {
            // Same parent deliberately catches accidental hierarchy-wide filtering.
            var blocker = Box("Other collider", new Vector3(2.3f, 2.1f, 0f), new Vector3(1f, .2f, 2f));
            blocker.transform.SetParent(vault.transform, true);
            Physics.SyncTransforms();
            bool stopped = false;
            Vector3 stoppedAt = Vector3.zero;
            for (int tick = 1; tick <= 14; tick++)
            {
                var moved = Step(tick / 15f);
                if (!stopped && State.TraversalObstructed)
                {
                    stopped = true; stoppedAt = moved.Position;
                    Assert.That(moved.Ceiling, Is.True);
                    Object.DestroyImmediate(blocker.gameObject); Physics.SyncTransforms();
                }
                else if (stopped)
                    Assert.That(Vector3.Distance(moved.Position, stoppedAt), Is.LessThan(.002f));
            }
            Assert.That(stopped, Is.True, "Other geometry must not be excluded with the vault collider.");
            Assert.That(Vector3.Distance(Step(1f).Position, target), Is.GreaterThan(.05f));
            AssertCleared();
        }

        [Test]
        public void ABlockedFutureLandingDoesNotFreezeEarlyArcButItsActualSweepStillBlocks()
        {
            var blocker = Box("Occupied landing", new Vector3(3.6f, 1.5f, 0f), new Vector3(.2f, 3f, 2f));
            Physics.SyncTransforms();
            // Start was admitted before the blocker appeared.
            Assert.That(Step(1f / 15f).Position.x, Is.GreaterThan(start.x));
            for (int tick = 2; tick <= 15; tick++) Step(tick / 15f);
            Assert.That(Vector3.Distance(driver.Position, target), Is.GreaterThan(.05f));
            Assert.That(driver.Position.x, Is.LessThan(blocker.bounds.min.x));
            driver.Teleport(start, 90f); Physics.SyncTransforms();
            Assert.That(driver.Probe().VaultClearance, Is.Zero, "Blocked landing admission is unchanged.");
        }

        [TestCase("cancel")]
        [TestCase("teleport")]
        [TestCase("disable")]
        [TestCase("teardown")]
        public void EndPathsClearTraversalIdentityAndQueryExclusion(string end)
        {
            Step(.4f);
            Assert.That(State.TraversalActive, Is.True);
            Assert.That(State.TraversalCollider, Is.SameAs(vault));
            Assert.That(State.IgnoredTraversalCollider, Is.Null, "Exclusion cannot escape a MoveTraversal call.");
            switch (end)
            {
                case "cancel": driver.Move(Vector3.zero, Vector3.zero, false, 90f, Dt); break;
                case "teleport": driver.Teleport(start, 90f); break;
                case "disable":
                    driver.enabled = false;
                    // Runtime-only callbacks are not dispatched by Edit Mode enable changes.
                    typeof(PlayerDriver).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(driver, null);
                    break;
                case "teardown": driver.Teardown(); break;
            }
            AssertCleared();
            Assert.That(Physics.GetIgnoreCollision(actor.GetComponent<CapsuleCollider>(), vault), Is.False);
        }

        private PlayerMoveResult Step(float progress)
        {
            // Deliberately tiny obsolete maximumSpeed proves there is no per-step clamp.
            var result = driver.MoveTraversal(start, target, progress, 1f, Vector3.zero, 90f, Dt, .1f);
            Physics.SyncTransforms();
            return result;
        }
        private PlayerDriverState State => (PlayerDriverState)typeof(PlayerDriver)
            .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
        private void AssertCleared()
        {
            Assert.That(State.TraversalActive || State.TraversalObstructed, Is.False);
            Assert.That(State.TraversalCollider, Is.Null);
            Assert.That(State.IgnoredTraversalCollider, Is.Null);
        }
        private BoxCollider Box(string name, Vector3 position, Vector3 size)
        {
            var item = new GameObject("[Test] " + name); item.transform.SetParent(root.transform, false);
            item.transform.localPosition = position;
            var box = item.AddComponent<BoxCollider>(); box.size = size; return box;
        }
        [TearDown]
        public void Teardown()
        {
            if (driver != null) driver.Teardown();
            if (actor != null) Object.DestroyImmediate(actor);
            if (root != null) Object.DestroyImmediate(root);
            if (config != null) Object.DestroyImmediate(config);
        }
    }
}
