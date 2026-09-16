// ============================================================================
// AudioFeedbackDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Retains presentation observations to prevent repeating sounds every simulation tick.
//   It records no gameplay authority and can be discarded when a floor or run changes.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Remember prior health, movement, flashlight and phase observations.
//   - Retain posture initialization and the continuous exertion envelope independently of gameplay.
//   - Deduplicate committed event identities and maintain short pickup chains.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Owned by AudioDriver, reset on each generation and full teardown. No engine calls.
//
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Presentation.Audio
{
    public sealed class AudioFeedbackDriverState
    {
        public readonly List<AudioFeedbackCommand> Commands = new List<AudioFeedbackCommand>();
        public readonly Dictionary<int, float> Health = new Dictionary<int, float>();
        public readonly Dictionary<int, RoomDestructionSample> Rooms = new Dictionary<int, RoomDestructionSample>();
        public readonly Dictionary<int, GeneratedRoomSample> Layout = new Dictionary<int, GeneratedRoomSample>();
        public float Openness;
        public float LocalCollapse;
        public readonly Dictionary<long, long> EventTicks = new Dictionary<long, long>();
        public readonly HashSet<int> Pickups = new HashSet<int>();
        public readonly Dictionary<long, int> ProjectileEmitters = new Dictionary<long, int>();
        public readonly HashSet<long> FlyingProjectiles = new HashSet<long>();
        public int NextProjectileEmitter;
        public Vector3 Position;
        public MovementState Movement;
        public long MovementTick = -1;
        public bool HasMovement;
        public bool IsCrouched;
        public bool IsSprinting;
        public bool IsAlive = true;
        public bool IsCritical;
        public float ExertionGain;
        public bool ExertionActive;
        public long PickupTick = -1000;
        public int Chain;
        public bool HasFlashlight;
        public bool Flashlight;
        public int Generation = -1;
        public int Revision = -1;
        public ProgressionPhase Phase;
        public int Wallet;
        public int CurseCount;
        public int WardCharges;
    }
}
