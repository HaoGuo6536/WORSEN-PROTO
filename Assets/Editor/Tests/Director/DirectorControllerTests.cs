// ============================================================================
// DirectorControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies pressure pacing using explicit movement samples and deterministic time.
//   The cases distinguish actual historical information from current-position
//   fallback, and exercise relief and intrusion boundaries without a scene.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Director.
// KEY RESPONSIBILITIES:
//   - Exercise variable batches, history warmup/wraparound, and pressure thresholds.
//   - Verify multiple-player assignment, lifecycle reset, and intrusion episodes.
//   - Distinguish tick delivery deadlines from earlier evaluation boundaries at
//     60 Hz and with fractional multi-evaluation batches, without early delivery.
// DEPENDENCIES:
//   - Core payloads, Domain Director pure rules/config, UnityEngine values, NUnit.
// USAGE NOTES:
//   Edit Mode suite; configuration assets are transient and destroyed after each
//   case. No live scene, engine probes, or competing tick loop is required.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Director;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Director
{
    public sealed class DirectorControllerTests
    {
        private DirectorConfig _config;
        private DirectorBehaviorState _state;
        private DirectorController _controller;
        private long _tick;
        private static readonly EntityId Player = new EntityId(1);
        private static readonly EntityId Hunter = new EntityId(10);
        private static readonly DirectorHunterSample[] DistantHunter = {
            new DirectorHunterSample(Hunter, Player, new Vector3(10000f, 0f, 10000f), true) };

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<DirectorConfig>();
            _state = new DirectorBehaviorState();
            _controller = new DirectorController(_state, _config, new System.Random(77));
            _tick = 0;
        }

        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(_config); }

        private void Tune(string name, object value)
        { typeof(DirectorConfig).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_config, value); }

        private DirectorTickResult Step(float dt = 0.5f, float speed = 8f, bool chase = false,
            bool exitOpen = false, Vector3 position = default, IReadOnlyList<DirectorHunterSample> hunters = null)
            => _controller.Tick(_tick++, dt,
                new[] { new DirectorPlayerSample(Player, position, new Vector3(speed, 0f, 0f), true, chase) },
                hunters ?? DistantHunter, exitOpen);

        private List<HintPayload> Advance(float seconds, bool exitOpen = false, bool chase = false, float speed = 8f)
        {
            var hints = new List<HintPayload>();
            int count = (int)Math.Round(seconds / 0.5f);
            for (int i = 0; i < count; i++) hints.AddRange(Step(exitOpen: exitOpen, chase: chase, speed: speed).Hints);
            return hints;
        }

        [Test]
        public void DefaultsKeepPrototypeTimingAndUncertainty()
        {
            Assert.That(_config.EvaluationIntervalSeconds, Is.EqualTo(0.5f));
            Assert.That(_config.HeatThresholdSeconds, Is.EqualTo(20f));
            Assert.That(_config.ReliefMinimumSeconds, Is.EqualTo(10f));
            Assert.That(_config.HintAgeSeconds, Is.EqualTo(3f));
            Assert.That(_config.HintRadiusMeters, Is.EqualTo(8f));
            Assert.That(_config.IntrusionDurationSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void AccumulatorEvaluatesAtTwoHertzAcrossUnevenBatches()
        {
            Assert.That(Step(0.2f).Pressure, Is.Empty);
            Assert.That(Step(0.2f).Pressure, Is.Empty);
            Assert.That(Step(0.2f).Pressure.Count, Is.EqualTo(1));
            Assert.That(Step(1.4f).Pressure.Count, Is.EqualTo(3));
            Assert.That(_state.EvaluationCount, Is.EqualTo(4));
            Assert.That(_state.ElapsedSeconds, Is.EqualTo(2d).Within(0.000001));
        }

        [Test]
        public void SixtyHertzBatchesDoNotDriftOrDuplicateEvaluation()
        {
            int observations = 0;
            for (int i = 0; i < 600; i++) observations += Step(1f / 60f).Pressure.Count;
            Assert.That(observations, Is.EqualTo(20));
            Assert.That(_state.ElapsedSeconds, Is.EqualTo(10d).Within(0.00001));
        }

        [Test]
        public void FirstHintRequiresStrictlyMoreThanTwentySecondsOfHeat()
        {
            Assert.That(Advance(20f), Is.Empty);
            var result = Step();
            Assert.That(result.Hints.Count, Is.EqualTo(1));
            Assert.That(result.Pressure[0].HeatSeconds, Is.EqualTo(20.5f));
            Assert.That(result.Pressure[0].HintIssued, Is.True);
        }

        [Test]
        public void MissingHistoryNeverFallsBackToLivePosition()
        {
            Tune("_heatThresholdSeconds", 0f);
            Assert.That(Step(20f, position: new Vector3(99f, 0f, 0f)).Hints, Is.Empty);
            Assert.That(Advance(2.5f), Is.Empty);
            var hint = Step().Hints[0];
            Assert.That(hint.Position.x, Is.EqualTo(99f));
            Assert.That(hint.AgeSeconds, Is.EqualTo(3f).Within(0.00001f));
            Assert.That(hint.ObservedTick, Is.EqualTo(0));
        }

        [Test]
        public void HintUsesThreeSecondOldPositionWithEightMeterRadius()
        {
            HintPayload hint = default;
            for (int i = 1; i <= 41; i++)
            {
                var result = Step(position: new Vector3(i * 4f, 0f, 0f));
                if (result.Hints.Count != 0) hint = result.Hints[0];
            }
            Assert.That(hint.Position.x, Is.EqualTo(140f).Within(0.0001f));
            Assert.That(hint.Position.x, Is.Not.EqualTo(164f));
            Assert.That(hint.AgeSeconds, Is.EqualTo(3f));
            Assert.That(hint.Radius, Is.EqualTo(8f));
            Assert.That(hint.ObservedTick, Is.EqualTo(34));
            Assert.That(hint.DeliveredTick, Is.EqualTo(40));
        }

        [Test]
        public void IrregularHistoricalSamplesAreInterpolatedAtDeliveryMinusAge()
        {
            Tune("_heatThresholdSeconds", 0f);
            HintPayload hint = default;
            for (int i = 1; i <= 12; i++)
            {
                var result = Step(0.3f, position: new Vector3(i * 3f, 0f, 0f));
                if (result.Hints.Count > 0) hint = result.Hints[0];
            }
            Assert.That(hint.Position.x, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(hint.AgeSeconds, Is.EqualTo(3f).Within(0.00001f));
        }

        [Test]
        public void RingWrapKeepsBoundedStorageAndCorrectHistoricalPosition()
        {
            HintPayload lastHint = default;
            for (int i = 1; i <= 2400; i++)
            {
                var result = Step(1f / 60f, position: new Vector3(i / 60f, 0f, 0f));
                if (result.Hints.Count > 0) lastHint = result.Hints[0];
            }
            Assert.That(_state.Players[Player].HistoryCount, Is.EqualTo(512));
            Assert.That(_state.Players[Player].History.Length, Is.EqualTo(512));
            float deliveredSeconds = (lastHint.DeliveredTick + 1) / 60f;
            Assert.That(lastHint.Position.x, Is.EqualTo(deliveredSeconds - 3f).Within(0.0001f));
        }

        [Test]
        public void TooShortHistorySuppressesHintsAfterRingWrap()
        {
            Tune("_historyCapacity", 2);
            _controller.Reset();
            Assert.That(Advance(30f), Is.Empty);
        }

        [Test]
        public void ChaseResetsHeatAndNoHintsOccurWhileChasing()
        {
            Advance(20.5f);
            Assert.That(Advance(30f, chase: true), Is.Empty);
            Assert.That(_state.Players[Player].HeatSeconds, Is.Zero);
            Assert.That(_state.Players[Player].ReliefSeconds, Is.Zero);
        }

        [Test]
        public void ChaseEndSuppressesHintsUntilReliefEqualsTenSeconds()
        {
            Tune("_heatThresholdSeconds", 0f);
            Advance(4f, chase: true);
            Assert.That(Advance(9.5f), Is.Empty);
            var result = Step();
            Assert.That(result.Hints.Count, Is.EqualTo(1));
            Assert.That(result.Pressure[0].ReliefSeconds, Is.EqualTo(10f));
        }

        [Test]
        public void ProximityStrictlyBelowTwentyMetersResetsHeat()
        {
            Advance(20f);
            var near = new[] { new DirectorHunterSample(Hunter, Player, new Vector3(19.999f, 0f, 0f), true) };
            Assert.That(Step(hunters: near).Hints, Is.Empty);
            Assert.That(_state.Players[Player].HeatSeconds, Is.Zero);
            var boundary = new[] { new DirectorHunterSample(Hunter, Player, new Vector3(20f, 0f, 0f), true) };
            Assert.That(Step(hunters: boundary).Pressure[0].IsWithinProximity, Is.False);
            Assert.That(_state.Players[Player].HeatSeconds, Is.EqualTo(0.5d));
        }

        [Test]
        public void AnyActiveHunterProvidesProximityEvenWhenNotAssigned()
        {
            var hunters = new[] { DistantHunter[0], new DirectorHunterSample(new EntityId(11), new EntityId(2), Vector3.zero, true) };
            var result = Step(hunters: hunters);
            Assert.That(_state.Players[Player].AssignedHunterId, Is.EqualTo(Hunter));
            Assert.That(result.Pressure[0].IsWithinProximity, Is.True);
        }

        [Test]
        public void HintEmissionPreservesRawPressureGapAndUsesCadence()
        {
            Advance(20.5f);
            Assert.That(Advance(4.5f), Is.Empty);
            var result = Step();
            Assert.That(result.Hints.Count, Is.EqualTo(1));
            Assert.That(result.Pressure[0].HeatSeconds, Is.EqualTo(25.5f));
        }

        [Test]
        public void ExitOpeningTightensCadenceImmediately()
        {
            Advance(20.5f);
            Assert.That(Advance(2f), Is.Empty);
            Assert.That(Step(exitOpen: true).Hints.Count, Is.EqualTo(1));
        }

        [Test]
        public void SixtyHertzHintCadenceUsesTheSameDeliveryClockAcrossExitOpening()
        {
            var hints = new List<HintPayload>();
            int evaluations = 0;
            for (int step = 1; step <= 2400 && hints.Count < 4; step++)
            {
                var result = Step(1f / 60f, exitOpen: hints.Count >= 2);
                evaluations += result.Pressure.Count;
                Assert.That(result.Pressure.Count, Is.EqualTo(step % 30 == 0 ? 1 : 0), "Decisions remain on the 2 Hz schedule.");
                hints.AddRange(result.Hints);
            }
            Assert.That(hints.Count, Is.EqualTo(4));
            Assert.That(hints[1].DeliveredTick - hints[0].DeliveredTick, Is.EqualTo(300));
            Assert.That(hints[2].DeliveredTick - hints[1].DeliveredTick, Is.EqualTo(150));
            Assert.That(hints[3].DeliveredTick - hints[2].DeliveredTick, Is.EqualTo(150));
            Assert.That(evaluations, Is.EqualTo((hints[3].DeliveredTick + 1) / 30));
        }

        [Test]
        public void FractionalMultiEvaluationHintBatchesNeverDeliverEarlyOrStack()
        {
            Advance(4f); // Keep actual history before the first large tick.
            var first = Step(16.625f);
            Assert.That(first.Hints.Count, Is.EqualTo(1));
            Assert.That(first.Pressure.Count, Is.EqualTo(33));
            Assert.That(_state.Players[Player].LastHintDeliveredAtSeconds, Is.EqualTo(20.625d));
            Assert.That(Step(4.75f).Hints, Is.Empty, "A deadline stored at delivery cannot be treated as the earlier evaluation boundary.");
            var due = Step(0.25f); // Evaluation at 25.5; actual delivery at 25.625.
            Assert.That(due.Hints.Count, Is.EqualTo(1));
            Assert.That(_state.Players[Player].LastHintDeliveredAtSeconds, Is.EqualTo(25.625d));
            var catchUp = Step(12.125f);
            Assert.That(catchUp.Pressure.Count, Is.EqualTo(24));
            Assert.That(catchUp.Hints.Count, Is.EqualTo(1), "Many crossed boundaries share one delivery time and must not stack hints.");
            Assert.That(_state.Players[Player].LastHintDeliveredAtSeconds, Is.EqualTo(37.75d));
            Assert.That(Step(4.5f).Hints, Is.Empty);
            Assert.That(Step(0.5f).Hints.Count, Is.EqualTo(1));
        }

        [Test]
        public void SixtyHertzRearmedIntrusionUsesItsActualDeliveryCooldown()
        {
            var intrusions = new List<IntrusionSample>();
            for (int step = 0; step < 1800 && intrusions.Count < 2; step++)
            {
                bool rearm = intrusions.Count == 1 && _tick == intrusions[0].Tick + 1;
                var result = Step(1f / 60f, speed: rearm ? 8f : 0f);
                if (intrusions.Count == 1 && result.Intrusions.Count > 0)
                    Assert.That(result.Intrusions[0].Tick - intrusions[0].Tick, Is.EqualTo(600),
                        "Default ten-second cooldown must be compared against the same delivery clock.");
                intrusions.AddRange(result.Intrusions);
            }
            Assert.That(intrusions.Count, Is.EqualTo(2));
            for (int i = 0; i < 900; i++) Assert.That(Step(1f / 60f, speed: 0f).Intrusions, Is.Empty,
                "Elapsed cooldown alone does not create another slow episode.");
        }

        [Test]
        public void FractionalMultiEvaluationIntrusionCooldownNeverDeliversEarly()
        {
            Assert.That(Step(3.625f, speed: 0f).Intrusions.Count, Is.EqualTo(1));
            Assert.That(_state.Players[Player].IntrusionAvailableAtSeconds, Is.EqualTo(13.625d));
            Step(0.125f, speed: 8f); // Rearm without changing the prior actual delivery deadline.
            Assert.That(Step(9.5f, speed: 0f).Intrusions, Is.Empty);
            Assert.That(Step(0.375f, speed: 0f).Intrusions.Count, Is.EqualTo(1),
                "Evaluation boundary 13.5 must observe actual delivery 13.625 at the cooldown deadline.");
            Assert.That(_state.Players[Player].IntrusionAvailableAtSeconds, Is.EqualTo(23.625d));
            Step(0.125f, speed: 8f);
            Assert.That(Step(19.875f, speed: 0f).Intrusions.Count, Is.EqualTo(1),
                "One rearmed episode still produces one intrusion across a large catch-up batch.");
            Assert.That(_state.Players[Player].IntrusionAvailableAtSeconds, Is.EqualTo(43.625d));
            Assert.That(Step(20f, speed: 0f).Intrusions, Is.Empty, "Slow episode remains disarmed after emission.");
        }

        [Test]
        public void MissingHuntersNeverProduceUndeliverableHints()
        {
            for (int i = 0; i < 60; i++) Assert.That(Step(hunters: Array.Empty<DirectorHunterSample>()).Hints, Is.Empty);
            Assert.That(Step().Hints.Count, Is.EqualTo(1));
        }

        [Test]
        public void AssignmentIsStableAcrossRegistryOrderAndReassignedOnRemoval()
        {
            var second = new DirectorHunterSample(new EntityId(11), Player, new Vector3(200f, 0f, 0f), true);
            Step(hunters: new[] { second, DistantHunter[0] });
            Assert.That(_state.Players[Player].AssignedHunterId, Is.EqualTo(Hunter));
            Step(hunters: new[] { DistantHunter[0], second });
            Assert.That(_state.Players[Player].AssignedHunterId, Is.EqualTo(Hunter));
            Step(hunters: new[] { second });
            Assert.That(_state.Players[Player].AssignedHunterId, Is.EqualTo(second.HunterId));
        }

        [Test]
        public void MultiplePlayersKeepSeparateReliefAndStableBalancedAssignments()
        {
            Tune("_heatThresholdSeconds", 0f);
            var secondId = new EntityId(2);
            var hunters = new[] { DistantHunter[0], new DirectorHunterSample(new EntityId(11), secondId, new Vector3(200f, 0f, 0f), true) };
            var players = new[] {
                new DirectorPlayerSample(secondId, Vector3.right, Vector3.right * 8f, true, false),
                new DirectorPlayerSample(Player, Vector3.zero, Vector3.right * 8f, true, true) };
            DirectorTickResult result = default;
            for (int i = 0; i < 7; i++) result = _controller.Tick(_tick++, 0.5f, players, hunters, false);
            Assert.That(result.Hints.Count, Is.EqualTo(1));
            Assert.That(result.Hints[0].Player, Is.EqualTo(secondId));
            Assert.That(_state.Players[Player].ReliefSeconds, Is.Zero);
            Assert.That(_state.Players[secondId].ReliefSeconds, Is.GreaterThanOrEqualTo(10d));
            Assert.That(_state.Players[Player].AssignedHunterId, Is.Not.EqualTo(_state.Players[secondId].AssignedHunterId));
        }

        [Test]
        public void CrossedHunterTargetsReceiveOnlyHintsTheyCanAccept()
        {
            Tune("_heatThresholdSeconds", 0f);
            var secondId = new EntityId(2);
            var secondHunter = new EntityId(11);
            var hunters = new[] {
                new DirectorHunterSample(Hunter, secondId, new Vector3(200f, 0f, 0f), true),
                new DirectorHunterSample(secondHunter, Player, new Vector3(200f, 0f, 0f), true) };
            var players = new[] {
                new DirectorPlayerSample(Player, Vector3.zero, Vector3.right * 8f, true, false),
                new DirectorPlayerSample(secondId, Vector3.right, Vector3.right * 8f, true, false) };
            DirectorTickResult result = default;
            for (int i = 0; i < 7; i++) result = _controller.Tick(_tick++, 0.5f, players, hunters, false);
            Assert.That(result.Hints.Count, Is.EqualTo(2));
            Assert.That(result.Hints[0].Player, Is.EqualTo(Player));
            Assert.That(result.Hints[0].Hunter, Is.EqualTo(secondHunter));
            Assert.That(result.Hints[1].Player, Is.EqualTo(secondId));
            Assert.That(result.Hints[1].Hunter, Is.EqualTo(Hunter));
        }

        [Test]
        public void HuntersWithDifferentTargetsDoNotCreateIssuedHintFacts()
        {
            var foreign = new[] { new DirectorHunterSample(Hunter, new EntityId(2), new Vector3(200f, 0f, 0f), true) };
            for (int i = 0; i < 60; i++)
            {
                var result = Step(hunters: foreign);
                Assert.That(result.Hints, Is.Empty);
                Assert.That(result.Pressure[0].HintIssued, Is.False);
            }
            Assert.That(_state.Players[Player].AssignedHunterId, Is.EqualTo(EntityId.None));
            Assert.That(Step().Hints.Count, Is.EqualTo(1));
        }

        [Test]
        public void BatchCatchUpDeliversOneHintAndStartsCadenceAtActualDelivery()
        {
            Advance(20.5f);
            var batch = Step(10f);
            Assert.That(batch.Hints.Count, Is.EqualTo(1));
            Assert.That(batch.Pressure.Count, Is.EqualTo(20));
            int issued = 0;
            foreach (var sample in batch.Pressure) if (sample.HintIssued) issued++;
            Assert.That(issued, Is.EqualTo(1));
            Assert.That(Advance(4.5f), Is.Empty);
            Assert.That(Step().Hints.Count, Is.EqualTo(1));
        }

        [Test]
        public void InvalidConfidenceIsRejectedAtConstructionAndBeforeTickMutation()
        {
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0.1f, 1.1f })
            {
                Tune("_hintConfidence", invalid);
                Assert.Throws<ArgumentException>(() => new DirectorController(new DirectorBehaviorState(), _config, new System.Random(77)));
                Assert.Throws<ArgumentException>(() => Step());
                Assert.That(_state.ElapsedSeconds, Is.Zero);
                Assert.That(_state.Players, Is.Empty);
            }
        }

        [Test]
        public void FiniteConfidenceEndpointsRemainValidConfiguration()
        {
            foreach (var endpoint in new[] { 0f, 1f })
            {
                Tune("_hintConfidence", endpoint);
                Assert.DoesNotThrow(() => new DirectorController(new DirectorBehaviorState(), _config, new System.Random(77)));
                _controller.Reset();
                _tick = 0;
                var hints = Advance(20.5f);
                Assert.That(hints.Count, Is.EqualTo(1));
                Assert.That(hints[0].Confidence, Is.EqualTo(endpoint));
            }
        }

        [Test]
        public void NonfiniteLivingPlayerPoseOrVelocityCannotEnterHistory()
        {
            var invalidVectors = new[] { new Vector3(float.NaN, 0f, 0f), new Vector3(0f, float.PositiveInfinity, 0f),
                new Vector3(0f, 0f, float.NegativeInfinity) };
            foreach (var invalid in invalidVectors)
            {
                var badPose = new[] { new DirectorPlayerSample(Player, invalid, Vector3.right, true, false) };
                var badVelocity = new[] { new DirectorPlayerSample(Player, Vector3.zero, invalid, true, false) };
                Assert.Throws<ArgumentException>(() => _controller.Tick(_tick++, 0.5f, badPose, DistantHunter, false));
                Assert.Throws<ArgumentException>(() => _controller.Tick(_tick++, 0.5f, badVelocity, DistantHunter, false));
                Assert.That(_state.ElapsedSeconds, Is.Zero);
                Assert.That(_state.Players, Is.Empty);
            }
        }

        [Test]
        public void NonfiniteActiveHunterPositionIsRejectedBeforeStateMutation()
        {
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var hunters = new[] { new DirectorHunterSample(Hunter, Player, new Vector3(0f, invalid, 0f), true) };
                Assert.Throws<ArgumentException>(() => Step(hunters: hunters));
                Assert.That(_state.ElapsedSeconds, Is.Zero);
                Assert.That(_state.Players, Is.Empty);
            }
        }

        [Test]
        public void StationaryPlayerGetsOneTwoSecondIntrusionAfterStrictThreeSeconds()
        {
            for (int i = 0; i < 6; i++) Assert.That(Step(speed: 0f).Intrusions, Is.Empty);
            var result = Step(speed: 0f);
            Assert.That(result.Intrusions.Count, Is.EqualTo(1));
            Assert.That(result.Intrusions[0].DurationSeconds, Is.EqualTo(2f));
            for (int i = 0; i < 60; i++) Assert.That(Step(speed: 0f).Intrusions, Is.Empty);
        }

        [Test]
        public void MotionRearmsIntrusionButCooldownStillApplies()
        {
            Advance(3f, speed: 0f);
            Assert.That(Step(speed: 0f).Intrusions.Count, Is.EqualTo(1));
            Step(speed: 8f);
            for (int i = 0; i < 18; i++) Assert.That(Step(speed: 0f).Intrusions, Is.Empty);
            Assert.That(Step(speed: 0f).Intrusions.Count, Is.EqualTo(1));
        }

        [Test]
        public void BatchedIntrusionCooldownStartsAtActualDelivery()
        {
            Assert.That(Step(20f, speed: 0f).Intrusions.Count, Is.EqualTo(1));
            Step(speed: 8f);
            for (int i = 0; i < 18; i++) Assert.That(Step(speed: 0f).Intrusions, Is.Empty);
            Assert.That(Step(speed: 0f).Intrusions.Count, Is.EqualTo(1));
        }

        [Test]
        public void SlowSpeedBoundaryIsInclusiveAndVerticalMotionDoesNotRearm()
        {
            var samples = new[] { new DirectorPlayerSample(Player, Vector3.zero, new Vector3(1f, 10f, 0f), true, false) };
            DirectorTickResult result = default;
            for (int i = 0; i < 7; i++) result = _controller.Tick(_tick++, 0.5f, samples, DistantHunter, false);
            Assert.That(result.Intrusions.Count, Is.EqualTo(1));
        }

        [Test]
        public void RemovingAndRespawningPlayerDiscardsHeatAndHistory()
        {
            Advance(25f);
            var result = _controller.Tick(_tick++, 0.5f, Array.Empty<DirectorPlayerSample>(), DistantHunter, false);
            Assert.That(result.Pressure, Is.Empty);
            Assert.That(_state.Players, Is.Empty);
            Assert.That(Step().Hints, Is.Empty);
            Assert.That(_state.Players[Player].HistoryCount, Is.EqualTo(1));
            Assert.That(_state.Players[Player].HeatSeconds, Is.EqualTo(0.5d));
        }

        [Test]
        public void DeadPlayerEmitsNeitherPressureHintsNorIntrusions()
        {
            Advance(25f);
            var dead = new[] { new DirectorPlayerSample(Player, Vector3.zero, Vector3.zero, false, false) };
            var result = _controller.Tick(_tick++, 10f, dead, DistantHunter, false);
            Assert.That(result.Hints, Is.Empty);
            Assert.That(result.Intrusions, Is.Empty);
            Assert.That(result.Pressure, Is.Empty);
            Assert.That(_state.Players, Is.Empty);
        }

        [Test]
        public void ResetClearsSceneClockAssignmentsAndEpisodes()
        {
            Advance(25f, speed: 0f);
            _controller.Reset();
            Assert.That(_state.Players, Is.Empty);
            Assert.That(_state.ElapsedSeconds, Is.Zero);
            Assert.That(_state.EvaluationCount, Is.Zero);
            Assert.That(_state.EvaluationAccumulatorSeconds, Is.Zero);
            Assert.That(_controller.Tick(0, 0.5f, Array.Empty<DirectorPlayerSample>(), DistantHunter, false).Hints, Is.Empty);
        }

        [Test]
        public void SeededRunsProduceIdenticalHintsWithoutConsumingRandom()
        {
            var random = new System.Random(99);
            var reference = new System.Random(99);
            var otherState = new DirectorBehaviorState();
            var other = new DirectorController(otherState, _config, random);
            var samples = new[] { new DirectorPlayerSample(Player, Vector3.right * 42f, Vector3.right * 8f, true, false) };
            for (int i = 0; i < 61; i++)
            {
                var a = _controller.Tick(i, 0.5f, samples, DistantHunter, false);
                var b = other.Tick(i, 0.5f, samples, DistantHunter, false);
                Assert.That(a.Hints.Count, Is.EqualTo(b.Hints.Count));
                for (int j = 0; j < a.Hints.Count; j++)
                { Assert.That(a.Hints[j].Position, Is.EqualTo(b.Hints[j].Position)); Assert.That(a.Hints[j].Radius, Is.EqualTo(b.Hints[j].Radius)); }
            }
            Assert.That(random.Next(), Is.EqualTo(reference.Next()));
        }

        [Test]
        public void InvalidTimeIsRejectedAndZeroTimeDoesNotAdvance()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Step(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Step(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Step(float.PositiveInfinity));
            Assert.That(Step(0f).Pressure, Is.Empty);
            Assert.That(_state.ElapsedSeconds, Is.Zero);
        }

        [Test]
        public void DuplicateSessionTickCannotDoubleAdvanceTheDirector()
        {
            Step();
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.Tick(0, 0.5f,
                Array.Empty<DirectorPlayerSample>(), DistantHunter, false));
            Assert.That(_state.ElapsedSeconds, Is.EqualTo(0.5d));
        }
    }
}
