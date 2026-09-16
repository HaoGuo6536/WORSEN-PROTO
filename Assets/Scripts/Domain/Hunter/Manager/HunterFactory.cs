// ============================================================================
// HunterFactory.cs
// ============================================================================
// PURPOSE:
//   Resolves exact configured Hunter roster keys before creating entities.
//   Validated profiles receive distinct identities and explicit state dependencies.
//   Unknown keys never silently spawn a default archetype.
// ARCHITECTURAL ROLE:
//   Factory (section 1c) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Scene-owned. No pooling; Initialize resets every life. Preserves disjoint negative IDs.
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
        private readonly Dictionary<string, HunterProfile> _profiles = new Dictionary<string, HunterProfile>(StringComparer.Ordinal);
        private System.Random _random;
        private IReadOnlyPlayerState _player;
        private IReadOnlyLevelState _level;
        private int _nextId = -1;
        private readonly Dictionary<EntityId, HunterManager> _spawned = new Dictionary<EntityId, HunterManager>();
        public void Configure(HunterProfile profile, System.Random random, IReadOnlyPlayerState player, IReadOnlyLevelState level)
            => Configure(new[] { profile }, random, player, level);
        public void Configure(IReadOnlyList<HunterProfile> profiles, System.Random random, IReadOnlyPlayerState player, IReadOnlyLevelState level)
        {
            if (profiles == null || profiles.Count == 0) throw new ArgumentException("At least one Hunter profile is required.", nameof(profiles));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (level == null) throw new ArgumentNullException(nameof(level));
            var validated = new Dictionary<string, HunterProfile>(StringComparer.Ordinal);
            foreach (HunterProfile profile in profiles)
            {
                if (profile == null || string.IsNullOrWhiteSpace(profile.ArchetypeKey) || profile.Prefab == null)
                    throw new ArgumentException("Hunter profiles require an archetype key and prefab.", nameof(profiles));
                if (validated.ContainsKey(profile.ArchetypeKey)) throw new ArgumentException("Duplicate Hunter archetype key.", nameof(profiles));
                validated.Add(profile.ArchetypeKey, profile);
            }
            _profiles.Clear();
            foreach (var pair in validated) _profiles.Add(pair.Key, pair.Value);
            _random = random; _player = player; _level = level;
        }
        public EntityId Spawn(SpawnRequest request)
        {
            if (_profiles.Count == 0 || _random == null || _player == null || _level == null)
                throw new InvalidOperationException("Configure HunterFactory before spawning.");
            if (string.IsNullOrEmpty(request.ArchetypeKey) || !_profiles.TryGetValue(request.ArchetypeKey, out HunterProfile profile))
                throw new ArgumentException("Unknown Hunter archetype.");
            EntityId id = new EntityId(_nextId--);
            GameObject instance = Instantiate(profile.Prefab, request.Position, request.Rotation);
            HunterManager hunter = instance.GetComponent<HunterManager>();
            try
            {
                if (hunter == null) throw new InvalidOperationException("Hunter prefab requires HunterManager.");
                hunter.Initialize(profile, new EntityContext(id, _random), _player, _level);
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
