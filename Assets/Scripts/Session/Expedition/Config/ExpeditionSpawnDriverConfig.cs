// ============================================================================
// ExpeditionSpawnDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Defines conservative physical admission for hunters added to an existing floor.
//   These dimensions reserve a body and test cover rather than teleporting into view.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Keep late-spawn capsule, navigation tolerance and viewing height designer-owned.
// DEPENDENCIES:
//   - Unity serialization only.
// USAGE NOTES:
//   Provisional dimensions must cover the largest configured roster body. Read-only at runtime.
// ============================================================================
using UnityEngine;
namespace Worsen.Session.Expedition
{
    [CreateAssetMenu(menuName = "Worsen/Expedition/Spawn Driver Config")]
    public sealed class ExpeditionSpawnDriverConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _radius = 0.4f;
        [SerializeField, Min(0.02f)] private float _height = 1.8f;
        [SerializeField, Min(0.001f)] private float _skin = 0.03f;
        [SerializeField, Min(0.001f)] private float _navigationTolerance = 0.25f;
        [SerializeField, Min(0.01f)] private float _eyeHeight = 1.6f;
        public float Radius => _radius;
        public float Height => _height;
        public float Skin => _skin;
        public float NavigationTolerance => _navigationTolerance;
        public float EyeHeight => _eyeHeight;
    }
}
