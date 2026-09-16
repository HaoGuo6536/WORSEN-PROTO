// ============================================================================
// ProgressionConfiguredThreatTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the serialized progression catalog that scene setup loads at runtime.
//   Fresh Config defaults cannot catch stale asset descriptions, so this test
//   checks the actual five-hunter roster, curse families, stock and shop cadence.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Progression.
// KEY RESPONSIBILITIES:
//   - Check the shipped roster, all curse families and the first reachable shop.
//   - Fail when setup leaves the obsolete placeholder catalog serialized.
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
        public void RuntimeCatalogHasFiveHuntersTwentyTwoCursesAndEarlyShop()
        {
            var config = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(
                "Assets/Resources/ScriptableObjects/Session/Progression/ProgressionConfig.asset");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.MaximumActiveThreats, Is.EqualTo(5));
            Assert.That(config.ShopInterval, Is.EqualTo(2));
            Assert.That(config.Threats.Count, Is.EqualTo(5));
            Assert.That(config.Curses.Count, Is.EqualTo(22));
            Assert.That(config.Offers.Count, Is.EqualTo(6));
            int general = 0;
            foreach (var curse in config.Curses)
            {
                Assert.That(curse.Traits, Is.Not.EqualTo(ProgressionTraits.None), curse.Id);
                if (string.IsNullOrEmpty(curse.RequiredThreatId)) general++;
            }
            Assert.That(general, Is.EqualTo(7));
            foreach (var threat in config.Threats)
            {
                int count = 0;
                foreach (var curse in config.Curses) if (curse.RequiredThreatId == threat.Id) count++;
                Assert.That(count, Is.EqualTo(3), threat.Id);
            }
            var controller = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(731));
            controller.StartRun(731);
            for (int combat = 0; combat < 2; combat++)
            {
                Assert.That(controller.ChooseThreat(controller.Snapshot().Choices[0].Id, controller.Snapshot().Revision), Is.True);
                Assert.That(controller.ChooseCurse(controller.Snapshot().Choices[0].Id, controller.Snapshot().Revision), Is.True);
                int generation = controller.GenerationRequest().GenerationId;
                Assert.That(controller.ConfirmFloorReady(generation), Is.True);
                Assert.That(controller.CompleteFloor(generation), Is.True);
            }
            Assert.That(controller.GenerationRequest().IsShop, Is.True);
            Assert.That(controller.Snapshot().Round, Is.EqualTo(3));
            Assert.That(controller.ConfirmFloorReady(controller.GenerationRequest().GenerationId), Is.True);
            Assert.That(controller.Snapshot().CanContinue, Is.True);
            foreach (var offer in controller.Snapshot().Offers) Assert.That(offer.StockRemaining, Is.EqualTo(1));
            Assert.That(controller.ContinueShop(controller.Snapshot().Revision), Is.True);
            Assert.That(controller.Snapshot().Round, Is.EqualTo(4));
        }
    }
}
