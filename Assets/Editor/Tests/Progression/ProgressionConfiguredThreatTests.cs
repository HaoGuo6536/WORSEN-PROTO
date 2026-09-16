// ============================================================================
// ProgressionConfiguredThreatTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the serialized progression catalog that scene setup loads at runtime.
//   Fresh Config defaults cannot catch stale asset descriptions, so this test
//   advances the actual asset through the first shop before checking threat copy.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Progression.
// KEY RESPONSIBILITIES:
//   - Check the shipped three-hunter cap and truthful descriptions at floor five.
//   - Confirm the capped Watcher remains a selectable neutral placeholder.
// DEPENDENCIES:
//   - UnityEditor asset loading, NUnit, Core contracts and Session Progression.
// USAGE NOTES:
//   Read-only asset access; runtime state is local and no scene is assembled.
// ============================================================================
using NUnit.Framework;
using UnityEditor;
using Worsen.Core;
using Worsen.Session.Progression;

namespace Worsen.Tests.Progression
{
    public sealed class ProgressionConfiguredThreatTests
    {
        [Test]
        public void RuntimeCatalogHasTruthfulCappedChoicesAfterFirstShop()
        {
            var config = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(
                "Assets/Resources/ScriptableObjects/Session/Progression/ProgressionConfig.asset");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.MaximumActiveThreats, Is.EqualTo(3));
            var controller = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(731));
            controller.StartRun(731);
            for (int round = 1; round <= 3; round++)
            {
                Assert.That(controller.ChooseThreat("watcher", controller.Snapshot().Revision), Is.True);
                Assert.That(controller.ChooseCurse("restless", controller.Snapshot().Revision), Is.True);
                int generation = controller.GenerationRequest().GenerationId;
                Assert.That(controller.ConfirmFloorReady(generation), Is.True);
                Assert.That(controller.CompleteFloor(generation), Is.True);
            }
            Assert.That(controller.ConfirmFloorReady(controller.GenerationRequest().GenerationId), Is.True);
            Assert.That(controller.ContinueShop(controller.Snapshot().Revision), Is.True);
            ProgressionSnapshot before = controller.Snapshot();
            Assert.That(before.Round, Is.EqualTo(5));
            Assert.That(before.Choices.Count, Is.EqualTo(3));
            Assert.That(before.Choices[0].Id, Is.EqualTo("watcher"));
            Assert.That(before.Choices[0].Description, Does.Contain("no additional hunter or other effect"));
            Assert.That(before.Choices[1].Description, Does.StartWith("No additional hunter.").And.Contain("8% faster"));
            Assert.That(before.Choices[2].Description, Does.StartWith("No additional hunter.").And.Contain("4% slower").And.Contain("12%"));
            Assert.That(controller.ChooseThreat("watcher", before.Revision), Is.True);
            ProgressionSnapshot after = controller.Snapshot();
            Assert.That(after.Effects, Is.EqualTo(before.Effects));
            Assert.That(after.ThreatCount, Is.EqualTo(4));
            Assert.That(after.Effects.ActiveThreatBudget, Is.EqualTo(3));
        }
    }
}
