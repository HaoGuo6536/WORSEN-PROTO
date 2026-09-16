// ============================================================================
// CueId.cs
// ============================================================================
//
// PURPOSE:
//   Names the audio cues emitted by run and presentation event routing.
//   Existing serialized numeric values remain stable; expansion cues are appended.
//   Posture and exertion cues distinguish committed movement feedback from traversal impacts.
//   Values cross system boundaries without exposing mutable runtime state.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Audio shared contracts.
//
// KEY RESPONSIBILITIES:
//   - Carry tick-stamped identity and immutable values between owning systems.
//   - Keep event payloads independent of Domain and Presentation implementations.
//
// DEPENDENCIES:
//   - Core definitions and pure UnityEngine value types only.
//
// USAGE NOTES:
//   Distances are metres and durations are seconds; ticks identify committed steps.
//   Constructors carry supplied values and perform no engine or gameplay operations.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    public enum CueId { Presence, Detection, Chase, Lose, Death, Footstep, ExitOpen, RoomTelegraph,
        Jump, Land, SlideStart, SlideLoop, SlideEnd, Vault, WallRebound, FlashlightOn,
        FlashlightOff, PlayerHit, PlayerCritical, GrabWarning, GrabStart, GrabEscape,
        GrabHit, Consumed, CakeCollect, GoldenCakeCollect, CakeChain, EnemyFootstep,
        EnemyWindup, EnemyAttack, EnemyMiss, EnemyHit, EnemyRecovery, EnemyScream,
        EnemyLost, RoomCrack, RoomTear, MistAdvance, RoomConsumed, TorchLoop, WindLoop,
        Drip, ChainCreak, DoorOpen, CurseOffer, CurseSelect, ShopOpen, ShopBuy, ShopReject,
        UiMove, UiConfirm, UiBack, Heal, WardBreak, RoundStart, Restart,
        ProjectileLaunch, ProjectileTravel, ProjectileImpact, SpikeWarning, SpikeErupt,
        TraversalMiss, FootstepWood, FootstepMetal, FootstepSoil,
        PostureRustle, SprintExertion
    }
}
