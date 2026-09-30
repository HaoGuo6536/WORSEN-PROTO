// ============================================================================
// ProceduralThemeUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Verifies four-theme selection and separation from topology randomness.
//   Wall height is theme data, so physical manifests can differ while seeded
//   edges, footprint reservations and objective identities remain stable.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Compare theme topology and seeded first-floor/per-floor selection policies.
// DEPENDENCIES:
//   - NUnit, UnityEditor serialization and Domain.Procedural.
// USAGE NOTES:
//   Temporary configs only; existing regression fixtures remain unchanged.
// ============================================================================
using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralThemeUtilityTests
    {
        [Test]
        public void ThemeSwapKeepsTopologyWhileApplyingItsHeightAcrossSeedSample()
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
                    Assert.That(themed.Graph.Edges, Is.EqualTo(legacy.Graph.Edges));
                    Assert.That(themed.Cells, Is.EqualTo(legacy.Cells));
                    Assert.That(themed.Graph.Rooms[0].Size.y, Is.EqualTo(themed.Theme.WallHeight));
                    Assert.That(themed.Manifest, Does.Contain("|Theme:" + themed.ThemeId));
                    Assert.That(themed.Graph.Anchors, Is.EqualTo(legacy.Graph.Anchors));
                    Assert.That(themed.HunterSpawnPositions, Is.EqualTo(legacy.HunterSpawnPositions));
                    Assert.That(themed.Manifest, Is.EqualTo(Generate(c, seed, 4).Manifest));

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
                var firsts = new HashSet<string>();
                for (int seed = 0; seed < 256; seed++)
                {
                    string previous = null;
                    for (int round = 1; round <= 16; round++)
                    {
                        string selected = ProceduralThemeUtility.Select(t, round, new System.Random(seed)).Id;
                        Assert.That(selected, Is.EqualTo(ProceduralThemeUtility.Select(t, round, new System.Random(seed)).Id));
                        Assert.That(selected, Is.Not.EqualTo(previous)); previous = selected;
                        if (round == 1) firsts.Add(selected);
                    }
                }
                Assert.That(firsts, Is.EquivalentTo(new[] { "castle", "hospital", "school", "basement" }));
                Assert.That(ProceduralThemeUtility.Select(t, int.MaxValue, new System.Random(17)).Id,
                    Is.Not.EqualTo(ProceduralThemeUtility.Select(t, int.MaxValue - 1, new System.Random(17)).Id));
                var s = new SerializedObject(t); s.FindProperty("_perRun").boolValue = true; s.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(ProceduralThemeUtility.Select(t, 1, new System.Random(17)).Id,
                    Is.EqualTo(ProceduralThemeUtility.Select(t, 9, new System.Random(17)).Id));
                s.FindProperty("_hospitalEnabled").boolValue = false;
                s.FindProperty("_schoolEnabled").boolValue = false;
                s.FindProperty("_basementEnabled").boolValue = false; s.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(ProceduralThemeUtility.Select(t, 9, new System.Random(17)).Id, Is.EqualTo("castle"));
            }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }
        private static ProceduralLayout Generate(ProceduralConfig c, int seed, int round) =>
            new ProceduralController(new ProceduralBehaviorState(), c, new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);
    }
}
