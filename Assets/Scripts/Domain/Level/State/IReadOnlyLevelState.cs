// ============================================================================
// IReadOnlyLevelState.cs
// ============================================================================
// PURPOSE:
//   Exposes the currently assembled level graph without granting mutation.
//   Consumers must check readiness before using a graph after marker lifecycle changes.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Level.
// KEY RESPONSIBILITIES:
//   - Expose readiness and the immutable topology snapshot.
// DEPENDENCIES:
//   - Core level contracts; no other Domain system and no upper runtime layer.
// USAGE NOTES:
//   The Level Manager supplies this view through explicit initialization.
// ============================================================================

using Worsen.Core;

namespace Worsen.Domain.Level
{
    public interface IReadOnlyLevelState
    {
        bool IsReady { get; }
        LevelGraph Graph { get; }
    }
}

