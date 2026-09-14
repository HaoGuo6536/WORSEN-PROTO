// ============================================================================
// TagArenaSceneSetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds the M0 TagArena and all runtime wiring from versioned source assets.
//   The builder preserves designer config values and scene asset identities,
//   keeps unrelated scenes untouched, and supplies a repeatable clean-clone path.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Scenes deterministic setup.
// KEY RESPONSIBILITIES:
//   - Create missing mirrored config and UI panel assets, then wire services.
//   - Save TagArena, register it as the first build scene, and set a 60 Hz tick.
// DEPENDENCIES:
//   - All M0 runtime systems and UnityEditor asset/scene serialization APIs.
// USAGE NOTES:
//   - Editor-only. Refuses Play Mode and refuses to rebuild a dirty TagArena.
//   - Creates its scene additively and restores the prior active scene; other
//     scenes are neither saved nor closed. Rebuild replaces only TagArena.
//   - Existing config/panel assets are reused so their GUIDs and tunings survive.
//   - Unity 6 stores Fixed Timestep as RationalTime: one tick at 60 ticks/second.
// ============================================================================

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Worsen.Orchestrator;
using Worsen.Presentation.DebugOverlay;
using Worsen.Presentation.Input;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;

namespace Worsen.Editor.Scenes
{
    public static class TagArenaSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/TagArena.unity";
        private const string UiRoot = "Assets/Resources/UI/Presentation/DebugOverlay/";

        [MenuItem("Worsen/Scenes/1 — Build TagArena")]
        public static void BuildTagArena()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before rebuilding TagArena.");
            var previous = SceneManager.GetActiveScene();
            var oldArena = SceneManager.GetSceneByPath(ScenePath);
            if (oldArena.IsValid() && oldArena.isLoaded && oldArena.isDirty)
                throw new InvalidOperationException("Save your TagArena edits before explicitly rebuilding it.");

            var inputConfig = EnsureAsset<InputDriverConfig>("Assets/Resources/ScriptableObjects/Presentation/Input/InputDriverConfig.asset");
            var overlayConfig = EnsureAsset<DebugOverlayDriverConfig>("Assets/Resources/ScriptableObjects/Presentation/DebugOverlay/DebugOverlayDriverConfig.asset");
            var panel = EnsureAsset<PanelSettings>(UiRoot + "DebugOverlayPanelSettings.asset");
            panel.themeStyleSheet = RequireAsset<ThemeStyleSheet>(UiRoot + "DebugOverlayTheme.tss");
            EditorUtility.SetDirty(panel);
            var tree = RequireAsset<VisualTreeAsset>(UiRoot + "DebugOverlay.uxml");
            SetFixedTimestep();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                if (oldArena.IsValid() && oldArena.isLoaded)
                    EditorSceneManager.CloseScene(oldArena, true);
                BuildServices(inputConfig, overlayConfig, panel, tree);
                BuildStage();
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Unity could not save TagArena.");
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                    .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            Debug.Log("TagArena M0 rebuilt: persistent services, input, debug overlay, and fixed timestep 1/60. Player movement is M1.");
        }

        private static void BuildServices(InputDriverConfig inputConfig, DebugOverlayDriverConfig overlayConfig,
            PanelSettings panel, VisualTreeAsset tree)
        {
            var run = new GameObject("Run Session").AddComponent<RunSessionManager>();
            var flow = new GameObject("Scene Flow").AddComponent<SceneFlowManager>();
            var inputObject = new GameObject("Input Service");
            var inputDriver = inputObject.AddComponent<PlayerInputDriver>();
            var input = inputObject.AddComponent<InputManager>();
            Wire(inputDriver, "_config", inputConfig);
            Wire(input, "_driver", inputDriver);
            var inputRoute = inputObject.AddComponent<InputOrchestrator>();
            Wire(inputRoute, "_input", input);
            Wire(inputRoute, "_run", run);
            Wire(inputRoute, "_sceneFlow", flow);

            var overlayObject = new GameObject("Debug Overlay");
            var document = overlayObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = tree;
            var overlayDriver = overlayObject.AddComponent<DebugOverlayDriver>();
            Wire(overlayDriver, "_document", document);
            Wire(overlayDriver, "_visualTree", tree);
            Wire(overlayDriver, "_panelSettings", panel);
            var overlay = overlayObject.AddComponent<DebugOverlayManager>();
            Wire(overlay, "_config", overlayConfig);
            Wire(overlay, "_driver", overlayDriver);
            var overlayRoute = overlayObject.AddComponent<DebugOverlayOrchestrator>();
            Wire(overlayRoute, "_overlay", overlay);
            Wire(overlayRoute, "_run", run);

            var root = new GameObject("TagArena Scene Root").AddComponent<TagArenaSceneRoot>();
            Wire(root, "_run", run);
            Wire(root, "_sceneFlow", flow);
            Wire(root, "_input", input);
            Wire(root, "_overlay", overlay);
        }

        private static void BuildStage()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "M0 Test Floor";
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(new Vector3(0f, 5f, -9f), Quaternion.Euler(25f, 0f, 0f));
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required source asset missing: " + path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }

        private static void Wire(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException(target.GetType().Name + " has no serialized field " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFixedTimestep()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
            var fixedStep = settings.FindProperty("Fixed Timestep");
            if (fixedStep == null) throw new InvalidOperationException("Unity's Fixed Timestep setting could not be resolved.");
            fixedStep.FindPropertyRelative("m_Count").longValue = 1;
            fixedStep.FindPropertyRelative("m_Rate.m_Numerator").intValue = 60;
            fixedStep.FindPropertyRelative("m_Rate.m_Denominator").intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
