// ============================================================================
// ProgressionAdmissionRegressionTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the batch 13 progression regressions without native Unity objects.
//   Explicit managed config inputs supplement the asset/default fixtures, allowing
//   real selection and shop transactions to run in the offline pure tier.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Verify round-gated, seed-stable menus and retained roster activation.
//   - Admit Faithless Arrow only after selecting Mimic through normal cadence.
//   - Check Nothing's flat discount and exactly-once uncapped shop growth.
// DEPENDENCIES:
//   - Core effects, Progression controllers/config data, reflection and NUnit.
// USAGE NOTES:
//   Managed-only config shells follow HunterAttackControllerTests. Every consumed
//   tuning field is explicitly supplied; these tests do not claim Unity defaults,
//   saved catalogue migration, geometry, physics or native object lifecycle.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using Worsen.Core;
using Worsen.Session.Progression;

namespace Worsen.Tests.Progression
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProgressionAdmissionRegressionTests
    {
        private static readonly string[] Roster = {
            "echo", "weaver", "ticking", "ram", "mannequin", "mimic", "blinder", "herald", "skip", "stare" };
        private static readonly int[] Gates = { 1, 1, 1, 4, 4, 5, 5, 6, 6, 6 };

        [Test]
        public void RoundOneMenuIsFixedLaterMenusVaryAndEveryRetainedHunterStaysActive()
        {
            var firstMenus = new HashSet<string>(); var laterMenus = new HashSet<string>();
            for (int seed = 0; seed < 32; seed++)
            {
                var left = Session(seed, Roster); var right = Session(seed, Roster);
                var retained = new List<string>(); var random = new System.Random(seed);
                for (int round = 1; round <= 10; round++)
                {
                    var snapshot = left.Snapshot();
                    Assert.That(snapshot.Round, Is.EqualTo(round));
                    Assert.That(left.GenerationRequest().Seed, Is.EqualTo(random.Next()));
                    string[] offered = snapshot.Choices.Select(choice => choice.Id).ToArray();
                    Assert.That(right.Snapshot().Choices.Select(choice => choice.Id), Is.EqualTo(offered));
                    Assert.That(left.Snapshot().Choices.Select(choice => choice.Id), Is.EqualTo(offered));
                    if (snapshot.Phase == ProgressionPhase.ChooseThreat)
                    {
                        string[] eligible = Roster.Where((id, index) => Gates[index] <= round).ToArray();
                        Assert.That(offered.Length, Is.EqualTo(3));
                        Assert.That(offered.Distinct().Count(), Is.EqualTo(3));
                        Assert.That(offered, Is.SubsetOf(eligible));
                        if (round == 1) { firstMenus.Add(string.Join(",", offered)); Assert.That(offered, Is.EquivalentTo(eligible)); }
                        if (round == 4) laterMenus.Add(string.Join(",", offered));
                        retained.Add(offered[0]);
                    }
                    Open(left); Open(right);
                    Assert.That(left.GenerationRequest().Effects.ActiveThreatIds, Is.EqualTo(retained));
                    Assert.That(left.GenerationRequest().Effects.ActiveThreatBudget,
                        Is.EqualTo(left.GenerationRequest().IsShop ? 0 : retained.Count));
                    Finish(left); Finish(right);
                }
            }
            Assert.That(firstMenus.Count, Is.EqualTo(1)); Assert.That(laterMenus.Count, Is.GreaterThan(1));
        }

        [Test]
        public void FaithlessArrowEntersOrdinaryMenuAfterRoundGatedMimicSelection()
        {
            const string arrow = "mimic-faithless-arrow";
            var session = Session(73, new[] { "echo", "mimic" }, new EffectCatalogueEntry(
                arrow, EffectKind.Curse, FearAxis.Information, "Faithless Arrow", "Replaces safe guidance.", hunters: new[] { "mimic" }));
            while (session.Snapshot().Round < 7)
            {
                var snapshot = session.Snapshot();
                if (snapshot.Phase == ProgressionPhase.ChooseThreat)
                {
                    Assert.That(snapshot.Choices.Select(choice => choice.Id), Does.Not.Contain("mimic"));
                    Assert.That(session.ChooseThreat("mimic", snapshot.Revision), Is.False);
                    Assert.That(session.ChooseThreat("echo", session.Snapshot().Revision), Is.True);
                }
                Assert.That(session.Snapshot().Choices.Select(choice => choice.Id), Does.Not.Contain(arrow));
                Open(session); Finish(session);
            }
            Assert.That(session.ChooseThreat("mimic", session.Snapshot().Revision), Is.True);
            Assert.That(session.Snapshot().Phase, Is.EqualTo(ProgressionPhase.ChooseCurse));
            Assert.That(session.Snapshot().Choices.Select(choice => choice.Id), Does.Contain(arrow));
            Assert.That(session.ChooseCurse(arrow, session.Snapshot().Revision), Is.True);
            Assert.That(session.EffectsSnapshot().ActiveEffects.Stacks(new EffectId(arrow)), Is.EqualTo(1));
            Assert.That(session.GenerationRequest().Effects.ActiveThreatIds, Does.Contain("mimic"));
        }

        [Test]
        public void NothingDiscountStaysFlatAndEachShopAddsOneStackAndOneRetainedHunter()
        {
            var session = Session(73, new[] { "echo" },
                new EffectCatalogueEntry("nothing", EffectKind.Curse, FearAxis.Unpredictability, "Nothing???", "Nothing???"),
                new EffectCatalogueEntry("test-consumable", EffectKind.Consumable, FearAxis.Stakes, "Test", "Adds a test item.", price: 100));
            int selections = 0, visits = 0;
            for (int round = 1; round <= 15; round++)
            {
                var snapshot = session.Snapshot();
                if (snapshot.Phase == ProgressionPhase.ChooseThreat) selections++;
                Open(session);
                if (session.GenerationRequest().IsShop)
                {
                    visits++;
                    var frozen = session.EffectsSnapshot().ActiveEffects;
                    Assert.That(frozen.Stacks(new EffectId("nothing")), Is.EqualTo(1 + visits));
                    // The shop's ordinary round inflation still applies; Nothing discounts it once, not once per stack.
                    int expectedPrice = (int)Math.Ceiling(100m * (1m + .15m * (round - 1)) * .85m);
                    Assert.That(session.Snapshot().Offers.Single().Price, Is.EqualTo(expectedPrice));
                    Assert.That(session.ConfirmFloorReady(session.Snapshot().GenerationId), Is.False);
                    Assert.That(session.Snapshot().ThreatCount, Is.EqualTo(selections + visits));
                    int revision = session.Snapshot().Revision;
                    Assert.That(session.ContinueShop(revision), Is.True);
                    Assert.That(session.ContinueShop(revision), Is.False);
                    Assert.That(session.EffectsSnapshot().ActiveEffects.Stacks(new EffectId("nothing")), Is.EqualTo(1 + visits));
                    Assert.That(frozen.Stacks(new EffectId("nothing")), Is.EqualTo(1 + visits));
                }
                else
                {
                    Assert.That(session.GenerationRequest().Effects.ActiveThreatBudget, Is.EqualTo(selections + visits));
                    Assert.That(session.GenerationRequest().Effects.ActiveThreatIds, Is.EqualTo(Enumerable.Repeat("echo", selections + visits)));
                    Finish(session);
                }
            }
            session.StartRun(73);
            Assert.That(session.Snapshot().ThreatCount, Is.Zero);
            Assert.That(session.EffectsSnapshot().ActiveEffects.Has(new EffectId("nothing")), Is.False);
        }

        private static ProgressionSessionController Session(int seed, string[] roster, params EffectCatalogueEntry[] entries)
        {
            var config = (ProgressionConfig)FormatterServices.GetUninitializedObject(typeof(ProgressionConfig));
            var catalogue = (EffectCatalogueConfig)FormatterServices.GetUninitializedObject(typeof(EffectCatalogueConfig));
            Set(catalogue, "_entries", entries); Set(config, "_effectCatalogue", catalogue);
            Set(config, "_threats", roster.Select(id => new ProgressionEntryConfig(id, id, "Adds a hunter.")).ToArray());
            Set(config, "_curses", new[] { new ProgressionEntryConfig(entries.Any(entry => entry.Id == "nothing") ? "nothing" : "test-curse",
                "Test curse", "Removes safety.") });
            Set(config, "_offers", Array.Empty<ProgressionEntryConfig>());
            Set(config, "_selectionInterval", 2); Set(config, "_shopInterval", 2); Set(config, "_goldenCakeValue", 1);
            Set(config, "_initialMaximumHealth", 100f); Set(config, "_minimumMaximumHealth", 30f); Set(config, "_maximumMaximumHealth", 200f);
            Set(config, "_minimumMultiplier", .4f); Set(config, "_maximumMovementMultiplier", 1.6f);
            Set(config, "_maximumHunterMultiplier", 1.5f); Set(config, "_maximumFogMultiplier", 2f); Set(config, "_maximumFlashlightMultiplier", 2f);
            Set(config, "_nothingShopPriceMultiplier", .85f); Set(config, "_fasterCollapseGoldenCakeMultiplier", 1.15f);
            Set(config, "_firstEventRound", 8); Set(config, "_eventInterval", 8); Set(config, "_eventHazardFloors", 3);
            Set(config, "_eventJitter", 0); Set(config, "_maximumMutationSpeedMultiplier", 4f);
            Set(config, "_eventPool", Array.Empty<ProgressionEventKind>());
            var session = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(seed));
            session.StartRun(seed); return session;
        }
        private static void Open(ProgressionSessionController session)
        {
            var snapshot = session.Snapshot();
            if (snapshot.Phase == ProgressionPhase.ChooseThreat)
                Assert.That(session.ChooseThreat(snapshot.Choices[0].Id, snapshot.Revision), Is.True);
            snapshot = session.Snapshot();
            if (snapshot.Phase == ProgressionPhase.ChooseCurse)
                Assert.That(session.ChooseCurse(snapshot.Choices[0].Id, snapshot.Revision), Is.True);
            Assert.That(session.ConfirmFloorReady(session.Snapshot().GenerationId), Is.True);
        }
        private static void Finish(ProgressionSessionController session)
        {
            if (session.GenerationRequest().IsShop) Assert.That(session.ContinueShop(session.Snapshot().Revision), Is.True);
            else Assert.That(session.CompleteFloor(session.Snapshot().GenerationId), Is.True);
        }
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
