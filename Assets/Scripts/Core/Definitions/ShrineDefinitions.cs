// ============================================================================
// ShrineDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries shrine sites, activations and resolved outcomes across system boundaries.
//   These immutable values let world shrines report use without owning run effects.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Shrine contracts.
// KEY RESPONSIBILITIES:
//   - Identify floor-local shrines and revision-bound shelter deals.
//   - Describe belief loss, delayed hearing and deferred world-effect integration.
// DEPENDENCIES:
//   - Core effect/noise values, System collections and Unity vector values only.
// USAGE NOTES:
//   Shrine ids are unique within a generation; Session APIs also require GenerationId.
//   A resolved fact describes committed intent, not proof that external wiring ran.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Core
{
    public enum ShrineKind { Chance, Bargain, Pacification, Wick, Passage, Protection, Echo, Purgatory }

    public readonly struct ShrineSite
    {
        public ShrineSite(Vector3 position, int roomId, bool gapEdge = false)
        { Position = position; RoomId = roomId; GapEdge = gapEdge; }
        public Vector3 Position { get; }
        public int RoomId { get; }
        public bool GapEdge { get; }
    }

    public readonly struct ShrinePlacement
    {
        public ShrinePlacement(int id, ShrineKind kind, ShrineSite site)
        { Id = id; Kind = kind; Site = site; }
        public int Id { get; }
        public ShrineKind Kind { get; }
        public ShrineSite Site { get; }
    }

    public readonly struct ShrineActivatedFact
    {
        public ShrineActivatedFact(int shrineId, ShrineKind kind, int roomId, Vector3 position, long tick)
        { ShrineId = shrineId; Kind = kind; RoomId = roomId; Position = position; Tick = tick; }
        public int ShrineId { get; }
        public ShrineKind Kind { get; }
        public int RoomId { get; }
        public Vector3 Position { get; }
        public long Tick { get; }
    }

    public readonly struct ShrineResolvedFact
    {
        public ShrineResolvedFact(int generationId, ShrineActivatedFact activation, ShrineKind resolvedKind,
            bool changedOutcome, int cost = 0, float shield = 0f, bool dropBeliefs = false,
            float wickSeconds = 0f, float yieldMultiplier = 1f, int extraHunters = 0, bool mutation = false)
        {
            GenerationId = generationId; Activation = activation; ResolvedKind = resolvedKind;
            ChangedOutcome = changedOutcome; Cost = cost; Shield = shield; DropBeliefs = dropBeliefs;
            WickSeconds = wickSeconds; YieldMultiplier = yieldMultiplier; ExtraHunters = extraHunters; Mutation = mutation;
        }
        public int GenerationId { get; }
        public ShrineActivatedFact Activation { get; }
        public ShrineKind ResolvedKind { get; }
        public bool ChangedOutcome { get; }
        public int Cost { get; }
        public float Shield { get; }
        public bool DropBeliefs { get; }
        public float WickSeconds { get; }
        public float YieldMultiplier { get; }
        public int ExtraHunters { get; }
        public bool Mutation { get; }
    }

    public readonly struct ShrineDealOffer
    {
        public ShrineDealOffer(string id, string title, string copy, int payout)
        { Id = id; Title = title; Copy = copy; Payout = payout; }
        public string Id { get; }
        public string Title { get; }
        public string Copy { get; }
        public int Payout { get; }
    }

    public readonly struct ShrineDealSnapshot
    {
        public ShrineDealSnapshot(bool pending, int floor, IReadOnlyList<ShrineDealOffer> offers)
        { Pending = pending; Floor = floor; Offers = offers; }
        public bool Pending { get; }
        public int Floor { get; }
        public IReadOnlyList<ShrineDealOffer> Offers { get; }
    }
}
