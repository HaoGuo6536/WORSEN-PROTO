// ============================================================================
// ProgressionSessionControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the expedition independently of generated geometry and frame time.
//   The tests cover a nine-floor run, economic replay protection, retained
//   effects and stale callbacks so scene integration can rely on these rules.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Progression.
// KEY RESPONSIBILITIES:
//   - Verify shops on floors four/eight and playable combat floors around them.
//   - Check wallet, purchase, seed, restart and invalid-input behavior.
//   - Verify capped threat copy without altering retained effects or selection flow.
// DEPENDENCIES:
//   - Core contracts, Session Progression, NUnit and Unity asset allocation.
// USAGE NOTES:
//   Pure controller tests use a temporary default Config and seeded randomness.
//   They do not assemble scenes or imply generated navigation has been verified.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
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

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [Test]
        public void RoundsOneThroughNineIncludeTwoSafeShopsAndRetainSelections()
        {
            int combatFloors = 0;
            int shops = 0;
            for (int round = 1; round <= 9; round++)
            {
                Assert.That(controller.Snapshot().Round, Is.EqualTo(round));
                bool shop = round == 4 || round == 8;
                if (shop)
                {
                    shops++;
                    Assert.That(controller.Snapshot().Phase, Is.EqualTo(ProgressionPhase.Generating));
                    Assert.That(controller.Snapshot().CanContinue, Is.False);
                    Assert.That(controller.Snapshot().Choices, Is.Empty);
                    Assert.That(controller.GenerationRequest().IsShop, Is.True);
                    Assert.That(controller.GenerationRequest().Effects.ActiveThreatBudget, Is.Zero);
                }
                else
                {
                    combatFloors++;
                    ChooseLoadout();
                    Assert.That(controller.GenerationRequest().IsShop, Is.False);
                    Assert.That(controller.GenerationRequest().Effects.ActiveThreatBudget, Is.EqualTo(Math.Min(combatFloors, 3)));
                }
                int generation = controller.GenerationRequest().GenerationId;
                Assert.That(controller.ConfirmFloorReady(generation), Is.True);
                Assert.That(controller.Snapshot().Phase, Is.EqualTo(shop ? ProgressionPhase.Shop : ProgressionPhase.Exploring));
                Assert.That(controller.Snapshot().ThreatCount, Is.EqualTo(combatFloors));
                Assert.That(controller.Snapshot().CurseCount, Is.EqualTo(combatFloors));
                if (round < 9)
                {
                    if (shop) Assert.That(controller.ContinueShop(Revision), Is.True);
                    else Assert.That(controller.CompleteFloor(generation), Is.True);
                }
            }
            Assert.That(shops, Is.EqualTo(2));
            Assert.That(combatFloors, Is.EqualTo(7));
        }

        [Test]
        public void ThreatDescriptionsChangeAfterThirdHunterAndResetWithTheRun()
        {
            for (int round = 1; round <= 3; round++)
            {
                ProgressionSnapshot before = controller.Snapshot();
                Assert.That(before.Round, Is.EqualTo(round));
                for (int index = 0; index < config.Threats.Count; index++)
                    Assert.That(before.Choices[index].Description, Is.EqualTo(config.Threats[index].Description));
                int generation = OpenCombatFloor();
                Assert.That(controller.CompleteFloor(generation), Is.True);
            }
            Assert.That(controller.Snapshot().Round, Is.EqualTo(4));
            Assert.That(controller.Snapshot().Choices, Is.Empty);
            Assert.That(controller.GenerationRequest().Effects.ActiveThreatBudget, Is.Zero);
            Assert.That(controller.ConfirmFloorReady(controller.GenerationRequest().GenerationId), Is.True);
            for (int index = 0; index < config.Offers.Count; index++)
                Assert.That(controller.Snapshot().Offers[index].Description, Is.EqualTo(config.Offers[index].Description));
            Assert.That(controller.ContinueShop(Revision), Is.True);

            ProgressionSnapshot capped = controller.Snapshot();
            Assert.That(capped.Round, Is.EqualTo(5));
            Assert.That(capped.Effects.ActiveThreatBudget, Is.EqualTo(3));
            Assert.That(capped.Choices[0].Description, Does.Contain("no additional hunter or other effect"));
            Assert.That(capped.Choices[1].Description, Does.StartWith("No additional hunter.").And.Contain("8% faster"));
            Assert.That(capped.Choices[2].Description, Does.StartWith("No additional hunter.").And.Contain("4% slower").And.Contain("12%"));
            Assert.That(controller.ChooseThreat("watcher", Revision), Is.True);
            for (int index = 0; index < config.Curses.Count; index++)
                Assert.That(controller.Snapshot().Choices[index].Description, Is.EqualTo(config.Curses[index].Description));
            Assert.That(controller.ChooseCurse("restless", Revision), Is.True);
            int fifthGeneration = controller.GenerationRequest().GenerationId;
            Assert.That(controller.ConfirmFloorReady(fifthGeneration), Is.True);
            Assert.That(controller.CompleteFloor(fifthGeneration), Is.True);
            Assert.That(controller.Snapshot().ThreatCount, Is.EqualTo(4));
            Assert.That(controller.Snapshot().Choices[0].Description, Is.EqualTo(capped.Choices[0].Description));

            controller.StartRun(731);
            Assert.That(controller.Snapshot().ThreatCount, Is.Zero);
            Assert.That(controller.Snapshot().Choices[0].Description, Is.EqualTo(config.Threats[0].Description));
            Assert.That(capped.Choices[0].Description, Does.Contain("no additional hunter or other effect"));
        }

        [TestCase("watcher", 1f, 1f)]
        [TestCase("rusher", 1.08f, 1f)]
        [TestCase("lurker", 0.96f, 1.12f)]
        public void CappedThreatChoicesRetainTheirExistingEffectsAndDoNotAddHunters(string id, float hunterFactor, float fogFactor)
        {
            ReachFirstShop(0);
            Assert.That(controller.ContinueShop(Revision), Is.True);
            ProgressionSnapshot before = controller.Snapshot();
            int revision = before.Revision;
            Assert.That(controller.ChooseThreat(id, revision), Is.True);
            Assert.That(controller.ChooseThreat(id, revision), Is.False);
            ProgressionSnapshot after = controller.Snapshot();
            Assert.That(after.Phase, Is.EqualTo(ProgressionPhase.ChooseCurse));
            Assert.That(after.ThreatCount, Is.EqualTo(4));
            Assert.That(after.Effects.ActiveThreatBudget, Is.EqualTo(3));
            Assert.That(after.Effects.HunterSpeedMultiplier, Is.EqualTo(before.Effects.HunterSpeedMultiplier * hunterFactor).Within(0.0001f));
            Assert.That(after.Effects.FogDensityMultiplier, Is.EqualTo(before.Effects.FogDensityMultiplier * fogFactor).Within(0.0001f));
            Assert.That(after.Effects.MovementSpeedMultiplier, Is.EqualTo(before.Effects.MovementSpeedMultiplier));
            Assert.That(after.Effects.FlashlightRangeMultiplier, Is.EqualTo(before.Effects.FlashlightRangeMultiplier));
            Assert.That(after.Health, Is.EqualTo(before.Health));
            Assert.That(after.MaxHealth, Is.EqualTo(before.MaxHealth));
            Assert.That(after.Wallet, Is.EqualTo(before.Wallet));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void CustomThreatWithoutCappedCopyCannotPromiseAnotherHunter(string cappedDescription)
        {
            SetConfigField("_maximumActiveThreats", 1);
            SetConfigField("_threats", new[] { new ProgressionEntryConfig("watcher", "WATCHER", "A hunter joins.", descriptionAtThreatCap: cappedDescription) });
            Assert.That(controller.Snapshot().Choices[0].Description, Is.EqualTo("A hunter joins."));
            int generation = OpenCombatFloor();
            Assert.That(controller.CompleteFloor(generation), Is.True);
            Assert.That(controller.Snapshot().Round, Is.EqualTo(2));
            Assert.That(controller.Snapshot().Choices[0].Description, Is.EqualTo("Hunter limit reached. No additional hunter joins."));
            Assert.That(controller.ChooseThreat("watcher", Revision), Is.True);
            Assert.That(controller.Snapshot().Effects.ActiveThreatBudget, Is.EqualTo(1));
        }

        [Test]
        public void ChoiceClicksCannotApplyTwiceOrUseAnEarlierRevision()
        {
            int threatRevision = Revision;
            Assert.That(controller.ChooseThreat("rusher", threatRevision), Is.True);
            Assert.That(controller.ChooseThreat("rusher", threatRevision), Is.False);
            Assert.That(controller.ChooseCurse("frailty", threatRevision), Is.False);
            int curseRevision = Revision;
            Assert.That(controller.ChooseCurse("frailty", curseRevision), Is.True);
            Assert.That(controller.ChooseCurse("frailty", curseRevision), Is.False);
            Assert.That(controller.Snapshot().ThreatCount, Is.EqualTo(1));
            Assert.That(controller.Snapshot().CurseCount, Is.EqualTo(1));
            Assert.That(controller.Snapshot().MaxHealth, Is.EqualTo(90f));
        }

        [Test]
        public void GenerationAndCompletionRequireTheCurrentIdentityAndPhase()
        {
            ChooseLoadout();
            int generation = controller.GenerationRequest().GenerationId;
            Assert.That(controller.CompleteFloor(generation), Is.False, "Unvalidated floor cannot complete.");
            Assert.That(controller.ConfirmFloorReady(generation + 1), Is.False);
            Assert.That(controller.ConfirmFloorReady(generation), Is.True);
            Assert.That(controller.ConfirmFloorReady(generation), Is.False);
            Assert.That(controller.CompleteFloor(generation), Is.True);
            Assert.That(controller.CompleteFloor(generation), Is.False);
            ChooseLoadout();
            Assert.That(controller.ConfirmFloorReady(generation), Is.False, "Old geometry cannot acknowledge the replacement.");
            Assert.That(controller.RecordGoldenCollected(generation, 0), Is.False);
        }

        [Test]
        public void GoldenCakeCreditsAreUniquePerFloorAndRetainedAcrossFloors()
        {
            int generation = OpenCombatFloor();
            Assert.That(controller.RecordGoldenCollected(generation, 7), Is.True);
            Assert.That(controller.RecordGoldenCollected(generation, 7), Is.False);
            Assert.That(controller.RecordGoldenCollected(generation, -1), Is.False);
            Assert.That(controller.RecordGoldenCollected(generation + 1, 8), Is.False);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(1));
            controller.CompleteFloor(generation);
            generation = OpenCombatFloor();
            Assert.That(controller.RecordGoldenCollected(generation, 7), Is.True, "Anchor identities may be reused by the next floor.");
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(2));
        }

        [Test]
        public void ShopDebitsOnceAndAppliesFunctionalUpgradeEffects()
        {
            ReachFirstShop(12, 40f);
            int purchaseRevision = Revision;
            Assert.That(controller.Purchase("medkit", purchaseRevision), Is.True);
            Assert.That(controller.Snapshot().Health, Is.EqualTo(75f));
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(9));
            Assert.That(controller.Purchase("medkit", purchaseRevision), Is.False);
            Assert.That(controller.Purchase("medkit", Revision), Is.False);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(9));
            Assert.That(controller.Purchase("running-shoes", Revision), Is.True);
            Assert.That(controller.Purchase("focus-lens", Revision), Is.True);
            Assert.That(controller.Snapshot().Wallet, Is.Zero);
            Assert.That(controller.Snapshot().Effects.MovementSpeedMultiplier, Is.EqualTo(1.08f).Within(0.0001f));
            Assert.That(controller.Snapshot().Effects.FlashlightRangeMultiplier, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(controller.Snapshot().Offers[0].Purchased, Is.True);
            Assert.That(controller.Snapshot().Offers[0].CanAfford, Is.False);
        }

        [Test]
        public void UnaffordableAndUnknownOffersDoNotDebitAndExplainFailure()
        {
            ReachFirstShop(2);
            int revision = Revision;
            Assert.That(controller.Purchase("medkit", revision), Is.False);
            Assert.That(Revision, Is.GreaterThan(revision), "Feedback is published as a new snapshot.");
            Assert.That(controller.Snapshot().Message, Does.Contain("Not enough"));
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(2));
            Assert.That(controller.Purchase("missing", Revision), Is.False);
            Assert.That(controller.Snapshot().Wallet, Is.EqualTo(2));
            Assert.That(controller.ContinueShop(Revision), Is.True, "Purchasing is optional.");
            Assert.That(controller.Snapshot().Round, Is.EqualTo(5));
        }

        [Test]
        public void ShopRequiresGenerationReadinessAndContinueIsIdempotent()
        {
            for (int i = 0; i < 3; i++) controller.CompleteFloor(OpenCombatFloor());
            Assert.That(controller.Snapshot().Round, Is.EqualTo(4));
            Assert.That(controller.Purchase("medkit", Revision), Is.False);
            Assert.That(controller.ContinueShop(Revision), Is.False);
            controller.ConfirmFloorReady(controller.GenerationRequest().GenerationId);
            int revision = Revision;
            Assert.That(controller.ContinueShop(revision), Is.True);
            Assert.That(controller.ContinueShop(revision), Is.False);
            Assert.That(controller.Snapshot().Round, Is.EqualTo(5));
        }

        [Test]
        public void HealthCarriesAcrossFloorsAndFrailtyClampsItWithoutHealing()
        {
            int generation = OpenCombatFloor();
            controller.RecordHealth(generation, 55f);
            controller.CompleteFloor(generation);
            ChooseLoadout("watcher", "frailty");
            Assert.That(controller.Snapshot().Health, Is.EqualTo(55f));
            Assert.That(controller.Snapshot().MaxHealth, Is.EqualTo(90f));
            Assert.That(controller.RecordHealth(controller.GenerationRequest().GenerationId, 100f), Is.False,
                "Spawn reset reports during generation must not overwrite retained health.");
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void InvalidHealthReportsCannotCorruptRun(float health)
        {
            int generation = OpenCombatFloor();
            Assert.That(controller.RecordHealth(generation, health), Is.False);
            Assert.That(controller.Snapshot().Health, Is.EqualTo(100f));
        }

        [Test]
        public void DeathEndsOnceClearsWalletAndRejectsFurtherGameplay()
        {
            int generation = OpenCombatFloor();
            controller.RecordGoldenCollected(generation, 1);
            Assert.That(controller.RecordHealth(generation, 0f), Is.True);
            Assert.That(controller.Snapshot().Phase, Is.EqualTo(ProgressionPhase.Ended));
            Assert.That(controller.Snapshot().Wallet, Is.Zero);
            Assert.That(controller.Snapshot().CanRestart, Is.True);
            Assert.That(controller.EndRun(generation), Is.False);
            Assert.That(controller.CompleteFloor(generation), Is.False);
            Assert.That(controller.RecordGoldenCollected(generation, 2), Is.False);
        }

        [Test]
        public void RestartClearsSelectionsWalletHealthAndPreservesReplayProtection()
        {
            int oldGeneration = OpenCombatFloor();
            int firstSeed = controller.GenerationRequest().Seed;
            controller.RecordGoldenCollected(oldGeneration, 1);
            controller.EndRun(oldGeneration);
            int previousRevision = Revision;
            controller = new ProgressionSessionController(state, config, new System.Random(731));
            controller.StartRun(731);
            Assert.That(Revision, Is.GreaterThan(previousRevision));
            Assert.That(controller.Snapshot().Round, Is.EqualTo(1));
            Assert.That(controller.Snapshot().Retained, Is.Empty);
            Assert.That(controller.Snapshot().Wallet, Is.Zero);
            Assert.That(controller.Snapshot().Health, Is.EqualTo(100f));
            ChooseLoadout();
            Assert.That(controller.GenerationRequest().GenerationId, Is.GreaterThan(oldGeneration));
            Assert.That(controller.GenerationRequest().Seed, Is.EqualTo(firstSeed));
            Assert.That(controller.ConfirmFloorReady(oldGeneration), Is.False);
        }

        [Test]
        public void MatchingSeedProducesMatchingRoundSeedsRegardlessOfPurchases()
        {
            var otherState = new ProgressionSessionBehaviorState();
            var other = new ProgressionSessionController(otherState, config, new System.Random(731));
            other.StartRun(731);
            for (int round = 1; round <= 9; round++)
            {
                foreach (ProgressionSessionController candidate in new[] { controller, other })
                {
                    if (round % 4 != 0)
                    {
                        candidate.ChooseThreat("watcher", candidate.Snapshot().Revision);
                        candidate.ChooseCurse("restless", candidate.Snapshot().Revision);
                    }
                    candidate.ConfirmFloorReady(candidate.GenerationRequest().GenerationId);
                }
                Assert.That(controller.GenerationRequest().Seed, Is.EqualTo(other.GenerationRequest().Seed));
                if (round % 4 == 0)
                {
                    controller.Purchase("medkit", Revision);
                    controller.ContinueShop(Revision);
                    other.ContinueShop(other.Snapshot().Revision);
                }
                else
                {
                    for (int anchor = 0; anchor < 4; anchor++) controller.RecordGoldenCollected(controller.GenerationRequest().GenerationId, anchor);
                    controller.CompleteFloor(controller.GenerationRequest().GenerationId);
                    other.CompleteFloor(other.GenerationRequest().GenerationId);
                }
            }
        }

        [Test]
        public void GenerationFailureCannotAdmitAnIncompleteFloor()
        {
            ChooseLoadout();
            int generation = controller.GenerationRequest().GenerationId;
            Assert.That(controller.FailGeneration(generation, "Blocked exit"), Is.True);
            Assert.That(controller.Snapshot().Phase, Is.EqualTo(ProgressionPhase.GenerationFailed));
            Assert.That(controller.Snapshot().Message, Does.Contain("Blocked exit"));
            Assert.That(controller.Snapshot().Message, Does.Contain(controller.GenerationRequest().Seed.ToString()));
            Assert.That(controller.Snapshot().CanRestart, Is.True);
            Assert.That(controller.ConfirmFloorReady(generation), Is.False);
            Assert.That(controller.CompleteFloor(generation), Is.False);
        }

        [Test]
        public void SnapshotsDoNotExposeMutableCollectionsOrChangeAfterSelection()
        {
            ProgressionSnapshot before = controller.Snapshot();
            var choices = (IList<ProgressionChoice>)before.Choices;
            Assert.Throws<NotSupportedException>(() => choices[0] = default);
            controller.ChooseThreat("watcher", Revision);
            Assert.That(before.Choices[0].SelectedCount, Is.Zero);
            Assert.That(before.Retained, Is.Empty);
            Assert.That(controller.Snapshot().Retained[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void DeepRunEffectsRemainFiniteAndBounded()
        {
            for (int round = 1; round <= 100; round++)
            {
                if (round % 4 == 0)
                {
                    controller.ConfirmFloorReady(controller.GenerationRequest().GenerationId);
                    controller.Purchase("running-shoes", Revision);
                    controller.ContinueShop(Revision);
                }
                else
                {
                    ChooseLoadout("rusher", round % 2 == 0 ? "frailty" : "fading-light");
                    int generation = controller.GenerationRequest().GenerationId;
                    controller.ConfirmFloorReady(generation);
                    for (int anchor = 0; anchor < 5; anchor++) controller.RecordGoldenCollected(generation, anchor);
                    controller.CompleteFloor(generation);
                }
            }
            ProgressionSnapshot snapshot = controller.Snapshot();
            Assert.That(snapshot.MaxHealth, Is.EqualTo(30f));
            Assert.That(snapshot.Effects.FlashlightRangeMultiplier, Is.EqualTo(0.4f));
            Assert.That(snapshot.Effects.HunterSpeedMultiplier, Is.EqualTo(1.5f));
            Assert.That(snapshot.Effects.MovementSpeedMultiplier, Is.EqualTo(1.6f));
        }

        [TestCase("_shopInterval", 0)]
        [TestCase("_goldenCakeValue", -1)]
        [TestCase("_maximumActiveThreats", 0)]
        public void InvalidConfigurationIsRejectedBeforeRun(string field, int value)
        {
            SetConfigField(field, value);
            Assert.Throws<ArgumentException>(() => new ProgressionSessionController(state, config, new System.Random(1)));
        }

        [Test]
        public void InvalidCadenceAndDuplicateCatalogIdsAreRejectedBeforeRun()
        {
            SetConfigField("_shopInterval", 0);
            Assert.Throws<ArgumentException>(() => new ProgressionSessionController(state, config, new System.Random(1)));
            SetConfigField("_shopInterval", 4);
            SetConfigField("_curses", new[] { new ProgressionEntryConfig("watcher", "DUPLICATE", "Invalid shared identity.") });
            Assert.Throws<ArgumentException>(() => new ProgressionSessionController(state, config, new System.Random(1)));
        }

        [Test]
        public void InvalidCatalogMultipliersAndPricesAreRejected()
        {
            SetConfigField("_offers", new[] { new ProgressionEntryConfig("bad", "BAD", "Invalid price.", price: -1) });
            Assert.Throws<ArgumentException>(() => new ProgressionSessionController(state, config, new System.Random(1)));
            SetConfigField("_offers", new[] { new ProgressionEntryConfig("bad", "BAD", "Invalid multiplier.", flashlightRangeMultiplier: float.NaN) });
            Assert.Throws<ArgumentException>(() => new ProgressionSessionController(state, config, new System.Random(1)));
        }

        private int Revision => controller.Snapshot().Revision;
        private void ChooseLoadout(string threat = "watcher", string curse = "restless")
        {
            Assert.That(controller.ChooseThreat(threat, Revision), Is.True);
            Assert.That(controller.ChooseCurse(curse, Revision), Is.True);
        }
        private int OpenCombatFloor()
        {
            ChooseLoadout();
            int generation = controller.GenerationRequest().GenerationId;
            Assert.That(controller.ConfirmFloorReady(generation), Is.True);
            return generation;
        }
        private void ReachFirstShop(int credits, float health = 100f)
        {
            for (int round = 1; round <= 3; round++)
            {
                int generation = OpenCombatFloor();
                if (round == 1)
                    for (int anchor = 0; anchor < credits; anchor++) controller.RecordGoldenCollected(generation, anchor);
                controller.RecordHealth(generation, health);
                controller.CompleteFloor(generation);
            }
            controller.ConfirmFloorReady(controller.GenerationRequest().GenerationId);
        }
        private void SetConfigField(string name, object value) => typeof(ProgressionConfig)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, value);
    }
}
