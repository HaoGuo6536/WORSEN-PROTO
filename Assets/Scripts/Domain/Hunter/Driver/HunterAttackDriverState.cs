// ============================================================================
// HunterAttackDriverState.cs
// ============================================================================
// PURPOSE:
//   Stores passive transient data for the owning Hunter engine boundary.
//   Handles, collections and movement or visual bookkeeping belong to one life.
//   The owning Driver initializes and clears this data during entity reuse.
// ARCHITECTURAL ROLE:
//   DriverState (section 7c) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Passive data only; no simulation or engine operations.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterAttackDriverState
    {
        public readonly List<LineRenderer> Warnings = new List<LineRenderer>();
        public readonly List<HunterProjectileDriverState> Projectiles = new List<HunterProjectileDriverState>();
        public readonly List<GameObject> Spikes = new List<GameObject>();
        public readonly List<Vector3> GroundPoints = new List<Vector3>();
        public EntityId Hunter;
        public string ArchetypeKey;
        public long Tick;
        public int NextEmitterId;
        public System.Func<Collider, bool> IsTarget;
        public int Serial;
        public HunterAttackStyle Style;
        public Vector3 Origin;
        public Vector3 Target;
        public float Radius;
        public float Range;
        public bool Split;
        public float SpikeSeconds;
        public Material FallbackMaterial;
    }
    public sealed class HunterProjectileDriverState
    {
        public GameObject Visual;
        public Vector3 Position;
        public Vector3 Direction;
        public float Speed;
        public float Radius;
        public int EmitterId;
        public float Remaining;
        public int Serial;
    }
}
