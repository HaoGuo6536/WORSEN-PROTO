// ============================================================================
// ShrineYieldCreditTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Purgatory credit on the real progression economy path.
//   Fractional yield must survive later pickups and floor replacement without double credit.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Progression.
// KEY RESPONSIBILITIES:
//   - Check carried rounding, overflow rollback, duplicate guards and new-run reset.
// DEPENDENCIES:
//   - Progression/Shop controllers, Core, NUnit and test-only config creation.
// USAGE NOTES:
//   Pure logic with explicit seed; no Managers or world construction.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Progression;
using Worsen.Session.Progression.Shop;
namespace Worsen.Tests.Progression
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ShrineYieldCreditTests
    {
        [Test]
        public void MultipliesBeforeRoundingAndFailedCreditDoesNotSpendRemainder()
        {
            var shop = new ShopController(new ShopBehaviorState(), new ShopRules(), null, new System.Random(3));
            var empty = new ActiveEffects(Array.Empty<ActiveEffect>());
            Assert.That(shop.GoldenCredit(0, 1, empty, out int credit, 1.5f), Is.True); Assert.That(credit, Is.EqualTo(1));
            Assert.That(shop.GoldenCredit(int.MaxValue, 1, empty, out _, 1.5f), Is.False);
            Assert.That(shop.GoldenCredit(0, 1, empty, out _, float.MaxValue), Is.False);
            Assert.That(shop.GoldenCredit(1, 1, empty, out credit, 1.5f), Is.True); Assert.That(credit, Is.EqualTo(2));
            Assert.That(shop.GoldenCredit(3, 1, empty, out credit), Is.True); Assert.That(credit, Is.EqualTo(1));
        }
        [Test]
        public void PurgatoryAffectsOnlyLaterUniqueGoldAndResetClearsFraction()
        {
            var config = ScriptableObject.CreateInstance<ProgressionConfig>();
            try
            {
                var controller = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(71));
                controller.StartRun(71); Explore(controller);
                int generation = controller.Snapshot().GenerationId;
                Assert.That(controller.RecordGoldenCollected(generation, 1), Is.True);
                int baseline = controller.Snapshot().Wallet;
                Assert.That(controller.ActivateShrine(generation, new ShrineActivatedFact(7, ShrineKind.Purgatory, 2, Vector3.zero, 1),
                    0f, .5f, out var result), Is.True);
                Assert.That(result.ExtraHunters, Is.EqualTo(1)); Assert.That(result.YieldMultiplier, Is.EqualTo(1.5f));
                Assert.That(controller.RecordGoldenCollected(generation, 2), Is.True);
                Assert.That(controller.RecordGoldenCollected(generation, 2), Is.False);
                Assert.That(controller.RecordGoldenCollected(generation, 3), Is.True);
                Assert.That(controller.Snapshot().Wallet - baseline, Is.EqualTo(config.GoldenCakeValue * 3));
                Assert.That(controller.CompleteFloor(generation), Is.True); Explore(controller);
                Assert.That(controller.ShrineYieldMultiplier, Is.EqualTo(1f));
                controller.StartRun(71); Explore(controller);
                Assert.That(controller.RecordGoldenCollected(controller.Snapshot().GenerationId, 1), Is.True);
                Assert.That(controller.Snapshot().Wallet, Is.EqualTo(config.GoldenCakeValue));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        private static void Explore(ProgressionSessionController controller)
        {
            for (int i = 0; i < 30; i++)
            {
                var s = controller.Snapshot();
                switch (s.Phase)
                {
                    case ProgressionPhase.Exploring: return;
                    case ProgressionPhase.Generating: controller.ConfirmFloorReady(s.GenerationId); break;
                    case ProgressionPhase.ChooseThreat: controller.ChooseThreat(s.Choices[0].Id, s.Revision); break;
                    case ProgressionPhase.ChooseCurse: controller.ChooseCurse(s.Choices[0].Id, s.Revision); break;
                    case ProgressionPhase.Shop: controller.ContinueShop(s.Revision); break;
                    default: Assert.Fail("Unexpected phase " + s.Phase); break;
                }
            }
            Assert.Fail("Exploration not reached.");
        }
    }
}
