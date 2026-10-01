// ============================================================================
// IReadOnlyPlayerRevivalState.cs
// ============================================================================
// PURPOSE:
//   Exposes Extra Life protection without allowing dependent systems to mutate it.
//   Optional capability checks preserve existing general Player state fixtures.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Player read-only state contract.
// KEY RESPONSIBILITIES:
//   - Distinguish hunter collision grace from source-independent damage immunity.
// DEPENDENCIES:
//   - No foreign systems or engine operations.
// USAGE NOTES:
//   Deadlines use the Player's injected simulation tick and are end-exclusive.
//   Floor and run initialization clear both intervals.
// ============================================================================
namespace Worsen.Domain.Player
{
    public interface IReadOnlyPlayerRevivalState
    {
        bool RevivalCollisionGraceActive { get; }
        bool RevivalDamageImmune { get; }
    }
}
