// ============================================================================
// HorrorHunterContentIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the saved five-hunter roster produced by HorrorHunterSetup rather
//   than constructing substitute profiles or clips. Factory-spawned creatures
//   evaluate their real imported animation graphs and deform their actual skins.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter integration.
// KEY RESPONSIBILITIES:
//   - Check distinct imported meshes, Generic Avatars, six clip roles and hidden capsules.
//   - Verify saved per-hunter curse identities and runtime attack configuration.
//   - Exercise actual factory initialization, clip evaluation and graph teardown.
// DEPENDENCIES:
//   - Core traits; Domain Hunter/Player/Level; Session Progression saved config.
//   - UnityEditor asset/scene APIs, Unity animation engine, NUnit and reflection.
// USAGE NOTES:
//   Coordinator holds the exclusive Unity lease. No assets are generated or saved.
//   Engine fixtures live in a preview scene which is closed in finally;
//   the original active scene, roots, dirty flags and registrations are retained. This proves
//   saved content and imported animation integration, not full live combat balance.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.SceneManagement;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Session.Progression;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Hunter
{
    public sealed class HorrorHunterContentIntegrationTests
    {
        private const string Profiles = "Assets/Resources/ScriptableObjects/Domain/Hunter/Expansion/";
        private const string Prefabs = "Assets/Prefabs/Horror/Hunters/";
        private const string Models = "Assets/External/NHance/Creatures/StylizedCreaturesBundle/Meshes/";
        private const string ProgressionPath = "Assets/Resources/ScriptableObjects/Session/Progression/ProgressionConfig.asset";
        private static readonly string[] Keys = { "rusher", "lurker", "watcher", "hexer", "thorncaller" };
        private static readonly string[] ModelPaths = {
            Models + "Werewolf/Werewolf.fbx", Models + "Goblin/GoblinMale.fbx", Models + "Satyr/Satyr_Full.fbx",
            Models + "Fairy/Fairy.fbx", Models + "Plant/Plant.fbx"
        };
        private static readonly HunterAttackStyle[] Styles = {
            HunterAttackStyle.Lunge, HunterAttackStyle.Lunge, HunterAttackStyle.Lunge, HunterAttackStyle.Projectile, HunterAttackStyle.GroundSpikes
        };
        private static readonly HunterLightResponse[] Responses = {
            HunterLightResponse.Investigate, HunterLightResponse.Avoid, HunterLightResponse.Flank, HunterLightResponse.Investigate, HunterLightResponse.Investigate
        };
        private static readonly string[][] CurseIds = {
            new[] { "rusher-long-stride", "rusher-second-wind", "rusher-blood-scent" },
            new[] { "lurker-dark-adaptation", "lurker-crooked-step", "lurker-stolen-silence" },
            new[] { "watcher-long-memory", "watcher-cutting-corners", "watcher-unquiet-gaze" },
            new[] { "hexer-split-bolt", "hexer-hasty-script", "hexer-lingering-hex" },
            new[] { "thorncaller-thorn-ring", "thorncaller-quick-roots", "thorncaller-reaching-roots" }
        };
        private static readonly ProgressionTraits[][] CurseTraits = {
            new[] { ProgressionTraits.RusherLongStride, ProgressionTraits.RusherSecondWind, ProgressionTraits.RusherBloodScent },
            new[] { ProgressionTraits.LurkerDarkAdaptation, ProgressionTraits.LurkerCrookedStep, ProgressionTraits.LurkerStolenSilence },
            new[] { ProgressionTraits.WatcherLongMemory, ProgressionTraits.WatcherCuttingCorners, ProgressionTraits.WatcherUnquietGaze },
            new[] { ProgressionTraits.HexerSplitBolt, ProgressionTraits.HexerHastyScript, ProgressionTraits.HexerLingeringHex },
            new[] { ProgressionTraits.ThorncallerThornRing, ProgressionTraits.ThorncallerQuickRoots, ProgressionTraits.ThorncallerReachingRoots }
        };
        private sealed class LevelFixture : IReadOnlyLevelState
        { public bool IsReady => false; public LevelGraph Graph => null; }

        [Test]
        public void SavedFiveProfilesHaveDistinctImportedMeshesAvatarsAndSixRealClipRoles()
        {
            var meshes = new HashSet<Mesh>();
            var avatars = new HashSet<Avatar>();
            HunterProfile[] profiles = LoadProfiles();
            for (int i = 0; i < profiles.Length; i++)
            {
                string key = Keys[i]; HunterProfile profile = profiles[i]; GameObject prefab = profile.Prefab;
                Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo(Prefabs + key + ".prefab"), key);
                Assert.That(profile.AttackStyle, Is.EqualTo(Styles[i]), key);
                Assert.That(profile.LightResponse, Is.EqualTo(Responses[i]), key);
                Assert.That(profile.LungeDamage, Is.GreaterThan(0), key);
                if (i >= 3)
                {
                    Assert.That(profile.LungeWindupSeconds, Is.GreaterThanOrEqualTo(0.8f), key + " needs a readable ranged warning.");
                    Assert.That(profile.RangedAttackDistance, Is.GreaterThan(profile.LungeDistance), key);
                }
                if (key == "hexer")
                { Assert.That(profile.ProjectileSpeed, Is.GreaterThan(0f)); Assert.That(profile.ProjectileRadius, Is.GreaterThan(0f)); }
                if (key == "thorncaller") Assert.That(profile.SpikeRadius, Is.GreaterThanOrEqualTo(0.5f));
                CapsuleCollider capsule = prefab.GetComponent<CapsuleCollider>();
                Assert.That(capsule != null && capsule.enabled && !capsule.isTrigger, Is.True, key + " retains its motor collider.");
                Assert.That(prefab.GetComponents<Renderer>().All(renderer => !renderer.enabled), Is.True, key + " root capsule must be invisible.");
                Transform oldBody = prefab.transform.Find("Hunter Body");
                if (oldBody != null) Assert.That(oldBody.GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.enabled), Is.True, key);
                Transform creature = prefab.transform.Find("Imported Creature");
                Assert.That(creature, Is.Not.Null, key);
                Assert.That(creature.GetComponentsInChildren<Collider>(true), Is.Empty, key + " art must not add colliders.");
                Assert.That(creature.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.enabled), Is.True, key);
                Animator animator = creature.GetComponentInChildren<Animator>(true);
                Assert.That(animator != null && animator.enabled && animator.avatar != null && animator.avatar.isValid, Is.True, key);
                Assert.That(animator.avatar.isHuman, Is.False, key + " must retain its imported Generic rig.");
                Assert.That(animator.applyRootMotion, Is.False, key);
                Assert.That(animator.runtimeAnimatorController, Is.Null, key + " direct clips must own animation.");
                avatars.Add(animator.avatar);
                SkinnedMeshRenderer skin = LargestSkin(creature.gameObject);
                Assert.That(AssetDatabase.GetAssetPath(skin.sharedMesh), Is.EqualTo(ModelPaths[i]), key);
                meshes.Add(skin.sharedMesh);
                HunterAnimationDriver animation = creature.GetComponent<HunterAnimationDriver>();
                Assert.That(animation, Is.Not.Null, key);
                var animationData = new SerializedObject(animation);
                HunterAnimationDriverConfig animationConfig = (HunterAnimationDriverConfig)animationData.FindProperty("_config").objectReferenceValue;
                Assert.That(animationData.FindProperty("_animator").objectReferenceValue, Is.EqualTo(animator), key);
                Assert.That(animationConfig, Is.EqualTo(LoadAsset<HunterAnimationDriverConfig>(Profiles + key + "_Animation.asset")), key);
                foreach (AnimationClip clip in Clips(animationConfig))
                {
                    Assert.That(clip, Is.Not.Null, key + " must provide all six roles without fallback.");
                    Assert.That(clip.length, Is.GreaterThan(0f), key + "/" + clip.name);
                    Assert.That(clip.legacy, Is.False, key + "/" + clip.name);
                    Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(ModelPaths[i]), key + "/" + clip.name);
                }
                HunterDriver motor = prefab.GetComponent<HunterDriver>();
                Assert.That(motor, Is.Not.Null, key);
                var motorData = new SerializedObject(motor);
                Assert.That(motorData.FindProperty("_animation").objectReferenceValue, Is.EqualTo(animation), key);
                Assert.That(motorData.FindProperty("_config").objectReferenceValue, Is.EqualTo(LoadAsset<HunterMotorDriverConfig>(Profiles + key + "_Motor.asset")), key);
                HunterAttackDriver attacks = prefab.GetComponent<HunterAttackDriver>();
                Assert.That(attacks, Is.Not.Null, key);
                Assert.That(motorData.FindProperty("_attacks").objectReferenceValue, Is.EqualTo(attacks), key);
                var attackData = new SerializedObject(attacks);
                Assert.That(attackData.FindProperty("_config").objectReferenceValue, Is.EqualTo(LoadAsset<HunterAttackDriverConfig>(Profiles + key + "_Attack.asset")), key);
            }
            Assert.That(meshes.Count, Is.EqualTo(5), "Five labels must not reuse one capsule or creature mesh.");
            Assert.That(avatars.Count, Is.EqualTo(5), "Each species needs its own compatible imported rig.");
        }

        [Test]
        public void SavedProgressionCatalogAssociatesExactlyThreeUniqueRuntimeTraitsWithEachActualHunter()
        {
            HunterProfile[] profiles = LoadProfiles(); ProgressionConfig progression = LoadAsset<ProgressionConfig>(ProgressionPath);
            CollectionAssert.AreEquivalent(profiles.Select(profile => profile.ArchetypeKey), progression.Threats.Select(threat => threat.Id));
            var seenIds = new HashSet<string>(); var seenTraits = new HashSet<ProgressionTraits>();
            for (int i = 0; i < Keys.Length; i++)
            {
                ProgressionEntryConfig[] associated = progression.Curses.Where(curse => curse.RequiredThreatId == Keys[i]).ToArray();
                Assert.That(associated.Length, Is.EqualTo(3), Keys[i]);
                CollectionAssert.AreEquivalent(CurseIds[i], associated.Select(curse => curse.Id), Keys[i]);
                for (int j = 0; j < 3; j++)
                {
                    ProgressionEntryConfig curse = associated.Single(entry => entry.Id == CurseIds[i][j]);
                    Assert.That(curse.Traits, Is.EqualTo(CurseTraits[i][j]), curse.Id);
                    Assert.That(curse.Repeatable, Is.False, curse.Id);
                    Assert.That(seenIds.Add(curse.Id) && seenTraits.Add(curse.Traits), Is.True, curse.Id + " must remain unique.");
                }
            }
            Assert.That(seenIds.Count, Is.EqualTo(15));
        }

        [Test]
        public void FactorySpawnsAllFiveSavedRigsAndRealClipGraphsDeformSkinsWithoutMovingCollisionRoots()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False, "Run this saved-content fixture in Edit Mode.");
            HunterProfile[] profiles = LoadProfiles(); Scene original = SceneManager.GetActiveScene();
            HunterManager[] originalRegistrations = HunterRegistry.Items.Where(item => item != null).ToArray();
            Scene[] existing = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            bool[] dirty = existing.Select(item => item.isDirty).ToArray();
            GameObject[][] roots = existing.Select(item => item.GetRootGameObjects()).ToArray();
            Scene scene = EditorSceneManager.NewPreviewScene();
            var meshes = new List<Mesh>();
            var ownedHunters = new List<GameObject>();
            try
            {
                Assert.That(scene.IsValid() && EditorSceneManager.IsPreviewScene(scene), Is.True);
                var factoryRoot = EditorUtility.CreateGameObjectWithHideFlags("Saved hunter content factory", HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(factoryRoot, scene);
                var factory = factoryRoot.AddComponent<HunterFactory>();
                var player = new PlayerBehaviorState { Id = new EntityId(9127), Position = new Vector3(0, 0, 1000), Health = 100, SprintSpeed = 8f };
                factory.Configure(profiles, new System.Random(113), player, new LevelFixture());
                var spawnedIds = new HashSet<EntityId>();
                for (int i = 0; i < profiles.Length; i++)
                {
                    string key = Keys[i]; EntityId id = factory.Spawn(new SpawnRequest(key, new Vector3(i * 30f, 0f, 0f), Quaternion.identity));
                    Assert.That(spawnedIds.Add(id), Is.True, key);
                    HunterManager hunter = HunterRegistry.Items.Single(item => item != null && item.Id == id && !originalRegistrations.Contains(item));
                    ownedHunters.Add(hunter.gameObject);
                    hunter.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    SceneManager.MoveGameObjectToScene(hunter.gameObject, scene);
                    Assert.That(hunter.gameObject.scene, Is.EqualTo(scene), key);
                    Assert.That(hunter.ReadOnlyState.IsActive, Is.True, key);
                    Assert.That(hunter.ReadOnlyState.TargetId, Is.EqualTo(player.Id), key);
                    // Factory initialization creates the actual graph. Evaluate animation directly;
                    // motor physics would query the active scene rather than this isolated preview.
                    HunterAnimationDriver animation = hunter.GetComponentInChildren<HunterAnimationDriver>(true);
                    Assert.That(animation != null && animation.IsReady, Is.True, key + " factory must initialize a real graph.");
                    var config = LoadAsset<HunterAnimationDriverConfig>(Profiles + key + "_Animation.asset");
                    FieldInfo stateField = typeof(HunterAnimationDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.That(stateField, Is.Not.Null);
                    var graphState = (HunterAnimationDriverState)stateField.GetValue(animation);
                    Assert.That(graphState.Clips.Length, Is.EqualTo(6), key);
                    AnimationClip[] expected = Clips(config);
                    for (int slot = 0; slot < 6; slot++) Assert.That(graphState.Clips[slot].GetAnimationClip(), Is.EqualTo(expected[slot]), key + " graph slot " + slot);
                    CapsuleCollider capsule = hunter.GetComponent<CapsuleCollider>();
                    Vector3 position = hunter.transform.position, colliderCenter = capsule.center;
                    Quaternion rotation = hunter.transform.rotation; float height = capsule.height, radius = capsule.radius;
                    animation.Apply(0.2f, 0f, 0, 0f);
                    animation.Apply(0.2f, 2f, 0, 0f);
                    animation.Apply(0.2f, 8f, 0, 0f);
                    animation.Apply(0.2f, 0f, 1, 0.5f);
                    SkinnedMeshRenderer skin = LargestSkin(hunter.gameObject);
                    var before = new Mesh { name = key + " attack pose A" }; var after = new Mesh { name = key + " attack pose B" };
                    meshes.Add(before); meshes.Add(after);
                    animation.Apply(0.2f, 0f, 2, 0.2f); skin.BakeMesh(before);
                    animation.Apply(0.2f, 0f, 2, 0.8f); skin.BakeMesh(after);
                    Assert.That(before.vertexCount, Is.GreaterThan(0), key);
                    Assert.That(after.vertexCount, Is.EqualTo(before.vertexCount), key);
                    Vector3[] a = before.vertices, b = after.vertices; float greatestMovement = 0f;
                    for (int vertex = 0; vertex < a.Length; vertex++) greatestMovement = Mathf.Max(greatestMovement, (a[vertex] - b[vertex]).sqrMagnitude);
                    Assert.That(greatestMovement, Is.GreaterThan(0.0000001f), key + " actual imported attack must deform the skin, not merely create an empty graph.");
                    AssertFiniteBounds(before.bounds, key); AssertFiniteBounds(after.bounds, key);
                    animation.Apply(0.2f, 0f, 3, 0.5f);
                    Assert.That(Vector3.Distance(hunter.transform.position, position), Is.LessThan(0.00001f), key + " animation must not move the capsule.");
                    Assert.That(Quaternion.Angle(hunter.transform.rotation, rotation), Is.LessThan(0.00001f), key);
                    Assert.That(capsule.center, Is.EqualTo(colliderCenter), key); Assert.That(capsule.height, Is.EqualTo(height), key); Assert.That(capsule.radius, Is.EqualTo(radius), key);
                    TestContext.WriteLine(key + ": graph ready, six real clip roles, " + after.vertexCount + " skinned vertices, max attack deformation squared=" + greatestMovement);
                    hunter.Teardown(); Assert.That(animation.IsReady, Is.False, key + " teardown must release its playable graph.");
                }
            }
            finally
            {
                // Destroy the owned instances before the factory so its remaining fake-null entries cannot call deferred Destroy in Edit Mode.
                foreach (GameObject root in ownedHunters) if (root != null) Object.DestroyImmediate(root);
                foreach (Mesh mesh in meshes) if (mesh != null) Object.DestroyImmediate(mesh);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(original), "Preview tests must never switch the active scene.");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(existing.Length));
            for (int i = 0; i < existing.Length; i++)
            {
                Assert.That(SceneManager.GetSceneAt(i), Is.EqualTo(existing[i]));
                Assert.That(existing[i].isDirty, Is.EqualTo(dirty[i]), "Foreign dirty flags must remain unchanged.");
                CollectionAssert.AreEquivalent(roots[i], existing[i].GetRootGameObjects(), "Foreign scene roots must remain unchanged.");
            }
            CollectionAssert.AreEquivalent(originalRegistrations, HunterRegistry.Items.Where(item => item != null), "Fixture must retain foreign registrations and remove every spawned hunter.");
        }

        private static HunterProfile[] LoadProfiles()
        {
            var profiles = new HunterProfile[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
            { profiles[i] = LoadAsset<HunterProfile>(Profiles + Keys[i] + ".asset"); Assert.That(profiles[i].ArchetypeKey, Is.EqualTo(Keys[i])); Assert.That(profiles[i].Prefab, Is.Not.Null, Keys[i]); }
            return profiles;
        }
        private static T LoadAsset<T>(string path) where T : Object
        { T value = AssetDatabase.LoadAssetAtPath<T>(path); Assert.That(value, Is.Not.Null, "Required saved content missing: " + path); return value; }
        private static AnimationClip[] Clips(HunterAnimationDriverConfig config)
            => new[] { config.Idle, config.Walk, config.Run, config.Windup, config.Attack, config.Recovery };
        private static SkinnedMeshRenderer LargestSkin(GameObject root)
        {
            SkinnedMeshRenderer skin = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(item => item.sharedMesh != null).OrderByDescending(item => item.sharedMesh.vertexCount).FirstOrDefault();
            Assert.That(skin, Is.Not.Null, root.name + " needs an actual imported skinned mesh."); return skin;
        }
        private static void AssertFiniteBounds(Bounds bounds, string key)
        {
            Assert.That(float.IsNaN(bounds.center.sqrMagnitude) || float.IsInfinity(bounds.center.sqrMagnitude), Is.False, key);
            Assert.That(float.IsNaN(bounds.size.sqrMagnitude) || float.IsInfinity(bounds.size.sqrMagnitude), Is.False, key);
            Assert.That(bounds.size.sqrMagnitude, Is.GreaterThan(0.0001f), key);
        }
    }
}
