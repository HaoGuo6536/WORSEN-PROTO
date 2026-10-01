// ============================================================================
// HorrorWorldAssetSetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds explicit medieval dressing, Lumen 2 lighting and spectral collapse
//   asset wiring for the HorrorRun scene builder. Imported sources remain intact;
//   project-owned material, texture and prefab identities survive repeated setup.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10) - Editor - Horror deterministic asset setup.
// KEY RESPONSIBILITIES:
//   - Validate every required source before creating or saving owned assets.
//   - Wire physical doors, URP castle surface copies and animated grasp hands.
//   - Build Environment/Lumen configuration and serialize required world shader references.
//   - Import and map the complete weathered exit assembly without changing its hinge or axes.
// DEPENDENCIES:
//   - Common SetupKit creates asset folders while retaining existing identities.
//   - Domain Floor/Procedural configs and Presentation Environment config schemas.
//   - UnityEditor asset/prefab APIs; original exit art, imported castle and embedded Lumen 2 assets.
// USAGE NOTES:
//   Editor-only. Caller owns the exclusive Unity lease and scene setup. This helper
//   never edits scenes, renderer/project settings, vendor assets or unrelated dirties.
//   The hand FBX must be published with its exact Grasp blend shape before execution.
//   WireConfig is the argument-free integration entry; existing designer tuning and
//   assigned Lumen references are preserved. Exit materials retain the portal shader.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Worsen.Domain.Floor;
using Worsen.Domain.Procedural;
using Worsen.Presentation.Environment;
using Object = UnityEngine.Object;

namespace Worsen.Editor.Horror
{
    public static class HorrorWorldAssetSetup
    {
        private const string Castle = "Assets/External/Environment/The_Modular_Medieval_Castle/";
        private const string DoorPath = "Assets/Art/Exit/WeatheredDoor/WORSEN_WeatheredExitDoor.fbx";
        private const string ExitPaintPath = "Assets/Art/Exit/WeatheredDoor/ExitPaint.png";
        private const string ExitMaterials = "Assets/Art/Exit/WeatheredDoor/Materials/";
        private const string DoorLeafPath = "Assets/Prefabs/Horror/Environment/ExitDoorLeaf.prefab";
        private const string HorrorFloorPath = "Assets/Resources/ScriptableObjects/Domain/Floor/HorrorFloorDriverConfig.asset";
        private const string ProceduralPath = "Assets/Resources/ScriptableObjects/Domain/Procedural/ProceduralDriverConfig.asset";
        private const string WallPath = Castle + "Materials/House/MC_Castle_Wall_A.mat";
        private const string FloorPath = Castle + "Materials/House/MC_Castle_Stone_Floor.mat";
        private const string CeilingPath = Castle + "Materials/House/MC_Floor_Board.mat";
        private const string TorchPath = Castle + "Prefabs/Props/MC_Torch_Wall.prefab";
        private const string FirePath = Castle + "VFX/MC_Fire_Torch.prefab";
        private const string ArchPath = Castle + "Prefabs/Castle/MC_Wood_Arch_7m.prefab";
        private const string ColumnPath = Castle + "Prefabs/House/MC_Column_01.prefab";
        private const string ChestPath = Castle + "Prefabs/Environment/MC_Chest_01.prefab";
        private const string LumenPath = "Packages/com.distantlands.lumen/Content/Prefabs/Lantern Effect.prefab";
        private const string HandModelPath = "Assets/Art/Horror/Collapse/WORSEN_SpectralHand_LOD.fbx";
        private const string HandPrefabPath = "Assets/Prefabs/Horror/Collapse/SpectralHand.prefab";
        private const string Materials = "Assets/Art/Horror/Collapse/Materials/";
        private const string EnvironmentPath = "Assets/Resources/ScriptableObjects/Presentation/Environment/EnvironmentDriverConfig.asset";
        private static readonly string[] BannerPaths = {
            Castle + "Prefabs/Environment/MC_Banner_01.prefab",
            Castle + "Prefabs/Environment/MC_Banner_03.prefab",
            Castle + "Prefabs/Environment/MC_Banner_05.prefab"
        };
        private static readonly string[] PropPaths = {
            Castle + "Prefabs/Environment/MC_Wooden_Barrel_01.prefab",
            Castle + "Prefabs/Environment/MC_Armor_Stand.prefab", ChestPath
        };

        [MenuItem("Worsen/Horror/Wire world asset references")]
        public static void WireConfig()
        {
            RequireIdle();
            Configure(RequireAsset<FloorDriverConfig>(HorrorFloorPath), RequireAsset<ProceduralDriverConfig>(ProceduralPath));
        }

        public static void Configure(FloorDriverConfig floor, ProceduralDriverConfig procedural)
        {
            RequireIdle();
            RequirePersistent(floor, "Assets/Resources/ScriptableObjects/Domain/Floor/");
            RequirePersistent(procedural, "Assets/Resources/ScriptableObjects/Domain/Procedural/");
            Worsen.Editor.Floor.FloorConfigGenerator.ConfigureShaders(floor);
            Worsen.Editor.Procedural.ProceduralShaderSetup.Configure(procedural);
            var floorData = new SerializedObject(floor);
            var proceduralData = new SerializedObject(procedural);
            foreach (string field in new[] { "_usePhysicalExitDoor", "_exitDoorPrefab", "_exitDoorPrefabYaw", "_exitDoorMaterial",
                "_exitDoorOpeningDuration", "_exitDoorOpeningAngle", "_handPrefab", "_crackMaterial", "_mistMaterial",
                "_handVisualScale", "_handGridWidth", "_portalInset", "_warningColor", "_closedColor", "_warningIntensity" }) Property(floorData, field);
            foreach (string field in new[] { "_wallMaterial", "_floorMaterial", "_ceilingMaterial" }) Property(proceduralData, field);
            GameObject door = BuildExitLeaf();
            Material wall = HorrorMaterialSetup.BuildMaterial(RequireAsset<Material>(WallPath));
            Material stone = HorrorMaterialSetup.BuildMaterial(RequireAsset<Material>(FloorPath));
            Material ceiling = HorrorMaterialSetup.BuildMaterial(RequireAsset<Material>(CeilingPath));
            GameObject sourceHand = RequireHandModel();
            Shader lit = RequireShader("Universal Render Pipeline/Lit");
            Shader unlit = RequireShader("Universal Render Pipeline/Unlit");
            Shader particles = RequireShader("Universal Render Pipeline/Particles/Unlit");

            Material handMaterial = BuildMaterial(Materials + "SpectralHand.mat", lit, new Color(0.023f, 0.013f, 0.045f, 1f), false);
            handMaterial.SetFloat("_Smoothness", 0.12f);
            handMaterial.SetColor("_EmissionColor", new Color(0.018f, 0.009f, 0.045f));
            handMaterial.EnableKeyword("_EMISSION"); Save(handMaterial);
            Material crackMaterial = BuildMaterial(Materials + "RoomFissures.mat", unlit, new Color(0.008f, 0.003f, 0.021f, 1f), false);
            Material mistMaterial = BuildMaterial(Materials + "ConsumingMist.mat", particles, new Color(0.02f, 0.01f, 0.043f, 0.84f), true);
            mistMaterial.SetTexture("_BaseMap", BuildMistFalloff()); Save(mistMaterial);
            GameObject hand = BuildHandPrefab(sourceHand, handMaterial);

            Property(floorData, "_usePhysicalExitDoor").boolValue = true;
            if (floor.LumenRoomWarningPrefab == null)
                Property(floorData, "_lumenRoomWarningPrefab").objectReferenceValue = HorrorLumenStyleSetup.EnsureRoomWarning();
            if (floor.LumenExitGlowPrefab == null)
                Property(floorData, "_lumenExitGlowPrefab").objectReferenceValue = HorrorLumenStyleSetup.EnsureExitGlow();
            Property(floorData, "_exitDoorPrefab").objectReferenceValue = door;
            Property(floorData, "_exitDoorPrefabYaw").floatValue = 0f;
            if (floor.ExitDoorMaterial == null) Property(floorData, "_exitDoorMaterial").objectReferenceValue = ceiling;
            if (floor.HandPrefab == null) Property(floorData, "_handPrefab").objectReferenceValue = hand;
            if (floor.CrackMaterial == null) Property(floorData, "_crackMaterial").objectReferenceValue = crackMaterial;
            if (floor.MistMaterial == null) Property(floorData, "_mistMaterial").objectReferenceValue = mistMaterial;
            // Door opening, crossing, collapse and color tunings belong to the designer.
            // In particular, do not replace the existing exit glow color/brightness policy.
            if (Property(proceduralData, "_wallMaterial").objectReferenceValue == null)
                Property(proceduralData, "_wallMaterial").objectReferenceValue = wall;
            if (Property(proceduralData, "_floorMaterial").objectReferenceValue == null)
                Property(proceduralData, "_floorMaterial").objectReferenceValue = stone;
            if (Property(proceduralData, "_ceilingMaterial").objectReferenceValue == null)
                Property(proceduralData, "_ceilingMaterial").objectReferenceValue = ceiling;
            floorData.ApplyModifiedPropertiesWithoutUndo(); proceduralData.ApplyModifiedPropertiesWithoutUndo();
            Save(floor); Save(procedural);
        }

        public static EnvironmentDriverConfig BuildEnvironmentConfig()
        {
            RequireIdle();
            GameObject torch = HorrorMaterialSetup.BuildPrefab(RequireAsset<GameObject>(TorchPath));
            GameObject fire = HorrorMaterialSetup.BuildPrefab(RequireAsset<GameObject>(FirePath));
            GameObject arch = HorrorMaterialSetup.BuildPrefab(RequireAsset<GameObject>(ArchPath));
            GameObject column = HorrorMaterialSetup.BuildPrefab(RequireAsset<GameObject>(ColumnPath));
            GameObject chest = HorrorMaterialSetup.BuildPrefab(RequireAsset<GameObject>(ChestPath));
            GameObject importedLumen = RequireAsset<GameObject>(LumenPath);
            GameObject lumen = HorrorLumenStyleSetup.EnsureSoftTorch(importedLumen);
            GameObject moon = HorrorLumenStyleSetup.EnsureMoon(importedLumen);
            var banners = LoadPrefabs(BannerPaths); var props = LoadPrefabs(PropPaths);
            bool lumenPlayer = false;
            foreach (Component component in lumen.GetComponentsInChildren<Component>(true))
                if (component != null && component.GetType().FullName == "DistantLands.Lumen.LumenEffectPlayer") lumenPlayer = true;
            if (!lumenPlayer) throw new InvalidOperationException("Lumen 2 Lantern Effect has no LumenEffectPlayer: " + LumenPath);
            EnvironmentDriverConfig config = LoadOwned<EnvironmentDriverConfig>(EnvironmentPath);
            if (config == null)
            { EnsureParent(EnvironmentPath); config = ScriptableObject.CreateInstance<EnvironmentDriverConfig>(); AssetDatabase.CreateAsset(config, EnvironmentPath); }
            var data = new SerializedObject(config);
            if (config.ChalkShader == null) Property(data, "_chalkShader").objectReferenceValue = RequireShader("Universal Render Pipeline/Particles/Unlit");
            if (config.PanelShader == null) Property(data, "_panelShader").objectReferenceValue = RequireShader("Universal Render Pipeline/Unlit");
            Property(data, "_wallTorchPrefab").objectReferenceValue = torch;
            Property(data, "_firePrefab").objectReferenceValue = fire;
            SetArray(data, "_wallDecorationPrefabs", banners); SetArray(data, "_floorPropPrefabs", props);
            Property(data, "_doorArchPrefab").objectReferenceValue = arch;
            Property(data, "_cornerColumnPrefab").objectReferenceValue = column;
            Property(data, "_merchantDisplayPrefab").objectReferenceValue = chest;
            Property(data, "_lumenLanternPrefab").objectReferenceValue = lumen;
            Property(data, "_lumenMoonPrefab").objectReferenceValue = moon;
            Property(data, "_lumenRangeMultiplier").floatValue = 1f;
            Property(data, "_lumenBrightness").floatValue = 0.95f;
            Property(data, "_lumenFlareScale").floatValue = 0.7f;
            Property(data, "_lightTemplate").objectReferenceValue = null;
            Property(data, "_warmColor").colorValue = new Color(1f, 0.57f, 0.25f);
            Property(data, "_moonColor").colorValue = new Color(0.34f, 0.52f, 0.8f);
            Property(data, "_maximumLumenEffects").intValue = 12;
            Property(data, "_maximumRealtimeLights").intValue = 0;
            Property(data, "_maximumShadowLights").intValue = 0;
            Property(data, "_effectDistance").floatValue = 60f;
            Property(data, "_refreshInterval").floatValue = 0.1f;
            data.ApplyModifiedPropertiesWithoutUndo();
            Save(config); return config;
        }

        private static GameObject BuildExitLeaf()
        {
            RequireIdle();
            var importer = AssetImporter.GetAtPath(DoorPath) as ModelImporter
                ?? throw new InvalidOperationException("Weathered exit requires a ModelImporter: " + DoorPath);
            if (importer.bakeAxisConversion || importer.globalScale != 1f || !importer.useFileScale ||
                importer.addCollider || importer.importAnimation || importer.animationType != ModelImporterAnimationType.None ||
                !importer.preserveHierarchy || importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
            {
                importer.bakeAxisConversion = false;
                importer.globalScale = 1f; importer.useFileScale = true;
                importer.addCollider = false; importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.preserveHierarchy = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
            }
            // Reacquire after import; normalizing individual parts would destroy the hinge.
            GameObject imported = RequireAsset<GameObject>(DoorPath);
            foreach (string name in new[] { "DoorFrame", "DoorLeaf", "Threshold", "EscapeSurface" })
            {
                int count = 0;
                foreach (Transform part in imported.GetComponentsInChildren<Transform>(true))
                    if (part.name == name && part.GetComponent<Renderer>() != null) count++;
                if (count != 1) throw new InvalidOperationException("Weathered exit requires exactly one rendered " + name);
            }
            Texture2D paint = RequireAsset<Texture2D>(ExitPaintPath);
            RequireShader("Worsen/ExitPortal"); RequireShader("Universal Render Pipeline/Lit");
            foreach (Renderer renderer in imported.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    ExitMaterialShader(material != null ? material.name : null);
            LoadOwned<GameObject>(DoorLeafPath);
            EnsureParent(DoorLeafPath);
            GameObject leaf = Object.Instantiate(imported);
            try
            {
                leaf.name = "Weathered Exit Door";
                leaf.transform.SetParent(null, false);
                leaf.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                leaf.transform.localScale = Vector3.one;
                foreach (Collider collider in leaf.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (Light light in leaf.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);
                foreach (Renderer renderer in leaf.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = BuildExitMaterial(materials[i], paint);
                    renderer.sharedMaterials = materials;
                    if (renderer.name == "EscapeSurface")
                    { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.enabled = false; }
                }
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(leaf, DoorLeafPath, out bool success);
                if (!success || saved == null) throw new InvalidOperationException("Weathered exit assembly save failed.");
                return saved;
            }
            finally { Object.DestroyImmediate(leaf); }
        }

        private static string ExitMaterialShader(string slot)
        {
            switch (slot)
            {
                case "Exit_EscapeSurface": return "Worsen/ExitPortal";
                case "Exit_PatinatedPaint":
                case "Exit_WornEdges":
                case "Exit_OldBrass":
                case "Exit_ThresholdStone": return "Universal Render Pipeline/Lit";
                default: throw new InvalidOperationException("Unknown or missing weathered exit material slot: " + slot);
            }
        }

        private static Material BuildExitMaterial(Material source, Texture2D paint)
        {
            Shader shader = RequireShader(ExitMaterialShader(source.name));
            string path = ExitMaterials + source.name + ".mat";
            Material material = LoadOwned<Material>(path);
            if (material == null)
            { EnsureParent(path); material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.name = source.name; material.enableInstancing = true;
            if (source.name != "Exit_EscapeSurface")
            {
                // Reset Lit state, but retain the persistent identity. The portal keeps
                // its authored sky properties rather than overwriting designer tuning.
                var template = new Material(shader);
                try { material.CopyPropertiesFromMaterial(template); }
                finally { Object.DestroyImmediate(template); }
                bool painted = source.name == "Exit_PatinatedPaint";
                Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
                    source.HasProperty("_Color") ? source.GetColor("_Color") :
                    throw new InvalidOperationException("Imported exit material has no exported color: " + source.name);
                material.SetColor("_BaseColor", painted ? Color.white : color);
                material.SetTexture("_BaseMap", painted ? paint : null);
                material.SetFloat("_Smoothness", 0.22f);
                material.SetFloat("_Metallic", source.name == "Exit_OldBrass" ? 0.65f : 0f);
                material.enableInstancing = true;
            }
            Save(material); return material;
        }

        private static GameObject RequireHandModel()
        {
            GameObject model = RequireAsset<GameObject>(HandModelPath);
            bool grasp = false;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh != null && renderer.sharedMesh.GetBlendShapeIndex("Grasp") >= 0) grasp = true;
            if (!grasp) throw new InvalidOperationException("Published spectral-hand FBX requires a SkinnedMeshRenderer with exact Grasp blend shape. Enable blend-shape import and reimport " + HandModelPath);
            return model;
        }

        private static GameObject BuildHandPrefab(GameObject source, Material material)
        {
            GameObject existing = LoadOwned<GameObject>(HandPrefabPath);
            EnsureParent(HandPrefabPath);
            GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(HandPrefabPath) : new GameObject("SpectralHand");
            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
                Transform child = root.transform.Find("Hand Visual");
                if (child == null)
                { var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform); visual.name = "Hand Visual"; child = visual.transform; }
                else
                {
                    Object imported = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                    if (imported == null || AssetDatabase.GetAssetPath(imported) != HandModelPath)
                        throw new InvalidOperationException("Existing Hand Visual belongs to a different source; refusing to replace its identity.");
                }
                child.localPosition = Vector3.zero; child.localRotation = Quaternion.identity; child.localScale = Vector3.one * 0.5f;
                SkinnedMeshRenderer[] skins = child.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skins.Length == 0) throw new InvalidOperationException("Spectral hand lost its imported skin renderer.");
                Bounds bounds = skins[0].bounds;
                foreach (SkinnedMeshRenderer skin in skins)
                {
                    if (skin.sharedMesh == null || skin.sharedMesh.GetBlendShapeIndex("Grasp") < 0)
                        throw new InvalidOperationException("Every spectral hand skin must preserve its Grasp shape.");
                    var materials = new Material[Mathf.Max(1, skin.sharedMesh.subMeshCount)];
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    skin.sharedMaterials = materials; skin.shadowCastingMode = ShadowCastingMode.Off; skin.receiveShadows = true;
                    skin.SetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("Grasp"), 0f); bounds.Encapsulate(skin.bounds);
                }
                child.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, HandPrefabPath, out bool success);
                if (!success || saved == null) throw new InvalidOperationException("Spectral hand prefab save failed: " + HandPrefabPath);
                return saved;
            }
            finally
            { if (existing != null) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        }

        private static Material BuildMaterial(string path, Shader shader, Color color, bool transparent)
        {
            Material material = LoadOwned<Material>(path);
            if (material == null) { EnsureParent(path); material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.enableInstancing = true;
            material.SetColor("_BaseColor", color); material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_AlphaClip", 0f); material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", (int)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetInt("_DstBlend", (int)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            if (material.HasProperty("_SrcBlendAlpha")) material.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha")) material.SetInt("_DstBlendAlpha", (int)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetInt("_ZWrite", transparent ? 0 : 1); material.SetInt("_Cull", (int)(transparent ? CullMode.Off : CullMode.Back));
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            material.SetShaderPassEnabled("ShadowCaster", !transparent);
            if (transparent) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
            Save(material); return material;
        }

        private static Texture2D BuildMistFalloff()
        {
            const string path = Materials + "MistFalloff.asset";
            Texture2D texture = LoadOwned<Texture2D>(path);
            if (texture == null)
            { EnsureParent(path); texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true) { name = "MistFalloff" }; AssetDatabase.CreateAsset(texture, path); }
            if (texture.width != 64 || texture.height != 64) texture.Reinitialize(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            { float falloff = Mathf.Clamp01(1f - new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude); pixels[y * 64 + x] = new Color(1f, 1f, 1f, falloff * falloff); }
            texture.SetPixels(pixels); texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            texture.Apply(false, false); Save(texture); return texture;
        }

        private static GameObject[] LoadPrefabs(string[] paths)
        { var items = new GameObject[paths.Length]; for (int i = 0; i < paths.Length; i++) items[i] = HorrorMaterialSetup.BuildPrefab(RequireAsset<GameObject>(paths[i])); return items; }
        private static void SetArray(SerializedObject data, string field, GameObject[] values)
        { SerializedProperty array = Property(data, field); array.arraySize = values.Length; for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
        private static SerializedProperty Property(SerializedObject data, string field)
            => Worsen.Editor.Common.SetupKit.RequireProperty(data, field);
        private static T RequireAsset<T>(string path) where T : Object
            => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Required imported " + typeof(T).Name + " is unavailable: " + path);
        private static T LoadOwned<T>(string path) where T : Object
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null && !(asset is T)) throw new InvalidOperationException("Refusing to replace an owned path containing another asset type: " + path);
            return asset as T;
        }
        private static Shader RequireShader(string name)
            => Shader.Find(name) ?? throw new InvalidOperationException("Required URP shader unavailable: " + name);
        private static void RequirePersistent(Object asset, string folder)
        {
            if (asset == null || !AssetDatabase.GetAssetPath(asset).StartsWith(folder, StringComparison.Ordinal))
                throw new InvalidOperationException("World setup requires the project-owned config under " + folder);
        }
        private static void EnsureParent(string path)
            => Worsen.Editor.Common.SetupKit.EnsureParent(path);
        private static void Save(Object asset) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("World asset setup requires an idle editor and the coordinator's exclusive Unity lease.");
        }
    }
}
