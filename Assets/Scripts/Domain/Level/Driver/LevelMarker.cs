// ============================================================================
// LevelMarker.cs
// ============================================================================
// PURPOSE:
//   Attaches stable authoring identity and traversal metadata to one scene object.
//   It captures engine positions as immutable records and announces enable/disable
//   facts to its owning Driver, preserving the Player-to-Core boundary.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by LevelDriver · Domain · Level.
// KEY RESPONSIBILITIES:
//   - Capture authored data and expose neutral traversal metadata to physics probes.
// DEPENDENCIES:
//   - Core level contracts; no other Domain system and no upper runtime layer.
// USAGE NOTES:
//   Scene-owned. Configured by the deterministic editor builder. Target is an
//   authored world-space landing position; no static events or game-state lookup.
//   Endpoint pairs are an explicit opt-in; graph bidirectionality alone never
//   changes a legacy Target or fabricates the opposite landing.
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Level
{
    [DisallowMultipleComponent]
    public sealed class LevelMarker : MonoBehaviour, ITraversalSurface, ITraversalEndpointPair
    {
        [SerializeField] private int _id;
        [SerializeField] private LevelMarkerKind _kind;
        [SerializeField] private int _roomId;
        [SerializeField] private int _targetRoomId;
        [SerializeField] private Vector3 _size = Vector3.one;
        [SerializeField] private bool _bidirectional = true;
        [SerializeField] private TraversalAccess _access = TraversalAccess.All;
        [SerializeField] private CakeAnchorType _anchorType;
        [SerializeField] private Vector3 _targetPosition;
        [SerializeField] private bool _hasEndpointPair;
        [SerializeField] private Vector3 _oppositeTargetPosition;

        public int SurfaceId => _id;
        public LevelMarkerKind MarkerKind => _kind;
        public Vector3 Target => _targetPosition;
        public bool HasEndpointPair => _hasEndpointPair;
        public Vector3 EndpointA => _targetPosition;
        public Vector3 EndpointB => _oppositeTargetPosition;
        public TraversalSurfaceKind Kind
        {
            get
            {
                switch (_kind)
                {
                    case LevelMarkerKind.VaultSurface: return TraversalSurfaceKind.Vault;
                    case LevelMarkerKind.ReboundSurface: return TraversalSurfaceKind.Rebound;
                    case LevelMarkerKind.SlideGate: return TraversalSurfaceKind.SlideGate;
                    case LevelMarkerKind.OneWayDrop: return TraversalSurfaceKind.OneWayDrop;
                    default: return TraversalSurfaceKind.None;
                }
            }
        }

        public event Action<LevelMarkerRecord> MarkerEnabled;
        public event Action<LevelMarkerRecord> MarkerDisabled;

        public LevelMarkerRecord Capture()
        {
            return new LevelMarkerRecord(_id, _kind, _roomId, _targetRoomId, transform.position,
                _size, _bidirectional, _access, _anchorType);
        }

        private void OnEnable() => MarkerEnabled?.Invoke(Capture());
        private void OnDisable() => MarkerDisabled?.Invoke(Capture());
    }
}
