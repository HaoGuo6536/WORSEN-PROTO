// ============================================================================
// ProceduralStoreyUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Exercises storey generation and directed objective validation without a scene.
//   Declared seeds cover round gates, alternate ascent and all drop silhouettes;
//   adversarial layouts prove a room-level graph cannot hide a stranded cake.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Verify deterministic manifests, base-kit envelopes and retained retry failures.
// DEPENDENCIES:
//   - Core, Domain.Procedural, NUnit and temporary Unity configuration instances.
// USAGE NOTES:
//   Seeds 0..31 at rounds 2, 3 and 8 are the declared default sample. Geometry and
//   native navigation are verified separately; these are not movement-playtest results.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralStoreyUtilityTests
    {
        private ProceduralConfig _config;
        [SetUp] public void SetUp() => _config = ScriptableObject.CreateInstance<ProceduralConfig>();
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_config);
        private void Set(string name, object value) => typeof(ProceduralConfig).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_config, value);
        private ProceduralLayout Generate(int seed, int round = 3, bool refuge = false)
            => new ProceduralController(new ProceduralBehaviorState(), _config,
                new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round, refuge);
        private static void SetLayout(ProceduralLayout layout, string name, object value)
            => typeof(ProceduralLayout).GetProperty(name).SetValue(layout, value);

        [Test]
        public void DeclaredSeedsGateStoreysAndRepeatFullManifests()
        {
            foreach (int round in new[] { 2, 3, 8 })
            {
                int count = 0;
                var kinds = new System.Collections.Generic.HashSet<ProceduralVerticalKind>();
                for (int seed = 0; seed < 32; seed++)
                {
                    var layout = Generate(seed, round);
                    Assert.That(layout.Manifest, Is.EqualTo(Generate(seed, round).Manifest));
                    Assert.DoesNotThrow(() => ProceduralStoreyUtility.Validate(layout, _config));
                    count += layout.Storeys.Count;
                    foreach (var s in layout.Storeys)
                    {
                        kinds.Add(s.Drop);
                        var routes = layout.VerticalRoutes.Where(r => r.RoomId == s.RoomId).ToArray();
                        Assert.That(routes.Single(r => r.Kind == ProceduralVerticalKind.Ramp).Access, Is.EqualTo(TraversalAccess.All));
                        Assert.That(routes.Single(r => r.Kind == s.Drop).Bidirectional, Is.False);
                        var climb = routes.Single(r => r.Kind == ProceduralVerticalKind.LedgeClimb);
                        Assert.That(climb.Access, Is.EqualTo(TraversalAccess.Player));
                        foreach (int i in new[] { 1, 3 })
                        {
                            var delta = climb.Points[i] - climb.Points[i - 1];
                            Assert.That(delta.y, Is.InRange(_config.BaseLedgeMinimumHeight, _config.BaseLedgeMaximumHeight));
                            Assert.That(new Vector2(delta.x, delta.z).magnitude, Is.LessThanOrEqualTo(_config.BaseLedgeReach));
                        }
                    }
                }
                TestContext.WriteLine("seeds=0..31 round=" + round + " storeys=" + count + " dropKinds=" + kinds.Count);
                if (round < _config.MultiFloorStartRound) Assert.That(count, Is.Zero);
                else { Assert.That(count, Is.GreaterThan(0)); Assert.That(kinds.Count, Is.EqualTo(4)); }
            }
        }

        [Test]
        public void ForcedStoreysPreserveFootprintsBudgetsAndKeepRefugesAndPocketsFlat()
        {
            Set("_oneCellWeight", 0f); Set("_twoCellWeight", 1f); Set("_threeCellWeight", 0f);
            Set("_storeyProbability", 1f); Set("_gapProbability", 1f); Set("_pocketProbability", 1f);
            var layout = Generate(19);
            Assert.That(layout.Storeys.Count, Is.EqualTo(layout.Modules.Count(m => m.Cells.Count > 1 && m.PocketId == 0)));
            foreach (var room in layout.Graph.Rooms.Where(r => layout.Modules[r.Id - 1].PocketId == 0))
                Assert.That(layout.Graph.Anchors.Count(a => a.RoomId == room.Id), Is.InRange(_config.MinimumCandidatesPerRoom, _config.MaximumCandidatesPerRoom));
            Assert.That(layout.Storeys.All(s => layout.Modules[s.RoomId - 1].PocketId == 0), Is.True);
            Assert.That(Generate(19, 3, true).Storeys, Is.Empty);
            Set("_baseReboundSupported", false);
            var noRebound = Generate(19);
            Assert.That(noRebound.VerticalRoutes.Any(r => r.Kind == ProceduralVerticalKind.ReboundClimb), Is.False);
            Assert.DoesNotThrow(() => ProceduralStoreyUtility.Validate(noRebound, _config));
        }

        [Test]
        public void DisabledVerticalObjectivesKeepOriginalGroundTypesAndPositions()
        {
            Set("_oneCellWeight", 0f); Set("_twoCellWeight", 1f); Set("_threeCellWeight", 0f);
            Set("_verticalPreference", 0f); Set("_storeyProbability", 1f);
            var raised = Generate(19);
            Assert.That(raised.Storeys, Is.Not.Empty);
            Set("_storeyProbability", 0f);
            Assert.That(raised.Graph.Anchors, Is.EqualTo(Generate(19).Graph.Anchors));
        }

        [Test]
        public void RequiredAnchorBehindOneWayDropWithoutReturnIsRejectedAndRetryReportsIt()
        {
            Set("_oneCellWeight", 0f); Set("_twoCellWeight", 1f); Set("_threeCellWeight", 0f); Set("_storeyProbability", 1f);
            var layout = Generate(17);
            var s = layout.Storeys[0];
            var upper = s.Origin + new Vector3(-3f, s.Height + _config.AnchorHeight, 2.6f);
            SetLayout(layout, nameof(layout.PlayerSpawnPosition), upper);
            SetLayout(layout, nameof(layout.Graph), LevelGraphUtility.Build(layout.Graph.Rooms, layout.Graph.Edges,
                layout.Graph.Anchors, s.RoomId, upper));
            // Keep rebound as a deliberate trap: optional abilities cannot rescue required validation.
            SetLayout(layout, nameof(layout.VerticalRoutes), layout.VerticalRoutes.Where(r => r.RoomId != s.RoomId ||
                r.Kind == s.Drop || r.Kind == ProceduralVerticalKind.ReboundClimb).ToArray());
            var failure = Assert.Throws<InvalidOperationException>(() => ProceduralStoreyUtility.Validate(layout, _config));
            Assert.That(failure.Message, Does.Contain("one-way trap"));
            var state = new ProceduralBehaviorState();
            var retry = new ProceduralGenerationController(state); retry.Begin(17, 3, 1);
            Assert.That(retry.Fail(failure.Message, ProceduralStoreyUtility.Manifest(layout, _config)), Is.True);
            Assert.That(state.AttemptSeed, Is.EqualTo(unchecked(17 + ProceduralGenerationController.SeedStride)));
            Assert.That(state.GenerationManifest, Does.Contain(Uri.EscapeDataString(failure.Message)));
            Assert.DoesNotThrow(() => ProceduralStoreyUtility.Validate(Generate(state.AttemptSeed), _config));
            Assert.That(retry.Fail(failure.Message, layout.Manifest), Is.False);
            Assert.That(state.UsedFallback, Is.True); Assert.That(state.GenerationSucceeded, Is.False);
        }

        [TestCase("_storeyHeight", float.NaN)] [TestCase("_storeyHeight", 4f)]
        [TestCase("_storeyProbability", -1f)] [TestCase("_baseLedgeMaximumHeight", 1f)]
        [TestCase("_baseLedgeReach", 0.5f)] [TestCase("_baseLedgeMinimumHeight", float.PositiveInfinity)]
        public void UnsupportedStoreyAndMovementMirrorsFailClosed(string name, float value)
        { Set(name, value); Assert.Throws<ArgumentException>(() => Generate(19)); }
    }
}
