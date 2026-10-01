// ============================================================================
// HUDGuidancePresenter.cs
// ============================================================================
// PURPOSE:
//   Keeps published guidance identities stable when a snapshot changes list order.
//   Turns displayed bearings with explicit frame time and associates one remaining
//   cake number with the objective arrow, without inventing routes or destinations.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · HUD.
// KEY RESPONSIBILITIES:
//   - Retain an identity only while it is present in the current published channel.
//   - Resolve otherwise ambiguous same-kind targets deterministically.
//   - Smooth displayed bearings independently from the latest supplied facts.
//   - Select the remaining count, tint and visibility for one counter.
//   - Reserve clearance above the entire rotated arrow envelope.
// DEPENDENCIES:
//   Core guidance values, own HUDDriverState and Unity value math only.
// USAGE NOTES:
//   Stateless; caller owns state and supplies time, turn speed, color and spacing.
//   One visible arrow: Exit Sense wins, then Golden Sense, then the white objective.
//   Underlying channels retain their identities; no absent target is retained.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.HUD
{
    public sealed class HUDGuidancePresenter
    {
        public GuidanceTarget? Select(IReadOnlyList<GuidanceTarget> targets, GuidanceKind kind, GuidanceTarget? previous)
        {
            GuidanceTarget? selected = null;
            bool retained = false;
            if (targets == null) return null;
            foreach (var target in targets)
            {
                if (target.Kind != kind || !Usable(target.WorldDirection)) continue;
                bool same = previous.HasValue && SameIdentity(target, previous.Value);
                if (selected.HasValue && (retained && !same || retained == same && Compare(target, selected.Value) >= 0)) continue;
                selected = target; retained = same;
            }
            return selected;
        }

        private static bool SameIdentity(GuidanceTarget a, GuidanceTarget b)
        {
            if (a.Kind != b.Kind) return false;
            if (a.EntityId.IsValid || b.EntityId.IsValid) return a.EntityId == b.EntityId;
            if (a.AnchorId >= 0 || b.AnchorId >= 0) return a.AnchorId == b.AnchorId;
            return a.TargetPosition.Equals(b.TargetPosition);
        }

        private static int Compare(GuidanceTarget a, GuidanceTarget b)
        {
            int order = a.EntityId.Value.CompareTo(b.EntityId.Value);
            if (order == 0) order = a.AnchorId.CompareTo(b.AnchorId);
            if (order == 0) order = CompareVector(a.TargetPosition, b.TargetPosition);
            // Duplicate identity samples are still independent of enumeration order.
            if (order == 0) order = a.IsFallback.CompareTo(b.IsFallback);
            return order == 0 ? CompareVector(a.WorldDirection, b.WorldDirection) : order;
        }

        private static int CompareVector(Vector3 a, Vector3 b)
        {
            int order = a.x.CompareTo(b.x);
            if (order == 0) order = a.y.CompareTo(b.y);
            return order == 0 ? a.z.CompareTo(b.z) : order;
        }

        public void SyncVisibility(HUDDriverState state)
        {
            if (!state.DirectionVisible) state.ArrowInitialized = false;
            else if (!state.ArrowInitialized) { state.DisplayArrowDegrees = state.ArrowDegrees; state.ArrowInitialized = true; }
            if (!state.GoldenSenseVisible) state.GoldenArrowInitialized = false;
            else if (!state.GoldenArrowInitialized) { state.DisplayGoldenArrowDegrees = state.GoldenSenseArrowDegrees; state.GoldenArrowInitialized = true; }
        }

        public void Tick(HUDDriverState state, float deltaTime, float degreesPerSecond)
        {
            SyncVisibility(state);
            if (!Finite(deltaTime) || deltaTime <= 0f || !Finite(degreesPerSecond) || degreesPerSecond <= 0f) return;
            float step = (float)Math.Min(180d, (double)deltaTime * degreesPerSecond);
            if (state.DirectionVisible) state.DisplayArrowDegrees = Turn(state.DisplayArrowDegrees, state.ArrowDegrees, step);
            if (state.GoldenSenseVisible) state.DisplayGoldenArrowDegrees = Turn(state.DisplayGoldenArrowDegrees, state.GoldenSenseArrowDegrees, step);
        }

        private static float Turn(float current, float target, float step)
            => Mathf.Repeat(Mathf.MoveTowardsAngle(current, target, step) + 180f, 360f) - 180f;

        public bool ArrowVisible(HUDDriverState state) => !state.ModalOpen &&
            (state.ExitSenseVisible || state.GoldenSenseVisible || state.DirectionVisible);
        public float ActiveDegrees(HUDDriverState state) => state.ExitSenseVisible ? state.ExitSenseArrowDegrees
            : state.GoldenSenseVisible ? state.DisplayGoldenArrowDegrees : state.DisplayArrowDegrees;
        public Color ArrowTint(HUDDriverState state, Color gold, Color exit) => state.ExitSenseVisible ? exit
            : state.GoldenSenseVisible ? gold : Color.white;
        public bool UsesGoldenCount(HUDDriverState state) => !state.ExitSenseVisible && state.GoldenSenseVisible;
        public string CountText(HUDDriverState state) => UsesGoldenCount(state) ? state.GoldenText : state.CountText;
        public Color CountTint(HUDDriverState state, Color gold) => UsesGoldenCount(state) ? gold : Color.white;
        public bool CountVisible(HUDDriverState state) => !state.HiddenCount && state.ChromeVisible && ArrowVisible(state) &&
            (UsesGoldenCount(state) ? state.GoldenCountKnown : state.CountKnown);
        public float CounterBottom(float compassSize, float gap)
            => Math.Max(0f, compassSize) * (1f + (float)Math.Sqrt(2d)) * .5f + Math.Max(0f, gap);

        private static bool Usable(Vector3 direction) => Finite(direction.x) && Finite(direction.y) && Finite(direction.z) &&
            (direction.x != 0f || direction.y != 0f || direction.z != 0f);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
