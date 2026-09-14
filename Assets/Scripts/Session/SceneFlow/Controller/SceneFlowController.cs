// ============================================================================
// SceneFlowController.cs
// ============================================================================
//
// PURPOSE:
//   Resolves shared scene keys to canonical asset paths without accessing Unity.
//   The reverse mapping recognizes loaded gameplay scenes while ignoring unrelated
//   scenes, allowing the Manager to publish only meaningful Core-typed facts.
//
// ARCHITECTURAL ROLE:
//   Controller (§2) · Session · SceneFlow.
//   Pure lookup rules used by the SceneFlow Manager at its loading boundary.
//
// KEY RESPONSIBILITIES:
//   - Reject invalid load identities rather than guessing a scene name.
//   - Translate canonical loaded paths into shared SceneKey values.
//
// DEPENDENCIES:
//   - Core SceneKey and this system's immutable SceneFlowDefinitions constants.
//
// USAGE NOTES:
//   This stateless mapping needs neither time nor randomness. It does not check
//   the build settings or load anything; those engine operations stay in Manager.
//
// ============================================================================

using System;
using Worsen.Core;

namespace Worsen.Session.SceneFlow
{
    public sealed class SceneFlowController
    {
        public string GetScenePath(SceneKey scene)
        {
            switch (scene)
            {
                case SceneKey.TagArena: return SceneFlowDefinitions.TagArenaPath;
                case SceneKey.FloorLoop: return SceneFlowDefinitions.FloorLoopPath;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scene), scene, "No gameplay scene is mapped to this key.");
            }
        }

        public bool TryGetSceneKey(string path, out SceneKey scene)
        {
            switch (path)
            {
                case SceneFlowDefinitions.TagArenaPath:
                    scene = SceneKey.TagArena;
                    return true;
                case SceneFlowDefinitions.FloorLoopPath:
                    scene = SceneKey.FloorLoop;
                    return true;
                default:
                    scene = SceneKey.None;
                    return false;
            }
        }
    }
}
