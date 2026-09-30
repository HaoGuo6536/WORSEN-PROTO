// ============================================================================
// ShrineProgressionControllerTests.cs
// ============================================================================
// PURPOSE:
//   Tests shrine rewards and timing without world or Session-manager wiring.
//   Seeded streams and explicit ticks make the provisional outcomes reproducible.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Progression.
// KEY RESPONSIBILITIES:
//   - Cover Chance pools/lifetime, Bargain quotes, Echo history and delayed hearing.
//   - Cover Protection admission and Purgatory's fixed downside and scaled yield.
// DEPENDENCIES:
//   - Session Progression, Core, NUnit and temporary catalogue allocation.
// USAGE NOTES:
//   Pure rules only; external hunter, lamp, pocket and spawn delivery needs integration tests.
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
    public sealed class ShrineProgressionControllerTests
    {
        private EffectCatalogueConfig catalogue;
        private ShrineProgressionController controller;
        private readonly ActiveEffects empty = default;
        [SetUp] public void SetUp()
        {
            catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            SetEntries(
                Entry("a", EffectKind.Curse, value: 3), Entry("b", EffectKind.Curse), Entry("c", EffectKind.Curse),
                Entry("good", EffectKind.Upgrade), Entry("hunter", EffectKind.Curse, hunters: new[] { "echo" }),
                Entry("late", EffectKind.Upgrade, floor: 99), Entry("item", EffectKind.Consumable));
            controller = New(71);
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(catalogue);
        private ShrineProgressionController New(int seed) => new ShrineProgressionController(
            new ShrineProgressionBehaviorState(), new ShrineProgressionRules(), catalogue, new System.Random(seed));
        private static EffectCatalogueEntry Entry(string id, EffectKind kind, int value = 1, int floor = 1, string[] hunters = null)
            => new EffectCatalogueEntry(id, kind, FearAxis.Stakes, id, "Changes a rule.", floor: floor, value: value, hunters: hunters);
        private void SetEntries(params EffectCatalogueEntry[] entries) => typeof(EffectCatalogueConfig)
            .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(catalogue, entries);
        private static ShrineActivatedFact Fact(int id, ShrineKind kind) => new ShrineActivatedFact(id, kind, 2, Vector3.one, 10);
        private ShrineResolvedFact Resolve(int id, ShrineKind kind, int floor = 8, int wallet = 100, float fraction = 0f)
        {
            Assert.That(controller.Resolve(5, floor, Fact(id, kind), wallet, 1000f, fraction, empty, out var result), Is.True);
            return result;
        }
        [Test]
        public void ChanceDrawsSeededGeneralCurseOrUpgradeOnlyAndCombinesWithoutMutatingRetained()
        {
            var selectedKinds = new System.Collections.Generic.HashSet<EffectKind>();
            for (int seed = 0; seed < 40; seed++)
            {
                var rules = New(seed);
                Assert.That(rules.Resolve(1, 8, Fact(0, ShrineKind.Chance), 0, 0f, 0f, empty, out _), Is.True);
                var effect = rules.FloorEffects.Single();
                Assert.That(effect.Id.Value, Is.EqualTo(new[] { "a", "b", "c", "good" }[new System.Random(seed).Next(4)]));
                selectedKinds.Add(effect.Kind);
                Assert.That(rules.Combined(empty).Count, Is.EqualTo(1));
                var frozen = rules.FloorEffects;
                rules.EndFloor();
                Assert.That(rules.FloorEffects.Count, Is.Zero); Assert.That(frozen.Count, Is.EqualTo(1));
                Assert.That(rules.History.Count, Is.EqualTo(1));
            }
            Assert.That(selectedKinds, Is.EquivalentTo(new[] { EffectKind.Curse, EffectKind.Upgrade }));
        }
        [Test]
        public void ChanceRespectsCapsAndDoesNotClearRetainedStacksAtFloorEnd()
        {
            SetEntries(new EffectCatalogueEntry("a", EffectKind.Upgrade, FearAxis.Agency, "A", "Adds speed.", cap: 2));
            var retained = new ActiveEffects(new[] { new ActiveEffect(new EffectId("a"), EffectKind.Upgrade, 1) });
            Assert.That(controller.Resolve(1, 8, Fact(1, ShrineKind.Chance), 0, 0f, 0f, retained, out _), Is.True);
            Assert.That(controller.Combined(retained).Stacks(new EffectId("a")), Is.EqualTo(2));
            Assert.That(controller.Resolve(1, 8, Fact(2, ShrineKind.Chance), 0, 0f, 0f, retained, out _), Is.False);
            controller.EndFloor(); Assert.That(controller.Combined(retained), Is.EqualTo(retained));
        }
        [Test]
        public void ChanceAdmitsHunterDependentUpgradesOnlyWhenTheirHunterIsActive()
        {
            SetEntries(Entry("upgrade", EffectKind.Upgrade, hunters: new[] { "echo" }));
            Assert.That(controller.Resolve(1, 8, Fact(1, ShrineKind.Chance), 0, 0f, 0f, empty, out _), Is.False);
            var active = new ActiveEffects(new[] { new ActiveEffect(new EffectId("echo"), EffectKind.Threat, 1) });
            Assert.That(controller.Resolve(1, 8, Fact(2, ShrineKind.Chance), 0, 0f, 0f, active, out _), Is.True);
            Assert.That(controller.FloorEffects.Single().Id.Value, Is.EqualTo("upgrade"));
        }
        [Test]
        public void BargainOffersThreeDistinctCursesAndPaysQuotedValueOnce()
        {
            Resolve(1, ShrineKind.Bargain, floor: 9);
            Assert.That(controller.Deal().Offers, Is.Empty);
            controller.EndFloor(); controller.OpenDeal(empty);
            var offers = controller.Deal().Offers;
            Assert.That(offers.Select(o => o.Id), Is.EquivalentTo(new[] { "a", "b", "c" }));
            Assert.That(offers.Single(o => o.Id == "a").Payout, Is.EqualTo(3 * (2 + 9 / 2)));
            Assert.That(offers.Single(o => o.Id == "b").Payout, Is.EqualTo(2 + 9 / 2));
            controller.OpenDeal(empty);
            Assert.That(controller.Deal().Offers, Is.EqualTo(offers));
            Assert.That(controller.TakeDeal("a", 0, empty, out var curse, out int payout), Is.True);
            Assert.That(curse.Kind, Is.EqualTo(EffectKind.Curse)); Assert.That(payout, Is.EqualTo(3 * (2 + 9 / 2)));
            Assert.That(controller.TakeDeal("a", payout, empty, out _, out _), Is.False);
        }
        [Test]
        public void WalkAwayAndRunResetClearDealsWithoutAwardingAnything()
        {
            Resolve(1, ShrineKind.Bargain); controller.OpenDeal(empty); controller.CloseDeal();
            Assert.That(controller.TakeDeal("a", 100, empty, out _, out int payout), Is.False);
            Assert.That(payout, Is.Zero); Assert.That(controller.Deal().Pending, Is.False);
            controller.ResetRun(); Assert.That(controller.History, Is.Empty);
            Assert.That(controller.Resolve(2, 8, Fact(1, ShrineKind.Echo), 100, 100f, 0f, empty, out _), Is.False);
        }
        [Test]
        public void PacificationDropsBeliefImmediatelyButEmitsNoiseOnlyAfterDelay()
        {
            var result = Resolve(1, ShrineKind.Pacification);
            Assert.That(result.DropBeliefs, Is.True);
            Assert.That(controller.Tick(1f, 11), Is.Empty);
            Assert.That(controller.Tick(1f, 11), Is.Empty, "Repeated ticks do not advance the queue.");
            var noise = controller.Tick(0.5f, 12).Single();
            Assert.That(noise.Tick, Is.EqualTo(12)); Assert.That(noise.Position, Is.EqualTo(Vector3.one));
            Assert.That(noise.SourceKind, Is.EqualTo(NoiseSourceKind.Shrine)); Assert.That(noise.Loudness, Is.EqualTo(20f));
            Assert.That(controller.Tick(10f, 13), Is.Empty);
            Resolve(2, ShrineKind.Pacification); controller.EndFloor();
            Assert.That(controller.Tick(10f, 14), Is.Empty);
        }
        [Test]
        public void ProtectionRejectsPoorOrAbsentPlayerAndReplayNeverGrantsTwice()
        {
            Assert.That(controller.Resolve(1, 8, Fact(1, ShrineKind.Protection), 5, 100f, 0f, empty, out _), Is.False);
            Assert.That(controller.Resolve(1, 8, Fact(1, ShrineKind.Protection), 100, 100f, 0f, empty, out _), Is.False);
            Assert.That(controller.Resolve(1, 8, Fact(2, ShrineKind.Protection), 100, 0f, 0f, empty, out _), Is.False);
            var result = Resolve(3, ShrineKind.Protection);
            Assert.That(result.Cost, Is.EqualTo(4 + 8 / 3)); Assert.That(result.Shield, Is.EqualTo(40f));
            Assert.That(controller.Resolve(1, 8, Fact(3, ShrineKind.Protection), 100, 100f, 0f, empty, out _), Is.False);
        }
        [Test]
        public void EchoReplaysLastEffectiveKindAcrossFloorsWithoutRecursion()
        {
            Resolve(1, ShrineKind.Protection); controller.EndFloor();
            var result = Resolve(1, ShrineKind.Echo);
            Assert.That(result.ResolvedKind, Is.EqualTo(ShrineKind.Protection)); Assert.That(result.ChangedOutcome, Is.True);
            Assert.That(result.Shield, Is.EqualTo(80f)); Assert.That(result.Cost, Is.EqualTo(2 * (4 + 8 / 3)));
            Assert.That(Resolve(2, ShrineKind.Echo).Shield, Is.EqualTo(80f));
            Assert.That(controller.History.Count, Is.EqualTo(3));
        }
        [Test]
        public void EchoChanceDrawsTwiceAndExhaustedPoolIsSafe()
        {
            SetEntries(Entry("a", EffectKind.Curse), Entry("good", EffectKind.Upgrade));
            Resolve(1, ShrineKind.Chance); controller.EndFloor(); Resolve(1, ShrineKind.Echo);
            Assert.That(controller.FloorEffects.Count, Is.EqualTo(2));
            Assert.That(controller.Resolve(1, 8, Fact(2, ShrineKind.Echo), 0, 0f, 0f, empty, out _), Is.False);
        }
        [TestCase(0f, 2f)] [TestCase(0.75f, 1.25f)] [TestCase(1f, 1f)]
        [TestCase(-1f, 2f)] [TestCase(2f, 1f)]
        public void PurgatoryScalesYieldButAlwaysAsksForOneHunter(float fraction, float expected)
        {
            var result = Resolve(1, ShrineKind.Purgatory, fraction: fraction);
            Assert.That(result.YieldMultiplier, Is.EqualTo(expected)); Assert.That(result.ExtraHunters, Is.EqualTo(1));
            Assert.That(result.Mutation, Is.EqualTo(new System.Random(71).NextDouble() < 0.25));
            Assert.That(controller.YieldMultiplier, Is.EqualTo(expected));
            controller.EndFloor(); Assert.That(controller.YieldMultiplier, Is.EqualTo(1f));
        }
        [Test]
        public void WickPassageAndChangedOutcomesRemainExplicitIntegrationFacts()
        {
            Assert.That(Resolve(1, ShrineKind.Wick).WickSeconds, Is.EqualTo(10f));
            Assert.That(Resolve(2, ShrineKind.Echo).WickSeconds, Is.EqualTo(20f));
            Assert.That(Resolve(3, ShrineKind.Passage).ResolvedKind, Is.EqualTo(ShrineKind.Passage));
            Assert.That(Resolve(4, ShrineKind.Echo).YieldMultiplier, Is.EqualTo(2f));
        }
        [Test]
        public void EchoBargainDoublesPayoutAndPacificationHalvesDelay()
        {
            Resolve(1, ShrineKind.Bargain); Resolve(2, ShrineKind.Echo);
            controller.EndFloor(); controller.OpenDeal(empty);
            Assert.That(controller.Deal().Offers.Single(o => o.Id == "a").Payout, Is.EqualTo(2 * 3 * (2 + 8 / 2)));
            controller.CloseDeal(); Resolve(3, ShrineKind.Pacification); Resolve(4, ShrineKind.Echo);
            var noise = controller.Tick(0.75f, 11).Single(); Assert.That(noise.Loudness, Is.EqualTo(40f));
            Assert.That(controller.Tick(0.75f, 12).Single().Loudness, Is.EqualTo(20f));
        }
    }
}
