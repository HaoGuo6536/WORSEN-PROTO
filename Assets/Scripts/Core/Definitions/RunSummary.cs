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
//   - Carry optional death, grab, exit timing and depth details for later producers.
//   - Distinguish a penalized bail from a normal escape without changing EndReason.
//
// DEPENDENCIES:
//   - Core definitions and pure UnityEngine value types only.
//
// USAGE NOTES:
//   Distances are metres and durations are seconds; ticks identify committed steps.
//   Constructors carry supplied values and perform no engine or gameplay operations.
//   None/empty means no reported killer; -1 means exit-to-escape timing is unavailable.
//   Depth zero means not reported. Legacy EndReason is never reinterpreted.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    public enum RunEndReason { Unknown, Escaped, Died }
    /// <summary>The completed run's measurements with optional detailed outcome facts.</summary>
    public readonly struct RunSummary
    {
        public RunSummary(double elapsedSeconds, int cakesCollected, int goldenCakesCollected, int chaseCount,
            int chasesEscaped, double totalChaseSeconds, RunEndReason endReason, int seed = 0,
            SceneKey scene = SceneKey.None, DeathCause deathCause = DeathCause.None,
            string killerArchetypeId = "", int grabsEscaped = 0, double secondsFromExitOpenToEscape = -1,
            int depthReached = 0, bool bailed = false)
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
            DeathCause = deathCause;
            KillerArchetypeId = killerArchetypeId ?? string.Empty;
            GrabsEscaped = grabsEscaped;
            SecondsFromExitOpenToEscape = secondsFromExitOpenToEscape;
            DepthReached = depthReached;
            Bailed = bailed;
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
        public DeathCause DeathCause { get; }
        public string KillerArchetypeId { get; }
        public int GrabsEscaped { get; }
        public double SecondsFromExitOpenToEscape { get; }
        public int DepthReached { get; }
        public bool Bailed { get; }
    }
}
