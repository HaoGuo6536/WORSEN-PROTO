// ============================================================================
// ProceduralKitTextureNativeTests.cs
// ============================================================================
// PURPOSE:
//   Exercises generated texture adoption with real URP materials and importers.
//   These tests verify editor-only effects that the managed harness cannot prove,
//   including channel selection, normal import and preservation of artist edits.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Art.
// KEY RESPONSIBILITIES:
//   - Verify maps, keywords and scale for every generated slot.
//   - Verify repeat setup leaves materials and importer metadata unchanged.
//   - Verify edited materials and incomplete sets are not overwritten.
// DEPENDENCIES:
//   - NUnit, UnityEditor import APIs, URP Lit and Editor.Procedural setup.
// USAGE NOTES:
//   Native Edit Mode, coordinator only; imports generated textures, never scenes.
//   Materials are temporary and destroyed in finally. No Play Mode or focus needed.
// ============================================================================
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Editor.Procedural;

namespace Worsen.Tests.Art
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    [Category("NativeUnity")]
    public sealed class ProceduralKitTextureNativeTests
    {
        [TestCase("Castle")] [TestCase("Hospital")] [TestCase("School")] [TestCase("Basement")]
        [Timeout(300000)]
        public void EverySlotAdoptsCorrectMapsAndSecondSetupIsIdempotent(string theme)
        {
            var recipes = Directory.GetFiles("Assets/Art/Textures/" + theme, "*.json");
            Assert.That(recipes, Is.Not.Empty);
            foreach (string recipe in recipes)
            {
                string slot = Path.GetFileNameWithoutExtension(recipe);
                string hex = Regex.Match(File.ReadAllText(recipe), "\"paletteSrgb\": \"(#[a-fA-F0-9]{6})\"").Groups[1].Value;
                Assert.That(ColorUtility.TryParseHtmlString(hex, out Color palette), Is.True, slot);
                var material = Flat(palette);
                try
                {
                    material.SetColor("_EmissionColor", Color.red); // Unrelated edit must survive initial adoption.
                    Assert.That(ProceduralKitAssetSetup.ApplySlotTextures(material, theme, slot), Is.True, slot);
                    string[] suffixes = { "Albedo", "Normal", "Smoothness" };
                    string[] properties = { "_BaseMap", "_BumpMap", "_MetallicGlossMap" };
                    var meta = new string[3];
                    for (int i = 0; i < suffixes.Length; i++)
                    {
                        string path = ProceduralKitAssetSetup.TexturePath(theme, slot, suffixes[i]);
                        Assert.That(AssetDatabase.GetAssetPath(material.GetTexture(properties[i])), Is.EqualTo(path));
                        Assert.That(material.GetTextureScale(properties[i]), Is.EqualTo(new Vector2(.5f, .5f)));
                        Assert.That(material.GetTextureOffset(properties[i]), Is.EqualTo(Vector2.zero));
                        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                        Assert.That(importer.textureType, Is.EqualTo(i == 1 ? TextureImporterType.NormalMap : TextureImporterType.Default));
                        Assert.That(importer.sRGBTexture, Is.EqualTo(i == 0));
                        Assert.That(importer.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
                        Assert.That(importer.wrapModeV, Is.EqualTo(TextureWrapMode.Repeat));
                        Assert.That(importer.mipmapEnabled, Is.True);
                        Assert.That(importer.alphaIsTransparency, Is.False);
                        Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
                        Assert.That(importer.convertToNormalmap, Is.False);
                        Assert.That(importer.flipGreenChannel, Is.False);
                        meta[i] = File.ReadAllText(path + ".meta");
                    }
                    Assert.That(material.color, Is.EqualTo(Color.white));
                    Assert.That(material.GetColor("_EmissionColor"), Is.EqualTo(Color.red));
                    Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(1f));
                    Assert.That(material.GetFloat("_WorkflowMode"), Is.EqualTo(1f));
                    Assert.That(material.GetFloat("_SmoothnessTextureChannel"), Is.EqualTo(0f));
                    Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
                    Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
                    Assert.That(material.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"), Is.False);
                    string before = EditorJsonUtility.ToJson(material);
                    Assert.That(ProceduralKitAssetSetup.ApplySlotTextures(material, theme, slot), Is.False);
                    Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(before));
                    for (int i = 0; i < suffixes.Length; i++)
                        Assert.That(File.ReadAllText(ProceduralKitAssetSetup.TexturePath(theme, slot, suffixes[i]) + ".meta"), Is.EqualTo(meta[i]));
                    material.color = Color.magenta;
                    material.SetTexture("_BumpMap", null); // Deliberate artist removal is not self-healed.
                    material.SetTextureScale("_BaseMap", new Vector2(3f, 4f));
                    material.SetFloat("_Smoothness", .42f);
                    before = EditorJsonUtility.ToJson(material);
                    Assert.That(ProceduralKitAssetSetup.ApplySlotTextures(material, theme, slot), Is.False);
                    Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(before));
                }
                finally { Object.DestroyImmediate(material); }
            }
        }

        [TestCase("tint")] [TestCase("map")] [TestCase("scale")]
        [TestCase("offset")] [TestCase("smoothness")]
        public void PreexistingArtistOverridesAreNotAdopted(string change)
        {
            ColorUtility.TryParseHtmlString("#4a4f55", out Color palette);
            var material = Flat(palette);
            try
            {
                if (change == "tint") material.color = Color.magenta;
                if (change == "map") material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                if (change == "scale") material.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
                if (change == "offset") material.SetTextureOffset("_BaseMap", new Vector2(.1f, 0f));
                if (change == "smoothness") material.SetFloat("_Smoothness", .7f);
                string before = EditorJsonUtility.ToJson(material);
                LogAssert.Expect(LogType.Warning, "Preserving edited kit material; texture adoption not applied: castle_stone");
                Assert.That(ProceduralKitAssetSetup.ApplySlotTextures(material, "Castle", "castle_stone"), Is.False);
                Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void MissingSetWarnsAndLeavesFlatColourUnchanged()
        {
            var material = Flat(Color.gray);
            try
            {
                string before = EditorJsonUtility.ToJson(material);
                LogAssert.Expect(LogType.Warning, "Missing kit texture; keeping existing material/flat colour: Assets/Art/Textures/Castle/castle_missing_test_Albedo.png");
                Assert.That(ProceduralKitAssetSetup.ApplySlotTextures(material, "Castle", "castle_missing_test"), Is.False);
                Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(material); }
        }

        private static Material Flat(Color palette)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "Coordinator must import URP before running native tests.");
            var material = new Material(shader) { color = palette };
            material.SetFloat("_Smoothness", .15f);
            return material;
        }
    }
}
