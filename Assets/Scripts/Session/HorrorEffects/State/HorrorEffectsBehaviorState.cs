// ============================================================================
// HorrorEffectsBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores the flashlight state and finite, current-floor effect schedules.
//   It keeps run timing and positions independent of Unity objects or callbacks.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Session · HorrorEffects.
// KEY RESPONSIBILITIES:
//   Retain bounded schedules, effect state and committed result records.
// DEPENDENCIES:
//   Core value contracts and the HorrorEffects system's own data only.
//   Unity value math is pure; engine lifecycle belongs only to the Manager.
// USAGE NOTES:
//   Owned only by HorrorEffectsController and its manager; no foreign mutable-state access.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Session.HorrorEffects
{
    public sealed class HorrorEffectsBehaviorState
    {
        public bool Active;
        public int GenerationId;
        public bool FlashlightEnabled = true;
        public ProgressionEffects Effects;
        public FlashlightSample Aim;
        public bool HasAim;
        public FlashlightSample Afterimage;
        public double AfterimageExpires;
        public double Elapsed;
        public long Tick;
        public long LastTick = -1;
        public long LastMovementTick = -1;
        public long LastLandingTick = -1;
        public PlayerMovementSample Movement;
        public bool HasMovement;
        public bool SprintHeld;
        public bool FlameDimmed;
        public double NextFlameUpdate;
        public double NextBorrowedStep;
        public Vector3 LastBorrowedStepPosition;
        public bool HasBorrowedStepPosition;
        public double NextRoomCrack;
        public int NextOptionalRoom;
        public readonly List<int> OptionalRooms = new List<int>();
        public readonly HashSet<int> MarkedDoors = new HashSet<int>();
        public readonly List<HorrorScheduledNoise> PendingNoises = new List<HorrorScheduledNoise>();
        public readonly List<HorrorEffectFact> Facts = new List<HorrorEffectFact>();
        public readonly Dictionary<(EntityId, CollapseHandEventKind), long> LastHandTicks = new Dictionary<(EntityId, CollapseHandEventKind), long>();
        public int ActorEffectsRevision;
        public readonly Dictionary<EntityId, int> AppliedActorEffects = new Dictionary<EntityId, int>();
        public readonly Dictionary<EntityId, float> GrabMultipliers = new Dictionary<EntityId, float>();
        public readonly HashSet<EntityId> BoundHunters = new HashSet<EntityId>();
    }
}
