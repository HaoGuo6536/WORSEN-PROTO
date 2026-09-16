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
//   - Store consumed sample identity and head offsets.
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
        public EntityId PlayerId;
        public long MovementTick = -1;
        public long TraversalTick = -1;
        public Vector3 EyePosition;
        public Vector3 Velocity;
        public float HeadingDegrees;
        public MovementState Movement;
        public bool LookBack;
        public float Pitch;
        public float HeadYaw;
        public float LookYaw;
        public float LookTweenFrom;
        public float LookTweenTo;
        public float LookTweenElapsed;
        public float LookTweenDuration;
        public float DetectionElapsed = -1f;
        public float ReboundElapsed = -1f;
        public float ReboundSign = 1f;
        public float Proximity;
        public bool DeathSnapped;
        public Quaternion DeathRotation = Quaternion.identity;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        public float HorizontalFieldOfView;
        public float VerticalFieldOfView;
        public float Roll;
    }
}
