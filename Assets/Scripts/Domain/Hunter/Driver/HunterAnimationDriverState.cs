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
//   - Retain the owned graph, clip handles and normalized mixer weights.
//   - Retain gait hysteresis, phase clocks and original Animator flags per life.
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
        public readonly float[] Weights = new float[7];
        public int ActiveClip = -1;
        public bool Running;
        public HunterAnimationPhase TriggeredPhase, LastModulePhase;
        public float TriggerElapsed, ModuleElapsed;
        public bool RestartTriggeredPose;
        public AnimatorCullingMode OriginalCullingMode;
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
