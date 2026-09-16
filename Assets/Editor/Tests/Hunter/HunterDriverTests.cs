// ============================================================================
// HunterDriverTests.cs
// ============================================================================
// PURPOSE:
//   Checks real capsule and ray behavior at the hunter engine boundary.
//   A wall must block swept contacts before a target behind it, while independent
//   head, chest and hips rays must preserve partially exposed sight samples.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Exercise actual Physics queries and committed active-lunge movement.
//   - Keep floor-level spawn contact from blocking a clear lunge or bypassing walls.
// DEPENDENCIES:
//   - Hunter Driver and config; UnityEditor serialized wiring and NUnit.
// USAGE NOTES:
//   Run only in the coordinator's admitted Unity test window. Temporary objects
//   live far from the arena and are removed symmetrically after every case.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterDriverTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<Collider> _contacts = new List<Collider>();
        private HunterMotorDriverConfig _config;
        private HunterDriver _driver;
        private readonly Vector3 _origin = new Vector3(1000f, 100f, 1000f);
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            Box("Ground", new Vector3(0f, -0.25f, 0f), new Vector3(20f, 0.5f, 20f));
            var root = new GameObject("Hunter Physics Fixture"); _objects.Add(root);
            root.transform.position = _origin + Vector3.up * 0.02f;
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f; capsule.radius = 0.4f; capsule.center = Vector3.up * 0.9f;
            root.AddComponent<Rigidbody>();
            _driver = root.AddComponent<HunterDriver>();
            var serialized = new SerializedObject(_driver); serialized.FindProperty("_config").objectReferenceValue = _config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _driver.Initialize(); _driver.OnLungeContact += Contact; Physics.SyncTransforms();
        }
        [TearDown] public void TearDown()
        {
            if (_driver != null) _driver.OnLungeContact -= Contact;
            foreach (GameObject item in _objects) if (item != null) Object.DestroyImmediate(item);
            _objects.Clear(); _contacts.Clear(); Object.DestroyImmediate(_config);
        }
        private void Contact(Collider collider) { _contacts.Add(collider); }
        private Collider Box(string name, Vector3 offset, Vector3 size)
        {
            var root = new GameObject(name); _objects.Add(root);
            root.transform.position = _origin + offset; var box = root.AddComponent<BoxCollider>(); box.size = size; return box;
        }
        private Collider Target()
        {
            var root = new GameObject("Target"); _objects.Add(root); root.transform.position = _origin + Vector3.forward * 2f;
            var collider = root.AddComponent<CapsuleCollider>(); collider.height = 1.8f; collider.radius = 0.35f;
            collider.center = Vector3.up * 0.9f; return collider;
        }
        [Test] public void SweptLungeReportsReachableTargetAndDoesNotTunnel()
        {
            Collider target = Target(); Physics.SyncTransforms();
            _driver.Move(_origin, 0f, 20f, 240f, 0.3f, false, true, Vector3.forward, 18f, 4f);
            Assert.That(_contacts, Does.Contain(target));
            Assert.That(_driver.Position.z, Is.LessThan(_origin.z + 2f));
        }
        [Test] public void WallPreventsContactWithTargetBehindIt()
        {
            Collider target = Target(); Collider wall = Box("Wall", new Vector3(0f, 1.5f, 1f), new Vector3(4f, 3f, 0.2f));
            Physics.SyncTransforms();
            _driver.Move(_origin, 0f, 20f, 240f, 0.3f, false, true, Vector3.forward, 18f, 4f);
            Assert.That(_contacts, Does.Contain(wall)); Assert.That(_contacts.Contains(target), Is.False);
            Assert.That(_driver.Position.z, Is.LessThan(_origin.z + 1f));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void FloorLevelSpawnMovesAlongGroundAndStillRespectsWalls(bool wallBlocks)
        {
            _driver.transform.position = _origin;
            _driver.GetComponent<Rigidbody>().position = _origin;
            _driver.Initialize();
            Collider target = Target();
            if (wallBlocks) Box("Wall", new Vector3(0f, 1.5f, 1f), new Vector3(4f, 3f, 0.2f));
            Physics.SyncTransforms();
            for (int tick = 0; tick < 18; tick++)
                _driver.Move(_origin, 0f, 20f, 240f, 1f / 60f, false, true, Vector3.forward, 18f, 4f);
            Assert.That(_driver.Position.z, Is.GreaterThan(_origin.z + 0.1f),
                "The tangent floor must not become a zero-distance frontal obstacle.");
            Assert.That(_driver.Position.y, Is.EqualTo(_origin.y).Within(0.001f));
            Assert.That(_contacts.Contains(target), Is.EqualTo(!wallBlocks));
            Assert.That(_driver.Position.z, Is.LessThan(_origin.z + (wallBlocks ? 1f : 2f)));
        }
        [Test] public void SightTestsIndependentBodySamples()
        {
            Collider target = Target(); Physics.SyncTransforms();
            SightProbe clear = _driver.ProbeSight(_origin + Vector3.forward * 2f, other => other == target);
            Assert.That(clear.HeadVisible && clear.ChestVisible && clear.HipsVisible, Is.True);
            Box("Low Cover", new Vector3(0f, 0.45f, 1.5f), new Vector3(2f, 0.9f, 0.1f)); Physics.SyncTransforms();
            SightProbe partial = _driver.ProbeSight(_origin + Vector3.forward * 2f, other => other == target);
            Assert.That(partial.HeadVisible, Is.True); Assert.That(partial.ChestVisible, Is.True);
            Assert.That(partial.HipsVisible, Is.False);
        }
        [Test] public void RecoveryCommandProducesNoPlanarMovement()
        {
            Vector3 position = _driver.Position;
            _driver.Move(_origin, 10f, 20f, 240f, 0.1f, true, false, Vector3.forward, 18f, 4f);
            Assert.That(_driver.Position.x, Is.EqualTo(position.x)); Assert.That(_driver.Position.z, Is.EqualTo(position.z));
        }
        [Test] public void EmptyRayDoesNotInventAVisibleTargetBody()
        {
            SightProbe empty = _driver.ProbeSight(_origin + Vector3.forward * 2f, other => false);
            Assert.That(empty.HeadVisible || empty.ChestVisible || empty.HipsVisible, Is.False);
        }
    }
}
