// ============================================================================
// HunterStairTraversalTests.cs
// ============================================================================
// PURPOSE:
//   Reproduces the saved Goblin hunter attacking into generated castle stairs.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter integration.
// KEY RESPONSIBILITIES:
//   - Check melee elevation admission using actual saved profiles.
//   - Exercise physical stepping with committed range/heading and blocking geometry.
//   - Follow a baked twelve-riser route at low speed and through repeated attacks.
// DEPENDENCIES:
//   - Saved Hunter profiles/prefabs, controller/driver, native Physics/NavMesh, NUnit.
// USAGE NOTES:
//   Coordinator executes under the Unity lease. Fixtures own remote geometry and
//   their NavMeshData only; cleanup never removes another scene's navigation data.
//   Stair dimensions match ProceduralCastlePresenter.Stairs: .2 rise, .4 tread,
//   width 2, twelve steps ending at the 2.4m raised gallery.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterStairTraversalTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<NavMeshBuildSource> _sources = new List<NavMeshBuildSource>();
        private readonly Vector3 _origin = new Vector3(1500f, 100f, 1500f);
        private NavMeshData _navigation;
        private NavMeshDataInstance _navigationInstance;
        private HunterDriver _driver;
        private HunterProfile _profile;
        private sealed class LevelFixture : IReadOnlyLevelState { public bool IsReady => false; public LevelGraph Graph => null; }
        private static HunterProfile Profile(string key)
        {
            var profile = AssetDatabase.LoadAssetAtPath<HunterProfile>("Assets/Resources/ScriptableObjects/Domain/Hunter/Expansion/" + key + ".asset");
            Assert.That(profile, Is.Not.Null, "Build the real saved hunter roster before this test.");
            return profile;
        }
        [TearDown] public void Cleanup()
        {
            if (_driver != null) _driver.Teardown();
            if (_navigationInstance.valid) _navigationInstance.Remove();
            if (_navigation != null) Object.DestroyImmediate(_navigation);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); _sources.Clear();
        }
        private BoxCollider Box(string name, Vector3 center, Vector3 size, bool navigation = true)
        {
            var owner = new GameObject(name); _objects.Add(owner); owner.transform.position = _origin + center;
            var box = owner.AddComponent<BoxCollider>(); box.size = size;
            if (navigation) _sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(owner.transform.position, Quaternion.identity, Vector3.one), size = size, area = 0 });
            return box;
        }
        private void BuildStairs()
        {
            _profile = Profile("lurker");
            Assert.That(_profile.MaximumMeleeElevation, Is.EqualTo(0.45f).Within(0.0001f), "Existing saved profiles must receive the new elevation default.");
            Box("Stair test ground", new Vector3(0, -0.25f, 0), new Vector3(20, 0.5f, 20));
            for (int i = 0; i < 12; i++)
            {
                float top = (i + 1) * 0.2f;
                Box("Castle step " + i, new Vector3(0, top * 0.5f, -3.6f + (i + 0.5f) * 0.4f), new Vector3(2, top, 0.4f));
            }
            Box("Castle upper gallery", new Vector3(0, 2.25f, 2.6f), new Vector3(8.6f, 0.3f, 2.8f));
            GameObject root = Object.Instantiate(_profile.Prefab, _origin + new Vector3(0, 0, -4.4f), Quaternion.identity);
            _objects.Add(root); _driver = root.GetComponent<HunterDriver>();
            Assert.That(_driver, Is.Not.Null);
            _driver.Initialize(); Physics.SyncTransforms();
        }
        private void BuildNavigation()
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.overrideVoxelSize = true; settings.voxelSize = 0.1f;
            _navigation = NavMeshBuilder.BuildNavMeshData(settings, _sources,
                new Bounds(_origin + Vector3.up * 2f, new Vector3(24, 12, 24)), Vector3.zero, Quaternion.identity);
            Assert.That(_navigation, Is.Not.Null);
            _navigationInstance = NavMesh.AddNavMeshData(_navigation);
            Assert.That(_navigationInstance.valid, Is.True);
        }

        [TestCase("lurker", 0.2f, true)]
        [TestCase("lurker", -0.2f, true)]
        [TestCase("lurker", 0.6f, false)]
        [TestCase("lurker", 2.4f, false)]
        [TestCase("hexer", 2.4f, true)]
        [TestCase("thorncaller", 2.4f, true)]
        public void SavedProfilesPermitSingleRiserButMeleeClimbsBeforeAttackingRaisedTargets(string key, float elevation, bool attacks)
        {
            HunterProfile profile = Profile(key);
            var player = new PlayerBehaviorState { Id = new EntityId(17), Health = 100, SprintSpeed = 8f, Position = new Vector3(0, elevation, 3f) };
            var state = new HunterBehaviorState(); var controller = new HunterController(state, profile, new System.Random(5), player, new LevelFixture());
            controller.Reset(new EntityId(-17), Vector3.zero, Vector3.forward);
            HunterTickResult result = controller.Tick(new SightProbe(true, true, true), 0.02f, 0);
            Assert.That(state.PlayerVisible, Is.True);
            Assert.That(result.BeginLunge, Is.EqualTo(attacks));
            if (!attacks)
            { Assert.That(state.LungePhase, Is.EqualTo(HunterLungePhase.None)); Assert.That(state.CurrentAction, Is.EqualTo(HunterAction.Chase)); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void CommittedGoblinLungeClimbsRealRisersButCannotStepThroughWallOrLowCeiling(int blocker)
        {
            BuildStairs();
            if (blocker == 1) Box("Blocking wall", new Vector3(0, 1.5f, -3.5f), new Vector3(3, 3, 0.2f), false);
            if (blocker == 2) Box("Low stair ceiling", new Vector3(0, 2.1f, -3.8f), new Vector3(3, 0.4f, 3), false);
            Physics.SyncTransforms(); Vector3 start = _driver.Position;
            for (int tick = 0; tick < 20; tick++)
                _driver.Move(_origin + new Vector3(0, 2.4f, 2), 0f, _profile.Acceleration, _profile.TurnRate, 0.02f,
                    false, true, Vector3.forward, _profile.LungeSpeed, _profile.LungeDistance);
            Vector3 displacement = _driver.Position - start;
            Assert.That(Mathf.Abs(displacement.x), Is.LessThan(0.001f), "Stepping cannot change the committed heading.");
            Assert.That(displacement.z, Is.LessThanOrEqualTo(_profile.LungeDistance + 0.01f), "Stepping cannot reset the committed travel budget.");
            if (blocker == 0)
            { Assert.That(displacement.y, Is.GreaterThan(0.35f)); Assert.That(displacement.z, Is.GreaterThan(1f)); }
            else
            { Assert.That(displacement.y, Is.LessThan(0.05f)); Assert.That(_driver.Position.z, Is.LessThan(_origin.z - 3.6f)); }
        }

        [Test] public void SlowPursuitClimbsRoundedRiserEdgesInsteadOfStallingAtTheFirstLip()
        {
            BuildStairs(); BuildNavigation();
            Vector3 target = _origin + new Vector3(0, 2.4f, 3.2f);
            for (int tick = 0; tick < 260; tick++)
                _driver.Move(target, 0.75f, _profile.Acceleration, _profile.TurnRate, 0.02f,
                    false, false, Vector3.zero, _profile.LungeSpeed, _profile.LungeDistance);
            string evidence = "slow pursuit pos=" + (_driver.Position - _origin).ToString("F4") + ", path=" + _driver.PathAvailable;
            TestContext.WriteLine(evidence);
            Assert.That(_driver.PathAvailable, Is.True, evidence);
            Assert.That(_driver.Position.y, Is.GreaterThan(_origin.y + 0.4f), evidence);
            Assert.That(_driver.Position.z, Is.GreaterThan(_origin.z - 3f), evidence);
        }

        [Test] public void GoblinBesideStairCanAttackLowerFloorTargetAcrossRepeatedCycles()
        {
            BuildStairs(); BuildNavigation();
            Vector3 start = _origin + new Vector3(-1.41f, 0f, -2.6f);
            _driver.transform.SetPositionAndRotation(start, Quaternion.LookRotation(Vector3.back));
            _driver.GetComponent<Rigidbody>().position = start; _driver.Initialize();
            Vector3 target = _origin + new Vector3(-1.41f, 0f, -5.4f);
            BoxCollider body = Box("Lower floor target", new Vector3(-1.41f, 0.9f, -5.4f), new Vector3(0.6f, 1.8f, 0.6f), false);
            var player = new PlayerBehaviorState { Id = new EntityId(37), Health = 100, SprintSpeed = 8f, Position = target };
            var state = new HunterBehaviorState(); var controller = new HunterController(state, _profile, new System.Random(19), player, new LevelFixture());
            controller.Reset(new EntityId(-37), start, Vector3.back);
            int contacts = 0, attacks = 0;
            _driver.OnLungeContact += collider => { if (collider == body && controller.TryAcceptContact(player.Id, out _)) contacts++; };
            Physics.SyncTransforms();
            for (int tick = 0; tick < 450 && contacts < 2; tick++)
            {
                SightProbe probe = controller.ShouldProbe(tick) ? _driver.ProbeSight(target, collider => collider == body) : default;
                HunterTickResult result = controller.Tick(probe, 0.02f, tick);
                if (result.BeginLunge) attacks++;
                _driver.Move(result.Target, result.Speed, _profile.Acceleration, _profile.TurnRate, 0.02f,
                    result.Phase == HunterLungePhase.Windup || result.Phase == HunterLungePhase.Recovery,
                    result.ActiveContact, result.LungeDirection, controller.LungeSpeed, controller.EffectiveAttackDistance);
                controller.CommitPose(_driver.Position, _driver.Velocity, _driver.Forward);
                if (!_driver.PathAvailable && result.Phase == HunterLungePhase.None) controller.ReportPathFailure();
            }
            string evidence = "stair-side pos=" + (_driver.Position - _origin).ToString("F4") + ", attacks=" + attacks + ", contacts=" + contacts;
            TestContext.WriteLine(evidence);
            Assert.That(_driver.Position.z, Is.LessThan(start.z - 1f), evidence);
            Assert.That(_driver.Position.y, Is.EqualTo(_origin.y).Within(0.02f), evidence);
            Assert.That(contacts, Is.GreaterThanOrEqualTo(2), evidence);
        }

        [Test] public void SavedGoblinTraversesTwelveRisersAndKeepsMakingProgressAcrossAttackCycles()
        {
            BuildStairs(); BuildNavigation();
            Vector3 target = _origin + new Vector3(0, 2.4f, 3.2f);
            BoxCollider body = Box("Target collider", new Vector3(0, 3.3f, 3.2f), new Vector3(0.6f, 1.8f, 0.6f), false);
            var player = new PlayerBehaviorState { Id = new EntityId(27), Health = 100, SprintSpeed = 8f, Position = target };
            var state = new HunterBehaviorState(); var controller = new HunterController(state, _profile, new System.Random(9), player, new LevelFixture());
            controller.Reset(new EntityId(-27), _driver.Position, _driver.Forward);
            var motor = (HunterDriverState)typeof(HunterDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_driver);
            int attacks = 0, contacts = 0;
            _driver.OnLungeContact += collider => { if (collider == body && controller.TryAcceptContact(player.Id, out _)) contacts++; };
            Physics.SyncTransforms();
            for (int tick = 0; tick < 900; tick++)
            {
                SightProbe probe = controller.ShouldProbe(tick) ? _driver.ProbeSight(target, collider => collider == body) : default;
                HunterTickResult result = controller.Tick(probe, 0.02f, tick);
                if (result.BeginLunge) attacks++;
                _driver.Move(result.Target, result.Speed, _profile.Acceleration, _profile.TurnRate, 0.02f,
                    result.Phase == HunterLungePhase.Windup || result.Phase == HunterLungePhase.Recovery,
                    result.ActiveContact, result.LungeDirection, controller.LungeSpeed, controller.EffectiveAttackDistance);
                controller.CommitPose(_driver.Position, _driver.Velocity, _driver.Forward);
                if (!_driver.PathAvailable && result.Phase == HunterLungePhase.None) controller.ReportPathFailure();
                if (tick % 100 == 0)
                    TestContext.WriteLine("Stair tick=" + tick + ", pos=" + (_driver.Position - _origin).ToString("F4") +
                        ", visible=" + state.PlayerVisible + ", action=" + state.CurrentAction + ", phase=" + state.LungePhase +
                        ", goal=" + (result.Target - _origin).ToString("F4") + ", path=" + _driver.PathAvailable +
                        ", corner=" + motor.Steering.CornerIndex + "/" + motor.Steering.Corners.Length +
                        ", velocity=" + _driver.Velocity.ToString("F4") + ", gap=" + motor.RepathActive +
                        ", next=" + (motor.Steering.CornerIndex < motor.Steering.Corners.Length ?
                            (motor.Steering.Corners[motor.Steering.CornerIndex] - _origin).ToString("F4") : "none"));
                if (contacts >= 2) break;
            }
            string diagnostic = "Goblin stair traversal: position=" + (_driver.Position - _origin).ToString("F4") +
                ", attack cycles=" + attacks + ", accepted contacts=" + contacts + ", path=" + _driver.PathAvailable +
                ", action=" + state.CurrentAction + ", visible=" + state.PlayerVisible;
            TestContext.WriteLine(diagnostic);
            Assert.That(_driver.Position.y, Is.GreaterThan(_origin.y + 2.3f), "The Goblin must reach the gallery instead of attacking forever into a riser. " + diagnostic);
            Assert.That(_driver.Position.z, Is.GreaterThan(_origin.z + 1.2f));
            Assert.That(attacks, Is.GreaterThanOrEqualTo(2));
            Assert.That(contacts, Is.GreaterThanOrEqualTo(2), "Multiple attack cycles must still reach the real target collider.");
            TestContext.WriteLine("Goblin stair traversal: position=" + (_driver.Position - _origin) + ", attack cycles=" + attacks + ", accepted contacts=" + contacts);
        }
    }
}
