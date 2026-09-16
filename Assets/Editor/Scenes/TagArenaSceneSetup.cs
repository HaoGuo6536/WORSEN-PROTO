// ============================================================================
// TagArenaSceneSetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds the movement TagArena and its runtime wiring from versioned source.
//   The builder preserves designer config values and scene asset identities,
//   keeps unrelated scenes untouched, and supplies a repeatable clean-clone path.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Scenes deterministic setup.
// KEY RESPONSIBILITIES:
//   - Create missing mirrored config and UI panel assets, then wire services.
//   - Save TagArena, register it as the first build scene, and set a 60 Hz tick.
//   - Reject editor play entry when the arena capture fingerprint is stale.
// DEPENDENCIES:
//   - Player/Level builders, Camera/PostFX/Telemetry generators and Session routing.
//   - UnityEditor asset and scene APIs; installed Unity AI Navigation package.
// USAGE NOTES:
//   - Editor-only. Refuses Play Mode and refuses to rebuild a dirty TagArena.
//   - Creates its scene additively and restores the prior active scene; other
//     scenes are neither saved nor closed. Rebuild replaces only TagArena.
//   - Existing config/panel assets are reused so their GUIDs and tunings survive.
//   - Unity 6 stores Fixed Timestep as RationalTime: one tick at 60 ticks/second.
// ============================================================================

using System;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Rendering.Universal;
using Worsen.Orchestrator;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using Worsen.Presentation.Telemetry;
using Worsen.Editor.Player;
using Worsen.Editor.Level;
using Worsen.Editor.Camera;
using Worsen.Editor.PostFX;
using Worsen.Editor.Telemetry;
using Worsen.Editor.Hunter;
using Worsen.Editor.Chase;
using Worsen.Editor.Audio;
using Worsen.Editor.HUD;
using Worsen.Editor.Results;
using Worsen.Domain.Hunter;
using Worsen.Domain.Chase;
using Worsen.Presentation.DebugOverlay;
using Worsen.Presentation.Input;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;

namespace Worsen.Editor.Scenes
{
    [InitializeOnLoad]
    public static class TagArenaSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/TagArena.unity";
        private const string UiRoot = "Assets/Resources/UI/Presentation/DebugOverlay/";

        static TagArenaSceneSetup()
        {
            EditorApplication.playModeStateChanged -= ValidateCaptureBeforePlay;
            EditorApplication.playModeStateChanged += ValidateCaptureBeforePlay;
            AssemblyReloadEvents.beforeAssemblyReload -= UnbindEditorEvents;
            AssemblyReloadEvents.beforeAssemblyReload += UnbindEditorEvents;
        }

        private static void UnbindEditorEvents()
        {
            EditorApplication.playModeStateChanged -= ValidateCaptureBeforePlay;
            AssemblyReloadEvents.beforeAssemblyReload -= UnbindEditorEvents;
        }

        private static void ValidateCaptureBeforePlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            var arena = SceneManager.GetSceneByPath(ScenePath);
            bool arenaLoaded = arena.IsValid() && arena.isLoaded;
            var overrideScene = EditorSceneManager.playModeStartScene;
            bool startsArena = overrideScene != null && AssetDatabase.GetAssetPath(overrideScene) == ScenePath;
            if (overrideScene != null && !startsArena) return;
            if (!arenaLoaded && !startsArena) return;
            try
            {
                if (!arenaLoaded) throw new InvalidOperationException("Open TagArena to validate the play-start scene.");
                var roots = arena.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<TagArenaSceneRoot>(true)).ToArray();
                if (roots.Length != 1 || !roots[0].isActiveAndEnabled)
                    throw new InvalidOperationException("TagArena requires exactly one active, enabled scene root.");
                foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Resources/ScriptableObjects" }))
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                        if (EditorUtility.IsDirty(asset))
                            throw new InvalidOperationException("Save the edited configuration assets before rebuilding.");
                var root = new SerializedObject(roots[0]);
                if (root.FindProperty("_sourceRevision").stringValue != HashFiles("Assets/Scripts", "*.cs") ||
                    root.FindProperty("_configSnapshotHash").stringValue != HashFiles("Assets/Resources/ScriptableObjects", "*.asset"))
                    throw new InvalidOperationException("Source or configuration differs from the recorded build snapshot.");
            }
            catch (Exception exception)
            {
                EditorApplication.isPlaying = false;
                Debug.LogError("TagArena capture provenance is not ready. " + exception.Message +
                    " Run Worsen/Scenes/1 — Build TagArena after saving intended edits. This check never saves assets automatically.");
            }
        }

        [MenuItem("Worsen/Scenes/1 — Build TagArena")]
        public static void BuildTagArena()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before rebuilding TagArena.");
            var previous = SceneManager.GetActiveScene();
            var oldArena = SceneManager.GetSceneByPath(ScenePath);
            bool oldArenaWasLoaded = oldArena.IsValid() && oldArena.isLoaded;
            bool previousWasArena = oldArenaWasLoaded && previous == oldArena;
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
            bool oldArenaClosed = false;
            bool sceneSaved = false;
            try
            {
                SceneManager.SetActiveScene(scene);
                EnsureHunterGateLayer();
                var root = BuildServices(inputConfig, overlayConfig, panel, tree);
                BuildStage(root);
                AssetDatabase.SaveAssetIfDirty(panel);
                var provenance = new SerializedObject(root);
                provenance.FindProperty("_sourceRevision").stringValue = HashFiles("Assets/Scripts", "*.cs");
                provenance.FindProperty("_configSnapshotHash").stringValue = HashFiles("Assets/Resources/ScriptableObjects", "*.asset");
                provenance.ApplyModifiedPropertiesWithoutUndo();
                if (oldArenaWasLoaded)
                {
                    if (!EditorSceneManager.CloseScene(oldArena, true))
                        throw new InvalidOperationException("Unity could not close the saved TagArena for replacement.");
                    oldArenaClosed = true;
                }
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Unity could not save TagArena.");
                sceneSaved = true;
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                    .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            }
            finally
            {
                if (!sceneSaved && oldArenaClosed)
                {
                    var restored = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                    if (previousWasArena) previous = restored;
                }
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                    if (!sceneSaved || !oldArenaWasLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
            Debug.Log("TagArena movement arena rebuilt with Level, Player, first-person view and recording at 60 Hz. Live acceptance remains a separate check.");
        }

        private static TagArenaSceneRoot BuildServices(InputDriverConfig inputConfig, DebugOverlayDriverConfig overlayConfig,
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
            var telemetry = TelemetrySetup.CreateService();
            var telemetryRoute = telemetry.gameObject.AddComponent<TelemetryOrchestrator>();
            Wire(telemetryRoute, "_telemetry", telemetry);
            Wire(telemetryRoute, "_run", run);
            Wire(root, "_telemetry", telemetry);
            var audio = AudioSetup.Create();
            var audioRoute = audio.gameObject.AddComponent<AudioOrchestrator>();
            Wire(audioRoute, "_audio", audio);
            Wire(audioRoute, "_run", run);
            Wire(root, "_audio", audio);
            var hud = HUDSetup.Create(root.transform);
            var hudRoute = hud.gameObject.AddComponent<HUDOrchestrator>();
            Wire(hudRoute, "_hud", hud);
            Wire(hudRoute, "_run", run);
            Wire(root, "_hud", hud);
            var results = ResultsSetup.Create(root.transform);
            var resultsRoute = results.gameObject.AddComponent<ResultsOrchestrator>();
            Wire(resultsRoute, "_results", results);
            Wire(resultsRoute, "_run", run);
            Wire(resultsRoute, "_sceneFlow", flow);
            Wire(root, "_results", results);
            return root;
        }

        private static void BuildStage(TagArenaSceneRoot root)
        {
            var content = new GameObject("TagArena Level");
            var level = TagArenaLevelSetup.Build(content.transform);
            TagArenaLevelSetup.BuildNavigation(level);
            var profile = PlayerPrefabGenerator.EnsureAssets();
            var factory = new GameObject("Player Factory").AddComponent<PlayerFactory>();
            Wire(root, "_level", level);
            Wire(root, "_playerFactory", factory);
            Wire(root, "_playerProfile", profile);
            var hunterProfile = HunterPrefabGenerator.EnsureAssets();
            var hunterFactory = new GameObject("Hunter Factory").AddComponent<HunterFactory>();
            var chase = new GameObject("Chase Service").AddComponent<ChaseManager>();
            Wire(root, "_hunterFactory", hunterFactory);
            Wire(root, "_hunterProfile", hunterProfile);
            Wire(root, "_chase", chase);
            Wire(root, "_chaseConfig", ChaseConfigGenerator.EnsureAssets());
            var serialized = new SerializedObject(root);
            serialized.FindProperty("_spawnPosition").vector3Value = TagArenaLevelSetup.SpawnPosition;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var camera = new GameObject("Main Camera").AddComponent<UnityEngine.Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(TagArenaLevelSetup.SpawnPosition + Vector3.up * 1.65f, Quaternion.Euler(0f, 90f, 0f));
            camera.gameObject.AddComponent<AudioListener>();
            var cameraManager = CameraRigSetup.Create(root.transform, camera);
            var postFX = PostFXSetup.Create(root.transform);
            var rendering = camera.GetComponent<UniversalAdditionalCameraData>();
            if (rendering == null) rendering = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            rendering.renderPostProcessing = true;
            rendering.volumeLayerMask = 1 << postFX.gameObject.layer;
            Wire(root, "_camera", cameraManager);
            Wire(root, "_postFX", postFX);
            var rootFields = new SerializedObject(root);
            var run = rootFields.FindProperty("_run").objectReferenceValue;
            var cameraRoute = cameraManager.gameObject.AddComponent<CameraOrchestrator>();
            Wire(cameraRoute, "_camera", cameraManager);
            Wire(cameraRoute, "_run", run);
            var postRoute = postFX.gameObject.AddComponent<PostFXOrchestrator>();
            Wire(postRoute, "_postFX", postFX);
            Wire(postRoute, "_run", run);
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void EnsureHunterGateLayer()
        {
            if (LayerMask.NameToLayer("HunterRouteGate") >= 0) return;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = "HunterRouteGate";
                settings.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
            throw new InvalidOperationException("No free custom layer for HunterRouteGate.");
        }

        private static string HashFiles(string directory, string pattern)
        {
            var text = new StringBuilder();
            foreach (string path in Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                text.Append(path.Replace('\\', '/')).Append(Environment.NewLine).Append(File.ReadAllText(path)).Append(Environment.NewLine);
            using (var hash = SHA256.Create())
                return "sha256:" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", string.Empty);
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
