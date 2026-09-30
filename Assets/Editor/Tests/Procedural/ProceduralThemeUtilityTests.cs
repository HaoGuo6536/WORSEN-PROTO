// ============================================================================
// ProceduralThemeUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that swapping a theme leaves the legacy physical manifest intact.
//   Selection is independently seeded so adding hospital content cannot consume
//   the random draws that place doors, objectives or first-contact spawns.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Compare castle and hospital topology and deterministic run/round selection.
// DEPENDENCIES:
//   - NUnit, UnityEditor serialization and Domain.Procedural.
// USAGE NOTES:
//   Temporary configs only; existing regression fixtures remain unchanged.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralThemeUtilityTests
    {
        [Test]
        public void ThemeSwapChangesOnlyItsManifestSuffixAcrossSeedSample()
        {
            var c = ScriptableObject.CreateInstance<ProceduralConfig>();
            var t = ScriptableObject.CreateInstance<ProceduralThemeConfig>();
            try
            {
                for (int seed = 0; seed < 32; seed++)
                {
                    var settings = new SerializedObject(c); settings.FindProperty("_themes").objectReferenceValue = null;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    var legacy = Generate(c, seed, 4);
                    settings.FindProperty("_themes").objectReferenceValue = t; settings.ApplyModifiedPropertiesWithoutUndo();
                    var themed = Generate(c, seed, 4);
                    Assert.That(themed.Manifest.Split(new[] { "|Theme:" }, StringSplitOptions.None)[0],
                        Is.EqualTo(legacy.Manifest.Split(new[] { "|Theme:" }, StringSplitOptions.None)[0]));
                    Assert.That(themed.Manifest, Does.Contain("|Theme:" + themed.ThemeId));
                    Assert.That(themed.Graph.Anchors, Is.EqualTo(legacy.Graph.Anchors));
                    Assert.That(themed.HunterSpawnPositions, Is.EqualTo(legacy.HunterSpawnPositions));
                    Assert.That(themed.Manifest, Is.EqualTo(Generate(c, seed, 4).Manifest));
                    Assert.That(Generate(c, seed, 3).ThemeId, Is.EqualTo("castle"));
                }
                Assert.That(t.Hospital.LightSource, Is.EqualTo("fluorescent"));
                Assert.That(t.Hospital.Families, Is.Not.EqualTo(t.Castle.Families));
                Assert.That(t.Castle.InheritMaterials, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(c); UnityEngine.Object.DestroyImmediate(t); }
        }
        [Test]
        public void AlternationAndPerRunModeAreDeterministic()
        {
            var t = ScriptableObject.CreateInstance<ProceduralThemeConfig>();
            try
            {
                string a = ProceduralThemeUtility.Select(t, 4, new System.Random(17)).Id;
                Assert.That(ProceduralThemeUtility.Select(t, 5, new System.Random(17)).Id, Is.Not.EqualTo(a));
                Assert.That(ProceduralThemeUtility.Select(t, 6, new System.Random(17)).Id, Is.EqualTo(a));
                var s = new SerializedObject(t); s.FindProperty("_perRun").boolValue = true; s.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(ProceduralThemeUtility.Select(t, 1, new System.Random(17)).Id,
                    Is.EqualTo(ProceduralThemeUtility.Select(t, 9, new System.Random(17)).Id));
                s.FindProperty("_hospitalEnabled").boolValue = false; s.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(ProceduralThemeUtility.Select(t, 9, new System.Random(17)).Id, Is.EqualTo("castle"));
            }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }
        private static ProceduralLayout Generate(ProceduralConfig c, int seed, int round) =>
            new ProceduralController(new ProceduralBehaviorState(), c, new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);
    }
}
