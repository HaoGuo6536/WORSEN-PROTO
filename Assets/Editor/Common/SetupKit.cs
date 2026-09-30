// ============================================================================
// SetupKit.cs
// ============================================================================
// PURPOSE:
//   Provides the shared serialization and folder operations used by editor setup.
//   Missing or mistyped bindings fail at their authoring site instead of silently
//   producing an asset that can only fail later at runtime.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Common setup infrastructure.
// KEY RESPONSIBILITIES:
//   - Resolve required serialized fields with target and path diagnostics.
//   - Wire object references and arrays without changing undo/save policy.
//   - Create canonical project asset folders without replacing existing identities.
// DEPENDENCIES:
//   - UnityEditor serialization and AssetDatabase; UnityEngine.Object only.
// USAGE NOTES:
//   Editor-only. Callers own lease, idle admission and saving. Use nameof for
//   accessible fields; private runtime fields use the checked string path here.
//   Null reference values remain legal for explicitly optional/runtime bindings.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Worsen.Editor.Common
{
    public static class SetupKit
    {
        public static SerializedProperty RequireProperty(this SerializedObject target, string path)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A serialized field path is required.", nameof(path));
            return target.FindProperty(path) ?? throw new InvalidOperationException(
                target.targetObject.GetType().FullName + " has no serialized field '" + path + "'.");
        }

        public static SerializedProperty RequireRelative(this SerializedProperty parent, string path)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            return parent.FindPropertyRelative(path) ?? throw new InvalidOperationException(
                parent.serializedObject.targetObject.GetType().FullName + " has no serialized field '" + parent.propertyPath + "." + path + "'.");
        }

        public static void Wire(Object target, string path, Object value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            using (var serialized = new SerializedObject(target))
            {
                var property = serialized.RequireProperty(path);
                RequireReference(property);
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static void Wire(Object target, string path, Object[] values)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (values == null) throw new ArgumentNullException(nameof(values));
            using (var serialized = new SerializedObject(target))
            {
                var property = serialized.RequireProperty(path);
                if (!property.isArray || property.propertyType == SerializedPropertyType.String)
                    throw new InvalidOperationException(target.GetType().FullName + "." + path + " is not an array.");
                property.arraySize = values.Length;
                for (int index = 0; index < values.Length; index++)
                {
                    var element = property.GetArrayElementAtIndex(index);
                    RequireReference(element);
                    element.objectReferenceValue = values[index];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void RequireReference(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                throw new InvalidOperationException(property.serializedObject.targetObject.GetType().FullName + "." +
                    property.propertyPath + " is not an object reference.");
        }

        public static void EnsureParent(string assetPath)
        {
            ValidateAssetPath(assetPath);
            int slash = assetPath.LastIndexOf('/');
            if (slash < 0) throw new ArgumentException("Expected an asset path beneath Assets.", nameof(assetPath));
            EnsureFolder(assetPath.Substring(0, slash));
        }

        public static void EnsureFolder(string path)
        {
            ValidateAssetPath(path);
            string current = "Assets";
            if (!AssetDatabase.IsValidFolder(current)) throw new InvalidOperationException("AssetDatabase has no Assets root.");
            string[] parts = path.Split('/');
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next) &&
                    (string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, parts[index])) || !AssetDatabase.IsValidFolder(next)))
                    throw new InvalidOperationException("Could not create asset folder: " + next);
                current = next;
            }
        }

        public static void ValidateAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Expected a canonical Assets path.", nameof(path));
            string[] parts = path.Split('/');
            if (parts[0] != "Assets") throw new ArgumentException("Path must be rooted at Assets: " + path, nameof(path));
            foreach (string part in parts)
                if (part.Length == 0 || part == "." || part == ".." || part.IndexOfAny(new[] { '\\', ':', '\r', '\n' }) >= 0)
                    throw new ArgumentException("Expected a canonical Assets path: " + path, nameof(path));
        }
    }
}
