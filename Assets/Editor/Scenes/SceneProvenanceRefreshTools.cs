// ============================================================================
// SceneProvenanceRefreshTools.cs
// ============================================================================
//
// PURPOSE:
//   Refreshes capture fingerprints after verified source/config changes without
//   rebuilding a level or reserializing its geometry. Exact scene bytes are
//   preserved except for the two existing string values on the scene root.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Scenes provenance maintenance.
//
// KEY RESPONSIBILITIES:
//   - Apply the exact fingerprint algorithm used by the existing play-entry gates.
//   - Refuse busy editors, dirty target scenes and unsaved configuration assets.
//   - Back up and atomically patch only root stamps, then verify imported values.
//   - Reload only clean targets and preserve unrelated scene instances and edits.
//
// DEPENDENCIES:
//   - UnityEditor scene/asset APIs and read-only reflection of TestRunnerApi.IsRunActive.
//   - Existing scene-root field/type contracts; no dependency on optional Horror types.
//
// USAGE NOTES:
//   Editor-only. The caller must hold this repository's exclusive Unity lease.
//   Does not call SaveScene, SaveAssets, setup builders, navigation baking or
//   RestoreSceneManagerSetup. SaveScene can rewrite unrelated serialization.
//   Target scenes reload to reflect exact byte patches; unrelated scenes keep
//   their objects/handles. Backups/evidence remain under Logs/AgentValidation.
//   Supports ordinary text-serialized scene-root components; prefab-stripped
//   roots or missing stamp fields fail closed rather than guessing YAML edits.
//
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Worsen.Editor.Scenes
{
    public static class SceneProvenanceRefreshTools
    {
        private const string ConfigDirectory = "Assets/Resources/ScriptableObjects";
        private const string SourceDirectory = "Assets/Scripts";
        private const string SourceField = "_sourceRevision";
        private const string ConfigField = "_configSnapshotHash";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        [MenuItem("Worsen/Scenes/Refresh Capture Provenance")]
        public static void RefreshLegacyCaptureProvenance() => Refresh(false);

        [MenuItem("Worsen/Scenes/Refresh Capture Provenance Including HorrorRun")]
        public static void RefreshAllCaptureProvenance() => Refresh(true);

        public static void Refresh(bool includeHorrorRun)
        {
            RequireIdle();
            RequireSavedConfigAssets();
            RequireProjectWorkingDirectory();
            Scene activeBefore = SceneManager.GetActiveScene();
            string activePathBefore = activeBefore.path;
            SceneSetup[] setupBefore = EditorSceneManager.GetSceneManagerSetup();
            SceneRecord[] scenesBefore = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(index => new SceneRecord(SceneManager.GetSceneAt(index))).ToArray();
            var targets = new List<Target>
            {
                new Target("Assets/Scenes/TagArena.unity", "Worsen.Orchestrator.TagArenaSceneRoot"),
                new Target("Assets/Scenes/FloorLoop.unity", "Worsen.Orchestrator.FloorLoopSceneRoot")
            };
            if (includeHorrorRun && File.Exists("Assets/Scenes/HorrorRun.unity"))
                targets.Add(new Target("Assets/Scenes/HorrorRun.unity", "Worsen.Orchestrator.HorrorRunSceneRoot"));
            string source = HashFiles(SourceDirectory, "*.cs");
            string config = HashFiles(ConfigDirectory, "*.asset");
            string output = Path.Combine("Logs", "AgentValidation", "SceneProvenanceRefresh",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            Scene scratch = default;
            bool succeeded = false;
            string failure = "";
            try
            {
                // Capture all admission state before opening or mutating any target.
                foreach (Target target in targets)
                {
                    target.WasListed = setupBefore.Any(item => item.path == target.Path);
                    target.WasLoaded = setupBefore.Any(item => item.path == target.Path && item.isLoaded);
                }
                foreach (Target target in targets)
                {
                    Scene existing = SceneManager.GetSceneByPath(target.Path);
                    if (!File.Exists(target.Path)) throw new FileNotFoundException("Required scene is missing.", target.Path);
                    if (existing.IsValid() && existing.isLoaded && existing.isDirty)
                        throw new InvalidOperationException("Refusing dirty target scene: " + target.Path);
                }
                foreach (Target target in targets)
                {
                    RequireIdle();
                    Scene scene = SceneManager.GetSceneByPath(target.Path);
                    if (!scene.IsValid() || !scene.isLoaded)
                        scene = EditorSceneManager.OpenScene(target.Path, OpenSceneMode.Additive);
                    RequireClean(scene);
                    MonoBehaviour root = FindRoot(scene, target.RootType);
                    if (PrefabUtility.IsPartOfPrefabInstance(root))
                        throw new InvalidOperationException("Prefab-instance scene roots require a dedicated override-aware refresh: " + target.Path);
                    var serialized = new SerializedObject(root);
                    SerializedProperty sourceProperty = RequireString(serialized, SourceField);
                    SerializedProperty configProperty = RequireString(serialized, ConfigField);
                    target.OldSource = sourceProperty.stringValue;
                    target.OldConfig = configProperty.stringValue;
                    target.Before = File.ReadAllBytes(target.Path);
                    ulong rootFileId = GlobalObjectId.GetGlobalObjectIdSlow(root).targetObjectId;
                    target.After = PatchRootStamps(target.Before, rootFileId, source, config);
                    target.Changed = !target.Before.SequenceEqual(target.After);
                    File.WriteAllBytes(Path.Combine(output, Path.GetFileNameWithoutExtension(target.Path) + ".before.unity"), target.Before);
                    File.WriteAllBytes(Path.Combine(output, Path.GetFileNameWithoutExtension(target.Path) + ".expected.unity"), target.After);
                }
                RequireSavedConfigAssets();
                RequireFingerprints(source, config);
                if (targets.Any(target => target.Changed))
                {
                    // Keep one owned scene loaded while clean targets are temporarily unloaded.
                    scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    foreach (Target target in targets.Where(item => item.Changed))
                        CloseTarget(target, !target.WasListed);
                    foreach (Target target in targets.Where(item => item.Changed))
                    {
                        RequireIdle();
                        RequireFingerprints(source, config);
                        if (!File.ReadAllBytes(target.Path).SequenceEqual(target.Before))
                            throw new InvalidOperationException("Scene bytes changed during admission: " + target.Path);
                        target.Attempted = true;
                        ReplaceAtomically(target.Path, target.After, output);
                        AssetDatabase.ImportAsset(target.Path, ImportAssetOptions.ForceUpdate);
                    }
                }
                foreach (Target target in targets)
                {
                    RequireIdle();
                    Scene scene = SceneManager.GetSceneByPath(target.Path);
                    if (!scene.IsValid() || !scene.isLoaded)
                        scene = EditorSceneManager.OpenScene(target.Path, OpenSceneMode.Additive);
                    RequireClean(scene);
                    var serialized = new SerializedObject(FindRoot(scene, target.RootType));
                    if (RequireString(serialized, SourceField).stringValue != source ||
                        RequireString(serialized, ConfigField).stringValue != config ||
                        !File.ReadAllBytes(target.Path).SequenceEqual(target.After))
                        throw new InvalidOperationException("Scene stamp or byte verification failed: " + target.Path);
                }
                RequireSavedConfigAssets();
                RequireFingerprints(source, config);
                succeeded = true;
            }
            catch (Exception error)
            {
                failure = error.ToString();
                try { RollBack(targets, output); }
                catch (Exception rollbackError) { failure += Environment.NewLine + "ROLLBACK: " + rollbackError; }
            }
            finally
            {
                try { RestoreSetup(targets, setupBefore, scenesBefore, activeBefore, activePathBefore, scratch); }
                catch (Exception restoreError)
                {
                    succeeded = false;
                    failure += Environment.NewLine + "SCENE RESTORE: " + restoreError;
                }
                File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new RefreshReport
                {
                    succeeded = succeeded, sourceRevision = source, configSnapshotHash = config,
                    scenePaths = targets.Select(target => target.Path).ToArray(),
                    changedScenes = targets.Where(target => target.Changed).Select(target => target.Path).ToArray(),
                    failure = failure
                }, true));
            }
            if (!succeeded)
                throw new InvalidOperationException("Provenance refresh failed; retained scene backups/evidence: " + output + Environment.NewLine + failure);
            Debug.Log("Capture provenance refreshed without scene reserialization. Scene bytes match exact root-stamp-only candidates. Evidence: " + output);
        }

        // Deliberately identical to TagArenaSceneSetup.HashFiles and FloorLoopSceneSetup.HashFiles.
        // Sort raw paths before slash normalization; retain File.ReadAllText and platform newlines.
        private static string HashFiles(string directory, string pattern)
        {
            var text = new StringBuilder();
            foreach (string path in Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                text.Append(path.Replace('\\', '/')).Append(Environment.NewLine).Append(File.ReadAllText(path)).Append(Environment.NewLine);
            using (var hash = SHA256.Create())
                return "sha256:" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", string.Empty);
        }

        private static byte[] PatchRootStamps(byte[] original, ulong rootFileId, string source, string config)
        {
            if (rootFileId == 0) throw new InvalidOperationException("Scene root has no persistent local file ID.");
            int bom = original.Length >= 3 && original[0] == 0xEF && original[1] == 0xBB && original[2] == 0xBF ? 3 : 0;
            string text = StrictUtf8.GetString(original, bom, original.Length - bom);
            if (!text.StartsWith("%YAML", StringComparison.Ordinal))
                throw new InvalidOperationException("Scene must use Unity UTF-8 text serialization.");
            var header = new Regex(@"^--- !u!114 &" + rootFileId.ToString(CultureInfo.InvariantCulture) + @"\r?$", RegexOptions.Multiline);
            MatchCollection roots = header.Matches(text);
            if (roots.Count != 1) throw new InvalidOperationException("Cannot uniquely locate the ordinary scene-root component block.");
            int start = roots[0].Index;
            Match next = new Regex(@"^--- !u!", RegexOptions.Multiline).Match(text, start + roots[0].Length);
            int end = next.Success ? next.Index : text.Length;
            string block = text.Substring(start, end - start);
            block = ReplaceField(block, SourceField, source);
            block = ReplaceField(block, ConfigField, config);
            byte[] body = StrictUtf8.GetBytes(text.Substring(0, start) + block + text.Substring(end));
            if (bom == 0) return body;
            var output = new byte[body.Length + 3];
            Buffer.BlockCopy(original, 0, output, 0, 3);
            Buffer.BlockCopy(body, 0, output, 3, body.Length);
            return output;
        }

        private static string ReplaceField(string block, string field, string value)
        {
            var pattern = new Regex(@"^(  " + Regex.Escape(field) + @":[ \t]*)([^\r\n]*)", RegexOptions.Multiline);
            MatchCollection matches = pattern.Matches(block);
            if (matches.Count != 1) throw new InvalidOperationException("Expected exactly one existing root field: " + field);
            Match match = matches[0];
            string prefix = match.Groups[1].Value;
            if (prefix.EndsWith(":", StringComparison.Ordinal)) prefix += " ";
            return block.Substring(0, match.Index) + prefix + value + block.Substring(match.Index + match.Length);
        }

        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Editor must be idle before refreshing capture provenance.");
            Type api = Type.GetType("UnityEditor.TestTools.TestRunner.Api.TestRunnerApi, UnityEditor.TestRunner", false);
            MethodInfo probe = api?.GetMethod("IsRunActive", BindingFlags.Static | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (probe == null || probe.ReturnType != typeof(bool))
                throw new InvalidOperationException("Cannot establish that the Unity test runner is idle.");
            if ((bool)probe.Invoke(null, null))
                throw new InvalidOperationException("Wait for the active Unity test run before refreshing provenance.");
        }

        private static void RequireSavedConfigAssets()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { ConfigDirectory }))
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (asset != null && EditorUtility.IsDirty(asset))
                        throw new InvalidOperationException("Save the intended configuration edits first: " + AssetDatabase.GetAssetPath(asset));
        }

        private static void RequireProjectWorkingDirectory()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar);
            string working = Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(project, working, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unity's working directory must be the project root to match the existing fingerprint gates.");
        }

        private static void RequireFingerprints(string source, string config)
        {
            if (HashFiles(SourceDirectory, "*.cs") != source || HashFiles(ConfigDirectory, "*.asset") != config)
                throw new InvalidOperationException("Source/configuration changed during the refresh. Retry after publication settles.");
        }

        private static void RequireClean(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.isDirty)
                throw new InvalidOperationException("Refusing unreadable or dirty target scene: " + scene.path);
        }

        private static MonoBehaviour FindRoot(Scene scene, string expectedType)
        {
            MonoBehaviour[] roots = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null && component.GetType().FullName == expectedType).ToArray();
            if (roots.Length != 1 || !roots[0].isActiveAndEnabled)
                throw new InvalidOperationException(scene.path + " requires exactly one active, enabled " + expectedType + ".");
            return roots[0];
        }

        private static SerializedProperty RequireString(SerializedObject root, string field)
        {
            SerializedProperty property = root.FindProperty(field);
            if (property == null || property.propertyType != SerializedPropertyType.String)
                throw new InvalidOperationException("Missing serialized string field: " + field);
            return property;
        }

        private static void CloseTarget(Target target, bool remove)
        {
            Scene scene = SceneManager.GetSceneByPath(target.Path);
            if (!scene.IsValid() || !scene.isLoaded) return;
            RequireClean(scene);
            if (!EditorSceneManager.CloseScene(scene, remove))
                throw new InvalidOperationException("Could not temporarily unload target scene: " + target.Path);
        }

        private static void ReplaceAtomically(string destination, byte[] bytes, string output)
        {
            string temporary = Path.Combine(output, Guid.NewGuid().ToString("N") + ".replacement.tmp");
            File.WriteAllBytes(temporary, bytes);
            File.Replace(temporary, destination, null);
        }

        private static void RollBack(IEnumerable<Target> targets, string output)
        {
            var errors = new List<Exception>();
            foreach (Target target in targets.Where(item => item.Attempted))
            {
                try
                {
                    byte[] current = File.ReadAllBytes(target.Path);
                    if (current.SequenceEqual(target.Before)) continue;
                    if (!current.SequenceEqual(target.After))
                        throw new InvalidOperationException("External scene edits detected; refusing to overwrite them. Original bytes retained for " + target.Path);
                    RequireIdle();
                    CloseTarget(target, !target.WasListed);
                    ReplaceAtomically(target.Path, target.Before, output);
                    AssetDatabase.ImportAsset(target.Path, ImportAssetOptions.ForceUpdate);
                }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count != 0) throw new AggregateException("Some target rollbacks could not complete safely.", errors);
        }

        private static void RestoreSetup(List<Target> targets, SceneSetup[] setupBefore, SceneRecord[] scenesBefore, Scene activeBefore,
            string activePathBefore, Scene scratch)
        {
            RequireIdle();
            var errors = new List<Exception>();
            foreach (Target target in targets)
            {
                try
                {
                    Scene scene = SceneManager.GetSceneByPath(target.Path);
                    if (target.WasLoaded)
                    {
                        if (!scene.IsValid() || !scene.isLoaded)
                            EditorSceneManager.OpenScene(target.Path, OpenSceneMode.Additive);
                    }
                    else if (scene.IsValid() && scene.isLoaded) CloseTarget(target, !target.WasListed);
                }
                catch (Exception error) { errors.Add(error); }
            }
            // Reorder retained entries without closing or recreating unrelated scene instances.
            for (int i = scenesBefore.Length - 2; i >= 0; i--)
            {
                try
                {
                    Scene first = ResolveScene(scenesBefore[i]);
                    Scene second = ResolveScene(scenesBefore[i + 1]);
                    if (first.IsValid() && second.IsValid() && first != second)
                        EditorSceneManager.MoveSceneBefore(first, second);
                }
                catch (Exception error) { errors.Add(error); }
            }
            Scene active = activeBefore.IsValid() && activeBefore.isLoaded
                ? activeBefore : SceneManager.GetSceneByPath(activePathBefore);
            try
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                if (scratch.IsValid() && scratch.isLoaded)
                {
                    if (scratch.isDirty || scratch.rootCount != 0)
                        throw new InvalidOperationException("The temporary refresh scene changed unexpectedly and was preserved.");
                    if (!EditorSceneManager.CloseScene(scratch, true))
                        throw new InvalidOperationException("Could not close the owned empty refresh scene.");
                }
            }
            catch (Exception error) { errors.Add(error); }
            SceneSetup[] restored = EditorSceneManager.GetSceneManagerSetup();
            if (restored.Length != setupBefore.Length || restored.Where((item, index) =>
                    item.path != setupBefore[index].path || item.isLoaded != setupBefore[index].isLoaded ||
                    item.isActive != setupBefore[index].isActive).Any())
                errors.Add(new InvalidOperationException("The original scene load/active ordering could not be restored exactly."));
            foreach (SceneRecord record in scenesBefore.Where(item => !targets.Any(target => target.Path == item.Path)))
                if (!record.Scene.IsValid() || record.Scene.isLoaded != record.WasLoaded ||
                    (record.WasLoaded && record.Scene.isDirty != record.WasDirty))
                    errors.Add(new InvalidOperationException("An unrelated scene changed during refresh and was preserved: " + record.Path));
            if (errors.Count != 0) throw new AggregateException("Scene setup restoration was incomplete; unsafe discards were refused.", errors);
        }

        private static Scene ResolveScene(SceneRecord record)
        {
            if (record.Scene.IsValid()) return record.Scene;
            return string.IsNullOrEmpty(record.Path) ? default : SceneManager.GetSceneByPath(record.Path);
        }

        private sealed class SceneRecord
        {
            public readonly Scene Scene;
            public readonly string Path;
            public readonly bool WasLoaded, WasDirty;
            public SceneRecord(Scene scene)
            { Scene = scene; Path = scene.path; WasLoaded = scene.isLoaded; WasDirty = scene.isDirty; }
        }

        private sealed class Target
        {
            public readonly string Path, RootType;
            public bool WasListed, WasLoaded, Changed, Attempted;
            public byte[] Before, After;
            public string OldSource, OldConfig;
            public Target(string path, string rootType) { Path = path; RootType = rootType; }
        }

        [Serializable]
        private sealed class RefreshReport
        {
            public bool succeeded;
            public string sourceRevision, configSnapshotHash, failure;
            public string[] scenePaths, changedScenes;
        }
    }
}
