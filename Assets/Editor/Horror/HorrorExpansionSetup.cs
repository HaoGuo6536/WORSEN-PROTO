// ============================================================================
// HorrorExpansionSetup.cs
// ============================================================================
// PURPOSE:
//   Migrates the prototype's serialized content to the approved horror expansion.
//   Explicit owned fields change while existing asset identities and unrelated tuning survive.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Horror deterministic content setup.
// KEY RESPONSIBILITIES:
//   - Replace placeholder catalogs with unique hunter, curse and shop definitions.
//   - Enable the reserved exit hub and vertical castle room generator.
// DEPENDENCIES:
//   - Session Progression and Domain Procedural config schemas; UnityEditor asset APIs.
// USAGE NOTES:
//   Editor-only, requires the exclusive Unity lease. Never saves unrelated dirty assets.
//   This command migrates content only; the HorrorRun scene builder owns scene wiring.
// ============================================================================
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Worsen.Session.Progression;
using Worsen.Domain.Procedural;

namespace Worsen.Editor.Horror
{
    public static class HorrorExpansionSetup
    {
        [MenuItem("Worsen/Horror/Update Expansion Content")]
        public static void BuildContent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Content migration requires an idle editor.");
            var progression = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(
                "Assets/Resources/ScriptableObjects/Session/Progression/ProgressionConfig.asset");
            if (progression == null) throw new InvalidOperationException("Build the base HorrorRun configuration first.");
            var defaults = ScriptableObject.CreateInstance<ProgressionConfig>();
            try
            {
                foreach (string name in new[] { "_threats", "_curses", "_offers", "_shopInterval", "_maximumActiveThreats" })
                {
                    FieldInfo field = typeof(ProgressionConfig).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field == null) throw new InvalidOperationException("Missing progression migration field: " + name);
                    field.SetValue(progression, field.GetValue(defaults));
                }
                EditorUtility.SetDirty(progression);
                AssetDatabase.SaveAssetIfDirty(progression);
            }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
            var procedural = AssetDatabase.LoadAssetAtPath<ProceduralConfig>(
                "Assets/Resources/ScriptableObjects/Domain/Procedural/ProceduralConfig.asset");
            if (procedural != null)
            {
                var serialized = new SerializedObject(procedural);
                // This optional property arrives in the geometry publication batch.
                var castle = serialized.FindProperty("_castleModules");
                if (castle != null)
                {
                    castle.boolValue = true;
                    serialized.FindProperty("_initialRoomCount").intValue = 7;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssetIfDirty(procedural);
                }
            }
        }
    }
}
