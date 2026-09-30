// ============================================================================
// ShaderReferenceTestSetup.cs
// ============================================================================
// PURPOSE:
//   Gives transient engine-test configs the same explicit shader dependencies as
//   editor-generated assets. Runtime code must not silently repair these bindings,
//   so fixtures that build visuals must supply them just like authored scenes.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Core fixture support.
// KEY RESPONSIBILITIES:
//   - Create transient configs with explicit, checked shader field bindings.
// DEPENDENCIES:
//   UnityEditor serialization and UnityEngine shader lookup, restricted to tests.
// USAGE NOTES:
//   Never saves an asset. Callers destroy the config; shaders are shared engine assets.
//   Pure math tests should keep ordinary CreateInstance and do not need this helper.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;

namespace Worsen.Tests.Core
{
    public static class ShaderReferenceTestSetup
    {
        public static T Create<T>() where T : ScriptableObject
        {
            var config = ScriptableObject.CreateInstance<T>();
            try
            {
                var data = new SerializedObject(config);
                Bind(data, "_surfaceShader", "Universal Render Pipeline/Lit");
                Bind(data, "_mistShader", "Universal Render Pipeline/Particles/Unlit");
                Bind(data, "_crackShader", "Universal Render Pipeline/Unlit");
                Bind(data, "_tileShader", "Universal Render Pipeline/Unlit");
                Bind(data, "_chalkShader", "Universal Render Pipeline/Particles/Unlit");
                Bind(data, "_panelShader", "Universal Render Pipeline/Unlit");
                Bind(data, "_attackShader", "Universal Render Pipeline/Unlit");
                Bind(data, "_webShader", "Sprites/Default");
                Bind(data, "_handShader", "Universal Render Pipeline/Unlit");
                data.ApplyModifiedPropertiesWithoutUndo();
                return config;
            }
            catch { UnityEngine.Object.DestroyImmediate(config); throw; }
        }

        private static void Bind(SerializedObject data, string field, string name)
        {
            var property = data.FindProperty(field);
            if (property == null) return;
            property.objectReferenceValue = Shader.Find(name)
                ?? throw new InvalidOperationException("Shader fixture requires " + name);
        }
    }
}
