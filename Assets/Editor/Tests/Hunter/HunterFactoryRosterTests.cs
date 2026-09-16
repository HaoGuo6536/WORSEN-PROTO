// ============================================================================
// HunterFactoryRosterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Hunter behavior with explicit reproducible fixtures.
//   Tests exercise observable light, physical attacks, route admission or creature
//   animation contracts without changing authored gameplay assets.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Coordinator runs Unity tests with the exclusive lease. Fixtures clean up their own objects.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterFactoryRosterTests
    {
        private sealed class LevelFixture : IReadOnlyLevelState { public bool IsReady => false; public LevelGraph Graph => null; }
        [Test] public void UnknownKeyNeverFallsBackAndInvalidCatalogsAreRejectedBeforeInstantiation()
        {
            var root = new GameObject("Factory fixture"); var prefab = new GameObject("Inactive fixture prefab"); prefab.SetActive(false);
            HunterProfile profile = ScriptableObject.CreateInstance<HunterProfile>();
            HunterProfile missing = ScriptableObject.CreateInstance<HunterProfile>();
            try
            {
                var so = new SerializedObject(profile); so.FindProperty("_prefab").objectReferenceValue = prefab;
                so.FindProperty("_archetypeKey").stringValue = "rusher"; so.ApplyModifiedPropertiesWithoutUndo();
                var factory = root.AddComponent<HunterFactory>(); var player = new PlayerBehaviorState(); var level = new LevelFixture(); var random = new System.Random(1);
                factory.Configure(new[] { profile }, random, player, level);
                Assert.Throws<ArgumentException>(() => factory.Spawn(new SpawnRequest("missing", Vector3.zero, Quaternion.identity)));
                Assert.Throws<ArgumentException>(() => factory.Configure(new[] { profile, profile }, random, player, level));
                Assert.Throws<ArgumentException>(() => factory.Configure(new[] { missing }, random, player, level));
                Assert.Throws<ArgumentException>(() => factory.Configure(new HunterProfile[] { null }, random, player, level));
                Assert.That(Array.Exists(Resources.FindObjectsOfTypeAll<GameObject>(), item => item.name == "Inactive fixture prefab(Clone)"), Is.False);
            }
            finally
            { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(prefab); UnityEngine.Object.DestroyImmediate(profile); UnityEngine.Object.DestroyImmediate(missing); }
        }
    }
}
