// ============================================================================
// WeaverDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries Weaver-local observations, commands and immutable published facts.
//   These payloads remain in Hunter until the coordinator promotes the outward
//   contracts to Core; Player and Presentation must not import Hunter to use them.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Keep sweep evidence tick-tagged and web slow separate from damage or grabs.
// DEPENDENCIES:
//   - Core identity and UnityEngine value types only.
// USAGE NOTES:
//   Web Cutter modifies slow strength, not duration: Player applies
//   1 - (1 - SlowMultiplier) * SlowStrengthMultiplier, without disabling slide.
//   Cue facts name sound/presentation observations, never directly play assets.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter.Archetypes.Weaver
{
    public enum WeaverAction { None, Reposition, Shoot }
    public enum WeaverFactKind { SkitteringAbove, WetClick, WebLaunched, WebGlow, DoorwayWebbed, WarningCancelled }
    public readonly struct WeaverShotSpot
    {
        public WeaverShotSpot(Vector3 position, bool reachable, bool clear)
        { Position = position; Reachable = reachable; Clear = clear; }
        public Vector3 Position { get; }
        public bool Reachable { get; }
        public bool Clear { get; }
    }
    public readonly struct WeaverObservation
    {
        public WeaverObservation(long tick, Vector3 origin, Vector3 target, float radius, bool clear,
            bool grounded, IReadOnlyList<WeaverShotSpot> spots)
        { Tick = tick; Origin = origin; Target = target; Radius = radius; Clear = clear; Grounded = grounded; Spots = spots; }
        public long Tick { get; }
        public Vector3 Origin { get; }
        public Vector3 Target { get; }
        public float Radius { get; }
        public bool Clear { get; }
        public bool Grounded { get; }
        public IReadOnlyList<WeaverShotSpot> Spots { get; }
    }
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
