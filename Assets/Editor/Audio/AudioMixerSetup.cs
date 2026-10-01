// ============================================================================
// AudioMixerSetup.cs
// ============================================================================
// PURPOSE:
//   Creates or repairs the three-bus runtime volume mixer without replacing its identity.
//   The coordinator runs this setup; it never starts Unity or downloads content.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Audio.
// KEY RESPONSIBILITIES:
//   - Preserve existing groups/parameters and reject conflicting parameter names.
//   - Initialize absent mixer views and repair an invalid current-view index.
//   - Wire the soundscape config; its Driver assigns every owned source on initialization.
// DEPENDENCIES:
//   - UnityEditor, Unity audio and Presentation Audio configs.
// USAGE NOTES:
//   Invoke under the coordinator's lease in an idle editor. Unity 6000.3 has no public
//   mixer authoring API; isolated reflection uses signatures checked against that editor.
//   Missing internal APIs fail visibly. Runtime contains no reflection or asset writes.
// ============================================================================
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Worsen.Presentation.Audio;

namespace Worsen.Editor.Audio
{
    public static class AudioMixerSetup
    {
        public const string MixerPath = "Assets/Resources/ScriptableObjects/Presentation/Audio/WorsenAudio.mixer";
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        [MenuItem("Worsen/Audio/Setup Runtime Volume Mixer")]
        public static void Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<AudioSoundscapeDriverConfig>(HorrorAudioSetup.ConfigPath);
            if (config == null) throw new InvalidOperationException("Build the soundscape config first.");
            Configure(MixerPath, config);
        }
        public static AudioMixer Configure(string path, AudioSoundscapeDriverConfig config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Mixer setup requires an idle editor and the coordinator's lease.");
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".mixer", StringComparison.Ordinal))
                throw new ArgumentException("Expected a project mixer path.", nameof(path));
            Type type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController", true);
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !type.IsInstanceOfType(existing)) throw new InvalidOperationException("Mixer path is occupied by another asset.");
            var mixer = existing as AudioMixer ?? (AudioMixer)Method(type, "CreateMixerControllerAtPath").Invoke(null, new object[] { path });
            var master = (AudioMixerGroup)Property(type, "masterGroup").GetValue(mixer);
            EnsureCurrentView(mixer);
            var music = Group(mixer, master, "Music"); var effects = Group(mixer, master, "Effects");
            Expose(mixer, master, "MasterVolume"); Expose(mixer, music, "MusicVolume"); Expose(mixer, effects, "EffectsVolume");
            var serialized = new SerializedObject(config);
            serialized.FindProperty("_mixer").objectReferenceValue = mixer;
            serialized.FindProperty("_musicGroup").objectReferenceValue = music;
            serialized.FindProperty("_effectsGroup").objectReferenceValue = effects;
            serialized.FindProperty("_ambienceGroup").objectReferenceValue = effects;
            serialized.FindProperty("_masterParameter").stringValue = "MasterVolume";
            serialized.FindProperty("_musicParameter").stringValue = "MusicVolume";
            serialized.FindProperty("_effectsParameter").stringValue = "EffectsVolume";
            PruneBanks(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mixer); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(mixer); AssetDatabase.SaveAssetIfDirty(config);
            return mixer;
        }
        private static void EnsureCurrentView(AudioMixer mixer)
        {
            // CreateMixerControllerAtPath creates groups/snapshots, not the editor's view.
            // AddGroupToCurrentView requires views[currentViewIndex] to already exist.
            var property = Property(mixer.GetType(), "views");
            var views = (Array)property.GetValue(mixer);
            var index = Property(mixer.GetType(), "currentViewIndex");
            if (views == null || views.Length == 0)
            {
                Type element = property.PropertyType.GetElementType();
                object view = Activator.CreateInstance(element);
                var guidsField = element.GetField("guids", Flags) ?? throw new MissingFieldException(element.FullName, "guids");
                var nameField = element.GetField("name", Flags) ?? throw new MissingFieldException(element.FullName, "name");
                var groups = mixer.FindMatchingGroups("");
                var guids = Array.CreateInstance(guidsField.FieldType.GetElementType(), groups.Length);
                for (int i = 0; i < groups.Length; i++)
                    guids.SetValue(Property(groups[i].GetType(), "groupID").GetValue(groups[i]), i);
                guidsField.SetValue(view, guids); nameField.SetValue(view, "View");
                views = Array.CreateInstance(element, 1); views.SetValue(view, 0);
                property.SetValue(mixer, views); index.SetValue(mixer, 0);
            }
            else if ((int)index.GetValue(mixer) < 0 || (int)index.GetValue(mixer) >= views.Length)
                index.SetValue(mixer, 0);
        }
        private static AudioMixerGroup Group(AudioMixer mixer, AudioMixerGroup master, string name)
        {
            var childrenProperty = Property(master.GetType(), "children");
            var children = (Array)childrenProperty.GetValue(master);
            AudioMixerGroup found = null;
            foreach (AudioMixerGroup child in children)
                if (child.name == name) { if (found != null) throw new InvalidOperationException("Duplicate mixer group: " + name); found = child; }
            if (found != null) return found;
            var group = (AudioMixerGroup)Method(mixer.GetType(), "CreateNewGroup").Invoke(mixer, new object[] { name, false });
            var replacement = Array.CreateInstance(children.GetType().GetElementType(), children.Length + 1);
            Array.Copy(children, replacement, children.Length); replacement.SetValue(group, children.Length);
            childrenProperty.SetValue(master, replacement);
            Method(mixer.GetType(), "AddGroupToCurrentView").Invoke(mixer, new object[] { group });
            EditorUtility.SetDirty(master);
            return group;
        }
        private static void PruneBanks(SerializedObject config)
        {
            var catalogue = new AudioCueCataloguePresenter();
            var seen = new System.Collections.Generic.HashSet<Worsen.Core.CueId>();
            var banks = config.FindProperty("_sounds");
            for (int i = 0; i < banks.arraySize;)
            {
                var bank = banks.GetArrayElementAtIndex(i);
                var cue = (Worsen.Core.CueId)bank.FindPropertyRelative("Cue").intValue;
                if (!catalogue.TryGet(cue, out var entry) || !seen.Add(catalogue.Canonical(cue))) { banks.DeleteArrayElementAtIndex(i); continue; }
                bank.FindPropertyRelative("Cue").intValue = (int)catalogue.Canonical(cue);
                bank.FindPropertyRelative("Spatial").boolValue = entry.Noise.HasValue;
                bank.FindPropertyRelative("MaxConcurrent").intValue = 1;
                bank.FindPropertyRelative("Loop").boolValue = cue == Worsen.Core.CueId.SlideLoop;
                bank.FindPropertyRelative("Ambience").boolValue = false;
                i++;
            }
        }
        private static void Expose(AudioMixer mixer, AudioMixerGroup group, string name)
        {
            object guid = Method(group.GetType(), "GetGUIDForVolume").Invoke(group, null);
            var property = Property(mixer.GetType(), "exposedParameters");
            var values = (Array)property.GetValue(mixer); Type element = values.GetType().GetElementType();
            var guidField = element.GetField("guid", Flags); var nameField = element.GetField("name", Flags);
            int index = -1;
            for (int i = 0; i < values.Length; i++)
            {
                object value = values.GetValue(i); bool same = guid.Equals(guidField.GetValue(value));
                if ((string)nameField.GetValue(value) == name && !same) throw new InvalidOperationException("Exposed parameter conflict: " + name);
                if (same) index = i;
            }
            if (index >= 0 && (string)nameField.GetValue(values.GetValue(index)) == name) return;
            var updated = Array.CreateInstance(element, values.Length + (index < 0 ? 1 : 0)); Array.Copy(values, updated, values.Length);
            object entry = Activator.CreateInstance(element); guidField.SetValue(entry, guid); nameField.SetValue(entry, name);
            updated.SetValue(entry, index < 0 ? values.Length : index); property.SetValue(mixer, updated);
            Method(mixer.GetType(), "OnChangedExposedParameter").Invoke(mixer, null);
        }
        private static MethodInfo Method(Type type, string name) => type.GetMethod(name, Flags) ?? throw new MissingMethodException(type.FullName, name);
        private static PropertyInfo Property(Type type, string name) => type.GetProperty(name, Flags) ?? throw new MissingMemberException(type.FullName, name);
    }
}
