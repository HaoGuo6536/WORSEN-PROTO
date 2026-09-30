// ============================================================================
// EnvironmentThemePresenter.cs
// ============================================================================
// PURPOSE:
//   Selects the fixture family and cosmetic flicker without touching layout.
//   Theme tags never move sockets or change Level's authoritative light state.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Prefer the published light-source tag for the current floor theme.
//   - Preserve castle flicker and compute a restrained fluorescent modulation.
// DEPENDENCIES:
//   - Own DriverState, EnvironmentPresenter and Unity value math only.
// USAGE NOTES:
//   Pure and stateless. Unknown room overrides use the castle appearance.
// ============================================================================
using UnityEngine;
namespace Worsen.Presentation.Environment
{
    public static class EnvironmentThemePresenter
    {
        public static bool IsFluorescent(EnvironmentDriverState state, int roomId)
        {
            if (state.RoomThemes.TryGetValue(roomId, out var room) && room.Theme != state.ThemeId)
                return room.Theme == "hospital";
            return state.LightSource == "fluorescent";
        }
        public static float LampBrightness(bool fluorescent, float elapsed, int identity, float gutter,
            float destruction, bool wick, float depth, float rate)
        {
            if (!fluorescent) return EnvironmentPresenter.LampBrightness(elapsed, identity, gutter, destruction, wick, false, 1f);
            float flicker = 1f - Mathf.Clamp01(depth) * (0.5f + 0.5f * Mathf.Sin(elapsed * rate + identity));
            return flicker * Mathf.Lerp(1f, 0.3f, wick ? 0f : Mathf.Clamp01(gutter)) * (1f - Mathf.Clamp01(destruction));
        }
        public static Vector3 PanelSize(Vector3 size, Vector3 envelope) => new Vector3(
            Mathf.Clamp(size.x, 0f, envelope.x), Mathf.Clamp(size.y, 0f, envelope.y), Mathf.Clamp(size.z, 0f, envelope.z));
    }
}
