// ============================================================================
// HorrorHunterSetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds the five horror hunter archetypes from imported animated creature rigs.
//   Visible models remain children of the proven capsule motor, and repeat runs
//   repair wiring while retaining prefab/profile identities and designer tuning.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Validate all rig and clip inputs before creating the roster's owned assets.
//   - Wire clip Playables, physical attack drivers and clear ranged telegraph materials.
//   - Preserve imported assets, root collision and stable generated asset GUIDs.
//   - Wire occasional attack vocals with a restrained Goblin probability.
// DEPENDENCIES:
//   - Hunter runtime types and existing HunterPrefabGenerator; UnityEditor/UnityEngine.
// USAGE NOTES:
//   Editor-only. Coordinator calls BuildProfiles during its exclusive Unity lease.
//   No scene setup, navigation baking, global saves, package edits or user-source edits.
//   Generated Imported Creature children are owned; imported source prefabs are untouched.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Domain.Hunter;

namespace Worsen.Editor.Hunter
{
    public static class HorrorHunterSetup
    {
        public const string ProfileDirectory = "Assets/Resources/ScriptableObjects/Domain/Hunter/Expansion";
        public const string PrefabDirectory = "Assets/Prefabs/Horror/Hunters";
        public const string ArtDirectory = "Assets/Art/Horror/Hunters";
        private const string Imported = "Assets/External/NHance/Creatures/StylizedCreaturesBundle/";

        private sealed class RosterEntry
        {
            public string Key, Prefab, Model;
            public string[] Clips;
            public float Height, Windup, Active, Recovery, Speed;
            public int LightResponse, AttackStyle, Damage;
            public bool Scream;
            public GameObject Source;
            public Avatar Avatar;
            public AnimationClip[] ResolvedClips;
        }

        [MenuItem("Worsen/Hunter/Build Horror Roster")]
        public static void BuildRosterMenu() { BuildProfiles(); }

        public static HunterProfile[] BuildProfiles()
        {
            RequireIdle();
            RosterEntry[] roster = Roster();
            foreach (RosterEntry entry in roster) ValidateSource(entry);
            EnsureFolder(ArtDirectory); EnsureFolder(PrefabDirectory); EnsureFolder(ProfileDirectory);
            Material warning = EnsureMaterial("AttackWarning.mat", Color.white, true);
            Material spell = EnsureMaterial("HexBolt.mat", new Color(0.55f, 0.13f, 1.5f, 1f), false);
            Material thorn = EnsureMaterial("RootSpike.mat", new Color(0.20f, 0.40f, 0.13f, 1f), false);
            GameObject bolt = EnsureAttackVisual("HexBolt", spell, false);
            GameObject spike = EnsureAttackVisual("RootSpike", thorn, true);
            var profiles = new HunterProfile[roster.Length];
            for (int i = 0; i < roster.Length; i++) profiles[i] = BuildEntry(roster[i], warning, spell, thorn, bolt, spike);
            return profiles;
        }

        private static RosterEntry[] Roster()
        {
            return new[]
            {
                new RosterEntry { Key="rusher", Prefab="Prefabs/Werewolf/Werewolf_Bk.prefab", Model="Meshes/Werewolf/Werewolf.fbx",
                    Clips=new[]{"idle","walk","run","Ready","attack","hit"}, Height=2.12f, Windup=.3f, Active=.3f, Recovery=.9f, Speed=1.12f, Damage=35, LightResponse=0, AttackStyle=0, Scream=true },
                new RosterEntry { Key="lurker", Prefab="Prefabs/GoblinMale/GoblinMale_Gn.prefab", Model="Meshes/Goblin/GoblinMale.fbx",
                    Clips=new[]{"idle","walk","run","Ready","attack","hit"}, Height=1.65f, Windup=.45f, Active=.3f, Recovery=1.1f, Speed=1.05f, Damage=30, LightResponse=1, AttackStyle=0 },
                new RosterEntry { Key="watcher", Prefab="Prefabs/Satyr/Satyr_Unarmed.prefab", Model="Meshes/Satyr/Satyr_Full.fbx",
                    Clips=new[]{"Idle_Unarmed","Walk_Unarmed","run","Ready_Unarmed","Attack_Unarmed","Hit_Unarmed"}, Height=2.05f, Windup=.55f, Active=.3f, Recovery=1.3f, Speed=1.02f, Damage=40, LightResponse=2, AttackStyle=0, Scream=true },
                new RosterEntry { Key="hexer", Prefab="Prefabs/Fairy/Fairy_Pe.prefab", Model="Meshes/Fairy/Fairy.fbx",
                    Clips=new[]{"Idle","Fly","Fly","Idle01","Attack","Hit"}, Height=1.6f, Windup=.95f, Active=.15f, Recovery=1.5f, Speed=.83f, Damage=25, LightResponse=0, AttackStyle=1 },
                new RosterEntry { Key="thorncaller", Prefab="Prefabs/Plant/Plant_Bl.prefab", Model="Meshes/Plant/Plant.fbx",
                    Clips=new[]{"idle","walk","run","idle","Attack01","hit"}, Height=1.85f, Windup=1.15f, Active=.15f, Recovery=1.7f, Speed=.72f, Damage=30, LightResponse=0, AttackStyle=2 }
            };
        }

        private static void ValidateSource(RosterEntry entry)
        {
            entry.Source = AssetDatabase.LoadAssetAtPath<GameObject>(Imported + entry.Prefab);
            if (entry.Source == null) throw new InvalidOperationException("Missing imported creature: " + Imported + entry.Prefab);
            Animator animator = entry.Source.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isValid)
                throw new InvalidOperationException(entry.Key + " requires the imported creature's valid Generic Avatar.");
            entry.Avatar = animator.avatar;
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(Imported + entry.Model).OfType<AnimationClip>().ToArray();
            entry.ResolvedClips = new AnimationClip[entry.Clips.Length];
            for (int i = 0; i < entry.Clips.Length; i++)
            {
                AnimationClip clip = clips.FirstOrDefault(candidate => candidate.name == entry.Clips[i]);
                if (clip == null || clip.length <= 0f)
                    throw new InvalidOperationException(entry.Key + " is missing nonempty clip " + entry.Clips[i] + " in " + Imported + entry.Model);
                entry.ResolvedClips[i] = clip;
            }
        }

        private static HunterProfile BuildEntry(RosterEntry entry, Material warning, Material spell, Material thorn, GameObject bolt, GameObject spike)
        {
            string profilePath = ProfileDirectory + "/" + entry.Key + ".asset";
            string prefabPath = PrefabDirectory + "/" + entry.Key + ".prefab";
            string motorPath = ProfileDirectory + "/" + entry.Key + "_Motor.asset";
            bool newProfile = AssetDatabase.LoadAssetAtPath<HunterProfile>(profilePath) == null;
            HunterProfile profile = HunterPrefabGenerator.EnsureAssets(prefabPath, profilePath, motorPath);
            HunterAnimationDriverConfig animationConfig = EnsureAsset<HunterAnimationDriverConfig>(ProfileDirectory + "/" + entry.Key + "_Animation.asset");
            HunterAttackDriverConfig attackConfig = EnsureAsset<HunterAttackDriverConfig>(ProfileDirectory + "/" + entry.Key + "_Attack.asset");
            ConfigureAnimation(animationConfig, entry.ResolvedClips);
            ConfigureAttack(attackConfig, entry, warning, spell, thorn, bolt, spike);
            ConfigureProfile(profile, entry, newProfile);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                root.name = entry.Key;
                CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
                Rigidbody body = root.GetComponent<Rigidbody>();
                HunterDriver driver = root.GetComponent<HunterDriver>();
                HunterManager manager = root.GetComponent<HunterManager>();
                if (capsule == null || body == null || driver == null || manager == null)
                    throw new InvalidOperationException(entry.Key + " lost required capsule motor components.");
                body.isKinematic = true; body.useGravity = false; capsule.enabled = true; capsule.isTrigger = false;
                foreach (Renderer renderer in root.GetComponents<Renderer>()) renderer.enabled = false;
                Transform capsuleVisual = root.transform.Find("Hunter Body");
                if (capsuleVisual != null)
                    foreach (Renderer renderer in capsuleVisual.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                GameObject creature = EnsureCreature(root, entry);
                Animator animator = creature.GetComponentInChildren<Animator>(true);
                animator.avatar = entry.Avatar; animator.applyRootMotion = false;
                animator.runtimeAnimatorController = null; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                HunterAnimationDriver animation = GetOrAdd<HunterAnimationDriver>(creature);
                HunterAttackDriver attack = GetOrAdd<HunterAttackDriver>(root);
                Wire(animation, "_animator", animator); Wire(animation, "_config", animationConfig);
                Wire(attack, "_config", attackConfig);
                Wire(driver, "_animation", animation); Wire(driver, "_attacks", attack);
                Wire(driver, "_capsule", capsule); Wire(driver, "_body", body);
                Wire(driver, "_config", AssetDatabase.LoadAssetAtPath<HunterMotorDriverConfig>(motorPath));
                Wire(manager, "_driver", driver);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save " + prefabPath);
                Wire(profile, "_prefab", prefab); AssetDatabase.SaveAssetIfDirty(profile);
                return profile;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject EnsureCreature(GameObject root, RosterEntry entry)
        {
            Transform found = root.transform.Find("Imported Creature");
            GameObject creature;
            if (found != null) creature = found.gameObject;
            else
            {
                creature = (GameObject)PrefabUtility.InstantiatePrefab(entry.Source, root.transform);
                PrefabUtility.UnpackPrefabInstance(creature, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                creature.name = "Imported Creature";
                creature.transform.localPosition = Vector3.zero; creature.transform.localRotation = Quaternion.identity;
                creature.transform.localScale = Vector3.one;
                FitCreature(creature, entry.Height);
            }
            Animator animator = creature.GetComponentInChildren<Animator>(true);
            if (animator == null) throw new InvalidOperationException(entry.Key + " Imported Creature has no Animator.");
            foreach (Collider collider in creature.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (Rigidbody body in creature.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (NavMeshAgent agent in creature.GetComponentsInChildren<NavMeshAgent>(true)) UnityEngine.Object.DestroyImmediate(agent);
            foreach (AudioSource audio in creature.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (Animator childAnimator in creature.GetComponentsInChildren<Animator>(true))
            { childAnimator.applyRootMotion = false; childAnimator.runtimeAnimatorController = null; childAnimator.enabled = childAnimator == animator; }
            return creature;
        }

        private static void FitCreature(GameObject creature, float targetHeight)
        {
            Renderer[] renderers = creature.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Imported Creature has no renderer.");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y <= 0.01f) throw new InvalidOperationException("Imported creature bounds are degenerate.");
            float scale = targetHeight / bounds.size.y;
            Vector3 origin = creature.transform.position;
            Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            creature.transform.localScale *= scale;
            creature.transform.position -= (foot - origin) * scale;
        }

        private static void ConfigureAnimation(HunterAnimationDriverConfig config, AnimationClip[] clips)
        {
            string[] names = { "_idle", "_walk", "_run", "_windup", "_attack", "_recovery" };
            var serialized = new SerializedObject(config);
            for (int i = 0; i < names.Length; i++) Required(serialized, names[i]).objectReferenceValue = clips[i];
            serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(config);
        }

        private static void ConfigureAttack(HunterAttackDriverConfig config, RosterEntry entry, Material warning, Material spell, Material thorn, GameObject bolt, GameObject spike)
        {
            var serialized = new SerializedObject(config);
            Required(serialized, "_warningMaterial").objectReferenceValue = warning;
            Required(serialized, "_projectileMaterial").objectReferenceValue = spell;
            Required(serialized, "_spikeMaterial").objectReferenceValue = thorn;
            Required(serialized, "_projectilePrefab").objectReferenceValue = bolt;
            Required(serialized, "_spikePrefab").objectReferenceValue = spike;
            Required(serialized, "_warningColor").colorValue = entry.AttackStyle == 2 ? new Color(.65f, 1f, .2f, 1f) : new Color(.9f, .3f, 1f, 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(config);
        }

        private static void ConfigureProfile(HunterProfile profile, RosterEntry entry, bool initializeDefaults)
        {
            var serialized = new SerializedObject(profile);
            Required(serialized, "_archetypeKey").stringValue = entry.Key;
            Required(serialized, "_lightResponse").enumValueIndex = entry.LightResponse;
            Required(serialized, "_attackStyle").enumValueIndex = entry.AttackStyle;
            if (initializeDefaults)
            {
                Required(serialized, "_lungeWindupSeconds").floatValue = entry.Windup;
                Required(serialized, "_lungeActiveSeconds").floatValue = entry.Active;
                Required(serialized, "_lungeRecoverySeconds").floatValue = entry.Recovery;
                Required(serialized, "_chaseSpeedMultiplier").floatValue = entry.Speed;
                Required(serialized, "_lungeDamage").intValue = entry.Damage;
                Required(serialized, "_screamOnDetection").boolValue = entry.Scream;
                Required(serialized, "_screamCooldownSeconds").floatValue = 14f;
                Required(serialized, "_attackScreamChance").floatValue = .25f;
                Required(serialized, "_rangedAttackDistance").floatValue = entry.AttackStyle == 2 ? 12f : 15f;
                Required(serialized, "_projectileSpeed").floatValue = 11f;
                Required(serialized, "_projectileRadius").floatValue = .22f;
                Required(serialized, "_spikeRadius").floatValue = 1.5f;
            }
            if (entry.Key == "lurker")
            {
                Required(serialized, "_screamOnDetection").boolValue = true;
                Required(serialized, "_attackScreamChance").floatValue = .15f;
            }
            if (entry.AttackStyle != 0)
                Required(serialized, "_lungeWindupSeconds").floatValue = Mathf.Max(.8f, Required(serialized, "_lungeWindupSeconds").floatValue);
            serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(profile);
        }

        private static GameObject EnsureAttackVisual(string name, Material material, bool spike)
        {
            string path = ArtDirectory + "/" + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            string meshPath = ArtDirectory + "/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null) { mesh = BuildCrystalMesh(name, spike); AssetDatabase.CreateAsset(mesh, meshPath); }
            var root = new GameObject(name);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                root.transform.localScale = spike ? new Vector3(.6f, .65f, .6f) : Vector3.one * .44f;
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Mesh BuildCrystalMesh(string name, bool spike)
        {
            int sides = spike ? 7 : 10;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            vertices.Add(new Vector3(0f, spike ? 1f : .75f, 0f));
            vertices.Add(new Vector3(0f, spike ? -1f : -.75f, 0f));
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(angle) * .5f, spike ? -.7f : 0f, Mathf.Sin(angle) * .5f));
            }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides + 2, current = i + 2;
                triangles.AddRange(new[] { 0, next, current, 1, current, next });
            }
            var mesh = new Mesh { name = name + " Mesh" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Material EnsureMaterial(string filename, Color color, bool vertexColors)
        {
            string path = ArtDirectory + "/" + filename;
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            Shader shader = Shader.Find(vertexColors ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("Required URP attack visual shader is missing.");
            var material = new Material(shader) { name = filename.Replace(".mat", ""), enableInstancing = true };
            material.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(material, path); return material;
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset;
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static T GetOrAdd<T>(GameObject root) where T : Component
        { T found = root.GetComponent<T>(); return found != null ? found : root.AddComponent<T>(); }
        private static void Wire(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var serialized = new SerializedObject(target); Required(serialized, field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        private static SerializedProperty Required(SerializedObject serialized, string field)
        { return serialized.FindProperty(field) ?? throw new InvalidOperationException(serialized.targetObject.name + " lacks " + field); }
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            { string next = parent + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]); parent = next; }
        }
        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || UnityEditor.BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Build the horror roster only in an idle Edit Mode editor under the Unity lease.");
        }
    }
}
