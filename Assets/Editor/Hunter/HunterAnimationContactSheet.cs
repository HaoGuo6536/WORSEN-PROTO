// ============================================================================
// HunterAnimationContactSheet.cs
// ============================================================================
// PURPOSE:
//   Renders reproducible roster contact sheets through the runtime Playables
//   driver, not AnimationClip.SampleAnimation. The same isolated playback probe
//   serves native regression tests, so pictures and assertions use one pose clock.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Instantiate saved roster prefabs and initialize/tear down their real Drivers.
//   - Advance explicit fixed-size frames through every animation role.
//   - Render fixed-camera sheets and record clip, frame, speed and binding evidence.
//   - Isolate all temporary engine objects and preserve authored assets/scenes.
// DEPENDENCIES:
//   - Hunter runtime, HunterRosterVisualSetup paths and UnityEditor preview APIs.
// USAGE NOTES:
//   Coordinator-only idle Edit Mode under the Unity lease. RenderAll writes only
//   a new directory under Logs; it neither builds/imports nor saves any assets.
//   PNG rows top-to-bottom: idle, walk, run, ready, attack, recovery, hit; six columns
//   span each clip. Weaver also receives a ceiling sheet. CSV records each tile.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Weaver;
using Object = UnityEngine.Object;

namespace Worsen.Editor.Hunter
{
    public static class HunterAnimationContactSheet
    {
        public static IReadOnlyList<string> Roles { get; } = Array.AsReadOnly(new[]
            { "idle", "walk", "run", "ready", "attack", "recovery", "hit" });
        public const float FrameSeconds = 1f / 60f;
        private const int Tile = 256, Columns = 6;

        [MenuItem("Worsen/Hunter/Render Animation Contact Sheets")]
        public static void RenderMenu()
        {
            string path = Path.GetFullPath("Logs/AgentValidation/PLAN-017/hunter-animation/" +
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
            RenderAll(path); Debug.Log("Hunter real-playback contact sheets: " + path);
        }
        public static void RenderAll(string outputDirectory)
        {
            RequireIdleEditor();
            string directory = Path.GetFullPath(outputDirectory);
            string logs = Path.GetFullPath("Logs") + Path.DirectorySeparatorChar;
            if (!directory.StartsWith(logs, StringComparison.OrdinalIgnoreCase) || Directory.Exists(directory))
                throw new ArgumentException("Use a new output directory under this project's Logs.", nameof(outputDirectory));
            Directory.CreateDirectory(directory);
            foreach (string hunter in HunterRosterVisualSetup.Names)
            {
                Render(hunter, false, directory);
                if (hunter == "Weaver") Render(hunter, true, directory);
            }
        }
        private static void RequireIdleEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Hunter playback evidence requires idle Edit Mode.");
        }
        private static void Render(string hunter, bool ceiling, string directory)
        {
            var preview = new PreviewRenderUtility();
            Texture2D sheet = null;
            try
            {
                using var probe = new PlaybackProbe(hunter);
                preview.AddSingleGO(probe.Root);
                Bounds envelope = default; bool measured = false;
                // Measure every sampled pose first, then keep one camera for the whole sheet.
                foreach (string role in Roles)
                {
                    probe.Begin(role, ceiling);
                    for (int column = 0; column < Columns; column++)
                    {
                        probe.AdvanceTo(column / (float)(Columns - 1));
                        Bounds bounds = HunterRosterVisualSetup.MeasureVisualBounds(probe.Animation.gameObject);
                        if (!measured) { envelope = bounds; measured = true; } else envelope.Encapsulate(bounds);
                    }
                }
                UnityEngine.Camera camera = preview.camera;
                float radius = Mathf.Max(.1f, envelope.extents.magnitude);
                camera.orthographic = true; camera.orthographicSize = radius * 1.12f;
                camera.nearClipPlane = .01f; camera.farClipPlane = radius * 12f + 1f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .13f, .15f, 1f);
                camera.transform.position = envelope.center + new Vector3(3, ceiling ? -2 : 2, -5).normalized * radius * 4f;
                camera.transform.LookAt(envelope.center, Vector3.up);
                preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(35, 30, 0);
                preview.lights[1].intensity = .9f; preview.lights[1].transform.rotation = Quaternion.Euler(315, 210, 0);
                preview.ambientColor = new Color(.35f, .35f, .35f, 1f);
                sheet = new Texture2D(Tile * Columns, Tile * Roles.Count, TextureFormat.RGB24, false);
                var csv = new StringBuilder("row,role,column,normalized_time,seconds,speed,clip\n");
                for (int row = 0; row < Roles.Count; row++)
                {
                    probe.Begin(Roles[row], ceiling);
                    for (int column = 0; column < Columns; column++)
                    {
                        float normalized = column / (float)(Columns - 1);
                        probe.AdvanceTo(normalized);
                        preview.BeginPreview(new Rect(0, 0, Tile, Tile), GUIStyle.none);
                        preview.Render(true);
                        Texture image = preview.EndPreview();
                        CopyTile(image, sheet, column * Tile, (Roles.Count - 1 - row) * Tile);
                        csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3:F6},{4:F6},{5:F6},{6}\n",
                            row, Roles[row], column, normalized, probe.Elapsed, probe.Speed, probe.Clip.name);
                    }
                }
                sheet.Apply(false, false);
                string stem = Path.Combine(directory, hunter + (ceiling ? "-ceiling" : ""));
                File.WriteAllBytes(stem + ".png", sheet.EncodeToPNG());
                File.WriteAllText(stem + ".csv", csv.ToString());
                File.WriteAllText(stem + "-bindings.txt", probe.BindingReport());
            }
            finally
            {
                if (sheet != null) Object.DestroyImmediate(sheet);
                preview.Cleanup();
            }
        }
        private static void CopyTile(Texture image, Texture2D destination, int x, int y)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture copy = RenderTexture.GetTemporary(Tile, Tile, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(image, copy); RenderTexture.active = copy;
                destination.ReadPixels(new Rect(0, 0, Tile, Tile), x, y, false);
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(copy); }
        }

        public sealed class PlaybackProbe : IDisposable
        {
            private Scene _scene;
            private string _role;
            private bool _ceiling;
            private HunterProfile _profile;
            public GameObject Root { get; private set; }
            public HunterDriver Driver { get; private set; }
            public HunterAnimationDriver Animation { get; private set; }
            public AnimationClip Clip { get; private set; }
            public float Speed { get; private set; }
            public float Duration { get; private set; }
            public float Elapsed { get; private set; }
            public PlaybackProbe(string hunter)
            {
                RequireIdleEditor();
                if (!HunterRosterVisualSetup.Names.Contains(hunter)) throw new ArgumentException("Unknown roster entry: " + hunter);
                try
                {
                    _profile = AssetDatabase.LoadAssetAtPath<HunterProfile>(HunterRosterVisualSetup.ProfilePath(hunter));
                    if (_profile == null || _profile.Prefab == null) throw new InvalidOperationException("Saved roster profile/prefab missing: " + hunter);
                    _scene = EditorSceneManager.NewPreviewScene();
                    Root = (GameObject)PrefabUtility.InstantiatePrefab(_profile.Prefab, _scene);
                    Root.hideFlags = HideFlags.HideAndDontSave;
                    Root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); Root.SetActive(true);
                    Driver = Root.GetComponent<HunterDriver>();
                    Animation = Root.GetComponentInChildren<HunterAnimationDriver>(true);
                    if (Driver == null || Animation == null) throw new InvalidOperationException("Missing real Hunter Drivers: " + hunter);
                    Driver.Initialize(_profile.MotorOverride);
                    if (!Animation.IsReady) throw new InvalidOperationException("PlayableGraph failed to initialize: " + hunter);
                }
                catch { Dispose(); throw; }
            }
            public void Begin(string role, bool ceiling = false)
            {
                if (!Roles.Contains(role)) throw new ArgumentException("Unknown clip role: " + role);
                Driver.Initialize(_profile.MotorOverride); // Runtime owner creates the graph, not the probe.
                _role = role; _ceiling = ceiling; Elapsed = 0f;
                if (ceiling)
                {
                    var rules = _profile.ArchetypeRules as WeaverConfig;
                    if (rules == null) throw new InvalidOperationException("Ceiling evidence requires Weaver's saved config.");
                    Driver.ConfigureWeaver(rules.DriverConfig); Driver.SetWeaverCeiling(5f, true);
                }
                var config = Animation.Config;
                switch (role)
                {
                    case "idle": Clip = config.Idle; break;
                    case "walk": Clip = config.Walk; break;
                    case "run": Clip = config.Run; break;
                    case "ready": Clip = config.Windup; break;
                    case "attack": Clip = config.Attack; break;
                    case "recovery": Clip = config.Recovery; break;
                    default: Clip = config.Hit; break;
                }
                if (Clip == null || Clip.length <= 0f) throw new InvalidOperationException("Missing nonempty clip: " + role);
                Speed = role == "walk" ? Mathf.Min(config.WalkStrideSpeed, config.RunThreshold * .5f) :
                    role == "run" ? Mathf.Max(config.RunStrideSpeed, config.RunThreshold + config.RunHysteresis + 1f) : 0f;
                float rate = role == "walk" || role == "run" ? new HunterAnimationPresenter().PlaybackRate(Speed,
                    role == "walk" ? config.WalkStrideSpeed : config.RunStrideSpeed, config.MinimumLocomotionRate, config.MaximumLocomotionRate) : 1f;
                if (!(rate > 0f)) throw new InvalidOperationException("Invalid authored locomotion speeds: " + role);
                Duration = Clip.length / rate;
                // Finish the blend before measuring, so idle-to-locomotion motion cannot
                // masquerade as a moving walk/run clip. Phase clips stay at progress zero.
                int settle = Mathf.CeilToInt(Mathf.Max(.5f, config.BlendSeconds * 3f +
                    (config.SampleRate > 0f ? 2f / config.SampleRate : 0f)) / FrameSeconds);
                for (int i = 0; i < settle; i++) ApplyFrame(FrameSeconds, 0f);
            }
            public void AdvanceTo(float normalized)
            {
                float target = Mathf.Clamp01(normalized) * Duration;
                if (target + .000001f < Elapsed) throw new ArgumentException("Playback samples must advance monotonically.");
                while (Elapsed + .000001f < target)
                {
                    float delta = Mathf.Min(FrameSeconds, target - Elapsed); Elapsed += delta;
                    ApplyFrame(delta, Elapsed / Duration);
                }
            }
            private void ApplyFrame(float dt, float progress)
            {
                int phase = _role == "ready" ? 1 : _role == "attack" ? 2 : _role == "recovery" ? 3 : _role == "hit" ? 4 : 0;
                Animation.Apply(dt, Speed, phase, progress);
                // Match the runtime's body placement command after sampling on the ceiling.
                if (_ceiling) Driver.SetWeaverCeiling(5f, true);
            }
            public string BindingReport()
            {
                var text = new StringBuilder(); var animator = Animation.Animator;
                text.AppendLine("Animator: " + AnimationUtility.CalculateTransformPath(animator.transform, Root.transform));
                text.AppendLine("Avatar: " + (animator.avatar != null ? animator.avatar.name : "null") +
                    "; human=" + animator.isHuman + "; initialized=" + animator.isInitialized + "; culling=" + animator.cullingMode);
                text.AppendLine("Graph: " + Animation.IsReady + "; slot=" + Animation.State.ActiveClip);
                foreach (AnimationClip clip in new[] { Animation.Config.Idle, Animation.Config.Walk, Animation.Config.Run,
                    Animation.Config.Windup, Animation.Config.Attack, Animation.Config.Recovery, Animation.Config.Hit }.Where(c => c != null).Distinct())
                {
                    text.AppendLine("CLIP " + AssetDatabase.GetAssetPath(clip) + " :: " + clip.name);
                    foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        Transform target = string.IsNullOrEmpty(binding.path) ? animator.transform : animator.transform.Find(binding.path);
                        text.AppendLine(binding.path + " :: " + binding.propertyName + " => " +
                            (target != null ? AnimationUtility.CalculateTransformPath(target, Root.transform) : "MISSING TRANSFORM"));
                    }
                }
                return text.ToString();
            }
            public void Dispose()
            {
                try { if (Driver != null) Driver.Teardown(); }
                finally
                {
                    if (Root != null) Object.DestroyImmediate(Root);
                    if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
                    Root = null; Driver = null; Animation = null;
                }
            }
        }
    }
}
