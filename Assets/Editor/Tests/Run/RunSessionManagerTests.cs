// ============================================================================
// RunSessionManagerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies shared random-source lifetime through the real persistent Manager.
//   The fixture enters Play Mode so Unity's persistence and teardown callbacks
//   execute in their supported context rather than being simulated in Edit Mode.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Run.
// KEY RESPONSIBILITIES:
//   - Preserve the Factory's random source across readiness and reset next run.
//   - Clean up its temporary service and leave Play Mode after each test.
// DEPENDENCIES:
//   - Core scene keys, Session Run Manager, NUnit and Unity Test Framework.
// USAGE NOTES:
//   Requires the Unity lease. The framework supplies and restores an isolated
//   test scene; this fixture does not edit or save the user's scene.
// ============================================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Session.Run;

namespace Worsen.Tests.Run
{
    public sealed class RunSessionManagerTests
    {
        [UnityTest]
        public IEnumerator ManagerKeepsFactoryRandomSourceAcrossReadinessAndResetsAtNextAssembly()
        {
            yield return new EnterPlayMode();
            Assert.That(RunSessionManager.Instance, Is.Null, "The test scene must have no active run.");
            var owner = new GameObject("RunSession initialization test");
            try
            {
                var manager = owner.AddComponent<RunSessionManager>().Initialize(73);
                manager.PrepareScene(SceneKey.TagArena);
                var factorySource = manager.RandomSource;
                var expected = new System.Random(73);
                Assert.That(factorySource.Next(), Is.EqualTo(expected.Next()));
                manager.HandleSceneReady(SceneKey.TagArena);
                Assert.That(manager.RandomSource, Is.SameAs(factorySource));
                Assert.That(manager.RandomSource.Next(), Is.EqualTo(expected.Next()));
                manager.SuspendForSceneLoad();
                manager.PrepareScene(SceneKey.TagArena);
                Assert.That(manager.RandomSource, Is.Not.SameAs(factorySource));
                Assert.That(manager.RandomSource.Next(), Is.EqualTo(new System.Random(73).Next()));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
