// ============================================================================
// FloorHandController.cs
// ============================================================================
// PURPOSE:
//   Controls a warning, escapable slow, one normal hit, and explicit lethal confirmation.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
// USAGE NOTES:
//   Scene-owned through FloorManager/FloorDriver. Time is supplied by the owner.
//   No persistent singleton, global settings, or independent update loop.
// ============================================================================
using System;
using Worsen.Core;

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

        public bool Tick(EntityId player, bool alive, FloorHandProbe probe, float dt, long tick, out CollapseHandFact fact)
        {
            fact = default;
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!player.IsValid) return false;
            if (!_state.Contacts.TryGetValue(player, out var contact))
            { contact = new FloorHandContactBehaviorState(); _state.Contacts.Add(player, contact); }
            contact.AwaitingDamageResult = false;
            if (!alive) return Release(contact, player, tick, CollapseHandEventKind.Released, out fact);
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
                contact.Phase = FloorHandPhase.Warning; contact.Elapsed = 0f;
                fact = Fact(contact, player, CollapseHandEventKind.Warning, tick); return true;
            }
            bool same = reachable && probe.RoomId == contact.RoomId && probe.HandId == contact.HandId;
            float escape = contact.Phase == FloorHandPhase.Grabbed ? _config.HandEscapeDistance : _config.HandReach;
            if (!same || probe.Distance > escape) return Release(contact, player, tick, CollapseHandEventKind.Escaped, out fact);
            contact.Elapsed += dt;
            if (contact.Phase == FloorHandPhase.Warning && contact.Elapsed >= _config.HandWarningDuration)
            {
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

        public void Reset() => _state.Contacts.Clear();
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
                kind == CollapseHandEventKind.Hit ? _config.HandDamage : 0f, tick);
    }
}
