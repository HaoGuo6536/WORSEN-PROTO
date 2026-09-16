// ============================================================================
// PlayerFactory.cs
// ============================================================================
// PURPOSE:
//   Creates identified player instances from an explicitly supplied archetype and tears them down symmetrically.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Factory (§1c) · Domain · Player (Service system).
// KEY RESPONSIBILITIES:
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Scene-owned. No pooling is used; Initialize still resets all per-life state. Positive ids are minted by this sole scene Player factory.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Player
{
    public sealed class PlayerFactory : MonoBehaviour
    {
        [SerializeField] private PlayerProfile _profile;
        private System.Random _random;
        private int _nextId = 1;
        private readonly Dictionary<EntityId, PlayerManager> _spawned = new Dictionary<EntityId, PlayerManager>();
        public void Configure(PlayerProfile profile, System.Random random)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }
        public EntityId Spawn(SpawnRequest request)
        {
            if (_profile == null || _profile.Prefab == null || _random == null)
                throw new InvalidOperationException("Configure PlayerFactory with generated assets and the run random source before spawning.");
            if (request.ArchetypeKey != _profile.ArchetypeKey) throw new ArgumentException("Unknown Player archetype.");
            var id = new EntityId(_nextId++);
            GameObject instance = Instantiate(_profile.Prefab, request.Position, request.Rotation);
            PlayerManager player = instance.GetComponent<PlayerManager>();
            try
            {
                if (player == null) throw new InvalidOperationException("Player prefab has no PlayerManager.");
                player.Initialize(_profile, new EntityContext(id, _random));
                _spawned.Add(id, player);
                PlayerRegistry.Register(player);
                return id;
            }
            catch { Destroy(instance); throw; }
        }
        public void Despawn(EntityId id)
        {
            if (!_spawned.TryGetValue(id, out PlayerManager player)) return;
            _spawned.Remove(id);
            PlayerRegistry.Unregister(player);
            if (player == null) return;
            player.Teardown();
            Destroy(player.gameObject);
        }
        private void OnDestroy()
        {
            foreach (EntityId id in new List<EntityId>(_spawned.Keys)) Despawn(id);
        }
    }
}