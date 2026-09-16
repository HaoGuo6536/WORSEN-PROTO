// ============================================================================
// HorrorEffectsController.cs
// ============================================================================
// PURPOSE:
//   Owns player flashlight authority and concrete general curse behavior.
//   Effects use committed old positions and explicit time, so they cannot grant hunters
//   hidden knowledge or replay stale schedules into a new floor.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Session · HorrorEffects.
// KEY RESPONSIBILITIES:
//   Compute beam state, delayed real-location noises, flame dimming and optional-room facts.
// DEPENDENCIES:
//   Core value contracts and the HorrorEffects system's own data only.
//   Unity value math is pure; engine lifecycle belongs only to the Manager.
// USAGE NOTES:
//   No randomness is required; schedules are deterministic and use run ticks.
//   Optional rooms must already be proven safe by generation; Floor validates each crack again.
//   ReceiveInput is an alternate input path: do not also pass that frame into Tick.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Session.HorrorEffects
{
    public sealed class HorrorEffectsController
    {
        private readonly HorrorEffectsBehaviorState state;
        private readonly HorrorEffectsConfig config;
        public HorrorEffectsController(HorrorEffectsBehaviorState state, HorrorEffectsConfig config)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }
        public bool FlashlightEnabled => state.Active && state.FlashlightEnabled;
        public int GenerationId => state.GenerationId;
        public float FootstepLoudnessMultiplier => Has(ProgressionTraits.FeltSoles) ? config.FeltSolesMultiplier : 1f;
        public float ReboundCooldownMultiplier => Has(ProgressionTraits.ClimberWraps) ? config.ClimberRecoveryMultiplier : 1f;
        public float OptionalWindowMultiplier => Has(ProgressionTraits.SealedSills) ? config.SealedWindowMultiplier : 1f;

        public void BeginFloor(int generationId, ProgressionEffects effects)
        {
            Suspend();
            state.GenerationId = generationId;
            state.Effects = effects;
            state.Active = true;
            state.FlashlightEnabled = true;
            state.Elapsed = 0d;
            state.LastTick = state.LastMovementTick = state.LastLandingTick = -1;
            state.Tick = 0;
            state.NextBorrowedStep = 0d;
            state.NextFlameUpdate = 0d;
            state.NextRoomCrack = config.OptionalRoomInterval;
            state.ActorEffectsRevision++;
        }

        public void UpdateEffects(ProgressionEffects effects)
        {
            ProgressionTraits movementTraits = ProgressionTraits.FeltSoles | ProgressionTraits.ClimberWraps;
            if ((state.Effects.Traits & movementTraits) != (effects.Traits & movementTraits)) state.ActorEffectsRevision++;
            state.Effects = effects;
            for (int i = state.PendingNoises.Count - 1; i >= 0; i--)
                if (!Has(state.PendingNoises[i].RequiredTrait)) state.PendingNoises.RemoveAt(i);
            if (!Has(ProgressionTraits.Afterimage)) EndAfterimage();
            if (!Has(ProgressionTraits.UnquietFlame)) RestoreFlame();
            PublishLight();
        }

        public void ObserveAim(FlashlightSample sample)
        {
            if (!state.Active || !sample.Source.IsValid || !Finite(sample.Origin) ||
                !Finite(sample.Direction) || sample.Direction.sqrMagnitude < 0.000001f) return;
            if (state.HasAim && sample.Tick < state.Aim.Tick) return;
            state.Aim = new FlashlightSample(sample.Source, sample.Tick, state.FlashlightEnabled,
                sample.Origin, sample.Direction.normalized, config.FlashlightRange, config.FlashlightCone);
            state.HasAim = true;
            PublishLight();
        }

        public void ReceiveInput(InputFrame frame)
        {
            if (!state.Active) return;
            state.SprintHeld = (frame.Held & InputButtons.Sprint) != 0;
            if ((frame.Pressed & InputButtons.UseItem) == 0) return;
            state.FlashlightEnabled = !state.FlashlightEnabled;
            if (!state.FlashlightEnabled && Has(ProgressionTraits.Afterimage) && state.HasAim)
            {
                state.Afterimage = Light(true);
                state.AfterimageExpires = state.Elapsed + config.AfterimageLifetime;
                state.Facts.Add(new HorrorEffectFact(HorrorEffectKind.Afterimage, light: state.Afterimage,
                    value: config.AfterimageLifetime));
            }
            PublishLight();
        }

        public void ObserveMovement(PlayerMovementSample sample)
        {
            if (!state.Active || !sample.Id.IsValid || !Finite(sample.Position) ||
                !Finite(sample.Velocity) || sample.Tick <= state.LastMovementTick) return;
            state.Movement = sample;
            state.HasMovement = true;
            state.LastMovementTick = sample.Tick;
            if (Has(ProgressionTraits.BorrowedFootsteps) && state.Elapsed >= state.NextBorrowedStep &&
                sample.Velocity.sqrMagnitude > 0.01f && sample.MovementState != MovementState.Air &&
                sample.MovementState != MovementState.Vault && (!state.HasBorrowedStepPosition ||
                Vector3.Distance(sample.Position, state.LastBorrowedStepPosition) >= config.BorrowedStepDistance))
            {
                Schedule(sample.Id, sample.Position, config.BorrowedStepLoudness,
                    config.BorrowedStepDelay, ProgressionTraits.BorrowedFootsteps);
                state.LastBorrowedStepPosition = sample.Position;
                state.HasBorrowedStepPosition = true;
                state.NextBorrowedStep = state.Elapsed + config.BorrowedStepInterval;
            }
        }

        public void ObserveTraversal(PlayerTraversalFact fact)
        {
            if (!state.Active || !Has(ProgressionTraits.EchoDebt) || !state.HasMovement ||
                fact.Id != state.Movement.Id || fact.Kind != TraversalKind.Land || !fact.Succeeded ||
                fact.Duration < config.HardLandingStumbleThreshold || fact.Tick <= state.LastLandingTick) return;
            state.LastLandingTick = fact.Tick;
            Schedule(fact.Id, state.Movement.Position, config.EchoLoudness, config.EchoDelay, ProgressionTraits.EchoDebt);
        }

        public void RecordGoldenCollected(EntityId source, Vector3 position, long tick)
        {
            if (!state.Active || !source.IsValid || !Finite(position) || !Has(ProgressionTraits.GildedHunger)) return;
            state.Facts.Add(new HorrorEffectFact(HorrorEffectKind.Noise,
                noise: new NoiseEvent(source, position, config.GildedLoudness, tick)));
        }

        public void SetOptionalRooms(int[] roomIds)
        {
            state.OptionalRooms.Clear();
            state.NextOptionalRoom = 0;
            if (roomIds == null) return;
            foreach (int id in roomIds)
            {
                if (id >= 0 && !state.OptionalRooms.Contains(id)) state.OptionalRooms.Add(id);
                if (state.OptionalRooms.Count >= config.MaximumOptionalRooms) break;
            }
        }

        public void RecordDoorCrossed(int doorId, Vector3 position)
        {
            if (!state.Active || !Has(ProgressionTraits.PilgrimChalk) || doorId < 0 || !Finite(position) ||
                state.MarkedDoors.Count >= config.MaximumChalkMarks || !state.MarkedDoors.Add(doorId)) return;
            state.Facts.Add(new HorrorEffectFact(HorrorEffectKind.DoorMark, position: position, roomId: doorId));
        }

        public void Tick(InputFrame frame, float dt, long tick)
        {
            if (!state.Active || tick <= state.LastTick || !Finite(dt) || dt <= 0f) return;
            state.LastTick = state.Tick = tick;
            ReceiveInput(frame);
            state.Elapsed += dt;
            if (state.Afterimage.Enabled && state.Elapsed >= state.AfterimageExpires) EndAfterimage();
            for (int i = 0; i < state.PendingNoises.Count;)
            {
                HorrorScheduledNoise pending = state.PendingNoises[i];
                if (pending.DueTime > state.Elapsed) { i++; continue; }
                state.PendingNoises.RemoveAt(i);
                if (Has(pending.RequiredTrait)) state.Facts.Add(new HorrorEffectFact(HorrorEffectKind.Noise,
                    noise: new NoiseEvent(pending.Source, pending.Position, pending.Loudness, tick)));
            }
            TickFlame();
            if (Has(ProgressionTraits.RestlessMasonry) && state.Elapsed >= state.NextRoomCrack &&
                state.NextOptionalRoom < state.OptionalRooms.Count)
            {
                state.Facts.Add(new HorrorEffectFact(HorrorEffectKind.OptionalRoomCrack,
                    roomId: state.OptionalRooms[state.NextOptionalRoom++]));
                state.NextRoomCrack = state.Elapsed + config.OptionalRoomInterval;
            }
        }

        public void Tick(float dt, long tick) => Tick(new InputFrame(Vector2.zero, Vector2.zero,
            state.SprintHeld ? InputButtons.Sprint : InputButtons.None, InputButtons.None, InputButtons.None), dt, tick);

        public void Suspend()
        {
            state.Facts.Clear();
            EndAfterimage();
            RestoreFlame();
            if (state.HasAim) state.Facts.Add(new HorrorEffectFact(HorrorEffectKind.Flashlight, light: Light(false)));
            state.Active = false;
            state.HasAim = state.HasMovement = state.HasBorrowedStepPosition = state.SprintHeld = false;
            state.Aim = default;
            state.Movement = default;
            state.PendingNoises.Clear();
            state.OptionalRooms.Clear();
            state.MarkedDoors.Clear();
            state.NextOptionalRoom = 0;
            state.LastHandTicks.Clear();
            state.AppliedActorEffects.Clear();
            state.GrabMultipliers.Clear();
            state.BoundHunters.Clear();
        }

        public HorrorHazardResolution ResolveHand(CollapseHandFact fact, bool playerAlive)
        {
            if (!state.Active || !fact.PlayerId.IsValid || !playerAlive || fact.Kind == CollapseHandEventKind.Warning ||
                fact.Kind == CollapseHandEventKind.Consumed) return default;
            var key = (fact.PlayerId, fact.Kind);
            if (state.LastHandTicks.TryGetValue(key, out long last) && fact.Tick <= last) return default;
            state.LastHandTicks[key] = fact.Tick;
            float speed = fact.Kind == CollapseHandEventKind.Grabbed && Finite(fact.SlowMultiplier)
                ? Mathf.Clamp(fact.SlowMultiplier, 0.1f, 1f) : 1f;
            float damage = fact.Kind == CollapseHandEventKind.Hit && Finite(fact.Damage) ? Mathf.Max(0f, fact.Damage) : 0f;
            state.GrabMultipliers[fact.PlayerId] = speed;
            return new HorrorHazardResolution(true, fact.Kind == CollapseHandEventKind.Grabbed, speed, damage);
        }

        public void ReleaseGrab(EntityId playerId) => state.GrabMultipliers.Remove(playerId);

        public bool TryGetActorEffects(EntityId playerId, out float footsteps, out float rebound, out float grabSpeed)
        {
            footsteps = FootstepLoudnessMultiplier;
            rebound = ReboundCooldownMultiplier;
            grabSpeed = state.GrabMultipliers.TryGetValue(playerId, out float grabbed) ? grabbed : 1f;
            if (!state.Active || !playerId.IsValid || (state.AppliedActorEffects.TryGetValue(playerId, out int revision) &&
                revision == state.ActorEffectsRevision)) return false;
            state.AppliedActorEffects[playerId] = state.ActorEffectsRevision;
            return true;
        }

        public bool TryGetHunterEffects(EntityId hunterId, out FlashlightSample light, out FlashlightSample afterimage, out float lifetime)
        {
            light = state.HasAim ? Light(state.FlashlightEnabled) : default;
            afterimage = state.Afterimage;
            lifetime = afterimage.Enabled ? Mathf.Max(0f, (float)(state.AfterimageExpires - state.Elapsed)) : 0f;
            return state.Active && hunterId.IsValid && state.BoundHunters.Add(hunterId);
        }

        public HorrorEffectFact[] DrainFacts()
        {
            if (state.Facts.Count == 0) return Array.Empty<HorrorEffectFact>();
            HorrorEffectFact[] facts = state.Facts.ToArray();
            state.Facts.Clear();
            return facts;
        }

        private void TickFlame()
        {
            bool shouldDim = Has(ProgressionTraits.UnquietFlame) && state.SprintHeld && state.HasMovement &&
                state.Movement.InputLockSeconds <= 0f && state.Movement.Velocity.sqrMagnitude > 0.01f;
            if (!shouldDim) { RestoreFlame(); return; }
            if (state.FlameDimmed && state.Elapsed < state.NextFlameUpdate) return;
            state.FlameDimmed = true;
            state.NextFlameUpdate = state.Elapsed + config.FlamePositionInterval;
            state.Facts.Add(new HorrorEffectFact(HorrorEffectKind.FlameDim, position: state.Movement.Position,
                radius: config.FlameRadius, value: config.FlameDimMultiplier));
        }

        private void RestoreFlame()
        {
            if (!state.FlameDimmed) return;
            state.FlameDimmed = false;
            state.Facts.Add(new HorrorEffectFact(HorrorEffectKind.FlameDim, position: state.Movement.Position,
                radius: config.FlameRadius, value: 1f));
        }

        private void EndAfterimage()
        {
            if (!state.Afterimage.Enabled) return;
            FlashlightSample old = state.Afterimage;
            state.Afterimage = new FlashlightSample(old.Source, state.Tick, false,
                old.Origin, old.Direction, old.Range, old.ConeDegrees);
            state.Facts.Add(new HorrorEffectFact(HorrorEffectKind.Afterimage, light: state.Afterimage));
        }

        private void PublishLight()
        {
            if (state.Active && state.HasAim)
                state.Facts.Add(new HorrorEffectFact(HorrorEffectKind.Flashlight, light: Light(state.FlashlightEnabled)));
        }

        private FlashlightSample Light(bool enabled)
        {
            float multiplier = Finite(state.Effects.FlashlightRangeMultiplier)
                ? Mathf.Clamp(state.Effects.FlashlightRangeMultiplier, 0.05f, 5f) : 1f;
            float cone = config.FlashlightCone * (Has(ProgressionTraits.ShutteredLens) ? config.ShutteredConeMultiplier : 1f);
            return new FlashlightSample(state.Aim.Source, Math.Max(state.Aim.Tick, state.Tick), enabled,
                state.Aim.Origin, state.Aim.Direction, config.FlashlightRange * multiplier, cone);
        }

        private void Schedule(EntityId source, Vector3 position, float loudness, float delay, ProgressionTraits trait)
        {
            if (state.PendingNoises.Count >= config.MaximumPendingNoises) return;
            state.PendingNoises.Add(new HorrorScheduledNoise(source, position, loudness, state.Elapsed + delay, trait));
        }
        private bool Has(ProgressionTraits trait) => (state.Effects.Traits & trait) != 0;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
