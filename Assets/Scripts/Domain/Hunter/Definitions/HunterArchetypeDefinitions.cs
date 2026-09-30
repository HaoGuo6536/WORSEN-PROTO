// ============================================================================
// HunterArchetypeDefinitions.cs
// ============================================================================
// PURPOSE:
//   Defines the pure rule seam between shared Hunter decisions and archetypes.
//   Injected world views stay Hunter-local while outward immutable facts live in
//   Core. No presentation types cross this decision boundary.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Carry injected tick inputs, ordered replay motion and immutable facts.
//   - Consume shared Core facts without coupling archetypes to Session consumers.
//   - Let specialised attacks opt out of the shared lunge without replacing sensing.
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
    public interface IHunterAttackRules
    {
        bool UsesSharedAttacks { get; }
    }
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

}
