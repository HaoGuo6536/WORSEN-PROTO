// ============================================================================
// HorrorShaderSetup.cs
// ============================================================================
// PURPOSE:
//   Binds fallback attack and web shaders to the Horror config before a build.
//   Explicit references preserve player inclusion while existing authored materials
//   and shader overrides remain untouched by repeated editor setup.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Horror.
// KEY RESPONSIBILITIES:
//   - Fill missing serialized attack and web shader dependencies.
//   - Provide an explicit repair action for the canonical Horror config.
// DEPENDENCIES:
//   HorrorDriverConfig, UnityEditor and Common SetupKit checked wiring.
// USAGE NOTES:
//   Caller owns the Unity lease. No auto-import hooks, scene edits or vendor writes.
//   Configure only wires the supplied asset; the caller decides when to save it.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Presentation.Horror;

namespace Worsen.Editor.Horror
{
    public static class HorrorShaderSetup
    {
        [MenuItem("Worsen/Horror/Wire shader references")]
        public static void WireConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<HorrorDriverConfig>(
                "Assets/Resources/ScriptableObjects/Presentation/Horror/HorrorDriverConfig.asset");
            Configure(config);
            AssetDatabase.SaveAssetIfDirty(config);
        }

        public static void Configure(HorrorDriverConfig config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Horror shader setup requires idle Edit Mode.");
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.AttackShader == null) Common.SetupKit.Wire(config, "_attackShader", Require("Universal Render Pipeline/Unlit"));
            if (config.WebShader == null) Common.SetupKit.Wire(config, "_webShader", Require("Sprites/Default"));
        }

        private static Shader Require(string name)
            => Shader.Find(name) ?? throw new InvalidOperationException("Horror setup requires shader: " + name);
    }
}
