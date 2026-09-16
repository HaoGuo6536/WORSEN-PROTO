// ============================================================================
// AudioFeedbackPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Maps committed movement, health, hunter, room and progression facts to audible feedback.
//   It deduplicates per-tick observations and distinguishes attack outcomes, collectible kinds and lifecycle transitions.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Scale landing impacts by committed stumble severity and sliding friction by actual turn rate.
//   - Map authoritative posture changes and fade sustained exertion without restarting its voice.
//   - Give health sole ownership of player damage/death; never replay a swing for its miss outcome.
//   - Choose presentation commands without changing gameplay.
//   - Suppress initialization damage and repeated same-event sounds.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Tick-based pickup chain window assumes the shared 60 Hz simulation.
//   Progression revisions and generation identities clear per-floor deduplication state.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Presentation.Audio
{
    public sealed class AudioFeedbackPresenter
    {
        public const int ExertionEmitter = -300000;
        public void Movement(AudioFeedbackDriverState state, PlayerMovementSample sample)
        {
            state.Commands.Clear(); if (sample.Tick <= state.MovementTick) return;
            state.MovementTick = sample.Tick; state.Position = sample.Position;
            state.Openness = state.LocalCollapse = 0f;
            foreach (GeneratedRoomSample room in state.Layout.Values)
                if (room.Bounds.Contains(sample.Position))
                {
                    state.Openness = room.OpenSky ? 1f : 0f;
                    if (state.Rooms.TryGetValue(room.RoomId, out RoomDestructionSample destruction)) state.LocalCollapse = destruction.Progress;
                    break;
                }
            if (state.Movement != MovementState.Slide && sample.MovementState == MovementState.Slide)
            { Add(state, CueId.SlideStart); Add(state, CueId.SlideLoop, emitter: -200000, gain: SlideFrictionGain(sample.SlideTurnRateDegrees)); }
            if (state.Movement == MovementState.Slide && sample.MovementState != MovementState.Slide)
            { Stop(state, -200000); Add(state, CueId.SlideEnd); }
            if (state.HasMovement && state.IsCrouched != sample.IsCrouched &&
                state.Movement != MovementState.Slide && sample.MovementState != MovementState.Slide)
                Add(state, CueId.PostureRustle);
            state.HasMovement = true; state.IsCrouched = sample.IsCrouched; state.IsSprinting = sample.IsSprinting;
            state.Movement = sample.MovementState;
        }
        public void TickExertion(AudioFeedbackDriverState state, float dt, float onsetSeconds, float releaseSeconds, float criticalMultiplier)
        {
            state.Commands.Clear();
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f) return;
            float target = state.HasMovement && state.IsAlive && state.IsSprinting ? 1f : 0f;
            if (state.IsCritical) target *= Unit(criticalMultiplier);
            if (state.IsCritical && target <= 0f) state.ExertionGain = 0f;
            float seconds = target > state.ExertionGain ? onsetSeconds : releaseSeconds;
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 1f;
            state.ExertionGain = state.IsAlive ? Mathf.MoveTowards(state.ExertionGain, target, dt / Mathf.Max(.05f, seconds)) : 0f;
            if (state.ExertionGain > 0f)
            {
                Add(state, CueId.SprintExertion, emitter: ExertionEmitter, gain: state.ExertionGain);
                state.ExertionActive = true;
            }
            else if (state.ExertionActive) { Stop(state, ExertionEmitter); state.ExertionActive = false; }
        }
        private float Unit(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        public float SlideFrictionGain(float turnRateDegrees) =>
            .5f + .5f * Mathf.Clamp01(float.IsNaN(turnRateDegrees) ? 0f : Mathf.Abs(turnRateDegrees) / 40f);
        public void Traversal(AudioFeedbackDriverState state, PlayerTraversalFact fact)
        {
            state.Commands.Clear();
            if (!Fresh(state, fact.Id.Value, 100 + (int)fact.Kind, fact.Tick)) return;
            if (!fact.Succeeded)
            {
                if (fact.Kind == TraversalKind.Vault || fact.Kind == TraversalKind.Mantle || fact.Kind == TraversalKind.Rebound) Add(state, CueId.TraversalMiss, gain: .4f);
                return;
            }
            switch (fact.Kind)
            {
                case TraversalKind.Jump: Add(state, CueId.Jump); break;
                case TraversalKind.Land: Add(state, CueId.Land, gain: .45f + .55f * Mathf.Clamp01(fact.Duration > 0f ? fact.Duration / .5f : 0f)); break;
                case TraversalKind.Rebound: Add(state, CueId.WallRebound); break;
                case TraversalKind.Vault: case TraversalKind.Mantle: Add(state, CueId.Vault); break;
            }
        }
        public void Hunter(AudioFeedbackDriverState state, HunterFeedbackEvent fact)
        {
            state.Commands.Clear();
            if (!state.IsAlive) return;
            if (fact.Kind == HunterFeedbackKind.ProjectileLaunched || fact.Kind == HunterFeedbackKind.ProjectileMoved || fact.Kind == HunterFeedbackKind.ProjectileTravel || fact.Kind == HunterFeedbackKind.ProjectileImpact || fact.Kind == HunterFeedbackKind.ProjectileExpired)
            {
                Projectile(state, fact); return;
            }
            int eventOwner = fact.EmitterId != 0 ? ProjectileEmitter(state, fact) : fact.Hunter.Value;
            if (!Fresh(state, eventOwner, 200 + (int)fact.Kind, fact.Tick)) return;
            CueId cue; bool spike = fact.ArchetypeKey == "thorncaller"; bool bolt = fact.ArchetypeKey == "hexer";
            switch (fact.Kind)
            {
                case HunterFeedbackKind.Detected: cue = CueId.Detection; break;
                case HunterFeedbackKind.LostTarget: cue = CueId.EnemyLost; break;
                case HunterFeedbackKind.AttackWindup: cue = CueId.EnemyWindup; break;
                case HunterFeedbackKind.AttackSwing: if (spike || bolt) return; cue = CueId.EnemyAttack; break;
                // Swing/launch owns the attack sound; health owns its accepted damage result.
                case HunterFeedbackKind.AttackMiss: case HunterFeedbackKind.AttackHit: return;
                case HunterFeedbackKind.AttackRecovery: cue = CueId.EnemyRecovery; break;
                case HunterFeedbackKind.LightReaction: cue = CueId.Presence; break;
                case HunterFeedbackKind.Scream: cue = CueId.EnemyScream; break;
                case HunterFeedbackKind.Footstep: cue = CueId.EnemyFootstep; break;
                case HunterFeedbackKind.SpikeWarning: cue = CueId.SpikeWarning; break;
                case HunterFeedbackKind.SpikeErupt: cue = CueId.SpikeErupt; break;
                default: return;
            }
            Add(state, cue, fact.Position, eventOwner);
        }
        private int ProjectileEmitter(AudioFeedbackDriverState state, HunterFeedbackEvent fact)
        {
            long key = ((long)fact.Hunter.Value << 32) | (uint)(fact.EmitterId != 0 ? fact.EmitterId : fact.AttackSerial);
            if (!state.ProjectileEmitters.TryGetValue(key, out int emitter))
            { emitter = int.MinValue + (++state.NextProjectileEmitter); state.ProjectileEmitters[key] = emitter; }
            return emitter;
        }
        private void Projectile(AudioFeedbackDriverState state, HunterFeedbackEvent fact)
        {
            long key = ((long)fact.Hunter.Value << 32) | (uint)(fact.EmitterId != 0 ? fact.EmitterId : fact.AttackSerial);
            int emitter = ProjectileEmitter(state, fact);
            if (!Fresh(state, emitter, 200 + (int)fact.Kind, fact.Tick)) return;
            if (fact.Kind == HunterFeedbackKind.ProjectileLaunched)
            {
                if (!state.FlyingProjectiles.Add(key)) return;
                Add(state, CueId.ProjectileLaunch, fact.Position, emitter);
                Add(state, CueId.ProjectileTravel, fact.Position, emitter, .4f);
            }
            else if (fact.Kind == HunterFeedbackKind.ProjectileMoved || fact.Kind == HunterFeedbackKind.ProjectileTravel)
            { if (state.FlyingProjectiles.Contains(key)) Add(state, CueId.ProjectileTravel, fact.Position, emitter, .4f); }
            else if (state.FlyingProjectiles.Remove(key))
            { Stop(state, emitter); if (fact.Kind == HunterFeedbackKind.ProjectileImpact) Add(state, CueId.ProjectileImpact, fact.Position, emitter); }
        }
        public void Pickup(AudioFeedbackDriverState state, PickupCollectedFact fact, Vector3 position)
        {
            state.Commands.Clear(); if (!state.Pickups.Add(fact.AnchorId)) return;
            state.Chain = fact.Tick - state.PickupTick <= 120 ? state.Chain + 1 : 1; state.PickupTick = fact.Tick;
            CueId cue = fact.Kind == PickupKind.GoldenCake ? CueId.GoldenCakeCollect :
                state.Chain >= 3 && state.Chain % 3 == 0 ? CueId.CakeChain : CueId.CakeCollect;
            Add(state, cue, position);
        }
        public void Hand(AudioFeedbackDriverState state, CollapseHandFact fact)
        {
            state.Commands.Clear(); if (!Fresh(state, fact.RoomId, 300 + (int)fact.Kind, fact.Tick)) return;
            CueId cue;
            switch (fact.Kind)
            {
                case CollapseHandEventKind.Warning: cue = CueId.GrabWarning; break;
                case CollapseHandEventKind.Grabbed: cue = CueId.GrabStart; break;
                case CollapseHandEventKind.Escaped: cue = CueId.GrabEscape; break;
                // Accepted health loss owns one hit/death; consumption visuals remain independent.
                case CollapseHandEventKind.Hit: case CollapseHandEventKind.Consumed: return;
                default: return;
            }
            Add(state, cue, fact.Position, -10000 - fact.RoomId);
        }
        public void Room(AudioFeedbackDriverState state, RoomDestructionSample sample, Vector3 position)
        {
            state.Commands.Clear(); int emitter = -10000 - sample.RoomId;
            bool has = state.Rooms.TryGetValue(sample.RoomId, out RoomDestructionSample before);
            bool changed = !has || before.Phase != sample.Phase;
            state.Rooms[sample.RoomId] = sample;
            if (changed)
            {
                switch (sample.Phase)
                {
                    case RoomPhase.Telegraph: Add(state, CueId.RoomCrack, position, emitter); break;
                    case RoomPhase.Tearing: Add(state, CueId.RoomTear, position, emitter); break;
                    case RoomPhase.Encroaching: Add(state, CueId.MistAdvance, position, emitter); break;
                    case RoomPhase.Closed: Stop(state, emitter); Add(state, CueId.RoomConsumed, position, emitter); break;
                }
            }
            else if (sample.Phase == RoomPhase.Telegraph && Mathf.FloorToInt(sample.Progress * 5) > Mathf.FloorToInt(before.Progress * 5))
                Add(state, CueId.RoomCrack, position, emitter, .65f);
        }
        public void Health(AudioFeedbackDriverState state, EntityId player, float health, float maximum)
        {
            state.Commands.Clear();
            bool had = state.Health.TryGetValue(player.Value, out float previous); state.Health[player.Value] = health;
            state.IsAlive = health > 0f; state.IsCritical = health > 0f && health <= maximum * .25f;
            if (!state.IsAlive) state.IsSprinting = false;
            if (!had) return;
            if (health < previous) Add(state, health <= 0f ? CueId.Death : CueId.PlayerHit);
            else if (health > previous) Add(state, CueId.Heal);
            // AudioMixPresenter's retained breath source owns critical breathing. Starting
            // PlayerCritical here would play the same breath clip twice at different phases.
        }
        public void Flashlight(AudioFeedbackDriverState state, FlashlightSample sample)
        {
            state.Commands.Clear();
            if (state.HasFlashlight && state.Flashlight != sample.Enabled) Add(state, sample.Enabled ? CueId.FlashlightOn : CueId.FlashlightOff, sample.Origin);
            state.Flashlight = sample.Enabled; state.HasFlashlight = true;
        }
        public void Progression(AudioFeedbackDriverState state, ProgressionSnapshot sample)
        {
            state.Commands.Clear(); if (sample.Revision <= state.Revision) return;
            if (sample.GenerationId != state.Generation)
            {
                state.Health.Clear(); state.Rooms.Clear(); state.EventTicks.Clear(); state.Pickups.Clear(); state.MovementTick = -1;
                state.ProjectileEmitters.Clear(); state.FlyingProjectiles.Clear(); state.NextProjectileEmitter = 0;
                state.HasFlashlight = false; state.Chain = 0; state.PickupTick = -1000; state.Movement = MovementState.Ground;
                state.HasMovement = state.IsCrouched = state.IsSprinting = state.IsCritical = state.ExertionActive = false;
                state.IsAlive = true; state.ExertionGain = 0f;
            }
            if (state.Revision >= 0)
            {
                if (sample.CurseCount > state.CurseCount) Add(state, CueId.CurseSelect);
                if (sample.Wallet < state.Wallet && state.Phase == ProgressionPhase.Shop) Add(state, CueId.ShopBuy);
                if (sample.GenerationId == state.Generation && sample.Phase == ProgressionPhase.Exploring && state.Phase == ProgressionPhase.Exploring && sample.Effects.WaxWardCharges < state.WardCharges) Add(state, CueId.WardBreak);
            }
            if (sample.Phase != state.Phase)
            {
                if (sample.Phase == ProgressionPhase.ChooseCurse || sample.Phase == ProgressionPhase.ChooseThreat) Add(state, CueId.CurseOffer);
                if (sample.Phase == ProgressionPhase.Shop) Add(state, CueId.ShopOpen);
                if (sample.Phase == ProgressionPhase.Exploring) Add(state, CueId.RoundStart);
            }
            state.Revision = sample.Revision; state.Generation = sample.GenerationId; state.Phase = sample.Phase;
            state.Wallet = sample.Wallet; state.CurseCount = sample.CurseCount; state.WardCharges = sample.Effects.WaxWardCharges;
        }
        private bool Fresh(AudioFeedbackDriverState state, int owner, int kind, long tick)
        {
            long key = ((long)kind << 32) | (uint)owner;
            if (state.EventTicks.TryGetValue(key, out long prior) && tick <= prior) return false;
            state.EventTicks[key] = tick; return true;
        }
        private void Add(AudioFeedbackDriverState state, CueId cue, Vector3? position = null, int emitter = 0, float gain = 1f) =>
            state.Commands.Add(new AudioFeedbackCommand { Cue = cue, Position = position ?? state.Position, Emitter = emitter, Gain = gain });
        private void Stop(AudioFeedbackDriverState state, int emitter) => state.Commands.Add(new AudioFeedbackCommand { StopEmitter = true, Emitter = emitter });
    }
}
