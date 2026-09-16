// ============================================================================
// HunterRangedDriverTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Hunter behavior with explicit reproducible fixtures.
//   Tests exercise observable light, physical attacks, route admission or creature
//   animation contracts without changing authored gameplay assets.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Coordinator runs Unity tests with the exclusive lease. Fixtures clean up their own objects.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterRangedDriverTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<Collider> _contacts = new List<Collider>();
        private readonly Vector3 _origin = new Vector3(2400f, 120f, 2400f);
        private HunterAttackDriver _driver;
        private Collider Box(string name, Vector3 position, Vector3 size)
        { var go = new GameObject(name); _objects.Add(go); go.transform.position = position; var box = go.AddComponent<BoxCollider>(); box.size = size; return box; }
        [SetUp] public void SetUp()
        {
            var root = new GameObject("Attack fixture"); _objects.Add(root); root.transform.position = _origin;
            _driver = root.AddComponent<HunterAttackDriver>(); _driver.Initialize(); _driver.SetTargetFilter(collider => collider.gameObject.name == "Target"); _driver.OnContact += Contact;
            Box("Floor", _origin + Vector3.down * 0.25f, new Vector3(30, 0.5f, 30));
        }
        private void Contact(Collider collider, int serial) { _contacts.Add(collider); }
        [TearDown] public void TearDown()
        {
            _driver.OnContact -= Contact; _driver.Teardown();
            foreach (GameObject item in _objects) if (item != null) Object.DestroyImmediate(item);
            _objects.Clear(); _contacts.Clear();
        }
        [TestCase(false)] [TestCase(true)]
        public void BoltsTravelAfterWarningAndStopAtWall(bool wall)
        {
            Collider target = Box("Target", _origin + new Vector3(0, 0.9f, 8), new Vector3(0.8f, 1.8f, 0.8f));
            if (wall) Box("Wall", _origin + new Vector3(0, 1.5f, 4), new Vector3(5, 3, 0.3f));
            Physics.SyncTransforms();
            _driver.BeginWarning(HunterAttackStyle.Projectile, 1, _origin + Vector3.forward * 8, 12, 0.2f, false, false);
            _driver.Tick(1f); Assert.That(_contacts, Is.Empty);
            _driver.Fire(10, 0.2f); _driver.Tick(0.1f); Assert.That(_contacts, Is.Empty);
            for (int i = 0; i < 14; i++) _driver.Tick(0.1f);
            Assert.That(_contacts.Contains(target), Is.EqualTo(!wall));
        }
        [TestCase(false)] [TestCase(true)]
        public void SpikesUseWarnedPositionSoMovingAwayDodges(bool dodge)
        {
            Vector3 point = _origin + Vector3.forward * 5;
            Collider target = Box("Target", point + Vector3.up * 0.9f, new Vector3(0.8f, 1.8f, 0.8f));
            Physics.SyncTransforms(); _driver.BeginWarning(HunterAttackStyle.GroundSpikes, 1, point, 12, 1f, false, false);
            Assert.That(_contacts, Is.Empty);
            if (dodge) target.transform.position += Vector3.right * 4f;
            Physics.SyncTransforms(); _driver.Fire(10, 0.2f);
            Assert.That(_contacts.Contains(target), Is.EqualTo(!dodge));
        }
        [Test] public void SpikeWarningCannotJumpOntoASeparateFloor()
        {
            Vector3 point = _origin + new Vector3(0, 5, 5);
            Box("Upper floor", point + Vector3.down * 0.25f, new Vector3(5, 0.5f, 5));
            Collider target = Box("Target", point + Vector3.up * 0.9f, new Vector3(0.8f, 1.8f, 0.8f));
            Physics.SyncTransforms(); _driver.BeginWarning(HunterAttackStyle.GroundSpikes, 1, point, 12, 1f, false, false); _driver.Fire(10, 0.2f);
            Assert.That(_contacts.Contains(target), Is.False);
        }
    }
}
