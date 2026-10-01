// ============================================================================
// ProceduralGenerationControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies retry provenance and fail-closed fallback without invoking Unity's
//   geometry builder. Failure reasons are supplied as real controller inputs.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check seed stepping, attempt bounds, terminal guards and retained evidence.
//   - Exercise the Manager's invalid-configuration retry path and teardown journal.
// DEPENDENCIES:
//   - Domain.Procedural and NUnit.
//   - Temporary Unity objects/serialization for the Manager wiring test.
// USAGE NOTES:
//   Physical failure injection is represented by a failed validation result; no
//   test claims to have baked navigation or run the engine assembly path.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralGenerationControllerTests
    {
        [Test]
        public void ManagerExhaustionCannotBecomeReadyAndJournalSurvivesTeardown()
        {
            var owner = new GameObject("Generation retry fixture");
            var config = ScriptableObject.CreateInstance<ProceduralConfig>();
            var driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            try
            {
                var settings = new SerializedObject(config);
                settings.FindProperty("_initialRoomCount").intValue = 3;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var manager = owner.AddComponent<ProceduralManager>();
                var failure = Assert.Throws<InvalidOperationException>(() => manager.Initialize(config, driver, 7, 1));
                Assert.That(failure.Message, Does.Contain("NoFloorAwaitingSession"));
                Assert.That(manager.UsedFallback, Is.True); Assert.That(manager.GenerationSucceeded, Is.False);
                Assert.That(manager.IsReady, Is.False); Assert.That(manager.Graph, Is.Null);
                Assert.That(manager.LayoutManifest, Does.Contain("attempt=3,"));
                string journal = manager.LayoutManifest;
                manager.Teardown(); Assert.That(manager.LayoutManifest, Is.EqualTo(journal));
            }
            finally
            { UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(driver); }
        }

        [TestCase(7)] [TestCase(int.MaxValue)] [TestCase(int.MinValue)]
        public void ThreeRetriesIncrementSeedsRecordReasonsAndEndInFallback(int seed)
        {
            var state = new ProceduralBehaviorState(); var controller = new ProceduralGenerationController(state);
            controller.Begin(seed, 2, 3);
            for (int attempt = 0; attempt <= 3; attempt++)
            {
                int expected = unchecked(seed + attempt * ProceduralGenerationController.SeedStride);
                Assert.That(state.AttemptSeed, Is.EqualTo(expected));
                Assert.That(controller.Fail("blocked-" + attempt, "layout-" + attempt), Is.EqualTo(attempt < 3));
                Assert.That(state.GenerationManifest, Does.Contain("attempt=" + attempt + ",seed=" + expected + ",result=failed:blocked-" + attempt));
                Assert.That(state.GenerationManifest, Does.Contain("layout=layout-" + attempt));
            }
            Assert.That(state.UsedFallback, Is.True); Assert.That(state.GenerationSucceeded, Is.False);
            Assert.That(state.IsReady, Is.False); Assert.That(state.Layout, Is.Null);
            Assert.That(state.GenerationManifest, Does.Contain("fallback=NoFloorAwaitingSession|generationSucceeded=false"));
            Assert.Throws<InvalidOperationException>(() => controller.Succeed("must-not-admit-fallback"));
            Assert.Throws<InvalidOperationException>(() => controller.Fail("extra", null));
        }

        [Test]
        public void LaterSuccessPreservesFailuresAndBeginClearsPreviousRun()
        {
            var state = new ProceduralBehaviorState(); var controller = new ProceduralGenerationController(state);
            controller.Begin(1, 1, 3);
            controller.Fail("NavMesh: blocked|reason", "bad-layout");
            controller.Succeed("valid-layout");
            Assert.That(state.GenerationSucceeded, Is.True); Assert.That(state.UsedFallback, Is.False);
            Assert.That(state.GenerationManifest, Does.Contain(Uri.EscapeDataString("NavMesh: blocked|reason")));
            Assert.That(state.GenerationManifest, Does.Contain("result=validated,layout=valid-layout"));
            Assert.That(state.IsReady, Is.False, "Only the generation manager can admit physical geometry.");
            controller.Begin(2, 3, 0);
            Assert.That(controller.Fail("invalid", null), Is.False);
            Assert.That(state.GenerationManifest, Does.Not.Contain("bad-layout"));
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.Begin(2, 3, 9));
        }
    }
}
