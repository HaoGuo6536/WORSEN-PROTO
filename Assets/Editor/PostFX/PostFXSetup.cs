// ============================================================================
// PostFXSetup.cs
// ============================================================================
//
// PURPOSE:
//   Constructs one scene-owned PostFX service under the coordinator's chosen parent.
//   It serializes only local system wiring and leaves scene identity and saving to its caller.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · PostFX.
//
// KEY RESPONSIBILITIES:
//   - Reuse the named child on repeated setup and preserve config assets.
//   - Wire Manager and Driver without starting runtime initialization.
//
// DEPENDENCIES:
//   - Worsen.Presentation.PostFX only; UnityEditor serialization APIs.
//
// USAGE NOTES:
//   - Editor-only; coordinator owns scene creation, dirty-scene checks, saving and lease.
//   - Package component construction stays inside the owning Driver.
//
// ============================================================================

using System;
using UnityEditor;
using UnityEngine;
using Worsen.Presentation.PostFX;

namespace Worsen.Editor.PostFX
{
    public static class PostFXSetup
    {
        public static PostFXManager Create(Transform parent)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before constructing PostFX wiring.");
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var child = parent.Find("PostFX Service");
            var owner = child != null ? child.gameObject : new GameObject("PostFX Service");
            owner.transform.SetParent(parent, false);
            var driver = owner.GetComponent<PostFXDriver>();
            if (driver == null) driver = owner.AddComponent<PostFXDriver>();
            driver.ConfigureForSetup();
            var manager = owner.GetComponent<PostFXManager>();
            if (manager == null) manager = owner.AddComponent<PostFXManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("_driver").objectReferenceValue = driver;
            serialized.FindProperty("_config").objectReferenceValue = PostFXConfigGenerator.LoadOrCreateConfig();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(manager);
            return manager;
        }
    }
}

