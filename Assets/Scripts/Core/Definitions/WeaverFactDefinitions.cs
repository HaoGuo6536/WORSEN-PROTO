// ============================================================================
// WeaverFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Shares Weaver observations without exposing its controller or world objects.
//   Player consumes a web contact while presentation owners independently consume
//   warned lines and cue facts through Session's Core-typed relays.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Weaver contracts.
// KEY RESPONSIBILITIES:
//   - Carry immutable web contact strength, lifetime and per-hunter serials.
// DEPENDENCIES:
//   - Core entity identities and UnityEngine position values only.
// USAGE NOTES:
//   Web Cutter changes strength, not duration: 1 - (1 - slow) * strength.
//   Serial is scoped to Hunter; duplicate archetypes have different entity ids.
// ============================================================================
using UnityEngine;
namespace Worsen.Core
{
    public enum WeaverFactKind { SkitteringAbove, WetClick, WebLaunched, WebGlow, DoorwayWebbed, WarningCancelled }
    public readonly struct WeaverFact
    {
        public WeaverFact(EntityId hunter, WeaverFactKind kind, long tick, Vector3 position,
            Vector3 end = default, float radius = 0f, float duration = 0f, int serial = 0)
        { Hunter = hunter; Kind = kind; Tick = tick; Position = position; End = end;
            Radius = radius; Duration = duration; Serial = serial; }
        public EntityId Hunter { get; }
        public WeaverFactKind Kind { get; }
        public long Tick { get; }
        public Vector3 Position { get; }
        public Vector3 End { get; }
        public float Radius { get; }
        public float Duration { get; }
        public int Serial { get; }
    }
    public readonly struct WebHitFact
    {
        public WebHitFact(EntityId hunter, EntityId player, long tick, int serial,
            float slowMultiplier, float duration, float slowStrengthMultiplier)
        { Hunter = hunter; Player = player; Tick = tick; Serial = serial; SlowMultiplier = slowMultiplier;
            Duration = duration; SlowStrengthMultiplier = slowStrengthMultiplier; }
        public EntityId Hunter { get; }
        public EntityId Player { get; }
        public long Tick { get; }
        public int Serial { get; }
        public float SlowMultiplier { get; }
        public float Duration { get; }
        public float SlowStrengthMultiplier { get; }
    }
}
