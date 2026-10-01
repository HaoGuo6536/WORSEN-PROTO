// ============================================================================
// HorrorWebPresenter.cs
// ============================================================================
// PURPOSE:
//   Converts committed Weaver facts into finite-lived warning and web visuals.
//   Cancellation and launch remove only the owning hunter's warning; independent
//   duplicate hunters and multiple doorway nests cannot erase each other's cues.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Deduplicate facts, reject stale warnings and expire visuals using injected time.
// DEPENDENCIES:
//   - Core Weaver facts and the Horror web state only.
// USAGE NOTES:
//   WetClick is the warning entry. WebGlow after DoorwayWebbed decorates the same
//   object rather than creating a second overlapping surface. No engine calls.
// ============================================================================
using System;
using System.Linq;
using Worsen.Core;
namespace Worsen.Presentation.Horror
{
    public sealed class HorrorWebPresenter
    {
        public void Observe(HorrorWebDriverState state, WeaverFact fact)
        {
            if (!fact.Hunter.IsValid) return;
            var key = (fact.Hunter, fact.Kind, fact.Serial);
            if (state.Ticks.TryGetValue(key, out long tick) && fact.Tick <= tick) return;
            state.Ticks[key] = fact.Tick;
            var warning = (fact.Hunter, WeaverFactKind.WetClick, 0);
            if (fact.Kind == WeaverFactKind.WarningCancelled || fact.Kind == WeaverFactKind.WebLaunched)
            {
                if (!state.Ticks.TryGetValue(warning, out long started) || fact.Tick >= started)
                { state.Visuals.Remove(warning); state.Ticks[warning] = fact.Tick; }
                return;
            }
            if (fact.Kind != WeaverFactKind.WetClick && fact.Kind != WeaverFactKind.WebGlow && fact.Kind != WeaverFactKind.DoorwayWebbed) return;
            if (!(fact.Duration > 0f) || float.IsInfinity(fact.Duration)) return;
            if (fact.Kind == WeaverFactKind.WebGlow && state.Visuals.ContainsKey((fact.Hunter, WeaverFactKind.DoorwayWebbed, fact.Serial))) return;
            if (fact.Kind == WeaverFactKind.DoorwayWebbed) state.Visuals.Remove((fact.Hunter, WeaverFactKind.WebGlow, fact.Serial));
            state.Visuals[key] = new HorrorWebVisualDriverState { Fact = fact, Remaining = fact.Duration };
        }
        public void Tick(HorrorWebDriverState state, float delta)
        {
            if (!(delta > 0f) || float.IsInfinity(delta)) return;
            foreach (var key in state.Visuals.Keys.ToArray())
            {
                var visual = state.Visuals[key]; visual.Remaining = Math.Max(0f, visual.Remaining - delta);
                if (visual.Remaining <= 0f) state.Visuals.Remove(key);
            }
        }
        public void Reset(HorrorWebDriverState state) { state.Visuals.Clear(); state.Ticks.Clear(); }
    }
}
