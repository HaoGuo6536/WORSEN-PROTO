// ============================================================================
// WeaverDefinitions.cs
// ============================================================================
// PURPOSE:
//   Names Weaver-local shot decisions for its pure decision module. Shared sweep
//   observations live in parent Hunter Definitions so siblings stay independent.
//   Outward web hits and cue facts live in Core so their consumers need no
//   dependency on Hunter implementation types.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Keep sweep evidence tick-tagged and web slow separate from damage or grabs.
// DEPENDENCIES:
//   - Core identity and UnityEngine value types only.
// USAGE NOTES:
//   Web Cutter modifies slow strength, not duration: Player applies
//   1 - (1 - SlowMultiplier) * SlowStrengthMultiplier, without disabling slide.
//   Cue facts name sound/presentation observations, never directly play assets.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Domain.Hunter.Archetypes.Weaver
{
    public enum WeaverAction { None, Reposition, Shoot }



}
