// ============================================================================
// ProceduralNavFallbackPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Exercises managed navigation preflight with explicit collision commands.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Reject obstructed sockets and sealed walking doors before native baking.
//   - Keep diagnostic labels and organic/multistorey admission semantics intact.
// DEPENDENCIES:
//   - NUnit, Core graph values and Domain.Procedural.
// USAGE NOTES:
//   These synthetic commands test preflight only, not native NavMesh connectivity.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralNavFallbackPresenterTests
    {
        [TestCase("player spawn")] [TestCase("exit room=1")]
        [TestCase("cake anchor=3 room=1")] [TestCase("hunter spawn=0")]
        public void ObstructedRequiredSocketIsRejectedWithItsIdentity(string label)
        {
            var layout = Layout(); var presenter = new ProceduralNavFallbackPresenter();
            var point = label == "player spawn" ? layout.PlayerSpawnPosition :
                presenter.RequiredPositions(layout).Single(p => p.label == label).position;
            var obstacle = new ProceduralBlock(1, ProceduralSurfaceKind.Wall, point + Vector3.up,
                new Vector3(.3f, 2f, .3f), pieceId: "fixture-pillar");
            var error = Assert.Throws<InvalidOperationException>(() => presenter.ValidateTemplate(layout, new[] { obstacle }, .3f, 1.8f));
            Assert.That(error.Message, Does.Contain(label)); Assert.That(error.Message, Does.Contain("fixture-pillar"));
        }

        [TestCase(false)] [TestCase(true)]
        public void SealedDoorRejectsEvenWhenEveryRequiredSocketIsClear(bool rotate)
        {
            var layout = Layout();
            Set(layout, "Doors", new[] { new ProceduralDoorPlan(1, 2, new Vector3(0f, 0f, 10f), !rotate) });
            var q = rotate ? new Quaternion(0f, (float)Math.Sqrt(.5), 0f, (float)Math.Sqrt(.5)) : Quaternion.identity;
            var seal = new ProceduralBlock(2, ProceduralSurfaceKind.Wall, new Vector3(0f, 1f, 10f), new Vector3(4f, 2f, .3f), rotation: q);
            Assert.That(Assert.Throws<InvalidOperationException>(() => new ProceduralNavFallbackPresenter()
                .ValidateTemplate(layout, new[] { seal }, .3f, 1.8f)).Message, Does.Contain("door 1->2"));
        }

        [Test]
        public void RadiusOverlapRejectsButFloorsHeadersAndVisualsDoNot()
        {
            var layout = Layout(); var presenter = new ProceduralNavFallbackPresenter();
            var pillar = new ProceduralBlock(1, ProceduralSurfaceKind.Wall, new Vector3(.35f, 1f, 0f), new Vector3(.2f, 2f, .2f));
            Assert.Throws<InvalidOperationException>(() => presenter.ValidateTemplate(layout, new[] { pillar }, .3f, 1.8f));
            var clear = new[] {
                new ProceduralBlock(1, ProceduralSurfaceKind.Floor, new Vector3(0f, -.1f, 0f), new Vector3(40f, .2f, 40f)),
                new ProceduralBlock(1, ProceduralSurfaceKind.Wall, new Vector3(0f, 3f, 0f), new Vector3(4f, .2f, 4f)),
                new ProceduralBlock(1, ProceduralSurfaceKind.Wall, Vector3.up, Vector3.one, role: ProceduralBlockRole.VisualOnly) };
            Assert.DoesNotThrow(() => presenter.ValidateTemplate(layout, clear, .3f, 1.8f));
            Set(layout, "TemplateRooms", Array.Empty<ProceduralTemplateRoom>());
            Assert.DoesNotThrow(() => presenter.ValidateTemplate(layout, new[] { pillar }, .3f, 1.8f),
                "Organic floors keep ramp-aware native validation, not a flat template preflight.");
        }

        [TestCase(0f, 1.8f)] [TestCase(.3f, float.NaN)] [TestCase(float.PositiveInfinity, 1.8f)]
        public void InvalidAgentDimensionsCannotAdmitTemplates(float radius, float height)
            => Assert.Throws<ArgumentException>(() => new ProceduralNavFallbackPresenter()
                .ValidateTemplate(Layout(), Array.Empty<ProceduralBlock>(), radius, height));

        private static ProceduralLayout Layout()
        {
            var layout = new ProceduralLayout();
            Set(layout, "TemplateRooms", new[] { new ProceduralTemplateRoom() });
            Set(layout, "Graph", new LevelGraph(Array.Empty<LevelRoom>(), Array.Empty<LevelEdge>(),
                new[] { new LevelAnchor(3, 1, CakeAnchorType.Flow, new Vector3(8f, .1f, 0f)) }, 1, new Vector3(4f, 0f, 0f)));
            Set(layout, "PlayerSpawnPosition", Vector3.zero);
            Set(layout, "HunterSpawnPositions", new[] { new Vector3(12f, .1f, 0f) });
            Set(layout, "Doors", Array.Empty<ProceduralDoorPlan>());
            return layout;
        }
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    }
}
