// ============================================================================
// ProceduralKitMaterialTests.cs
// ============================================================================
// PURPOSE:
//   Locks the kit material provenance decision without creating engine objects.
//   Source changes may rebuild only recognised generator output, never an edited
//   or unrecognised material. Source hashes include both embedded state and maps.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Art.
// KEY RESPONSIBILITIES:
//   - Check stale rebuild, idempotence and fail-closed artist preservation.
//   - Check deterministic, unambiguous embedded/texture-set hashing.
// DEPENDENCIES:
//   - NUnit and Editor.Procedural pure provenance methods.
// USAGE NOTES:
//   Pure Edit Mode tests; no native Unity object, importer or editor required.
// ============================================================================
using System;
using System.Globalization;
using NUnit.Framework;
using Worsen.Editor.Procedural;

namespace Worsen.Tests.Art
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralKitMaterialTests
    {
        [TestCase("new", "old", "generated", "generated", false, true)]
        [TestCase("same", "same", "generated", "generated", false, false)]
        [TestCase("new", "old", "artist", "generated", false, false)]
        [TestCase("same", "same", "artist", "generated", false, false)]
        [TestCase("new", "", "legacy", "", true, true)]
        [TestCase("new", "", "artist", "", false, false)]
        [TestCase("new", "", "generated", "generated", false, false)]
        [TestCase("new", "old", "artist", "generated", true, false)]
        [TestCase("", "old", "generated", "generated", false, false)]
        [TestCase("new", "old", "", "generated", false, false)]
        public void OnlyUneditedStaleOrRecognisedLegacyOutputRebuilds(string source, string savedSource,
            string current, string generated, bool legacy, bool rebuild)
            => Assert.That(ProceduralKitAssetSetup.ShouldRebuildSlot(source, savedSource, current, generated, legacy), Is.EqualTo(rebuild));

        [Test]
        public void EmbeddedAndTextureSetChangesBothInvalidateSource()
        {
            string original = ProceduralKitAssetSetup.SlotSourceHash("base+emission+smoothness+metallic", "set-a");
            Assert.That(original, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(ProceduralKitAssetSetup.SlotSourceHash("base+emission+smoothness+metallic", "set-a"), Is.EqualTo(original));
            Assert.That(ProceduralKitAssetSetup.SlotSourceHash("changed-emission", "set-a"), Is.Not.EqualTo(original));
            Assert.That(ProceduralKitAssetSetup.SlotSourceHash("base+emission+smoothness+metallic", "set-b"), Is.Not.EqualTo(original));
        }

        [Test]
        public void HashFieldsCannotAliasAcrossSeparators()
            => Assert.That(ProceduralKitAssetSetup.SlotSourceHash("a\nb", "c"),
                Is.Not.EqualTo(ProceduralKitAssetSetup.SlotSourceHash("a", "b\nc")));

        [Test]
        public void HashIsIndependentOfCurrentCulture()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                string expected = ProceduralKitAssetSetup.SlotSourceHash("embedded", "maps");
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                Assert.That(ProceduralKitAssetSetup.SlotSourceHash("embedded", "maps"), Is.EqualTo(expected));
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }

        [TestCase(null, "maps")] [TestCase("embedded", null)]
        public void MissingHashInputsAreRejected(string embedded, string maps)
            => Assert.Throws<ArgumentNullException>(() => ProceduralKitAssetSetup.SlotSourceHash(embedded, maps));
    }
}
