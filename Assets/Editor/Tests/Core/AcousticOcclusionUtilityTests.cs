// ============================================================================
// AcousticOcclusionUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Verifies one shared hearing rule for distance, portals, doors and audibility.
//   Deterministic graph fixtures protect hunter and audio consumers from divergence.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Core Hearing.
// KEY RESPONSIBILITIES:
//   - Check shortest paths, reciprocal sound, edge thresholds and invalid inputs.
// DEPENDENCIES:
//   - Core graph/hearing types, NUnit and pure UnityEngine value types.
// USAGE NOTES:
//   Pure Edit Mode tests; fixture values are not production tuning defaults.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Tests.Core
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class AcousticOcclusionUtilityTests
    {
        private static LevelGraph Graph(params LevelEdge[] edges) => LevelGraphUtility.Build(
            new[] { new LevelRoom(1, Vector3.zero, Vector3.one), new LevelRoom(2, Vector3.zero, Vector3.one),
                new LevelRoom(3, Vector3.zero, Vector3.one), new LevelRoom(4, Vector3.zero, Vector3.one) },
            edges, Array.Empty<LevelAnchor>(), 1, Vector3.zero);
        private static HearingModelSettings Settings(float threshold = 0.1f) => new HearingModelSettings(1f, 1f, 0.5f, 0.25f, threshold);
        private static HearingSample Sample(LevelGraph graph, int listener, IReadOnlyDictionary<int, bool> doors = null) =>
            AcousticOcclusionUtility.Sample(graph, 1, Vector3.zero, listener, Vector3.zero, 1f, Settings(), doors);

        [Test]
        public void SameRoomHasNoPortalLossAndDistanceFallsOffBeyondReference()
        {
            var near = Sample(Graph(), 1);
            Assert.That(near.PerceivedLoudness, Is.EqualTo(1f));
            Assert.That(near.PortalCount, Is.Zero);
            Assert.That(near.CrossedClosedDoor, Is.False);
            Assert.That(near.Audible, Is.True);
            var far = AcousticOcclusionUtility.Sample(Graph(), 1, Vector3.zero, 1, new Vector3(2f, 0f, 0f), 1f, Settings());
            Assert.That(far.PerceivedLoudness, Is.EqualTo(0.5f));
        }

        [Test]
        public void PortalAttenuationIsMonotonicWithHopsAndShortestPathWins()
        {
            var chain = Graph(new LevelEdge(11, 1, 2, true), new LevelEdge(12, 2, 3, true));
            Assert.That(Sample(chain, 2).PortalCount, Is.EqualTo(1));
            Assert.That(Sample(chain, 2).PerceivedLoudness, Is.EqualTo(0.5f));
            Assert.That(Sample(chain, 3).PortalCount, Is.EqualTo(2));
            Assert.That(Sample(chain, 3).PerceivedLoudness, Is.LessThan(Sample(chain, 2).PerceivedLoudness));
            var shortcut = Graph(new LevelEdge(11, 1, 2, true), new LevelEdge(12, 2, 3, true), new LevelEdge(13, 1, 3, true));
            Assert.That(Sample(shortcut, 3).PortalCount, Is.EqualTo(1));
        }

        [Test]
        public void ClosedDoorsMultiplyLossAndMissingEntriesAreOpen()
        {
            var graph = Graph(new LevelEdge(11, 1, 2, true), new LevelEdge(12, 2, 3, true));
            var one = new Dictionary<int, bool> { [11] = true };
            var two = new Dictionary<int, bool> { [11] = true, [12] = true };
            Assert.That(Sample(graph, 3, one).PerceivedLoudness, Is.LessThan(Sample(graph, 3).PerceivedLoudness));
            Assert.That(Sample(graph, 3, two).PerceivedLoudness, Is.LessThan(Sample(graph, 3, one).PerceivedLoudness));
            Assert.That(Sample(graph, 3, one).CrossedClosedDoor, Is.True);
            Assert.That(Sample(graph, 3, new Dictionary<int, bool> { [11] = false }), Is.EqualTo(Sample(graph, 3)));
        }

        [Test]
        public void UnreachableOrUnknownRoomsAreInaudibleEvenWithZeroThreshold()
        {
            foreach (int room in new[] { 4, 99 })
            {
                var sample = AcousticOcclusionUtility.Sample(Graph(), 1, Vector3.zero, room, Vector3.zero, 1f, Settings(0f));
                Assert.That(sample.Audible, Is.False);
                Assert.That(sample.PerceivedLoudness, Is.Zero);
                Assert.That(sample.PortalCount, Is.EqualTo(-1));
            }
        }

        [Test]
        public void ThresholdIsInclusiveButSilenceIsNeverAudibleAndAmplitudeIsClamped()
        {
            var graph = Graph();
            Assert.That(AcousticOcclusionUtility.Sample(graph, 1, Vector3.zero, 1, Vector3.zero, 0.5f, Settings(0.5f)).Audible, Is.True);
            Assert.That(AcousticOcclusionUtility.Sample(graph, 1, Vector3.zero, 1, Vector3.zero, 0.499f, Settings(0.5f)).Audible, Is.False);
            Assert.That(AcousticOcclusionUtility.Sample(graph, 1, Vector3.zero, 1, Vector3.zero, -1f, Settings(0f)).Audible, Is.False);
            Assert.That(AcousticOcclusionUtility.Sample(graph, 1, Vector3.zero, 1, Vector3.zero, 2f, Settings()).PerceivedLoudness, Is.EqualTo(1f));
        }

        [Test]
        public void SoundIsReciprocalAcrossActorRestrictedOneWayEdges()
        {
            var graph = Graph(new LevelEdge(11, 2, 1, false, TraversalAccess.Player));
            var reverse = AcousticOcclusionUtility.Sample(graph, 2, Vector3.zero, 1, Vector3.zero, 1f, Settings());
            Assert.That(Sample(graph, 2), Is.EqualTo(reverse));
            Assert.That(reverse.PortalCount, Is.EqualTo(1));
        }

        [Test]
        public void EqualHopPathsPreferFewerClosedDoorsIndependentlyOfInputOrder()
        {
            var edges = new[] { new LevelEdge(11, 1, 2, true), new LevelEdge(12, 2, 4, true),
                new LevelEdge(13, 1, 3, true), new LevelEdge(14, 3, 4, true) };
            var graph = Graph(edges);
            var doors = new Dictionary<int, bool> { [11] = true };
            var expected = Sample(graph, 4, doors);
            Assert.That(expected.CrossedClosedDoor, Is.False);
            Assert.That(expected.PortalCount, Is.EqualTo(2));
            Array.Reverse(edges);
            for (int i = 0; i < 20; i++) Assert.That(Sample(Graph(edges), 4, doors), Is.EqualTo(expected));
        }

        [Test]
        public void InvalidModelAndNonfiniteInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HearingModelSettings(0f, 1f, 1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HearingModelSettings(1f, float.NaN, 1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HearingModelSettings(1f, 1f, 2f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HearingModelSettings(1f, 1f, 1f, -1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HearingModelSettings(1f, 1f, 1f, 1f, float.NaN));
            Assert.Throws<ArgumentException>(() => AcousticOcclusionUtility.Sample(Graph(), 1, Vector3.zero, 1, Vector3.zero, 1f, default));
            Assert.Throws<ArgumentException>(() => AcousticOcclusionUtility.Sample(Graph(), 1, new Vector3(float.NaN, 0f, 0f), 1, Vector3.zero, 1f, Settings()));
            Assert.Throws<ArgumentOutOfRangeException>(() => AcousticOcclusionUtility.Sample(Graph(), 1, Vector3.zero, 1, Vector3.zero, float.PositiveInfinity, Settings()));
        }
    }
}
