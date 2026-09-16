// ============================================================================
// HorrorRuntimeInspectionTools.cs
// ============================================================================
// PURPOSE:
//   Records the live scene's rendering, collision, lighting and audio state when
//   bridge expressions cannot return a useful diagnostic result. A timestamped
//   JSON snapshot preserves material identities and object paths for investigating
//   invisible geometry or unsupported shaders without changing the scene.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Horror read-only runtime diagnostics.
// KEY RESPONSIBILITIES:
//   - Record every active scene renderer and deduplicate its shared materials.
//   - Capture collider bounds, cameras, lights, audio and loaded object counts.
//   - Preserve shader diagnostic failures explicitly instead of hiding them.
//   - Sample delivered editor frames for a bounded window without changing FPS.
// DEPENDENCIES:
//   UnityEngine scene/rendering APIs, UnityEditor AssetDatabase/ShaderUtil and
//   filesystem JSON output. No project runtime system or mutation APIs.
// USAGE NOTES:
//   Invoke Capture on Unity's main thread, including during Play Mode. Returns
//   the absolute JSON path. Writes only Logs/HorrorExpansion/runtime-inspection;
//   does not instantiate materials, compile shaders, save/import assets, change
//   runtime objects or advance simulation. Snapshot enumeration is synchronous.
//   StartFrameSample/StopFrameSample own temporary EditorApplication callbacks;
//   sampling stops on deadline, sample bound, Play Mode exit or assembly reload.
//   Frame intervals describe Editor callback observations, not standalone GPU time.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Worsen.Editor.Horror
{
    public static class HorrorRuntimeInspectionTools
    {
        private static FrameSampleReport frameSample;
        private static string frameSamplePath;
        private static double frameSampleStarted, frameSamplePrevious;
        private static int frameSampleLastFrame;
        private const int MaximumFrameSamples = 12000;

        public static string StartFrameSample(float seconds = 20f)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Frame sampling requires Play Mode.");
            if (frameSample != null) throw new InvalidOperationException("A frame sample is already active: " + frameSamplePath);
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Duration must be finite and positive.");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/HorrorExpansion/runtime-inspection"));
            Directory.CreateDirectory(directory);
            frameSamplePath = Path.Combine(directory, "frames-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N") + ".json");
            frameSampleStarted = frameSamplePrevious = EditorApplication.timeSinceStartup;
            frameSampleLastFrame = Time.frameCount;
            frameSample = new FrameSampleReport {
                status = "sampling", startedUtc = DateTime.UtcNow.ToString("o"), assetsPath = Application.dataPath,
                measurement = "EditorApplication.update observations of new Play Mode Time.frameCount values; delivered intervals include editor/tool overhead and are not standalone GPU frame time. UnityStats counters are editor-global observations, not GPU timings or isolated Game View counters. Percentiles use nearest rank.",
                requestedSeconds = seconds, boundedSeconds = Math.Min(60f, seconds), maximumSamples = MaximumFrameSamples,
                firstFrame = frameSampleLastFrame, managedBytesStart = GC.GetTotalMemory(false),
                scenePathsStart = ScenePaths(), graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), qualityLevel = QualitySettings.GetQualityLevel(),
                qualityName = QualitySettings.names[QualitySettings.GetQualityLevel()],
                pipeline = GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.GetType().FullName,
                pipelinePath = AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline),
                vSyncCount = QualitySettings.vSyncCount, targetFrameRate = Application.targetFrameRate,
                gameViewFocusedAtStart = Application.isFocused, pausedAtStart = EditorApplication.isPaused
            };
            try { File.WriteAllText(frameSamplePath, JsonUtility.ToJson(frameSample, true)); }
            catch { frameSample = null; throw; }
            // The first interval starts after setup/initial report I/O so that
            // reserving the evidence file is not charged to its first frame.
            frameSampleStarted = frameSamplePrevious = EditorApplication.timeSinceStartup;
            frameSampleLastFrame = frameSample.firstFrame = Time.frameCount;
            EditorApplication.update += SampleFrame;
            EditorApplication.playModeStateChanged += SamplePlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += SampleAssemblyReload;
            return frameSamplePath;
        }

        public static string StopFrameSample() => FinishFrameSample("manual-stop");

        private static void SamplePlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) FinishFrameSample("play-mode-exit");
        }

        private static void SampleAssemblyReload() => FinishFrameSample("assembly-reload");

        private static void SampleFrame()
        {
            if (frameSample == null) return;
            if (!Application.isPlaying) { FinishFrameSample("play-mode-exit"); return; }
            double now = EditorApplication.timeSinceStartup;
            // A stalled/paused editor can deliver a late callback. Do not count
            // that callback as a frame within the bounded observation window.
            if (now - frameSampleStarted >= frameSample.boundedSeconds) { FinishFrameSample("duration-complete"); return; }
            int frame = Time.frameCount;
            if (frame == frameSampleLastFrame) return;
            if (frame < frameSampleLastFrame) { FinishFrameSample("frame-counter-reset"); return; }
            int advanced = frame - frameSampleLastFrame;
            frameSample.unobservedFrames += Math.Max(0, advanced - 1);
            frameSample.samples.Add(new FrameSampleRow {
                frame = frame, renderedFrame = Time.renderedFrameCount, framesAdvanced = advanced,
                editorElapsedSeconds = now - frameSampleStarted, intervalMilliseconds = (now - frameSamplePrevious) * 1000d,
                unityUnscaledDeltaMilliseconds = Time.unscaledDeltaTime * 1000d,
                drawCalls = UnityStats.drawCalls, triangles = UnityStats.triangles, vertices = UnityStats.vertices
            });
            frameSampleLastFrame = frame;
            frameSamplePrevious = now;
            if (frameSample.samples.Count >= MaximumFrameSamples) FinishFrameSample("sample-limit");
        }

        private static string FinishFrameSample(string reason)
        {
            if (frameSample == null) return frameSamplePath ?? string.Empty;
            EditorApplication.update -= SampleFrame;
            EditorApplication.playModeStateChanged -= SamplePlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= SampleAssemblyReload;
            FrameSampleReport report = frameSample;
            frameSample = null;
            report.status = "completed"; report.stopReason = reason; report.endedUtc = DateTime.UtcNow.ToString("o");
            report.editorElapsedSeconds = EditorApplication.timeSinceStartup - frameSampleStarted;
            report.lastFrame = frameSampleLastFrame; report.managedBytesEnd = GC.GetTotalMemory(false);
            report.managedBytesDifference = report.managedBytesEnd - report.managedBytesStart;
            report.scenePathsEnd = ScenePaths(); report.sampleCount = report.samples.Count;
            report.hasFrameEvidence = report.sampleCount > 0;
            report.intervalsMilliseconds = Describe(report.samples.Select(row => row.intervalMilliseconds));
            report.drawCalls = Describe(report.samples.Select(row => (double)row.drawCalls));
            report.triangles = Describe(report.samples.Select(row => (double)row.triangles));
            report.vertices = Describe(report.samples.Select(row => (double)row.vertices));
            File.WriteAllText(frameSamplePath, JsonUtility.ToJson(report, true));
            return frameSamplePath;
        }

        private static string[] ScenePaths() => Enumerable.Range(0, SceneManager.sceneCount)
            .Select(index => SceneManager.GetSceneAt(index).path).ToArray();

        private static Distribution Describe(IEnumerable<double> values)
        {
            double[] ordered = values.OrderBy(value => value).ToArray();
            if (ordered.Length == 0) return new Distribution();
            return new Distribution {
                count = ordered.Length, median = ordered.Length % 2 == 1 ? ordered[ordered.Length / 2] :
                    (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) / 2d,
                p95 = ordered[(int)Math.Ceiling(ordered.Length * 0.95d) - 1],
                p99 = ordered[(int)Math.Ceiling(ordered.Length * 0.99d) - 1], max = ordered[ordered.Length - 1]
            };
        }

        [Serializable] private sealed class FrameSampleReport {
            public string status, startedUtc, endedUtc, assetsPath, measurement, graphicsDevice, graphicsApi, qualityName, pipeline, pipelinePath, stopReason;
            public string[] scenePathsStart, scenePathsEnd;
            public int firstFrame, lastFrame, maximumSamples, sampleCount, unobservedFrames, qualityLevel, vSyncCount, targetFrameRate;
            public float requestedSeconds, boundedSeconds;
            public bool gameViewFocusedAtStart, pausedAtStart, hasFrameEvidence;
            public long managedBytesStart, managedBytesEnd, managedBytesDifference;
            public double editorElapsedSeconds;
            public Distribution intervalsMilliseconds, drawCalls, triangles, vertices;
            public List<FrameSampleRow> samples = new List<FrameSampleRow>();
        }
        [Serializable] private sealed class FrameSampleRow {
            public int frame, renderedFrame, framesAdvanced, drawCalls, triangles, vertices;
            public double editorElapsedSeconds, intervalMilliseconds, unityUnscaledDeltaMilliseconds;
        }
        [Serializable] private sealed class Distribution { public int count; public double median, p95, p99, max; }

        [MenuItem("Worsen/Horror/Capture Runtime Inspection")]
        public static void CaptureMenu() => Debug.Log(Capture());

        public static string Capture()
        {
            var report = new Snapshot {
                capturedUtc = DateTime.UtcNow.ToString("o"), assetsPath = Application.dataPath,
                playing = Application.isPlaying, paused = EditorApplication.isPaused,
                frame = Time.frameCount, renderedFrame = Time.renderedFrameCount,
                seconds = Time.timeAsDouble, realtimeSeconds = Time.realtimeSinceStartupAsDouble,
                deltaTime = Time.deltaTime, fixedDeltaTime = Time.fixedDeltaTime, timeScale = Time.timeScale,
                graphicsDevice = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                colorSpace = QualitySettings.activeColorSpace.ToString(), qualityLevel = QualitySettings.GetQualityLevel(),
                pipeline = GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.GetType().FullName,
                pipelinePath = AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline),
                ambientMode = RenderSettings.ambientMode.ToString(), ambientLight = RenderSettings.ambientLight,
                ambientIntensity = RenderSettings.ambientIntensity, fog = RenderSettings.fog,
                fogColor = RenderSettings.fogColor, fogDensity = RenderSettings.fogDensity,
                shadowDistance = QualitySettings.shadowDistance, shadowResolution = QualitySettings.shadowResolution.ToString(),
                audioListenerVolume = AudioListener.volume, audioListenerPaused = AudioListener.pause
            };
            var objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(item => item.gameObject.scene.IsValid()).ToArray();
            report.sceneObjectCount = objects.Length;
            report.activeObjectCount = objects.Count(item => item.gameObject.activeInHierarchy);
            report.roomObjectCount = objects.Count(item => item.name.IndexOf("room", StringComparison.OrdinalIgnoreCase) >= 0);
            report.handObjectCount = objects.Count(item => item.name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0);
            report.hunterObjectCount = objects.Count(item => item.name.IndexOf("hunter", StringComparison.OrdinalIgnoreCase) >= 0);
            report.countDefinition = "Named object counts include inactive pool objects; componentCounts disambiguates actual runtime component types.";
            report.componentCounts = objects.SelectMany(item => item.GetComponents<Component>()).Where(item => item != null)
                .GroupBy(item => item.GetType().FullName).OrderBy(group => group.Key)
                .Select(group => new CountRow { type = group.Key, total = group.Count(), active = group.Count(item => item.gameObject.activeInHierarchy) }).ToArray();
            report.scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(index => {
                Scene scene = SceneManager.GetSceneAt(index);
                return new SceneRow { name = scene.name, path = scene.path, loaded = scene.isLoaded, dirty = scene.isDirty };
            }).ToArray();

            var materials = new Dictionary<int, Material>();
            report.renderers = Active<Renderer>().Select(renderer => {
                Material[] shared = renderer.sharedMaterials;
                foreach (Material material in shared) if (material != null) materials[material.GetInstanceID()] = material;
                return new RendererRow {
                    path = Hierarchy(renderer.transform), type = renderer.GetType().Name, id = renderer.GetInstanceID(),
                    enabled = renderer.enabled, forceRenderingOff = renderer.forceRenderingOff, isVisible = renderer.isVisible,
                    layer = renderer.gameObject.layer, layerName = LayerMask.LayerToName(renderer.gameObject.layer),
                    bounds = renderer.bounds, position = renderer.transform.position, scale = renderer.transform.lossyScale,
                    materialIds = shared.Select(material => material == null ? 0 : material.GetInstanceID()).ToArray(),
                    hasPropertyBlock = renderer.HasPropertyBlock(), shadows = renderer.shadowCastingMode.ToString(),
                    receiveShadows = renderer.receiveShadows, renderingLayerMask = renderer.renderingLayerMask
                };
            }).ToArray();
            report.materials = materials.Values.OrderBy(material => material.GetInstanceID()).Select(material => {
                var row = new MaterialRow {
                    id = material.GetInstanceID(), name = material.name, path = AssetDatabase.GetAssetPath(material),
                    shader = material.shader == null ? "<missing>" : material.shader.name,
                    shaderId = material.shader == null ? 0 : material.shader.GetInstanceID(),
                    supported = material.shader != null && material.shader.isSupported, renderQueue = material.renderQueue,
                    hasColor = material.HasProperty("_Color"), hasBaseColor = material.HasProperty("_BaseColor"),
                    keywords = material.shaderKeywords, instancing = material.enableInstancing,
                    renderType = material.GetTag("RenderType", false, "")
                };
                if (row.hasColor) row.color = material.GetColor("_Color");
                if (row.hasBaseColor) row.baseColor = material.GetColor("_BaseColor");
                row.textures = material.GetTexturePropertyNames().Select(property => {
                    Texture texture = material.GetTexture(property);
                    return new TextureRow { property = property, name = texture == null ? "<null>" : texture.name,
                        path = AssetDatabase.GetAssetPath(texture), width = texture == null ? 0 : texture.width,
                        height = texture == null ? 0 : texture.height };
                }).ToArray();
                return row;
            }).ToArray();
            report.shaders = materials.Values.Select(material => material.shader).Where(shader => shader != null).Distinct()
                .Select(ReadShader).ToArray();
            report.colliders = Active<Collider>().Select(collider => new ColliderRow {
                path = Hierarchy(collider.transform), type = collider.GetType().Name, id = collider.GetInstanceID(),
                enabled = collider.enabled, trigger = collider.isTrigger, layer = collider.gameObject.layer,
                bounds = collider.bounds, hasRendererOnSameObject = collider.GetComponent<Renderer>() != null
            }).ToArray();
            report.cameras = Active<UnityEngine.Camera>().Select(camera => new CameraRow {
                path = Hierarchy(camera.transform), enabled = camera.enabled, tag = camera.tag,
                position = camera.transform.position, euler = camera.transform.eulerAngles, fieldOfView = camera.fieldOfView,
                near = camera.nearClipPlane, far = camera.farClipPlane, cullingMask = camera.cullingMask,
                clearFlags = camera.clearFlags.ToString(), background = camera.backgroundColor, depth = camera.depth,
                hdr = camera.allowHDR, msaa = camera.allowMSAA, occlusion = camera.useOcclusionCulling,
                targetTexture = camera.targetTexture == null ? "<screen>" : camera.targetTexture.name,
                pixelWidth = camera.pixelWidth, pixelHeight = camera.pixelHeight
            }).ToArray();
            report.lights = Active<Light>().Select(light => new LightRow {
                path = Hierarchy(light.transform), enabled = light.enabled, type = light.type.ToString(),
                color = light.color, intensity = light.intensity, range = light.range, cullingMask = light.cullingMask,
                shadows = light.shadows.ToString(), shadowResolution = light.shadowResolution.ToString(),
                customShadowResolution = light.shadowCustomResolution, shadowStrength = light.shadowStrength,
                shadowBias = light.shadowBias, shadowNormalBias = light.shadowNormalBias,
                position = light.transform.position, bakeType = light.lightmapBakeType.ToString()
            }).ToArray();
            report.audio = Active<AudioSource>().Select(source => new AudioRow {
                path = Hierarchy(source.transform), enabled = source.enabled, playing = source.isPlaying,
                mute = source.mute, volume = source.volume, pitch = source.pitch, loop = source.loop,
                spatialBlend = source.spatialBlend, minDistance = source.minDistance, maxDistance = source.maxDistance,
                rolloff = source.rolloffMode.ToString(), clip = source.clip == null ? "<none>" : source.clip.name,
                clipPath = AssetDatabase.GetAssetPath(source.clip), mixerGroup = source.outputAudioMixerGroup == null ? "<none>" : source.outputAudioMixerGroup.name,
                position = source.transform.position, ignoreListenerPause = source.ignoreListenerPause,
                ignoreListenerVolume = source.ignoreListenerVolume
            }).ToArray();
            report.listenerPaths = Active<AudioListener>().Select(listener => Hierarchy(listener.transform) + " enabled=" + listener.enabled).ToArray();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/HorrorExpansion/runtime-inspection"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            return path;
        }

        private static T[] Active<T>() where T : Component => Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(item => item.gameObject.scene.IsValid()).OrderBy(item => item.GetInstanceID()).ToArray();

        private static string Hierarchy(Transform item)
        {
            string path = item.name;
            for (Transform parent = item.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return item.gameObject.scene.name + ":/" + path;
        }

        private static ShaderRow ReadShader(Shader shader)
        {
            var row = new ShaderRow { id = shader.GetInstanceID(), name = shader.name,
                path = AssetDatabase.GetAssetPath(shader), supported = shader.isSupported };
            try {
                row.messages = ShaderUtil.GetShaderMessages(shader).Select(message => new ShaderMessageRow {
                    severity = message.severity.ToString(), message = message.message, file = message.file,
                    line = message.line, platform = message.platform.ToString()
                }).ToArray();
            }
            catch (Exception error) { row.inspectionError = error.ToString(); }
            return row;
        }

        [Serializable] private sealed class Snapshot
        {
            public string capturedUtc, assetsPath, graphicsDevice, graphicsApi, colorSpace, pipeline, pipelinePath, ambientMode, shadowResolution, countDefinition;
            public bool playing, paused, fog, audioListenerPaused;
            public int frame, renderedFrame, qualityLevel, sceneObjectCount, activeObjectCount, roomObjectCount, handObjectCount, hunterObjectCount;
            public double seconds, realtimeSeconds;
            public float deltaTime, fixedDeltaTime, timeScale, ambientIntensity, fogDensity, shadowDistance, audioListenerVolume;
            public Color ambientLight, fogColor;
            public SceneRow[] scenes; public CountRow[] componentCounts; public RendererRow[] renderers;
            public MaterialRow[] materials; public ShaderRow[] shaders; public ColliderRow[] colliders;
            public CameraRow[] cameras; public LightRow[] lights; public AudioRow[] audio; public string[] listenerPaths;
        }
        [Serializable] private sealed class CountRow { public string type; public int total, active; }
        [Serializable] private sealed class SceneRow { public string name, path; public bool loaded, dirty; }
        [Serializable] private sealed class RendererRow {
            public string path, type, layerName, shadows; public int id, layer; public uint renderingLayerMask;
            public bool enabled, forceRenderingOff, isVisible, hasPropertyBlock, receiveShadows;
            public Bounds bounds; public Vector3 position, scale; public int[] materialIds;
        }
        [Serializable] private sealed class MaterialRow {
            public int id, shaderId, renderQueue; public string name, path, shader, renderType; public string[] keywords;
            public bool supported, hasColor, hasBaseColor, instancing; public Color color, baseColor; public TextureRow[] textures;
        }
        [Serializable] private sealed class TextureRow { public string property, name, path; public int width, height; }
        [Serializable] private sealed class ShaderRow { public int id; public string name, path, inspectionError; public bool supported; public ShaderMessageRow[] messages; }
        [Serializable] private sealed class ShaderMessageRow { public string severity, message, file, platform; public int line; }
        [Serializable] private sealed class ColliderRow { public string path, type; public int id, layer; public bool enabled, trigger, hasRendererOnSameObject; public Bounds bounds; }
        [Serializable] private sealed class CameraRow {
            public string path, tag, clearFlags, targetTexture; public bool enabled, hdr, msaa, occlusion;
            public Vector3 position, euler; public Color background; public float fieldOfView, near, far, depth;
            public int cullingMask, pixelWidth, pixelHeight;
        }
        [Serializable] private sealed class LightRow {
            public string path, type, shadows, shadowResolution, bakeType; public bool enabled; public Color color;
            public float intensity, range, shadowStrength, shadowBias, shadowNormalBias; public int cullingMask, customShadowResolution; public Vector3 position;
        }
        [Serializable] private sealed class AudioRow {
            public string path, rolloff, clip, clipPath, mixerGroup; public bool enabled, playing, mute, loop, ignoreListenerPause, ignoreListenerVolume;
            public float volume, pitch, spatialBlend, minDistance, maxDistance; public Vector3 position;
        }
    }
}
