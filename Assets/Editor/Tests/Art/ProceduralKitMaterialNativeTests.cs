// ============================================================================
// ProceduralKitMaterialNativeTests.cs
// ============================================================================
// PURPOSE:
//   Exercises appearance preservation and provenance using real URP materials.
//   Preprocessed sources carry colour in a base map and emission in a separate
//   map; generator updates must retain both without overwriting artist edits.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Art.
// KEY RESPONSIBILITIES:
//   - Verify base/emission maps, UVs, keywords and surface values survive copying.
//   - Verify previous-hash migration, stale rebuild and serialized provenance stability.
//   - Verify edited output stays unchanged, including deliberate map removals.
//   - Verify generated texture adoption overrides the base but retains emission.
// DEPENDENCIES:
//   - NUnit, UnityEditor serialization, URP Lit and Editor.Procedural setup.
// USAGE NOTES:
//   NativeUnity Edit Mode checks for the coordinator; no focus or Play Mode.
//   Temporary objects/assets are destroyed in finally. Adoption only reads prepared
//   production maps; importer repair is tested on a uniquely named texture copy.
// ============================================================================
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Editor.Procedural;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Art
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    [Category("NativeUnity")]
    public sealed class ProceduralKitMaterialNativeTests
    {
        private const string Slot = "castle_missing_material_test";

        [TestCase("none")] [TestCase("tint")] [TestCase("base-removal")]
        [TestCase("emission-removal")] [TestCase("replacement")] [TestCase("uv")]
        [TestCase("smoothness")] [TestCase("keyword")] [TestCase("unknown-version")]
        public void PreviousHashMigratesOnlyExactGeneratedOutput(string edit)
        {
            var source = NewMaterial(); var output = NewMaterial();
            var map = Pixel(Color.red); var replacement = Pixel(Color.red);
            try
            {
                source.SetTexture("_BaseMap", map); source.SetTexture("_EmissionMap", map);
                MissingSet();
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot, true), Is.True);
                output.SetOverrideTag("WorsenKitHashVersion", "");
                // Recreate the pre-f7fef42 serialized-only hash, not the current
                // safe hash. Unsaved map refs are the version's lossy boundary.
                var hash = typeof(ProceduralKitAssetSetup).GetMethod("MaterialHash", BindingFlags.NonPublic | BindingFlags.Static);
                string previous = (string)hash.Invoke(null, new object[] { output, false });
                Assert.That((string)hash.Invoke(null, new object[] { output, true }), Is.Not.EqualTo(previous));
                output.SetOverrideTag("WorsenKitGeneratedHash", previous);
                if (edit == "tint") output.color = Color.magenta;
                if (edit == "base-removal") output.SetTexture("_BaseMap", null);
                if (edit == "emission-removal") output.SetTexture("_EmissionMap", null);
                if (edit == "replacement") output.SetTexture("_BaseMap", replacement);
                if (edit == "uv") output.SetTextureOffset("_BaseMap", Vector2.one);
                if (edit == "smoothness") output.SetFloat("_Smoothness", .91f);
                if (edit == "keyword") output.EnableKeyword("_EMISSION");
                if (edit == "unknown-version") output.SetOverrideTag("WorsenKitHashVersion", "future");
                string before = EditorJsonUtility.ToJson(output);
                if (edit == "none") MissingSet();
                else LogAssert.Expect(LogType.Warning, "Preserving edited or unrecognised kit material: " + Slot);
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot), Is.EqualTo(edit == "none"));
                if (edit != "none") Assert.That(EditorJsonUtility.ToJson(output), Is.EqualTo(before));
                else
                {
                    Assert.That(output.GetTag("WorsenKitHashVersion", false), Is.EqualTo("2"));
                    Assert.That(output.GetTexture("_BaseMap"), Is.SameAs(map));
                    Assert.That(output.GetTexture("_EmissionMap"), Is.SameAs(map));
                    string migrated = EditorJsonUtility.ToJson(output);
                    Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot), Is.False);
                    Assert.That(EditorJsonUtility.ToJson(output), Is.EqualTo(migrated));
                    source.color = Color.blue;
                    MissingSet();
                    Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot), Is.True);
                    Assert.That(output.color, Is.EqualTo(Color.blue));
                }
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(output); Object.DestroyImmediate(map); Object.DestroyImmediate(replacement); }
        }

        [Test]
        public void UrpPreprocessedBaseAndEmissionAppearanceIsPreserved()
        {
            var source = NewMaterial(); var output = NewMaterial();
            var baseMap = Pixel(new Color(.2f, .3f, .4f)); var emissionMap = Pixel(new Color(1f, .4f, .1f));
            try
            {
                source.SetColor("_BaseColor", Color.white);
                source.SetTexture("_BaseMap", baseMap);
                source.SetTextureScale("_BaseMap", new Vector2(2f, 3f));
                source.SetTextureOffset("_BaseMap", new Vector2(.2f, .3f));
                source.SetColor("_EmissionColor", new Color(2f, 1f, .5f));
                source.SetTexture("_EmissionMap", emissionMap);
                source.SetTextureScale("_EmissionMap", new Vector2(4f, 5f));
                source.SetTextureOffset("_EmissionMap", new Vector2(.4f, .5f));
                source.EnableKeyword("_EMISSION");
                source.SetFloat("_Smoothness", .67f); source.SetFloat("_Metallic", .23f);
                source.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                ProceduralKitAssetSetup.CopyEmbeddedAppearance(output, source);
                Assert.That(output.GetColor("_BaseColor"), Is.EqualTo(Color.white));
                foreach (string property in new[] { "_BaseMap", "_EmissionMap" })
                {
                    Assert.That(output.GetTexture(property), Is.SameAs(source.GetTexture(property)));
                    Assert.That(output.GetTextureScale(property), Is.EqualTo(source.GetTextureScale(property)));
                    Assert.That(output.GetTextureOffset(property), Is.EqualTo(source.GetTextureOffset(property)));
                }
                Assert.That(output.GetColor("_EmissionColor"), Is.EqualTo(source.GetColor("_EmissionColor")));
                Assert.That(output.IsKeywordEnabled("_EMISSION"), Is.True);
                Assert.That(output.globalIlluminationFlags, Is.EqualTo(source.globalIlluminationFlags));
                Assert.That(output.GetFloat("_Smoothness"), Is.EqualTo(.67f));
                Assert.That(output.GetFloat("_Metallic"), Is.EqualTo(.23f));
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(output); Object.DestroyImmediate(baseMap); Object.DestroyImmediate(emissionMap); }
        }

        [Test]
        public void LegacyWhiteSlotRebuildsFromEmbeddedMapAndEmission()
        {
            var source = NewMaterial(); var output = NewMaterial(); var map = Pixel(Color.red);
            try
            {
                source.SetTexture("_BaseMap", map);
                source.SetColor("_EmissionColor", Color.yellow); source.EnableKeyword("_EMISSION");
                output.color = source.color; output.SetFloat("_Smoothness", .15f);
                MissingSet();
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot), Is.True);
                Assert.That(output.GetTexture("_BaseMap"), Is.SameAs(map));
                Assert.That(output.GetColor("_EmissionColor"), Is.EqualTo(Color.yellow));
                Assert.That(output.IsKeywordEnabled("_EMISSION"), Is.True);
                Assert.That(output.GetTag("WorsenKitSourceHash", false), Does.Match("^[0-9a-f]{64}$"));
                Assert.That(output.GetTag("WorsenKitGeneratedHash", false), Does.Match("^[0-9a-f]{64}$"));
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(output); Object.DestroyImmediate(map); }
        }

        [Test]
        public void StaleOutputRebuildsAndSerializedRepeatIsIdempotent()
        {
            var source = NewMaterial(); var output = NewMaterial(); var restored = NewMaterial();
            try
            {
                source.SetColor("_BaseColor", Color.red); source.SetColor("_EmissionColor", Color.yellow);
                MissingSet();
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot, true), Is.True);
                string oldSource = output.GetTag("WorsenKitSourceHash", false);
                source.SetColor("_BaseColor", Color.blue); source.SetColor("_EmissionColor", Color.cyan);
                source.SetFloat("_Smoothness", .72f); source.SetFloat("_Metallic", .28f);
                MissingSet();
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot), Is.True);
                Assert.That(output.GetTag("WorsenKitSourceHash", false), Is.Not.EqualTo(oldSource));
                Assert.That(output.color, Is.EqualTo(Color.blue));
                Assert.That(output.GetColor("_EmissionColor"), Is.EqualTo(Color.cyan));
                Assert.That(output.GetFloat("_Smoothness"), Is.EqualTo(.72f));
                Assert.That(output.GetFloat("_Metallic"), Is.EqualTo(.28f));
                string json = EditorJsonUtility.ToJson(output);
                EditorJsonUtility.FromJsonOverwrite(json, restored);
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(restored, source, "Castle", Slot), Is.False);
                Assert.That(EditorJsonUtility.ToJson(restored), Is.EqualTo(json));
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(output); Object.DestroyImmediate(restored); }
        }

        [TestCase("tint")] [TestCase("map-removal")] [TestCase("emission")]
        [TestCase("emission-map-removal")] [TestCase("map-replacement")]
        [TestCase("uv")] [TestCase("smoothness")] [TestCase("metallic")]
        [TestCase("culling")] [TestCase("keyword")] [TestCase("tag")]
        public void ArtistEditsSurviveAChangedSource(string edit)
        {
            var source = NewMaterial(); var output = NewMaterial(); var map = Pixel(Color.red); var replacement = Pixel(Color.red);
            try
            {
                source.SetTexture("_BaseMap", map);
                source.SetTexture("_EmissionMap", map);
                MissingSet();
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot, true), Is.True);
                Assert.That(output.GetTexture("_BaseMap"), Is.SameAs(map));
                Assert.That(output.GetTexture("_EmissionMap"), Is.SameAs(map));
                if (edit == "tint") output.color = Color.magenta;
                if (edit == "map-removal") output.SetTexture("_BaseMap", null);
                if (edit == "emission-map-removal") output.SetTexture("_EmissionMap", null);
                if (edit == "map-replacement") output.SetTexture("_BaseMap", replacement);
                if (edit == "emission") output.SetColor("_EmissionColor", Color.green);
                if (edit == "uv") output.SetTextureScale("_BaseMap", new Vector2(3f, 4f));
                if (edit == "smoothness") output.SetFloat("_Smoothness", .81f);
                if (edit == "metallic") output.SetFloat("_Metallic", .82f);
                if (edit == "culling") output.SetFloat("_Cull", 0f);
                if (edit == "keyword") output.EnableKeyword("_EMISSION");
                if (edit == "tag") output.SetOverrideTag("ArtistNote", "keep");
                source.color = Color.blue;
                string before = EditorJsonUtility.ToJson(output);
                LogAssert.Expect(LogType.Warning, "Preserving edited or unrecognised kit material: " + Slot);
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot), Is.False);
                Assert.That(EditorJsonUtility.ToJson(output), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(output); Object.DestroyImmediate(map); Object.DestroyImmediate(replacement); }
        }

        [TestCase("_BaseMap")] [TestCase("_EmissionMap")]
        public void PersistentMapRemovalSurvivesReloadAndChangedSource(string property)
        {
            string root = "Assets/KitMaterialTest-" + Guid.NewGuid().ToString("N");
            var source = NewMaterial(); var output = NewMaterial(); var map = Pixel(Color.red);
            try
            {
                Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(root)), Is.Not.Empty);
                AssetDatabase.CreateAsset(map, root + "/Map.asset");
                source.SetTexture(property, map);
                MissingSet();
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot, true), Is.True);
                Assert.That(output.GetTexture(property), Is.EqualTo(map));
                string path = root + "/Material.mat";
                output.name = "Material";
                // Stamp after assigning the persistent asset name, as the builder does.
                MissingSet();
                ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot, true);
                AssetDatabase.CreateAsset(output, path); AssetDatabase.SaveAssetIfDirty(output);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                output = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(output.GetTexture(property), Is.EqualTo(map));
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot), Is.False);
                output.SetTexture(property, null); EditorUtility.SetDirty(output); AssetDatabase.SaveAssetIfDirty(output);
                string before = File.ReadAllText(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                output = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(output.GetTexture(property), Is.Null);
                source.SetColor("_BaseColor", Color.blue);
                LogAssert.Expect(LogType.Warning, "Preserving edited or unrecognised kit material: " + Slot);
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot), Is.False);
                Assert.That(output.GetTexture(property), Is.Null);
                Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(source);
                if (output != null && !EditorUtility.IsPersistent(output)) Object.DestroyImmediate(output);
                if (map != null && !EditorUtility.IsPersistent(map)) Object.DestroyImmediate(map);
                AssetDatabase.DeleteAsset(root);
            }
        }

        [Test, Timeout(300000)]
        public void ImporterPreparationIsExplicitAndReadOnlyLoadingCannotRepairIt()
        {
            string root = "Assets/KitImporterTest-" + Guid.NewGuid().ToString("N");
            string path = root + "/Normal.png";
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var load = typeof(ProceduralKitAssetSetup).GetMethod("LoadPreparedTexture", flags);
            var prepare = typeof(ProceduralKitAssetSetup).GetMethod("ImportTexture", flags);
            try
            {
                Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(root)), Is.Not.Empty);
                File.Copy(ProceduralKitAssetSetup.TexturePath("Castle", "castle_stone", "Normal"), path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.SaveAndReimport();
                string unprepared = File.ReadAllText(path + ".meta");
                var error = Assert.Throws<TargetInvocationException>(() => load.Invoke(null, new object[] { path, true, false }));
                Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(File.ReadAllText(path + ".meta"), Is.EqualTo(unprepared));
                prepare.Invoke(null, new object[] { path, true, false });
                importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.NormalMap)); Assert.That(importer.sRGBTexture, Is.False);
                string prepared = File.ReadAllText(path + ".meta");
                Assert.That(load.Invoke(null, new object[] { path, true, false }), Is.Not.Null);
                prepare.Invoke(null, new object[] { path, true, false });
                Assert.That(File.ReadAllText(path + ".meta"), Is.EqualTo(prepared));
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [Test]
        public void UntaggedArtistEmissionIsNotMistakenForLegacyOutput()
        {
            var source = NewMaterial(); var output = NewMaterial();
            try
            {
                output.color = source.color; output.SetFloat("_Smoothness", .15f);
                output.SetColor("_EmissionColor", Color.magenta);
                string before = EditorJsonUtility.ToJson(output);
                LogAssert.Expect(LogType.Warning, "Preserving edited or unrecognised kit material: " + Slot);
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", Slot), Is.False);
                Assert.That(EditorJsonUtility.ToJson(output), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(output); }
        }

        [Test]
        [Timeout(300000)]
        public void CompleteGeneratedSetOverridesPreprocessedBaseButKeepsEmission()
        {
            var source = NewMaterial(); var output = NewMaterial(); var map = Pixel(Color.red);
            string[] suffixes = { "Albedo", "Normal", "Smoothness" };
            string[] importPaths = Array.ConvertAll(suffixes, suffix => ProceduralKitAssetSetup.TexturePath("Castle", "castle_stone", suffix) + ".meta");
            try
            {
                string[] importMetadata = Array.ConvertAll(importPaths, File.ReadAllText);
                source.SetTexture("_BaseMap", map); source.SetTexture("_EmissionMap", map);
                source.SetColor("_EmissionColor", new Color(2f, 1f, .5f)); source.EnableKeyword("_EMISSION");
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", "castle_stone", true), Is.True);
                Assert.That(AssetDatabase.GetAssetPath(output.GetTexture("_BaseMap")),
                    Is.EqualTo(ProceduralKitAssetSetup.TexturePath("Castle", "castle_stone", "Albedo")));
                Assert.That(output.GetTexture("_EmissionMap"), Is.SameAs(map));
                Assert.That(output.GetColor("_EmissionColor"), Is.EqualTo(source.GetColor("_EmissionColor")));
                Assert.That(output.IsKeywordEnabled("_EMISSION"), Is.True);
                string before = EditorJsonUtility.ToJson(output);
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", "castle_stone"), Is.False);
                Assert.That(EditorJsonUtility.ToJson(output), Is.EqualTo(before));
                // An older generated-set/source id must rebuild even though the
                // old one-time adoption tag is present. No source file is edited.
                output.SetOverrideTag("WorsenKitSourceHash", "older-texture-set");
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", "castle_stone"), Is.True);
                Assert.That(output.GetTexture("_EmissionMap"), Is.SameAs(map));
                Assert.That(output.IsKeywordEnabled("_NORMALMAP"), Is.True);
                // A deliberate removal is different from a stale source and
                // must remain removed even when emission changes upstream.
                output.SetTexture("_BumpMap", null);
                source.SetColor("_EmissionColor", Color.cyan);
                before = EditorJsonUtility.ToJson(output);
                LogAssert.Expect(LogType.Warning, "Preserving edited or unrecognised kit material: castle_stone");
                Assert.That(ProceduralKitAssetSetup.RebuildSlotMaterial(output, source, "Castle", "castle_stone"), Is.False);
                Assert.That(EditorJsonUtility.ToJson(output), Is.EqualTo(before));
                for (int i = 0; i < importPaths.Length; i++)
                    Assert.That(File.ReadAllText(importPaths[i]), Is.EqualTo(importMetadata[i]), "Material tests must not rewrite project importers.");
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(output); Object.DestroyImmediate(map); }
        }

        private static void MissingSet()
            => LogAssert.Expect(LogType.Warning, "Missing kit texture; keeping existing material/flat colour: Assets/Art/Textures/Castle/" + Slot + "_Albedo.png");

        private static Material NewMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            return new Material(shader) { name = "kit material fixture" };
        }

        private static Texture2D Pixel(Color color)
        {
            var texture = new Texture2D(1, 1) { name = "embedded colour" };
            texture.SetPixel(0, 0, color); texture.Apply();
            return texture;
        }
    }
}
