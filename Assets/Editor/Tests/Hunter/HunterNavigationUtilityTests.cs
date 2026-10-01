// ============================================================================
// HunterNavigationUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Verifies directed Hunter paths, alternate corridors and bounded search points.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Reject closed rooms and reverse-only routes.
//   - Preserve fixed search order and graceful absence of doorway observations.
// DEPENDENCIES:
//   - Core topology, HunterNavigationUtility, UnityEngine values and NUnit.
// USAGE NOTES:
//   Pure immutable fixtures; no scene or physics operations.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterNavigationUtilityTests
    {
        private static LevelGraph Graph() => new LevelGraph(new[] {
            new LevelRoom(1, new Vector3(0, 2, 0), new Vector3(8, 4, 8)),
            new LevelRoom(2, new Vector3(0, 2, 10), new Vector3(8, 4, 8)),
            new LevelRoom(3, new Vector3(10, 2, 0), new Vector3(8, 4, 8)) },
            new[] { new LevelEdge(1, 1, 2, false), new LevelEdge(2, 1, 3, true), new LevelEdge(3, 3, 2, false) },
            Array.Empty<LevelAnchor>(), 2, Vector3.forward * 10f);
        [Test] public void AlternatePathAvoidsDirectFirstEdgeAndHonorsDirectionAndClosedRooms()
        {
            var graph = Graph();
            Assert.That(HunterNavigationUtility.Path(graph, 1, 2, null), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(HunterNavigationUtility.ParallelPath(graph, 1, 2, null), Is.EqualTo(new[] { 1, 3, 2 }));
            Assert.That(HunterNavigationUtility.Path(graph, 2, 1, null), Is.Empty);
            Assert.That(HunterNavigationUtility.ParallelPath(graph, 1, 2, new HashSet<int> { 3 }), Is.Empty);
            Assert.That(HunterNavigationUtility.Path(graph, 1, 2, new HashSet<int> { 2 }), Is.Empty);
        }
        [Test] public void SearchWithoutObservedDoorUsesHeadingAndReturnsOnceToLossPoint()
        {
            Vector3 loss = Vector3.zero;
            var route = HunterNavigationUtility.Search(Graph(), loss, Vector3.forward, 0, 1, 2f, null);
            Assert.That(route, Is.EqualTo(new[] { loss, Vector3.right * 2f, Vector3.left * 4f,
                Vector3.forward * 4f, Vector3.forward * 10f, loss }));
            Assert.That(HunterNavigationUtility.Search(Graph(), loss, Vector3.forward, 0, 1, 2f, null), Is.EqualTo(route));
        }
    }
}
