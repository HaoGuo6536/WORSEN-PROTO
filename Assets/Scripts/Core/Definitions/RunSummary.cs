// ============================================================================
// RunSummary.cs
// ============================================================================
//
// PURPOSE:
//   Describes the final immutable outcome and measurements of a completed run.
//   Values cross system boundaries without exposing mutable runtime state.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Run shared contracts.
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
    public enum RunEndReason { Unknown, Escaped, Died }
    public readonly struct RunSummary
    {
        public RunSummary(double elapsedSeconds, int cakesCollected, int goldenCakesCollected, int chaseCount, int chasesEscaped, double totalChaseSeconds, RunEndReason endReason, int seed = 0, SceneKey scene = SceneKey.None)
        {
            ElapsedSeconds = elapsedSeconds;
            CakesCollected = cakesCollected;
            GoldenCakesCollected = goldenCakesCollected;
            ChaseCount = chaseCount;
            ChasesEscaped = chasesEscaped;
            TotalChaseSeconds = totalChaseSeconds;
            EndReason = endReason;
            Seed = seed;
            Scene = scene;
        }
        public double ElapsedSeconds { get; }
        public int CakesCollected { get; }
        public int GoldenCakesCollected { get; }
        public int ChaseCount { get; }
        public int ChasesEscaped { get; }
        public double TotalChaseSeconds { get; }
        public RunEndReason EndReason { get; }
        public int Seed { get; }
        public SceneKey Scene { get; }
    }
}
