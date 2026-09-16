// ============================================================================
// AudioSetup.cs
// ============================================================================
//
// PURPOSE:
//   Builds a dedicated Audio service root with explicit config and Driver wiring.
//   Scene setup owners call this helper to recreate the persistent service from source.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Audio.
//
// KEY RESPONSIBILITIES:
//   - Assemble only the owned Audio Manager and Driver on a dedicated root.
//   - Assign the reproducible config without modifying shared scenes or routing.
//
// DEPENDENCIES:
//   - Presentation Audio Manager, Driver and own AudioConfigGenerator; UnityEditor.
//
// USAGE NOTES:
//   - Editor-only; the caller owns the Unity lease and scene save.
//   - Runtime scene assembly calls Initialize and retains its canonical return value.
//
// ============================================================================

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Worsen.Presentation.Audio;

namespace Worsen.Editor.Audio
{
    public static class AudioSetup
    {
        public static AudioManager Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before creating an Audio service.");
            var config = AudioConfigGenerator.LoadOrCreateConfig();
            GameObject root = null;
            foreach (GameObject candidate in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (candidate.name != "Audio Service") continue;
                if (root != null) throw new InvalidOperationException("The scene has multiple Audio Service roots; resolve duplicates before setup.");
                root = candidate;
            }
            if (root == null) root = new GameObject("Audio Service");
            var driver = root.GetComponent<AudioDriver>();
            if (driver == null) driver = root.AddComponent<AudioDriver>();
            var manager = root.GetComponent<AudioManager>();
            if (manager == null) manager = root.AddComponent<AudioManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("_config").objectReferenceValue = config;
            serialized.FindProperty("_driver").objectReferenceValue = driver;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return manager;
        }
    }
}
