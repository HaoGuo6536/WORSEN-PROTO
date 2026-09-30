// ============================================================================
// ConsumableDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries immutable item selection, aiming and committed consumable outcomes.
//   Owners can react without sharing Session state or engine objects. Inventory
//   receipts remain the existing ProgressionSnapshot.Inventory contract.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Consumable contracts.
// KEY RESPONSIBILITIES:
//   - Describe slot uses, face observations, stun/slip outcomes and door jams.
//   - Identify sensory cleansing and successful uses without issuing engine commands.
// DEPENDENCIES:
//   - Core identities/inventory records, System collections and Unity value types.
// USAGE NOTES:
//   Collections are frozen copies supplied by Progression. Door and patch ids are
//   floor-local; consumers discard them on teardown. Strength is relative, not damage.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Core
{
    public readonly struct ConsumableInventorySnapshot
    {
        public ConsumableInventorySnapshot(IReadOnlyList<ProgressionInventorySlot> inventory,
            IReadOnlyList<int> remainingUses, int selectedIndex)
        { Inventory = inventory; RemainingUses = remainingUses; SelectedIndex = selectedIndex; }
        public IReadOnlyList<ProgressionInventorySlot> Inventory { get; }
        public IReadOnlyList<int> RemainingUses { get; }
        public int SelectedIndex { get; }
    }

    public readonly struct HunterFaceSample
    {
        public HunterFaceSample(EntityId hunterId, Vector3 head, Vector3 feet, bool visible)
        { HunterId = hunterId; Head = head; Feet = feet; Visible = visible; }
        public EntityId HunterId { get; }
        public Vector3 Head { get; }
        public Vector3 Feet { get; }
        public bool Visible { get; }
    }

    public readonly struct HunterStunFact
    {
        public HunterStunFact(EntityId hunterId, float seconds, float strength, long tick)
        { HunterId = hunterId; Seconds = seconds; Strength = strength; Tick = tick; }
        public EntityId HunterId { get; }
        public float Seconds { get; }
        public float Strength { get; }
        public long Tick { get; }
    }

    public readonly struct HunterSlipFact
    {
        public HunterSlipFact(EntityId hunterId, int patchId, Vector3 position, float seconds, long tick)
        { HunterId = hunterId; PatchId = patchId; Position = position; Seconds = seconds; Tick = tick; }
        public EntityId HunterId { get; }
        public int PatchId { get; }
        public Vector3 Position { get; }
        public float Seconds { get; }
        public long Tick { get; }
    }

    public readonly struct DoorJamFact
    {
        public DoorJamFact(int doorId, Vector3 position, float seconds, float breakSeconds, bool active)
        { DoorId = doorId; Position = position; Seconds = seconds; BreakSeconds = breakSeconds; Active = active; }
        public int DoorId { get; }
        public Vector3 Position { get; }
        public float Seconds { get; }
        public float BreakSeconds { get; }
        public bool Active { get; }
    }

    public readonly struct SensoryCleanseFact
    {
        public SensoryCleanseFact(EntityId playerId, long tick) { PlayerId = playerId; Tick = tick; }
        public EntityId PlayerId { get; }
        public long Tick { get; }
    }

    public readonly struct ConsumableUsedFact
    {
        public ConsumableUsedFact(EntityId playerId, string itemId, long tick)
        { PlayerId = playerId; ItemId = itemId; Tick = tick; }
        public EntityId PlayerId { get; }
        public string ItemId { get; }
        public long Tick { get; }
    }
}
