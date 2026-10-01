// ============================================================================
// ProceduralNavigationPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies source-area classification and enabled partition-link permissions.
//   These tests keep vertical shortcuts out of every hunter link catalogue and
//   reject masks that would let ordinary hunters take partition-only routes.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check source roles, configured area/mask validation and horizontal link endpoints.
// DEPENDENCIES:
//   - Core, Domain.Procedural, NUnit and temporary Unity configuration instances.
// USAGE NOTES:
//   Default link installation is enabled; ordinary admission remains mask 1.
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
    public sealed class ProceduralNavigationPresenterTests
    {
        private ProceduralConfig _config;
        private ProceduralDriverConfig _driver;
        [SetUp] public void SetUp()
        { _config = ScriptableObject.CreateInstance<ProceduralConfig>(); _driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>(); }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(_config); UnityEngine.Object.DestroyImmediate(_driver); }

        [Test]
        public void PlayerCollisionRemainsAnObstacleAndPartitionLinksNeverChangeStorey()
        {
            var presenter = new ProceduralNavigationPresenter();
            Assert.That(_driver.EnablePartitionIgnoringLinks, Is.True);
            Assert.DoesNotThrow(() => presenter.Validate(_driver));
            foreach (ProceduralBlockRole role in Enum.GetValues(typeof(ProceduralBlockRole)))
            {
                var b = new ProceduralBlock(1, ProceduralSurfaceKind.Floor, Vector3.zero, Vector3.one, role: role);
                Assert.That(presenter.Area(b), Is.EqualTo(role == ProceduralBlockRole.PlayerOnly ? 1 : 0));
            }
            var layout = new ProceduralController(new ProceduralBehaviorState(), _config,
                new System.Random(ProceduralController.LayoutSeed(19, 3))).Generate(19, 3);
            var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driver);
            typeof(ProceduralLayout).GetProperty(nameof(layout.Interactables)).SetValue(layout,
                new ProceduralInteractablePresenter().Build(layout, _config, _driver, blocks, new System.Random(19)));
            var links = presenter.Links(layout, blocks, _driver);
            Assert.That(links, Is.Not.Empty);
            Assert.That(links.All(l => l.Area == _driver.PartitionIgnoringArea && l.Start.y == l.End.y), Is.True);
            Assert.That(links.All(l => (_driver.HunterAreaMask & (1 << l.Area)) == 0 &&
                (_driver.PartitionIgnoringAreaMask & (1 << l.Area)) != 0), Is.True);
            var balcony = new ProceduralBlock(1, ProceduralSurfaceKind.Wall, Vector3.zero, Vector3.one,
                99, TraversalSurfaceKind.Vault, Vector3.up * 3.2f, Vector3.forward);
            Assert.That(presenter.Links(layout, blocks.Concat(new[] { balcony }).ToArray(), _driver), Is.EqualTo(links));
        }

        [TestCase("_hunterAreaMask", -1)] [TestCase("_hunterAreaMask", 9)]
        [TestCase("_partitionIgnoringArea", 1)] [TestCase("_partitionIgnoringArea", 32)]
        [TestCase("_partitionIgnoringAreaMask", 1)]
        public void UnsafeOrInconsistentMasksAreRejected(string field, int value)
        {
            typeof(ProceduralDriverConfig).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_driver, value);
            Assert.Throws<ArgumentException>(() => new ProceduralNavigationPresenter().Validate(_driver));
        }
    }
}
