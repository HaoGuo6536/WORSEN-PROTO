// ============================================================================
// HunterClearTurnTests.cs
// ============================================================================
// PURPOSE:
//   Proves that physical arc clearance preserves wide-turn inertia without allowing
//   the recorded Goblin support shortcut, and never mutates live state while probing.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Compare clear pillar turns with the actual blocked slide-wall support route.
//   - Preserve collision boundaries and bounded turning at ordinary chase speed.
// DEPENDENCIES:
//   Hunter motor/state/presenter/config, Unity Physics, UnityEditor, NUnit.
// USAGE NOTES:
//   Coordinator owns Unity lease. Isolated distant geometry uses the recorded path;
//   existing HunterCornerClearanceTests separately exercise normal baked repathing.
// ============================================================================
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterClearTurnTests
    {
        [TestCase(0f, true)] [TestCase(90f, true)]
        [TestCase(0f, false)] [TestCase(90f, false)]
        public void OnlyPhysicallyClearArcsKeepMomentum(float yaw, bool wideTurn)
        {
            var objects = new List<GameObject>(); var obstacles = new List<Collider>();
            var config = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            HunterDriver driver = null;
            Vector3 origin = new Vector3(2100f, 100f, 2100f); Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            try
            {
                Box(objects, origin, rotation, new Vector3(0, -0.25f, 0), new Vector3(40, 0.5f, 40));
                Vector3 start, forward, goal; Vector3[] path;
                if (wideTurn)
                {
                    obstacles.Add(Box(objects, origin, rotation, new Vector3(-5, 2, 3), new Vector3(3, 4, 6)));
                    start = new Vector3(-5.362f, 0, -0.857f); forward = new Vector3(-0.961f, 0, 0.277f).normalized;
                    goal = new Vector3(-8.3f, 0, 5);
                    path = new[] { start, new Vector3(-6.6f, 0, -0.5f), new Vector3(-7f, 0, 0), goal };
                }
                else
                {
                    obstacles.Add(Box(objects, origin, rotation, new Vector3(0.3f, 0.53f, 0), new Vector3(0.6f, 1.06f, 0.6f)));
                    obstacles.Add(Box(objects, origin, rotation, new Vector3(3, 2.53f, 0), new Vector3(6, 2.96f, 0.6f)));
                    start = new Vector3(-0.3860035f, 0, -0.2013558f); forward = new Vector3(0.5734f, 0, -0.8193f).normalized;
                    goal = new Vector3(2.850106f, 0, -4.8247418f);
                    path = new[] { new Vector3(-0.5f, 0, -0.2013558f), new Vector3(-0.5f, 0, -0.3999996f), goal };
                }
                var actor = new GameObject("Corner-clearance hunter"); objects.Add(actor);
                actor.transform.SetPositionAndRotation(origin + rotation * start, Quaternion.LookRotation(rotation * forward));
                var capsule = actor.AddComponent<CapsuleCollider>(); capsule.radius = 0.4f; capsule.height = 1.8f; capsule.center = Vector3.up * 0.9f;
                actor.AddComponent<Rigidbody>(); driver = actor.AddComponent<HunterDriver>();
                var serialized = new SerializedObject(driver); serialized.FindProperty("_config").objectReferenceValue = config; serialized.ApplyModifiedPropertiesWithoutUndo();
                driver.Initialize();
                var state = (HunterDriverState)typeof(HunterDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
                for (int i = 0; i < path.Length; i++) path[i] = origin + rotation * path[i];
                new HunterSteeringPresenter().SetPath(state.Steering, path); state.Steering.CornerIndex = 1;
                state.Steering.Velocity = wideTurn ? rotation * forward * 8.96f : Vector3.zero;
                state.PathAvailable = true; state.PathCooldown = 100f; state.LastTarget = origin + rotation * goal;
                Physics.SyncTransforms();
                Vector3 beforePosition = state.Steering.Position, beforeVelocity = state.Steering.Velocity, beforeForward = state.Steering.Forward;
                bool clear = (bool)typeof(HunterDriver).GetMethod("HasClearCornerArc", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(driver, new object[] { 8.96f, 20f, 240f });
                Assert.That(clear, Is.EqualTo(wideTurn), "The fact must come from capsule/floor clearance, not a looser turning-angle threshold.");
                Assert.That(state.Steering.Position, Is.EqualTo(beforePosition)); Assert.That(state.Steering.Velocity, Is.EqualTo(beforeVelocity));
                Assert.That(state.Steering.Forward, Is.EqualTo(beforeForward)); Assert.That(state.Steering.CornerIndex, Is.EqualTo(1));
                state.ClearCornerArc = clear;
                float minimumSpeed = float.PositiveInfinity;
                int ticks = wideTurn ? 40 : 360;
                for (int tick = 0; tick < ticks; tick++)
                {
                    driver.Move(state.LastTarget, 8.96f, 20f, 240f, 1f / 60f, false, false, Vector3.zero, 18f, 4f);
                    minimumSpeed = Mathf.Min(minimumSpeed, new Vector2(driver.Velocity.x, driver.Velocity.z).magnitude);
                    foreach (Collider obstacle in obstacles)
                    {
                        bool overlap = Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation,
                            obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out float depth);
                        Assert.That(!overlap || depth <= 0.021f, Is.True);
                    }
                }
                if (wideTurn)
                {
                    Assert.That(minimumSpeed, Is.GreaterThan(6f), "An unobstructed chase arc retains momentum instead of stopping at each bevel.");
                    Assert.That((Quaternion.Inverse(rotation) * (driver.Position - origin)).z, Is.GreaterThan(2f));
                }
                else Assert.That(Vector3.Distance(driver.Position, state.LastTarget), Is.LessThan(0.1f));
            }
            finally
            {
                if (driver != null) driver.Teardown();
                for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
                Object.DestroyImmediate(config);
            }
        }
        private static Collider Box(List<GameObject> objects, Vector3 origin, Quaternion rotation, Vector3 position, Vector3 size)
        {
            var owner = new GameObject("Owned corner geometry"); objects.Add(owner);
            owner.transform.SetPositionAndRotation(origin + rotation * position, rotation);
            var box = owner.AddComponent<BoxCollider>(); box.size = size; return box;
        }
    }
}
