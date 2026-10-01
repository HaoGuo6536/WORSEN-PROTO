// ============================================================================
// PlayerArmsSetupTests.cs
// ============================================================================
// PURPOSE:
//   Checks repeatable relaxed-arm config migration and real imported shoulder
//   pivots. These engine cases complement headless geometry math without claiming
//   that Blender's importer proves Unity skin orientation or renderer bounds.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Verify one-time default migration preserves later designer tuning.
//   - Verify repeated limb rebuilds preserve shoulder pivots and skin references.
//   - Verify final camera callbacks preserve gravity alignment and composition.
// DEPENDENCIES:
//   - Player editor setup, PlayerLimbStandIn, UnityEditor, UnityEngine and NUnit.
// USAGE NOTES:
//   Coordinator imports Blocky Character first and runs under the Unity lease.
//   Temporary objects only; these tests never save assets, prefabs or scenes.
// ============================================================================
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Editor.Player;

namespace Worsen.Tests.Player
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PlayerArmsSetupTests
    {
        [Test]
        public void MigrationWritesDefaultsOnceAndPreservesDesignerEdits()
        {
            var config = ScriptableObject.CreateInstance<PlayerMoverDriverConfig>();
            try
            {
                using (var serialized = new SerializedObject(config))
                {
                    serialized.FindProperty("_handOffset").vector3Value = new Vector3(.32f, -.25f, .5f);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                PlayerPrefabGenerator.ConfigureRelaxedArms(config);
                Assert.That(config.HandOffset, Is.EqualTo(PlayerMoverDriverConfig.DefaultShoulderOffset));
                Assert.That(config.ArmSwingDegrees, Is.EqualTo(PlayerMoverDriverConfig.DefaultArmSwingDegrees));
                Assert.That(config.ArmSwingReferenceSpeed, Is.EqualTo(PlayerMoverDriverConfig.DefaultArmSwingReferenceSpeed));
                Assert.That(config.ArmSwingFrequency, Is.EqualTo(PlayerMoverDriverConfig.DefaultArmSwingFrequency));
                Assert.That(config.ArmSwingEaseSeconds, Is.EqualTo(PlayerMoverDriverConfig.DefaultArmSwingEaseSeconds));
                Assert.That(config.RelaxedArmsVersion, Is.EqualTo(1));
                using (var serialized = new SerializedObject(config))
                {
                    serialized.FindProperty("_handOffset").vector3Value = new Vector3(.27f, -.24f, .06f);
                    serialized.FindProperty("_armSwingDegrees").floatValue = 4f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                string tuned = EditorJsonUtility.ToJson(config);
                PlayerPrefabGenerator.ConfigureRelaxedArms(config);
                Assert.That(EditorJsonUtility.ToJson(config), Is.EqualTo(tuned));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void ImportedRelaxedArmsRebuildAtShouldersAndFollowOnlyCameraYaw()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(BlockyCharacterSetup.ArmsPath);
            Assert.That(model, Is.Not.Null, "Import Blocky Character before this test.");
            var visuals = new GameObject("[Test] Relaxed arms");
            var cameraObject = new GameObject("[Test] Relaxed view");
            var config = ScriptableObject.CreateInstance<PlayerMoverDriverConfig>();
            var baked = new Mesh();
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.tag = "MainCamera";
                camera.fieldOfView = 75f; camera.aspect = 16f / 9f; camera.nearClipPlane = .05f;
                MethodInfo callback = typeof(PlayerLimbStandIn).GetMethod("BeforeCameraRendering", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int rebuild = 0; rebuild < 2; rebuild++)
                {
                    PlayerLimbStandIn limbs = PlayerPrefabGenerator.RebuildLimbs(visuals, model);
                    var shown = new SerializedObject(limbs);
                    Assert.That(shown.FindProperty("_showHands").boolValue, Is.EqualTo(PlayerPrefabGenerator.ShowFirstPersonArms), "Arms follow the owner's visibility switch.");
                    shown.FindProperty("_showHands").boolValue = true; // exercise the opt-in placement path
                    shown.ApplyModifiedPropertiesWithoutUndo();
                    Assert.That(visuals.transform.childCount, Is.EqualTo(4));
                    Assert.That(visuals.GetComponentsInChildren<Animator>(true), Is.Empty);
                    Assert.That(visuals.GetComponentsInChildren<Collider>(true), Is.Empty);
                    foreach (SkinnedMeshRenderer renderer in visuals.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        string side = renderer.name == "LeftArm" ? "Left" : "Right";
                        Transform root = renderer.transform.parent;
                        Transform shoulder = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == side + "Shoulder");
                        Transform hand = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == side + "Hand");
                        Assert.That(Vector3.Distance(root.position, shoulder.position), Is.LessThan(.00001f));
                        Vector3 localHand = root.InverseTransformPoint(hand.position);
                        Assert.That(localHand.y, Is.LessThan(-.45f));
                        Assert.That(localHand.z, Is.GreaterThan(0f), "Forearms lean forward in Unity's imported basis.");
                        Assert.That(localHand.x * (side == "Left" ? -1f : 1f), Is.GreaterThan(0f), "Arms lean outwards, not inward.");
                        Assert.That(renderer.bones.All(b => b != null && b.IsChildOf(root)), Is.True);
                    }
                    foreach (float pitch in new[] { 0f, 45f, -30f, 85f })
                    foreach (float yaw in new[] { 0f, 180f })
                    {
                        camera.transform.SetPositionAndRotation(new Vector3(3f, 1.6f, 4f), Quaternion.Euler(pitch, yaw, 0f));
                        // Owner playtest 2026-09-30 removed held crouch; Slide alone suppresses swing.
                        limbs.Apply(MovementState.Ground, config.EyeHeight, config.HandOffset, config.FootOffset, 0f, .02f, config);
                        callback.Invoke(limbs, new object[] { default(UnityEngine.Rendering.ScriptableRenderContext), camera });
                        foreach (SkinnedMeshRenderer renderer in visuals.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            Assert.That(Vector3.Dot(renderer.transform.parent.up, Vector3.up), Is.GreaterThan(.99999f));
                            renderer.BakeMesh(baked);
                            int visible = 0;
                            foreach (Vector3 vertex in baked.vertices)
                            {
                                Vector3 world = renderer.transform.TransformPoint(vertex);
                                Vector3 viewport = camera.WorldToViewportPoint(world);
                                Assert.That(viewport.z, Is.GreaterThan(camera.nearClipPlane));
                                if (!renderer.enabled || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
                                visible++;
                                if (pitch == 0f) Assert.That(viewport.x <= .2f || viewport.x >= .8f, Is.True);
                            }
                            if (pitch == 45f) Assert.That(visible, Is.GreaterThan(0));
                            if (pitch == -30f) Assert.That(renderer.enabled, Is.False);
                            Vector3 before = renderer.transform.parent.position;
                            callback.Invoke(limbs, new object[] { default(UnityEngine.Rendering.ScriptableRenderContext), camera });
                            Assert.That(Vector3.Distance(renderer.transform.parent.position, before), Is.LessThan(.00001f), "Repeated renders do not accumulate clearance or swing.");
                        }
                    }
                    limbs.Apply(MovementState.Ground, 0f, Vector3.zero, Vector3.zero);
                    Assert.That(visuals.transform.Cast<Transform>().All(t => !t.gameObject.activeSelf), Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(baked); Object.DestroyImmediate(config);
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(visuals);
            }
        }
    }
}
