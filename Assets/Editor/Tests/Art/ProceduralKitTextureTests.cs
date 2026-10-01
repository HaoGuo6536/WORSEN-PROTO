// ============================================================================
// ProceduralKitTextureTests.cs
// ============================================================================
// PURPOSE:
//   Locks the generated kit texture naming and metre-to-UV contract without
//   loading Unity assets. These cases also require complete on-disk map sets so
//   a partial generation cannot appear as successful pure coverage.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Art.
// KEY RESPONSIBILITIES:
//   - Check metre-to-tile scale, invalid inputs and theme-local path validation.
//   - Require complete 1024 px PNG sets alongside matching per-slot recipes.
// DEPENDENCIES:
//   - NUnit, System.IO and Editor.Procedural texture contract methods.
// USAGE NOTES:
//   Pure Edit Mode tests; no Unity editor process or native object required.
// ============================================================================
using System;
using System.IO;
using NUnit.Framework;
using Worsen.Editor.Procedural;

namespace Worsen.Tests.Art
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralKitTextureTests
    {
        [TestCase(.5f, 2f)] [TestCase(1f, 1f)] [TestCase(2f, .5f)] [TestCase(4f, .25f)]
        public void MetresPerTileIsReciprocalUvScale(float metres, float expected)
        {
            Assert.That(ProceduralKitAssetSetup.TextureScale(metres), Is.EqualTo(expected));
            Assert.That(6f * ProceduralKitAssetSetup.TextureScale(metres), Is.EqualTo(6f / metres));
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)] [TestCase(float.Epsilon)]
        public void InvalidMetreScaleIsRejected(float metres)
            => Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralKitAssetSetup.TextureScale(metres));

        [TestCase("Castle", "castle_stone", "Albedo")]
        [TestCase("Hospital", "hospital_tile", "Normal")]
        [TestCase("School", "school_chalk_green", "Smoothness")]
        [TestCase("Basement", "basement_door_steel", "Albedo")]
        public void MapPathsAreExactAndThemeLocal(string theme, string slot, string suffix)
            => Assert.That(ProceduralKitAssetSetup.TexturePath(theme, slot, suffix),
                Is.EqualTo("Assets/Art/Textures/" + theme + "/" + slot + "_" + suffix + ".png"));

        [TestCase("castle", "castle_stone", "Albedo")]
        [TestCase("Castle", "hospital_tile", "Albedo")]
        [TestCase("Castle", "castle_../stone", "Albedo")]
        [TestCase("Castle", "castle_stone", "../Normal")]
        [TestCase("Castle", "", "Albedo")]
        public void UnknownOrUnsafePathsAreRejected(string theme, string slot, string suffix)
            => Assert.Throws<ArgumentException>(() => ProceduralKitAssetSetup.TexturePath(theme, slot, suffix));

        [TestCase("Castle", 7)] [TestCase("Hospital", 14)]
        [TestCase("School", 19)] [TestCase("Basement", 11)]
        public void EveryPublishedRecipeHasAllThreePngMaps(string theme, int count)
        {
            string folder = "Assets/Art/Textures/" + theme;
            var recipes = Directory.GetFiles(folder, "*.json");
            Assert.That(recipes.Length, Is.EqualTo(count), "Review palette/recipe inventory changes explicitly.");
            foreach (string recipe in recipes)
            {
                Assert.That(File.ReadAllText(recipe), Does.Contain("\"metresPerTile\": 2.0"));
                Assert.That(File.ReadAllText(recipe), Does.Contain("\"resolution\": 1024"));
                foreach (string suffix in new[] { "Albedo", "Normal", "Smoothness" })
                {
                    string path = ProceduralKitAssetSetup.TexturePath(theme, Path.GetFileNameWithoutExtension(recipe), suffix);
                    var bytes = File.ReadAllBytes(path);
                    Assert.That(bytes.Length, Is.GreaterThan(128), path);
                    CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, new ArraySegment<byte>(bytes, 0, 8));
                    // PNG IHDR width and height are unsigned big-endian words.
                    CollectionAssert.AreEqual(new byte[] { 73, 72, 68, 82 }, new ArraySegment<byte>(bytes, 12, 4));
                    CollectionAssert.AreEqual(new byte[] { 0, 0, 4, 0, 0, 0, 4, 0 }, new ArraySegment<byte>(bytes, 16, 8), path);
                    Assert.That(bytes[24], Is.EqualTo(8), "Expected eight-bit channels: " + path);
                    Assert.That(bytes[25], Is.EqualTo(suffix == "Smoothness" ? 6 : 2), "RGBA packing versus RGB maps: " + path);
                }
            }
        }
    }
}
