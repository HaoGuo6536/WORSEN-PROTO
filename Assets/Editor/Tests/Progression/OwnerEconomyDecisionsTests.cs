// ============================================================================
// OwnerEconomyDecisionsTests.cs
// ============================================================================
// PURPOSE:
//   Locks the 2026-09-30 curse and shop decisions at pure transaction boundaries.
//   Temporary configs do not migrate serialized assets or imply live floor wiring.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Cover retired bail APIs, cheap upgrades and round/prerequisite admission.
//   - Keep Extra Life purchase history after consumption and clear it only on reset.
//   - Check flat discounts and exactly-once, uncapped shop growth of Nothing???.
//   - Verify the ordinary Mimic curse enables FaithlessWindow without opt-in.
// DEPENDENCIES:
//   Core, Progression/Shop, Hunter Mimic, existing Hunter test views, NUnit and Unity.
// USAGE NOTES:
//   Coordinator-run Edit Mode tests; injected random streams and deltas only.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Mimic;
using Worsen.Domain.Player;
using Worsen.Presentation.ProgressionUI;
using Worsen.Session.Progression;
using Worsen.Session.Progression.Shop;
using Worsen.Tests.Hunter;
using Object = UnityEngine.Object;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Progression
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class OwnerEconomyDecisionsTests
    {
        private ProgressionConfig config;
        private EffectCatalogueConfig catalogue;
        private static ActiveEffects Empty => default;
        private static ActiveEffects Held(string id, int count = 1, EffectKind kind = EffectKind.Curse) =>
            new ActiveEffects(new[] { new ActiveEffect(new EffectId(id), kind, count) });
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<ProgressionConfig>();
            catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            Set(config, "_effectCatalogue", catalogue);
            Set(config, "_eventPool", Array.Empty<ProgressionEventKind>());
        }
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(config); Object.DestroyImmediate(catalogue); }

        [Test] public void RetiredBailApisAndCatalogueRowsAreAbsent()
        {
            Assert.That(typeof(ProgressionConfig).GetProperty("EarlyBailWalletFraction"), Is.Null);
            Assert.That(typeof(ShopController).GetMethod("BailDebit"), Is.Null);
            foreach (var type in new[] { typeof(ProgressionSessionController), typeof(ProgressionSessionManager) })
                Assert.That(type.GetMethods().Where(m => m.Name == "CompleteFloor").All(m => m.GetParameters().Length == 1), Is.True);
            Assert.That(EffectCatalogueUtility.Find(catalogue, "bail-bond"), Is.Null);
            Assert.That(EffectCatalogueUtility.Find(catalogue, "thin-skin"), Is.Null);
        }

        [Test] public void FloorPresentationHasNoEarlyBailActionOrCard()
        {
            Assert.That(Enum.GetNames(typeof(ProgressionUIAction)).Any(name =>
                name.IndexOf("bail", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            var view = new ProgressionUIDriverState();
            new ProgressionUIPresenter().Present(view, new ProgressionSnapshot(1, 1, 1, 73, 10, 1, 1,
                ProgressionPhase.Exploring, 100f, 100f, null, null, null, default, "", false, false));
            Assert.That(view.Cards, Is.Empty);
            Assert.That(view.CanContinue, Is.False);
        }

        [TestCase(.99f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidFasterCollapseMultiplierIsRejected(float multiplier)
        {
            Set(config, "_fasterCollapseGoldenCakeMultiplier", multiplier);
            Assert.Throws<ArgumentException>(() => new ProgressionSessionController(
                new ProgressionSessionBehaviorState(), config, new System.Random(73)));
        }

        [Test] public void RoundOneCanDrawBothCheapUpgradesButNotTheExpensiveTier()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            var rules = new ShopRules();
            Assert.That(rules.ExpensivePrice, Is.EqualTo(8));
            foreach (string id in new[] { "soft-landing", "quiet-slide" })
            {
                var entry = EffectCatalogueUtility.Find(catalogue, id);
                Assert.That(entry.AvailabilityRound, Is.EqualTo(1));
                Assert.That(entry.Price, Is.InRange(4, 5));
            }
            foreach (var entry in catalogue.Entries.Where(e => e.Kind == EffectKind.Upgrade &&
                e.Id != "soft-landing" && e.Id != "quiet-slide")) Assert.That(entry.Price, Is.GreaterThanOrEqualTo(8));
            for (int seed = 0; seed < 64; seed++)
            {
                var shop = new ShopController(new ShopBehaviorState(), rules, catalogue, new System.Random(seed));
                shop.BeginVisit(1, Empty);
                foreach (var offer in shop.Offers(5, Empty).Where(o => o.Kind == EffectKind.Upgrade))
                { Assert.That(offer.Price, Is.InRange(4, 5)); Assert.That(offer.CanAfford, Is.True); seen.Add(offer.Id); }
            }
            Assert.That(seen, Is.EquivalentTo(new[] { "soft-landing", "quiet-slide" }));
        }

        [Test] public void NoRegenNeedsRoundTwelveAndSlowMend()
        {
            var entry = EffectCatalogueUtility.Find(catalogue, "no-regen");
            for (int round = 1; round < 12; round++)
                Assert.That(EffectCatalogueUtility.Eligible(entry, round, Held("slow-mend")), Is.False);
            Assert.That(EffectCatalogueUtility.Eligible(entry, 12, Empty), Is.False);
            Assert.That(EffectCatalogueUtility.Eligible(entry, 12, Held("slow-mend")), Is.True);
        }

        [Test] public void CurseEconomyDefaultsAreExplicitAndDiscountDoesNotCompoundWithStacks()
        {
            Assert.That(config.FasterCollapseGoldenCakeMultiplier, Is.EqualTo(1.15f));
            Assert.That(config.NothingShopPriceMultiplier, Is.EqualTo(.85f));
            var shop = new ShopController(new ShopBehaviorState(), new ShopRules(), catalogue, new System.Random(73), config.NothingShopPriceMultiplier);
            shop.BeginVisit(1, Empty);
            var entry = new EffectCatalogueEntry("test-price", EffectKind.Upgrade, FearAxis.Agency, "Price", "Adds a test effect.", price: 100);
            Assert.That(shop.Price(entry, Empty), Is.EqualTo(100));
            Assert.That(shop.Price(entry, Held("nothing")), Is.EqualTo(85));
            Assert.That(shop.Price(entry, Held("nothing", 100)), Is.EqualTo(85));
            var tuned = new ShopController(new ShopBehaviorState(), new ShopRules(), catalogue, new System.Random(73), .5f);
            tuned.BeginVisit(1, Empty); Assert.That(tuned.Price(entry, Held("nothing", 100)), Is.EqualTo(50));
        }

        [Test] public void ExtraLifeRemainsAvailableUntilPurchaseThenNeverRestocksUntilRunReset()
        {
            var life = EffectCatalogueUtility.Find(catalogue, "extra-life");
            Set(catalogue, "_entries", new[] { life });
            var state = new ShopBehaviorState();
            var shop = new ShopController(state, new ShopRules(), catalogue, new System.Random(73));
            shop.BeginVisit(6, Empty);
            Assert.That(shop.Offers(100, Empty).Single().Id, Is.EqualTo("extra-life"));
            Assert.That(shop.Purchase("extra-life", 0, Empty, out _, out _, out _), Is.False);
            shop.BeginVisit(9, Empty);
            Assert.That(shop.Offers(100, Empty).Single().Id, Is.EqualTo("extra-life"));
            Assert.That(shop.Purchase("extra-life", 100, Empty, out _, out _, out _), Is.True);
            Assert.That(shop.Offers(100, Empty), Is.Empty);
            Assert.That(shop.Purchase("extra-life", 100, Empty, out _, out _, out _), Is.False);
            Assert.That(shop.Reroll(100, Empty, out _), Is.False);
            shop = new ShopController(state, new ShopRules(), catalogue, new System.Random(74));
            shop.BeginVisit(12, Empty); Assert.That(shop.Offers(100, Empty), Is.Empty);
            shop.Reset(); shop.BeginVisit(6, Empty);
            Assert.That(shop.Offers(100, Empty).Single().Id, Is.EqualTo("extra-life"));
        }

        private ProgressionSessionController Session(params EffectCatalogueEntry[] entries)
        {
            Set(catalogue, "_entries", entries);
            Set(config, "_threats", new[] { new ProgressionEntryConfig("mimic", "Mimic", "Adds a hunter.") });
            Set(config, "_curses", new[] { new ProgressionEntryConfig("nothing", "Nothing???", "Nothing???") });
            var session = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(73));
            session.StartRun(73); return session;
        }
        private static void Open(ProgressionSessionController session)
        {
            var s = session.Snapshot();
            if (s.Phase == ProgressionPhase.ChooseThreat) Assert.That(session.ChooseThreat("mimic", s.Revision), Is.True);
            s = session.Snapshot();
            if (s.Phase == ProgressionPhase.ChooseCurse) Assert.That(session.ChooseCurse(s.Choices[0].Id, s.Revision), Is.True);
            Assert.That(session.ConfirmFloorReady(session.Snapshot().GenerationId), Is.True);
        }

        [Test] public void NothingAddsOneStackAndOneRosterMemberExactlyOncePerShopBeyondItsAcquisitionCap()
        {
            var session = Session(EffectCatalogueUtility.Find(catalogue, "nothing"));
            int visits = 0;
            for (int round = 1; round <= 15; round++)
            {
                if (session.GenerationRequest().IsShop)
                {
                    visits++;
                    var frozen = session.EffectsSnapshot().ActiveEffects;
                    Assert.That(frozen.Stacks(new EffectId("nothing")), Is.EqualTo(1 + visits));
                    Open(session);
                    Assert.That(session.ConfirmFloorReady(session.Snapshot().GenerationId), Is.False);
                    Assert.That(session.EffectsSnapshot().ActiveEffects.Stacks(new EffectId("nothing")), Is.EqualTo(1 + visits));
                    int revision = session.Snapshot().Revision;
                    Assert.That(session.ContinueShop(revision), Is.True);
                    Assert.That(session.ContinueShop(revision), Is.False);
                    Assert.That(frozen.Stacks(new EffectId("nothing")), Is.EqualTo(1 + visits));
                }
                else
                {
                    Open(session);
                    int selectionHunters = (round + 2) / 3;
                    Assert.That(session.Snapshot().ThreatCount, Is.EqualTo(selectionHunters + visits));
                    Assert.That(session.GenerationRequest().Effects.ActiveThreatIds.Count, Is.EqualTo(selectionHunters + visits));
                    session.CompleteFloor(session.Snapshot().GenerationId);
                }
            }
            session.StartRun(73);
            Assert.That(session.EffectsSnapshot().ActiveEffects.Has(new EffectId("nothing")), Is.False);
            Assert.That(session.Snapshot().ThreatCount, Is.Zero);
        }

        [Test] public void PurchasedLifeIsConsumedAndExcludedFromLaterSessionShopSnapshots()
        {
            var session = Session(new EffectCatalogueEntry("extra-life", EffectKind.Upgrade, FearAxis.Stakes,
                "Extra Life", "Revives once per run.", price: 0));
            for (int i = 0; i < 2; i++) { Open(session); session.CompleteFloor(session.Snapshot().GenerationId); }
            Open(session); Assert.That(session.Purchase("extra-life", session.Snapshot().Revision), Is.True);
            Assert.That(session.Snapshot().Offers, Is.Empty);
            session.ContinueShop(session.Snapshot().Revision); Open(session);
            Assert.That(session.TryConsumeExtraLife(session.Snapshot().GenerationId), Is.True);
            Assert.That(session.EffectsSnapshot().ActiveEffects.Has(new EffectId("extra-life")), Is.False);
            session.CompleteFloor(session.Snapshot().GenerationId); Open(session);
            session.CompleteFloor(session.Snapshot().GenerationId); Open(session);
            Assert.That(session.Snapshot().Offers, Is.Empty);
            Assert.That(session.Purchase("extra-life", session.Snapshot().Revision), Is.False);
        }

        [Test] public void FaithlessArrowIsAnOrdinaryRosterGatedChoiceAndEnablesTheMimicWindow()
        {
            var entry = EffectCatalogueUtility.Find(catalogue, MimicController.FaithlessArrow.Value);
            Assert.That(entry.Kind, Is.EqualTo(EffectKind.Curse));
            Assert.That(EffectCatalogueUtility.Eligible(entry, 12, Empty), Is.False);
            Assert.That(EffectCatalogueUtility.Eligible(entry, 12, Held("mimic", kind: EffectKind.Threat)), Is.True);
            var session = Session(entry);
            session.ChooseThreat("mimic", session.Snapshot().Revision);
            Assert.That(session.Snapshot().Choices.Select(c => c.Id), Does.Contain(entry.Id));
            Assert.That(session.ChooseCurse(entry.Id, session.Snapshot().Revision), Is.True);
            var mimicConfig = ScriptableObject.CreateInstance<MimicConfig>();
            try
            {
                var mimic = new MimicController(mimicConfig, new System.Random(73));
                var hunter = new RosterBTestHunter { Id = new EntityId(-1), IsActive = true };
                var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f };
                var world = new EchoControllerTests.World();
                HunterArchetypeContext Context(IReadOnlyActiveEffects effects, long tick) => new HunterArchetypeContext(
                    hunter, player, world, world, world.Doors, world, effects, mimicConfig.FaithlessInterval, tick, true, 1f);
                mimic.Reset(Context(Empty, 0)); mimic.Tick(Context(Empty, 1));
                Assert.That(mimic.FaithlessEnabled, Is.False);
                while (mimic.TakeFact(out var fact)) Assert.That(fact.Kind, Is.Not.EqualTo(MimicFactKind.FaithlessWindow));
                mimic.Tick(Context(session.EffectsSnapshot().ActiveEffects, 2));
                Assert.That(mimic.FaithlessEnabled, Is.True);
                Assert.That(mimic.TakeFact(out var window), Is.True);
                Assert.That(window.Kind, Is.EqualTo(MimicFactKind.FaithlessWindow));
                Assert.That(window.Seconds, Is.EqualTo(mimicConfig.FaithlessSeconds));
                mimic.Tick(Context(Empty, 3)); Assert.That(mimic.FaithlessEnabled, Is.False);
                Assert.That(mimic.TakeFact(out _), Is.False);
            }
            finally { Object.DestroyImmediate(mimicConfig); }
        }
    }
}
