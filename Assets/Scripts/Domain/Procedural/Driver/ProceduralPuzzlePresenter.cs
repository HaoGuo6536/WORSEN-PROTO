// ============================================================================
// ProceduralPuzzlePresenter.cs
// ============================================================================
// PURPOSE:
//   Computes four movement challenges from contact, completed-vault and motion data.
//   Nothing reads an engine clock: explicit elapsed time makes failures, resets and
//   one-shot solved facts reproducible independently of rendering or physics timing.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Reset incorrect sequences, dark-path departures and missed deadlines.
//   - Keep the movement door transient and publish each solution exactly once.
// DEPENDENCIES:
//   - Own state, kind and scalar configuration only.
// USAGE NOTES:
//   Three steps per authored lane. Index 3 is inside the optional reward cage.
//   Vault input must describe a completed traversal of the named obstacle, not a jump.
// ============================================================================
using System;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralPuzzlePresenter
    {
        public bool Step(ProceduralPuzzleDriverState state, ProceduralPuzzleKind kind, float dt,
            int tile, float speed, bool nearby, float window, float segment, float minimumSpeed,
            bool contact = false, bool vault = false)
        {
            if (!(dt >= 0f) || float.IsInfinity(dt) || !(speed >= 0f) || float.IsInfinity(speed) ||
                !(window > 0f) || float.IsInfinity(window) || !(segment > 0f) || float.IsInfinity(segment) ||
                !(minimumSpeed > 0f) || float.IsInfinity(minimumSpeed) || tile < -1 || tile > 3)
                throw new ArgumentException("Invalid puzzle input.");
            if (kind == ProceduralPuzzleKind.MovingDoor)
            {
                state.GateOpen = nearby && speed > minimumSpeed;
                if (state.GateOpen && tile == 3) state.Solved = true;
                if (!state.GateOpen && !state.Solved) Reset(state);
            }
            else if (!state.Solved)
            {
                if (state.Started) state.Elapsed += dt;
                bool expired = state.Started && (kind == ProceduralPuzzleKind.DimmingPath ?
                    tile < 0 || tile > 2 || state.Elapsed >= (tile + 1) * segment : state.Elapsed >= window);
                if (expired || !nearby) Reset(state);
                else if ((contact && kind != ProceduralPuzzleKind.TimedVaults) || (vault && kind == ProceduralPuzzleKind.TimedVaults))
                {
                    if (tile != state.LastContact)
                    {
                        state.LastContact = tile;
                        if (tile != state.Next || tile < 0 || tile > 2) Reset(state);
                        else
                        {
                            state.Started = true;
                            state.Next++;
                            state.Solved = state.Next == 3;
                        }
                    }
                }
                if (tile < 0) state.LastContact = -1;
                state.GateOpen = state.Solved;
            }
            if (!state.Solved || state.Published) return false;
            state.Published = true;
            return true;
        }
        public float Brightness(ProceduralPuzzleDriverState state, int tile, float segment)
            => !state.Started || state.Solved ? 1f : Math.Max(0f, Math.Min(1f, (tile + 1) - state.Elapsed / segment));
        public void Fail(ProceduralPuzzleDriverState state) { if (!state.Solved) Reset(state); }
        private static void Reset(ProceduralPuzzleDriverState state)
        { state.Next = 0; state.LastContact = -1; state.Elapsed = 0f; state.Started = false; state.GateOpen = false; }
    }
}
