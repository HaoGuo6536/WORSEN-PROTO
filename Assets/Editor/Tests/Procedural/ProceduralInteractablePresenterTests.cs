// ============================================================================
// ProceduralInteractablePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks deterministic world-object placement against the actual generated shell.
//   Compatibility tests keep Light snapshots aligned with existing torch sockets
//   while the presentation-owner binding is integrated separately.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Protect exit portals and required routes; check density and stable identities.
//   - Match partitions to existing collision and lights to Environment placement.
//   - Scope the legacy rectangular Environment socket contract to one-cell rooms.
//   - Compare exact multi-cell torch sockets and stable ids, excluding seams and notches.
// DEPENDENCIES:
//   - Core, Domain.Procedural/Level, Presentation.Environment and NUnit.
// USAGE NOTES:
//   Temporary configs only. Engine/navigation integration remains a coordinator gate.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Domain.Procedural;
using Worsen.Presentation.Environment;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralInteractablePresenterTests
    {
        [TestCase(false)] [TestCase(true)]
        public void SeedSamplePreservesRoutesAndMatchesExistingSockets(bool castle)
        {
            var config = ScriptableObject.CreateInstance<ProceduralConfig>();
            var driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            try
            {
                var settings = new SerializedObject(config);
                settings.FindProperty("_castleModules").boolValue = castle;
                settings.FindProperty("_ordinaryDoorFraction").floatValue = 1f;
                settings.FindProperty("_twoCellWeight").floatValue = 0f;
                settings.FindProperty("_threeCellWeight").floatValue = 0f;
                settings.FindProperty("_gapProbability").floatValue = 0f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var presenter = new ProceduralInteractablePresenter();
                for (int seed = 0; seed < 32; seed++)
                {
                    var layout = new ProceduralController(new ProceduralBehaviorState(), config,
                        new System.Random(ProceduralController.LayoutSeed(seed, 3))).Generate(seed, 3);
                    var blocks = new ProceduralGeometryPresenter().Build(layout, config, driver);
                    var plans = presenter.Build(layout, config, driver, blocks, new System.Random(seed));
                    Assert.That(presenter.Manifest(plans), Is.EqualTo(presenter.Manifest(
                        presenter.Build(layout, config, driver, blocks, new System.Random(seed)))));
                    Assert.That(plans.Select(p => p.State.Id).Distinct().Count(), Is.EqualTo(plans.Count));
                    new LevelInteractableController(new LevelInteractableBehaviorState()).Load(layout.Graph, plans.Select(p => p.State).ToArray());
                    var doors = plans.Where(p => p.State.Kind == InteractableKind.Door).ToArray();
                    Assert.That(doors.Length, Is.EqualTo(layout.Doors.Count(d => !d.IsOptional &&
                        d.FromRoomId != layout.Graph.ExitRoomId && d.ToRoomId != layout.Graph.ExitRoomId)));
                    foreach (var plan in doors)
                    {
                        var edge = layout.Graph.Edges.Single(e => e.Id == plan.State.EdgeId);
                        Assert.That(edge.Access, Is.EqualTo(TraversalAccess.All));
                        Assert.That(plan.State.Value, Is.EqualTo(InteractableStateValue.Open));
                        Assert.That(edge.FromRoomId, Is.Not.EqualTo(layout.Graph.ExitRoomId));
                        Assert.That(edge.ToRoomId, Is.Not.EqualTo(layout.Graph.ExitRoomId));
                    }
                    Assert.That(plans.Count(p => p.State.Kind == InteractableKind.ThresholdMark), Is.EqualTo(layout.Doors.Count(d => !d.IsOptional)));
                    Assert.That(plans.Where(p => p.State.Kind == InteractableKind.ThresholdMark).All(p => p.State.Value == InteractableStateValue.Inactive), Is.True);
                    Assert.That(plans.Where(p => p.State.Kind == InteractableKind.Partition).Select(p => p.SurfaceId),
                        Is.EquivalentTo(blocks.Where(b => b.TraversalKind == TraversalSurfaceKind.Vault).Select(b => b.SurfaceId)));
                    foreach (var actor in new[] { TraversalAccess.Player, TraversalAccess.Hunter })
                        Assert.That(LevelGraphUtility.DistancesTo(layout.Graph, layout.Graph.ExitRoomId, actor).Values.All(d => d >= 0), Is.True);
                    foreach (var room in layout.Graph.Rooms)
                    {
                        var props = plans.Where(p => p.State.RoomId == room.Id && p.State.Kind == InteractableKind.KnockableProp).ToArray();
                        Assert.That(props.Length, Is.EqualTo(config.KnockablePropsPerRoom));
                        foreach (var prop in props)
                        {
                            Vector3 delta = prop.State.Position - room.Center;
                            Assert.That(Mathf.Abs(delta.x) - prop.Size.x * 0.5f, Is.GreaterThan(room.Size.x * 0.5f - config.CandidatePerimeterInset));
                            Assert.That(Mathf.Abs(delta.z) - prop.Size.z * 0.5f, Is.GreaterThan(room.Size.z * 0.5f - config.CandidatePerimeterInset));
                        }
                        var portals = layout.Doors.Where(d => d.FromRoomId == room.Id || d.ToRoomId == room.Id).Select(d => d.Center).ToArray();
                        var torches = EnvironmentPresenter.BuildSlots(room.Id, room.Bounds, portals).Where(s => s.Torch).Select(s => s.Position);
                        Assert.That(plans.Where(p => p.State.RoomId == room.Id && p.State.Kind == InteractableKind.Light).Select(p => p.State.Position), Is.EquivalentTo(torches));
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(driver); }
        }

        [TestCase(2, false)] [TestCase(3, false)] [TestCase(3, true)]
        public void MultiCellLightsMatchEnvironmentExactlyAndRetainSingleCellIdentities(int cells, bool bent)
        {
            var config = ScriptableObject.CreateInstance<ProceduralConfig>();
            var driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            try
            {
                var settings = new SerializedObject(config);
                settings.FindProperty("_oneCellWeight").floatValue = 0f;
                settings.FindProperty("_twoCellWeight").floatValue = cells == 2 ? 1f : 0f;
                settings.FindProperty("_threeCellWeight").floatValue = cells == 3 ? 1f : 0f;
                settings.FindProperty("_lShapeWeight").floatValue = bent ? 1f : 0f;
                settings.FindProperty("_gapProbability").floatValue = 1f;
                settings.FindProperty("_pocketProbability").floatValue = 1f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var presenter = new ProceduralInteractablePresenter();
                int extensionLights = 0;
                for (int seed = 0; seed < 12; seed++)
                {
                    var layout = new ProceduralController(new ProceduralBehaviorState(), config,
                        new System.Random(ProceduralController.LayoutSeed(seed, 3))).Generate(seed, 3);
                    var blocks = new ProceduralGeometryPresenter().Build(layout, config, driver);
                    var plans = presenter.Build(layout, config, driver, blocks, new System.Random(seed));
                    Assert.That(plans.Select(p => p.State.Id).Distinct().Count(), Is.EqualTo(plans.Count));
                    Assert.That(plans, Is.EqualTo(presenter.Build(layout, config, driver, blocks, new System.Random(seed))));
                    foreach (var room in layout.Graph.Rooms)
                    {
                        var portals = layout.Doors.Where(d => d.FromRoomId == room.Id || d.ToRoomId == room.Id).Select(d => d.Center).ToArray();
                        var lights = plans.Where(p => p.State.RoomId == room.Id && p.State.Kind == InteractableKind.Light).ToArray();
                        var torches = EnvironmentPresenter.BuildSlots(room.Id, room.Bounds, portals, room.Cells).Where(s => s.Torch).ToArray();
                        Assert.That(lights.Select(p => p.State.Position), Is.EquivalentTo(torches.Select(s => s.Position)));
                        Assert.That(lights.Select(p => p.State.Position).Distinct().Count(), Is.EqualTo(lights.Length));
                        foreach (var light in lights) Assert.That(room.ContainsXZ(light.State.Position), Is.True);
                        extensionLights += lights.Count(p => p.State.Id >= 4000000);
                        if (room.Cells.Count != 1) continue;
                        foreach (var light in lights)
                        {
                            int index = light.State.Id - 400000 - room.Id * 10;
                            Assert.That(index, Is.InRange(0, 7));
                            var position = room.Center; position.y = room.Bounds.min.y + 2.75f;
                            float offset = index % 2 == 0 ? -.28f : .28f;
                            if (index / 2 == 0 || index / 2 == 2)
                            { position.x += room.Size.x * offset; position.z = index / 2 == 0 ? room.Bounds.min.z + .3f : room.Bounds.max.z - .3f; }
                            else
                            { position.z += room.Size.z * offset; position.x = index / 2 == 1 ? room.Bounds.max.x - .3f : room.Bounds.min.x + .3f; }
                            Assert.That(light.State.Position, Is.EqualTo(position));
                        }
                    }
                }
                Assert.That(extensionLights, Is.GreaterThan(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(driver); }
        }

        [Test]
        public void ZeroDensityRemovesDoorsAndPropsWithoutChangingTopology()
        {
            var config = ScriptableObject.CreateInstance<ProceduralConfig>();
            var driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            try
            {
                var settings = new SerializedObject(config);
                settings.FindProperty("_ordinaryDoorFraction").floatValue = 0f;
                settings.FindProperty("_knockablePropsPerRoom").intValue = 0;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var layout = new ProceduralController(new ProceduralBehaviorState(), config, new System.Random(7)).Generate(7, 1);
                var plans = new ProceduralInteractablePresenter().Build(layout, config, driver,
                    new ProceduralGeometryPresenter().Build(layout, config, driver), new System.Random(7));
                Assert.That(plans.Any(p => p.State.Kind == InteractableKind.Door || p.State.Kind == InteractableKind.KnockableProp), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(driver); }
        }
    }
}
