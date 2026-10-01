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
//   - Preserve embedded appearance, rebuild stale slots and retain artist overrides.
//   - Reject over-budget meshes and publish reusable kit prefab bindings.
//   - Assemble deterministic room prefabs from the runtime placement presenter.
//   - Keep one kit visual separate from independently owned primitive collision.
// DEPENDENCIES:
//   - Common SetupKit creates asset folders while retaining existing identities.
//   - UnityEditor asset APIs and Domain.Procedural definitions/presenters.
// USAGE NOTES:
//   Called only by the explicit content setup in an idle coordinated editor.
//   Material/prefab assets are written only in Unity. Generated PNGs and per-slot
//   scale metadata come from tools/textures/generate_theme_textures.py. Texture
//   provenance tags record source and generated-state hashes. Only unchanged
//   generated outputs rebuild; unrecognised legacy materials are kept intact.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
            var textured = new HashSet<string>(StringComparer.Ordinal);
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
                        importer.bakeAxisConversion = false;
                        var originalRemaps = importer.GetExternalObjectMap().Where(p => p.Key.type == typeof(Material)).ToArray();
                        var pendingRemaps = new Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object>();
                        bool completed = false;
                        GameObject model;
                        try
                        {
                            // Some importer versions omit remapped embedded subassets.
                            // Unmap during source import, then restore in finally even
                            // if validation fails; never use generated output as input.
                            foreach (var remap in originalRemaps) importer.RemoveRemap(remap.Key);
                            importer.SaveAndReimport();
                            model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                            // Count every submesh, not just the first material slot.
                            int triangles = model.GetComponentsInChildren<MeshFilter>(true).Sum(m => Enumerable.Range(0, m.sharedMesh.subMeshCount)
                                .Sum(i => (int)m.sharedMesh.GetIndexCount(i) / 3));
                            int limit = piece.Kind == "prop" ? 1500 : ProceduralTemplateValidationUtility.Wall(piece) || piece.Kind == "floor" ? 300 : int.MaxValue;
                            if (triangles > limit) throw new InvalidOperationException(path + " exceeds triangle budget: " + triangles);
                            var embedded = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray();
                            if (embedded.Length == 0) throw new InvalidOperationException("Embedded kit materials unavailable: " + path);
                            foreach (var material in embedded)
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
                                    mapped = new Material(shader) { name = material.name };
                                    RebuildSlotMaterial(mapped, material, theme, material.name, true);
                                    AssetDatabase.CreateAsset(mapped, materialPath);
                                    textured.Add(materialPath);
                                }
                                if (textured.Add(materialPath)) RebuildSlotMaterial(mapped, material, theme, material.name);
                                AssetDatabase.SaveAssetIfDirty(mapped);
                                pendingRemaps[new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name)] = mapped;
                            }
                            completed = true;
                        }
                        finally
                        {
                            foreach (var remap in originalRemaps) importer.AddRemap(remap.Key, remap.Value);
                            if (completed)
                                foreach (var remap in pendingRemaps) importer.AddRemap(remap.Key, remap.Value);
                            importer.SaveAndReimport();
                        }
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
                                CreateBlock(block, binding?.Prefab, root.transform);
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
                Worsen.Editor.Common.SetupKit.RequireRelative(item, nameof(ProceduralRoomCatalogueData.KitAsset.Theme)).stringValue = bindings[i].Theme;
                Worsen.Editor.Common.SetupKit.RequireRelative(item, nameof(ProceduralRoomCatalogueData.KitAsset.Id)).stringValue = bindings[i].Id;
                Worsen.Editor.Common.SetupKit.RequireRelative(item, nameof(ProceduralRoomCatalogueData.KitAsset.Prefab)).objectReferenceValue = bindings[i].Prefab;
            }
            serialized.ApplyModifiedProperties(); AssetDatabase.SaveAssetIfDirty(asset);
        }
        public static void CreateBlock(ProceduralBlock block, GameObject prefab, Transform parent)
        {
            // Compound KitCollision commands repeat the piece id, not its art.
            // Ordinary solids still need collision when their prefab has no collider.
            if (prefab != null && block.HasRenderer && block.Role != ProceduralBlockRole.KitCollision)
            {
                var visual = UnityEngine.Object.Instantiate(prefab);
                visual.name = "Kit " + block.PieceId;
                visual.transform.SetPositionAndRotation(block.PiecePosition, block.Rotation);
                visual.transform.SetParent(parent, true);
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            }
            if (block.Role == ProceduralBlockRole.KitVisual) return; // Its parts supply missing-art fallback.
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = "Primitive " + block.Kind;
            item.transform.SetPositionAndRotation(block.Center, block.Rotation);
            item.transform.localScale = block.Size;
            item.transform.SetParent(parent, true);
            item.GetComponent<Renderer>().enabled = block.HasRenderer && prefab == null;
            if (!block.HasCollision) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
        }
        private const string TextureTag = "WorsenGeneratedTextures";
        private const string SourceTag = "WorsenKitSourceHash";
        private const string StateTag = "WorsenKitGeneratedHash";

        // A source change alone is not permission to overwrite an artist's edits.
        // Kept pure so the fail-closed provenance decision has headless coverage.
        public static bool ShouldRebuildSlot(string sourceHash, string savedSourceHash,
            string currentHash, string savedGeneratedHash, bool recognisedLegacy)
            => !string.IsNullOrEmpty(sourceHash) && !string.IsNullOrEmpty(currentHash) &&
               (string.IsNullOrEmpty(savedGeneratedHash) ? recognisedLegacy :
               !string.IsNullOrEmpty(savedSourceHash) && currentHash == savedGeneratedHash && sourceHash != savedSourceHash);

        public static string SlotSourceHash(string embeddedState, string textureSetIds)
        {
            if (embeddedState == null) throw new ArgumentNullException(nameof(embeddedState));
            if (textureSetIds == null) throw new ArgumentNullException(nameof(textureSetIds));
            return Hash("kit-material-v2:" + embeddedState.Length.ToString(CultureInfo.InvariantCulture) + ":" +
                embeddedState + textureSetIds.Length.ToString(CultureInfo.InvariantCulture) + ":" + textureSetIds);
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        private static string ReferenceId(UnityEngine.Object value)
        {
            if (value == null) return "null";
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id))
                return guid + ":" + id.ToString(CultureInfo.InvariantCulture);
            if (value is Texture texture) return "texture:" + texture.name + ":" + texture.imageContentsHash;
            if (value is Shader shader) return "shader:" + shader.name;
            throw new InvalidOperationException("Unstable material reference: " + value.name);
        }

        private static string MaterialHash(Material material)
        {
            // Full serialized state catches edits to shader, keywords, emission,
            // culling, maps (including removals), UVs and otherwise unused values.
            // Replace session-local instance ids with durable asset ids. Do not
            // hash texture CONTENT here: regenerated pixels are source changes,
            // not edits to the material's assigned texture reference.
            var copy = new Material(material) { name = material.name, hideFlags = material.hideFlags };
            try
            {
                copy.SetOverrideTag(SourceTag, ""); copy.SetOverrideTag(StateTag, "");
                var references = new Dictionary<int, string> { { 0, "null" } };
                var property = new SerializedObject(copy).GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        references[property.objectReferenceInstanceIDValue] = ReferenceId(property.objectReferenceValue);
                string json = Regex.Replace(EditorJsonUtility.ToJson(copy), "\"instanceID\"\\s*:\\s*(-?[0-9]+)", match =>
                    "\"assetId\":\"" + references[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)] + "\"");
                return Hash(json);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        private static string SourceHash(Material embedded, string theme, string slot)
        {
            var ids = new StringBuilder();
            for (int i = 0; i < embedded.shader.GetPropertyCount(); i++)
                if (embedded.shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                {
                    var texture = embedded.GetTexture(embedded.shader.GetPropertyName(i));
                    if (texture != null) ids.Append(ReferenceId(texture)).Append(':').Append(texture.imageContentsHash).Append('\n');
                }
            foreach (string path in new[] { TexturePath(theme, slot, "Albedo"), TexturePath(theme, slot, "Normal"),
                TexturePath(theme, slot, "Smoothness"), "Assets/Art/Textures/" + theme + "/" + slot + ".json" })
            {
                // Content ids are independent of whether Unity has imported the
                // set yet (first adoption may create its .meta/GUID).
                ids.Append(path).Append(':');
                if (File.Exists(path))
                    using (var sha = SHA256.Create())
                    using (var stream = File.OpenRead(path)) ids.Append(BitConverter.ToString(sha.ComputeHash(stream)));
                else ids.Append("missing");
                ids.Append('\n');
            }
            return SlotSourceHash(MaterialHash(embedded), ids.ToString());
        }

        public static void CopyEmbeddedAppearance(Material target, Material embedded)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (embedded == null) throw new ArgumentNullException(nameof(embedded));
            string color = embedded.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (embedded.HasProperty(color)) target.SetColor("_BaseColor", embedded.GetColor(color));
            CopyMap(target, embedded, "_BaseMap", embedded.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex");
            if (embedded.HasProperty("_EmissionColor")) target.SetColor("_EmissionColor", embedded.GetColor("_EmissionColor"));
            CopyMap(target, embedded, "_EmissionMap", "_EmissionMap");
            foreach (string property in new[] { "_Smoothness", "_Metallic" })
                if (embedded.HasProperty(property)) target.SetFloat(property, embedded.GetFloat(property));
            if (!embedded.HasProperty("_Smoothness") && embedded.HasProperty("_Glossiness"))
                target.SetFloat("_Smoothness", embedded.GetFloat("_Glossiness"));
            bool emission = embedded.IsKeywordEnabled("_EMISSION") || target.GetColor("_EmissionColor").maxColorComponent > 0f;
            if (emission) target.EnableKeyword("_EMISSION"); else target.DisableKeyword("_EMISSION");
            target.globalIlluminationFlags = embedded.globalIlluminationFlags;
            if (emission) target.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        private static void CopyMap(Material target, Material embedded, string targetProperty, string sourceProperty)
        {
            if (!embedded.HasProperty(sourceProperty)) return;
            target.SetTexture(targetProperty, embedded.GetTexture(sourceProperty));
            target.SetTextureScale(targetProperty, embedded.GetTextureScale(sourceProperty));
            target.SetTextureOffset(targetProperty, embedded.GetTextureOffset(sourceProperty));
        }

        public static bool RebuildSlotMaterial(Material mapped, Material embedded, string theme, string slot, bool newlyCreated = false)
        {
            if (mapped == null) throw new ArgumentNullException(nameof(mapped));
            if (embedded == null) throw new ArgumentNullException(nameof(embedded));
            if (mapped == embedded) throw new ArgumentException("Source must be the embedded material, not its remap.", nameof(embedded));
            string sourceHash = SourceHash(embedded, theme, slot);
            string savedSource = mapped.GetTag(SourceTag, false, "");
            string savedState = mapped.GetTag(StateTag, false, "");
            string current = MaterialHash(mapped);
            bool legacy = false;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader unavailable.");
            if (!newlyCreated && string.IsNullOrEmpty(savedState) && !mapped.isVariant)
            {
                // Exact old generator output only. In particular, do not treat
                // arbitrary white materials or emission overrides as untouched.
                var old = new Material(shader) { name = mapped.name, color = embedded.color };
                try
                {
                    old.SetFloat("_Smoothness", .15f);
                    legacy = current == MaterialHash(old);
                    if (!legacy && mapped.GetTag(TextureTag, false, "") == "1")
                    {
                        ApplySlotTextures(old, theme, slot);
                        legacy = current == MaterialHash(old);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(old); }
            }
            if (!newlyCreated && (mapped.isVariant || !ShouldRebuildSlot(sourceHash, savedSource, current, savedState, legacy)))
            {
                if (current != savedState)
                    Debug.LogWarning("Preserving edited or unrecognised kit material: " + slot);
                return false;
            }
            var rebuilt = new Material(shader) { name = mapped.name };
            try
            {
                CopyEmbeddedAppearance(rebuilt, embedded);
                ApplySlotTextures(rebuilt, theme, slot, true);
                mapped.shader = shader; mapped.CopyPropertiesFromMaterial(rebuilt);
                // Explicitly set tags: CopyPropertiesFromMaterial's tag transfer
                // is not a provenance contract, and an old adoption must clear.
                mapped.SetOverrideTag(TextureTag, rebuilt.GetTag(TextureTag, false, ""));
                mapped.SetOverrideTag(SourceTag, sourceHash);
                mapped.SetOverrideTag(StateTag, MaterialHash(mapped));
                EditorUtility.SetDirty(mapped);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(rebuilt); }
        }

        [Serializable]
        private sealed class TextureRecipe
        {
            public float metresPerTile = 0f;
            public string paletteSrgb = "";
        }

        // SurfaceMetres UVs are metres, so a two-metre repeat needs scale 0.5.
        public static float TextureScale(float metresPerTile)
        {
            if (float.IsNaN(metresPerTile) || float.IsInfinity(metresPerTile) || metresPerTile <= 0f)
                throw new ArgumentOutOfRangeException(nameof(metresPerTile));
            float scale = 1f / metresPerTile;
            if (float.IsInfinity(scale)) throw new ArgumentOutOfRangeException(nameof(metresPerTile));
            return scale;
        }

        public static string TexturePath(string theme, string slot, string suffix)
        {
            if (theme != "Castle" && theme != "Hospital" && theme != "School" && theme != "Basement")
                throw new ArgumentException("Unknown texture theme.", nameof(theme));
            if (string.IsNullOrEmpty(slot) || !slot.StartsWith(theme.ToLowerInvariant() + "_", StringComparison.Ordinal) ||
                slot.Any(c => !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_'))
                throw new ArgumentException("Invalid texture slot.", nameof(slot));
            if (suffix != "Albedo" && suffix != "Normal" && suffix != "Smoothness")
                throw new ArgumentException("Unknown texture map.", nameof(suffix));
            return "Assets/Art/Textures/" + theme + "/" + slot + "_" + suffix + ".png";
        }

        public static bool ApplySlotTextures(Material material, string theme, string slot)
            => ApplySlotTextures(material, theme, slot, false);

        private static bool ApplySlotTextures(Material material, string theme, string slot, bool generated)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            string albedoPath = TexturePath(theme, slot, "Albedo");
            string normalPath = TexturePath(theme, slot, "Normal");
            string smoothnessPath = TexturePath(theme, slot, "Smoothness");
            string recipePath = "Assets/Art/Textures/" + theme + "/" + slot + ".json";
            // Validate the complete set before touching any material or importer.
            foreach (string path in new[] { albedoPath, normalPath, smoothnessPath, recipePath })
                if (!File.Exists(path))
                {
                    Debug.LogWarning("Missing kit texture; keeping existing material/flat colour: " + path);
                    return false;
                }
            var recipe = JsonUtility.FromJson<TextureRecipe>(File.ReadAllText(recipePath));
            if (recipe == null || !ColorUtility.TryParseHtmlString(recipe.paletteSrgb, out Color palette))
                throw new InvalidOperationException("Invalid kit texture recipe: " + recipePath);
            float scale = TextureScale(recipe.metresPerTile);
            bool adopted = material.GetTag(TextureTag, false, "") == "1";
            if (!generated && !adopted && !IsUntouchedFlatSlot(material, palette))
            {
                Debug.LogWarning("Preserving edited kit material; texture adoption not applied: " + slot);
                return false;
            }
            var albedo = ImportTexture(albedoPath, false, true);
            var normal = ImportTexture(normalPath, true, false);
            var smoothness = ImportTexture(smoothnessPath, false, false);
            if (adopted) return false; // Preserve artist edits to maps, tint, UVs and shader settings.
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_MetallicGlossMap", smoothness);
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
            {
                material.SetTextureScale(property, new Vector2(scale, scale));
                material.SetTextureOffset(property, Vector2.zero);
            }
            // Albedo already contains the palette. Multiplying by the old flat
            // colour would darken it twice. Unrelated emission/culling edits stay.
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_WorkflowMode", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SPECULAR_SETUP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.SetOverrideTag(TextureTag, "1");
            EditorUtility.SetDirty(material);
            if (AssetDatabase.Contains(material)) AssetDatabase.SaveAssetIfDirty(material);
            return true;
        }

        private static bool IsUntouchedFlatSlot(Material material, Color palette)
        {
            if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit") return false;
            if (!NearColor(material.color, palette) && !NearColor(material.color, palette.linear)) return false;
            if (Math.Abs(material.GetFloat("_Smoothness") - .15f) > .0001f ||
                material.GetFloat("_WorkflowMode") != 1f || material.GetFloat("_SmoothnessTextureChannel") != 0f ||
                material.GetFloat("_Metallic") != 0f || material.GetFloat("_BumpScale") != 1f) return false;
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
                if (material.GetTexture(property) != null || material.GetTextureScale(property) != Vector2.one ||
                    material.GetTextureOffset(property) != Vector2.zero) return false;
            return true;
        }

        private static bool NearColor(Color a, Color b)
            => Math.Abs(a.r - b.r) < .002f && Math.Abs(a.g - b.g) < .002f &&
               Math.Abs(a.b - b.b) < .002f && Math.Abs(a.a - b.a) < .002f;

        private static Texture2D ImportTexture(string path, bool normal, bool srgb)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null) throw new InvalidOperationException("Expected texture importer: " + path);
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool changed = importer.textureType != type || importer.sRGBTexture != srgb ||
                importer.wrapModeU != TextureWrapMode.Repeat || importer.wrapModeV != TextureWrapMode.Repeat ||
                !importer.mipmapEnabled || importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                importer.alphaIsTransparency || importer.convertToNormalmap || importer.flipGreenChannel;
            if (changed)
            {
                importer.textureType = type; importer.sRGBTexture = srgb;
                importer.wrapModeU = TextureWrapMode.Repeat; importer.wrapModeV = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false; importer.convertToNormalmap = false; importer.flipGreenChannel = false;
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Texture import failed: " + path);
            return texture;
        }

        private static void Folder(string path)
            => Worsen.Editor.Common.SetupKit.EnsureFolder(path);
    }
}
