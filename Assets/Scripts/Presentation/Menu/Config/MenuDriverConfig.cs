// ============================================================================
// MenuDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Sets the restrained title and pause overlay's layout and palette.
//   Runtime preferences are carried separately as Core records, never stored here.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Menu.
// KEY RESPONSIBILITIES:
//   - Expose provisional readable sizing and backdrop values.
// DEPENDENCIES:
//   Unity ScriptableObject and value types only.
// USAGE NOTES:
//   Author under Resources/ScriptableObjects/Presentation/Menu.
// ============================================================================
using UnityEngine;
namespace Worsen.Presentation.Menu
{
    [CreateAssetMenu(fileName = "MenuDriverConfig", menuName = "Worsen/Menu/Driver Config")]
    public sealed class MenuDriverConfig : ScriptableObject
    {
        [SerializeField, Min(240f)] private float _width = 560f;
        [SerializeField, Min(12)] private int _fontSize = 20;
        [SerializeField, Min(0f)] private float _padding = 24f;
        [SerializeField, Min(24f)] private float _buttonHeight = 44f;
        [SerializeField] private Color _backdrop = new Color(0f, 0f, 0f, .96f);
        [SerializeField] private Color _text = Color.white;
        public float Width => _width;
        public int FontSize => _fontSize;
        public float Padding => _padding;
        public float ButtonHeight => _buttonHeight;
        public Color Backdrop => _backdrop;
        public Color Text => _text;
    }
}
