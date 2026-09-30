// ============================================================================
// FloorHandController.cs
// ============================================================================
// PURPOSE:
//   Controls a boundary warning, escapable slow, set damage plus throw, and cooldown.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Consume the floor's Wax Heart before any per-player Wax Ward, never both.
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
//   - Break one grab per armed Wax Ward and publish room phases without mutating Player.
//   - Reject protected contacts and release warnings/grabs from the Player read-only effect view.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
//   - Player IReadOnlyPlayerEffectState, injected by FloorManager; never mutable Player state.
// USAGE NOTES:
//   Scene-owned through FloorManager/FloorDriver. Time is supplied by the owner.
//   No persistent singleton, global settings, or independent update loop.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Floor
{
    public sealed class FloorHandController
    {
        private readonly FloorHandBehaviorState _state;
        private readonly FloorConfig _config;
        public FloorHandController(FloorHandBehaviorState state, FloorConfig config)
        { _state = state ?? throw new ArgumentNullException(nameof(state)); _config = config ?? throw new ArgumentNullException(nameof(config)); }

        public bool Target(EntityId player, out int room, out int hand)
        {
            room = 0; hand = -1;
            if (!_state.Contacts.TryGetValue(player, out var contact) ||
                (contact.Phase != FloorHandPhase.Warning && contact.Phase != FloorHandPhase.Grabbed)) return false;
            room = contact.RoomId; hand = contact.HandId; return true;
        }

        public bool Tick(EntityId player, bool alive, FloorHandProbe probe, float dt, long tick, out CollapseHandFact fact,
            IReadOnlyPlayerEffectState effects = null)
        {
            fact = default;
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!player.IsValid) return false;
            if (!_state.Contacts.TryGetValue(player, out var contact))
            { contact = new FloorHandContactBehaviorState(); _state.Contacts.Add(player, contact); }
            contact.AwaitingDamageResult = false;
            if (!alive) return Release(contact, player, tick, CollapseHandEventKind.Released, out fact);
            if (effects != null && effects.IsUngrabbable)
                return Release(contact, player, tick, CollapseHandEventKind.Released, out fact);
            if (contact.Phase == FloorHandPhase.Cooldown)
            {
                contact.Elapsed += dt;
                if (contact.Elapsed >= _config.HandCooldown) { contact.Elapsed = 0f; contact.Phase = FloorHandPhase.Idle; }
                return false;
            }
            bool finite = !float.IsNaN(probe.Distance) && !float.IsInfinity(probe.Distance) && probe.Distance >= 0f;
            bool reachable = probe.Available && finite;
            if (contact.Phase == FloorHandPhase.Idle)
            {
                if (!reachable || probe.Distance > _config.HandReach) return false;
                contact.RoomId = probe.RoomId; contact.HandId = probe.HandId; contact.Position = probe.Position;
                contact.Outward = probe.Outward;
                contact.GrabOrigin = probe.PlayerPosition ?? probe.Position;
                contact.Phase = FloorHandPhase.Warning; contact.Elapsed = 0f;
                fact = Fact(contact, player, CollapseHandEventKind.Warning, tick); return true;
            }
            bool same = reachable && probe.RoomId == contact.RoomId && probe.HandId == contact.HandId;
            float escape = contact.Phase == FloorHandPhase.Grabbed ? _config.HandEscapeDistance : _config.HandReach;
            float distance = probe.PlayerPosition.HasValue ? Vector3.Distance(contact.GrabOrigin, probe.PlayerPosition.Value) : probe.Distance;
            if (!same || distance > escape) return Release(contact, player, tick, CollapseHandEventKind.Escaped, out fact);
            contact.Elapsed += dt;
            if (contact.Phase == FloorHandPhase.Warning && contact.Elapsed >= _config.HandWarningDuration)
            {
                if (_state.WaxHeartAvailable)
                { _state.WaxHeartAvailable = false; return Release(contact, player, tick, CollapseHandEventKind.Escaped, out fact); }
                if (_state.WaxWards.Remove(player)) return Release(contact, player, tick, CollapseHandEventKind.Escaped, out fact);
                contact.Phase = FloorHandPhase.Grabbed; contact.Elapsed = 0f;
                fact = Fact(contact, player, CollapseHandEventKind.Grabbed, tick); return true;
            }
            if (contact.Phase == FloorHandPhase.Grabbed && contact.Elapsed >= _config.HandEscapeGrace)
            {
                contact.Phase = FloorHandPhase.Cooldown; contact.Elapsed = 0f;
                contact.AwaitingDamageResult = true; contact.HitTick = tick;
                fact = Fact(contact, player, CollapseHandEventKind.Hit, tick); return true;
            }
            return false;
        }

        public bool Cancel(EntityId player, long tick, out CollapseHandFact fact)
        {
            fact = default;
            return _state.Contacts.TryGetValue(player, out var contact) &&
                Release(contact, player, tick, CollapseHandEventKind.Escaped, out fact);
        }

        public bool ConfirmDeath(EntityId player, int room, bool alive, long tick, out CollapseHandFact fact)
        {
            fact = default;
            if (alive || !_state.Contacts.TryGetValue(player, out var contact) || !contact.AwaitingDamageResult ||
                contact.RoomId != room || contact.HitTick != tick) return false;
            contact.AwaitingDamageResult = false;
            fact = Fact(contact, player, CollapseHandEventKind.Consumed, tick); return true;
        }

        public bool ArmWaxWard(EntityId player) => player.IsValid && _state.WaxWards.Add(player);
        public NoiseEvent GrabNoise(CollapseHandFact fact) => new NoiseEvent(fact.PlayerId, fact.Position,
            _config.HandNoiseLoudness, fact.Tick, NoiseSourceKind.Other);
        public Vector3 BoundaryAcceleration(FloorHandProbe probe)
        {
            if (!probe.Available || !probe.Closed || probe.Distance > _config.HandReach) return Vector3.zero;
            return probe.Outward.normalized * (_config.BoundaryContactAcceleration +
                _config.BoundarySpringAcceleration * Mathf.Max(0f, probe.Penetration));
        }
        public void CopyRoomPhases(IDictionary<int, FloorHandPhase> rooms)
        {
            foreach (int room in rooms.Keys.ToArray()) rooms[room] = FloorHandPhase.Idle;
            foreach (var contact in _state.Contacts.Values)
            {
                if (!rooms.TryGetValue(contact.RoomId, out var current)) continue;
                if (current == FloorHandPhase.Grabbed || contact.Phase == FloorHandPhase.Idle) continue;
                if (current == FloorHandPhase.Warning && contact.Phase == FloorHandPhase.Cooldown) continue;
                rooms[contact.RoomId] = contact.Phase;
            }
        }
        public void Reset() { _state.Contacts.Clear(); _state.WaxWards.Clear(); _state.WaxHeartAvailable = false; }
        private bool Release(FloorHandContactBehaviorState contact, EntityId player, long tick, CollapseHandEventKind kind, out CollapseHandFact fact)
        {
            fact = default;
            if (contact.Phase != FloorHandPhase.Warning && contact.Phase != FloorHandPhase.Grabbed) return false;
            contact.Phase = FloorHandPhase.Cooldown; contact.Elapsed = 0f; contact.AwaitingDamageResult = false;
            fact = Fact(contact, player, kind, tick); return true;
        }
        private CollapseHandFact Fact(FloorHandContactBehaviorState contact, EntityId player, CollapseHandEventKind kind, long tick)
            => new CollapseHandFact(player, contact.RoomId, kind, contact.Position,
                kind == CollapseHandEventKind.Grabbed ? _config.HandSlowMultiplier : 1f,
                kind == CollapseHandEventKind.Hit ? _config.HandDamage : 0f, tick, HitSeverity.Light, HitSource.Hand,
                kind == CollapseHandEventKind.Hit ? contact.Outward.normalized * _config.HandThrowSpeed : Vector3.zero);
    }
}
