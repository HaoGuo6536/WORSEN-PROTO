// ============================================================================
// HorrorBuildValidation.cs
// ============================================================================
// PURPOSE:
//   Builds the saved HorrorRun scene into an isolated local validation directory.
//   Records the actual BuildPipeline result so a queued build is never a success claim.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Horror build verification.
// KEY RESPONSIBILITIES:
//   - Refuse busy editors and unsaved scene content.
//   - Preserve project settings and write a fresh result beside the local player.
// DEPENDENCIES:
//   UnityEditor BuildPipeline/reporting, saved HorrorRun scene and filesystem APIs.
// USAGE NOTES:
//   Requires the coordinator's exclusive Unity lease. QueueBuild returns an output
//   directory; callers wait for completed.json and verify its result and executable.
//   Build uses the current graphics configuration and a normal non-Development player.
//   This avoids development networking and its profiler connection; runtime logs remain available.
// ============================================================================
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Worsen.Editor.Scenes;

namespace Worsen.Editor.Horror
{
    public static class HorrorBuildValidation
    {
        [Serializable]
        private sealed class Result
        {
            public string startedUtc;
            public string endedUtc;
            public string result;
            public string executable;
            public string error;
            public int errors;
            public int warnings;
            public double durationSeconds;
            public ulong bytes;
        }

        [MenuItem("Worsen/Horror/Build Validation Player")]
        public static void BuildMenu() => QueueBuild();

        public static string QueueBuild()
        {
            RequireIdle();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Builds/HorrorExpansion"));
            string output = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff"));
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(root, "latest-build.txt"), output);
            EditorApplication.delayCall += () => Build(output);
            return output;
        }

        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Build validation requires an idle editor.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve unsaved scenes before building.");
        }

        private static void Build(string output)
        {
            var result = new Result { startedUtc = DateTime.UtcNow.ToString("o"), result = "Failed",
                executable = Path.Combine(output, "WORSEN.exe") };
            try
            {
                RequireIdle();
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { HorrorRunSceneSetup.ScenePath }, locationPathName = result.executable,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.StrictMode
                });
                result.result = report.summary.result.ToString();
                result.errors = (int)report.summary.totalErrors;
                result.warnings = (int)report.summary.totalWarnings;
                result.durationSeconds = report.summary.totalTime.TotalSeconds;
                result.bytes = report.summary.totalSize;
            }
            catch (Exception exception) { result.error = exception.ToString(); Debug.LogException(exception); }
            finally
            {
                result.endedUtc = DateTime.UtcNow.ToString("o");
                File.WriteAllText(Path.Combine(output, "completed.json"), JsonUtility.ToJson(result, true));
            }
        }
    }
}
