// ============================================================================
// SharedSceneRootTests.cs
// ============================================================================
// PURPOSE:
//   Protects the assembly-order contracts moved into the shared SceneRoot helper.
//   These source checks run without Unity and complement, rather than replace,
//   the existing live scene fixtures and the helper's engine wiring tests.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Scenes.
// KEY RESPONSIBILITIES:
//   - Check each scene's explicit helper parameters and readiness boundary.
//   - Preserve canonical selection, validation order and shared seeded spawning.
//   - Preserve strict generated-scene wiring and symmetric catch teardown.
// DEPENDENCIES:
//   - NUnit and System.IO read the Orchestrator/Scenes source contracts.
// USAGE NOTES:
//   Pure source-contract tests: no Unity API, scene load or asset write.
//   Locate the checkout from NUnit's work directory or the process directory.
//   Runtime behavior is still gated by coordinator-run Unity integration tests.
// ============================================================================
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Worsen.Tests.Scenes
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class SharedSceneRootTests
    {
        [TestCase("TagArena", "1", "out _")]
        [TestCase("FloorLoop", "2", "out var player")]
        public void CompatibilityParametersAndReadyHandoffRemainSceneSpecific(string scene, string command, string playerResult)
        {
            string root = Source(scene + "SceneRoot.cs");
            Assert.That(root, Does.Contain("Compatibility layer (§6b)"));
            Assert.That(root, Does.Contain($"SharedSceneRoot.TryAssembleCompatibility(SceneKey.{scene}, \"Worsen/Scenes/{command} — Build {scene}\", this,"));
            Assert.That(root, Does.Contain("_seed, ref _run, ref _sceneFlow, ref _input, ref _overlay, ref _telemetry, ref _audio,"));
            Assert.That(root, Does.Contain("_level, _playerFactory, _playerProfile, _spawnPosition, _camera, _postFX,"));
            Assert.That(root, Does.Contain("_hunterFactory, _hunterProfile, _hunterSpawnPosition, _chase, _chaseConfig, _hud,"));
            Assert.That(root, Does.Contain("_results, _sourceRevision, _configSnapshotHash, " + playerResult + ")) return;"));
            InOrder(root, "SharedSceneRoot.TryAssembleCompatibility(", "_run.BindGameplay(", "_assembled = true;", $"SceneReady?.Invoke(SceneKey.{scene});");
            Assert.That(root, Does.Contain("SharedSceneRoot.ClearCatch(_results, _audio, clearExpansion: false);"));
        }

        [Test]
        public void FloorLoopKeepsDirectorAndFloorAssemblyBeforeReadiness()
        {
            InOrder(Source("FloorLoopSceneRoot.cs"), "SharedSceneRoot.TryAssembleCompatibility(",
                "if (_floor == null", "_run.BindGameplay(_chase, _floor, _director);", "_floor.Initialize(",
                "_director.Initialize(", "_director.SetLevelView(_level.ReadOnlyState);", "_assembled = true;",
                "SceneReady?.Invoke(SceneKey.FloorLoop);");
        }

        [Test]
        public void HorrorRunParametersKeepGeneratedReadinessAndTitleGating()
        {
            string root = Source("HorrorRunSceneRoot.cs");
            string start = Method(root, "private void Start()");
            Assert.That(start, Does.Contain("ref _run, ref _sceneFlow, ref _input, ref _inputRoute, ref _overlay, ref _audio, ref _telemetry,"));
            Assert.That(start, Does.Contain("_hud, _camera, _postFX);"));
            Assert.That(start, Does.Not.Contain("SceneReady?.Invoke"));
            Assert.That(root, Does.Contain("=> SceneReady?.Invoke(SceneKey.HorrorRun);"));
            InOrder(start, "SharedSceneRoot.InitializeGeneratedServices(", "_horror.Initialize(",
                "_progression = _progression.Initialize(", "_expedition = _expedition.Initialize();",
                "_run.ConfigureCapture(", "_expedition.ConfigureScene(", "_audio.GetComponent<AudioOrchestrator>().ConfigureExpansion(",
                "SharedSceneRoot.ConfigureCatch(_audio, _camera, null, requireAudioRoute: true);",
                "_assembled = true;", "OnEnable();", "_results.Initialize();", "_settings.PublishCurrent();",
                "_input.SetInputEnabled(false);", "_progressionUI.Hide();", "_menu.ShowTitle();");
            Assert.That(root, Does.Contain("SharedSceneRoot.ClearCatch(null, _audio, clearExpansion: true);"));
        }

        [Test]
        public void CanonicalServicesPreserveFallbackAndInputRouteResolutionOrder()
        {
            string body = Method(Source("SharedSceneRoot.cs"), "public static void InitializeServices(");
            InOrder(body, "run = allowCanonicalFallback && run == null ? RunSessionManager.Instance : run.Initialize(seed);",
                "sceneFlow = allowCanonicalFallback && sceneFlow == null ? SceneFlowManager.Instance : sceneFlow.Initialize();",
                "input = allowCanonicalFallback && input == null ? InputManager.Instance : input.Initialize();",
                "if (resolveInputRoute) inputRoute = input.GetComponent<InputOrchestrator>();",
                "overlay = allowCanonicalFallback && overlay == null ? DebugOverlayManager.Instance : overlay.Initialize();");
        }

        [Test]
        public void CompatibilityValidatesAndInitializesBeforeConsumingThePreparedRandomSource()
        {
            string body = Method(Source("SharedSceneRoot.cs"), "public static bool TryAssembleCompatibility(");
            Assert.That(body, Does.Contain("allowCanonicalFallback: true, resolveInputRoute: false"));
            InOrder(body, "InitializeServices(", "if (run == null", "if (level == null", "telemetry = telemetry.Initialize();",
                "if (hunterFactory == null", "audio = audio.Initialize();", "hud.Initialize();", "results.Initialize();",
                "run.ConfigureCapture(", "run.PrepareScene(scene);", "level.Initialize();", "InitializeViews(camera, postFX);",
                "if (!camera.IsReady || !postFX.IsReady)", "ConfigureCatch(audio, camera, results, requireAudioRoute: false);",
                "playerFactory.Configure(playerProfile, run.RandomSource);", "playerFactory.Spawn(", "PlayerRegistry.TryGet(",
                "hunterFactory.Configure(hunterProfile, run.RandomSource, player.ReadOnlyState, level.ReadOnlyState);",
                "hunterFactory.Spawn(", "chase.Initialize(chaseConfig, player.ReadOnlyState);", "return true;");
            Assert.That(body, Does.Contain("Quaternion.Euler(0f, 90f, 0f)"));
            Assert.That(body, Does.Contain("Quaternion.Euler(0f, 270f, 0f)"));
            Assert.That(body, Does.Not.Contain("SceneReady"));
            Assert.That(body, Does.Not.Contain("ConfigureExpansion"));
        }

        [Test]
        public void GeneratedServicesPreserveStrictBootstrapAndDifferentPresentationOrder()
        {
            string body = Method(Source("SharedSceneRoot.cs"), "public static void InitializeGeneratedServices(");
            InOrder(body, "InitializeServices(seed, allowCanonicalFallback: false, resolveInputRoute: true,",
                "audio = audio.Initialize();", "telemetry = telemetry.Initialize();", "hud.Initialize();",
                "InitializeViews(camera, postFX);", "camera.GetComponent<CameraOrchestrator>().Configure(run, camera);");
            Assert.That(body, Does.Not.Contain("PrepareScene"));
            Assert.That(body, Does.Not.Contain("results.Initialize"));
            InOrder(Method(Source("SharedSceneRoot.cs"), "private static void InitializeViews("),
                "camera.Initialize();", "postFX.Initialize();");
        }

        [Test]
        public void CatchTeardownPreservesResultsThenAudioThenOptionalExpansion()
        {
            InOrder(Method(Source("SharedSceneRoot.cs"), "public static void ClearCatch("),
                "results.GetComponent<ResultsOrchestrator>()?.ConfigureCatch(null);", "if (audio == null) return;",
                "audio.GetComponent<AudioOrchestrator>()?.ClearCatch();",
                "if (clearExpansion) audio.GetComponent<AudioOrchestrator>()?.ClearExpansion();");
        }

        private static void InOrder(string source, params string[] fragments)
        {
            int cursor = 0;
            foreach (string fragment in fragments)
            {
                int next = source.IndexOf(fragment, cursor, StringComparison.Ordinal);
                Assert.That(next, Is.GreaterThanOrEqualTo(cursor), "Missing or reordered: " + fragment);
                cursor = next + fragment.Length;
            }
        }

        private static string Method(string source, string signature)
        {
            int signatureAt = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureAt, Is.GreaterThanOrEqualTo(0), signature);
            int start = source.IndexOf('{', signatureAt), depth = 0;
            for (int i = start; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
            }
            Assert.Fail("Unclosed method: " + signature);
            return null;
        }

        private static string Source(string file)
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.WorkDirectory })
                for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    string path = Path.Combine(directory.FullName, "Assets", "Scripts", "Orchestrator", "Scenes", file);
                    if (File.Exists(path))
                        return Regex.Replace(File.ReadAllText(path), @"\r\n?", "\n");
                }
            Assert.Fail("Cannot locate the checkout source: " + file);
            return null;
        }
    }
}
