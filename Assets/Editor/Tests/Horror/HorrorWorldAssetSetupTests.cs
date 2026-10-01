// ============================================================================
// HorrorWorldAssetSetupTests.cs
// ============================================================================
// PURPOSE:
//   Checks the deterministic weathered exit material contract without an Editor.
//   Native cases then prove the imported assembly, persistent material mapping and
//   repeatable config repair without replacing designer tuning or asset identities.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Horror asset setup verification.
// KEY RESPONSIBILITIES:
//   - Verify exact material-slot routing and fail closed on unknown slots.
//   - Check metre-scale axes, named parts, hinge and aperture in the saved prefab.
//   - Check persistent URP/portal materials and exported non-paint colors.
//   - Exercise repeated Configure with disposable persistent configs.
// DEPENDENCIES:
//   NUnit, HorrorWorldAssetSetup, Floor/Procedural configs and UnityEditor asset APIs.
// USAGE NOTES:
//   NativeEditMode cases require coordinator setup and the exclusive Unity lease.
//   Tests create/delete only uniquely named config copies; generated setup outputs
//   remain for integration. No scene edits or runtime lifecycle callbacks are used.
//   Reflection accesses the private slot contract without widening the editor API.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Worsen.Domain.Floor;
using Worsen.Domain.Procedural;
using Worsen.Editor.Horror;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Horror
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class HorrorWorldAssetSetupTests
    {
        private const string ModelPath = "Assets/Art/Exit/WeatheredDoor/WORSEN_WeatheredExitDoor.fbx";
        private const string PaintPath = "Assets/Art/Exit/WeatheredDoor/ExitPaint.png";
        private const string PrefabPath = "Assets/Prefabs/Horror/Environment/ExitDoorLeaf.prefab";
        private const string MaterialRoot = "Assets/Art/Exit/WeatheredDoor/Materials/";
        private const string FloorPath = "Assets/Resources/ScriptableObjects/Domain/Floor/HorrorFloorDriverConfig.asset";
        private const string ProceduralPath = "Assets/Resources/ScriptableObjects/Domain/Procedural/ProceduralDriverConfig.asset";

        [TestCase("Exit_EscapeSurface", "Worsen/ExitPortal")]
        [TestCase("Exit_PatinatedPaint", "Universal Render Pipeline/Lit")]
        [TestCase("Exit_WornEdges", "Universal Render Pipeline/Lit")]
        [TestCase("Exit_OldBrass", "Universal Render Pipeline/Lit")]
        [TestCase("Exit_ThresholdStone", "Universal Render Pipeline/Lit")]
        public void ExactExportedSlotsRouteToTheRequiredShader(string slot, string shader)
        {
            Assert.That(SlotMethod().Invoke(null, new object[] { slot }), Is.EqualTo(shader));
        }

        [TestCase(null)] [TestCase("")] [TestCase("Exit_EscapeSurface (Instance)")] [TestCase("CastleWood")]
        public void UnknownOrMissingSlotsFailClosed(string slot)
        {
            var error = Assert.Throws<TargetInvocationException>(() => SlotMethod().Invoke(null, new object[] { slot }));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void GateEntryIsPublicStaticAndArgumentFree()
        {
            var method = typeof(HorrorWorldAssetSetup).GetMethod("WireConfig", BindingFlags.Public | BindingFlags.Static,
                null, Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
        }

        [Test, Category("NativeEditMode")]
        public void CanonicalHorrorConfigUsesTheCompleteAssemblyWithZeroPrefabYaw()
        {
            var floor = Require<FloorDriverConfig>(FloorPath);
            Assert.That(floor.UsePhysicalExitDoor, Is.True);
            Assert.That(floor.ExitDoorPrefab, Is.SameAs(Require<GameObject>(PrefabPath)));
            Assert.That(floor.ExitDoorPrefabYaw, Is.Zero);
        }

        [Test, Category("NativeEditMode")]
        public void ImportedAssemblyPreservesNamedPartsMetreAxesAndHinge()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.bakeAxisConversion, Is.False);
            Assert.That(importer.globalScale, Is.EqualTo(1f));
            Assert.That(importer.useFileScale, Is.True);
            Assert.That(importer.addCollider || importer.importAnimation, Is.False);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.None));
            Assert.That(importer.preserveHierarchy, Is.True);
            var prefab = Require<GameObject>(PrefabPath);
            var source = Require<GameObject>(ModelPath);
            var parts = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(parts.Select(part => part.name), Is.EquivalentTo(new[] { "DoorFrame", "DoorLeaf", "Threshold", "EscapeSurface" }));
            Assert.That(prefab.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(prefab.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Animator>(true), Is.Empty);
            foreach (var part in parts)
            {
                var original = source.GetComponentsInChildren<Renderer>(true).Single(item => item.name == part.name);
                Assert.That(part.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(original.GetComponent<MeshFilter>().sharedMesh));
                Assert.That(part.transform.localPosition, Is.EqualTo(original.transform.localPosition));
                Assert.That(part.transform.localRotation, Is.EqualTo(original.transform.localRotation));
                Assert.That(part.transform.localScale, Is.EqualTo(original.transform.localScale));
            }
            var leaf = parts.Single(part => part.name == "DoorLeaf");
            Assert.That(Vector3.Distance(leaf.transform.localPosition, Vector3.left), Is.LessThan(.001f));
            Assert.That(leaf.localBounds.center.x, Is.EqualTo(1f).Within(.025f));
            Assert.That(leaf.localBounds.center.y, Is.EqualTo(1.5f).Within(.025f));
            Assert.That(leaf.localBounds.size.x, Is.EqualTo(1.98f).Within(.025f));
            Assert.That(leaf.localBounds.size.y, Is.EqualTo(2.96f).Within(.025f));
            var escape = parts.Single(part => part.name == "EscapeSurface");
            var mesh = escape.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            Assert.That(escape.localBounds.size.x, Is.EqualTo(2f).Within(.001f));
            Assert.That(escape.localBounds.size.y, Is.EqualTo(2.96f).Within(.001f));
            Assert.That(Vector3.Dot(escape.transform.TransformDirection(mesh.normals[0]), Vector3.back), Is.GreaterThan(.999f));
            Assert.That(escape.enabled, Is.False);
            Assert.That(escape.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(escape.receiveShadows, Is.False);
        }

        [Test, Category("NativeEditMode")]
        public void PrefabSlotsUsePersistentPortalAndLitMaterialsWithExportedColors()
        {
            var prefab = Require<GameObject>(PrefabPath);
            var original = Require<GameObject>(ModelPath).GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials).GroupBy(material => material.name)
                .ToDictionary(group => group.Key, group => group.First());
            var materials = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials)
                .GroupBy(material => material.name).Select(group => group.First()).ToArray();
            Assert.That(materials.Select(material => material.name), Is.EquivalentTo(original.Keys));
            foreach (var material in materials)
            {
                Assert.That(AssetDatabase.GetAssetPath(material), Is.EqualTo(MaterialRoot + material.name + ".mat"));
                Assert.That(EditorUtility.IsPersistent(material), Is.True);
                Assert.That(material.shader.name, Is.EqualTo(SlotMethod().Invoke(null, new object[] { material.name })));
                if (material.name == "Exit_EscapeSurface") continue;
                Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(.22f).Within(.0001f));
                Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(material.name == "Exit_OldBrass" ? .65f : 0f).Within(.0001f));
                if (material.name == "Exit_PatinatedPaint")
                {
                    Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.white));
                    Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(Require<Texture2D>(PaintPath)));
                }
                else
                {
                    var exported = original[material.name];
                    var color = exported.GetColor(exported.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
                    Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(color));
                }
            }
        }

        [Test, Category("NativeEditMode")]
        public void ConfigureIsIdempotentAndPreservesUnrelatedDesignerValues()
        {
            string suffix = "_ExitWiringTest_" + Guid.NewGuid().ToString("N") + ".asset";
            string floorCopy = FloorPath.Replace(".asset", suffix), proceduralCopy = ProceduralPath.Replace(".asset", suffix);
            bool ownsFloor = false, ownsProcedural = false;
            try
            {
                Require<FloorDriverConfig>(FloorPath); Require<ProceduralDriverConfig>(ProceduralPath);
                ownsFloor = AssetDatabase.CopyAsset(FloorPath, floorCopy); Assert.That(ownsFloor, Is.True);
                ownsProcedural = AssetDatabase.CopyAsset(ProceduralPath, proceduralCopy); Assert.That(ownsProcedural, Is.True);
                var floor = Require<FloorDriverConfig>(floorCopy);
                var procedural = Require<ProceduralDriverConfig>(proceduralCopy);
                var data = new SerializedObject(floor);
                data.FindProperty("_exitDoorOpeningDuration").floatValue = 2.3f;
                data.FindProperty("_exitDoorOpeningAngle").floatValue = 115f;
                data.FindProperty("_exitCrossingDistance").floatValue = .47f;
                data.FindProperty("_exitDoorYaw").floatValue = 37f;
                data.FindProperty("_handVisualScale").floatValue = 1.7f;
                data.FindProperty("_handGridWidth").intValue = 4;
                data.FindProperty("_portalInset").floatValue = .4f;
                data.FindProperty("_warningIntensity").floatValue = 3.7f;
                data.FindProperty("_warningColor").colorValue = new Color(.11f, .22f, .33f, 1f);
                data.FindProperty("_closedColor").colorValue = new Color(.33f, .22f, .11f, 1f);
                data.FindProperty("_usePhysicalExitDoor").boolValue = false;
                data.FindProperty("_exitDoorPrefabYaw").floatValue = 90f;
                // Distinct persistent references prove that setup does not replace
                // authored non-exit dependencies with its generated defaults.
                var customMaterial = Require<Material>(MaterialRoot + "Exit_PatinatedPaint.mat");
                data.FindProperty("_handPrefab").objectReferenceValue = Require<GameObject>(PrefabPath);
                data.FindProperty("_crackMaterial").objectReferenceValue = customMaterial;
                data.FindProperty("_mistMaterial").objectReferenceValue = customMaterial;
                data.FindProperty("_exitDoorMaterial").objectReferenceValue = customMaterial;
                data.ApplyModifiedPropertiesWithoutUndo();
                var proceduralData = new SerializedObject(procedural);
                foreach (string field in new[] { "_wallMaterial", "_floorMaterial", "_ceilingMaterial" })
                    proceduralData.FindProperty(field).objectReferenceValue = customMaterial;
                proceduralData.ApplyModifiedPropertiesWithoutUndo();
                var tuning = Tuning(floor);
                var proceduralTuning = Tuning(procedural);
                var warning = floor.LumenRoomWarningPrefab; var glow = floor.LumenExitGlowPrefab;
                var legacyMaterial = floor.ExitDoorMaterial; var cake = floor.CakePrefab;
                Assert.That(warning, Is.Not.Null, "Coordinator world setup must run before this native fixture.");
                Assert.That(glow, Is.Not.Null);
                HorrorWorldAssetSetup.Configure(floor, procedural);
                Assert.That(floor.UsePhysicalExitDoor, Is.True);
                Assert.That(AssetDatabase.GetAssetPath(floor.ExitDoorPrefab), Is.EqualTo(PrefabPath));
                Assert.That(floor.ExitDoorPrefabYaw, Is.Zero);
                Assert.That(Tuning(floor), Is.EquivalentTo(tuning));
                Assert.That(Tuning(procedural), Is.EquivalentTo(proceduralTuning));
                Assert.That(floor.LumenRoomWarningPrefab, Is.SameAs(warning));
                Assert.That(floor.LumenExitGlowPrefab, Is.SameAs(glow));
                Assert.That(floor.ExitDoorMaterial, Is.SameAs(legacyMaterial));
                Assert.That(floor.CakePrefab, Is.SameAs(cake));
                Assert.That(floor.HandPrefab, Is.SameAs(Require<GameObject>(PrefabPath)));
                Assert.That(floor.CrackMaterial, Is.SameAs(customMaterial));
                Assert.That(floor.MistMaterial, Is.SameAs(customMaterial));
                proceduralData.Update();
                foreach (string field in new[] { "_wallMaterial", "_floorMaterial", "_ceilingMaterial" })
                    Assert.That(proceduralData.FindProperty(field).objectReferenceValue, Is.SameAs(customMaterial));
                string floorJson = EditorJsonUtility.ToJson(floor), proceduralJson = EditorJsonUtility.ToJson(procedural);
                string prefabText = File.ReadAllText(PrefabPath), importJson = EditorJsonUtility.ToJson(AssetImporter.GetAtPath(ModelPath));
                var identities = ExitIdentities();
                HorrorWorldAssetSetup.Configure(floor, procedural);
                Assert.That(EditorJsonUtility.ToJson(floor), Is.EqualTo(floorJson));
                Assert.That(EditorJsonUtility.ToJson(procedural), Is.EqualTo(proceduralJson));
                Assert.That(File.ReadAllText(PrefabPath), Is.EqualTo(prefabText));
                Assert.That(EditorJsonUtility.ToJson(AssetImporter.GetAtPath(ModelPath)), Is.EqualTo(importJson));
                Assert.That(ExitIdentities(), Is.EquivalentTo(identities));
            }
            finally
            {
                try { if (ownsFloor) Assert.That(AssetDatabase.DeleteAsset(floorCopy), Is.True); }
                finally { if (ownsProcedural) Assert.That(AssetDatabase.DeleteAsset(proceduralCopy), Is.True); }
            }
        }

        private static MethodInfo SlotMethod() => typeof(HorrorWorldAssetSetup).GetMethod("ExitMaterialShader", BindingFlags.NonPublic | BindingFlags.Static);
        private static T Require<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Coordinator must run HorrorWorldAssetSetup.WireConfig first: " + path);
            return asset;
        }
        private static Dictionary<string, object> Tuning(Object config)
        {
            var values = new Dictionary<string, object>();
            var property = new SerializedObject(config).GetIterator();
            while (property.Next(true))
            {
                if (property.propertyPath == "_usePhysicalExitDoor" || property.propertyPath == "_exitDoorPrefabYaw") continue;
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Boolean: values.Add(property.propertyPath, property.boolValue); break;
                    case SerializedPropertyType.Float: values.Add(property.propertyPath, property.floatValue); break;
                    case SerializedPropertyType.Integer: values.Add(property.propertyPath, property.intValue); break;
                    case SerializedPropertyType.Color: values.Add(property.propertyPath, property.colorValue); break;
                    case SerializedPropertyType.Vector3: values.Add(property.propertyPath, property.vector3Value); break;
                }
            }
            return values;
        }
        private static string[] ExitIdentities()
        {
            var prefab = Require<GameObject>(PrefabPath);
            var assets = prefab.GetComponentsInChildren<Transform>(true).Select(part => (Object)part.gameObject)
                .Concat(prefab.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials));
            return assets.Select(asset =>
            {
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId), Is.True);
                return asset.name + ":" + guid + ":" + localId;
            }).ToArray();
        }
    }
}
