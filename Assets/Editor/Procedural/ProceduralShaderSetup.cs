// ============================================================================
// ProceduralShaderSetup.cs
// ============================================================================
// PURPOSE:
//   Serializes the shaders used to construct procedural surfaces and puzzle tiles.
//   Editor resolution makes them build dependencies instead of relying on runtime
//   shader names surviving stripping. Existing designer references are preserved.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Procedural.
// KEY RESPONSIBILITIES:
//   - Fill missing surface, fracture and tile shader references deterministically.
//   - Reject unavailable required shaders before runtime geometry is requested.
// DEPENDENCIES:
//   Procedural config schemas, UnityEditor and Common SetupKit checked wiring.
// USAGE NOTES:
//   Explicit setup only; caller owns the Unity lease and saves persistent configs.
//   Does not create scenes, import packages or change existing shader overrides.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Editor.Procedural
{
    public static class ProceduralShaderSetup
    {
        public static void Configure(ProceduralDriverConfig config)
        {
            RequireIdle();
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.SurfaceShader == null) Common.SetupKit.Wire(config, "_surfaceShader", Require("Universal Render Pipeline/Lit"));
            if (config.CrackShader == null) Common.SetupKit.Wire(config, "_crackShader", Require("Universal Render Pipeline/Unlit"));
        }

        public static void Configure(ProceduralChallengeConfig config)
        {
            RequireIdle();
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.TileShader == null) Common.SetupKit.Wire(config, "_tileShader", Require("Universal Render Pipeline/Unlit"));
        }

        private static Shader Require(string name)
            => Shader.Find(name) ?? throw new InvalidOperationException("Procedural setup requires shader: " + name);
        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Procedural shader setup requires idle Edit Mode.");
        }
    }
}
