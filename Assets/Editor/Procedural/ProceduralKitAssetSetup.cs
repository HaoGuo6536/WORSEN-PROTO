// ============================================================================
// ProceduralKitAssetSetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds theme kit prefabs and room previews from validated art manifests.
//   Import conversion, material mapping and triangle admission are explicit;
//   absent art leaves a null kit binding and the runtime primitive fallback.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Procedural.
// KEY RESPONSIBILITIES:
//   - Import FBX with axis conversion disabled and theme-local URP slot materials.
//   - Reject over-budget meshes and publish reusable kit prefab bindings.
//   - Assemble deterministic room prefabs from the runtime placement presenter.
// DEPENDENCIES:
//   - UnityEditor asset APIs and Domain.Procedural definitions/presenters.
// USAGE NOTES:
//   Called only by the explicit content setup in an idle coordinated editor.
//   Generated assets are never authored by a worker outside Unity.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Editor.Procedural
{
    public static class ProceduralKitAssetSetup
    {
        public static void Build(ProceduralRoomCatalogueData asset, ProceduralConfig config)
        {
            var bindings = new List<ProceduralRoomCatalogueData.KitAsset>();
            const string output = "Assets/Resources/ProceduralKits";
            Folder(output);
            foreach (var catalogue in asset.Catalogues)
            {
                string theme = char.ToUpperInvariant(catalogue.Theme[0]) + catalogue.Theme.Substring(1);
                string source = "Assets/Art/Environment/" + theme + "/Kit/";
                string folder = output + "/" + theme; Folder(folder); Folder(folder + "/Materials"); Folder(folder + "/Rooms");
                foreach (var piece in catalogue.Kit)
                {
                    string path = source + piece.File;
                    GameObject prefab = null;
                    if (File.Exists(path))
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                        if (importer == null) throw new InvalidOperationException("Expected FBX model importer: " + path);
                        importer.bakeAxisConversion = false; importer.SaveAndReimport();
                        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        // Count every submesh, not just the first material slot.
                        int triangles = model.GetComponentsInChildren<MeshFilter>(true).Sum(m => Enumerable.Range(0, m.sharedMesh.subMeshCount)
                            .Sum(i => (int)m.sharedMesh.GetIndexCount(i) / 3));
                        int limit = piece.Kind == "prop" ? 1500 : ProceduralTemplateValidationUtility.Wall(piece) || piece.Kind == "floor" ? 300 : int.MaxValue;
                        if (triangles > limit) throw new InvalidOperationException(path + " exceeds triangle budget: " + triangles);
                        foreach (var material in model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct())
                        {
                            if (material == null || !material.name.StartsWith(catalogue.Theme + "_", StringComparison.Ordinal) ||
                                material.name.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
                                throw new InvalidOperationException("Invalid theme material slot in " + path);
                            string materialPath = folder + "/Materials/" + material.name + ".mat";
                            var mapped = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                            if (mapped == null)
                            {
                                var shader = Shader.Find("Universal Render Pipeline/Lit");
                                if (shader == null) throw new InvalidOperationException("URP Lit shader unavailable.");
                                mapped = new Material(shader) { name = material.name, color = material.color };
                                mapped.SetFloat("_Smoothness", .15f); AssetDatabase.CreateAsset(mapped, materialPath);
                            }
                            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), mapped);
                        }
                        importer.SaveAndReimport();
                        model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        var instance = UnityEngine.Object.Instantiate(model);
                        try
                        {
                            instance.name = theme + "_" + piece.Id;
                            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                            prefab = PrefabUtility.SaveAsPrefabAsset(instance, folder + "/" + piece.Id + ".prefab");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(instance); }
                    }
                    else Debug.LogWarning("Missing kit piece; runtime primitive fallback: " + path);
                    bindings.Add(new ProceduralRoomCatalogueData.KitAsset { Theme = catalogue.Theme, Id = piece.Id, Prefab = prefab });
                }
                var driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
                try
                {
                    foreach (var room in catalogue.Templates)
                    {
                        // Editor writes system-local snapshots, not a live generator state.
                        var placed = new ProceduralTemplateRoom { RoomId = 1, Template = room, OpenDoors = Enumerable.Range(0, room.Doors.Length).ToArray() };
                        var presenter = new ProceduralTemplateGeometryPresenter();
                        var blocks = presenter.Build(catalogue, placed, config, driver);
                        var root = new GameObject(room.Id);
                        try
                        {
                            foreach (var block in blocks)
                            {
                                var binding = bindings.FirstOrDefault(b => b.Theme == catalogue.Theme && b.Id == block.PieceId);
                                GameObject item;
                                if (binding?.Prefab != null)
                                {
                                    item = UnityEngine.Object.Instantiate(binding.Prefab);
                                    item.transform.SetPositionAndRotation(block.PiecePosition, block.Rotation);
                                }
                                else
                                {
                                    item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                                    item.name = "Primitive " + block.Kind;
                                    item.transform.SetPositionAndRotation(block.Center, block.Rotation); item.transform.localScale = block.Size;
                                    if (!block.HasCollision) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
                                }
                                item.transform.SetParent(root.transform, true);
                            }
                            PrefabUtility.SaveAsPrefabAsset(root, folder + "/Rooms/" + room.Id + ".prefab");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(root); }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(driver); }
            }
            var serialized = new SerializedObject(asset); var pieces = serialized.FindProperty("_pieces"); pieces.arraySize = bindings.Count;
            for (int i = 0; i < bindings.Count; i++)
            {
                var item = pieces.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Theme").stringValue = bindings[i].Theme;
                item.FindPropertyRelative("Id").stringValue = bindings[i].Id;
                item.FindPropertyRelative("Prefab").objectReferenceValue = bindings[i].Prefab;
            }
            serialized.ApplyModifiedProperties(); AssetDatabase.SaveAssetIfDirty(asset);
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
