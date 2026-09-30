// ============================================================================
// HunterAnimationDriverState.cs
// ============================================================================
// PURPOSE:
//   Stores passive transient data for the owning Hunter engine boundary.
//   Handles, collections and movement or visual bookkeeping belong to one life.
//   The owning Driver initializes and clears this data during entity reuse.
// ARCHITECTURAL ROLE:
//   DriverState (section 7c) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
//   - Retain injected sample-clock remainder, gaze commands and independent foot weights.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Passive data only; no simulation or engine operations.
// ============================================================================
using UnityEngine.Animations;
using UnityEngine;
using UnityEngine.Playables;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterAnimationDriverState
    {
        public PlayableGraph Graph;
        public AnimationMixerPlayable Mixer;
        public AnimationClipPlayable[] Clips;
        public readonly float[] Weights = new float[6];
        public int ActiveClip = -1;
        public double PoseRemainder;
        public float SampleRate, PoseDeltaTime, LookWeight;
        public Vector3 LookTarget;
        public bool Looking, CatchActive, HasHumanoidRig, OriginalRootMotion, IKApplied;
        public readonly float[] FootWeights = new float[2];
        public readonly Vector3[] FootPositions = new Vector3[2];
        public readonly Quaternion[] FootRotations = { Quaternion.identity, Quaternion.identity };
        public readonly RaycastHit[] FootHits = new RaycastHit[16];
    }
}
