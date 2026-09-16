// ============================================================================
// ProgressionSessionControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the expedition independently of generated geometry and frame time.
//   The tests cover long runs, economic replay protection, retained
//   effects and stale callbacks so scene integration can rely on these rules.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Progression.
// KEY RESPONSIBILITIES:
//   - Verify shops after two combat floors and three committed eligible hunter/curse choices.
//   - Check wallet, purchase, seed, restart and invalid-input behavior.
//   - Verify stock, automatic ward use, exhaustion and retained ownership.
// DEPENDENCIES:
//   - Core contracts, Session Progression, NUnit and Unity asset allocation.
// USAGE NOTES:
//   Pure controller tests use a temporary default Config and seeded randomness.
//   They do not assemble scenes or imply generated navigation has been verified.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Progression;

namespace Worsen.Tests.Progression
{
    public sealed class ProgressionSessionControllerTests
    {
        private ProgressionConfig config;
        private ProgressionSessionBehaviorState state;
        private ProgressionSessionController controller;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<ProgressionConfig>();
            state = new ProgressionSessionBehaviorState();
            controller = new ProgressionSessionController(state, config, new System.Random(731));
            controller.StartRun(731);
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(config);
        private int Revision => controller.Snapshot().Revision;

        [Test]
        public void ShopsFollowTwoCompletedCombatFloorsAndNeverCountShopVisits()
        {
            int combats = 0;
            for (int round = 1; round <= 15; round++)
            {
                bool shop = round % 3 == 0;
                Assert.That(controller.Snapshot().Round, Is.EqualTo(round));
                Assert.That(state.CompletedCombatFloors, Is.EqualTo(combats));
                if (shop)
                {
                    Assert.That(controller.GenerationRequest().IsShop, Is.True);
                    Assert.That(controller.GenerationRequest().Effects.ActiveThreatBudget, Is.Zero);
                    Assert.That(controller.ContinueShop(Revision), Is.False, "Unready refuge cannot continue.");
                    Assert.That(controller.ConfirmFloorReady(state.GenerationId), Is.True);
                    int revision = Revision;
                    Assert.That(controller.ContinueShop(revision), Is.True);
                    Assert.That(controller.ContinueShop(revision), Is.False);
                    Assert.That(state.CompletedCombatFloors, Is.EqualTo(combats));
                }
                else
                {
                    int generation = OpenCombatFloor();
                    Assert.That(controller.GenerationRequest().IsShop, Is.False);
                    Assert.That(controller.CompleteFloor(generation), Is.True);
                    Assert.That(controller.CompleteFloor(generation), Is.False);
                    combats++;
                }
            }
            Assert.That(combats, Is.EqualTo(10));
        }

        [Test]
        public void CursesAreUniqueOfferedOnlyAndExhaustionStartsGeneration()
        {
            var chosen = new HashSet<string>();
            for (int combat = 0; combat < config.Curses.Count + 3; combat++)
            {
                SkipPendingShop();
                if (state.Phase == ProgressionPhase.ChooseThreat)
                    Assert.That(controller.ChooseThreat(controller.Snapshot().Choices[0].Id, Revision), Is.True);
                ProgressionSnapshot choices = controller.Snapshot();
                if (choices.Phase == ProgressionPhase.ChooseCurse)
                {
                    Assert.That(choices.Choices.Count, Is.InRange(1, 3));
                    foreach (var choice in choices.Choices) Assert.That(chosen.Contains(choice.Id), Is.False);
                    string id = choices.Choices[0].Id;
                    Assert.That(controller.ChooseCurse(id, Revision), Is.True);
                    Assert.That(chosen.Add(id), Is.True);
                }
                else Assert.That(choices.Phase, Is.EqualTo(ProgressionPhase.Generating));
                controller.ConfirmFloorReady(state.GenerationId);
                controller.CompleteFloor(state.GenerationId);
            }
            Assert.That(chosen.Count, Is.EqualTo(config.Curses.Count));
            Assert.That(state.CurseCount, Is.EqualTo(config.Curses.Count));
            foreach (var selection in controller.Snapshot().Retained)
                if (selection.Kind == ProgressionChoiceKind.Curse) Assert.That(selection.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnofferedAndPreviouslyCarriedCursesAreRejectedWithoutMutation()
        {
            controller.ChooseThreat(controller.Snapshot().Choices[0].Id, Revision);
            var snapshot = controller.Snapshot();
            string unoffered = null;
            foreach (var entry in config.Curses)
            {
                bool offered = false;
                foreach (var choice in snapshot.Choices) if (choice.Id == entry.Id) offered = true;
                if (!offered) { unoffered = entry.Id; break; }
            }
            Assert.That(unoffered, Is.Not.Null);
            Assert.That(controller.ChooseCurse(unoffered, Revision), Is.False);
            string selected = snapshot.Choices[0].Id;
            controller.ChooseCurse(selected, Revision);
            controller.ConfirmFloorReady(state.GenerationId);
            controller.CompleteFloor(state.GenerationId);
            controller.ChooseThreat(controller.Snapshot().Choices[0].Id, Revision);
            ProgressionTraits before = controller.Snapshot().Effects.Traits;
            Assert.That(controller.ChooseCurse(selected, Revision), Is.False);
            Assert.That(controller.Snapshot().Effects.Traits, Is.EqualTo(before));
            Assert.That(state.CurseCount, Is.EqualTo(1));
        }

        [Test]
        public void ChoiceClicksCannotApplyTwiceOrUseEarlierRevision()
        {
            int stale = Revision;
            string threat = controller.Snapshot().Choices[0].Id;
            Assert.That(controller.ChooseThreat(threat, stale), Is.True);
            Assert.That(controller.ChooseThreat(threat, stale), Is.False);
            string curse = controller.Snapshot().Choices[0].Id;
            Assert.That(controller.ChooseCurse(curse, stale), Is.False);
            int current = Revision;
            Assert.That(controller.ChooseCurse(curse, current), Is.True);
            Assert.That(controller.ChooseCurse(curse, current), Is.False);
            Assert.That(state.CurseCount, Is.EqualTo(1));
            Assert.That(state.ThreatCount, Is.EqualTo(1));
        }

        [Test]
        public void GenerationAndCompletionRequireCurrentIdentityAndPhase()
        {
            ChooseLoadout();
            int generation = state.GenerationId;
            Assert.That(controller.CompleteFloor(generation), Is.False);
            Assert.That(controller.ConfirmFloorReady(generation + 1), Is.False);
            Assert.That(controller.ConfirmFloorReady(generation), Is.True);
            Assert.That(controller.ConfirmFloorReady(generation), Is.False);
            Assert.That(controller.CompleteFloor(generation), Is.True);
            Assert.That(controller.CompleteFloor(generation), Is.False);
            ChooseLoadout();
            Assert.That(controller.ConfirmFloorReady(generation), Is.False);
            Assert.That(controller.RecordGoldenCollected(generation, 0), Is.False);
        }

        [Test]
        public void GoldenCakeCreditsAreUniquePerFloorAndRetained()
        {
            int generation = OpenCombatFloor();
            Assert.That(controller.RecordGoldenCollected(generation, 7), Is.True);
            Assert.That(controller.RecordGoldenCollected(generation, 7), Is.False);
            Assert.That(controller.RecordGoldenCollected(generation, -1), Is.False);
            Assert.That(controller.RecordGoldenCollected(generation + 1, 8), Is.False);
            controller.CompleteFloor(generation);
            generation = OpenCombatFloor();
            Assert.That(controller.RecordGoldenCollected(generation, 7), Is.True);
            Assert.That(state.Wallet, Is.EqualTo(2));
        }

        [Test]
        public void UniqueUpgradePurchasePersistsAcrossShopsAndAppliesOnce()
        {
            ReachFirstShop(20);
            int revision = Revision;
            Assert.That(controller.Purchase("felt-soles", revision), Is.True);
            Assert.That(controller.Snapshot().Effects.Traits.HasFlag(ProgressionTraits.FeltSoles), Is.True);
            Assert.That(controller.Purchase("felt-soles", revision), Is.False);
            Assert.That(controller.Purchase("felt-soles", Revision), Is.False);
            Assert.That(state.Wallet, Is.EqualTo(17));
            ReachNextShop();
            ProgressionOffer offer = Offer("felt-soles");
            Assert.That(offer.Purchased, Is.True);
            Assert.That(offer.StockRemaining, Is.Zero);
            Assert.That(offer.CanAfford, Is.False);
            Assert.That(offer.UnavailableReason, Does.Contain("Already owned"));
            Assert.That(controller.Purchase("felt-soles", Revision), Is.False);
            Assert.That(state.Wallet, Is.EqualTo(17));
        }

        [Test]
        public void AllFourUniqueItemsExposeTheirDistinctTraits()
        {
            ReachFirstShop(20);
            foreach (string id in new[] { "shuttered-lens", "felt-soles", "climber-wraps", "pilgrim-chalk" })
                Assert.That(controller.Purchase(id, Revision), Is.True);
            var traits = controller.Snapshot().Effects.Traits;
            foreach (var trait in new[] { ProgressionTraits.ShutteredLens, ProgressionTraits.FeltSoles,
                ProgressionTraits.ClimberWraps, ProgressionTraits.PilgrimChalk }) Assert.That(traits.HasFlag(trait), Is.True);
            Assert.That(state.Wallet, Is.EqualTo(8));
        }

        [Test]
        public void DressingRejectsFullHealthAndRestocksOnNextVisit()
        {
            ReachFirstShop(10);
            Assert.That(Offer("field-dressing").CanAfford, Is.False);
            Assert.That(Offer("field-dressing").UnavailableReason, Does.Contain("full"));
            Assert.That(controller.Purchase("field-dressing", Revision), Is.False);
            Assert.That(state.Wallet, Is.EqualTo(10));
            ReachNextShop(60f);
            Assert.That(controller.Purchase("field-dressing", Revision), Is.True);
            Assert.That(state.Health, Is.EqualTo(95f));
            Assert.That(controller.Purchase("field-dressing", Revision), Is.False);
            Assert.That(Offer("field-dressing").UnavailableReason, Does.Contain("Sold out"));
            ReachNextShop();
            Assert.That(Offer("field-dressing").StockRemaining, Is.EqualTo(1));
            Assert.That(controller.Purchase("field-dressing", Revision), Is.True);
            Assert.That(state.Health, Is.EqualTo(100f));
            Assert.That(state.Wallet, Is.EqualTo(6));
        }

        [Test]
        public void WardCarriesOneChargeConsumesOnceAndRejectsWrongGeneration()
        {
            ReachFirstShop(12);
            Assert.That(controller.Purchase("wax-ward", Revision), Is.True);
            Assert.That(controller.TryConsumeWaxWard(state.GenerationId), Is.False, "Shop has no eligible grabs.");
            ReachNextShop();
            Assert.That(Offer("wax-ward").UnavailableReason, Does.Contain("already carry"));
            Assert.That(controller.Purchase("wax-ward", Revision), Is.False);
            controller.ContinueShop(Revision);
            int generation = OpenCombatFloor();
            Assert.That(controller.TryConsumeWaxWard(generation - 1), Is.False);
            Assert.That(controller.TryConsumeWaxWard(generation), Is.True);
            Assert.That(controller.TryConsumeWaxWard(generation), Is.False);
            Assert.That(controller.Snapshot().Effects.WaxWardCharges, Is.Zero);
            controller.CompleteFloor(generation);
            controller.CompleteFloor(OpenCombatFloor());
            controller.ConfirmFloorReady(state.GenerationId);
            Assert.That(controller.Purchase("wax-ward", Revision), Is.True);
            Assert.That(state.WaxWardCharges, Is.EqualTo(1));
        }

        [Test]
        public void UnaffordableAndUnknownOffersDoNotDebitAndAllowContinue()
        {
            ReachFirstShop(1, 60f);
            int revision = Revision;
            Assert.That(controller.Purchase("field-dressing", revision), Is.False);
            Assert.That(Revision, Is.GreaterThan(revision));
            Assert.That(controller.Snapshot().Message, Does.Contain("Not enough"));
            Assert.That(controller.Purchase("missing", Revision), Is.False);
            Assert.That(state.Wallet, Is.EqualTo(1));
            Assert.That(controller.ContinueShop(Revision), Is.True);
            Assert.That(state.Round, Is.EqualTo(4));
        }

        [Test]
        public void HealthCarriesBetweenFloorsAndGenerationReportsCannotHeal()
        {
            int generation = OpenCombatFloor();
            controller.RecordHealth(generation, 55f);
            controller.CompleteFloor(generation);
            ChooseLoadout();
            Assert.That(state.Health, Is.EqualTo(55f));
            Assert.That(controller.RecordHealth(state.GenerationId, 100f), Is.False);
            Assert.That(state.Health, Is.EqualTo(55f));
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidHealthReportsCannotCorruptRun(float health)
        {
            int generation = OpenCombatFloor();
            Assert.That(controller.RecordHealth(generation, health), Is.False);
            Assert.That(state.Health, Is.EqualTo(100f));
        }

        [Test]
        public void DeathEndsOnceClearsWalletAndRejectsFurtherGameplay()
        {
            int generation = OpenCombatFloor();
            controller.RecordGoldenCollected(generation, 1);
            Assert.That(controller.RecordHealth(generation, 0f), Is.True);
            Assert.That(state.Phase, Is.EqualTo(ProgressionPhase.Ended));
            Assert.That(state.Wallet, Is.Zero);
            Assert.That(controller.EndRun(generation), Is.False);
            Assert.That(controller.CompleteFloor(generation), Is.False);
            Assert.That(controller.RecordGoldenCollected(generation, 2), Is.False);
        }

        [Test]
        public void RestartClearsOwnershipWardHealthAndKeepsIdentitiesMonotonic()
        {
            int firstGeneration = OpenCombatFloor();
            int firstSeed = state.RoundSeed;
            for (int i = 0; i < 20; i++) controller.RecordGoldenCollected(firstGeneration, i);
            controller.CompleteFloor(firstGeneration);
            controller.CompleteFloor(OpenCombatFloor());
            controller.ConfirmFloorReady(state.GenerationId);
            controller.Purchase("felt-soles", Revision);
            controller.Purchase("wax-ward", Revision);
            controller.ContinueShop(Revision);
            int oldGeneration = OpenCombatFloor();
            controller.EndRun(oldGeneration);
            int previousRevision = Revision;
            controller = new ProgressionSessionController(state, config, new System.Random(731));
            controller.StartRun(731);
            Assert.That(Revision, Is.GreaterThan(previousRevision));
            Assert.That(state.Round, Is.EqualTo(1));
            Assert.That(state.CompletedCombatFloors, Is.Zero);
            Assert.That(controller.Snapshot().Retained, Is.Empty);
            Assert.That(state.Traits, Is.EqualTo(ProgressionTraits.None));
            Assert.That(state.WaxWardCharges, Is.Zero);
            Assert.That(state.Wallet, Is.Zero);
            Assert.That(state.Health, Is.EqualTo(100f));
            ChooseLoadout();
            Assert.That(state.GenerationId, Is.GreaterThan(oldGeneration));
            Assert.That(state.RoundSeed, Is.EqualTo(firstSeed));
            Assert.That(controller.ConfirmFloorReady(oldGeneration), Is.False);
        }

        [Test]
        public void MatchingSeedKeepsGenerationSeedsIndependentOfMenuQueriesAndPurchases()
        {
            var other = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(731));
            other.StartRun(731);
            for (int round = 1; round <= 30; round++)
            {
                foreach (var candidate in new[] { controller, other })
                {
                    if (candidate.Snapshot().Phase == ProgressionPhase.ChooseThreat)
                        candidate.ChooseThreat(candidate.Snapshot().Choices[0].Id, candidate.Snapshot().Revision);
                    if (candidate.Snapshot().Phase == ProgressionPhase.ChooseCurse)
                        candidate.ChooseCurse(candidate.Snapshot().Choices[0].Id, candidate.Snapshot().Revision);
                    candidate.ConfirmFloorReady(candidate.GenerationRequest().GenerationId);
                }
                Assert.That(controller.GenerationRequest().Seed, Is.EqualTo(other.GenerationRequest().Seed));
                if (controller.Snapshot().Phase == ProgressionPhase.Shop)
                {
                    controller.Purchase("wax-ward", Revision);
                    controller.Purchase("field-dressing", Revision);
                    controller.ContinueShop(Revision);
                    other.ContinueShop(other.Snapshot().Revision);
                }
                else
                {
                    for (int anchor = 0; anchor < 4; anchor++) controller.RecordGoldenCollected(state.GenerationId, anchor);
                    controller.CompleteFloor(state.GenerationId);
                    other.CompleteFloor(other.GenerationRequest().GenerationId);
                }
            }
        }

        [Test]
        public void GenerationFailureCannotAdmitIncompleteFloor()
        {
            ChooseLoadout();
            int generation = state.GenerationId;
            Assert.That(controller.FailGeneration(generation, "Blocked exit"), Is.True);
            Assert.That(state.Phase, Is.EqualTo(ProgressionPhase.GenerationFailed));
            Assert.That(controller.Snapshot().Message, Does.Contain("Blocked exit"));
            Assert.That(controller.Snapshot().CanRestart, Is.True);
            Assert.That(controller.ConfirmFloorReady(generation), Is.False);
            Assert.That(controller.CompleteFloor(generation), Is.False);
        }

        [Test]
        public void SnapshotsAreImmutableAndDoNotChangeAfterSelection()
        {
            ProgressionSnapshot before = controller.Snapshot();
            var choices = (IList<ProgressionChoice>)before.Choices;
            Assert.Throws<NotSupportedException>(() => choices[0] = default);
            controller.ChooseThreat(controller.Snapshot().Choices[0].Id, Revision);
            Assert.That(before.Choices[0].SelectedCount, Is.Zero);
            Assert.That(before.Retained, Is.Empty);
            Assert.That(controller.Snapshot().Retained[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void HundredRoundRunRemainsBoundedAfterCurseExhaustion()
        {
            for (int round = 0; round < 100; round++)
            {
                if (state.Phase == ProgressionPhase.Generating && state.IsShop) SkipPendingShop();
                int generation = OpenCombatFloor("rusher");
                controller.CompleteFloor(generation);
            }
            Assert.That(state.CurseCount, Is.LessThanOrEqualTo(config.Curses.Count));
            Assert.That(state.HunterSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(float.IsNaN(state.Health), Is.False);
            Assert.That(controller.Snapshot().Effects.ActiveThreatBudget, Is.LessThanOrEqualTo(config.MaximumActiveThreats));
        }

        [TestCase("_shopInterval", 0)] [TestCase("_goldenCakeValue", -1)] [TestCase("_maximumActiveThreats", 0)]
        public void InvalidConfigurationRejectedBeforeRun(string field, int value)
        {
            SetConfigField(field, value);
            Assert.Throws<ArgumentException>(() => new ProgressionSessionController(state, config, new System.Random(1)));
        }
        [Test]
        public void DuplicateCatalogIdsInvalidPricesAndStockAreRejected()
        {
            foreach (var entry in new[] {
                new ProgressionEntryConfig("watcher", "DUPLICATE", "Shared identity"),
                new ProgressionEntryConfig("bad", "BAD", "Invalid price", price: -1),
                new ProgressionEntryConfig("bad", "BAD", "Invalid stock", stockPerVisit: 0),
                new ProgressionEntryConfig("bad", "BAD", "Invalid multiplier", flashlightRangeMultiplier: float.NaN) })
            {
                SetConfigField("_offers", new[] { entry });
                Assert.Throws<ArgumentException>(() => new ProgressionSessionController(state, config, new System.Random(1)));
            }
        }

        [Test]
        public void ThreatRosterIsUniqueAndSnapshotsRetainActualSelectedIdentities()
        {
            StartOffering("hexer");
            Assert.That(controller.ChooseThreat("hexer", Revision), Is.True);
            var before = controller.Snapshot();
            controller.ChooseCurse(before.Choices[0].Id, Revision);
            controller.ConfirmFloorReady(state.GenerationId);
            controller.CompleteFloor(state.GenerationId);
            foreach (var choice in controller.Snapshot().Choices) Assert.That(choice.Id, Is.Not.EqualTo("hexer"));
            Assert.That(controller.ChooseThreat("hexer", Revision), Is.False);
            string second = controller.Snapshot().Choices[0].Id;
            Assert.That(controller.ChooseThreat(second, Revision), Is.True);
            Assert.That(controller.Snapshot().Effects.ActiveThreatIds, Is.EqualTo(new[] { "hexer", second }));
            Assert.That(before.Effects.ActiveThreatIds, Is.EqualTo(new[] { "hexer" }));
            var roster = (IList<string>)before.Effects.ActiveThreatIds;
            Assert.Throws<NotSupportedException>(() => roster[0] = "rusher");
        }

        [Test]
        public void EveryHunterHasThreeRelatedCursesAndMenusMixEligibleGeneralChoices()
        {
            foreach (var threat in config.Threats)
            {
                int related = 0;
                foreach (var curse in config.Curses) if (curse.RequiredThreatId == threat.Id) related++;
                Assert.That(related, Is.EqualTo(3), threat.Id);
            }
            StartOffering("hexer");
            Assert.That(controller.ChooseThreat("hexer", Revision), Is.True);
            bool hasGeneral = false, hasRelated = false;
            foreach (var choice in controller.Snapshot().Choices)
                foreach (var curse in config.Curses)
                    if (curse.Id == choice.Id)
                    {
                        if (string.IsNullOrEmpty(curse.RequiredThreatId)) hasGeneral = true;
                        else { Assert.That(curse.RequiredThreatId, Is.EqualTo("hexer")); hasRelated = true; }
                    }
            Assert.That(hasGeneral && hasRelated, Is.True);
        }

        [Test]
        public void SmallerThreatCapSkipsNoOpChoicesAndNeverOffersUnspawnedHunterCurses()
        {
            SetConfigField("_maximumActiveThreats", 1);
            StartOffering("hexer");
            int generation = OpenCombatFloor("hexer");
            Assert.That(controller.CompleteFloor(generation), Is.True);
            Assert.That(state.Phase, Is.EqualTo(ProgressionPhase.ChooseCurse));
            Assert.That(controller.ChooseThreat("thorncaller", Revision), Is.False);
            foreach (var choice in controller.Snapshot().Choices)
                foreach (var curse in config.Curses)
                    if (curse.Id == choice.Id && !string.IsNullOrEmpty(curse.RequiredThreatId))
                        Assert.That(curse.RequiredThreatId, Is.EqualTo("hexer"));
            Assert.That(controller.Snapshot().Effects.ActiveThreatIds, Is.EqualTo(new[] { "hexer" }));
        }

        [Test]
        public void MissingRequiredHunterFailsCatalogValidation()
        {
            SetConfigField("_curses", new[] { new ProgressionEntryConfig("bad", "BAD", "Missing hunter", requiredThreatId: "missing") });
            Assert.Throws<ArgumentException>(() => new ProgressionSessionController(state, config, new System.Random(1)));
        }

        [Test]
        public void HunterMenusAreCommittedThreeChoicesAndRejectUnofferedIds()
        {
            var first = controller.Snapshot();
            string[] ids = first.Choices.Select(choice => choice.Id).ToArray();
            Assert.That(ids.Length, Is.EqualTo(3));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(3));
            string hidden = config.Threats.First(entry => !ids.Contains(entry.Id)).Id;
            int roundSeed = controller.GenerationRequest().Seed;
            Assert.That(controller.ChooseThreat(hidden, first.Revision), Is.False);
            Assert.That(controller.Snapshot().Effects.ActiveThreatIds, Is.Empty);
            Assert.That(controller.Snapshot().Retained, Is.Empty);
            Assert.That(controller.Snapshot().Choices.Select(choice => choice.Id), Is.EqualTo(ids));
            Assert.That(controller.GenerationRequest().Seed, Is.EqualTo(roundSeed));
            Assert.That(controller.ChooseThreat(ids[0], first.Revision), Is.False, "Rejected click invalidates stale revision.");
            Assert.That(controller.ChooseThreat(ids[0], Revision), Is.True);
        }

        [Test]
        public void InjectedSeedDeterminesStableMenusAndFullRosterRemainsAvailableAcrossRuns()
        {
            var seen = new HashSet<string>();
            var menus = new HashSet<string>();
            for (int seed = 0; seed < 32; seed++)
            {
                var left = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(seed));
                var right = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(seed));
                left.StartRun(seed); right.StartRun(seed);
                string[] ids = left.Snapshot().Choices.Select(choice => choice.Id).ToArray();
                Assert.That(ids.Length, Is.EqualTo(3));
                Assert.That(right.Snapshot().Choices.Select(choice => choice.Id), Is.EqualTo(ids));
                for (int read = 0; read < 5; read++) Assert.That(left.Snapshot().Choices.Select(choice => choice.Id), Is.EqualTo(ids));
                Assert.That(left.GenerationRequest().Seed, Is.EqualTo(new System.Random(seed).Next()), "Offers must not consume a second random draw.");
                foreach (string id in ids)
                {
                    seen.Add(id);
                    var branch = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(seed));
                    branch.StartRun(seed);
                    Assert.That(branch.ChooseThreat(id, branch.Snapshot().Revision), Is.True, "Every displayed hunter must be selectable.");
                    Assert.That(branch.Snapshot().Effects.ActiveThreatIds, Is.EqualTo(new[] { id }));
                    foreach (var curse in branch.Snapshot().Choices)
                    {
                        var curseBranch = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(seed));
                        curseBranch.StartRun(seed);
                        Assert.That(curseBranch.ChooseThreat(id, curseBranch.Snapshot().Revision), Is.True);
                        Assert.That(curseBranch.ChooseCurse(curse.Id, curseBranch.Snapshot().Revision), Is.True, "Every displayed curse must be selectable.");
                    }
                }
                menus.Add(string.Join(",", ids));
            }
            Assert.That(seen, Is.EquivalentTo(config.Threats.Select(entry => entry.Id)));
            Assert.That(menus.Count, Is.GreaterThan(1));
            Assert.That(config.Threats.Count, Is.EqualTo(5));
            Assert.That(config.Curses.Count, Is.EqualTo(22));
            Assert.That(config.Offers.Count, Is.EqualTo(6), "The shop catalog remains unchanged.");
        }

        [TestCase(1)] [TestCase(29)] [TestCase(731)]
        public void ThreeChoiceMenusRetainEligibilityMixAndExhaustEveryHunterAndCurse(int seed)
        {
            state = new ProgressionSessionBehaviorState();
            controller = new ProgressionSessionController(state, config, new System.Random(seed));
            controller.StartRun(seed);
            var hunters = new HashSet<string>(); var curses = new HashSet<string>();
            for (int combat = 0; combat < config.Curses.Count + 2; combat++)
            {
                SkipPendingShop();
                string added = null;
                if (state.Phase == ProgressionPhase.ChooseThreat)
                {
                    var offered = controller.Snapshot().Choices;
                    Assert.That(offered.Count, Is.EqualTo(Math.Min(3, config.Threats.Count - hunters.Count)));
                    Assert.That(offered.Select(choice => choice.Id).Distinct().Count(), Is.EqualTo(offered.Count));
                    foreach (var choice in offered) Assert.That(hunters.Contains(choice.Id), Is.False);
                    added = offered[0].Id;
                    Assert.That(controller.ChooseThreat(added, Revision), Is.True); hunters.Add(added);
                }
                var eligible = config.Curses.Where(entry => !curses.Contains(entry.Id) &&
                    (string.IsNullOrEmpty(entry.RequiredThreatId) || hunters.Contains(entry.RequiredThreatId))).ToArray();
                if (eligible.Length > 0)
                {
                    Assert.That(state.Phase, Is.EqualTo(ProgressionPhase.ChooseCurse));
                    var offered = controller.Snapshot().Choices;
                    Assert.That(offered.Count, Is.EqualTo(Math.Min(3, eligible.Length)));
                    Assert.That(offered.Select(choice => choice.Id).Distinct().Count(), Is.EqualTo(offered.Count));
                    foreach (var choice in offered) Assert.That(eligible.Any(entry => entry.Id == choice.Id), Is.True);
                    if (eligible.Any(entry => string.IsNullOrEmpty(entry.RequiredThreatId)))
                        Assert.That(offered.Any(choice => eligible.Any(entry => entry.Id == choice.Id && string.IsNullOrEmpty(entry.RequiredThreatId))), Is.True);
                    if (added != null && eligible.Any(entry => entry.RequiredThreatId == added))
                        Assert.That(offered.Any(choice => eligible.Any(entry => entry.Id == choice.Id && entry.RequiredThreatId == added)), Is.True);
                    string selected = offered[0].Id;
                    Assert.That(controller.ChooseCurse(selected, Revision), Is.True); curses.Add(selected);
                }
                Assert.That(state.Phase, Is.EqualTo(ProgressionPhase.Generating));
                Assert.That(controller.ConfirmFloorReady(state.GenerationId), Is.True);
                Assert.That(controller.CompleteFloor(state.GenerationId), Is.True);
            }
            Assert.That(hunters.Count, Is.EqualTo(5));
            Assert.That(curses.Count, Is.EqualTo(22));
        }

        private void StartOffering(string threat)
        {
            for (int seed = 0; seed < 100; seed++)
            {
                state = new ProgressionSessionBehaviorState();
                controller = new ProgressionSessionController(state, config, new System.Random(seed));
                controller.StartRun(seed);
                foreach (var choice in controller.Snapshot().Choices) if (choice.Id == threat) return;
            }
            Assert.Fail("No deterministic test seed offered " + threat);
        }

        private void ChooseLoadout(string threat = "watcher")
        {
            if (state.Phase == ProgressionPhase.ChooseThreat)
            {
                string selected = controller.Snapshot().Choices[0].Id;
                foreach (var choice in controller.Snapshot().Choices) if (choice.Id == threat) selected = threat;
                Assert.That(controller.ChooseThreat(selected, Revision), Is.True);
            }
            if (state.Phase == ProgressionPhase.ChooseCurse)
                Assert.That(controller.ChooseCurse(controller.Snapshot().Choices[0].Id, Revision), Is.True);
            Assert.That(state.Phase, Is.EqualTo(ProgressionPhase.Generating));
        }
        private int OpenCombatFloor(string threat = "watcher")
        {
            ChooseLoadout(threat);
            int generation = state.GenerationId;
            Assert.That(controller.ConfirmFloorReady(generation), Is.True);
            return generation;
        }
        private void SkipPendingShop()
        {
            if (!state.IsShop) return;
            if (state.Phase == ProgressionPhase.Generating) controller.ConfirmFloorReady(state.GenerationId);
            Assert.That(controller.ContinueShop(Revision), Is.True);
        }
        private void ReachFirstShop(int credits, float health = 100f)
        {
            for (int combat = 0; combat < 2; combat++)
            {
                int generation = OpenCombatFloor();
                if (combat == 0)
                    for (int anchor = 0; anchor < credits; anchor++) controller.RecordGoldenCollected(generation, anchor);
                controller.RecordHealth(generation, health);
                controller.CompleteFloor(generation);
            }
            Assert.That(controller.ConfirmFloorReady(state.GenerationId), Is.True);
        }
        private void ReachNextShop(float health = -1f)
        {
            Assert.That(controller.ContinueShop(Revision), Is.True);
            for (int combat = 0; combat < 2; combat++)
            {
                int generation = OpenCombatFloor();
                if (health >= 0f) controller.RecordHealth(generation, health);
                controller.CompleteFloor(generation);
            }
            Assert.That(controller.ConfirmFloorReady(state.GenerationId), Is.True);
        }
        private ProgressionOffer Offer(string id)
        {
            foreach (var offer in controller.Snapshot().Offers) if (offer.Id == id) return offer;
            throw new InvalidOperationException("Missing test offer " + id);
        }
        private void SetConfigField(string name, object value) => typeof(ProgressionConfig)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, value);
    }
}
