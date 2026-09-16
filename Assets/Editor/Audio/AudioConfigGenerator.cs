// ============================================================================
// AudioConfigGenerator.cs
// ============================================================================
//
// PURPOSE:
//   Restores the audio config and its audible prototype clip wiring deterministically.
//   Existing settings and replacement clips are retained so rebuilding a scene
//   cannot discard a designer's sound work.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Audio.
//
// KEY RESPONSIBILITIES:
//   - Create the missing mirrored DriverConfig and original prototype samples.
//   - Fill only missing cue entries and clip references, preserving existing tuning.
//   - Save only the owned config asset after wiring.
//
// DEPENDENCIES:
//   - Core CueId; Presentation Audio config and definition types; UnityEditor.
//
// USAGE NOTES:
//   - Editor-only; caller must hold the repository Unity lease during generation.
//   - No scene loads or saves; AudioSetup assembles the service for a scene owner.
//
// ============================================================================

using System;
using UnityEditor;
using UnityEngine;
using Worsen.Presentation.Audio;

namespace Worsen.Editor.Audio
{
    public static class AudioConfigGenerator
    {
        public const string ConfigPath = "Assets/Resources/ScriptableObjects/Presentation/Audio/AudioDriverConfig.asset";

        [MenuItem("Worsen/Audio/Create Config and Prototype Samples")]
        public static void CreateConfig() => LoadOrCreateConfig();

        public static AudioDriverConfig LoadOrCreateConfig()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before generating Audio config.");
            AudioSampleGenerator.EnsureSamples();
            EnsureFolder();
            var config = AssetDatabase.LoadAssetAtPath<AudioDriverConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<AudioDriverConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            var serialized = new SerializedObject(config);
            FillClip(serialized.FindProperty("_breathLoop"), "BreathLoop");
            FillClip(serialized.FindProperty("_hunterLoop"), "HunterLoop");
            var defaults = ScriptableObject.CreateInstance<AudioDriverConfig>();
            try
            {
                var entries = serialized.FindProperty("_cues");
                foreach (AudioCueDefinition entry in defaults.Cues) EnsureCue(entries, entry);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
            AssetDatabase.SaveAssetIfDirty(config);
            return config;
        }

        private static void EnsureCue(SerializedProperty entries, AudioCueDefinition definition)
        {
            SerializedProperty found = null;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var item = entries.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("_cue").intValue == (int)definition.Cue) { found = item; break; }
            }
            if (found == null)
            {
                int index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                found = entries.GetArrayElementAtIndex(index);
                found.FindPropertyRelative("_cue").intValue = (int)definition.Cue;
                found.FindPropertyRelative("_clip").objectReferenceValue = null;
                found.FindPropertyRelative("_gain").floatValue = definition.Gain;
                found.FindPropertyRelative("_priority").intValue = definition.Priority;
                found.FindPropertyRelative("_fadeSeconds").floatValue = definition.FadeSeconds;
            }
            FillClip(found.FindPropertyRelative("_clip"), definition.Cue.ToString());
        }

        private static void FillClip(SerializedProperty property, string name)
        {
            if (property.objectReferenceValue != null) return;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioSampleGenerator.AudioFolder + "/" + name + ".wav");
            if (clip == null) throw new InvalidOperationException("Audio sample did not import: " + name);
            property.objectReferenceValue = clip;
        }

        private static void EnsureFolder()
        {
            string folder = "Assets";
            foreach (string part in new[] { "Resources", "ScriptableObjects", "Presentation", "Audio" })
            {
                string next = folder + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(folder, part);
                folder = next;
            }
        }
    }
}
