// ============================================================================
// FogDensityPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Specifies the pure density field's topology, shape and incremental behavior.
//   Tests use synthetic Core facts without a renderer, scene or gameplay simulation.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Cover clear fields, portal leakage, wall isolation, profiles and bounded grids.
// DEPENDENCIES:
//   - Core, Presentation Fog, NUnit and transient config ScriptableObjects.
// USAGE NOTES:
//   Edit Mode tests. Texture upload, shader correctness and frame time are separate
//   coordinator gates; these assertions make no live rendering claims.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Fog;

namespace Worsen.Tests.Fog
{
    public sealed class FogDensityPresenterTests
    {
        private FogDriverConfig _config;
        [SetUp] public void SetUp() => _config = ScriptableObject.CreateInstance<FogDriverConfig>();
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_config);

        [Test] public void ZeroProgressIsZeroEverywhere()
        {
            var state = Build(); FogDensityPresenter.Rebuild(state, _config, true);
            Assert.That(state.Density.All(value => value == 0), Is.True);
        }
        [Test] public void CollapsedRoomLeaksOnlyThroughPortals()
        {
            var state = Build(); Set(state, 0, 1f);
            Assert.That(At(state, 0, 0, .4f, 0), Is.GreaterThan(.8f));
            Assert.That(At(state, 1, 6.5f, 1f, 0), Is.GreaterThan(.8f));
            Assert.That(state.Rooms[2].Voxels.All(index => state.Density[index] == 0), Is.True, "Wall-only neighbor must stay clear.");
            Assert.That(state.Rooms[3].Voxels.All(index => state.Density[index] == 0), Is.True, "Leaked density does not seed a second hop.");
            Assert.That(At(state, 0, 0, .5f, 5.9f), Is.Zero, "Guard stops interpolation across solid wall.");
        }
        [Test] public void FloorExceedsCeilingAndMouthHasRoundedSaturatedCore()
        {
            var state = Build(); Set(state, 0, 1f);
            Assert.That(At(state, 0, 0, .4f, 0), Is.GreaterThan(At(state, 0, 0, 3.8f, 0)));
            Assert.That(At(state, 0, 6, 1.5f, 0), Is.EqualTo(1f));
            Assert.That(At(state, 1, 6, 1.5f, 0), Is.EqualTo(1f));
            Assert.That(At(state, 0, 6, 2.8f, 1.4f), Is.Zero, "Rounded upper corner, not a rectangle.");
            Assert.That(At(state, 0, 6, 4, 0), Is.Zero, "No ceiling attachment.");
        }
        [Test] public void NeighborProgressThickensAndExtendsLeak()
        {
            var state = Build(); Set(state, 0, 1f);
            float before = At(state, 1, 9, 1, 0);
            Set(state, 1, .5f);
            Assert.That(At(state, 1, 9, 1, 0), Is.GreaterThan(before));
        }
        [Test] public void IncrementalEqualsFullRebuildIncludingProgressDecreases()
        {
            var incremental = Build(); var full = Build(); var random = new System.Random(1848);
            for (int update = 0; update < 50; update++)
            {
                int room = random.Next(4); float progress = (float)random.NextDouble();
                FogDensityPresenter.SetProgress(incremental, room, progress, _config);
                FogDensityPresenter.SetProgress(full, room, progress, _config);
                FogDensityPresenter.Rebuild(incremental, _config);
                FogDensityPresenter.Rebuild(full, _config, true);
                CollectionAssert.AreEqual(full.Density, incremental.Density);
            }
        }
        [Test] public void EpsilonAccumulatesAgainstAcceptedProgressAndEndpointsAlwaysCommit()
        {
            var state = Build(); state.UploadPending = false;
            Assert.That(FogDensityPresenter.SetProgress(state, 0, .005f, _config), Is.False);
            FogDensityPresenter.Rebuild(state, _config);
            Assert.That(state.LastRebuiltRooms, Is.Zero); Assert.That(state.UploadPending, Is.False);
            Set(state, 0, .02f); Assert.That(state.LastRebuiltRooms, Is.EqualTo(2));
            Set(state, 0, .995f); Set(state, 0, 1f);
            Assert.That(state.Rooms[0].Progress, Is.EqualTo(1f));
            Set(state, 0, 0f);
            Assert.That(state.Density.All(value => value == 0), Is.True);
            Assert.That(FogDensityPresenter.SetProgress(state, 0, float.NaN, _config), Is.False);
            Assert.That(FogDensityPresenter.SetProgress(state, 999, 1f, _config), Is.False);
        }
        [Test] public void LargeFloorScalesVoxelsInsteadOfExceedingCap()
        {
            var room = new GeneratedRoomSample(0, new Bounds(Vector3.zero, new Vector3(1000, 100, 2000)), false, false, Array.Empty<Vector3>());
            var graph = new LevelGraph(new[] { new LevelRoom(0, room.Bounds.center, room.Bounds.size) }, Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 0, Vector3.zero);
            var state = FogDensityPresenter.Build(new[] { room }, graph, _config);
            Assert.That(state.Size.x, Is.LessThanOrEqualTo(128)); Assert.That(state.Size.y, Is.LessThanOrEqualTo(16)); Assert.That(state.Size.z, Is.LessThanOrEqualTo(128));
            Assert.That(state.Voxel.x, Is.GreaterThan(1f)); Assert.That(state.Voxel.y, Is.GreaterThan(.75f)); Assert.That(state.Voxel.z, Is.GreaterThan(1f));
        }
        [Test] public void GraphWithoutMatchedOpeningDoesNotInventPortal()
        {
            var state = Build(false); Set(state, 0, 1f);
            Assert.That(state.UnmatchedEdges, Is.EqualTo(1));
            Assert.That(state.Rooms[1].Voxels.All(index => state.Density[index] == 0), Is.True);
        }
        [Test] public void InputOrderingDoesNotChangeVoxelOwnership()
        {
            Inputs(true, out var rooms, out var graph);
            var a = FogDensityPresenter.Build(rooms, graph, _config);
            var b = FogDensityPresenter.Build(rooms.Reverse().ToArray(), graph, _config);
            Set(a, 0, 1f); Set(b, 0, 1f); CollectionAssert.AreEqual(a.Density, b.Density);
        }
        private FogDriverState Build(bool portals = true)
        { Inputs(portals, out var rooms, out var graph); return FogDensityPresenter.Build(rooms, graph, _config); }
        private void Set(FogDriverState state, int id, float value)
        { FogDensityPresenter.SetProgress(state, id, value, _config); FogDensityPresenter.Rebuild(state, _config); }
        private float At(FogDriverState state, int room, float x, float y, float z) => FogDensityPresenter.Evaluate(state, room, new Vector3(x, y, z), _config);
        private static void Inputs(bool matching, out GeneratedRoomSample[] samples, out LevelGraph graph)
        {
            Vector3 first = new Vector3(6, 0, 0), second = new Vector3(18, 0, 0);
            var rooms = new[] { new LevelRoom(0, new Vector3(0, 2, 0), new Vector3(12, 4, 12)),
                new LevelRoom(1, new Vector3(12, 2, 0), new Vector3(12, 4, 12)),
                new LevelRoom(2, new Vector3(0, 2, 12), new Vector3(12, 4, 12)),
                new LevelRoom(3, new Vector3(24, 2, 0), new Vector3(12, 4, 12)) };
            samples = new[] { new GeneratedRoomSample(0, rooms[0].Bounds, false, false, new[] { first }),
                new GeneratedRoomSample(1, rooms[1].Bounds, false, false, matching ? new[] { first, second } : new[] { second }),
                new GeneratedRoomSample(2, rooms[2].Bounds, false, false, Array.Empty<Vector3>()),
                new GeneratedRoomSample(3, rooms[3].Bounds, false, false, new[] { second }) };
            graph = new LevelGraph(rooms, new[] { new LevelEdge(0, 0, 1, true), new LevelEdge(1, 1, 3, true) }, Array.Empty<LevelAnchor>(), 3, rooms[3].Center);
        }
    }
}
