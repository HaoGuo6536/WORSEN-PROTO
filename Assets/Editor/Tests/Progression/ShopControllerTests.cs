// ============================================================================
// ShopControllerTests.cs
// ============================================================================
// PURPOSE:
//   Checks catalogue draws and every economy upgrade without generating rooms.
//   Real controller transactions verify that reservations never move money or items.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Cover seeded eligibility, prices, rerolls, receipts, replacement and wallet bounds.
//   - Cover retained economy upgrades, cheap opening offers and revision protection.
// DEPENDENCIES:
//   - Core, Session Progression/Shop, NUnit and temporary Unity config allocation.
// USAGE NOTES:
//   Edit Mode. Config reflection authors test data only; no scene or asset writes.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Progression;
using Worsen.Session.Progression.Shop;

namespace Worsen.Tests.Progression
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ShopControllerTests
    {
        private EffectCatalogueConfig catalogue;
        private ProgressionConfig config;
        private ShopConfig tuning;
        private static readonly ActiveEffects Empty = new ActiveEffects(Array.Empty<ActiveEffect>());
        [SetUp] public void SetUp()
        {
            catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            config = ScriptableObject.CreateInstance<ProgressionConfig>();
            tuning = ScriptableObject.CreateInstance<ShopConfig>();
            Set(config, "_effectCatalogue", catalogue); Set(config, "_shopConfig", tuning);
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(catalogue);
            UnityEngine.Object.DestroyImmediate(tuning);
        }
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private void Entries(params EffectCatalogueEntry[] entries) => Set(catalogue, "_entries", entries);
        private static EffectCatalogueEntry Entry(string id, EffectKind kind = EffectKind.Consumable, int price = 4, int cap = 1) =>
            new EffectCatalogueEntry(id, kind, FearAxis.Agency, id, "Adds a test effect.", cap: cap, price: price);
        private static ActiveEffects Active(string id, int stacks = 1, EffectKind kind = EffectKind.Upgrade) =>
            new ActiveEffects(new[] { new ActiveEffect(new EffectId(id), kind, stacks) });
        private ShopController Shop(int round = 5, IReadOnlyActiveEffects active = null, int seed = 73)
        {
            var shop = new ShopController(new ShopBehaviorState(), tuning.Rules, catalogue, new System.Random(seed));
            shop.BeginVisit(round, active ?? Empty); return shop;
        }

        [Test]
        public void DefaultDrawsAreSeededUniqueSubsetsAndExpensiveUpgradesUnlockAtFive()
        {
            var signatures = new HashSet<string>();
            for (int seed = 0; seed < 32; seed++)
            {
                var left = Shop(5, seed: seed); var right = Shop(5, seed: seed);
                var ids = left.Offers(100, Empty).Select(o => o.Id).ToArray();
                Assert.That(ids.Length, Is.EqualTo(4)); Assert.That(ids.Distinct().Count(), Is.EqualTo(4));
                Assert.That(right.Offers(100, Empty).Select(o => o.Id), Is.EqualTo(ids));
                Assert.That(left.Reroll(100, Empty, out _), Is.True);
                Assert.That(right.Reroll(100, Empty, out _), Is.True);
                Assert.That(right.Offers(100, Empty).Select(o => o.Id), Is.EqualTo(left.Offers(100, Empty).Select(o => o.Id)));
                signatures.Add(string.Join(",", ids));
                Assert.That(Shop(4, seed: seed).Offers(100, Empty).All(o => o.Kind == EffectKind.Consumable ||
                    EffectCatalogueUtility.Find(catalogue, o.Id).Price < tuning.Rules.ExpensivePrice), Is.True);
            }
            Assert.That(signatures.Count, Is.GreaterThan(1));
            Entries(Entry("expensive", EffectKind.Upgrade, 8));
            Assert.That(Shop(4).Offers(100, Empty), Is.Empty);
            Assert.That(Shop(5).Offers(100, Empty).Single().Id, Is.EqualTo("expensive"));
        }

        [Test]
        public void DrawAdmissionChecksFloorPrerequisiteStackCapAndAnyRequiredHunter()
        {
            Entries(new EffectCatalogueEntry("gated", EffectKind.Upgrade, FearAxis.Agency, "Gated", "Adds a rule.",
                floor: 6, cap: 2, price: 1, prerequisite: "key", hunters: new[] { "watcher", "echo" }));
            ActiveEffects Held(bool key, bool hunter, int stacks)
            {
                var held = new List<ActiveEffect>();
                if (key) held.Add(new ActiveEffect(new EffectId("key"), EffectKind.Upgrade, 1));
                if (hunter) held.Add(new ActiveEffect(new EffectId("echo"), EffectKind.Threat, 1));
                if (stacks > 0) held.Add(new ActiveEffect(new EffectId("gated"), EffectKind.Upgrade, stacks));
                return new ActiveEffects(held);
            }
            Assert.That(Shop(5, Held(true, true, 0)).Offers(100, Held(true, true, 0)), Is.Empty);
            foreach (var active in new[] { Held(false, true, 0), Held(true, false, 0), Held(true, true, 2) })
                Assert.That(Shop(6, active).Offers(100, active), Is.Empty);
            var valid = Held(true, true, 1);
            Assert.That(Shop(6, valid).Offers(100, valid).Single().Id, Is.EqualTo("gated"));
        }

        [Test]
        public void RerollsUseOneFreeThenEscalateAndCannotOverdraw()
        {
            var shop = Shop(); int wallet = 10;
            foreach (int price in new[] { 0, 2, 3, 4 })
            {
                Assert.That(shop.RerollPrice(Empty), Is.EqualTo(price));
                int before = wallet;
                Assert.That(shop.Reroll(wallet, Empty, out wallet), Is.True);
                Assert.That(wallet, Is.EqualTo(before - price));
            }
            Assert.That(shop.Reroll(wallet, Empty, out int unchanged), Is.False);
            Assert.That(unchanged, Is.EqualTo(wallet));
            Assert.That(shop.FreeRerolls(Active("shop-reroll")), Is.EqualTo(1), "A newly bought stack grants a free reroll even after paid rerolls.");
            shop.BeginVisit(6, Empty); Assert.That(shop.RerollPrice(Empty), Is.Zero);
            var boosted = Active("shop-reroll", 2);
            Assert.That(Shop(active: boosted).FreeRerolls(boosted), Is.EqualTo(3));
            Assert.That(shop.SelectionRerolls(Active("lucky-reroll", 2)), Is.EqualTo(2));
            Assert.That(shop.FreeRerolls(Active("lucky-reroll", 2)), Is.EqualTo(1), "Selection rerolls never become shop rerolls.");
        }

        [Test]
        public void BargainIsNextVisitOnlyLoyaltyStacksAndPricesRoundUp()
        {
            Entries(Entry("bargain-hunter", EffectKind.Upgrade, 1), Entry("loyalty-card", EffectKind.Upgrade, 1, 3), Entry("item", price: 10));
            var shop = Shop(1); var item = catalogue.Entries.Last();
            Assert.That(shop.Purchase("bargain-hunter", 100, Empty, out _, out _, out _), Is.True);
            Assert.That(shop.Price(item, Empty), Is.EqualTo(10));
            shop.BeginVisit(2, Active("bargain-hunter"));
            Assert.That(shop.Price(item, Empty), Is.EqualTo(9));
            Assert.That(shop.Price(item, Active("loyalty-card", 2)), Is.EqualTo(7));
            shop.BeginVisit(3, Empty);
            Assert.That(shop.Price(item, Empty), Is.EqualTo(13));
            Assert.That(shop.Price(item, Active("loyalty-card", 2)), Is.EqualTo(11));
        }

        [Test]
        public void GoldenTouchBusinessLicenseAndInterestRespectRoundingCapsAndOverflow()
        {
            var shop = Shop();
            Assert.That(shop.Interest(49, Active("interest")), Is.EqualTo(4));
            Assert.That(shop.Interest(100, Active("interest")), Is.EqualTo(5));
            Assert.That(shop.Interest(int.MaxValue, Active("interest")), Is.Zero);
            Assert.That(shop.Interest(100, Empty), Is.Zero);
            Assert.That(shop.GoldenCredit(0, 1, Active("golden-touch"), out int credit), Is.True);
            Assert.That(credit, Is.EqualTo(2));
            foreach (int stacks in new[] { 1, 2, 3 })
            {
                shop.Reset(); int total = 0;
                for (int i = 0; i < 4; i++)
                { Assert.That(shop.GoldenCredit(total, 1, Active("business-license", stacks), out credit), Is.True); total += credit; }
                Assert.That(total, Is.EqualTo(stacks == 1 ? 5 : 6));
            }
            shop.Reset();
            var combined = new ActiveEffects(new[] { new ActiveEffect(new EffectId("golden-touch"), EffectKind.Upgrade, 1),
                new ActiveEffect(new EffectId("business-license"), EffectKind.Upgrade, 2) });
            Assert.That(shop.GoldenCredit(0, 1, combined, out credit), Is.True); Assert.That(credit, Is.EqualTo(3));
            Assert.That(shop.GoldenCredit(int.MaxValue, 1, combined, out credit), Is.False);
            Assert.That(credit, Is.Zero);
        }

        [Test]
        public void BiggerPocketsAndExtraPedestalUseCatalogueCaps()
        {
            Assert.That(Shop().Inventory(Empty).Count, Is.EqualTo(3));
            foreach (int stacks in new[] { 1, 2, 3, 4 })
                Assert.That(Shop().Inventory(Active("bigger-pockets", stacks)).Count, Is.EqualTo(3 + Math.Min(stacks, 3)));
            var active = Active("extra-pedestal", 2);
            Assert.That(Shop(active: active).Offers(100, active).Count, Is.EqualTo(5));
        }

        [Test]
        public void ExtraPedestalExpandsWithoutRestockingAlreadySoldOffers()
        {
            var shop = Shop(); var first = shop.Offers(100, Empty)[0];
            Assert.That(shop.Purchase(first.Id, 100, Empty, out _, out _, out _), Is.True);
            var active = Active("extra-pedestal"); shop.ExpandPedestals(active);
            Assert.That(shop.Offers(100, active).Count, Is.EqualTo(5));
            Assert.That(shop.Offers(100, active).Single(o => o.Id == first.Id).Purchased, Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void ReplacementIsDeferredAndRefundUsesDiscardedReceiptOnly(bool refund)
        {
            Entries(Entry("a"), Entry("b"), Entry("c"), Entry("d"), Entry("refund", EffectKind.Upgrade));
            Set(tuning.Rules, "_pedestals", 5);
            var active = refund ? Active("refund") : Empty; var shop = Shop(3, active); int wallet = 100;
            foreach (string id in new[] { "a", "b", "c" })
                Assert.That(shop.Purchase(id, wallet, active, out wallet, out _, out _), Is.True);
            var frozen = shop.Inventory(active); int before = wallet;
            Assert.That(shop.Purchase("d", wallet, active, out wallet, out _, out _), Is.True);
            Assert.That(shop.HasPending, Is.True); Assert.That(wallet, Is.EqualTo(before));
            Assert.That(shop.Reroll(wallet, active, out wallet), Is.False);
            Assert.That(shop.Purchase("d", wallet, active, out wallet, out _, out _), Is.False);
            Assert.That(shop.CancelReplacement(), Is.True); Assert.That(wallet, Is.EqualTo(before));
            Assert.That(shop.Inventory(active), Is.EqualTo(frozen));
            Assert.That(shop.Purchase("d", wallet, active, out wallet, out _, out _), Is.True);
            Assert.That(shop.Purchase("d", wallet, active, out wallet, out _, out _, 3), Is.False);
            Assert.That(shop.Purchase("d", wallet, active, out wallet, out string removed, out _, 1), Is.True);
            Assert.That(removed, Is.EqualTo("b"));
            Assert.That(wallet, Is.EqualTo(before - 6 + (refund ? 3 : 0)));
            Assert.That(shop.Inventory(active)[1].Id, Is.EqualTo("d")); Assert.That(frozen[1].Id, Is.EqualTo("b"));
            Assert.That(shop.Purchase("d", wallet, active, out wallet, out _, out _, 1), Is.False);
        }

        private ProgressionSessionController Session(params EffectCatalogueEntry[] entries)
        {
            Entries(entries);
            var controller = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(73));
            controller.StartRun(73);
            for (int floor = 0; floor < 2; floor++)
            {
                Open(controller);
                if (floor == 0) for (int i = 0; i < 100; i++) controller.RecordGoldenCollected(controller.Snapshot().GenerationId, i);
                Assert.That(controller.CompleteFloor(controller.Snapshot().GenerationId), Is.True);
            }
            Assert.That(controller.ConfirmFloorReady(controller.Snapshot().GenerationId), Is.True);
            return controller;
        }
        private static void Open(ProgressionSessionController controller)
        {
            if (controller.Snapshot().Phase == ProgressionPhase.ChooseThreat) controller.ChooseThreat(controller.Snapshot().Choices[0].Id, controller.Snapshot().Revision);
            if (controller.Snapshot().Phase == ProgressionPhase.ChooseCurse) controller.ChooseCurse(controller.Snapshot().Choices[0].Id, controller.Snapshot().Revision);
            Assert.That(controller.ConfirmFloorReady(controller.Snapshot().GenerationId), Is.True);
        }

        [Test]
        public void SessionReservationCancellationAndConfirmationAreRevisionGuardedAndPublishHeldEffects()
        {
            var controller = Session(Entry("wax-ward"), Entry("b"), Entry("c"), Entry("d"));
            foreach (string id in new[] { "wax-ward", "b", "c" }) Assert.That(controller.Purchase(id, controller.Snapshot().Revision), Is.True);
            var frozen = controller.EffectsSnapshot(); int before = controller.Snapshot().Wallet;
            int revision = controller.Snapshot().Revision;
            Assert.That(controller.Purchase("d", revision), Is.True); Assert.That(controller.Purchase("d", revision), Is.False);
            Assert.That(controller.Snapshot().PendingOfferId, Is.EqualTo("d"));
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(before));
            Assert.That(controller.ContinueShop(controller.Snapshot().Revision), Is.False);
            Assert.That(controller.RerollShop(controller.Snapshot().Revision), Is.False);
            Assert.That(controller.CancelReplacement(controller.Snapshot().Revision), Is.True);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(before));
            Assert.That(controller.Snapshot().Inventory, Is.EqualTo(frozen.Progression.Inventory));
            Assert.That(controller.Purchase("d", controller.Snapshot().Revision), Is.True);
            revision = controller.Snapshot().Revision;
            Assert.That(controller.ReplaceInventorySlot(0, revision), Is.True);
            Assert.That(controller.ReplaceInventorySlot(1, revision), Is.False);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(before - 6));
            Assert.That(controller.EffectsSnapshot().ActiveEffects.Has(new EffectId("wax-ward")), Is.False);
            Assert.That(controller.Snapshot().Effects.WaxWardCharges, Is.Zero);
            Assert.That(controller.EffectsSnapshot().ActiveEffects.Has(new EffectId("d")), Is.True);
            Assert.That(frozen.ActiveEffects.Has(new EffectId("wax-ward")), Is.True);
            Assert.Throws<NotSupportedException>(() => ((IList<ProgressionInventorySlot>)frozen.Progression.Inventory)[0] = default);
        }

        [Test]
        public void SessionUpgradeStacksRerollsAndSelectionBudgetsCannotReplay()
        {
            var controller = Session(Entry("lucky-reroll", EffectKind.Upgrade, 1, 3), Entry("shop-reroll", EffectKind.Upgrade, 1, 3),
                Entry("bigger-pockets", EffectKind.Upgrade, 1, 3));
            Assert.That(controller.Purchase("shop-reroll", controller.Snapshot().Revision), Is.True);
            Assert.That(controller.Snapshot().FreeRerollsRemaining, Is.EqualTo(2));
            for (int stack = 1; stack <= 3; stack++)
            {
                if (stack > 1)
                {
                    int revision = controller.Snapshot().Revision; Assert.That(controller.RerollShop(revision), Is.True);
                    Assert.That(controller.RerollShop(revision), Is.False);
                }
                Assert.That(controller.Purchase("lucky-reroll", controller.Snapshot().Revision), Is.True);
                Assert.That(controller.Purchase("bigger-pockets", controller.Snapshot().Revision), Is.True);
                Assert.That(controller.Snapshot().Inventory.Count, Is.EqualTo(3 + stack));
            }
            Assert.That(controller.Purchase("bigger-pockets", controller.Snapshot().Revision), Is.False);
            Assert.That(controller.ContinueShop(controller.Snapshot().Revision), Is.True);
            foreach (bool hunter in new[] { true, false })
            {
                for (int remaining = 3; remaining > 0; remaining--)
                {
                    Assert.That(controller.Snapshot().FreeRerollsRemaining, Is.EqualTo(remaining));
                    int revision = controller.Snapshot().Revision;
                    Assert.That(controller.RerollSelection(revision), Is.True); Assert.That(controller.RerollSelection(revision), Is.False);
                }
                Assert.That(controller.RerollSelection(controller.Snapshot().Revision), Is.False);
                string id = controller.Snapshot().Choices[0].Id;
                if (hunter) Assert.That(controller.ChooseThreat(id, controller.Snapshot().Revision), Is.True);
                else Assert.That(controller.ChooseCurse(id, controller.Snapshot().Revision), Is.True);
            }
        }

        [Test]
        public void SessionWalletEffectsUseActualPurchasesAndNormalCompletionIsExactlyOnce()
        {
            var controller = Session(Entry("golden-touch", EffectKind.Upgrade, 0), Entry("business-license", EffectKind.Upgrade, 0, 2),
                Entry("interest", EffectKind.Upgrade, 0));
            foreach (var offer in controller.Snapshot().Offers) Assert.That(controller.Purchase(offer.Id, controller.Snapshot().Revision), Is.True);
            Assert.That(controller.ContinueShop(controller.Snapshot().Revision), Is.True);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(105));
            Open(controller); int generation = controller.Snapshot().GenerationId;
            Assert.That(controller.RecordGoldenCollected(generation, 0), Is.True);
            Assert.That(controller.RecordGoldenCollected(generation, 0), Is.False);
            Assert.That(controller.RecordGoldenCollected(generation, 1), Is.True);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(110));
            Assert.That(controller.CompleteFloor(generation), Is.True);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(115));
            Assert.That(controller.CompleteFloor(generation), Is.False);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(115));
        }
    }
}
