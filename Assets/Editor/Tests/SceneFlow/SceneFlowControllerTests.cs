// ============================================================================
// SceneFlowControllerTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies scene identity and path mapping without loading scenes. Invalid
//   keys and unrelated engine callbacks must never be mistaken for an assembled
//   gameplay scene, so both lookup directions are tested explicitly.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · SceneFlow.
//
// KEY RESPONSIBILITIES:
//   - Check canonical scene-key round trips.
//   - Reject unknown requests and ignore unrelated loaded scene paths.
//
// DEPENDENCIES:
//   - NUnit, the pure SceneFlowController, and Core SceneKey values.
//
// USAGE NOTES:
//   Editor-only tests. Build availability and real scene hand-offs are verified
//   separately in the connected Unity Editor after the setup tool creates assets.
//
// ============================================================================

using System;
using NUnit.Framework;
using Worsen.Core;
using Worsen.Session.SceneFlow;

namespace Worsen.Tests.SceneFlow
{
    public sealed class SceneFlowControllerTests
    {
        [TestCase(SceneKey.TagArena, "Assets/Scenes/TagArena.unity")]
        [TestCase(SceneKey.FloorLoop, "Assets/Scenes/FloorLoop.unity")]
        public void SceneKeysResolveToCanonicalPathsAndBack(SceneKey key, string expected)
        {
            var controller = new SceneFlowController();
            Assert.That(controller.GetScenePath(key), Is.EqualTo(expected));
            Assert.That(controller.TryGetSceneKey(expected, out SceneKey restored), Is.True);
            Assert.That(restored, Is.EqualTo(key));
        }

        [TestCase(SceneKey.None)]
        [TestCase((SceneKey)999)]
        public void UnmappedLoadRequestsFailVisibly(SceneKey key)
        {
            var controller = new SceneFlowController();
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.GetScenePath(key));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Assets/Scenes/Scene1.unity")]
        [TestCase("TagArena")]
        public void UnrelatedPathsDoNotAnnounceGameplaySceneCompletion(string path)
        {
            var controller = new SceneFlowController();
            Assert.That(controller.TryGetSceneKey(path, out SceneKey key), Is.False);
            Assert.That(key, Is.EqualTo(SceneKey.None));
        }
    }
}
