// ============================================================================
// ShrineModelSetup.cs
// ============================================================================
// PURPOSE:
//   Imports the eight original shrine FBXs and wires their serialized model references.
//   Explicit importer axes and URP material remaps make binary art wiring reproducible.
//   Repeat setup preserves config/material identities and all existing palette tuning.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Shrine.
// KEY RESPONSIBILITIES:
//   - Preflight all eight exports and manifests before configuring static model imports.
//   - Create missing URP materials and remap the two named FBX material slots.
//   - Wire the mirrored ShrineDriverConfig without editing scenes or prefabs.
// DEPENDENCIES:
//   - Domain Shrine, Core ShrineKind, Common SetupKit and UnityEditor asset APIs.
// USAGE NOTES:
//   Coordinator-only, idle Edit Mode under its lease, after integrating the sources/art.
//   Never automatic. Writes importers, Art/Shrine materials and the mirrored config only.
//   Run twice and verify identities, assignments and an in-game lineup before acceptance.
// ============================================================================
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Shrine;
namespace Worsen.Editor.Shrine
{
    public static class ShrineModelSetup
    {
        public const string ConfigPath = "Assets/Resources/ScriptableObjects/Domain/Shrine/ShrineDriverConfig.asset";
        [Serializable]
        private sealed class Palette
        {
            public string name = "";
            public float[] color_srgb = Array.Empty<float>();
            public float emission = 0f;
            public float roughness = 0f;
        }
        [Serializable]
        private sealed class Manifest
        {
            public string kind = "";
            public Palette[] materials = Array.Empty<Palette>();
        }
        [MenuItem("Worsen/Shrine/Import and Wire Shrine Models")]
        public static void ImportAndWire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Shrine model setup requires idle Edit Mode.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit is required for shrine emission.");
            var kinds = (ShrineKind[])Enum.GetValues(typeof(ShrineKind));
            var manifests = kinds.Select(ReadManifest).ToArray();
            foreach (var kind in kinds)
                if (!File.Exists(ModelPath(kind))) throw new FileNotFoundException("Regenerate shrine models first.", ModelPath(kind));
            var config = AssetDatabase.LoadAssetAtPath<ShrineDriverConfig>(ConfigPath);
            if (config == null && AssetDatabase.LoadMainAssetAtPath(ConfigPath) != null)
                throw new InvalidOperationException("Wrong asset type at " + ConfigPath);
            var models = new GameObject[kinds.Length];
            for (int i = 0; i < kinds.Length; i++) models[i] = Import(kinds[i], manifests[i], shader);
            if (config == null)
            {
                Worsen.Editor.Common.SetupKit.EnsureFolder(Path.GetDirectoryName(ConfigPath).Replace('\\', '/'));
                config = ScriptableObject.CreateInstance<ShrineDriverConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            var serialized = new SerializedObject(config);
            for (int i = 0; i < kinds.Length; i++)
            {
                string name = kinds[i].ToString();
                serialized.FindProperty("_" + char.ToLowerInvariant(name[0]) + name.Substring(1) + "Model").objectReferenceValue = models[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(config);
            foreach (var kind in kinds)
                if (config.GetModel(kind) != AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(kind)))
                    throw new InvalidOperationException("Shrine model wiring did not persist: " + kind);
            Debug.Log("Shrine model setup: eight static imports and eight model references wired; no scene changes.");
        }
        public static string ModelPath(ShrineKind kind) => "Assets/Art/Shrine/" + kind + "/WORSEN_Shrine" + kind + ".fbx";
        private static Manifest ReadManifest(ShrineKind kind)
        {
            string path = "ArtSource/Shrine/" + kind + "/WORSEN_Shrine" + kind + ".manifest.json";
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            if (manifest == null || manifest.kind != kind.ToString() || manifest.materials == null || manifest.materials.Length != 2 ||
                !manifest.materials.Select(p => p.name).OrderBy(n => n).SequenceEqual(
                    new[] { ShrineDriverConfig.BodyMaterialName, ShrineDriverConfig.AccentMaterialName }.OrderBy(n => n)))
                throw new InvalidOperationException("Invalid shrine palette manifest: " + path);
            foreach (var palette in manifest.materials)
                if (palette.color_srgb == null || palette.color_srgb.Length != 3)
                    throw new InvalidOperationException("Invalid shrine material colour: " + path);
            return manifest;
        }
        private static GameObject Import(ShrineKind kind, Manifest manifest, Shader shader)
        {
            string path = ModelPath(kind);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("No ModelImporter at " + path);
            importer.globalScale = 1f; importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.preserveHierarchy = true; importer.optimizeGameObjects = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            string folder = "Assets/Art/Shrine/" + kind + "/Materials";
            Worsen.Editor.Common.SetupKit.EnsureFolder(folder);
            foreach (var palette in manifest.materials)
            {
                string materialPath = folder + "/" + palette.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(materialPath) != null)
                        throw new InvalidOperationException("Wrong asset type at " + materialPath);
                    var c = palette.color_srgb;
                    material = new Material(shader) { name = palette.name };
                    material.SetColor("_BaseColor", new Color(c[0], c[1], c[2], 1f));
                    material.SetFloat("_Smoothness", 1f - palette.roughness);
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", new Color(c[0], c[1], c[2], 1f) * palette.emission);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                if (material.name != palette.name || !material.HasProperty("_EmissionColor"))
                    throw new InvalidOperationException("Incompatible authored shrine material: " + materialPath);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), palette.name), material);
            }
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new InvalidOperationException("Shrine import failed: " + path);
            var slots = model.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
            if (slots.Length != 2 || slots.Any(m => m == null) ||
                !slots.Select(m => m.name).OrderBy(n => n).SequenceEqual(manifest.materials.Select(p => p.name).OrderBy(n => n)))
                throw new InvalidOperationException("Shrine imported material slots differ from manifest: " + path);
            return model;
        }
    }
}
