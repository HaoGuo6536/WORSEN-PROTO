// ============================================================================
// ProceduralChallengeConfig.cs
// ============================================================================
// PURPOSE:
//   Tunes optional movement challenges and authored threshold situations.
//   Referencing this asset opts into the provisional pacing; an unwired asset
//   leaves legacy generation untouched rather than silently changing old floors.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Bound round gates, execution deadlines, lane dimensions and freeze hearing range.
// DEPENDENCIES:
//   - Unity serialization only.
// USAGE NOTES:
//   Traversal, freeze and puzzle rooms share one floor budget. Null references
//   retain the authored legacy fixtures; deterministic content setup wires pacing.
//   TileShader is a required serialized build dependency when a puzzle is constructed.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    [CreateAssetMenu(menuName = "Worsen/Procedural/Challenge Config")]
    public sealed class ProceduralChallengeConfig : ScriptableObject
    {
        [SerializeField] private Shader _tileShader = null;
        public Shader TileShader => _tileShader;
        [SerializeField] private int _puzzleFirstRound = 3, _freezeFirstRound = 3;
        [SerializeField] private int _gimmickFirstRound = 3, _gimmickFullRound = 8;
        [SerializeField] private int _gimmickInitialBudget = 1, _gimmickMaximumBudget = 3;
        public int GimmickFirstRound => _gimmickFirstRound;
        public int GimmickFullRound => _gimmickFullRound;
        public int GimmickInitialBudget => _gimmickInitialBudget;
        public int GimmickMaximumBudget => _gimmickMaximumBudget;
        [SerializeField] private float _sequenceSeconds = 6f, _segmentSeconds = 1.5f;
        [SerializeField] private float _movingSpeed = 2f, _nearbyRadius = 6f;
        [SerializeField] private float _laneLength = 8f, _laneWidth = 1.6f;
        [SerializeField] private float _cageHeight = 2.5f, _panelThickness = 0.1f;
        [SerializeField] private float _contactHeight = 0.25f, _vaultHeight = 1f;
        [SerializeField] private float _clearance = 0.8f, _audibleDistance = 10f;
        [SerializeField] private Color _litColor = new Color(0.8f, 0.65f, 0.2f);
        [SerializeField] private Color _darkColor = new Color(0.04f, 0.04f, 0.04f);
        public int PuzzleFirstRound => _puzzleFirstRound;
        public int FreezeFirstRound => _freezeFirstRound;
        public float SequenceSeconds => _sequenceSeconds;
        public float SegmentSeconds => _segmentSeconds;
        public float MovingSpeed => _movingSpeed;
        public float NearbyRadius => _nearbyRadius;
        public float LaneLength => _laneLength;
        public float LaneWidth => _laneWidth;
        public float CageHeight => _cageHeight;
        public float PanelThickness => _panelThickness;
        public float ContactHeight => _contactHeight;
        public float VaultHeight => _vaultHeight;
        public float Clearance => _clearance;
        public float AudibleDistance => _audibleDistance;
        public Color LitColor => _litColor;
        public Color DarkColor => _darkColor;
    }
}
