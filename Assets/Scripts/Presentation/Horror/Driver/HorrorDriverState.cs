// ============================================================================
// HorrorDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Stores current flashlight choices and all atmosphere restoration snapshots.
//   Keeping this data outside the Driver makes effect math and round resets directly testable.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Horror.
//
// KEY RESPONSIBILITIES:
//   - Retain current multiplier outputs, created objects and per-enemy cue state.
//   - Retain the exact camera, daylight, and render values to restore on release.
//
// DEPENDENCIES:
//   - Core EntityId; UnityEngine and rendering references stored without operating on them.
//
// USAGE NOTES:
//   Owned by HorrorDriver; scene-owned and never shared with another system.
//   No engine calls, events or mutable static state.
//
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Horror
{
    public sealed class HorrorDriverState
    {
        public bool OwnerEnabled;
        public bool FlashlightEnabled = true;
        public float FogMultiplier = 1f;
        public float FlashlightMultiplier = 1f;
        public float FogCurveStart;
        public float FogCurveEnd;
        public float FlashlightRange;
        public float FlashlightIntensity;
        public GameObject CueRoot;
        public Material CueMaterial;
        public bool OwnsCueMaterial;
        public readonly Dictionary<EntityId, HorrorAttackDriverState> Attacks =
            new Dictionary<EntityId, HorrorAttackDriverState>();
        public readonly Dictionary<EntityId, HorrorAttackCueDriver> Cues =
            new Dictionary<EntityId, HorrorAttackCueDriver>();
    }

    public sealed class HorrorAtmosphereDriverState
    {
        public bool AtmosphereCaptured;
        public AmbientMode PreviousAmbientMode;
        public Color PreviousAmbientLight;
        public float PreviousAmbientIntensity;
        public float PreviousReflectionIntensity;
        public Material PreviousSkybox;
        public Light PreviousSun;
        public bool PreviousBuiltInFog;
        public CameraClearFlags PreviousClearFlags;
        public Color PreviousBackgroundColor;
        public readonly List<Light> Daylights = new List<Light>();
        public readonly List<bool> DaylightEnabled = new List<bool>();
        public VolumeProfile PreviousFogProfile;
        public VolumeProfile RuntimeFogProfile;
        public bool PreviousFogVolumeEnabled;
        public GameObject LightRoot;
        public Light Flashlight;
        public Light NearFill;
    }

    public sealed class HorrorAttackDriverState
    {
        public bool HasSample;
        public int Phase;
        public float Progress;
    }

    public sealed class HorrorAttackCueDriverState
    {
        public LineRenderer Ring;
        public LineRenderer Arrow;
        public AudioSource Growl;
        public MaterialPropertyBlock Properties;
    }
}
