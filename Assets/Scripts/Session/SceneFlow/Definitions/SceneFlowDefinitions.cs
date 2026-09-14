// ============================================================================
// SceneFlowDefinitions.cs
// ============================================================================
//
// PURPOSE:
//   Defines the project's canonical gameplay scene paths in one place. The setup
//   tool and scene-loading rules share these constants so neither silently loads
//   or rebuilds a different asset after a scene name changes.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Session · SceneFlow.
//
// KEY RESPONSIBILITIES:
//   - Name the TagArena asset built by the M0 setup tool.
//   - Reserve the later floor-loop path without creating that scene.
//
// DEPENDENCIES:
//   - None; these are immutable path constants with no execution logic.
//
// USAGE NOTES:
//   These paths identify assets, not active scene instances. SceneFlow validates
//   build availability before loading, including for the reserved FloorLoop path.
//
// ============================================================================

namespace Worsen.Session.SceneFlow
{
    public static class SceneFlowDefinitions
    {
        public const string TagArenaPath = "Assets/Scenes/TagArena.unity";
        public const string FloorLoopPath = "Assets/Scenes/FloorLoop.unity";
    }
}
