// ============================================================================
// TelemetrySetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds the Telemetry service and its mirrored config deterministically. Existing config assets retain their identity and settings while scene wiring can be reconstructed after imports.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Create missing config and a root service with explicit serialized Driver/config references.
// DEPENDENCIES:
//   - UnityEditor and Telemetry runtime types only.
// USAGE NOTES:
//   - Editor-only. Caller owns shared-scene construction and Unity lease; persistent services must remain root objects.
// ============================================================================
using System.IO;
using UnityEditor;
using UnityEngine;
using Worsen.Presentation.Telemetry;

namespace Worsen.Editor.Telemetry
{
    public static class TelemetrySetup
    {
        private const string ConfigPath = "Assets/Resources/ScriptableObjects/Presentation/Telemetry/TelemetryDriverConfig.asset";
        [MenuItem("Worsen/Telemetry/Create Missing Config")]
        public static void CreateMissingConfig() => EnsureConfig();
        public static TelemetryDriverConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<TelemetryDriverConfig>(ConfigPath);
            if (config != null) return config;
            EnsureFolder("Assets/Resources/ScriptableObjects/Presentation/Telemetry");
            config = ScriptableObject.CreateInstance<TelemetryDriverConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssetIfDirty(config);
            return config;
        }
        public static TelemetryManager CreateService()
        {
            var root = new GameObject("Telemetry Service");
            var driver = root.AddComponent<TelemetryDriver>();
            var manager = root.AddComponent<TelemetryManager>();
            var driverObject = new SerializedObject(driver);
            driverObject.FindProperty("_config").objectReferenceValue = EnsureConfig();
            driverObject.ApplyModifiedPropertiesWithoutUndo();
            var managerObject = new SerializedObject(manager);
            managerObject.FindProperty("_driver").objectReferenceValue = driver;
            managerObject.ApplyModifiedPropertiesWithoutUndo();
            return manager;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}