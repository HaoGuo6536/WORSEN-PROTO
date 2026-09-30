// ============================================================================
// DefaultHunterController.cs
// ============================================================================
// PURPOSE:
//   Leaves shared Hunter decisions untouched for the five existing profiles.
//   Common neutral hooks live in the parent HunterArchetypeController so sibling
//   plug-ins do not depend on this legacy compatibility module.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Supply identity perception and utility filters and no movement override.
// DEPENDENCIES:
//   - Hunter-local definitions and Core sight values only.
// USAGE NOTES:
//   Stateless default module. Manager creates a separate module for each life.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Default
{
    public class DefaultHunterController : HunterArchetypeController
    {
        public override bool AllowsLegacyTraits => GetType() == typeof(DefaultHunterController);
    }
}
