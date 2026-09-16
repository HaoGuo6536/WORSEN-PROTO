// ============================================================================
// HunterFactory.cs
// ============================================================================
// PURPOSE:
//   Creates hunter instances with disjoint identity values and typed dependencies.
//   The factory owns every spawned object and reverses registration and initialization
//   during despawn so scene lifetimes cannot leave stale consumers.
// ARCHITECTURAL ROLE:
//   Factory (§1c) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Resolve the configured archetype, spawn it and inject explicit read-only views.
// DEPENDENCIES:
//   - Player and Level read-only state views; Core spawn request and shared randomness.
// USAGE NOTES:
//   Scene-owned. No pooling; Initialize still resets all per-life data.
//   One factory mints negative ids; Player's existing factory mints positive ids.
//   Shared random is consumed by Hunter patrol selection and accepted hint imprecision.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterFactory : MonoBehaviour
    {
        private HunterProfile _profile;
        private System.Random _random;
        private IReadOnlyPlayerState _player;
        private IReadOnlyLevelState _level;
        private int _nextId = -1;
        private readonly Dictionary<EntityId, HunterManager> _spawned = new Dictionary<EntityId, HunterManager>();
        public void Configure(HunterProfile profile, System.Random random, IReadOnlyPlayerState player, IReadOnlyLevelState level)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _level = level ?? throw new ArgumentNullException(nameof(level));
        }
        public EntityId Spawn(SpawnRequest request)
        {
            if (_profile == null || _profile.Prefab == null || _random == null || _player == null || _level == null)
                throw new InvalidOperationException("Configure HunterFactory before spawning.");
            if (request.ArchetypeKey != _profile.ArchetypeKey) throw new ArgumentException("Unknown Hunter archetype.");
            EntityId id = new EntityId(_nextId--);
            GameObject instance = Instantiate(_profile.Prefab, request.Position, request.Rotation);
            HunterManager hunter = instance.GetComponent<HunterManager>();
            try
            {
                if (hunter == null) throw new InvalidOperationException("Hunter prefab requires HunterManager.");
                hunter.Initialize(_profile, new EntityContext(id, _random), _player, _level);
                _spawned.Add(id, hunter); HunterRegistry.Register(hunter); return id;
            }
            catch { Destroy(instance); throw; }
        }
        public void Despawn(EntityId id)
        {
            if (!_spawned.TryGetValue(id, out HunterManager hunter)) return;
            _spawned.Remove(id); HunterRegistry.Unregister(hunter);
            if (hunter == null) return;
            hunter.Teardown(); Destroy(hunter.gameObject);
        }
        private void OnDestroy()
        { foreach (EntityId id in new List<EntityId>(_spawned.Keys)) Despawn(id); }
    }
}
