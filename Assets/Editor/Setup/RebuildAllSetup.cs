// ============================================================================
// RebuildAllSetup.cs
// ============================================================================
// PURPOSE:
//   Provides the single explicit rebuild entry point for shipped game content.
//   Dependencies and precedence are declared in one ordered manifest, independent
//   of editor selection, filesystem ordering and menu discovery.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Setup aggregate composition.
// KEY RESPONSIBILITIES:
//   - Admit only an idle editor with saved assets and scenes.
//   - Compose the existing production setup tools in dependency order.
//   - Return a fail-fast report, treating logged Unity errors as step failures.
//   - Refresh all scene stamps only after all shared configuration has settled.
// DEPENDENCIES:
//   - Common sequencing, scene builders and existing per-system editor tools.
// USAGE NOTES:
//   Explicit coordinator invocation under its Unity lease only. Existing builders
//   save assets, renderer features, navigation, three scenes and project settings.
//   No rollback: a failure report lists the completed prefix. Diagnostic capture,
//   player builds, FogSpike demo and tuning windows are not production rebuilds.
//   Hunter tools are invoked through existing APIs but are not modified here.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Worsen.Editor.Common;
using Worsen.Editor.Scenes;

namespace Worsen.Editor.Setup
{
    public static class RebuildAllSetup
    {
        [MenuItem("Worsen/Setup/Rebuild All")]
        public static void RebuildAllMenu()
        {
            SetupReport report = RebuildAll();
            if (report.Succeeded) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
        }

        public static SetupReport RebuildAll()
        {
            var guarded = new List<SetupStep>();
            foreach (var step in CreateManifest())
            {
                SetupStep captured = step;
                guarded.Add(new SetupStep(step.Name, step.Reason, () => RunChecked(captured.Execute)));
            }
            return SetupSequence.Run(guarded);
        }

        public static SetupStep[] CreateManifest() => new[]
        {
            new SetupStep("Admission", "Reject play, imports, builds and unsaved work before any write.", RequireReady),
            new SetupStep("Base configs", "Progression and Procedural must exist before content migrations.", EnsureBaseConfigs),
            new SetupStep("Player effects", "Create the runtime fallback config before assembling Player.", Player.PlayerEffectConfigSetup.EnsureEffectConfig),
            new SetupStep("Player art and prefab", "Import the full character and arms before prefab consumers.", Player.BlockyCharacterSetup.ImportBlockyCharacter),
            new SetupStep("Hunter base", "Wire the base prefab on the pre-provisioned HunterBody layer before Glimpse and scenes.", Hunter.HunterPrefabGenerator.BuildHunterAssets),
            new SetupStep("Chase", "Supply scene chase tuning.", Chase.ChaseConfigGenerator.BuildChaseConfig),
            new SetupStep("Floor", "Create the base visuals copied by HorrorRun.", Floor.FloorConfigGenerator.BuildFloorAssets),
            new SetupStep("Director", "Supply FloorLoop and HorrorRun pacing tuning.", Director.DirectorConfigGenerator.CreateConfig),
            new SetupStep("Camera", "Persist camera tuning and its retained hand material.", Camera.CameraConfigGenerator.CreateConfig),
            new SetupStep("PostFX", "Supply view tuning before scene service assembly.", PostFX.PostFXConfigGenerator.CreateConfig),
            new SetupStep("Telemetry", "Supply capture configuration before scene assembly.", Telemetry.TelemetrySetup.CreateMissingConfig),
            new SetupStep("Audio base", "Create original samples and legacy config before soundscape wiring.", Audio.AudioConfigGenerator.CreateConfig),
            new SetupStep("HUD", "Restore panel and UI references before scene services.", HUD.HUDSetup.RestoreAssets),
            new SetupStep("Results", "Restore panel and UI references before scene services.", Results.ResultsSetup.RestoreAssets),
            new SetupStep("Procedural content", "Publish room catalogues and kits before the generated scene consumes them.", ConfigureProcedural),
            new SetupStep("Shop and catalogue", "Bind the catalogue and economy after base Progression exists.", Progression.ShopSetup.EnsureShop),
            new SetupStep("Shrine", "Bind shrine progression and spawn configs before scenes.", Shrine.ShrineSetup.EnsureShrineAssets),
            new SetupStep("TagArena", "Build legacy services and shared Input/DebugOverlay assets first.", TagArenaSceneSetup.BuildTagArena),
            new SetupStep("FloorLoop", "Build the floor fixture after base assets; preserve legacy support.", FloorLoopSceneSetup.BuildFloorLoop),
            new SetupStep("HorrorRun", "Build expansion, roster, soundscape/mixer, world and fog; promote the title scene last.", HorrorRunSceneSetup.Build),
            new SetupStep("Hunter roster audio", "Apply reviewed roster bindings after HorrorRun soundscape setup.", Audio.HunterRosterAudioSetup.BuildMenu),
            new SetupStep("Final provenance", "Stamp every scene after the last shared config mutation.", SceneProvenanceRefreshTools.RefreshAllCaptureProvenance)
        };

        private static void EnsureBaseConfigs()
        {
            AssetDatabase.SaveAssetIfDirty(HorrorRunSceneSetup.Ensure<Worsen.Session.Progression.ProgressionConfig>(Progression.EffectCatalogueSetup.ProgressionPath));
            AssetDatabase.SaveAssetIfDirty(HorrorRunSceneSetup.Ensure<Worsen.Domain.Procedural.ProceduralConfig>(
                "Assets/Resources/ScriptableObjects/Domain/Procedural/ProceduralConfig.asset"));
        }

        private static void ConfigureProcedural() => Procedural.ProceduralContentSetup.Configure(
            HorrorRunSceneSetup.Require<Worsen.Domain.Procedural.ProceduralConfig>("Assets/Resources/ScriptableObjects/Domain/Procedural/ProceduralConfig.asset"));

        private static void RequireReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Rebuild All requires an idle Edit Mode editor and the coordinator's Unity lease.");
            Type api = Type.GetType("UnityEditor.TestTools.TestRunner.Api.TestRunnerApi, UnityEditor.TestRunner", false);
            var probe = api?.GetMethod("IsRunActive", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (probe == null || probe.ReturnType != typeof(bool) || (bool)probe.Invoke(null, null))
                throw new InvalidOperationException("Cannot establish an idle Unity test runner before Rebuild All.");
            if (LayerMask.NameToLayer("HunterBody") < 0)
                throw new InvalidOperationException("The coordinator must provision HunterBody before Rebuild All.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save or revert scene edits before Rebuild All: " + SceneManager.GetSceneAt(index).path);
            foreach (var asset in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
                if (EditorUtility.IsPersistent(asset) && EditorUtility.IsDirty(asset))
                    throw new InvalidOperationException("Save or revert asset edits before Rebuild All: " + AssetDatabase.GetAssetPath(asset));
        }

        private static void RunChecked(Action action)
        {
            var errors = new List<string>();
            void Capture(string message, string trace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    errors.Add(message + "\n" + trace);
            }
            Application.logMessageReceived += Capture;
            try
            {
                action();
                if (errors.Count != 0) throw new InvalidOperationException("Setup logged errors:\n" + string.Join("\n", errors));
            }
            finally { Application.logMessageReceived -= Capture; }
        }
    }
}
