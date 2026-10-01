// ============================================================================
// HeldItemDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Stores the position and motion of the arm-free first-person item silhouette.
//   Designers can tune its lower-right placement independently of the camera and
//   flashlight. These defaults are provisional until the owner's in-game review.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · HeldItem.
// KEY RESPONSIBILITIES:
//   - Expose view placement, scale, raise/lower timing and small idle sway.
//   - Supply a build-retained unlit shader and placeholder palette.
// DEPENDENCIES:
//   Unity ScriptableObject and value types only.
// USAGE NOTES:
//   Mirrored asset: ScriptableObjects/Presentation/HeldItem/HeldItemDriverConfig.
//   Shader should be URP/Unlit; missing wiring warns and renders nothing.
// ============================================================================
using UnityEngine;
namespace Worsen.Presentation.HeldItem
{
    [CreateAssetMenu(fileName = "HeldItemDriverConfig", menuName = "Worsen/Held Item/Driver Config")]
    public sealed class HeldItemDriverConfig : ScriptableObject
    {
        [SerializeField] private Vector3 _position = new Vector3(.22f, -.12f, .45f);
        [SerializeField] private Vector3 _euler = new Vector3(-12f, -18f, 8f);
        [SerializeField, Min(.01f)] private float _scale = .10f;
        [SerializeField, Min(.01f)] private float _transitionSeconds = .16f;
        [SerializeField, Min(0f)] private float _lowerDistance = .35f;
        [SerializeField, Min(0f)] private float _swayAmplitude = .004f;
        [SerializeField, Min(.01f)] private float _swayPeriod = 2.4f;
        [SerializeField] private Color _bodyColor = new Color(.78f, .73f, .60f, 1f);
        [SerializeField] private Color _detailColor = new Color(.24f, .32f, .34f, 1f);
        [SerializeField] private Shader _shader = null;
        public Vector3 Position => _position;
        public Vector3 Euler => _euler;
        public float Scale => _scale;
        public float TransitionSeconds => _transitionSeconds;
        public float LowerDistance => _lowerDistance;
        public float SwayAmplitude => _swayAmplitude;
        public float SwayPeriod => _swayPeriod;
        public Color BodyColor => _bodyColor;
        public Color DetailColor => _detailColor;
        public Shader Shader => _shader;
    }
}
