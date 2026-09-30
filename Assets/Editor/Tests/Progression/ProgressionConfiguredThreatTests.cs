// ============================================================================
// ProgressionConfiguredThreatTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the serialized progression catalog that scene setup loads at runtime.
//   Fresh Config defaults cannot catch stale asset descriptions, so this test
//   checks the approved ten-hunter roster, retired flags, stock and shop cadence.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Progression.
// KEY RESPONSIBILITIES:
//   - Check the shipped roster, all curse families and the first reachable shop.
//   - Require a pure combat floor between the opening selection and first shop.
//   - Check hunter content independently of ignored legacy shop serialization.
// DEPENDENCIES:
//   - UnityEditor asset loading, NUnit, Core contracts and Session Progression.
// USAGE NOTES:
//   Read-only asset access; runtime state is local and no scene is assembled.
// ============================================================================
using NUnit.Framework;
using System.Linq;
using UnityEditor;
using Worsen.Core;
using Worsen.Session.Progression;

namespace Worsen.Tests.Progression
{
    public sealed class ProgressionConfiguredThreatTests
    {
        [Test]
        public void RuntimeCatalogHasApprovedRosterAndEarlyShop()
        {
            var config = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(
                "Assets/Resources/ScriptableObjects/Session/Progression/ProgressionConfig.asset");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.ShopInterval, Is.EqualTo(2));
            Assert.That(config.Threats.Select(entry => entry.Id), Is.EquivalentTo(new[] {
                "echo", "weaver", "ticking", "ram", "mannequin", "mimic", "blinder", "skip", "herald", "stare" }));
            Assert.That(config.Curses.All(entry => !ProgressionRosterUtility.Retired(entry.Id) && entry.Traits == ProgressionTraits.None), Is.True);
            Assert.That(config.EffectCatalogue, Is.Not.Null, "Run Ensure Effect Catalogue before integration tests.");
            var controller = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(731));
            controller.StartRun(731);
            Assert.That(controller.Snapshot().Choices.Select(choice => choice.Id), Is.EquivalentTo(new[] { "echo", "weaver", "ticking" }));
            for (int combat = 0; combat < 2; combat++)
            {
                if (combat == 0)
                {
                    Assert.That(controller.ChooseThreat(controller.Snapshot().Choices[0].Id, controller.Snapshot().Revision), Is.True);
                    Assert.That(controller.ChooseCurse(controller.Snapshot().Choices[0].Id, controller.Snapshot().Revision), Is.True);
                }
                else Assert.That(controller.Snapshot().Phase, Is.EqualTo(ProgressionPhase.Generating));
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
