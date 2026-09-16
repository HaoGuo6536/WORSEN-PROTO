// ============================================================================
// TuningWindow.cs
// ============================================================================
// PURPOSE:
//   Collects current Config and Profile assets into a serialized editor panel. It discovers real assets each refresh and changes only the asset a designer edits, preserving Undo and Inspector restrictions.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Presentation · Tuning.
// KEY RESPONSIBILITIES:
//   - Discover, filter and edit project tuning assets with SerializedObject.
// DEPENDENCIES:
//   - UnityEditor asset discovery and serialized editing only; no runtime GUI or public setters.
// USAGE NOTES:
//   - Editor-only window. Agent-driven edits require the Unity lease; human edits remain visible as normal Unity changes.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Worsen.Editor.Tuning
{
    public sealed class TuningWindow : EditorWindow
    {
        private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        private readonly HashSet<int> _expanded = new HashSet<int>();
        private Vector2 _scroll;
        private string _filter = "";
        [SerializeField] private bool _tuningChanged;
        [MenuItem("Worsen/Tuning/Open Tuning Window")]
        public static void Open() => GetWindow<TuningWindow>("Worsen Tuning");
        private void OnEnable() { EditorApplication.projectChanged += RefreshAssets; RefreshAssets(); }
        private void OnDisable() => EditorApplication.projectChanged -= RefreshAssets;
        private void RefreshAssets()
        {
            _assets.Clear();
            if (!AssetDatabase.IsValidFolder("Assets/Resources/ScriptableObjects")) return;
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Resources/ScriptableObjects" });
            var paths = new List<string>();
            foreach (string guid in guids) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null) continue;
                string name = asset.GetType().Name;
                if (name.EndsWith("Config", StringComparison.Ordinal) || name.EndsWith("Profile", StringComparison.Ordinal))
                    _assets.Add(asset);
            }
            Repaint();
        }
        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _filter = EditorGUILayout.TextField("Filter", _filter);
                if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshAssets();
            }
            EditorGUILayout.HelpBox(_tuningChanged ?
                "Tuning changed. Rebuild the arena before using recordings as evidence: the stored config hash describes the previous build snapshot." :
                "Capture hashes describe the arena build snapshot. Rebuild after Inspector or tuning edits before recording current-config evidence.",
                _tuningChanged ? MessageType.Warning : MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var asset in _assets)
            {
                if (asset == null) continue;
                string path = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(_filter) && path.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                int id = asset.GetInstanceID();
                bool open = EditorGUILayout.Foldout(_expanded.Contains(id), asset.name + " (" + asset.GetType().Name + ")", true);
                if (open) _expanded.Add(id); else _expanded.Remove(id);
                if (!open) continue;
                EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                using (var serialized = new SerializedObject(asset))
                {
                    serialized.Update();
                    var property = serialized.GetIterator();
                    bool children = true;
                    while (property.NextVisible(children))
                    {
                        children = false;
                        using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                            EditorGUILayout.PropertyField(property, true);
                    }
                    if (serialized.ApplyModifiedProperties())
                    {
                        _tuningChanged = true;
                        AssetDatabase.SaveAssetIfDirty(asset);
                    }
                }
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
