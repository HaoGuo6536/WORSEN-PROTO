// ============================================================================
// HunterAttackController.cs
// ============================================================================
// PURPOSE:
//   Advances committed Hunter attacks independently of perception and route choice.
//   It retains the existing phase arithmetic, feedback order and contact admission
//   so extracting the attack seam does not change damage or random draw timing.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Advance windup, active and recovery phases and compute missed-lunge stumble.
//   - Begin committed attacks and choose their occasional vocal cue.
//   - Admit shared melee/ranged contacts and expose attack samples.
// DEPENDENCIES:
//   - Hunter state, profile and archetype contracts; Core facts and Player read-only view.
// USAGE NOTES:
//   Per-life pure logic. Time and the shared random stream are injected. All mutable
//   data stays in HunterBehaviorState; the coordinator owns reset and call ordering.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterAttackController
    {
        private readonly HunterBehaviorState _state;
        private readonly HunterProfile _profile;
        private readonly System.Random _random;
        private readonly IReadOnlyPlayerState _player;
        private readonly IHunterArchetypeController _archetype;
        public HunterAttackController(HunterBehaviorState state, HunterProfile profile, System.Random random,
            IReadOnlyPlayerState player, IHunterArchetypeController archetype)
        { _state = state; _profile = profile; _random = random; _player = player; _archetype = archetype; }
        private bool Cursed(ProgressionTraits trait) => (_state.Traits & trait) != 0;
        public bool PlayerRevivalProtected => _player is IReadOnlyPlayerRevivalState protection &&
            (protection.RevivalCollisionGraceActive || protection.RevivalDamageImmune);
        private bool ArchetypeHeld => (_archetype as IHunterObservationRules)?.Hold ?? false;
        private bool Dormant => (_archetype as IHunterDormancyRules)?.Dormant ?? false;
        public float Duration(HunterLungePhase phase)
        {
            if (phase == HunterLungePhase.Windup)
                return Mathf.Max(0.15f, _profile.LungeWindupSeconds *
                    (Cursed(ProgressionTraits.LurkerStolenSilence) || Cursed(ProgressionTraits.HexerHastyScript) || Cursed(ProgressionTraits.ThorncallerQuickRoots) ? 0.7f : 1f));
            if (phase == HunterLungePhase.Active) return Mathf.Max(0.05f, _profile.LungeActiveSeconds);
            float recovery = Mathf.Max(0.15f, _profile.LungeRecoverySeconds * (Cursed(ProgressionTraits.RusherSecondWind) ? 0.65f : 1f));
            return !_state.LungeHitAccepted && _profile.AttackStyle == HunterAttackStyle.Lunge ? Mathf.Max(recovery, _profile.MissStaggerSeconds) : recovery;
        }
        public Vector3 Advance(float dt, out float stumbleSeconds)
        {
            float recoveryBefore = _state.LungePhase == HunterLungePhase.Recovery ? _state.PhaseSeconds : 0f;
            float untilRecovery = _state.LungePhase == HunterLungePhase.Windup ?
                Duration(HunterLungePhase.Windup) - _state.PhaseSeconds + Duration(HunterLungePhase.Active) :
                _state.LungePhase == HunterLungePhase.Active ? Duration(HunterLungePhase.Active) - _state.PhaseSeconds : 0f;
            bool wasAttacking = _state.LungePhase != HunterLungePhase.None;
            if (_state.LungePhase != HunterLungePhase.None)
            {
                _state.PhaseSeconds += dt;
                while (_state.LungePhase != HunterLungePhase.None &&
                    _state.PhaseSeconds + 0.000001f >= Duration(_state.LungePhase))
                {
                    _state.PhaseSeconds = Mathf.Max(0f, _state.PhaseSeconds - Duration(_state.LungePhase));
                    _state.LungePhase = _state.LungePhase == HunterLungePhase.Windup ? HunterLungePhase.Active :
                        _state.LungePhase == HunterLungePhase.Active ? HunterLungePhase.Recovery : HunterLungePhase.None;
                    if (_state.LungePhase == HunterLungePhase.Active)
                    {
                        _state.AttackBecameActive = true;
                        if (_profile.AttackStyle != HunterAttackStyle.Lunge) _state.FiredRangedAttacks.Add(_state.AttackSerial);
                        _state.Feedback.Enqueue(HunterFeedbackKind.AttackSwing);
                    }
                    if (_state.LungePhase == HunterLungePhase.Recovery)
                    {
                        if (_profile.AttackStyle == HunterAttackStyle.Lunge && !_state.LungeHitAccepted) _state.Feedback.Enqueue(HunterFeedbackKind.AttackMiss);
                        _state.Feedback.Enqueue(HunterFeedbackKind.AttackRecovery);
                    }
                    if (_state.LungePhase == HunterLungePhase.None)
                    { _state.PlannedFacts = ulong.MaxValue; _state.PhaseSeconds = 0f; }
                }
            }
            stumbleSeconds = !_archetype.OwnsPursuit && wasAttacking && !_state.LungeHitAccepted && _profile.AttackStyle == HunterAttackStyle.Lunge ?
                Mathf.Clamp(recoveryBefore + dt - untilRecovery, 0f, _profile.MissStaggerSeconds) -
                Mathf.Clamp(recoveryBefore, 0f, _profile.MissStaggerSeconds) : 0f;
            return _state.LungeDirection * (stumbleSeconds * _profile.MissStumbleMeters / _profile.MissStaggerSeconds);
        }
        public bool TryBegin()
        {
            if (_state.Action != HunterAction.Lunge || !_state.PlayerVisible || _state.IsDeliberating ||
                (_archetype is IHunterAttackRules attacks && !attacks.UsesSharedAttacks)) return false;
            _state.LungePhase = HunterLungePhase.Windup; _state.PhaseSeconds = 0f;
            _state.AttackSerial++; _state.AttackTarget = _player.Position;
            _state.AcceptedRangedAttacks.RemoveWhere(serial => serial < _state.AttackSerial - 8);
            _state.FiredRangedAttacks.RemoveWhere(serial => serial < _state.AttackSerial - 8);
            Vector3 direction = _player.Position - _state.Position; direction.y = 0f;
            _state.LungeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : _state.Forward;
            _state.LungeHitAccepted = false;
            if ((_profile.AttackScreamsEnabled || Cursed(ProgressionTraits.WatcherUnquietGaze)) &&
                _state.ScreamCooldown <= 0f && _random.NextDouble() < _profile.AttackScreamChance)
            { _state.Feedback.Enqueue(HunterFeedbackKind.Scream); _state.ScreamCooldown = _profile.ScreamCooldownSeconds; }
            else _state.Feedback.Enqueue(HunterFeedbackKind.AttackWindup);
            return true;
        }
        public bool TryAcceptRangedContact(EntityId target, int attackSerial, out HunterHit hit)
        {
            hit = default;
            if (PlayerRevivalProtected) return false;
            if (_state.StunRemaining > 0f || _state.SlipRemaining > 0f || _state.ReactionHeld || _state.BreakingDoor != 0 ||
                ArchetypeHeld || Dormant || _profile.AttackStyle == HunterAttackStyle.Lunge || target != _state.TargetId || !_player.IsAlive || !_state.IsActive ||
                attackSerial <= 0 || !_state.FiredRangedAttacks.Contains(attackSerial) || attackSerial > _state.AttackSerial || attackSerial < _state.AttackSerial - 8 ||
                !_state.AcceptedRangedAttacks.Add(attackSerial)) return false;
            _state.Feedback.Enqueue(HunterFeedbackKind.AttackHit);
            hit = new HunterHit(_state.Id, target, _profile.LungeDamage, _state.Tick, _state.Position,
                _profile.AttackStyle == HunterAttackStyle.Projectile ? ChaseEndReason.Projectile : ChaseEndReason.GroundSpike);
            return true;
        }
        public void ReportAttackMiss(int attackSerial)
        {
            if (!_state.AcceptedRangedAttacks.Contains(attackSerial)) _state.Feedback.Enqueue(HunterFeedbackKind.AttackMiss);
        }
        public HunterAttackSample AttackSample()
        {
            bool attacking = _state.IsActive && _state.LungePhase != HunterLungePhase.None;
            return new HunterAttackSample(_state.Id, _state.Position,
                attacking ? _state.LungeDirection : _state.Forward,
                attacking ? (int)_state.LungePhase : 0,
                attacking ? Mathf.Clamp01(_state.PhaseSeconds / Mathf.Max(0.0001f, Duration(_state.LungePhase))) : 0f);
        }
        public bool TryAcceptContact(EntityId target, out HunterHit hit)
        {
            hit = default;
            if (PlayerRevivalProtected) return false;
            if (_state.StunRemaining > 0f || _state.SlipRemaining > 0f || _state.ReactionHeld || _state.BreakingDoor != 0 ||
                ArchetypeHeld || _state.CatchActive ||
                target != _state.TargetId || !_player.IsAlive || !_state.IsActive) return false;
            if (_archetype is IHunterContactRules contact)
            {
                if (!contact.ContactReady) return false;
                contact.CommitContact();
            }
            else
            {
                if (Dormant || _state.LungePhase != HunterLungePhase.Active || _state.LungeHitAccepted) return false;
                _state.LungeHitAccepted = true;
                _state.Feedback.Enqueue(HunterFeedbackKind.AttackHit);
            }
            hit = new HunterHit(_state.Id, target, _profile.LungeDamage, _state.Tick, _state.Position);
            return true;
        }
    }
}
