// ============================================================================
// PlayerEffectDefinitions.cs
// ============================================================================
// PURPOSE:
//   Names the Player rules that effect data may modify without naming catalogue ids.
//   An optional read-only capability lets Floor inspect slide protection without
//   breaking existing implementations of the general Player state interface.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Describe modifier channels, arithmetic operations and grab protection.
// DEPENDENCIES:
//   - No foreign systems or engine operations.
// USAGE NOTES:
//   Serialized enum ordinals are append-only. Presence channels use Add with one.
// ============================================================================
namespace Worsen.Domain.Player
{
    public enum PlayerEffectStat
    {
        GraceSeconds, BoostDuration, Regeneration, FloorStartHealth, MaximumHealth,
        SprintSpeed, GroundAcceleration, AirAcceleration, TraversalDuration,
        SlideDuration, SlideRetention, JumpHeight, StoredMomentum, SoftLanding,
        QuietSlide, LowProfile, NoLookBack
    }

    public enum PlayerEffectOperation { Multiply, Add, AddFraction }

    public interface IReadOnlyPlayerEffectState
    {
        bool IsUngrabbable { get; }
    }
}
