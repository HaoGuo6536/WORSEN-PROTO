// ============================================================================
// ProceduralCataloguePublishTests.cs
// ============================================================================
// PURPOSE:
//   Proves the four checked-in theme catalogues survive publication into the
//   catalogue asset that generation reads. A format mismatch once published zero
//   catalogues silently, so every floor fell back to primitive organic rooms.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests (§11) · Procedural content setup.
// KEY RESPONSIBILITIES:
//   - Parse each theme's kit and room manifests and publish them together.
//   - Require every catalogue, theme and template count to round-trip.
// DEPENDENCIES:
//   NUnit, UnityEditor serialization and the Procedural manifest setup.
// USAGE NOTES:
//   Requires Unity Edit Mode (JsonUtility, SerializedObject). Uses a transient
//   catalogue asset instance; nothing is saved.
// ============================================================================
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Procedural;
using Worsen.Editor.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralCataloguePublishTests
    {
        [Test]
        public void AllFourThemeCataloguesSurvivePublication()
        {
            var themes = new[] { "Castle", "Hospital", "School", "Basement" };
            var catalogues = themes.Select(theme => ProceduralRoomManifestSetup.Parse(
                File.ReadAllText("Assets/Art/Environment/" + theme + "/Kit/" + theme + "Kit.manifest.json"),
                File.ReadAllText("Assets/Art/Environment/" + theme + "/Rooms/" + theme + "Rooms.manifest.json"))).ToArray();
            var asset = ScriptableObject.CreateInstance<ProceduralRoomCatalogueData>();
            try
            {
                Assert.That(AssetDatabase.Contains(asset), Is.False, "The fixture must not touch the real catalogue asset.");
                ProceduralRoomManifestSetup.Publish(asset, catalogues);
                var published = new SerializedObject(asset).FindProperty("_catalogues");
                Assert.That(published.arraySize, Is.EqualTo(themes.Length));
                for (int i = 0; i < themes.Length; i++)
                {
                    var entry = published.GetArrayElementAtIndex(i);
                    Assert.That(entry.FindPropertyRelative("Theme").stringValue, Is.EqualTo(catalogues[i].Theme));
                    Assert.That(entry.FindPropertyRelative("Templates").arraySize, Is.EqualTo(catalogues[i].Templates.Length).And.GreaterThan(0));
                    Assert.That(entry.FindPropertyRelative("Kit").arraySize, Is.EqualTo(catalogues[i].Kit.Length).And.GreaterThan(0));
                }
            }
            finally { Object.DestroyImmediate(asset); }
        }
    }
}
