// ============================================================================
// FogSpikeWindow.cs
// ============================================================================
// PURPOSE:
//   Drives the synthetic floor in Play Mode and records genuine rendering timings.
//   Missing GPU samples, wrong resolution or interrupted runs produce INCOMPLETE,
//   never a fabricated zero-cost pass. This tool does not launch Play Mode itself.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Animate ordered collapse and the authored camera path without gameplay systems.
//   - Probe whole-floor density sweeps and write raw sample evidence as JSON.
// DEPENDENCIES:
//   - Own setup/profile, FogManager, FogRendererFeature sampler and UnityEditor.
// USAGE NOTES:
//   Close other scenes first. OnEnable/OnDisable pair editor updates. Probe budgets
//   are declared before sampling: GPU <=1.5 ms at 1920x1080; upload CPU <=.3 ms.
//   Measures the raymarch marker only, not third-party distance fog or whole-frame
//   time. GPU profiler support is required. Upload samples exclude field evaluation.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Worsen.Presentation.Fog;

namespace Worsen.Editor.Fog
{
    public sealed class FogSpikeWindow : EditorWindow
    {
        [Serializable] private sealed class Evidence
        {
            public string utc, gpu, graphicsApi, unity, status, reason;
            public string scenario = "Whole-floor density sweep, moving camera, coalesced progress uploads";
            public float gpuBudgetMs = 1.5f, uploadBudgetMs = .3f;
            public int requiredWidth = 1920, requiredHeight = 1080, requestedFrames, observedFrames, invalidFrames;
            public int warmupFrames;
            public string configJson, profileJson;
            public List<float> gpuMilliseconds = new List<float>();
            public List<double> uploadMilliseconds = new List<double>();
            public List<Vector2Int> renderSizes = new List<Vector2Int>();
            public float maximumGpuMs;
            public double maximumUploadMs;
        }
        private FogManager _fog;
        private FogSpikeProfile _profile;
        private UnityEngine.Camera _camera;
        private Transform _path;
        private Evidence _evidence;
        private double _start;
        private int _lastFrame, _frames, _uploadRevision;
        private bool _running;
        private string _message = "Enter Play Mode in FogSpike. Set Game View to 1920x1080 and URP render scale to 1 for the budget probe.";

        [MenuItem("Worsen/Fog/3 - Animate and Probe")]
        public static void Open() => GetWindow<FogSpikeWindow>("Fog Spike");
        private void OnEnable() => EditorApplication.update += Tick;
        private void OnDisable() { EditorApplication.update -= Tick; Finish("Window closed"); }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox(_message, MessageType.Info);
            if (GUILayout.Button("Animate collapse room by room")) Begin(false);
            if (GUILayout.Button("Measure declared budget (whole-floor sweep)")) Begin(true);
            if (GUILayout.Button("Stop")) Finish("Stopped by operator");
        }
        private void Begin(bool probe)
        {
            Finish("Replaced by a new run");
            Scene scene = SceneManager.GetSceneByPath(FogSpikeSetup.ScenePath);
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || !scene.isLoaded || SceneManager.sceneCount != 1)
            { _message = "Requires unpaused Play Mode with only FogSpike loaded."; return; }
            var root = scene.GetRootGameObjects().SingleOrDefault(item => item.name == "FogSpike");
            _fog = root == null ? null : root.GetComponent<FogManager>();
            _camera = root == null ? null : root.GetComponentInChildren<UnityEngine.Camera>();
            _path = root == null ? null : root.transform.Find("Camera Path");
            _profile = FogSpikeSetup.Profile;
            if (_fog == null || _camera == null || _path == null || _path.childCount != 16 || _profile == null || FogSpikeSetup.Config == null)
            { _message = "Spike wiring missing; rebuild with the setup tool."; return; }
            FogSpikeSetup.CreateInputs(_profile, out var rooms, out var graph);
            _fog.Initialize(FogSpikeSetup.Config); _fog.SetEnabled(true); _fog.SetRooms(rooms, graph);
            _start = EditorApplication.timeSinceStartup; _frames = 0; _lastFrame = -1; _uploadRevision = 0; _running = true;
            if (probe)
            {
                _evidence = new Evidence { utc = DateTime.UtcNow.ToString("o"), gpu = SystemInfo.graphicsDeviceName,
                    graphicsApi = SystemInfo.graphicsDeviceType.ToString(), unity = Application.unityVersion,
                    requestedFrames = _profile.SampleFrames, warmupFrames = _profile.WarmupFrames,
                    configJson = EditorJsonUtility.ToJson(FogSpikeSetup.Config), profileJson = EditorJsonUtility.ToJson(_profile) };
                FogRendererFeature.Sampler.enableRecording = true;
            }
            _message = probe ? "Probing: GPU <=1.5 ms at 1920x1080; CPU upload <=0.3 ms. No result until complete." : "Animating collapse from room 16 toward room 1.";
        }
        private void Tick()
        {
            if (!_running) return;
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || _fog == null || _camera == null)
            { Finish("Play Mode interrupted"); return; }
            double elapsed = EditorApplication.timeSinceStartup - _start;
            if (_evidence != null && elapsed > _profile.TimeoutSeconds) { Finish("Measurement timeout"); return; }
            if (_lastFrame == Time.frameCount) return;
            _lastFrame = Time.frameCount;
            if (_evidence != null && _frames++ >= _profile.WarmupFrames)
            {
                var sampler = FogRendererFeature.Sampler;
                _evidence.observedFrames++;
                Vector2Int size = FogRendererFeature.LastRenderSize;
                _evidence.renderSizes.Add(size);
                if (FogRendererFeature.LastRenderFrame < Time.frameCount - 1 || sampler.gpuSampleCount != 1 || sampler.gpuElapsedTime <= 0f || size != new Vector2Int(1920, 1080))
                    _evidence.invalidFrames++;
                else _evidence.gpuMilliseconds.Add(sampler.gpuElapsedTime);
                if (_fog.UploadRevision != _uploadRevision)
                    _evidence.uploadMilliseconds.Add(_fog.LastUploadMilliseconds);
                _uploadRevision = _fog.UploadRevision;
                if (_evidence.observedFrames >= _profile.SampleFrames) { Finish(null); return; }
            }
            else _uploadRevision = _fog.UploadRevision;
            float seconds = (float)elapsed;
            for (int id = 0; id < 16; id++)
            {
                float progress = _evidence == null ? Mathf.Clamp01(seconds / _profile.SecondsPerRoom - (15 - id)) :
                    Mathf.Lerp(_profile.MinimumProbeProgress, 1f, Mathf.PingPong(seconds / _profile.SecondsPerRoom, 1f));
                _fog.SetRoomProgress(id + 1, progress);
            }
            // Ping-pong along the snake avoids teleporting through walls at the loop.
            float segment = Mathf.PingPong(seconds / _profile.SecondsPerCameraSegment, 15f);
            int first = Mathf.Min(14, Mathf.FloorToInt(segment));
            Vector3 a = _path.GetChild(first).position, b = _path.GetChild(first + 1).position;
            _camera.transform.position = Vector3.Lerp(a, b, segment - first);
            _camera.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
        }
        private void Finish(string interruption)
        {
            _running = false;
            FogRendererFeature.Sampler.enableRecording = false;
            if (_evidence == null) return;
            Evidence result = _evidence; _evidence = null;
            bool complete = interruption == null && result.invalidFrames == 0 && result.gpuMilliseconds.Count == result.requestedFrames && result.uploadMilliseconds.Count > 0;
            result.maximumGpuMs = result.gpuMilliseconds.Count == 0 ? -1 : result.gpuMilliseconds.Max();
            result.maximumUploadMs = result.uploadMilliseconds.Count == 0 ? -1 : result.uploadMilliseconds.Max();
            result.status = !complete ? "INCOMPLETE" : result.maximumGpuMs <= result.gpuBudgetMs && result.maximumUploadMs <= result.uploadBudgetMs ? "PASS" : "FAIL";
            result.reason = interruption ?? (complete ? "Compared maximum observed samples to declared budgets." : "GPU support, resolution, pass count or upload evidence was missing.");
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs/AgentValidation/PLAN-018");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "fog-spike-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            _message = result.status + ": " + path; Debug.Log(_message); Repaint();
        }
    }
}
