// ============================================================================
// HorrorCollapsePresenter.cs
// ============================================================================
// PURPOSE:
//   Computes how much of the current floor has died from published room phases.
//   Smooths that fraction with injected time so fog and torch loss share one clock
//   without reading gameplay state or making the initial sweep unreadable.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Count Closed rooms and a configurable partial weight for Encroaching rooms.
//   - Exclude the protected exit, unknown room identities and duplicate observations.
//   - Compute monotonic fog/torch outputs and multiply the existing darkness hook.
// DEPENDENCIES:
//   - Core room payloads, own HorrorDriverState/Config and pure Unity value math.
// USAGE NOTES:
//   Stateless over caller-owned state. BeginFloor clears identities and smoothing;
//   only collapsible rooms form the denominator so all non-exit rooms Closed is 100%.
//   Legacy scenes without room facts retain their authored distance-fog settings.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Horror
{
    public static class HorrorCollapsePresenter
    {
        public static void BeginFloor(HorrorDriverState state, IReadOnlyList<GeneratedRoomSample> rooms, int? exitRoom)
        {
            Reset(state);
            state.HasCollapseFloor = rooms != null;
            if (rooms == null) return;
            foreach (var room in rooms)
                if (room.RoomId != exitRoom) state.CollapseRooms[room.RoomId] = RoomPhase.Open;
        }

        public static void Reset(HorrorDriverState state)
        {
            state.CollapseRooms.Clear();
            state.HasCollapseFloor = false;
            state.CollapseFraction = state.SmoothedCollapseFraction = 0f;
        }

        public static void Observe(HorrorDriverState state, RoomDestructionSample sample, HorrorDriverConfig config)
        {
            if (!state.CollapseRooms.ContainsKey(sample.RoomId)) return;
            state.CollapseRooms[sample.RoomId] = sample.Phase;
            state.CollapseFraction = Fraction(state.CollapseRooms, config.EncroachingCollapseWeight);
        }

        public static float Fraction(IReadOnlyDictionary<int, RoomPhase> rooms, float encroachingWeight)
        {
            if (rooms == null || rooms.Count == 0) return 0f;
            float weight = Unit(encroachingWeight, 0f), total = 0f;
            foreach (var phase in rooms.Values)
                total += phase == RoomPhase.Closed ? 1f :
                    phase == RoomPhase.Encroaching || phase == RoomPhase.Tearing ? weight : 0f;
            return total / rooms.Count;
        }

        public static bool Tick(HorrorDriverState state, HorrorDriverConfig config, float dt)
        {
            if (!state.HasCollapseFloor || !Finite(dt) || dt <= 0f) return false;
            float previous = state.SmoothedCollapseFraction;
            float seconds = config.CollapseSmoothingSeconds;
            state.SmoothedCollapseFraction = !Finite(seconds) || seconds <= 0f ? state.CollapseFraction :
                Mathf.MoveTowards(previous, state.CollapseFraction, dt / seconds);
            return previous != state.SmoothedCollapseFraction;
        }

        public static float FogNear(HorrorDriverState state, HorrorDriverConfig config)
        {
            float sweep = NonNegative(config.SweepFogNearMeters);
            float collapsed = Mathf.Min(sweep, NonNegative(config.CollapsedFogNearMeters));
            return Mathf.Lerp(sweep, collapsed, Unit(state.SmoothedCollapseFraction, 0f));
        }

        public static float TorchMultiplier(HorrorDriverState state, HorrorDriverConfig config)
        {
            float sweep = Unit(config.SweepTorchCountMultiplier, 1f);
            float collapsed = Mathf.Min(sweep, Unit(config.CollapsedTorchCountMultiplier, sweep));
            float collapse = state.HasCollapseFloor ? Mathf.Lerp(sweep, collapsed, Unit(state.SmoothedCollapseFraction, 0f)) : 1f;
            return Unit(state.TorchCountMultiplier, 1f) * collapse;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Unit(float value, float fallback) => Finite(value) ? Mathf.Clamp01(value) : fallback;
        private static float NonNegative(float value) => Finite(value) ? Mathf.Max(0f, value) : 0f;
    }
}
