// ============================================================================
// HorrorPresentationSettings.cs
// ============================================================================
//
// PURPOSE:
//   Carries immutable-by-convention lighting and warning tunables into pure math.
//   The snapshot separates Unity asset references from atmosphere and attack calculations.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Presentation · Horror.
//
// KEY RESPONSIBILITIES:
//   - Describe fog, flashlight, and warning dimensions and colors.
//
// DEPENDENCIES:
//   - UnityEngine value types only; no gameplay systems.
//
// USAGE NOTES:
//   Copied from HorrorDriverConfig. This struct contains data, never engine work.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.Horror
{
    public struct HorrorPresentationSettings
    {
        public float FogNearMeters;
        public float FogFarMeters;
        public float FlashlightRange;
        public float FlashlightIntensity;
        public float AttackRadius;
        public float WindupStartScale;
        public float WindupEndScale;
        public float AttackArrowLength;
        public float AttackHeight;
        public Color WindupColor;
        public Color ActiveColor;
        public Color RecoveryColor;
    }

    public struct HorrorAttackVisual
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Color Color;
        public float Radius;
        public float ArrowLength;
        public bool Visible;
        public bool PlayGrowl;
    }
}

