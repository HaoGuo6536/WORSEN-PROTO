// ============================================================================
// HunterCornerClearanceTests.cs
// ============================================================================
// PURPOSE:
//   Reproduces the live Goblin stall caused by skipping a nearby turning corner.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Require arrival at a genuine turn while preserving collinear waypoint tolerance.
//   - Replay the recorded slide-wall route against real capsule collision in two orientations.
// DEPENDENCIES:
//   - Hunter driver/presenter/state/config, Unity Physics, UnityEditor, NUnit.
// USAGE NOTES:
//   Native fixture injects the exact observed valid navigation corners; it isolates
//   steering and collision rather than asking a new bake to invent the repro route.
//   Coordinator owns Unity lease. Only owned distant objects/configs are destroyed.
// ============================================================================
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterCornerClearanceTests
    {
        private static readonly Vector3 Start = new Vector3(-0.3860035f, 0f, -0.2013558f);
        private static readonly Vector3 Forward = new Vector3(0.5734f, 0f, -0.8193f).normalized;
        private static readonly Vector3 Goal = new Vector3(2.850106f, 0f, -4.8247418f);
        private static Vector3[] Corners() => new[] {
            new Vector3(-0.5f, 0.05f, -0.2013558f), new Vector3(-0.5f, 0.05f, -0.3999996f),
            new Vector3(Goal.x, 0.05f, Goal.z)
        };

        [Test] public void TurningCornerInsideToleranceMustBeReachedBeforeTheFarGoalIsSelected()
        {
            var presenter = new HunterSteeringPresenter(); var state = new HunterSteeringDriverState();
            presenter.Reset(state, Start, Forward); presenter.SetPath(state, Corners());
            state.CornerIndex = 1;
            presenter.Tick(state, 1f / 60f, 8.4f, 20f, 240f, false, false, Vector3.zero, 18f, 4f, 0.25f);
            Assert.That(state.CornerIndex, Is.EqualTo(1), "The recorded turn is .229m away, inside .25m tolerance but still required for wall clearance.");
            float closest = float.PositiveInfinity;
            for (int tick = 0; tick < 360 && state.CornerIndex == 1; tick++)
            {
                Vector3 before = state.Position - Corners()[1]; before.y = 0; closest = Mathf.Min(closest, before.magnitude);
                presenter.Tick(state, 1f / 60f, 8.4f, 20f, 240f, false, false, Vector3.zero, 18f, 4f, 0.25f);
                Vector3 after = state.Position - Corners()[1]; after.y = 0; closest = Mathf.Min(closest, after.magnitude);
            }
            Assert.That(state.CornerIndex, Is.EqualTo(2));
            Assert.That(closest, Is.LessThan(0.001f), "Turning-corner completion must preserve its actual safe route point.");
        }

        [Test] public void CollinearIntermediateCornerRetainsCallerTolerance()
        {
            var presenter = new HunterSteeringPresenter(); var state = new HunterSteeringDriverState();
            presenter.Reset(state, Vector3.forward * 0.8f, Vector3.forward);
            presenter.SetPath(state, new[] { Vector3.zero, Vector3.forward, Vector3.forward * 4f }); state.CornerIndex = 1;
            presenter.Tick(state, 1f / 60f, 8.4f, 20f, 240f, false, false, Vector3.zero, 18f, 4f, 0.25f);
            Assert.That(state.CornerIndex, Is.EqualTo(2));
        }

        [Test] public void PendingCornerAlignmentSurvivesRepathWithoutMovingOrExceedingTurnRate()
        {
            var presenter = new HunterSteeringPresenter(); var state = new HunterSteeringDriverState();
            presenter.Reset(state, Vector3.zero, Vector3.right); state.AlignAfterCorner = true;
            for (int tick = 0; tick < 60 && state.AlignAfterCorner; tick++)
            {
                presenter.SetPath(state, new[] { Vector3.zero, Vector3.forward * 4f });
                Vector3 heading = state.Forward;
                Vector3 movement = presenter.Tick(state, 1f / 60f, 8.4f, 20f, 240f, false, false, Vector3.zero, 18f, 4f, 0.25f);
                Assert.That(movement, Is.EqualTo(Vector3.zero));
                Assert.That(state.Position, Is.EqualTo(Vector3.zero));
                Assert.That(Vector3.Angle(heading, state.Forward), Is.LessThanOrEqualTo(4.01f));
            }
            Assert.That(state.AlignAfterCorner, Is.False);
            Assert.That(Vector3.Angle(state.Forward, Vector3.forward), Is.LessThan(1f));
            state.AlignAfterCorner = true; presenter.Reset(state, Vector3.zero, Vector3.forward);
            Assert.That(state.AlignAfterCorner, Is.False, "A new life must not retain old alignment work.");
        }

        [TestCase(0f, false)] [TestCase(90f, false)]
        [TestCase(0f, true)] [TestCase(90f, true)]
        public void RecordedWallCornerProducesProgressWithoutPenetratingTheSlideWall(float yaw, bool rebakeAndRepath)
        {
            var objects = new List<GameObject>(); var walls = new List<Collider>();
            HunterMotorDriverConfig config = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            Vector3 origin = new Vector3(1800f, 100f, 1800f); Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            HunterDriver driver = null;
            NavMeshData navigation = null; NavMeshDataInstance navigationInstance = default;
            try
            {
                Box(objects, origin, rotation, new Vector3(0, -0.25f, -3), new Vector3(30, 0.5f, 30), "Floor");
                walls.Add(Box(objects, origin, rotation, new Vector3(0.3f, 0.53f, 0), new Vector3(0.6f, 1.06f, 0.6f), "Recorded slide-wall support"));
                walls.Add(Box(objects, origin, rotation, new Vector3(3f, 2.53f, 0), new Vector3(6f, 2.96f, 0.6f), "Recorded slide-wall lintel"));
                var actor = new GameObject("Recorded Goblin motor"); objects.Add(actor);
                actor.transform.SetPositionAndRotation(origin + rotation * Start, Quaternion.LookRotation(rotation * Forward));
                CapsuleCollider capsule = actor.AddComponent<CapsuleCollider>(); capsule.radius = 0.4f; capsule.height = 1.8f; capsule.center = Vector3.up * 0.9f;
                actor.AddComponent<Rigidbody>(); driver = actor.AddComponent<HunterDriver>();
                var serialized = new SerializedObject(driver); serialized.FindProperty("_config").objectReferenceValue = config; serialized.ApplyModifiedPropertiesWithoutUndo();
                driver.Initialize();
                var state = (HunterDriverState)typeof(HunterDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
                Vector3 target = origin + rotation * Goal;
                var route = Corners(); for (int i = 0; i < route.Length; i++) route[i] = origin + rotation * route[i];
                if (rebakeAndRepath)
                {
                    var sources = new List<NavMeshBuildSource>();
                    foreach (GameObject item in objects)
                    {
                        BoxCollider box = item.GetComponent<BoxCollider>();
                        if (box == null) continue;
                        sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = box.transform.localToWorldMatrix,
                            size = box.size, area = walls.Contains(box) ? 1 : 0 });
                    }
                    NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0); settings.overrideVoxelSize = true; settings.voxelSize = 0.1f;
                    navigation = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(origin, new Vector3(26, 12, 26)), Vector3.zero, Quaternion.identity);
                    Assert.That(navigation, Is.Not.Null); navigationInstance = NavMesh.AddNavMeshData(navigation);
                    Assert.That(navigationInstance.valid, Is.True);
                }
                else
                {
                    new HunterSteeringPresenter().SetPath(state.Steering, route);
                    state.PathAvailable = true; state.PathCooldown = 100f; state.LastTarget = target;
                }
                Physics.SyncTransforms();
                for (int tick = 0; tick < 360; tick++)
                {
                    driver.Move(target, 8.4f, 20f, 240f, 1f / 60f, false, false, Vector3.zero, 18f, 4f);
                    foreach (Collider wall in walls)
                    {
                        bool overlaps = Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation,
                            wall, wall.transform.position, wall.transform.rotation, out _, out float distance);
                        Assert.That(!overlaps || distance <= 0.021f, Is.True, "No wall crossing beyond the existing .02m skin allowance.");
                    }
                }
                string evidence = "wall corner yaw=" + yaw + ", normal repathing=" + rebakeAndRepath + ", final=" + (Quaternion.Inverse(rotation) * (driver.Position - origin)).ToString("F4") +
                    ", corner=" + state.Steering.CornerIndex + ", path=" + driver.PathAvailable;
                TestContext.WriteLine(evidence);
                Assert.That(Vector3.Distance(driver.Position, target), Is.LessThan(0.1f), evidence);
            }
            finally
            {
                if (driver != null) driver.Teardown();
                if (navigationInstance.valid) navigationInstance.Remove();
                if (navigation != null) Object.DestroyImmediate(navigation);
                for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
                Object.DestroyImmediate(config);
            }
        }
        private static Collider Box(List<GameObject> objects, Vector3 origin, Quaternion rotation, Vector3 local, Vector3 size, string name)
        {
            var owner = new GameObject(name); objects.Add(owner); owner.transform.SetPositionAndRotation(origin + rotation * local, rotation);
            var collider = owner.AddComponent<BoxCollider>(); collider.size = size; return collider;
        }
    }
}
