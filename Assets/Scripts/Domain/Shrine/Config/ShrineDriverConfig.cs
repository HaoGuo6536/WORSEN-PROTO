// ============================================================================
// ShrineDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Tunes the temporary code-built shrine silhouettes without touching game rules.
//   These shapes identify the kind and spent state until authored art is supplied.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Domain · Shrine.
// KEY RESPONSIBILITIES:
//   - Supply mesh scale, label size and used-state color.
// DEPENDENCIES:
//   - Unity serialization and value types only.
// USAGE NOTES:
//   No runtime writes; ShrineDriver owns every generated object and material.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Shrine
{
    [CreateAssetMenu(menuName = "Worsen/Shrine/Shrine Driver Config")]
    public sealed class ShrineDriverConfig : ScriptableObject
    {
        [SerializeField] private Vector3 _size = new Vector3(0.6f, 1f, 0.6f);
        [SerializeField] private Color _color = new Color(0.6f, 0.5f, 0.2f);
        [SerializeField] private Color _spentColor = Color.gray;
        [SerializeField] private float _labelSize = 0.1f;
        public Vector3 Size => _size;
        public Color Color => _color;
        public Color SpentColor => _spentColor;
        public float LabelSize => _labelSize;
    }
}
