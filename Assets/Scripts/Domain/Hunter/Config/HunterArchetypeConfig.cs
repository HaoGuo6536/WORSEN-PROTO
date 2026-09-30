// ============================================================================
// HunterArchetypeConfig.cs
// ============================================================================
// PURPOSE:
//   Marks immutable per-archetype rule data referenced by a Hunter profile.
//   Construction belongs to the Manager, never to asset hooks, so configs cannot
//   own or accidentally share a controller's mutable recording state.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Provide the typed asset slot for specialised rule configurations.
// DEPENDENCIES:
//   - UnityEngine ScriptableObject authoring only.
// USAGE NOTES:
//   A null profile slot selects the default module; existing assets need no migration.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    public abstract class HunterArchetypeConfig : ScriptableObject
    {
        public virtual bool NeverLoses => false;
    }
}
