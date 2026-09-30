// ============================================================================
// ProceduralConfig.cs
// ============================================================================
// PURPOSE:
//   Defines bounded castle growth, interior elevations and typed candidate density. Later
//   rounds grow within a fixed room budget while keeping wide, ordinary walking
//   routes available without an upgrade or special traversal requirement.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Store layout growth, room dimensions and candidate budgets/type preferences.
//   - Keep broad cloister/gallery rooms enclosed beneath a higher ceiling.
//   - Tune first-contact path separation, bounded retries and world-object density.
//   - Weight post-hub footprints and gate reserved gaps and optional pocket chains.
//   - Gate upper storeys in extension cells and mirror the base Player ledge envelope.
// DEPENDENCIES:
//   - UnityEngine serialization only; no other gameplay system.
// USAGE NOTES:
//   Designer data only. The controller rejects invalid combinations rather
//   than silently building an incomplete or unbounded map. Legacy cake-line fields
//   remain serialized for compatibility; Floor owns actual cake selection.
//   Room-count budgets govern the connected component. PocketRoomCount is a separate
//   optional budget. The fixed four/six-room walking loop remains single-cell.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    [CreateAssetMenu(menuName = "Worsen/Procedural/Config")]
    public sealed class ProceduralConfig : ScriptableObject
    {
        [SerializeField] private int _initialRoomCount = 7;
        [SerializeField] private int _roomsPerRound = 2;
        [SerializeField] private int _maximumRoomCount = 15;
        [SerializeField, Min(1)] private int _multiCellStartRound = 1;
        [SerializeField, Min(0f)] private float _oneCellWeight = 0.55f;
        [SerializeField, Min(0f)] private float _twoCellWeight = 0.3f;
        [SerializeField, Min(0f)] private float _threeCellWeight = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _lShapeWeight = 0.4f;
        [SerializeField, Min(1)] private int _gapStartRound = 2;
        [SerializeField, Range(0f, 1f)] private float _gapProbability = 0.35f;
        [SerializeField, Range(1, 8)] private int _maximumGapCells = 3;
        [SerializeField, Range(0f, 1f)] private float _pocketProbability = 0.5f;
        [SerializeField, Range(1, 3)] private int _pocketRoomCount = 2;
        public int MultiCellStartRound => _multiCellStartRound;
        public float OneCellWeight => _oneCellWeight;
        public float TwoCellWeight => _twoCellWeight;
        public float ThreeCellWeight => _threeCellWeight;
        public float LShapeWeight => _lShapeWeight;
        public int GapStartRound => _gapStartRound;
        public float GapProbability => _gapProbability;
        public int MaximumGapCells => _maximumGapCells;
        public float PocketProbability => _pocketProbability;
        public int PocketRoomCount => _pocketRoomCount;
        [SerializeField, Min(1)] private int _multiFloorStartRound = 3;
        [SerializeField, Range(0f, 1f)] private float _storeyProbability = 0.65f;
        [SerializeField] private float _storeyHeight = 3.2f;
        [SerializeField] private float _baseLedgeMinimumHeight = 0.5f;
        [SerializeField] private float _baseLedgeMaximumHeight = 1.8f;
        [SerializeField] private float _baseLedgeReach = 1.2f;
        [SerializeField] private bool _baseReboundSupported = true;
        public int MultiFloorStartRound => _multiFloorStartRound;
        public float StoreyProbability => _storeyProbability;
        public float StoreyHeight => _storeyHeight;
        public float BaseLedgeMinimumHeight => _baseLedgeMinimumHeight;
        public float BaseLedgeMaximumHeight => _baseLedgeMaximumHeight;
        public float BaseLedgeReach => _baseLedgeReach;
        public bool BaseReboundSupported => _baseReboundSupported;
        [SerializeField] private float _roomSize = 12f;
        [SerializeField] private float _roomHeight = 4f;
        [SerializeField] private float _doorWidth = 3.2f;
        [SerializeField] private float _doorHeight = 2.8f;
        [SerializeField] private float _doorOffset = 2f;
        [SerializeField] private int _cakesPerLine = 5;
        [SerializeField] private float _cakeSpacing = 2f;
        [SerializeField] private float _cakeLineOffset = 2f;
        [SerializeField, Range(1, 5)] private int _minimumCandidatesPerRoom = 3;
        [SerializeField, Range(1, 5)] private int _maximumCandidatesPerRoom = 5;
        [SerializeField, Min(0f)] private float _flowPreference = 1f;
        [SerializeField, Min(0f)] private float _precisionPreference = 2f;
        [SerializeField, Min(0f)] private float _detourPreference = 2f;
        [SerializeField, Min(0f)] private float _riskPreference = 2f;
        [SerializeField, Min(0f)] private float _verticalPreference = 2f;
        [SerializeField, Min(0.01f)] private float _candidatePerimeterInset = 1.3f;
        [SerializeField] private float _anchorHeight = 0.05f;
        [SerializeField] private float _spawnHeight = 0.1f;
        [SerializeField] private float _spawnSideOffset = 4f;
        [SerializeField, Min(1)] private int _minimumHunterSpawnRooms = 2;
        [SerializeField, Range(0, 8)] private int _generationRetries = 3;
        [SerializeField, Range(0f, 1f)] private float _ordinaryDoorFraction = 0.35f;
        [SerializeField, Range(0, 4)] private int _knockablePropsPerRoom = 2;
        public int MinimumHunterSpawnRooms => _minimumHunterSpawnRooms;
        public int GenerationRetries => _generationRetries;
        public float OrdinaryDoorFraction => _ordinaryDoorFraction;
        public int KnockablePropsPerRoom => _knockablePropsPerRoom;
        [SerializeField] private Vector2 _origin = Vector2.zero;
        [SerializeField] private bool _castleModules = true;
        [SerializeField] private float _castleHeight = 7f;
        [SerializeField] private float _highCeilingHeight = 10f;
        [SerializeField] private float _upperDeckHeight = 2.4f;
        public bool CastleModules => _castleModules;
        public float CastleHeight => _castleHeight;
        public float HighCeilingHeight => _highCeilingHeight;
        public float UpperDeckHeight => _upperDeckHeight;
        public int InitialRoomCount => _initialRoomCount;
        public int RoomsPerRound => _roomsPerRound;
        public int MaximumRoomCount => _maximumRoomCount;
        public float RoomSize => _roomSize;
        public float RoomHeight => _roomHeight;
        public float DoorWidth => _doorWidth;
        public float DoorHeight => _doorHeight;
        public float DoorOffset => _doorOffset;
        public int CakesPerLine => _cakesPerLine;
        public float CakeSpacing => _cakeSpacing;
        public float CakeLineOffset => _cakeLineOffset;
        public int MinimumCandidatesPerRoom => _minimumCandidatesPerRoom;
        public int MaximumCandidatesPerRoom => _maximumCandidatesPerRoom;
        public float FlowPreference => _flowPreference;
        public float PrecisionPreference => _precisionPreference;
        public float DetourPreference => _detourPreference;
        public float RiskPreference => _riskPreference;
        public float VerticalPreference => _verticalPreference;
        public float CandidatePerimeterInset => _candidatePerimeterInset;
        public float AnchorHeight => _anchorHeight;
        public float SpawnHeight => _spawnHeight;
        public float SpawnSideOffset => _spawnSideOffset;
        public Vector2 Origin => _origin;
    }
}
