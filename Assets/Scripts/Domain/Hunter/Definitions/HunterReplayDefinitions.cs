// ============================================================================
// HunterReplayDefinitions.cs
// ============================================================================
// PURPOSE:
//   Defines an opt-in kinematic replay instead of navigation and shared attacks.
//   Recorded poses are authoritative, including height and heading; contact is
//   an ordinary hit candidate, not permission to displace or pause the recording.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Carry exact body poses and the ordered segments traversed this tick.
//   - Expose presence and once-per-tick contact admission independently of a lunge.
// DEPENDENCIES:
//   - Hunter contact contract and UnityEngine value types only.
// USAGE NOTES:
//   The Manager applies every segment synchronously before publishing replay facts.
//   An absent replay still records ticks, but has no visible or collidable body.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    public readonly struct HunterReplayPose
    {
        public HunterReplayPose(Vector3 position, float headingDegrees)
        { Position = position; HeadingDegrees = headingDegrees; }
        public Vector3 Position { get; }
        public float HeadingDegrees { get; }
    }
    public interface IHunterKinematicReplayRules : IHunterContactRules
    {
        bool ReplayActive { get; }
        HunterReplayPose ReplayPose { get; }
        IReadOnlyList<HunterReplayPose> ReplayPoses { get; }
    }
}
