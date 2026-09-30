// ============================================================================
// FogLookPresenter.cs
// ============================================================================
// PURPOSE:
//   Chooses optical colours for the floor's published fog look.
//   It does not receive progress or density state, keeping collapse timing intact.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Select the cold hospital palette or preserve the default black-mist palette.
// DEPENDENCIES:
//   - Own DriverConfig and Unity colour values only.
// USAGE NOTES:
//   Pure and stateless. Unknown tags retain the castle palette.
// ============================================================================
using UnityEngine;
namespace Worsen.Presentation.Fog
{
    public static class FogLookPresenter
    {
        public static Color Body(string look, FogDriverConfig config)
            => look == "cold-black-mist" ? config.ColdBodyColor : config.BodyColor;
        public static Color Thin(string look, FogDriverConfig config)
            => look == "cold-black-mist" ? config.ColdThinColor : config.ThinColor;
    }
}
