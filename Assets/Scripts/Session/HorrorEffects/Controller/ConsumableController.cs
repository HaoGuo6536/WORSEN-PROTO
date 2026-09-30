// ============================================================================
// ConsumableController.cs
// ============================================================================
// PURPOSE:
//   Computes item admission, floor-local effect lifetimes and face-aimed stuns.
//   Input and time are injected by the existing Run tick. Progression commits a
//   use before CommitUse; no item action changes player movement or locks input.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Session · HorrorEffects.
// KEY RESPONSIBILITIES:
//   - Validate targets/restrictions, pause Gauze while still and expire speed bursts.
//   - Require continuous face aim, spend one charge and recharge with Steady Hand.
//   - Retain throws until an observed impact and publish oil/door/stun facts once.
// DEPENDENCIES:
//   - Own BehaviorState/Config; immutable Core input, effect and world observations.
// USAGE NOTES:
//   BeginFloor clears world effects but preserves charge; ResetRun refills it.
//   Tick is idempotent by floor-local tick. Engine sweeps are supplied by the Manager.
//   StunCone/VialCone are full cone angles. No random decisions are required.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Session.HorrorEffects
{
    public sealed class ConsumableController
    {
        private readonly ConsumableBehaviorState state;
        private readonly HorrorEffectsConfig config;
        public ConsumableController(ConsumableBehaviorState state, HorrorEffectsConfig config)
        { this.state = state ?? throw new ArgumentNullException(nameof(state)); this.config = config ?? throw new ArgumentNullException(nameof(config)); }
        public bool Charged => state.RechargeRemaining <= 0d;
        public bool Active => state.Active;
        public bool RevivalPending => state.Reviving.IsValid;
        public float SpeedMultiplier => state.BurstRemaining > 0d ? config.AdrenalineMultiplier : 1f;
        public float HealingRate => config.GauzeHealing / config.GauzeSeconds;
        public float RevivalHealthFraction => config.RevivalHealthFraction;
        public void ResetRun() { Suspend(); state.RechargeRemaining = 0d; }
        public void BeginFloor() { Suspend(); state.Active = true; state.LastTick = -1; state.Clock = 0d; }
        public void Suspend()
        {
            foreach (var jam in state.Jams) state.DoorFacts.Add(Jam(jam.Key, jam.Value.Position, false));
            state.Active = state.Hold = false; state.AimSeconds = 0d; state.AimTarget = EntityId.None;
            state.GauzeRemaining = state.BurstRemaining = 0d; state.Reviving = EntityId.None;
            state.Flights.Clear(); state.Patches.Clear(); state.OilContacts.Clear(); state.Jams.Clear();
            state.Stuns.Clear(); state.Slips.Clear(); state.Noises.Clear();
        }
        public bool BeginRevival(EntityId player)
        {
            if (!state.Active || !player.IsValid || RevivalPending) return false;
            state.Reviving = player; state.GauzeRemaining = state.BurstRemaining = state.AimSeconds = 0d;
            state.Hold = false; return true;
        }
        public bool CompleteRevival(EntityId player)
        {
            if (!player.IsValid || state.Reviving != player) return false;
            state.Reviving = EntityId.None; return true;
        }
        public void ReceiveInput(InputFrame frame) => state.Hold = (frame.Held & InputButtons.UseItem) != 0;
        public static int CycleDirection(InputFrame frame) =>
            ((frame.Pressed & InputButtons.CycleConsumable) != 0 ? 1 : 0) -
            ((frame.Pressed & InputButtons.CycleConsumablePrevious) != 0 ? 1 : 0);

        public bool CanUse(string id, float health, float maximum, FlashlightSample aim,
            IReadOnlyList<HunterFaceSample> hunters, IReadOnlyList<InteractableState> doors,
            Vector3 position, out EntityId target, out int door)
        {
            target = EntityId.None; door = -1;
            if (!state.Active || RevivalPending || !Finite(health) || !Finite(maximum) || health <= 0f || maximum <= 0f) return false;
            switch (id)
            {
                case "firecracker": return ValidAim(aim);
                case "gauze": return state.GauzeRemaining <= 0d;
                case "smelling-salts": case "wax-ward": case "oil-flask": return true;
                case "adrenaline": return health <= maximum * config.CriticalFraction && state.BurstRemaining <= 0d;
                case "glass-vial":
                    target = Face(aim, hunters, config.VialRange, config.VialCone); return target.IsValid;
                case "doorstop":
                    if (!ValidAim(aim) || doors == null) return false;
                    float nearest = config.DoorstopRange * config.DoorstopRange;
                    foreach (var candidate in doors)
                    {
                        Vector3 offset = candidate.Position - position;
                        if (candidate.Kind != InteractableKind.Door || candidate.Value == InteractableStateValue.Broken ||
                            state.Jams.ContainsKey(candidate.Id) || Vector3.Dot(offset, aim.Direction) >= 0f || offset.sqrMagnitude > nearest) continue;
                        nearest = offset.sqrMagnitude; door = candidate.Id;
                    }
                    return door > 0;
                default: return false;
            }
        }
        public void CommitUse(string id, FlashlightSample aim, Vector3 position, EntityId target, int door, Vector3 doorPosition, long tick)
        {
            switch (id)
            {
                case "firecracker": state.Flights.Add(++state.NextId, (aim.Origin, aim.Direction.normalized * config.ThrowSpeed, state.Clock + config.ThrowLifetime)); break;
                case "gauze": state.GauzeRemaining = config.GauzeSeconds; break;
                case "adrenaline": state.BurstRemaining = config.AdrenalineSeconds; break;
                case "oil-flask": state.Patches.Add(++state.NextId, (position, state.Clock + config.OilLifetime)); break;
                case "glass-vial": state.Stuns.Add(new HunterStunFact(target, config.VialSeconds, config.VialStrength, tick)); break;
                case "doorstop":
                    state.Jams.Add(door, (doorPosition, state.Clock + config.DoorstopSeconds));
                    state.DoorFacts.Add(Jam(door, doorPosition, true)); break;
            }
        }
        public bool Tick(float dt, long tick, FlashlightSample light, IReadOnlyList<HunterFaceSample> hunters,
            Vector3 playerVelocity, bool playerAlive, int steadyHandStacks, out float healingSeconds)
        {
            healingSeconds = 0f;
            if (!state.Active || !Finite(dt) || dt <= 0f || tick <= state.LastTick) return false;
            state.LastTick = tick; state.Clock += dt;
            double aimDelta = Math.Max(0d, dt - state.RechargeRemaining);
            state.RechargeRemaining = Math.Max(0d, state.RechargeRemaining - dt);
            state.BurstRemaining = Math.Max(0d, state.BurstRemaining - dt);
            if (playerAlive && playerVelocity.sqrMagnitude > config.MovingSpeed * config.MovingSpeed)
            { healingSeconds = (float)Math.Min(dt, state.GauzeRemaining); state.GauzeRemaining -= healingSeconds; }
            EntityId face = playerAlive && state.Hold && light.Enabled && Charged
                ? Face(light, hunters, light.Range, Math.Min(light.ConeDegrees, config.StunCone)) : EntityId.None;
            if (!face.IsValid || face != state.AimTarget) state.AimSeconds = 0d;
            state.AimTarget = face;
            if (face.IsValid)
            {
                state.AimSeconds += aimDelta;
                if (state.AimSeconds + 0.000001d >= config.StunAimSeconds)
                {
                    state.Stuns.Add(new HunterStunFact(face, config.StunSeconds, config.StunStrength, tick));
                    state.AimSeconds = 0d;
                    state.RechargeRemaining = config.StunRechargeSeconds * Math.Pow(config.SteadyHandMultiplier, Math.Max(0, steadyHandStacks));
                }
            }
            foreach (int id in new List<int>(state.Jams.Keys))
                if (state.Clock >= state.Jams[id].Expires) EndJam(id, false, tick);
            foreach (int id in new List<int>(state.Patches.Keys))
            {
                var patch = state.Patches[id];
                if (state.Clock >= patch.Expires)
                { state.Patches.Remove(id); state.OilContacts.RemoveWhere(pair => pair.Patch == id); continue; }
                if (hunters == null) continue;
                foreach (var hunter in hunters)
                {
                    var key = (id, hunter.HunterId);
                    if ((hunter.Feet - patch.Position).sqrMagnitude > config.OilRadius * config.OilRadius) { state.OilContacts.Remove(key); continue; }
                    if (hunter.HunterId.IsValid && state.OilContacts.Add(key))
                        state.Slips.Add(new HunterSlipFact(hunter.HunterId, id, patch.Position, config.OilSlipSeconds, tick));
                }
            }
            return true;
        }
        public (int Id, Vector3 From, Vector3 To)[] AdvanceThrows(float dt)
        {
            var result = new List<(int, Vector3, Vector3)>();
            if (!state.Active || !Finite(dt) || dt <= 0f) return result.ToArray();
            foreach (int id in new List<int>(state.Flights.Keys))
            {
                var flight = state.Flights[id];
                if (state.Clock >= flight.Expires) { state.Flights.Remove(id); continue; }
                Vector3 next = flight.Position + flight.Velocity * dt - Vector3.up * (config.ThrowGravity * dt * dt * 0.5f);
                state.Flights[id] = (next, flight.Velocity - Vector3.up * (config.ThrowGravity * dt), flight.Expires);
                result.Add((id, flight.Position, next));
            }
            return result.ToArray();
        }
        public bool Impact(int flightId, Vector3 point, long tick)
        {
            if (!state.Flights.Remove(flightId)) return false;
            state.Noises.Add(new NoiseEvent(EntityId.None, point, config.FirecrackerLoudness, tick)); return true;
        }
        public bool IsJammed(int door) => state.Jams.ContainsKey(door);
        public float DoorBreakSeconds => config.DoorBreakSeconds;
        public bool EndJam(int door, bool broken, long tick)
        {
            if (!state.Jams.TryGetValue(door, out var jam)) return false;
            state.Jams.Remove(door); state.DoorFacts.Add(Jam(door, jam.Position, false));
            if (broken) state.Noises.Add(new NoiseEvent(EntityId.None, jam.Position, config.DoorBreakLoudness, tick));
            return true;
        }
        private DoorJamFact Jam(int id, Vector3 position, bool active) => new DoorJamFact(id, position, active ? config.DoorstopSeconds : 0f, config.DoorBreakSeconds, active);
        public HunterStunFact[] DrainStuns() { var result = state.Stuns.ToArray(); state.Stuns.Clear(); return result; }
        public HunterSlipFact[] DrainSlips() { var result = state.Slips.ToArray(); state.Slips.Clear(); return result; }
        public DoorJamFact[] DrainDoors() { var result = state.DoorFacts.ToArray(); state.DoorFacts.Clear(); return result; }
        public NoiseEvent[] DrainNoises() { var result = state.Noises.ToArray(); state.Noises.Clear(); return result; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidAim(FlashlightSample aim) => aim.Source.IsValid && aim.Direction.sqrMagnitude > 0f;
        private static EntityId Face(FlashlightSample aim, IReadOnlyList<HunterFaceSample> hunters, float range, float cone)
        {
            if (!ValidAim(aim) || hunters == null) return EntityId.None;
            EntityId result = EntityId.None;
            float nearest = range * range, cosine = Mathf.Cos(cone * 0.5f * Mathf.Deg2Rad);
            foreach (var sample in hunters)
            {
                Vector3 offset = sample.Head - aim.Origin;
                if (!sample.HunterId.IsValid || !sample.Visible || offset.sqrMagnitude <= 0f || offset.sqrMagnitude > nearest ||
                    Vector3.Dot(offset.normalized, aim.Direction.normalized) < cosine) continue;
                result = sample.HunterId; nearest = offset.sqrMagnitude;
            }
            return result;
        }
    }
}
