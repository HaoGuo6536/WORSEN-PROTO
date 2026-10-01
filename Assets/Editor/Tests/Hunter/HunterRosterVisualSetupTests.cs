// ============================================================================
// HunterRosterVisualSetupTests.cs
// ============================================================================
// PURPOSE:
//   Proves manifest parsing and colour conversion without the Unity engine, then
//   checks the real generated roster under the coordinator's admitted editor run.
//   Saved content tests deliberately fail on missing art rather than substituting it.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), test suite (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Exercise real manifests, invalid inputs and managed sRGB conversion headlessly.
//   - Verify ten saved rigs, six clip roles, project materials and visible dimensions.
//   - Compare collision and protected gameplay/source assets against the base.
//   - Prove first-import shader validation and repeated-build GUID/material stability.
// DEPENDENCIES:
//   - HunterRosterVisualSetup, Hunter configs, NUnit and UnityEditor test APIs.
// USAGE NOTES:
//   Pure fixture never calls UnityEditor or native colour helpers. Unity fixture
//   runs Build in OneTimeSetUp and again for repeatability; hold the Unity lease.
//   No Play Mode/focus requirement. Generated assets are intentional setup output,
//   not disposable fixtures; all loaded prefab contents are released in finally.
//   The fresh pack-import regression alone owns a temporary material folder and
//   deletes it in finally, without replacing any roster or vendor assets.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Worsen.Domain.Hunter;
using Worsen.Editor.Hunter;
using Setup = Worsen.Editor.Hunter.HunterRosterVisualSetup;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterRosterVisualSetupTests
    {
        private const string Json = "{\"schema_version\":1,\"hunter\":\"Echo\",\"fps\":30,\"height_m\":1.8," +
            "\"actions\":[{\"name\":\"idle\",\"frames\":[0,60],\"loop\":true},{\"name\":\"walk\",\"frames\":[0,30],\"loop\":true}," +
            "{\"name\":\"run\",\"frames\":[0,20],\"loop\":true},{\"name\":\"ready\",\"frames\":[0,18],\"loop\":false}," +
            "{\"name\":\"attack\",\"frames\":[0,30],\"loop\":false,\"contact_frame\":12},{\"name\":\"hit\",\"frames\":[0,18],\"loop\":false}]," +
            "\"materials\":[{\"name\":\"Cloth\",\"base_color_srgb\":[0.5,0.02,1,0.25],\"emission_color_srgb\":[1,0.5,0,1],\"emission_strength\":2}]}";

        [Test]
        public void ParsesFramesLoopFlagsContactAndEmissionWithoutEngine()
        {
            var manifest = Setup.ParseManifest(Json, "Echo");
            Assert.That(manifest.Height, Is.EqualTo(1.8f)); Assert.That(manifest.Fps, Is.EqualTo(30));
            Assert.That(manifest.Actions.Length, Is.EqualTo(6));
            CollectionAssert.AreEqual(new[] { 0, 60 }, manifest.Actions[0].Frames);
            Assert.That(manifest.Actions[0].Loop, Is.True); Assert.That(manifest.Actions[3].Loop, Is.False);
            Assert.That(manifest.Actions[4].ContactFrame, Is.EqualTo(12));
            Assert.That(manifest.Materials[0].EmissionStrength, Is.EqualTo(2f));
            Assert.That(manifest.Materials[0].BaseColor[3], Is.EqualTo(0.25f));
        }
        [Test]
        public void ManifestLoopFlagsAreDataNotHardcodedByRole()
        {
            var manifest = Setup.ParseManifest(Json.Replace("\"loop\":true", "\"loop\":false"), "Echo");
            Assert.That(manifest.Actions.All(action => !action.Loop), Is.True);
        }
        [TestCase("\"schema_version\":1", "\"schema_version\":2")]
        [TestCase("\"height_m\":1.8", "\"height_m\":0")]
        [TestCase("\"fps\":30", "\"fps\":0")]
        [TestCase("\"name\":\"hit\"", "\"name\":\"idle\"")]
        [TestCase("[0,60]", "[60,0]")]
        [TestCase("\"contact_frame\":12", "\"contact_frame\":31")]
        [TestCase(",\"contact_frame\":12", "")]
        [TestCase("\"name\":\"Cloth\"", "\"name\":\"../Escape\"")]
        [TestCase("[0.5,0.02,1,0.25]", "[1.1,0,0,1]")]
        [TestCase("\"emission_strength\":2", "\"emission_strength\":-1")]
        [TestCase(",\"loop\":true", "")]
        public void RejectsInvalidManifest(string before, string after)
        {
            Assert.Throws<FormatException>(() => Setup.ParseManifest(Json.Replace(before, after), "Echo"));
        }
        [TestCase("")]
        [TestCase("{")]
        [TestCase("{}")]
        [TestCase("null")]
        public void RejectsEmptyOrMalformedJson(string json) => Assert.Throws<FormatException>(() => Setup.ParseManifest(json, "Echo"));
        [Test] public void RejectsWrongHunter() => Assert.Throws<FormatException>(() => Setup.ParseManifest(Json, "Mimic"));

        [TestCase("Echo")]
        [TestCase("Weaver")]
        [TestCase("Ticking")]
        [TestCase("Mimic")]
        [TestCase("Herald")]
        [TestCase("Mannequin")]
        [TestCase("Stare")]
        public void ParsesActualCheckedInManifest(string name)
        {
            // Offline runner's working directory is its evidence folder, below this checkout.
            string directory = Directory.GetCurrentDirectory();
            while (directory != null && !File.Exists(Path.Combine(directory, Setup.ManifestPath(name))))
                directory = Directory.GetParent(directory)?.FullName;
            Assert.That(directory, Is.Not.Null, "Could not locate the actual project manifest.");
            var manifest = Setup.ParseManifest(File.ReadAllText(Path.Combine(directory, Setup.ManifestPath(name))), name);
            Assert.That(manifest.Hunter, Is.EqualTo(name)); Assert.That(manifest.Materials.Length, Is.GreaterThan(0));
            Assert.That(manifest.Actions.Where(action => action.Loop).Select(action => action.Name), Is.EquivalentTo(new[] { "idle", "walk", "run" }));
            if (name == "Mimic")
            {
                var cake = manifest.Materials.Single(material => material.Name == "M_HunterMimic_Cake");
                Assert.That(cake.Textures.Base, Is.EqualTo("CakePalette.png"));
                Assert.That(cake.Textures.Emission, Is.EqualTo("CakeEmission.png"));
            }
        }
        [Test]
        public void ConvertsSrgbToLinearAndLeavesAlphaUnchanged()
        {
            Color actual = Setup.ToLinearColor(new[] { 0.5f, 0.02f, 1f, 0.25f });
            Assert.That(actual.r, Is.EqualTo(0.21404114048223255f).Within(0.0000001f));
            Assert.That(actual.g, Is.EqualTo(0.0015479876160990713f).Within(0.00000001f));
            Assert.That(actual.b, Is.EqualTo(1f)); Assert.That(actual.a, Is.EqualTo(0.25f));
            Color threshold = Setup.ToLinearColor(new[] { 0f, 0.04045f, 1f, 0f });
            Assert.That(threshold.r, Is.Zero); Assert.That(threshold.g, Is.EqualTo(0.0031308049535603713f).Within(0.00000001f));
            Assert.That(threshold.a, Is.Zero);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-0.01f)] [TestCase(1.01f)]
        public void RejectsInvalidColourChannel(float value) => Assert.Throws<FormatException>(() => Setup.ToLinearColor(new[] { value, 0f, 0f, 1f }));
        [Test]
        public void RejectsMissingColourChannels()
        {
            Assert.Throws<FormatException>(() => Setup.ToLinearColor(null));
            Assert.Throws<FormatException>(() => Setup.ToLinearColor(new[] { 1f, 1f, 1f }));
        }
    }

    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class HunterRosterVisualSetupUnityTests
    {
        private Dictionary<string, string> protectedFiles;
        private Dictionary<string, string> gameplay;
        private string[] firstPaths;
        private Dictionary<string, string> firstGuids;
        private Dictionary<string, string> firstMaterials;
        [OneTimeSetUp]
        public void BuildRoster()
        {
            protectedFiles = ProtectedFiles();
            gameplay = Setup.Names.ToDictionary(name => name, ProfileGameplay);
            Assert.That(Setup.Build(), Is.EqualTo(Setup.Summary));
            // Capture immediately: another test/Inspector must not warm up the first save.
            firstPaths = GeneratedPaths();
            firstGuids = firstPaths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID);
            firstMaterials = firstPaths.Where(path => path.EndsWith(".mat", StringComparison.Ordinal))
                .ToDictionary(path => path, path => EditorJsonUtility.ToJson(Load<Material>(path)));
        }
        [Test]
        public void TenProfilesUseTenDistinctExpectedPrefabs()
        {
            var paths = Setup.Names.Select(name => AssetDatabase.GetAssetPath(Profile(name).Prefab)).ToArray();
            Assert.That(paths.Distinct().Count(), Is.EqualTo(10));
            CollectionAssert.AreEquivalent(Setup.Names.Select(Setup.PrefabPath), paths);
        }
        [TestCase("Echo")] [TestCase("Weaver")] [TestCase("Ticking")] [TestCase("Ram")] [TestCase("Skip")]
        [TestCase("Mimic")] [TestCase("Blinder")] [TestCase("Herald")] [TestCase("Mannequin")] [TestCase("Stare")]
        public void SavedBodyHasGenericAvatarSixClipsMaterialsAndTargetHeight(string name)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Setup.PrefabPath(name));
            try
            {
                Transform visual = root.transform.Find("Imported Creature"); Assert.That(visual, Is.Not.Null, name);
                Animator animator = visual.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null); Assert.That(animator.enabled, Is.True);
                Assert.That(animator.avatar != null && animator.avatar.isValid && !animator.avatar.isHuman, Is.True, name);
                Assert.That(animator.applyRootMotion, Is.False); Assert.That(animator.runtimeAnimatorController, Is.Null);
                var animation = visual.GetComponent<HunterAnimationDriver>(); Assert.That(animation, Is.Not.Null);
                Assert.That(animation.Animator, Is.EqualTo(animator)); Assert.That(animation.Config, Is.Not.Null);
                var config = animation.Config;
                AnimationClip[] clips = { config.Idle, config.Walk, config.Run, config.Windup, config.Attack, config.Recovery };
                Assert.That(clips.Distinct().Count(), Is.EqualTo(6));
                foreach (AnimationClip clip in clips)
                { Assert.That(clip, Is.Not.Null); Assert.That(clip.length, Is.GreaterThan(0)); Assert.That(clip.legacy, Is.False); }
                var driver = new SerializedObject(root.GetComponent<HunterDriver>());
                Assert.That(driver.FindProperty("_animation").objectReferenceValue, Is.EqualTo(animation));
                var attack = root.GetComponent<HunterAttackDriver>(); Assert.That(attack, Is.Not.Null);
                Assert.That(driver.FindProperty("_attacks").objectReferenceValue, Is.EqualTo(attack));
                var attackConfig = (HunterAttackDriverConfig)new SerializedObject(attack).FindProperty("_config").objectReferenceValue;
                Assert.That(attackConfig, Is.Not.Null);
                Assert.That(new SerializedObject(attackConfig).FindProperty("_warningMaterial").objectReferenceValue, Is.Not.Null);
                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true); Assert.That(renderers.Length, Is.GreaterThan(0));
                foreach (Renderer renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterials.Length, Is.GreaterThan(0));
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null); Assert.That(material.shader, Is.Not.Null);
                        Assert.That(material.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"));
                        Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith(Setup.ArtPath(name) + "/Materials/"));
                    }
                }
                Assert.That(root.GetComponentsInChildren<Renderer>(true).Where(renderer => !renderer.transform.IsChildOf(visual)).All(renderer => !renderer.enabled), Is.True);
                Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(visual.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                float target = name == "Ram" ? 2.1f : name == "Skip" ? 1.2f : name == "Blinder" ? 1.5f :
                    Setup.ParseManifest(File.ReadAllText(Setup.ManifestPath(name)), name).Height;
                Bounds bounds = Setup.MeasureVisualBounds(visual.gameObject);
                Assert.That(Math.Abs(bounds.size.y / target - 1f), Is.LessThanOrEqualTo(Setup.VisualHeightRelativeTolerance), name + " height");
                Assert.That(bounds.min.y, Is.EqualTo(root.transform.position.y).Within(Setup.VisualFootTolerance), name + " feet");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        [Test]
        public void ProjectImportSettingsMatchManifestsAndDisableRootMotion()
        {
            foreach (string name in Setup.Names.Where(value => value != "Ram" && value != "Skip" && value != "Blinder"))
            {
                var manifest = Setup.ParseManifest(File.ReadAllText(Setup.ManifestPath(name)), name);
                var importer = (ModelImporter)AssetImporter.GetAtPath(Setup.ArtPath(name) + "/WORSEN_Hunter" + name + ".fbx");
                Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Generic));
                Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CreateFromThisModel));
                Assert.That(importer.bakeAxisConversion, Is.False);
                Assert.That(importer.clipAnimations.Length, Is.EqualTo(6));
                foreach (var clip in importer.clipAnimations)
                {
                    var action = manifest.Actions.Single(value => value.Name == clip.name);
                    Assert.That(clip.loopTime, Is.EqualTo(action.Loop)); Assert.That(clip.loopPose, Is.EqualTo(action.Loop));
                    Assert.That(clip.lastFrame - clip.firstFrame, Is.EqualTo(action.Frames[1] - action.Frames[0]).Within(0.1f));
                    Assert.That(clip.lockRootRotation && clip.lockRootHeightY && clip.lockRootPositionXZ, Is.True);
                }
            }
        }
        [Test]
        public void MotorCollisionGameplayAndTickingKeysRemainUnchanged()
        {
            foreach (string name in Setup.Names)
            {
                GameObject source = Load<GameObject>(Setup.BasePrefabPath(name)); GameObject target = Profile(name).Prefab;
                var a = source.GetComponent<CapsuleCollider>(); var b = target.GetComponent<CapsuleCollider>();
                Assert.That(b.height, Is.EqualTo(a.height), name); Assert.That(b.radius, Is.EqualTo(a.radius), name);
                Assert.That(b.center, Is.EqualTo(a.center), name); Assert.That(b.direction, Is.EqualTo(a.direction), name);
                Assert.That(b.enabled, Is.EqualTo(a.enabled), name); Assert.That(b.isTrigger, Is.EqualTo(a.isTrigger), name);
                Assert.That(b.sharedMaterial, Is.EqualTo(a.sharedMaterial), name); Assert.That(b.contactOffset, Is.EqualTo(a.contactOffset), name);
                Assert.That(b.includeLayers.value, Is.EqualTo(a.includeLayers.value), name); Assert.That(b.excludeLayers.value, Is.EqualTo(a.excludeLayers.value), name);
                Assert.That(target.layer, Is.EqualTo(source.layer), name); Assert.That(target.transform.localScale, Is.EqualTo(source.transform.localScale), name);
                Assert.That(target.GetComponent<Rigidbody>().isKinematic, Is.EqualTo(source.GetComponent<Rigidbody>().isKinematic));
                Assert.That(target.GetComponent<Rigidbody>().useGravity, Is.EqualTo(source.GetComponent<Rigidbody>().useGravity));
                Assert.That(new SerializedObject(target.GetComponent<HunterDriver>()).FindProperty("_config").objectReferenceValue,
                    Is.EqualTo(new SerializedObject(source.GetComponent<HunterDriver>()).FindProperty("_config").objectReferenceValue));
                foreach (Component component in source.GetComponents<Component>()) Assert.That(target.GetComponent(component.GetType()), Is.Not.Null, name);
                Assert.That(ProfileGameplay(name), Is.EqualTo(gameplay[name]), name + " tuning changed");
            }
            AssertProtectedFiles();
        }
        [Test]
        public void MimicClosedSurfaceUsesRealCakePropertiesAndTextures()
        {
            Material cake = Load<Material>(Setup.CakeMaterialPath);
            Material mimic = Load<Material>(Setup.ArtPath("Mimic") + "/Materials/M_HunterMimic_Cake.mat");
            Assert.That(mimic.shader, Is.EqualTo(cake.shader));
            foreach (string field in new[] { "_BaseColor", "_EmissionColor" }) Assert.That(mimic.GetColor(field), Is.EqualTo(cake.GetColor(field)));
            foreach (string field in new[] { "_BaseMap", "_EmissionMap" })
            {
                Assert.That(mimic.GetTexture(field), Is.EqualTo(cake.GetTexture(field)));
                Assert.That(mimic.GetTextureScale(field), Is.EqualTo(cake.GetTextureScale(field)));
                Assert.That(mimic.GetTextureOffset(field), Is.EqualTo(cake.GetTextureOffset(field)));
            }
            Assert.That(mimic.GetFloat("_Smoothness"), Is.EqualTo(cake.GetFloat("_Smoothness")));
            CollectionAssert.AreEquivalent(cake.shaderKeywords, mimic.shaderKeywords);
        }
        [Test]
        public void MimicClosedExteriorMatchesRealCakeGeometry()
        {
            GameObject mimic = PrefabUtility.LoadPrefabContents(Setup.PrefabPath("Mimic"));
            GameObject cake = null;
            try
            {
                cake = PrefabUtility.LoadPrefabContents("Assets/Art/Horror/Cake/CakeSlice.prefab");
                Transform visual = mimic.transform.Find("Imported Creature");
                var animation = visual.GetComponent<HunterAnimationDriver>();
                animation.Config.Idle.SampleAnimation(animation.Animator.gameObject, 0f);
                Vector3[] actual = SurfaceVertices(visual.gameObject, "M_HunterMimic_Cake");
                Vector3[] expected = SurfaceVertices(cake, null);
                CenterAtFeet(actual); CenterAtFeet(expected);
                Assert.That(actual.Length, Is.GreaterThan(0)); Assert.That(expected.Length, Is.GreaterThan(0));
                // Vertex duplication from hard normals/UV seams is irrelevant; both directions
                // must match the real exterior, including the closed jaw, within the art contract.
                foreach (Vector3 point in actual) Assert.That(expected.Min(other => (point - other).sqrMagnitude), Is.LessThanOrEqualTo(0.005f * 0.005f));
                foreach (Vector3 point in expected) Assert.That(actual.Min(other => (point - other).sqrMagnitude), Is.LessThanOrEqualTo(0.005f * 0.005f));
            }
            finally
            { if (cake != null) PrefabUtility.UnloadPrefabContents(cake); PrefabUtility.UnloadPrefabContents(mimic); }
        }
        private static Vector3[] SurfaceVertices(GameObject root, string materialName)
        {
            var points = new List<Vector3>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                Mesh mesh = null; bool temporary = false;
                try
                {
                    if (renderer is SkinnedMeshRenderer skin) { mesh = new Mesh(); temporary = true; skin.BakeMesh(mesh); }
                    else mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    Assert.That(mesh, Is.Not.Null);
                    Vector3[] vertices = mesh.vertices;
                    Matrix4x4 toWorld = renderer is SkinnedMeshRenderer
                        ? Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one)
                        : renderer.transform.localToWorldMatrix;
                    for (int slot = 0; slot < mesh.subMeshCount; slot++)
                    {
                        if (materialName != null && renderer.sharedMaterials[slot].name != materialName) continue;
                        foreach (int index in mesh.GetIndices(slot).Distinct()) points.Add(toWorld.MultiplyPoint3x4(vertices[index]));
                    }
                }
                finally { if (temporary) Object.DestroyImmediate(mesh); }
            }
            return points.ToArray();
        }
        private static void CenterAtFeet(Vector3[] points)
        {
            Assert.That(points, Is.Not.Empty);
            Bounds bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 point in points) bounds.Encapsulate(point);
            Vector3 offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            for (int i = 0; i < points.Length; i++) points[i] -= offset;
        }
        [Test]
        public void FirstBuildPersistsShaderFloatDefaultsAndValidationIsStable()
        {
            Assert.That(firstMaterials, Is.Not.Empty);
            foreach (var pair in firstMaterials)
            {
                Material clone = Object.Instantiate(Load<Material>(pair.Key));
                try
                {
                    // Restore the first-build snapshot even if the repeat-build test ran first.
                    EditorJsonUtility.FromJsonOverwrite(pair.Value, clone);
                    var serialized = new SerializedObject(clone);
                    SerializedProperty floats = serialized.FindProperty("m_SavedProperties.m_Floats");
                    var savedNames = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < floats.arraySize; i++)
                        savedNames.Add(floats.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue);
                    for (int i = 0; i < clone.shader.GetPropertyCount(); i++)
                    {
                        ShaderPropertyType type = clone.shader.GetPropertyType(i);
                        if (type == ShaderPropertyType.Float || type == ShaderPropertyType.Range)
                            Assert.That(savedNames, Does.Contain(clone.shader.GetPropertyName(i)), pair.Key);
                    }
                    string before = EditorJsonUtility.ToJson(clone);
                    var editor = (MaterialEditor)UnityEditor.Editor.CreateEditor(clone, typeof(MaterialEditor));
                    try
                    {
                        Assert.That(editor.customShaderGUI, Is.Not.Null, pair.Key);
                        editor.customShaderGUI.ValidateMaterial(clone);
                    }
                    finally { Object.DestroyImmediate(editor); }
                    Assert.That(EditorJsonUtility.ToJson(clone), Is.EqualTo(before), pair.Key + " changed on validation");
                }
                finally { Object.DestroyImmediate(clone); }
            }
        }
        [Test]
        public void FreshPackMaterialIsAlreadyInPostImportForm()
        {
            // Batch 17's Goblin source: use the actual vendor material, never a warmed-up
            // generated variant. Exercise PackMaterial without adding a public test API.
            const string sourceGuid = "44011044716e5da40b1952e25d719fa1";
            Material source = Load<Material>(AssetDatabase.GUIDToAssetPath(sourceGuid));
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id), Is.True);
            Assert.That(guid, Is.EqualTo(sourceGuid)); Assert.That(id, Is.EqualTo(2100000L));
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string sourceHash = Hash(sourcePath), sourceMetaHash = Hash(sourcePath + ".meta");
            var build = typeof(Setup).GetMethod("PackMaterial", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(build, Is.Not.Null);
            string hunter = "PackImportTest_" + Guid.NewGuid().ToString("N");
            string folder = Setup.ArtPath(hunter);
            string key = sourceGuid + "_2100000";
            string path = folder + "/Materials/Pack_" + key + ".mat";
            Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
            try
            {
                AssetDatabase.CreateFolder("Assets/Art/Hunter", hunter);
                AssetDatabase.CreateFolder(folder, "Materials");
                Assert.That(File.Exists(path), Is.False, "The regression requires a genuinely fresh material.");
                Material material = (Material)build.Invoke(null, new object[] { hunter, key, source });
                string firstFile = File.ReadAllText(path), firstJson = EditorJsonUtility.ToJson(material);
                string firstGuid = AssetDatabase.AssetPathToGUID(path);
                Assert.That(material, Is.EqualTo(Load<Material>(path)), "Builder must return the imported asset.");
                var floats = new SerializedObject(material).FindProperty("m_SavedProperties.m_Floats");
                Assert.That(Enumerable.Range(0, floats.arraySize).Select(index =>
                    floats.GetArrayElementAtIndex(index).FindPropertyRelative("first").stringValue), Does.Contain("_XRMotionVectorsPass"));
                var versions = AssetDatabase.LoadAllAssetsAtPath(path).Where(asset =>
                    asset.GetType().FullName == "UnityEditor.Rendering.Universal.AssetVersion").ToArray();
                Assert.That(versions.Length, Is.EqualTo(1), "URP must create its version marker during the first build.");
                string firstVersion = EditorJsonUtility.ToJson(versions[0]);
                Assert.That(new SerializedObject(versions[0]).FindProperty("version").intValue, Is.GreaterThan(0));

                void AssertUnchanged()
                {
                    Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(firstGuid));
                    Assert.That(EditorJsonUtility.ToJson(Load<Material>(path)), Is.EqualTo(firstJson));
                    var importedVersions = AssetDatabase.LoadAllAssetsAtPath(path).Where(asset =>
                        asset.GetType().FullName == "UnityEditor.Rendering.Universal.AssetVersion").ToArray();
                    Assert.That(importedVersions.Length, Is.EqualTo(1));
                    Assert.That(EditorJsonUtility.ToJson(importedVersions[0]), Is.EqualTo(firstVersion));
                    // Flush even a marker-only upgrade before comparing raw on-disk serialization.
                    foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path)) AssetDatabase.SaveAssetIfDirty(asset);
                    Assert.That(File.ReadAllText(path), Is.EqualTo(firstFile));
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssertUnchanged();
                build.Invoke(null, new object[] { hunter, key, source });
                AssertUnchanged();
                Assert.That(Hash(sourcePath), Is.EqualTo(sourceHash));
                Assert.That(Hash(sourcePath + ".meta"), Is.EqualTo(sourceMetaHash));
            }
            finally { if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder); }
        }
        [Test]
        public void SecondBuildKeepsEveryGeneratedGuidAndMaterialValue()
        {
            Assert.That(Setup.Build(), Is.EqualTo(Setup.Summary));
            CollectionAssert.AreEquivalent(firstPaths, GeneratedPaths());
            foreach (var pair in firstGuids) Assert.That(AssetDatabase.AssetPathToGUID(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            foreach (var pair in firstMaterials) Assert.That(EditorJsonUtility.ToJson(Load<Material>(pair.Key)), Is.EqualTo(pair.Value), pair.Key);
            foreach (string name in Setup.Names) Assert.That(ProfileGameplay(name), Is.EqualTo(gameplay[name]), name);
            AssertProtectedFiles();
        }
        private static string[] GeneratedPaths() => Setup.Names.SelectMany(name =>
            AssetDatabase.FindAssets("", new[] { Setup.ArtPath(name), Path.GetDirectoryName(Setup.ProfilePath(name)).Replace('\\', '/') })
                .Select(AssetDatabase.GUIDToAssetPath).Concat(new[] { Setup.PrefabPath(name) }))
            .Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
        private static HunterProfile Profile(string name) => Load<HunterProfile>(Setup.ProfilePath(name));
        private static T Load<T>(string path) where T : Object
        { T asset = AssetDatabase.LoadAssetAtPath<T>(path); Assert.That(asset, Is.Not.Null, path); return asset; }
        private static string ProfileGameplay(string name)
        {
            HunterProfile clone = Object.Instantiate(Profile(name));
            try
            {
                var serialized = new SerializedObject(clone); serialized.FindProperty("_prefab").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo(); return JsonUtility.ToJson(clone);
            }
            finally { Object.DestroyImmediate(clone); }
        }
        private static Dictionary<string, string> ProtectedFiles()
        {
            var roots = new List<string> { TickingProfileSetup.HunterPrefabPath, TickingProfileSetup.KeyPrefabPath,
                TickingProfileSetup.ConfigPath, TickingProfileSetup.DriverPath, TickingProfileSetup.MotorPath, Setup.CakeMaterialPath };
            foreach (string key in new[] { "rusher", "lurker", "watcher", "hexer", "thorncaller" })
            { roots.Add(Setup.PrefabPath(key)); roots.Add(HorrorHunterSetup.ProfileDirectory + "/" + key + ".asset"); }
            foreach (string path in roots) Assert.That(File.Exists(path), Is.True, "Protected source missing: " + path);
            return roots.SelectMany(path => AssetDatabase.GetDependencies(path, true)).Concat(roots).Distinct()
                .SelectMany(path => new[] { path, path + ".meta" }).Where(File.Exists).ToDictionary(path => path, Hash);
        }
        private void AssertProtectedFiles()
        { foreach (var pair in protectedFiles) Assert.That(Hash(pair.Key), Is.EqualTo(pair.Value), pair.Key + " was changed by visual setup"); }
        private static string Hash(string path)
        { using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
    }
}
