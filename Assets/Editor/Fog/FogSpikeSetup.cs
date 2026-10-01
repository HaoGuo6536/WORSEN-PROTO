// ============================================================================
// FogSpikeSetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds renderer wiring and a synthetic four-by-four portal-connected floor.
//   The coordinator invokes these tools in Unity; no authored scene is needed here.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Install one fog feature without removing or replacing other renderer features.
//   - Reuse config identities and create only the dedicated additive spike scene.
// DEPENDENCIES:
//   - Common SetupKit creates asset folders while retaining existing identities.
//   - Core graph data, Presentation Fog, UnityEditor and URP renderer assets.
// USAGE NOTES:
//   Refuses Play Mode, duplicate fog features, dirty renderer assets and a loaded
//   spike scene. Unrelated scenes are neither saved nor closed. Does not change
//   Build Settings. The test camera explicitly selects PC_Renderer in the active
//   pipeline; a missing renderer is an error, not a silent default fallback.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Worsen.Core;
using Worsen.Presentation.Fog;

namespace Worsen.Editor.Fog
{
    public static class FogSpikeSetup
    {
        public const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        public const string ScenePath = "Assets/Scenes/Spikes/FogSpike.unity";
        public const string ConfigRoot = "Assets/Resources/ScriptableObjects/Presentation/Fog";
        public static FogDriverConfig Config => AssetDatabase.LoadAssetAtPath<FogDriverConfig>(ConfigRoot + "/FogDriverConfig.asset");
        public static FogSpikeProfile Profile => AssetDatabase.LoadAssetAtPath<FogSpikeProfile>(ConfigRoot + "/FogSpikeProfile.asset");

        [MenuItem("Worsen/Fog/1 - Install Renderer Feature")]
        public static void Install()
        {
            RequireEditMode();
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Fog/CollapseFog.shader");
            if (renderer == null || shader == null) throw new InvalidOperationException("Fog renderer or shader is missing.");
            if (EditorUtility.IsDirty(renderer)) throw new InvalidOperationException("Save or revert PC_Renderer edits before installing fog.");
            var features = renderer.rendererFeatures.OfType<FogRendererFeature>().ToArray();
            if (features.Length > 1) throw new InvalidOperationException("Multiple fog features exist; owner review is required.");
            EnsureFolder(ConfigRoot);
            EnsureAsset<FogDriverConfig>("FogDriverConfig");
            EnsureAsset<FogSpikeProfile>("FogSpikeProfile");
            var feature = features.Length == 1 ? features[0] : ScriptableObject.CreateInstance<FogRendererFeature>();
            if (features.Length == 0)
            {
                feature.name = "Worsen Collapse Fog";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            feature.Initialize(shader);
            feature.SetActive(true);
            // Persist the local IDs that URP uses to repair feature references on import.
            var rendererObject = new SerializedObject(renderer);
            var map = rendererObject.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                long id = 0;
                if (renderer.rendererFeatures[i] != null)
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string _, out id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            rendererObject.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty(); EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(feature); AssetDatabase.SaveAssetIfDirty(renderer);
            Debug.Log("Fog installed once. Existing renderer feature order and distance fog preserved.");
        }

        [MenuItem("Worsen/Fog/2 - Build Spike Scene")]
        public static void BuildScene()
        {
            RequireEditMode();
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) throw new InvalidOperationException("Close FogSpike before rebuilding it; unrelated scenes are preserved.");
            Install();
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("The active pipeline is not URP.");
            var renderers = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            int rendererIndex = -1;
            for (int i = 0; i < renderers.arraySize; i++)
                if (AssetDatabase.GetAssetPath(renderers.GetArrayElementAtIndex(i).objectReferenceValue) == RendererPath) rendererIndex = i;
            if (rendererIndex < 0) throw new InvalidOperationException("Active URP asset does not reference PC_Renderer.");
            FogSpikeProfile profile = Profile; FogDriverConfig config = Config;
            if (config.PortalWidth >= profile.RoomSize || config.PortalHeight >= profile.RoomHeight)
                throw new InvalidOperationException("Spike openings must fit inside the room.");
            EnsureFolder("Assets/Scenes/Spikes");
            Scene prior = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root = new GameObject("FogSpike");
                var manager = root.AddComponent<FogManager>();
                var managerObject = new SerializedObject(manager);
                managerObject.FindProperty("_config").objectReferenceValue = config;
                managerObject.FindProperty("_driver").objectReferenceValue = root.GetComponent<FogDriver>();
                managerObject.ApplyModifiedPropertiesWithoutUndo();
                var cameraObject = new GameObject("Spike Camera"); cameraObject.transform.SetParent(root.transform);
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.fieldOfView = profile.FieldOfView; camera.nearClipPlane = profile.CameraNearClip; camera.farClipPlane = profile.RoomSize * profile.CameraFarRoomMultiplier;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(rendererIndex);
                var path = new GameObject("Camera Path"); path.transform.SetParent(root.transform);
                CreateInputs(profile, out var rooms, out var graph);
                for (int i = 0; i < rooms.Count; i++)
                {
                    int row = i / 4, column = row % 2 == 0 ? i % 4 : 3 - i % 4;
                    var waypoint = new GameObject("Waypoint " + i.ToString("D2"));
                    waypoint.transform.SetParent(path.transform);
                    waypoint.transform.position = new Vector3(column * profile.RoomSize, profile.EyeHeight, row * profile.RoomSize);
                    Bounds b = rooms[i].Bounds;
                    Box(root.transform, "Floor " + i, new Vector3(b.center.x, -profile.WallThickness * .5f, b.center.z), new Vector3(profile.RoomSize, profile.WallThickness, profile.RoomSize));
                    Box(root.transform, "Ceiling " + i, new Vector3(b.center.x, profile.RoomHeight + profile.WallThickness * .5f, b.center.z), new Vector3(profile.RoomSize, profile.WallThickness, profile.RoomSize));
                    int x = i % 4, z = i / 4;
                    Wall(root.transform, b, 0, 1, x < 3, profile, config);
                    Wall(root.transform, b, 2, 1, z < 3, profile, config);
                    if (x == 0) Wall(root.transform, b, 0, -1, false, profile, config);
                    if (z == 0) Wall(root.transform, b, 2, -1, false, profile, config);
                    var lamp = new GameObject("Room Light " + i); lamp.transform.SetParent(root.transform);
                    lamp.transform.position = new Vector3(b.center.x, profile.RoomHeight - profile.LightCeilingOffset, b.center.z);
                    var light = lamp.AddComponent<Light>(); light.type = LightType.Point; light.intensity = profile.LightIntensity; light.range = profile.RoomSize; light.shadows = LightShadows.None;
                }
                cameraObject.transform.position = path.transform.GetChild(0).position;
                cameraObject.transform.LookAt(path.transform.GetChild(1).position);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save FogSpike.");
                Debug.Log("FogSpike created. Close other scenes, enter Play Mode, then use Worsen/Fog/3 - Animate and Probe.");
            }
            finally { if (prior.IsValid() && prior.isLoaded) SceneManager.SetActiveScene(prior); }
        }

        public static void CreateInputs(FogSpikeProfile profile, out List<GeneratedRoomSample> samples, out LevelGraph graph)
        {
            samples = new List<GeneratedRoomSample>(); var rooms = new List<LevelRoom>(); var edges = new List<LevelEdge>();
            float size = profile.RoomSize;
            for (int z = 0; z < 4; z++) for (int x = 0; x < 4; x++)
            {
                int id = z * 4 + x + 1;
                var room = new LevelRoom(id, new Vector3(x * size, profile.RoomHeight * .5f, z * size), new Vector3(size, profile.RoomHeight, size));
                rooms.Add(room); var portals = new List<Vector3>();
                if (x > 0) portals.Add(new Vector3((x - .5f) * size, 0, z * size));
                if (x < 3) { portals.Add(new Vector3((x + .5f) * size, 0, z * size)); edges.Add(new LevelEdge(edges.Count + 1, id, id + 1, true)); }
                if (z > 0) portals.Add(new Vector3(x * size, 0, (z - .5f) * size));
                if (z < 3) { portals.Add(new Vector3(x * size, 0, (z + .5f) * size)); edges.Add(new LevelEdge(edges.Count + 1, id, id + 4, true)); }
                samples.Add(new GeneratedRoomSample(id, room.Bounds, false, false, portals.ToArray()));
            }
            graph = LevelGraphUtility.Build(rooms, edges, Array.Empty<LevelAnchor>(), 16, rooms[15].Center);
        }
        private static void Wall(Transform root, Bounds room, int axis, int side, bool opening, FogSpikeProfile p, FogDriverConfig c)
        {
            Vector3 center = room.center; center[axis] += side * p.RoomSize * .5f;
            Vector3 size = new Vector3(p.RoomSize, p.RoomHeight, p.RoomSize); size[axis] = p.WallThickness;
            if (!opening) { Box(root, "Solid Wall", center, size); return; }
            int across = 2 - axis; float wing = (p.RoomSize - c.PortalWidth) * .5f;
            size[across] = wing;
            Vector3 left = center, right = center;
            left[across] -= (c.PortalWidth + wing) * .5f; right[across] += (c.PortalWidth + wing) * .5f;
            Box(root, "Door Frame", left, size); Box(root, "Door Frame", right, size);
            size[across] = c.PortalWidth; size.y = p.RoomHeight - c.PortalHeight;
            center.y = c.PortalHeight + size.y * .5f; Box(root, "Lintel", center, size);
        }
        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale)
        { var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(parent); box.transform.position = position; box.transform.localScale = scale; }
        private static void EnsureAsset<T>(string name) where T : ScriptableObject
        { string path = ConfigRoot + "/" + name + ".asset"; if (AssetDatabase.LoadAssetAtPath<T>(path) == null) AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path); }
        private static void EnsureFolder(string path)
            => Worsen.Editor.Common.SetupKit.EnsureFolder(path);
        private static void RequireEditMode()
        { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Fog setup requires Edit Mode."); }
    }
}
