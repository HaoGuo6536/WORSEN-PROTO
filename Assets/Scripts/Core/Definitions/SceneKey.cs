// ============================================================================
// SceneKey.cs
// ============================================================================
//
// PURPOSE:
//   Gives scene requests and readiness announcements a stable shared identity.
//   Callers name a scene without embedding file paths or referencing Unity scene
//   objects; the SceneFlow system resolves the key at the loading boundary.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared scene lifecycle data.
//
// KEY RESPONSIBILITIES:
//   - Name authored arenas and the generated horror run without changing existing ids.
//   - Represent an unassigned scene explicitly with None.
//
// DEPENDENCIES:
//   - None; scene path mapping belongs to the Session SceneFlow system.
//
// USAGE NOTES:
//   SceneFlow
//   rejects a request visibly when the corresponding build scene is unavailable.
//
// ============================================================================

namespace Worsen.Core
{
    public enum SceneKey
    {
        None = 0,
        TagArena = 1,
        FloorLoop = 2,
        HorrorRun = 3
    }
}
