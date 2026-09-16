// ============================================================================
// ProceduralConfig.cs
// ============================================================================
// PURPOSE:
//   Defines the size and objective density of generated enclosed floors. Later
//   rounds grow within a fixed room budget while keeping wide, ordinary walking
//   routes available without an upgrade or special traversal requirement.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Store layout growth, room dimensions and straight cake-line spacing.
// DEPENDENCIES:
//   - UnityEngine serialization only; no other gameplay system.
// USAGE NOTES:
//   Designer data only. The controller rejects invalid combinations rather
//   than silently building an incomplete or unbounded map.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    [CreateAssetMenu(menuName = "Worsen/Procedural/Config")]
    public sealed class ProceduralConfig : ScriptableObject
    {
        [SerializeField] private int _initialRoomCount = 5;
        [SerializeField] private int _roomsPerRound = 2;
        [SerializeField] private int _maximumRoomCount = 15;
        [SerializeField] private float _roomSize = 12f;
        [SerializeField] private float _roomHeight = 4f;
        [SerializeField] private float _doorWidth = 3.2f;
        [SerializeField] private float _doorHeight = 2.8f;
        [SerializeField] private float _doorOffset = 2f;
        [SerializeField] private int _cakesPerLine = 5;
        [SerializeField] private float _cakeSpacing = 2f;
        [SerializeField] private float _cakeLineOffset = 2f;
        [SerializeField] private float _anchorHeight = 0.05f;
        [SerializeField] private float _spawnHeight = 0.1f;
        [SerializeField] private float _spawnSideOffset = 4f;
        [SerializeField] private Vector2 _origin = Vector2.zero;
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
        public float AnchorHeight => _anchorHeight;
        public float SpawnHeight => _spawnHeight;
        public float SpawnSideOffset => _spawnSideOffset;
        public Vector2 Origin => _origin;
    }
}
