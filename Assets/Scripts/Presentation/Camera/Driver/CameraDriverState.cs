// ============================================================================
// CameraDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Retains the last player view sample and transient visual envelopes.
//   Keeping this data separate makes presentation timing reproducible without a scene.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Camera.
//
// KEY RESPONSIBILITIES:
//   - Retain progress/cancellation height, severity-scaled landing and stumble clocks.
//   - Retain runtime lens and comfort overrides across transient view resets.
//   - Keep unshaken aim separate from cosmetic banking and deterministic shake envelopes.
//   - Store consumed sample identity and head offsets.
//   - Latch catch start/target poses, approach/hold clocks and timing facts until reset.
//   - Store output pose and lens values for the Driver.
//
// DEPENDENCIES:
//   - Core identity and movement types; pure UnityEngine value types.
//
// USAGE NOTES:
//   - Scene-owned through CameraDriver; no engine operations or game rules.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Camera
{
    public sealed class CameraDriverState
    {
        public bool HasMovement;
        public float? BaseFieldOfView;
        public bool? TiltEnabled;
        public bool PunchEnabled = true;
        public EntityId PlayerId;
        public long MovementTick = -1;
        public long TraversalTick = -1;
        public long ProgressTick = -1, StumbleTick = -1;
        public bool VaultActive;
        public float VaultHeight, VaultReturnHeight, VaultReturnElapsed;
        public float LandingDepth, LandingElapsed, StumbleElapsed, StumbleDuration;
        public Vector3 EyePosition;
        public Vector3 Velocity;
        public float HeadingDegrees;
        public MovementState Movement;
        public bool LookBack;
        public float Pitch;
        public float HeadYaw;
        public float LookYaw;

        public float DetectionElapsed = -1f;
        public float ReboundElapsed = -1f;
        public float ReboundSign = 1f;
        public float Proximity;
        public bool Consumed;
        public float CatchElapsed, CatchApproachDuration, CatchHoldElapsed, CatchHoldDuration;
        public bool CatchHoldStarted, CatchHoldEnded;
        public Vector3 CatchStartPosition, CatchTargetPosition;
        public Quaternion CatchStartRotation = Quaternion.identity;
        public bool DeathSnapped;
        public Quaternion DeathRotation = Quaternion.identity;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        public float HorizontalFieldOfView;
        public float VerticalFieldOfView;
        public float Roll;
        public float SlideTurnRateDegrees;
        public float SlideBank;
        public float ShakeElapsed;
        public float ShakeDuration;
        public float ShakeStrength;
        public Quaternion AimRotation = Quaternion.identity;
    }
}
