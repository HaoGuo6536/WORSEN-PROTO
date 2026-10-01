// ============================================================================
// BlockyCharacterSetupTests.cs
// ============================================================================
// PURPOSE:
//   Regresses generated arm replacement, hidden fallback and imported skin wiring.
//   These fixtures make the coordinator's real Unity import and camera checks
//   explicit rather than treating offline C# compilation as art integration.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Check configured avatars/clips and stable, collider-free limb replacement.
//   - Check real skinned vertices beyond the camera plane after pitch/look-back.
// DEPENDENCIES:
//   - Player editor setup, PlayerLimbStandIn, NUnit and Unity editor/engine APIs.
// USAGE NOTES:
//   Coordinator runs Import Blocky Character once before these Edit Mode tests.
//   No files, scenes or assets are saved by this fixture; temporary objects only.
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
    public sealed class BlockyCharacterSetupTests
    {
        [Test]
        public void MissingModelRebuildsHiddenFallbackWithoutDuplicates()
        {
            var visuals = new GameObject("[Test] Fallback visuals");
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    PlayerLimbStandIn limbs = PlayerPrefabGenerator.RebuildLimbs(visuals, null);
                    Assert.That(visuals.transform.childCount, Is.EqualTo(4));
                    Assert.That(visuals.GetComponentsInChildren<Collider>(true), Is.Empty);
                    limbs.Apply(MovementState.Ground, 1.6f, new Vector3(.32f, -.25f, .5f), Vector3.zero);
                    Assert.That(visuals.transform.Cast<Transform>().All(t => !t.gameObject.activeSelf), Is.True);
                    Assert.That(new SerializedObject(limbs).FindProperty("_showHands").boolValue, Is.False);
                }
            }
            finally { Object.DestroyImmediate(visuals); }
        }

        [TestCase(true)] [TestCase(false)]
        public void ImportedModelsHaveExpectedAvatarsAndClips(bool character)
        {
            string path = character ? BlockyCharacterSetup.CharacterPath : BlockyCharacterSetup.ArmsPath;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null, "Run Worsen/Player/Import Blocky Character first.");
            Assert.That(importer.animationType, Is.EqualTo(character ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic));
            Assert.That(importer.globalScale, Is.EqualTo(1f));
            Assert.That(importer.optimizeGameObjects, Is.False);
            string[] names = character ? new[] { "Idle", "Walk" } : new[] { "Hold", "Sway" };
            CollectionAssert.AreEquivalent(names, importer.clipAnimations.Select(c => c.name));
            foreach (ModelImporterClipAnimation clip in importer.clipAnimations)
                Assert.That(clip.loopTime, Is.EqualTo(clip.name != "Hold"));
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            CollectionAssert.AreEquivalent(names, clips.Select(c => c.name));
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().Single();
            Assert.That(avatar.isValid, Is.True);
            Assert.That(avatar.isHuman, Is.EqualTo(character));
            if (character)
            {
                Assert.That(importer.humanDescription.human.Any(b => b.boneName == "LeftShoulder"), Is.True);
                Assert.That(importer.humanDescription.human.Any(b => b.boneName == "RightShoulder"), Is.True);
            }
        }

        [Test]
        public void RealArmsRebuildAndClearTheFinalCameraPlane()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(BlockyCharacterSetup.ArmsPath);
            Assert.That(model, Is.Not.Null, "Import generated art before running integration tests.");
            var visuals = new GameObject("[Test] Blocky visuals");
            var cameraObject = new GameObject("[Test] Blocky camera");
            var baked = new Mesh();
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.tag = "MainCamera";
                camera.nearClipPlane = .3f;
                camera.fieldOfView = 75f;
                camera.aspect = 16f / 9f;
                MethodInfo callback = typeof(PlayerLimbStandIn).GetMethod("BeforeCameraRendering", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int rebuild = 0; rebuild < 2; rebuild++)
                {
                    PlayerLimbStandIn limbs = PlayerPrefabGenerator.RebuildLimbs(visuals, model);
                    Assert.That(visuals.transform.childCount, Is.EqualTo(4));
                    Assert.That(visuals.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.EqualTo(2));
                    Assert.That(visuals.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(visuals.GetComponentsInChildren<Animator>(true), Is.Empty);
                    foreach (float pitch in new[] { -85f, 0f, 85f })
                    foreach (float yaw in new[] { 0f, 180f })
                    {
                        camera.transform.SetPositionAndRotation(new Vector3(4f, 1.6f, 7f), Quaternion.Euler(pitch, yaw, 6f));
                        limbs.Apply(MovementState.Ground, 1.6f, new Vector3(.24f, -.22f, .08f), Vector3.zero);
                        callback.Invoke(limbs, new object[] { default(UnityEngine.Rendering.ScriptableRenderContext), camera });
                        foreach (SkinnedMeshRenderer renderer in visuals.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            Assert.That(renderer.bones.All(b => b != null && b.IsChildOf(renderer.transform.parent)), Is.True);
                            renderer.BakeMesh(baked);
                            foreach (Vector3 vertex in baked.vertices)
                                Assert.That(camera.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex)).z,
                                    Is.GreaterThan(camera.nearClipPlane));
                            // Relaxed arms hang from shoulder pivots anchored to the body's heading, not the head:
                            // the pivot sits below the eye, on its own side of the yaw-only frame.
                            Vector3 pivot = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * (renderer.transform.parent.position - camera.transform.position);
                            Assert.That(pivot.y, Is.LessThan(0f));
                            Assert.That(pivot.x, renderer.name == "LeftArm" ? Is.LessThan(0f) : Is.GreaterThan(0f));
                        }
                        Assert.That(visuals.transform.Find("Left Foot").gameObject.activeSelf, Is.False);
                        Assert.That(visuals.transform.Find("Right Foot").gameObject.activeSelf, Is.False);
                    }
                    limbs.Apply(MovementState.Ground, 0f, Vector3.zero, Vector3.zero);
                    Assert.That(visuals.transform.Cast<Transform>().All(t => !t.gameObject.activeSelf), Is.True);
                }
                PlayerPrefabGenerator.RebuildLimbs(visuals, null);
                Assert.That(visuals.GetComponentsInChildren<SkinnedMeshRenderer>(true), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(visuals);
            }
        }
    }
}
