// ============================================================================
// HorrorAfterglowPresenter.cs
// ============================================================================
// PURPOSE:
//   Presents an authoritative Afterglow safety window as a fading broken light.
//   Gameplay supplies the accepted duration; this calculator never freezes hunters.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Admit exact upgrade identities and finite broken-light observations.
//   - Advance independent, bounded light envelopes with injected time.
// DEPENDENCIES:
//   Core effect/interactable contracts and pure Unity value math.
// USAGE NOTES:
//   Repeated observations cannot extend a window. Relighting removes its record.
//   Safety duration and room-wide Mannequin gating belong to Domain/Session.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Presentation.Horror
{
    public static class HorrorAfterglowPresenter
    {
        public static bool Observe(HorrorAfterglowDriverState state, IReadOnlyActiveEffects effects, InteractableState light, float seconds)
        {
            if (light.Id <= 0 || light.Kind != InteractableKind.Light) return false;
            if (effects?.Has(new EffectId("afterglow")) != true || light.Value != InteractableStateValue.Broken
                || !Finite(seconds) || seconds <= 0f || !Finite(light.Position.x) || !Finite(light.Position.y) || !Finite(light.Position.z))
                return state.Lights.Remove(light.Id);
            if (state.Lights.TryGetValue(light.Id, out var existing))
            { existing.Remaining = Mathf.Min(existing.Remaining, seconds); return true; }
            state.Lights.Add(light.Id, new HorrorAfterglowLightDriverState { Light = light, Remaining = seconds, Duration = seconds });
            return true;
        }
        public static void Tick(HorrorAfterglowDriverState state, float dt)
        {
            if (!Finite(dt) || dt <= 0f) return;
            // Keep expired identities until floor reset/relight, so replayed break facts cannot re-arm them.
            foreach (var light in state.Lights.Values) light.Remaining = Mathf.Max(0f, light.Remaining - dt);
        }
        public static float Intensity(HorrorAfterglowLightDriverState light, float strength)
            => light.Duration > 0f && Finite(strength) ? Mathf.Clamp01(light.Remaining / light.Duration) * Mathf.Max(0f, strength) : 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
