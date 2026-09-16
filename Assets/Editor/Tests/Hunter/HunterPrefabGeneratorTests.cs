// ============================================================================
// HunterPrefabGeneratorTests.cs
// ============================================================================
// PURPOSE:
//   Exercises real Unity prefab creation and rebuilding so missing-component
//   wrappers cannot pass as usable bodies, and regeneration preserves asset identity.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify required components, serialized wiring, stable IDs and authored tuning.
// DEPENDENCIES:
//   - Hunter generator/runtime types, UnityEditor asset APIs and NUnit.
// USAGE NOTES:
//   Run only under the coordinator's Unity lease. Each case owns a unique temporary
//   asset folder and removes it afterward; canonical Hunter assets are never changed.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Hunter;
using Worsen.Editor.Hunter;

namespace Worsen.Tests.Hunter
{
    public sealed class HunterPrefabGeneratorTests
    {
        private string _folder;
        private string PrefabPath => _folder + "/Hunter.prefab";
        private string ProfilePath => _folder + "/HunterProfile.asset";
        private string ConfigPath => _folder + "/HunterMotorDriverConfig.asset";

        [SetUp]
        public void SetUp()
        {
            string name = "__GeneratedHunter_" + Guid.NewGuid().ToString("N");
            _folder = "Assets/Editor/Tests/Hunter/" + name;
            Assert.That(AssetDatabase.CreateFolder("Assets/Editor/Tests/Hunter", name), Is.Not.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_folder) && AssetDatabase.IsValidFolder(_folder))
                Assert.That(AssetDatabase.DeleteAsset(_folder), Is.True, "Temporary Hunter assets must be removed.");
        }

        [Test]
        public void NewPrefabCreatesUsableBodyAndWiresRequiredComponents()
        {
            HunterProfile profile = HunterPrefabGenerator.EnsureAssets(PrefabPath, ProfilePath, ConfigPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            HunterMotorDriverConfig config = AssetDatabase.LoadAssetAtPath<HunterMotorDriverConfig>(ConfigPath);
            Assert.That(profile != null && prefab != null && config != null, Is.True);
            Assert.That(profile.Prefab, Is.EqualTo(prefab));
            Assert.That(prefab.GetComponents<Rigidbody>().Length, Is.EqualTo(1));
            Rigidbody body = prefab.GetComponent<Rigidbody>();
            Assert.That(body != null, Is.True, "A Unity missing-component wrapper is not a usable body.");
            Assert.That(body.isKinematic, Is.True); Assert.That(body.useGravity, Is.False);
            CapsuleCollider capsule = prefab.GetComponent<CapsuleCollider>();
            HunterDriver driver = prefab.GetComponent<HunterDriver>();
            HunterManager manager = prefab.GetComponent<HunterManager>();
            Assert.That(capsule != null && driver != null && manager != null, Is.True);
            Assert.That(capsule.radius, Is.EqualTo(config.Radius));
            Assert.That(new SerializedObject(driver).FindProperty("_body").objectReferenceValue, Is.EqualTo(body));
            Assert.That(new SerializedObject(driver).FindProperty("_capsule").objectReferenceValue, Is.EqualTo(capsule));
            Assert.That(new SerializedObject(driver).FindProperty("_config").objectReferenceValue, Is.EqualTo(config));
            Assert.That(new SerializedObject(manager).FindProperty("_driver").objectReferenceValue, Is.EqualTo(driver));
        }

        [Test]
        public void RebuildPreservesAssetAndComponentIdentitiesAndAuthoredTuning()
        {
            HunterProfile profile = HunterPrefabGenerator.EnsureAssets(PrefabPath, ProfilePath, ConfigPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            HunterMotorDriverConfig config = AssetDatabase.LoadAssetAtPath<HunterMotorDriverConfig>(ConfigPath);
            string[] identities = Identities(profile, config, prefab);
            var profileFields = new SerializedObject(profile);
            profileFields.FindProperty("_chaseSpeedMultiplier").floatValue = 1.37f;
            profileFields.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(profile);
            var configFields = new SerializedObject(config);
            configFields.FindProperty("_radius").floatValue = 0.47f;
            configFields.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(config);

            profile = HunterPrefabGenerator.EnsureAssets(PrefabPath, ProfilePath, ConfigPath);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            config = AssetDatabase.LoadAssetAtPath<HunterMotorDriverConfig>(ConfigPath);
            Assert.That(Identities(profile, config, prefab), Is.EqualTo(identities));
            Assert.That(profile.ChaseSpeedMultiplier, Is.EqualTo(1.37f));
            Assert.That(config.Radius, Is.EqualTo(0.47f));
            Assert.That(prefab.GetComponent<CapsuleCollider>().radius, Is.EqualTo(0.47f));
            Assert.That(prefab.GetComponents<Rigidbody>().Length, Is.EqualTo(1));
            Assert.That(prefab.GetComponents<CapsuleCollider>().Length, Is.EqualTo(1));
            Assert.That(prefab.GetComponents<HunterDriver>().Length, Is.EqualTo(1));
            Assert.That(prefab.GetComponents<HunterManager>().Length, Is.EqualTo(1));
            Assert.That(profile.Prefab, Is.EqualTo(prefab));
        }

        private static string[] Identities(HunterProfile profile, HunterMotorDriverConfig config, GameObject prefab) =>
            new[] { Identity(profile), Identity(config), Identity(prefab), Identity(prefab.GetComponent<Rigidbody>()),
                Identity(prefab.GetComponent<CapsuleCollider>()), Identity(prefab.GetComponent<HunterDriver>()),
                Identity(prefab.GetComponent<HunterManager>()), Identity(prefab.transform.Find("Hunter Body").gameObject) };

        private static string Identity(UnityEngine.Object value)
        {
            Assert.That(value != null, Is.True);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localId), Is.True);
            return guid + ":" + localId;
        }
    }
}
