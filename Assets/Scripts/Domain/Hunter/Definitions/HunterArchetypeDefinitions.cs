// ============================================================================
// HunterArchetypeDefinitions.cs
// ============================================================================
// PURPOSE:
//   Defines the pure rule seam between shared Hunter decisions and archetypes.
//   Recording commands and observations stay Hunter-local until their shared
//   payloads are promoted by the coordinator; no presentation types cross here.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Carry injected tick inputs, ordered replay motion and immutable facts.
//   - Carry duplicate indices without changing the coordinator-owned spawn DTO.
// DEPENDENCIES:
//   - Core values; read-only Player, Level and Floor views in Hunter's existing order.
// USAGE NOTES:
//   Modules are per entity, constructed by its Manager. Replay paths are consumed
//   synchronously and acknowledged before the next tick. Facts own copied paths.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using Worsen.Domain.Floor;
namespace Worsen.Domain.Hunter
{
    public interface IHunterArchetypeController
    {
        bool OwnsPursuit { get; }
        bool NeverLoses { get; }
        IReadOnlyList<Vector3> ReplayPath { get; }
        void Reset(HunterArchetypeContext context);
        void Tick(HunterArchetypeContext context);
        bool FilterVisibility(bool visible, SightProbe probe, HunterArchetypeContext context);
        float GoalUtility(HunterGoal goal, float utility);
        bool TryMovement(out Vector3 target, out float speed);
        void CommitReplay(int reachedPoints, bool unreachable = false);
        bool TryTakeFact(out HunterArchetypeFact fact);
    }
    public readonly struct HunterArchetypeContext
    {
        public HunterArchetypeContext(IReadOnlyHunterState hunter, IReadOnlyPlayerState player,
            IReadOnlyLevelState level, IReadOnlyFloorState floor, IReadOnlyDictionary<int, bool> doors,
            IReadOnlyInteractableSet interactables, IReadOnlyActiveEffects effects, float dt, long tick,
            bool canReplay, float speedRatio, IReadOnlyList<Bounds> unavailableRooms = null)
        { Hunter = hunter; Player = player; Level = level; Floor = floor; ClosedDoors = doors;
            Interactables = interactables; Effects = effects; DeltaTime = dt; Tick = tick;
            CanReplay = canReplay; SpeedRatio = speedRatio; UnavailableRooms = unavailableRooms; }
        public IReadOnlyHunterState Hunter { get; }
        public IReadOnlyPlayerState Player { get; }
        public IReadOnlyLevelState Level { get; }
        public IReadOnlyFloorState Floor { get; }
        public IReadOnlyDictionary<int, bool> ClosedDoors { get; }
        public IReadOnlyInteractableSet Interactables { get; }
        public IReadOnlyActiveEffects Effects { get; }
        public float DeltaTime { get; }
        public long Tick { get; }
        public bool CanReplay { get; }
        public float SpeedRatio { get; }
        public IReadOnlyList<Bounds> UnavailableRooms { get; }
    }
    public enum HunterArchetypeFactKind { ReplayedFootstep, ReplayedDoorPassage, TrailRevealed, ReplayTruncated, RecordingOverrun }
    public readonly struct HunterArchetypeFact
    {
        public HunterArchetypeFact(Worsen.Core.EntityId hunter, HunterArchetypeFactKind kind, Vector3 position,
            long tick, long recordedTick = -1, int objectId = -1, float gain = 1f, float pitch = 1f,
            float duration = 0f, IReadOnlyList<Vector3> path = null)
        { Hunter = hunter; Kind = kind; Position = position; Tick = tick; RecordedTick = recordedTick;
            ObjectId = objectId; Gain = gain; Pitch = pitch; Duration = duration;
            Path = path == null ? System.Array.Empty<Vector3>() : new List<Vector3>(path).AsReadOnly(); }
        public Worsen.Core.EntityId Hunter { get; }
        public HunterArchetypeFactKind Kind { get; }
        public Vector3 Position { get; }
        public long Tick { get; }
        public long RecordedTick { get; }
        public int ObjectId { get; }
        public float Gain { get; }
        public float Pitch { get; }
        public float Duration { get; }
        public IReadOnlyList<Vector3> Path { get; }
    }
    public readonly struct HunterSpawnRequest
    {
        public HunterSpawnRequest(SpawnRequest spawn, int duplicateIndex)
        { Spawn = spawn; DuplicateIndex = duplicateIndex; }
        public SpawnRequest Spawn { get; }
        public int DuplicateIndex { get; }
    }
}
