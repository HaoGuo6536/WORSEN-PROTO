// ============================================================================
// ConsumableInventoryTests.cs
// ============================================================================
// PURPOSE:
//   Exercises real Progression purchases, physical selection and item consumption.
//   Frozen snapshots and revision guards keep repeated input from spending twice.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Cover all item use counts, ward arming, selection wrap and once-per-run revival.
// DEPENDENCIES:
//   - Core, Progression/Shop pure controllers, NUnit and temporary config allocation.
// USAGE NOTES:
//   Edit Mode; reflection authors test-only catalogue fields, never project assets.
// ============================================================================
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Progression;
using Worsen.Session.Progression.Shop;

namespace Worsen.Tests.Progression
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ConsumableInventoryTests
    {
        private ProgressionConfig config;
        private EffectCatalogueConfig catalogue;
        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<ProgressionConfig>();
            catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            Set(config, "_effectCatalogue", catalogue);
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(catalogue); }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Open(ProgressionSessionController session)
        {
            if (session.Snapshot().Phase == ProgressionPhase.ChooseThreat) session.ChooseThreat(session.Snapshot().Choices[0].Id, session.Snapshot().Revision);
            if (session.Snapshot().Phase == ProgressionPhase.ChooseCurse) session.ChooseCurse(session.Snapshot().Choices[0].Id, session.Snapshot().Revision);
            Assert.That(session.ConfirmFloorReady(session.Snapshot().GenerationId), Is.True);
        }
        private ProgressionSessionController Stock(string id, EffectKind kind = EffectKind.Consumable)
        {
            Set(catalogue, "_entries", new[] { new EffectCatalogueEntry(id, kind, FearAxis.Agency, id, "Test purchase.", price: 0) });
            var session = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(73));
            session.StartRun(73);
            for (int i = 0; i < 2; i++) { Open(session); session.CompleteFloor(session.Snapshot().GenerationId); }
            session.ConfirmFloorReady(session.Snapshot().GenerationId);
            Assert.That(session.Purchase(id, session.Snapshot().Revision), Is.True);
            session.ContinueShop(session.Snapshot().Revision); Open(session); return session;
        }
        [TestCase("firecracker", 2)] [TestCase("doorstop", 2)] [TestCase("gauze", 1)]
        [TestCase("smelling-salts", 1)] [TestCase("wax-ward", 1)] [TestCase("oil-flask", 1)]
        [TestCase("glass-vial", 1)] [TestCase("adrenaline", 1)]
        public void EveryConsumableSpendsUsesThenReleasesItsSlotAndRejectsStaleInput(string id, int uses)
        {
            var session = Stock(id); var frozen = session.Consumables();
            Assert.That(frozen.Inventory.Count, Is.EqualTo(3)); Assert.That(frozen.RemainingUses[0], Is.EqualTo(uses));
            int generation = session.Snapshot().GenerationId;
            Assert.That(session.TryConsumeSelected(generation - 1, session.Snapshot().Revision, id), Is.False);
            for (int i = uses; i > 0; i--)
            {
                int revision = session.Snapshot().Revision;
                Assert.That(session.TryConsumeSelected(generation, revision, "wrong-item"), Is.False);
                Assert.That(session.TryConsumeSelected(generation, revision, id), Is.True);
                Assert.That(session.TryConsumeSelected(generation, revision, id), Is.False);
                Assert.That(session.Consumables().RemainingUses[0], Is.EqualTo(i - 1));
            }
            Assert.That(string.IsNullOrEmpty(session.Snapshot().Inventory[0].Id), Is.True);
            Assert.That(session.EffectsSnapshot().ActiveEffects.Has(new EffectId(id)), Is.False);
            Assert.That(frozen.Inventory[0].Id, Is.EqualTo(id)); Assert.That(frozen.RemainingUses[0], Is.EqualTo(uses));
        }
        [Test]
        public void WardIsInactiveInSlotAndArmsExactlyOneAutomaticGrabBreak()
        {
            var session = Stock("wax-ward"); int generation = session.Snapshot().GenerationId;
            Assert.That(session.TryConsumeWaxWard(generation), Is.False);
            Assert.That(session.TryConsumeSelected(generation, session.Snapshot().Revision, "wax-ward"), Is.True);
            Assert.That(session.Snapshot().Effects.WaxWardCharges, Is.EqualTo(1));
            Assert.That(session.TryConsumeWaxWard(generation), Is.True); Assert.That(session.TryConsumeWaxWard(generation), Is.False);
        }
        [Test]
        public void SelectionWrapsInBothDirectionsIncludingBiggerPocketsAndEmptySlots()
        {
            var session = Stock("bigger-pockets", EffectKind.Upgrade); int generation = session.Snapshot().GenerationId;
            Assert.That(session.Consumables().Inventory.Count, Is.EqualTo(4));
            Assert.That(session.CycleConsumable(generation, -1), Is.True); Assert.That(session.Consumables().SelectedIndex, Is.EqualTo(3));
            Assert.That(session.CycleConsumable(generation, 1), Is.True); Assert.That(session.Consumables().SelectedIndex, Is.Zero);
            Assert.That(session.CycleConsumable(generation - 1, 1), Is.False);
            Assert.That(session.CycleConsumable(generation, 0), Is.False);
        }
        [Test]
        public void ExtraLifeCanOnlyBeSpentOnceAcrossFloorsAndNeverWithoutTheActiveEffect()
        {
            var session = Stock("extra-life", EffectKind.Upgrade);
            var frozen = session.EffectsSnapshot().ActiveEffects;
            Assert.That(session.TryConsumeExtraLife(session.Snapshot().GenerationId - 1), Is.False);
            Assert.That(session.TryConsumeExtraLife(session.Snapshot().GenerationId), Is.True);
            Assert.That(session.EffectsSnapshot().ActiveEffects.Has(new EffectId("extra-life")), Is.False);
            Assert.That(frozen.Has(new EffectId("extra-life")), Is.True);
            Assert.That(session.TryConsumeExtraLife(session.Snapshot().GenerationId), Is.False);
            session.CompleteFloor(session.Snapshot().GenerationId); Open(session);
            Assert.That(session.TryConsumeExtraLife(session.Snapshot().GenerationId), Is.False);
            session.StartRun(73); Open(session);
            Assert.That(session.TryConsumeExtraLife(session.Snapshot().GenerationId), Is.False, "A new run also drops the upgrade.");
        }

    }
}
