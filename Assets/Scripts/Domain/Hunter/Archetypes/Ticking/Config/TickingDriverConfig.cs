// ============================================================================
// TickingDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Authors the key placeholder's contact volume and conservative placement probes.
//   Audio identifiers are facts for the Audio owner, never locally played clips.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Domain · Hunter Ticking.
// KEY RESPONSIBILITIES:
//   - Keep key prefab, navigation tolerances and sound-set identifiers designer-owned.
//   - Supply the same corner lookahead default as cake guidance.
// DEPENDENCIES:
//   - UnityEngine asset and value types; local sound identifiers only.
// USAGE NOTES:
//   Walkable-only queries reject gaps, links and blocked direct detours. Missing
//   key assets fail initialization rather than silently producing an invisible key.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Ticking
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Ticking Driver Config")]
    public sealed class TickingDriverConfig : ScriptableObject
    {
        [SerializeField] private GameObject _keyPrefab = null;
        [SerializeField, Min(.01f)] private float _sampleRadius = .5f;
        [SerializeField, Min(.01f)] private float _maximumElevation = .35f;
        [SerializeField, Min(.01f)] private float _clearanceRadius = .3f;
        [SerializeField, Min(.01f)] private float _clearanceHeight = .8f;
        [SerializeField, Min(.01f)] private float _contactRadius = .65f;
        [SerializeField, Min(0f)] private float _directionCornerSkipDistance = 1f;
        [SerializeField] private LayerMask _obstacleMask = ~0;
        [SerializeField] private string[] _soundIds = { "ticking.tick", "ticking.winding", "ticking.stop", "ticking.wake", "ticking.key-appeared" };
        public GameObject KeyPrefab => _keyPrefab;
        public float SampleRadius => Mathf.Max(.01f, _sampleRadius);
        public float MaximumElevation => Mathf.Max(.01f, _maximumElevation);
        public float ClearanceRadius => Mathf.Max(.01f, _clearanceRadius);
        public float ClearanceHeight => Mathf.Max(ClearanceRadius, _clearanceHeight);
        public float ContactRadius => Mathf.Max(.01f, _contactRadius);
        public float DirectionCornerSkipDistance => Mathf.Max(0f, _directionCornerSkipDistance);
        public int ObstacleMask => _obstacleMask;
        public System.Collections.Generic.IReadOnlyList<string> SoundIds => _soundIds;
    }
}
