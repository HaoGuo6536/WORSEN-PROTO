// ============================================================================
// ExpeditionProfileRosterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies authored roster repair, real factory duplicates and first-contact admission.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Expedition.
// KEY RESPONSIBILITIES:
//   - Select only the ten expansion entries while preserving matching authored profiles.
//   - Carry Session requests through the Core overload into distinct Hunter entities.
//   - Reuse Procedural first-contact validation rather than inventing placement policy.
// DEPENDENCIES:
//   - Core, Expedition, Hunter/Player/Level/Procedural, setup, UnityEditor and NUnit.
// USAGE NOTES:
//   Coordinator-only Edit Mode tests. Asset reads only; instances and profile copies
//   are temporary. No scene save, project asset creation or navigation bake occurs.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Domain.Procedural;
using Worsen.Editor.Scenes;
using Worsen.Orchestrator;
using Worsen.Session.Expedition;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Expedition
{
    public sealed class ExpeditionProfileRosterTests
    {
        [Test] public void RosterRepairPreservesAssignedProfilesAndFactorySpawnsSelectedDuplicates()
        {
            var rootObject = new GameObject("roster repair fixture"); rootObject.SetActive(false);
            var prefab = new GameObject("roster actor fixture"); prefab.SetActive(false); prefab.AddComponent<HunterManager>();
            var owned = new List<Object>(); var actors = new List<HunterManager>();
            try
            {
                var root = rootObject.AddComponent<HorrorRunSceneRoot>();
                var custom = ScriptableObject.CreateInstance<HunterProfile>(); owned.Add(custom);
                var customData = new SerializedObject(custom); customData.FindProperty("_archetypeKey").stringValue = "echo"; customData.ApplyModifiedPropertiesWithoutUndo();
                var data = new SerializedObject(root); var entries = data.FindProperty("_hunterRoster"); entries.arraySize = 1;
                entries.GetArrayElementAtIndex(0).objectReferenceValue = custom; data.ApplyModifiedPropertiesWithoutUndo();
                HorrorRunSceneSetup.RestoreHunterRoster(root); HorrorRunSceneSetup.RestoreHunterRoster(root); data.Update();
                Assert.That(entries.arraySize, Is.EqualTo(10)); Assert.That(entries.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(custom));
                Assert.That(Enumerable.Range(0, entries.arraySize).Select(i => ((HunterProfile)entries.GetArrayElementAtIndex(i).objectReferenceValue).ArchetypeKey),
                    Is.EquivalentTo(new[] { "echo", "weaver", "ticking", "ram", "skip", "mimic", "blinder", "herald", "mannequin", "stare" }));
                var profiles = new List<HunterProfile>();
                foreach (string name in new[] { "Echo", "Weaver", "Ticking" })
                {
                    var authored = AssetDatabase.LoadAssetAtPath<HunterProfile>("Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/" + name + "/" + name + "Profile.asset");
                    Assert.That(authored, Is.Not.Null); var profile = Object.Instantiate(authored); owned.Add(profile);
                    var so = new SerializedObject(profile); so.FindProperty("_prefab").objectReferenceValue = prefab; so.ApplyModifiedPropertiesWithoutUndo(); profiles.Add(profile);
                }
                var factory = rootObject.AddComponent<HunterFactory>();
                factory.Configure(profiles, new System.Random(7), new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8 }, new World());
                var controller = new ExpeditionSessionController(new ExpeditionSessionBehaviorState()); controller.Bind(SceneKey.HorrorRun);
                var keys = new[] { "echo", "weaver", "ticking", "echo", "weaver", "ticking" };
                controller.Queue(new ProgressionGenerationRequest(1, 7, 1, false, new ProgressionEffects(1, 1, 1, 1, 100, 100, keys.Length, activeThreatIds: keys))); controller.Begin(1);
                var ids = new HashSet<EntityId>();
                foreach (var request in controller.HunterSpawns("fallback", Enumerable.Range(0, keys.Length).Select(i => Vector3.right * i).ToArray()))
                {
                    var id = factory.Spawn(request); Assert.That(ids.Add(id), Is.True); Assert.That(HunterRegistry.TryGet(id, out var h), Is.True); actors.Add(h);
                    Assert.That(h.ArchetypeKey, Is.EqualTo(request.ArchetypeKey)); Assert.That(h.DuplicateIndex, Is.EqualTo(request.DuplicateIndex));
                }
                Assert.That(actors.Select(h => h.DuplicateIndex), Is.EqualTo(new[] { 0, 0, 0, 1, 1, 1 }));
            }
            finally
            {
                foreach (var actor in actors) Object.DestroyImmediate(actor.gameObject);
                Object.DestroyImmediate(rootObject); Object.DestroyImmediate(prefab); foreach (var value in owned) Object.DestroyImmediate(value);
            }
        }
        [Test] public void FirstContactRejectsPlayerRoomWhileAdmittingCoveredConnectedRoom()
        {
            var layout = new ProceduralLayout();
            var graph = new LevelGraph(new[] { new LevelRoom(1, Vector3.zero, Vector3.one * 12), new LevelRoom(2, Vector3.right * 16, Vector3.one * 12) },
                new[] { new LevelEdge(1, 1, 2, true) }, Array.Empty<LevelAnchor>(), 1, Vector3.zero);
            void Set(string name, object value) => typeof(ProceduralLayout).GetProperty(name).SetValue(layout, value);
            Set("Graph", graph); Set("PlayerSpawnPosition", Vector3.zero); Set("Doors", Array.Empty<ProceduralDoorPlan>());
            Assert.That(ProceduralSpawnUtility.Validate(layout, Vector3.zero, 2f, 1, out _), Is.False);
            Assert.That(ProceduralSpawnUtility.Validate(layout, Vector3.right * 16, 2f, 1, out _), Is.True);
        }
        private sealed class World : IReadOnlyLevelState { public bool IsReady => false; public LevelGraph Graph => null; }
    }
}
