// ============================================================================
// HorrorEffectsDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries controller results to its owning manager without engine references.
//   These local records never cross the system boundary; the manager emits Core values.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Session · HorrorEffects.
// KEY RESPONSIBILITIES:
//   Describe delayed noises and committed local effect facts.
// DEPENDENCIES:
//   Core value contracts and the HorrorEffects system's own data only.
//   Unity value math is pure; engine lifecycle belongs only to the Manager.
// USAGE NOTES:
//   Types contain no behavior; effect timing is owned by the controller.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Session.HorrorEffects
{
    public enum HorrorEffectKind { Flashlight, Afterimage, Noise, FlameDim, OptionalRoomCrack, DoorMark }
    public readonly struct HorrorEffectFact
    {
        public HorrorEffectFact(HorrorEffectKind kind, FlashlightSample light = default,
            NoiseEvent noise = default, Vector3 position = default, float radius = 0f,
            float value = 0f, int roomId = -1)
        { Kind = kind; Light = light; Noise = noise; Position = position; Radius = radius; Value = value; RoomId = roomId; }
        public HorrorEffectKind Kind { get; }
        public FlashlightSample Light { get; }
        public NoiseEvent Noise { get; }
        public Vector3 Position { get; }
        public float Radius { get; }
        public float Value { get; }
        public int RoomId { get; }
    }
    public readonly struct HorrorScheduledNoise
    {
        public HorrorScheduledNoise(EntityId source, Vector3 position, float loudness,
            double dueTime, ProgressionTraits requiredTrait)
        { Source = source; Position = position; Loudness = loudness; DueTime = dueTime; RequiredTrait = requiredTrait; }
        public EntityId Source { get; }
        public Vector3 Position { get; }
        public float Loudness { get; }
        public double DueTime { get; }
        public ProgressionTraits RequiredTrait { get; }
    }
    public readonly struct HorrorHazardResolution
    {
        public HorrorHazardResolution(bool accepted, bool tryWard, float speedMultiplier, float damage)
        { Accepted = accepted; TryWard = tryWard; SpeedMultiplier = speedMultiplier; Damage = damage; }
        public bool Accepted { get; }
        public bool TryWard { get; }
        public float SpeedMultiplier { get; }
        public float Damage { get; }
    }
}
