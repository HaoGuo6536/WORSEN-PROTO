// ============================================================================
// ProceduralDriverTests.cs
// ============================================================================
// PURPOSE:
//   Verifies runtime generation against actual Unity colliders and navigation.
//   Test maps are placed far outside authored scenes so an unrelated navigation
//   asset cannot make a broken procedural floor appear reachable.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check native door/cake clearance, enclosed ceilings and complete paths.
//   - Verify regeneration and teardown remove owned geometry/navigation.
// DEPENDENCIES:
//   - Domain.Procedural, Core, NUnit, UnityEditor configuration and UnityEngine.AI.
// USAGE NOTES:
//   Requires the repository Unity testing lease. Removes only this fixture's
//   objects/data; never clears global navigation or another owner's scene.
// ============================================================================
using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Domain.Procedural;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Floor;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralDriverTests
    {
        private ProceduralConfig _config;
        private ProceduralDriverConfig _driverConfig;
        private GameObject _owner;
        private ProceduralManager _manager;
        private GameObject _actor;
        private PlayerProfile _playerProfile;
        private PlayerMoverDriverConfig _playerDriverConfig;
        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ProceduralConfig>();
            _driverConfig = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_castleModules").boolValue = false;
            serialized.FindProperty("_initialRoomCount").intValue = 5;
            serialized.FindProperty("_origin").vector2Value = new Vector2(10000f, 10000f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _owner = new GameObject("Procedural native verification owner");
            _manager = _owner.AddComponent<ProceduralManager>();
        }
        [TearDown]
        public void TearDown()
        {
            if (_actor != null) Object.DestroyImmediate(_actor);
            if (_playerProfile != null) Object.DestroyImmediate(_playerProfile);
            if (_playerDriverConfig != null) Object.DestroyImmediate(_playerDriverConfig);
            if (_manager != null) _manager.Teardown();
            if (_owner != null) Object.DestroyImmediate(_owner);
            Object.DestroyImmediate(_config); Object.DestroyImmediate(_driverConfig);
        }

        [TestCase(4, 1)] [TestCase(87, 4)]
        public void GeneratesPhysicalEnclosureAndUsableNavigationBeforeAdmission(int seed, int round)
        {
            _manager.Initialize(_config, _driverConfig, seed, round);
            Assert.That(_manager.IsReady, Is.True);
            Assert.That(_manager.TraversalMarkers.Count, Is.EqualTo(_manager.Graph.Rooms.Count * 3));
            foreach (var anchor in _manager.Graph.Anchors)
            {
                Assert.That(Physics.Raycast(anchor.Position + Vector3.up, Vector3.down, out var floor, 2f), Is.True);
                Assert.That(floor.point.y, Is.EqualTo(0f).Within(0.01f));
                Assert.That(Physics.CheckCapsule(anchor.Position + Vector3.up * 0.31f,
                    anchor.Position + Vector3.up * 1.49f, 0.3f, ~0, QueryTriggerInteraction.Ignore), Is.False);
            }
            var generated = new ProceduralController(new ProceduralBehaviorState(), _config,
                new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);
            foreach (var door in generated.Doors)
                Assert.That(Physics.CheckCapsule(door.Center + Vector3.up * 0.31f,
                    door.Center + Vector3.up * 1.49f, 0.3f, ~0, QueryTriggerInteraction.Ignore), Is.False);
            Assert.That(Physics.Raycast(_manager.PlayerSpawnPosition + Vector3.up, Vector3.up, out var ceiling, 5f), Is.True);
            Assert.That(ceiling.point.y, Is.EqualTo(4f).Within(0.01f));
            Assert.That(NavMesh.SamplePosition(new Vector3(_manager.PlayerSpawnPosition.x, 4.3f,
                _manager.PlayerSpawnPosition.z), out _, 0.15f, new NavMeshQueryFilter { agentTypeID = 0, areaMask = 1 }), Is.False);
        }

        [Test]
        public void TeardownRemovesTheGeneratedMapAndCanGenerateAnotherRound()
        {
            _manager.Initialize(_config, _driverConfig, 51, 1);
            var oldSpawn = _manager.PlayerSpawnPosition;
            var firstManifest = _manager.LayoutManifest;
            _manager.Teardown();
            Assert.That(_manager.IsReady, Is.False); Assert.That(_manager.Graph, Is.Null);
            Assert.That(_owner.GetComponentsInChildren<Collider>().Length, Is.Zero);
            Assert.That(NavMesh.SamplePosition(oldSpawn, out _, 0.75f,
                new NavMeshQueryFilter { agentTypeID = 0, areaMask = 1 }), Is.False);
            _manager.Initialize(_config, _driverConfig, 51, 2);
            Assert.That(_manager.IsReady, Is.True); Assert.That(_manager.Graph.Rooms.Count, Is.EqualTo(7));
            Assert.That(_manager.LayoutManifest, Is.Not.EqualTo(firstManifest));
        }

        [TestCase(1, false)] [TestCase(1, true)] [TestCase(2, false)] [TestCase(2, true)]
        public void GeneratedVaultAndWindowSupportRealPlayerCompletionInBothDirections(int roomId, bool reverse)
        {
            _manager.Initialize(_config, _driverConfig, 19, 1);
            var surface = _owner.GetComponentsInChildren<ProceduralTraversalSurface>()
                .Single(value => value.SurfaceId == 50000 + roomId * 10 + 2);
            var across = (surface.EndpointB - surface.EndpointA).normalized * (reverse ? -1f : 1f);
            var entry = reverse ? surface.EndpointB : surface.EndpointA;
            var target = reverse ? surface.EndpointA : surface.EndpointB;
            var player = SpawnPlayer(entry - across * 0.6f, across);
            Assert.That(_actor.GetComponent<PlayerDriver>().Probe().VaultCandidate, Is.True);
            bool completed = false;
            for (int tick = 1; tick <= 18; tick++)
            {
                var buttons = InputButtons.Sprint | (tick == 1 ? InputButtons.Jump : InputButtons.None);
                player.Tick(new InputFrame(Vector2.up, Vector2.zero, buttons,
                    tick == 1 ? InputButtons.Jump : InputButtons.None,
                    tick == 2 ? InputButtons.Jump : InputButtons.None), 1f / 60f, tick);
                Physics.SyncTransforms();
                foreach (var fact in player.LastTraversalFacts.Where(value => value.Kind == TraversalKind.Vault))
                {
                    Assert.That(fact.Succeeded, Is.True, "The generated sill/window must be physically traversable.");
                    Assert.That(Vector3.Distance(player.ReadOnlyState.Position, target), Is.LessThanOrEqualTo(_playerProfile.VaultCompletionTolerance));
                    completed = true;
                }
            }
            Assert.That(completed, Is.True);
        }

        [Test]
        public void GeneratedSlideApertureBlocksStandingButPermitsRealPlayerSliding()
        {
            _manager.Initialize(_config, _driverConfig, 19, 1);
            var surface = _owner.GetComponentsInChildren<ProceduralTraversalSurface>()
                .Single(value => value.SurfaceId == 50032);
            var across = (surface.EndpointB - surface.EndpointA).normalized;
            var player = SpawnPlayer(surface.EndpointA - across * 3f, across);
            bool slidePressed = false, slid = false, clearanceBlocked = false, passed = false;
            for (int tick = 1; tick <= 90; tick++)
            {
                float approach = Vector3.Dot(surface.EndpointA - player.ReadOnlyState.Position, across);
                bool press = !slidePressed && approach < 1.4f;
                if (press) slidePressed = true;
                player.Tick(new InputFrame(Vector2.up, Vector2.zero, InputButtons.Sprint,
                    press ? InputButtons.Crouch : InputButtons.None, InputButtons.None), 1f / 60f, tick);
                Physics.SyncTransforms();
                slid |= player.ReadOnlyState.MovementState == MovementState.Slide;
                clearanceBlocked |= _actor.GetComponent<PlayerDriver>().Probe().StandingBlocked;
                if (Vector3.Dot(player.ReadOnlyState.Position - surface.EndpointB, across) > 0.4f) { passed = true; break; }
            }
            Assert.That(slid && clearanceBlocked && passed, Is.True, "The full-height actor must use the intentional low shortcut.");
        }

        [Test]
        public void GeneratedPartitionCornersExposeNativeReboundProbeWithinOrdinaryRoute()
        {
            _manager.Initialize(_config, _driverConfig, 19, 1);
            var corner = _owner.GetComponentsInChildren<ProceduralTraversalSurface>()
                .Single(value => value.SurfaceId == 50010);
            var bounds = corner.GetComponent<Collider>().bounds;
            var normal = bounds.size.x > bounds.size.z ? Vector3.back : Vector3.left;
            var point = new Vector3(bounds.center.x, 0.3f, bounds.center.z) + normal * 0.9f;
            SpawnPlayer(point, -normal);
            var probe = _actor.GetComponent<PlayerDriver>().Probe();
            Assert.That(probe.WallDetected, Is.True); Assert.That(probe.WallId, Is.EqualTo(corner.SurfaceId));
            Assert.That(probe.WallDistance, Is.LessThanOrEqualTo(0.6f));
            Assert.That(Vector3.Distance(probe.WallNormal, normal), Is.LessThan(0.00001f),
                "Native cast normal must align with the authored rebound face within floating-point precision.");
        }

        [TestCase(1)] [TestCase(3)] [TestCase(20)]
        public void TwentyCastleSeedsPassActualNavigationForEveryElevatedObjective(int round)
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_castleModules").boolValue = true;
            settings.FindProperty("_initialRoomCount").intValue = 7;
            settings.ApplyModifiedPropertiesWithoutUndo();
            for (int seed = 0; seed < 20; seed++)
            {
                _manager.Initialize(_config, _driverConfig, seed, round);
                Assert.That(_manager.IsReady, Is.True, "Seed " + seed + " round " + round);
                Assert.That(_manager.Graph.Anchors.Any(a => a.Position.y > 2f), Is.True);
                Assert.That(_manager.Doors.Count(d => d.FromRoomId == _manager.Graph.ExitRoomId || d.ToRoomId == _manager.Graph.ExitRoomId), Is.EqualTo(4));
                foreach (var anchor in _manager.Graph.Anchors)
                    Assert.That(Physics.CheckCapsule(anchor.Position + Vector3.up * 0.31f,
                        anchor.Position + Vector3.up * 1.49f, 0.3f, ~0, QueryTriggerInteraction.Ignore), Is.False);
                _manager.Teardown();
            }
        }

        [Test]
        public void CastleStairClimbsWithoutJumpAndHasSafeLowerBailout()
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_castleModules").boolValue = true;
            settings.FindProperty("_initialRoomCount").intValue = 7;
            settings.ApplyModifiedPropertiesWithoutUndo();
            _manager.Initialize(_config, _driverConfig, 8, 1);
            var module = _manager.RoomModules.First(m => m.Kind == ProceduralModuleKind.OpenStairHall);
            var room = _manager.Graph.Rooms[module.RoomId - 1];
            var along = module.AlongX ? Vector3.right : Vector3.forward;
            var across = module.AlongX ? Vector3.forward : Vector3.right;
            var ground = new Vector3(room.Center.x, 0.1f, room.Center.z);
            var player = SpawnPlayer(ground - along * 3f - across * 4.4f, across);
            for (int tick = 1; tick <= 240; tick++)
            {
                player.Tick(new InputFrame(Vector2.up, Vector2.zero, InputButtons.None, InputButtons.None, InputButtons.None), 1f / 60f, tick);
                Physics.SyncTransforms();
                if (Vector3.Dot(player.ReadOnlyState.Position - ground, across) >= 1.8f) break;
            }
            Assert.That(player.ReadOnlyState.Position.y, Is.EqualTo(_config.UpperDeckHeight).Within(0.09f),
                "The ordinary movement kit must ascend the entire staircase without a jump purchase.");
            Object.DestroyImmediate(_actor); Object.DestroyImmediate(_playerProfile); Object.DestroyImmediate(_playerDriverConfig);
            player = SpawnPlayer(ground + Vector3.up * _config.UpperDeckHeight + across * 2.6f, -across);
            for (int tick = 1; tick <= 100; tick++)
            {
                player.Tick(new InputFrame(Vector2.up, Vector2.zero, InputButtons.None, InputButtons.None, InputButtons.None), 1f / 60f, tick);
                Physics.SyncTransforms();
            }
            Assert.That(player.ReadOnlyState.Position.y, Is.InRange(-0.05f, 0.15f), "A bridge drop must have a usable lower floor.");
        }

        [Test]
        public void EveryCastleWindowCompletesRealVaultInBothDirections()
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_castleModules").boolValue = true;
            settings.FindProperty("_initialRoomCount").intValue = 7;
            settings.ApplyModifiedPropertiesWithoutUndo();
            _manager.Initialize(_config, _driverConfig, 8, 3);
            var windows = _owner.GetComponentsInChildren<ProceduralTraversalSurface>()
                .Where(s => s.SurfaceId >= 80000 && s.Kind == TraversalSurfaceKind.Vault).ToArray();
            Assert.That(windows.Length, Is.GreaterThan(0));
            foreach (var surface in windows)
            foreach (bool reverse in new[] { false, true })
            {
                var across = (surface.EndpointB - surface.EndpointA).normalized * (reverse ? -1f : 1f);
                var entry = reverse ? surface.EndpointB : surface.EndpointA;
                var target = reverse ? surface.EndpointA : surface.EndpointB;
                var player = SpawnPlayer(entry - across * 0.35f, across);
                Assert.That(_actor.GetComponent<PlayerDriver>().Probe().VaultCandidate, Is.True,
                    "Window " + surface.SurfaceId + " must be in range at a1.4m center approach.");
                bool completed = false;
                for (int tick = 1; tick <= 24; tick++)
                {
                    player.Tick(new InputFrame(Vector2.up, Vector2.zero,
                        InputButtons.Sprint | (tick == 1 ? InputButtons.Jump : InputButtons.None),
                        tick == 1 ? InputButtons.Jump : InputButtons.None,
                        tick == 2 ? InputButtons.Jump : InputButtons.None), 1f / 60f, tick);
                    Physics.SyncTransforms();
                    foreach (var fact in player.LastTraversalFacts.Where(f => f.Kind == TraversalKind.Vault))
                    {
                        Assert.That(fact.Succeeded, Is.True, "Castle window " + surface.SurfaceId + " reverse " + reverse);
                        Assert.That(Vector3.Distance(player.ReadOnlyState.Position, target), Is.LessThanOrEqualTo(_playerProfile.VaultCompletionTolerance));
                        completed = true;
                    }
                    if (completed) break;
                }
                Assert.That(completed, Is.True, "Every graph window edge must have real physical completion.");
                ReleaseActor();
            }
        }

        [Test]
        public void EveryCastleSlideLinkRequiresCrouchAndPassesInBothDirections()
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_castleModules").boolValue = true;
            settings.FindProperty("_initialRoomCount").intValue = 7;
            settings.ApplyModifiedPropertiesWithoutUndo();
            _manager.Initialize(_config, _driverConfig, 8, 3);
            var slides = _owner.GetComponentsInChildren<ProceduralTraversalSurface>()
                .Where(s => s.SurfaceId >= 80000 && s.Kind == TraversalSurfaceKind.SlideGate).ToArray();
            Assert.That(slides.Length, Is.GreaterThan(0));
            foreach (var surface in slides)
            foreach (bool reverse in new[] { false, true })
            {
                var across = (surface.EndpointB - surface.EndpointA).normalized * (reverse ? -1f : 1f);
                var entry = reverse ? surface.EndpointB : surface.EndpointA;
                var target = reverse ? surface.EndpointA : surface.EndpointB;
                var player = SpawnPlayer(entry - across * 0.35f, across);
                bool pressed = false, slid = false, standingBlocked = false, passed = false;
                for (int tick = 1; tick <= 90; tick++)
                {
                    var velocity = player.ReadOnlyState.Velocity;
                    bool press = !pressed && new Vector2(velocity.x, velocity.z).magnitude >= _playerProfile.SlideMinimumSpeed;
                    if (press) pressed = true;
                    player.Tick(new InputFrame(Vector2.up, Vector2.zero, InputButtons.Sprint,
                        press ? InputButtons.Crouch : InputButtons.None, InputButtons.None), 1f / 60f, tick);
                    Physics.SyncTransforms();
                    slid |= player.ReadOnlyState.MovementState == MovementState.Slide;
                    standingBlocked |= _actor.GetComponent<PlayerDriver>().Probe().StandingBlocked;
                    if (Vector3.Dot(player.ReadOnlyState.Position - target, across) > 0.3f) { passed = true; break; }
                }
                Assert.That(slid && standingBlocked && passed, Is.True,
                    "Castle slide " + surface.SurfaceId + " reverse " + reverse + " must be traversable through actual movement.");
                ReleaseActor();
            }
        }

        [TestCase(1980825774, 2)] [TestCase(798225482, 1)]
        public void IdleCastlePlayerDoesNotOverlapAnyPickupTrigger(int seed, int round)
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_castleModules").boolValue = true;
            settings.FindProperty("_initialRoomCount").intValue = 7;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var pickupConfig = ScriptableObject.CreateInstance<FloorDriverConfig>();
            try
            {
                _manager.Initialize(_config, _driverConfig, seed, round);
                var player = SpawnPlayer(_manager.PlayerSpawnPosition, _manager.PlayerSpawnRotation * Vector3.forward);
                for (int tick = 1; tick <= 30; tick++)
                {
                    player.Tick(default, 1f / 60f, tick);
                    Physics.SyncTransforms();
                    var capsule = _actor.GetComponent<CapsuleCollider>();
                    foreach (var anchor in _manager.Graph.Anchors)
                    {
                        var triggerCenter = anchor.Position + Vector3.up * pickupConfig.PickupHeight;
                        float separation = Vector3.Distance(capsule.ClosestPoint(triggerCenter), triggerCenter);
                        Assert.That(separation, Is.GreaterThan(pickupConfig.PickupRadius),
                            "Idle spawn overlaps pickup " + anchor.Id + " at tick " + tick);
                    }
                }
            }
            finally { Object.DestroyImmediate(pickupConfig); }
        }

        private void ReleaseActor()
        {
            if (_actor != null) Object.DestroyImmediate(_actor);
            if (_playerProfile != null) Object.DestroyImmediate(_playerProfile);
            if (_playerDriverConfig != null) Object.DestroyImmediate(_playerDriverConfig);
            _actor = null; _playerProfile = null; _playerDriverConfig = null;
        }

        private PlayerManager SpawnPlayer(Vector3 feet, Vector3 forward)
        {
            _playerProfile = ScriptableObject.CreateInstance<PlayerProfile>();
            _playerDriverConfig = ScriptableObject.CreateInstance<PlayerMoverDriverConfig>();
            _actor = new GameObject("Procedural native Player");
            _actor.transform.SetPositionAndRotation(feet, Quaternion.LookRotation(forward, Vector3.up));
            var player = _actor.AddComponent<PlayerManager>();
            var serialized = new SerializedObject(_actor.GetComponent<PlayerDriver>());
            serialized.FindProperty("_config").objectReferenceValue = _playerDriverConfig;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            player.Initialize(_playerProfile, new EntityContext(new EntityId(801), new System.Random(19)));
            Physics.SyncTransforms();
            return player;
        }
    }
}
