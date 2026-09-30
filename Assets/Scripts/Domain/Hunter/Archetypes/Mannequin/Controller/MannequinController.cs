// ============================================================================
// MannequinController.cs
// ============================================================================
// PURPOSE:
//   Gates every shared movement and attack on light and camera observation.
//   It is silent except for an accepted snap/crunch catch. It waits at safe
//   light boundaries, with rare seeded light failures and an optional permanent
//   room-darkening curse leaving visible evidence behind it.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Freeze while observed, illuminated or under Wick; move only in darkness.
//   - Read capped neutral curse hooks and publish Core light/silence facts.
//   - Admit the distinct catch fact once per life for the Manager to publish.
// DEPENDENCIES:
//   - Hunter default seam, injected camera/world views, Core effects and facts.
// USAGE NOTES:
//   Unknown room/camera data holds safely. Light overrides are locally predicted
//   and must be routed by Session to Level; no engine calls occur here.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter.Archetypes.Default;
namespace Worsen.Domain.Hunter.Archetypes.Mannequin
{
    public sealed class MannequinController : DefaultHunterController, IHunterObservationRules
    {
        private readonly MannequinConfig _config;
        private readonly System.Random _random;
        private readonly MannequinBehaviorState _state = new MannequinBehaviorState();
        public MannequinController(MannequinConfig config, System.Random random)
        { _config = config ?? throw new ArgumentNullException(nameof(config)); _random = random ?? throw new ArgumentNullException(nameof(random)); }
        public bool Hold => _state.Hold || _state.Wick;
        public bool Silent => true;
        public float SpeedMultiplier => _state.Speed;
        public float LossMultiplier => 1f;
        public void Observe(HunterPlayerView view, bool clear, bool illuminated, IReadOnlyHunterWorldView world, bool wick)
        { _state.View = view; _state.Clear = clear; _state.Illuminated = illuminated; _state.World = world; _state.Wick = wick; }
        public override void Reset(HunterArchetypeContext context)
        {
            _state.Hold = true; _state.Wick = false; _state.View = default; _state.World = null;
            _state.CatchPublished = false;
            _state.Room = _state.FailureRoom = 0; _state.FailureRemaining = 0;
            _state.CheckRemaining = _config.FailureCheckSeconds; _state.LampStacks = -1;
            _state.LastTick = -1; _state.Speed = 1f; _state.BrokenRooms.Clear(); _state.Facts.Clear();
            _state.Facts.Enqueue(new MannequinFact(context.Hunter.Id, MannequinFactKind.SilentSoundSet, context.Tick));
        }
        private static int Stacks(HunterArchetypeContext context, string id, int cap) =>
            Mathf.Clamp(context.Effects?.Stacks(new EffectId(id)) ?? 0, 0, cap);
        public override void Tick(HunterArchetypeContext context)
        {
            if (!(context.DeltaTime > 0f) || float.IsInfinity(context.DeltaTime) || context.Tick <= _state.LastTick) return;
            _state.LastTick = context.Tick; _state.Hold = true;
            int lamps = Stacks(context, "mannequin-fewer-lamps", 3);
            if (lamps != _state.LampStacks)
            {
                _state.LampStacks = lamps;
                _state.Facts.Enqueue(new MannequinFact(context.Hunter.Id, MannequinFactKind.LampBudget, context.Tick,
                    value: Mathf.Pow(_config.FewerLampsMultiplier, lamps)));
            }
            _state.Speed = Mathf.Pow(_config.LongerStridesMultiplier, Stacks(context, "mannequin-longer-strides", 3));
            _state.FailureRemaining = Mathf.Max(0f, _state.FailureRemaining - context.DeltaTime);
            int room = HunterNavigationUtility.RoomAt(context.Level?.Graph, context.Hunter.Position);
            if (_state.Wick || !context.Player.IsAlive || !HunterViewUtility.Fresh(_state.View, context.Tick) ||
                room == 0 || _state.World == null || !_state.World.TryGetRoomLit(room, out bool lit)) return;
            bool entered = room != _state.Room; _state.Room = room;
            if (entered && lit && Stacks(context, "mannequin-broken-lights", 1) > 0 && _state.BrokenRooms.Add(room))
                _state.Facts.Enqueue(new MannequinFact(context.Hunter.Id, MannequinFactKind.RoomLightOverride,
                    context.Tick, room, permanent: true));
            if (_state.BrokenRooms.Contains(room)) lit = false;
            if (_state.FailureRemaining > 0f && _state.FailureRoom == room) lit = false;
            bool observed = _state.Clear && HunterViewUtility.Contains(_state.View,
                context.Hunter.Position + Vector3.up * _config.ObservationHeight,
                Stacks(context, "mannequin-peripheral-creep", 1) > 0 ? _config.DirectLookHalfAngle : 0f);
            if (lit && !observed && !_state.Illuminated && _state.FailureRemaining <= 0f)
            {
                _state.CheckRemaining -= context.DeltaTime;
                if (_state.CheckRemaining <= 0f)
                {
                    _state.CheckRemaining = _config.FailureCheckSeconds;
                    if (_random.NextDouble() < _config.FailureChance)
                    {
                        _state.FailureRoom = room; _state.FailureRemaining = _config.FailureSeconds;
                        lit = false;
                        _state.Facts.Enqueue(new MannequinFact(context.Hunter.Id, MannequinFactKind.RoomLightOverride,
                            context.Tick, room, lit, _config.FailureSeconds));
                    }
                }
            }
            _state.Hold = observed || lit || _state.Illuminated;
        }
        public bool TryCatch()
        {
            if (_state.CatchPublished) return false;
            _state.CatchPublished = true; return true;
        }
        public override bool FilterVisibility(bool visible, SightProbe probe, HunterArchetypeContext context) => !Hold && visible;
        public bool TakeFact(out MannequinFact fact)
        { fact = default; if (_state.Facts.Count == 0) return false; fact = _state.Facts.Dequeue(); return true; }
    }
}
