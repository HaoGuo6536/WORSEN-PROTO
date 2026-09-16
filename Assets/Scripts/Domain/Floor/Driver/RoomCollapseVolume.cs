// ============================================================================
// RoomCollapseVolume.cs
// ============================================================================
// PURPOSE:
//   Applies one room warning, collision enclosure and carved navigation exclusion.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FloorDriver · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Scene-owned. Four boundary blockers enclose all doors because LevelEdge has no doorway geometry. Carving blocks all actors, including hunters; no costs are treated as impassable. A closure overlap sweep detects stationary occupants immediately.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;

namespace Worsen.Domain.Floor
{
    [RequireComponent(typeof(BoxCollider), typeof(NavMeshObstacle))]
    public sealed class RoomCollapseVolume : MonoBehaviour
    {
        private int _roomId;
        private BoxCollider _volume;
        private NavMeshObstacle _obstacle;
        private GameObject[] _blockers;
        private Light _warning;
        private RoomPhase _phase;
        public event Action<Collider, int> LethalContact;
        public void Configure(int roomId, Vector3 size, GameObject[] blockers, Light warning)
        {
            _roomId = roomId; _blockers = blockers; _warning = warning;
            _volume = GetComponent<BoxCollider>(); _volume.size = size; _volume.isTrigger = true;
            _obstacle = GetComponent<NavMeshObstacle>(); _obstacle.shape = NavMeshObstacleShape.Box;
            _obstacle.size = size; _obstacle.carving = true; _obstacle.carveOnlyStationary = false;
            _obstacle.enabled = false; _phase = RoomPhase.Open;
            foreach (var blocker in _blockers) blocker.SetActive(false);
            _warning.enabled = false;
        }
        public void ApplyPhase(RoomPhase phase, Color warningColor, Color closedColor)
        {
            _phase = phase;
            _warning.enabled = phase != RoomPhase.Open;
            _warning.color = phase == RoomPhase.Closed ? closedColor : warningColor;
            if (phase != RoomPhase.Closed) return;
            _obstacle.enabled = true;
            foreach (var blocker in _blockers) blocker.SetActive(true);
            Physics.SyncTransforms();
            foreach (var occupant in Physics.OverlapBox(_volume.bounds.center, _volume.bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                LethalContact?.Invoke(occupant, _roomId);
        }
        public void SetWarningIntensity(float intensity) { if (_warning != null) _warning.intensity = intensity; }
        private void OnTriggerEnter(Collider other) { if (_phase == RoomPhase.Closed) LethalContact?.Invoke(other, _roomId); }
        private void OnTriggerStay(Collider other) { if (_phase == RoomPhase.Closed) LethalContact?.Invoke(other, _roomId); }
    }
}