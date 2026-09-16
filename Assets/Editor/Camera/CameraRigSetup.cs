// ============================================================================
// CameraRigSetup.cs
// ============================================================================
//
// PURPOSE:
//   Constructs one scene-owned Camera service under the coordinator's chosen parent.
//   It serializes only local system wiring and leaves scene identity and saving to its caller.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Camera.
//
// KEY RESPONSIBILITIES:
//   - Reuse the named child on repeated setup and preserve config assets.
//   - Wire Manager and Driver without starting runtime initialization.
//
// DEPENDENCIES:
//   - Worsen.Presentation.Camera only; UnityEditor serialization APIs.
//
// USAGE NOTES:
//   - Editor-only; coordinator owns scene creation, dirty-scene checks, saving and lease.
//   - Package component construction stays inside the owning Driver.
//
// ============================================================================

using System;
using UnityEditor;
using UnityEngine;
using Worsen.Presentation.Camera;

namespace Worsen.Editor.Camera
{
    public static class CameraRigSetup
    {
        public static CameraManager Create(Transform parent, UnityEngine.Camera outputCamera)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before constructing Camera wiring.");
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (outputCamera == null) throw new ArgumentNullException(nameof(outputCamera));
            var child = parent.Find("Camera Service");
            var owner = child != null ? child.gameObject : new GameObject("Camera Service");
            owner.transform.SetParent(parent, false);
            var driver = owner.GetComponent<CameraDriver>();
            if (driver == null) driver = owner.AddComponent<CameraDriver>();
            driver.ConfigureForSetup(outputCamera);
            var manager = owner.GetComponent<CameraManager>();
            if (manager == null) manager = owner.AddComponent<CameraManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("_driver").objectReferenceValue = driver;
            serialized.FindProperty("_config").objectReferenceValue = CameraConfigGenerator.LoadOrCreateConfig();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(manager);
            return manager;
        }
    }
}

