// ============================================================================
// ProgressionShrineTests.cs
// ============================================================================
// PURPOSE:
//   Exercises shrine transactions through the actual Progression controller boundary.
//   This protects generation/revision guards and the ordinary floor lifecycle.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Progression.
// KEY RESPONSIBILITIES:
//   - Verify wallet debit/payment once, forced shelter and free dismissal.
//   - Verify floor effects clear without leaking permanent slots or consuming layout randomness.
// DEPENDENCIES:
//   - Session Progression, Core, NUnit and temporary config allocation.
// USAGE NOTES:
//   Pure controller fixture; managers and physical shrine placement are tested separately.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Progression;
namespace Worsen.Tests.Progression
{
    public sealed class ProgressionShrineTests
    {
        private ProgressionConfig config;
        private EffectCatalogueConfig catalogue;
        private ProgressionSessionController controller;
        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<ProgressionConfig>();
            catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            Set(catalogue, "_entries", new[] {
                new EffectCatalogueEntry("a", EffectKind.Curse, FearAxis.Stakes, "A", "Removes safety.", floor: 8, value: 3),
                new EffectCatalogueEntry("b", EffectKind.Curse, FearAxis.Time, "B", "Removes time.", floor: 8),
                new EffectCatalogueEntry("c", EffectKind.Curse, FearAxis.Information, "C", "Removes sight.", floor: 8),
                new EffectCatalogueEntry("good", EffectKind.Upgrade, FearAxis.Agency, "Good", "Adds speed.", cap: 3) });
            Set(config, "_effectCatalogue", catalogue);
            Set(config, "_curses", new[] { new ProgressionEntryConfig("legacy", "Legacy", "Removes light.") });
            controller = New(); AdvanceToFloor(controller, 8);
        }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(catalogue); }
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private ProgressionSessionController New()
        {
            var result = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(71));
            result.StartRun(71); return result;
        }
        private static void AdvanceToFloor(ProgressionSessionController value, int floor)
        {
            for (int safety = 0; safety < 200; safety++)
            {
                var snapshot = value.Snapshot();
                if (snapshot.Round == floor && snapshot.Phase == ProgressionPhase.Exploring) return;
                switch (snapshot.Phase)
                {
                    case ProgressionPhase.ChooseThreat: value.ChooseThreat(snapshot.Choices[0].Id, snapshot.Revision); break;
                    case ProgressionPhase.ChooseCurse: value.ChooseCurse(snapshot.Choices[0].Id, snapshot.Revision); break;
                    case ProgressionPhase.Generating: value.ConfirmFloorReady(snapshot.GenerationId); break;
                    case ProgressionPhase.Shop: value.ContinueShop(snapshot.Revision); break;
                    case ProgressionPhase.Exploring: value.CompleteFloor(snapshot.GenerationId); break;
                    default: Assert.Fail("Unexpected fixture phase " + snapshot.Phase); break;
                }
            }
            Assert.Fail("Fixture did not reach floor.");
        }
        private bool Activate(int id, ShrineKind kind, out ShrineResolvedFact result, int generation = -1) => controller.ActivateShrine(
            generation < 0 ? controller.Snapshot().GenerationId : generation,
            new ShrineActivatedFact(id, kind, 2, Vector3.zero, 10), 1000f, 0f, out result);
        [Test]
        public void ProtectionDebitsExactlyOnceWithGenerationGuard()
        {
            int generation = controller.Snapshot().GenerationId;
            for (int i = 0; i < 20; i++) controller.RecordGoldenCollected(generation, i);
            int before = controller.Snapshot().Wallet;
            Assert.That(Activate(1, ShrineKind.Protection, out _, generation - 1), Is.False);
            Assert.That(Activate(1, ShrineKind.Protection, out var result), Is.True);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(before - result.Cost));
            Assert.That(Activate(1, ShrineKind.Protection, out _), Is.False);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(before - result.Cost));
        }
        [Test]
        public void BargainOnlyOpensAfterEscapeAndPaymentCannotRepeatWithFreshOrStaleRevision()
        {
            Assert.That(Activate(1, ShrineKind.Bargain, out _), Is.True);
            Assert.That(controller.Snapshot().Bargain.Pending, Is.False);
            controller.CompleteFloor(controller.Snapshot().GenerationId);
            Assert.That(controller.GenerationRequest().IsShop, Is.True);
            controller.ConfirmFloorReady(controller.Snapshot().GenerationId);
            var snapshot = controller.Snapshot(); Assert.That(snapshot.Bargain.Offers.Count, Is.EqualTo(3));
            var offer = snapshot.Bargain.Offers.Single(o => o.Id == "a");
            Assert.That(offer.Payout, Is.EqualTo(3 * (2 + 8 / 2)));
            Assert.That(controller.TakeBargain(offer.Id, snapshot.Revision - 1), Is.False);
            Assert.That(controller.TakeBargain(offer.Id, snapshot.Revision), Is.True);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(snapshot.Wallet + offer.Payout));
            Assert.That(controller.EffectsSnapshot().ActiveEffects.Stacks(new EffectId("a")), Is.EqualTo(1));
            Assert.That(controller.TakeBargain(offer.Id, snapshot.Revision), Is.False);
            Assert.That(controller.TakeBargain(offer.Id, controller.Snapshot().Revision), Is.False);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(snapshot.Wallet + offer.Payout));
        }
        [Test]
        public void WalkAwayIsFreeAndBargainCanForceAnOffCadenceShelter()
        {
            controller = New(); AdvanceToFloor(controller, 1);
            Assert.That(Activate(1, ShrineKind.Bargain, out _), Is.True);
            controller.CompleteFloor(controller.Snapshot().GenerationId);
            Assert.That(controller.Snapshot().Round, Is.EqualTo(2)); Assert.That(controller.GenerationRequest().IsShop, Is.True);
            controller.ConfirmFloorReady(controller.Snapshot().GenerationId);
            int before = controller.Snapshot().Wallet, curses = controller.Snapshot().CurseCount;
            Assert.That(controller.ContinueShop(controller.Snapshot().Revision), Is.True);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(before)); Assert.That(controller.Snapshot().CurseCount, Is.EqualTo(curses));
            Assert.That(controller.Snapshot().Bargain.Pending, Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void ChanceIsPublishedInCombinedEffectsAndClearsAtFloorEnd(bool death)
        {
            var retained = new ActiveEffects(controller.EffectsSnapshot().ActiveEffects);
            Assert.That(Activate(1, ShrineKind.Chance, out _), Is.True);
            Assert.That(controller.FloorEffects.Count, Is.EqualTo(1));
            var effect = controller.FloorEffects.Single();
            Assert.That(controller.EffectsSnapshot().ActiveEffects.Has(effect.Id), Is.True);
            if (death) controller.EndRun(controller.Snapshot().GenerationId);
            else controller.CompleteFloor(controller.Snapshot().GenerationId);
            Assert.That(controller.FloorEffects.Count, Is.Zero);
            Assert.That(new ActiveEffects(controller.EffectsSnapshot().ActiveEffects), Is.EqualTo(retained));
        }
        [Test]
        public void TemporaryUpgradeDoesNotLeakIntoPermanentInventoryOrRetainedSummary()
        {
            Set(catalogue, "_entries", new[] { new EffectCatalogueEntry("bigger-pockets", EffectKind.Upgrade,
                FearAxis.Agency, "Bigger Pockets", "Adds a slot.") });
            controller = New(); AdvanceToFloor(controller, 8);
            int slots = controller.Snapshot().Inventory.Count;
            Assert.That(Activate(1, ShrineKind.Chance, out _), Is.True);
            Assert.That(controller.EffectsSnapshot().ActiveEffects.Has(new EffectId("bigger-pockets")), Is.True);
            Assert.That(controller.Snapshot().Retained.Any(entry => entry.Id == "bigger-pockets"), Is.False);
            controller.CompleteFloor(controller.Snapshot().GenerationId);
            controller.ConfirmFloorReady(controller.Snapshot().GenerationId);
            Assert.That(controller.Snapshot().Inventory.Count, Is.EqualTo(slots));
        }
        [Test]
        public void ShrineDrawsNeverAdvanceLayoutRandomnessAndRestartClearsHistory()
        {
            var comparison = New(); AdvanceToFloor(comparison, 8);
            Activate(1, ShrineKind.Chance, out _);
            controller.CompleteFloor(controller.Snapshot().GenerationId);
            comparison.CompleteFloor(comparison.Snapshot().GenerationId);
            Assert.That(controller.GenerationRequest().Seed, Is.EqualTo(comparison.GenerationRequest().Seed));
            controller.StartRun(71); Assert.That(controller.ShrineHistory, Is.Empty); Assert.That(controller.FloorEffects.Count, Is.Zero);
        }
    }
}
