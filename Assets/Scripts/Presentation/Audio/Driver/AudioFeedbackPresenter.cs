// ============================================================================
// AudioFeedbackPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Maps committed movement, health, hunter, room and progression facts to audible feedback.
//   It deduplicates committed observations, removes out-of-budget layers and uses
//   explicit shop transactions rather than guessing a purchase from wallet changes.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Scale landing impacts by committed stumble severity and sliding friction by actual turn rate.
//   - Keep posture, exertion and health changes silent; embodiment owns breathing.
//   - Admit one death sting only at catch hold start and one physical exit-opening cue.
//   - Choose presentation commands without changing gameplay.
//   - Suppress initialization damage and repeated same-event sounds.
//   - Preserve one ordinary or golden pickup cue without a combo replacement.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Pickup anchor identities deduplicate collection; cadence never changes its cue.
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
            { Add(state, CueId.SlideLoop, emitter: -200000, gain: SlideFrictionGain(sample.SlideTurnRateDegrees)); }
            if (state.Movement == MovementState.Slide && sample.MovementState != MovementState.Slide)
            { Stop(state, -200000); Add(state, CueId.SlideEnd); }

            state.HasMovement = true; state.IsCrouched = sample.IsCrouched; state.IsSprinting = sample.IsSprinting;
            state.Movement = sample.MovementState;
        }
        public void TickExertion(AudioFeedbackDriverState state, float dt, float onsetSeconds, float releaseSeconds, float criticalMultiplier)
        {
            state.Commands.Clear();
            state.ExertionGain = 0f; state.ExertionActive = false;
        }
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
            int eventOwner = fact.Hunter.Value;
            if (!Fresh(state, eventOwner, 200 + (int)fact.Kind, fact.Tick)) return;
            CueId cue;
            switch (fact.Kind)
            {
                case HunterFeedbackKind.Detected: cue = CueId.Detection; break;
                case HunterFeedbackKind.LostTarget: return;
                case HunterFeedbackKind.AttackWindup: cue = CueId.EnemyWindup; break;
                case HunterFeedbackKind.AttackSwing: return;
                // Swing/launch owns the attack sound; health owns its accepted damage result.
                case HunterFeedbackKind.AttackMiss: case HunterFeedbackKind.AttackHit: return;
                case HunterFeedbackKind.AttackRecovery: return;
                case HunterFeedbackKind.LightReaction: cue = CueId.Presence; break;
                case HunterFeedbackKind.Scream: cue = CueId.EnemyScream; break;
                case HunterFeedbackKind.Footstep: cue = CueId.EnemyFootstep; break;
                case HunterFeedbackKind.SpikeWarning: cue = CueId.SpikeWarning; break;
                case HunterFeedbackKind.SpikeErupt: return;
                default: return;
            }
            Add(state, cue, fact.Position, eventOwner);
        }

        public void Pickup(AudioFeedbackDriverState state, PickupCollectedFact fact, Vector3 position)
        {
            state.Commands.Clear(); if (!state.Pickups.Add(fact.AnchorId)) return;
            CueId cue = fact.Kind == PickupKind.GoldenCake ? CueId.GoldenCakeCollect : CueId.CakeCollect;
            Add(state, cue, position);
        }
        public void Hand(AudioFeedbackDriverState state, CollapseHandFact fact)
        {
            state.Commands.Clear(); // Room pulses and grace embodiment replace the hand layers.
        }
        public void Room(AudioFeedbackDriverState state, RoomDestructionSample sample, Vector3 position)
        {
            state.Commands.Clear();
            state.Rooms[sample.RoomId] = sample;
            // AudioWorldMixPresenter is the sole collapse pulse clock.
        }
        public void Health(AudioFeedbackDriverState state, EntityId player, float health, float maximum)
        {
            state.Commands.Clear();
            state.Health[player.Value] = health;
            state.IsAlive = health > 0f; state.IsCritical = health > 0f && health <= maximum * .25f;
            if (!state.IsAlive) state.IsSprinting = false;

            // AudioMixPresenter's retained breath source owns critical breathing. Starting
            // PlayerCritical here would play the same breath clip twice at different phases.
        }
        public bool TryCatchSting(AudioFeedbackDriverState state, EntityId player)
        {
            if (!player.IsValid || state.CatchStingIssued) return false;
            state.CatchStingIssued = true;
            return true;
        }

        public void Flashlight(AudioFeedbackDriverState state, FlashlightSample sample)
        {
            state.Commands.Clear();

            state.Flashlight = sample.Enabled; state.HasFlashlight = true;
        }
        public void Progression(AudioFeedbackDriverState state, ProgressionSnapshot sample)
        {
            state.Commands.Clear(); if (sample.Revision <= state.Revision) return;
            if (sample.GenerationId != state.Generation)
            {
                state.Health.Clear(); state.Rooms.Clear(); state.EventTicks.Clear(); state.Pickups.Clear(); state.MovementTick = -1;
                state.ProjectileEmitters.Clear(); state.FlyingProjectiles.Clear(); state.NextProjectileEmitter = 0;
                state.HasFlashlight = false; state.Movement = MovementState.Ground;
                state.HasMovement = state.IsCrouched = state.IsSprinting = state.IsCritical = state.ExertionActive = false;
                state.IsAlive = true; state.ExertionGain = 0f;
                state.ExitSoundIssued = false;
            }
            if (state.Revision >= 0)
            {
                if (sample.CurseCount > state.CurseCount) Add(state, CueId.CurseSelect);

            }
            if (sample.Phase != state.Phase)
            {
                if (sample.Phase == ProgressionPhase.ChooseCurse || sample.Phase == ProgressionPhase.ChooseThreat) Add(state, CueId.CurseOffer);
                if (sample.Phase == ProgressionPhase.Shop) Add(state, CueId.ShopOpen);

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
        public void Transaction(AudioFeedbackDriverState state, ProgressionSnapshot previous, ProgressionSnapshot current, string operation)
        {
            state.Commands.Clear();
            if (previous.Phase != ProgressionPhase.Shop || current.Phase != ProgressionPhase.Shop || current.Revision <= previous.Revision || current.Revision <= state.TransactionRevision) return;
            state.TransactionRevision = current.Revision;
            if (operation == "Purchase" && string.IsNullOrEmpty(current.PendingOfferId)) Add(state, CueId.ShopBuy);
            else if (operation == "RerollShop") Add(state, CueId.UiMove);
            // ReservePurchase and CancelReplacement are intentionally silent.
        }
        public void Exit(AudioFeedbackDriverState state, FloorDisplaySnapshot sample, Vector3 position, float threshold)
        {
            state.Commands.Clear();
            if (state.ExitSoundIssued || !(sample.OpeningProgress >= threshold) || sample.OpeningProgress <= 0f) return;
            state.ExitSoundIssued = true; Add(state, CueId.DoorOpen, position);
        }
        private void Add(AudioFeedbackDriverState state, CueId cue, Vector3? position = null, int emitter = 0, float gain = 1f)
        {
            var catalogue = new AudioCueCataloguePresenter();
            if (!catalogue.Admits(cue, state.Phase == ProgressionPhase.Exploring)) return;
            state.Commands.Add(new AudioFeedbackCommand { Cue = catalogue.Canonical(cue), Position = position ?? state.Position, Emitter = emitter, Gain = gain });
        }
        private void Stop(AudioFeedbackDriverState state, int emitter) => state.Commands.Add(new AudioFeedbackCommand { StopEmitter = true, Emitter = emitter });
    }
}
