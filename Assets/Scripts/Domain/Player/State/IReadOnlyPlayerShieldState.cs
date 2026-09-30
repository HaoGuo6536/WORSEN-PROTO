// ============================================================================
// IReadOnlyPlayerShieldState.cs
// ============================================================================
// PURPOSE:
//   Adds a read-only shield view without breaking existing Player-state implementers.
//   Shield health belongs to Player even when Session transports it across actor replacement.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Player read-only state contract.
// KEY RESPONSIBILITIES:
//   - Expose remaining non-regenerating shield hit points to Session and HUD routing.
// DEPENDENCIES:
//   - Own existing read-only Player contract only.
// USAGE NOTES:
//   Orchestrators pass the primitive Shield value to Presentation, never this interface.
// ============================================================================
namespace Worsen.Domain.Player
{
    public interface IReadOnlyPlayerShieldState : IReadOnlyPlayerState
    {
        float Shield { get; }
    }
}
