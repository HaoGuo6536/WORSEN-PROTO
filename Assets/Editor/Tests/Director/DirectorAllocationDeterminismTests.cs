// ============================================================================
// DirectorAllocationDeterminismTests.cs
// ============================================================================
// PURPOSE:
//   Compare retained scratch output with the pre-cleanup pacing and seeded trial rules.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Director.
// KEY RESPONSIBILITIES:
//   - Check every output and its ordering across shuffled multi-player observations.
//   - Preserve delivered snapshots across subsequent ticks and reset.
//   - Measure warmed non-evaluation ticks without native engine dependencies.
// DEPENDENCIES:
//   NUnit, Director/Core values and managed reflection for configuration data.
// USAGE NOTES:
//   Config is an uninitialized managed data shell, as in the Floor pure fixtures.
//   The oracle uses the old ID ordering, strict thresholds and delivery deadlines.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Director;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Director
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class DirectorAllocationDeterminismTests
    {
        [TestCase(7)] [TestCase(77)] [TestCase(2026)]
        public void SeededOutputsMatchLegacyPacingOrderingAndRandomConsumption(int seed)
        {
            var config = Config();
            var controller = new DirectorController(new DirectorBehaviorState(), config, new System.Random(seed));
            var expectedRandom = new System.Random(seed);
            var inputRandom = new System.Random(seed + 1);
            var ids = Enumerable.Range(1, 8).ToArray();
            var hunters = ids.Select(id => new DirectorHunterSample(new EntityId(id + 20), new EntityId(id),
                Vector3.zero, true, true)).Reverse().ToArray();
            var retained = new List<DirectorTickResult>();
            var copies = new List<DirectorPressureSample[]>();
            for (int tick = 1; tick <= 100; tick++)
            {
                var players = ids.OrderBy(_ => inputRandom.Next()).Select(id => new DirectorPlayerSample(
                    new EntityId(id), new Vector3(id, 0f, tick * 4f), id % 3 == 0 ? Vector3.zero : Vector3.forward * 8f,
                    true, id % 2 == 0)).ToArray();
                var actual = controller.Tick(tick, .5f, players, hunters, false);
                var hints = new List<HintPayload>();
                var intrusions = new List<IntrusionSample>();
                var pressure = new List<DirectorPressureSample>();
                var retreats = new List<EntityId>();
                // Former reconciliation sorted player IDs before evaluation and hunter IDs before trials.
                foreach (int id in ids.OrderBy(id => id))
                {
                    bool chase = id % 2 == 0, hint = !chase && tick >= 2;
                    if (hint) hints.Add(new HintPayload(new EntityId(id + 20), new EntityId(id), tick - 1, tick,
                        new Vector3(id, 0f, (tick - 1) * 4f), .5f, 8f, .5f));
                    if (id % 3 == 0 && tick == 2) intrusions.Add(new IntrusionSample(new EntityId(id), tick, 1f));
                    pressure.Add(new DirectorPressureSample(new EntityId(id), tick, chase ? 0f : tick * .5f,
                        chase ? 0f : tick * .5f, chase, false, hint));
                    if (chase && tick % 2 == 1 && expectedRandom.NextDouble() < .5)
                        retreats.Add(new EntityId(id + 20));
                }
                Assert.That(actual.Hints, Is.EqualTo(hints), "hints at " + tick);
                Assert.That(actual.Intrusions, Is.EqualTo(intrusions), "intrusions at " + tick);
                Assert.That(actual.Pressure, Is.EqualTo(pressure), "pressure at " + tick);
                Assert.That(actual.Retreats, Is.EqualTo(retreats), "retreats at " + tick);
                Assert.That(actual.Regions, Is.Empty); Assert.That(actual.Noises, Is.Empty);
                retained.Add(actual); copies.Add(actual.Pressure.ToArray());
            }
            controller.Reset();
            for (int i = 0; i < retained.Count; i++) Assert.That(retained[i].Pressure, Is.EqualTo(copies[i]));
        }

        [Test]
        public void WarmedNonEvaluationTicksAllocateNoManagedScratch()
        {
            var config = Config(); Set(config, "_evaluationIntervalSeconds", 1000f);
            var controller = new DirectorController(new DirectorBehaviorState(), config, new System.Random(7));
            var players = new[] { new DirectorPlayerSample(new EntityId(1), Vector3.zero, Vector3.one, true, false) };
            var hunters = Array.Empty<DirectorHunterSample>();
            for (int tick = 1; tick <= 32; tick++) controller.Tick(tick, .001f, players, hunters, false);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int tick = 33; tick <= 132; tick++) controller.Tick(tick, .001f, players, hunters, false);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private static DirectorConfig Config()
        {
            var config = (DirectorConfig)FormatterServices.GetUninitializedObject(typeof(DirectorConfig));
            Set(config, "_evaluationIntervalSeconds", .5f); Set(config, "_hintAgeSeconds", .5f);
            Set(config, "_hintCadenceSeconds", .5f); Set(config, "_exitOpenHintCadenceSeconds", .5f);
            Set(config, "_intrusionDurationSeconds", 1f); Set(config, "_retreatCooldownSeconds", 1f);
            Set(config, "_slowThresholdSeconds", .5f); Set(config, "_slowSpeedMetersPerSecond", 1f);
            Set(config, "_historyCapacity", 128); Set(config, "_retreatProbability", .5f);
            Set(config, "_hintRadiusMeters", 8f); Set(config, "_hintConfidence", .5f);
            return config;
        }
        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
