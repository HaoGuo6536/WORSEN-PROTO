// ============================================================================
// ProceduralPuzzleDriverState.cs
// ============================================================================
// PURPOSE:
//   Holds one movement challenge's progress independently of its visible objects.
//   A fresh state is created per generated cage, so retries and later floors cannot
//   retain a solved gate or publish a previous floor's reward again.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Store progress, explicit elapsed time, contact debounce and one-shot publication.
// DEPENDENCIES:
//   - None.
// USAGE NOTES:
//   Data only. Presenter resets attempts; the owning sub-driver replaces state on setup.
// ============================================================================
namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralPuzzleDriverState
    {
        public int Next;
        public int LastContact = -1;
        public float Elapsed;
        public bool Started, Solved, Published, GateOpen;
    }
}
