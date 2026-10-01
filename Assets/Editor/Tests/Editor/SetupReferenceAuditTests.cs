// ============================================================================
// SetupReferenceAuditTests.cs
// ============================================================================
// PURPOSE:
//   Audits published setup output before dependent fixtures use its references.
//   Broken serialized references and missing scripts are reported with the asset,
//   component and property path instead of surfacing as later runtime nulls.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Editor generated-asset audit.
// KEY RESPONSIBILITIES:
//   - Inspect every serialized object reference in generated config/prefab roots.
//   - Reject dangling references and missing components, including inactive children.
//   - Require authored project references to resolve unless listed as optional.
//   - Keep each optional slot tied to the runtime fallback that handles its null.
//   - Prove the audit detects a cleared binding on an in-memory generated schema.
// DEPENDENCIES:
//   - UnityEditor serialization/AssetDatabase, Player/Floor generator paths and NUnit.
// USAGE NOTES:
//   Run after the coordinator's Rebuild All; read-only, never performs setup itself.
//   A null outside OptionalReferences fails. Add an entry only with the code path that
//   handles the null (coordinator decision 2026-09-30, from an offline scan of the
//   generated assets). Engine bookkeeping nulls (prefab ancestry) are not authored.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Player;
using Worsen.Editor.Player;
using Worsen.Editor.Floor;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Editor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class SetupReferenceAuditTests
    {
        // Type.field (last path segment) -> the runtime fallback that handles a null value.
        private static readonly Dictionary<string, string> OptionalReferences = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "AudioSoundscapeDriverConfig.Clip", "AudioSoundscapeDriver roster lookup falls back to the binding's canonical bank" },
            { "AudioSoundscapeDriverConfig._heartbeatClip", "AudioSoundscapeDriver.HeartbeatClip falls back to the generated heartbeat" },
            { "CameraDriverConfig._handMesh", "CameraHandCatchDriver builds the primitive hand when no mesh is set" },
            { "HorrorDriverConfig._webMaterial", "HorrorWebDriver builds the web material from WebShader (HorrorShaderSetup); it errors only when both are missing" },
            { "EnvironmentDriverConfig._lightTemplate", "no runtime consumer yet; the HorrorEnvironmentLightSetup template is not wired (tracked gap)" },
            { "FloorDriverConfig._cakePrefab", "FloorDriver keeps the primitive cake when no visual prefab is set" },
            { "FloorDriverConfig._crackMaterial", "RoomCollapseVolume uses its dark material" },
            { "FloorDriverConfig._exitDoorMaterial", "FloorDriver builds the default wood material" },
            { "FloorDriverConfig._exitDoorPrefab", "FloorExitDoor keeps the primitive door" },
            { "FloorDriverConfig._handPrefab", "RoomCollapseVolume.BuildHand" },
            { "FloorDriverConfig._mistMaterial", "RoomCollapseVolume.MakeFogMaterial" },
            { "HunterDriver._animation", "optional animation driver on legacy hunter prefabs" },
            { "HunterDriver._attacks", "HunterDriver resolves the sibling HunterAttackDriver or runs without one" },
            { "HunterDriver._config", "HunterDriver.Initialize uses the profile motor override, then the Resources HunterMotorDriverConfig" },
            { "HunterProfile._motorOverride", "optional per-profile motor override; the shared motor config applies" },
            { "HunterProfile._archetypeRules", "null selects the registered Default module (HunterArchetypeFactory); legacy profiles use it" },
            { "PlayerManager._effectConfig", "PlayerDriver.ResolveEffectConfig loads the Resources fallback" },
        };

        [Test]
        public void OptionalReferencesEachCiteAFallback()
        {
            foreach (var entry in OptionalReferences)
            {
                Assert.That(entry.Key, Does.Match(@"^[A-Za-z]\w*\.\w+$"), "Key must be Type.field");
                Assert.That(entry.Value, Is.Not.Empty, entry.Key + " needs the fallback that handles its null");
            }
        }

        [Test]
        public void GeneratedAssetsAndPrefabsHaveResolvedSerializedReferences()
        {
            string[] roots = { "Assets/Resources/ScriptableObjects", "Assets/Resources/UI", "Assets/Resources/ProceduralKits", "Assets/Prefabs" };
            string[] paths = roots.Where(AssetDatabase.IsValidFolder)
                .SelectMany(root => AssetDatabase.FindAssets("", new[] { root }))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".asset", StringComparison.Ordinal) || path.EndsWith(".prefab", StringComparison.Ordinal))
                .Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.That(paths, Is.Not.Empty, "No generated assets were available to audit; run Rebuild All.");
            var failures = new List<string>();
            int references = 0;
            foreach (string path in paths)
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets.Length == 0 || assets.All(asset => asset == null)) { failures.Add(path + ": asset failed to load"); continue; }
                foreach (Object asset in assets.Where(asset => asset != null))
                {
                    references += Audit(asset, path, failures);
                    if (!(asset is GameObject root)) continue;
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        foreach (Component component in child.GetComponents<Component>())
                        {
                            if (component == null) { failures.Add(path + "/" + child.name + ": missing script"); continue; }
                            references += Audit(component, path + "/" + child.name, failures);
                        }
                }
            }
            Assert.That(references, Is.GreaterThan(0), "An empty reference traversal is not a pass.");
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void RequiredGeneratedProfileAndPrefabBindingsAreNonNull()
        {
            var failures = new List<string>();
            var profile = AssetDatabase.LoadAssetAtPath<PlayerProfile>(PlayerPrefabGenerator.ProfilePath);
            Assert.That(profile, Is.Not.Null, "Run Rebuild All before the reference audit.");
            Audit(profile, PlayerPrefabGenerator.ProfilePath, failures);
            int references = 0;
            foreach (string path in new[] { PlayerPrefabGenerator.PrefabPath, FloorConfigGenerator.PrefabPath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                    foreach (Component component in child.GetComponents<Component>())
                    {
                        Assert.That(component, Is.Not.Null, path + "/" + child.name + " missing script");
                        if (component is MonoBehaviour) references += Audit(component, path + "/" + child.name, failures);
                    }
            }
            Assert.That(AssetDatabase.LoadAssetAtPath<PlayerEffectConfig>(PlayerEffectConfigSetup.AssetPath), Is.Not.Null,
                "The PlayerManager Resources fallback must also resolve.");
            Assert.That(references, Is.GreaterThan(0));
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void AuditRejectsNullBindingAndAcceptsResolvedBinding()
        {
            var profile = ScriptableObject.CreateInstance<PlayerProfile>();
            var reference = new GameObject("Audit reference");
            try
            {
                var failures = new List<string>();
                Audit(profile, "in-memory profile", failures);
                Assert.That(failures, Has.Some.Contains("PlayerProfile._prefab: required reference is null"));
                Worsen.Editor.Common.SetupKit.Wire(profile, "_prefab", reference);
                failures.Clear();
                Audit(profile, "in-memory profile", failures);
                Assert.That(failures, Is.Empty, string.Join("\n", failures));
            }
            finally { Object.DestroyImmediate(profile); Object.DestroyImmediate(reference); }
        }

        private static int Audit(Object owner, string context, List<string> failures)
        {
            int references = 0;
            using (var serialized = new SerializedObject(owner))
            {
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    references++;
                    if (property.objectReferenceValue != null) continue;
                    bool missing = property.objectReferenceInstanceIDValue != 0;
                    bool authored = (owner.GetType().Namespace ?? "").StartsWith("Worsen.", StringComparison.Ordinal) &&
                        !property.propertyPath.StartsWith("m_", StringComparison.Ordinal) &&
                        !OptionalReferences.ContainsKey(owner.GetType().Name + "." + property.name);
                    if (missing || authored || property.propertyPath == "m_Script")
                        failures.Add(context + " / " + owner.GetType().Name + "." + property.propertyPath +
                            (missing ? ": unresolved serialized reference" : ": required reference is null"));
                }
            }
            return references;
        }
    }
}
