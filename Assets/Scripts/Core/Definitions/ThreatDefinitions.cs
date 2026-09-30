// ============================================================================
// ThreatDefinitions.cs
// ============================================================================
// PURPOSE:
//   Names threat categories and progression event kinds shared by future owners.
//   Mutation facts identify a changed rule and its tell without explanatory text.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Threat and Progression contracts.
// KEY RESPONSIBILITIES:
//   - Carry catalogue identifiers and explicit committed ticks across layers.
// DEPENDENCIES:
//   - System only; no other project layers.
// USAGE NOTES:
//   Ids are nonblank, case-sensitive strings preserved exactly. A mutation targets
//   an archetype; applying it to retained instances is the progression owner's rule.
// ============================================================================
using System;

namespace Worsen.Core
{
    /// <summary>The behavioural category of a threat, not an active-body budget.</summary>
    public enum ThreatKind { Pursuer, Annoyance, Systemic }
    /// <summary>The kind of progression intervention selected for a run.</summary>
    public enum ProgressionEventKind { EnvironmentalHazard, HunterUpgrade, ExtraHunter, Random, HiddenMutation }

    /// <summary>A committed archetype rule mutation whose only presentation key is its tell id.</summary>
    public readonly struct RuleMutationFact
    {
        public RuleMutationFact(string threatArchetypeId, string mutationId, string tellId, long tick)
        {
            if (string.IsNullOrWhiteSpace(threatArchetypeId)) throw new ArgumentException("Archetype id required.", nameof(threatArchetypeId));
            if (string.IsNullOrWhiteSpace(mutationId)) throw new ArgumentException("Mutation id required.", nameof(mutationId));
            if (string.IsNullOrWhiteSpace(tellId)) throw new ArgumentException("Tell id required.", nameof(tellId));
            ThreatArchetypeId = threatArchetypeId; MutationId = mutationId; TellId = tellId; Tick = tick;
        }
        public string ThreatArchetypeId { get; }
        public string MutationId { get; }
        public string TellId { get; }
        public long Tick { get; }
    }
}
