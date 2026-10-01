// ============================================================================
// ShrineController.cs
// ============================================================================
// PURPOSE:
//   Assigns eligible shrine kinds to supplied sites and admits in-motion activation.
//   Contact is a positional envelope around a site, not a collision that stops movement.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Shrine.
// KEY RESPONSIBILITIES:
//   - Enforce depth, gap-edge placement, nearest Interact and single use.
//   - Return activation facts using the supplied committed player pose and tick.
//   - Select separated rooms with a seeded first site, never repeated room placements.
// DEPENDENCIES:
//   - Own state/config, Core values and injected System.Random only.
// USAGE NOTES:
//   No Domain-to-Domain dependencies. Assembly supplies reachable sites; event axes do not exclude shrines.
//   Contact wins inside its radius; a pressed Interact selects only the nearest live shrine.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Shrine
{
    public sealed class ShrineController
    {
        private readonly ShrineBehaviorState state;
        private readonly ShrineConfig config;
        private readonly System.Random random;
        public ShrineController(ShrineBehaviorState state, ShrineConfig config, System.Random random)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            foreach (float value in new[] { config.MinimumSpeed, config.ContactRadius, config.InteractRadius, config.VerticalTolerance })
                if (!float.IsFinite(value) || value < 0f) throw new ArgumentException("Invalid shrine activation range.");
            if (config.InteractRadius < config.ContactRadius || config.BaseCap < 0 || config.MoreShrinesBonus < 0)
                throw new ArgumentException("Invalid shrine count or Interact range.");
        }
        public int CountForFloor(int floor, bool moreShrines)
        {
            int count = 0;
            foreach (int start in config.CountFloors) if (floor >= start) count++;
            return count == 0 ? 0 : (int)Math.Min(int.MaxValue, (long)Math.Min(count, config.BaseCap) +
                (moreShrines ? config.MoreShrinesBonus : 0));
        }
        public IReadOnlyList<ShrinePlacement> Assemble(IReadOnlyList<ShrineSite> sites, int floor,
            bool moreShrines = false)
        {
            var placements = new List<ShrinePlacement>();
            int count = CountForFloor(floor, moreShrines);
            var candidates = sites == null ? new List<int>() : Enumerable.Range(0, sites.Count)
                .Where(i => sites[i].RoomId >= 0 && Finite(sites[i].Position)).ToList();
            while (candidates.Count > 0 && placements.Count < count)
            {
                int i = placements.Count == 0 ? candidates[random.Next(candidates.Count)] : candidates
                    .OrderByDescending(index => placements.Min(p => (p.Site.Position - sites[index].Position).sqrMagnitude)).ThenBy(index => index).First();
                candidates.Remove(i);
                var site = sites[i];
                var kinds = new List<ShrineKind>();
                foreach (var entry in config.Availability)
                {
                    if (floor >= entry.Floor && (entry.Kind != ShrineKind.Passage || site.GapEdge)) kinds.Add(entry.Kind);
                }
                if (kinds.Count > 0)
                {
                    placements.Add(new ShrinePlacement(i, kinds[random.Next(kinds.Count)], site));
                    candidates.RemoveAll(index => sites[index].RoomId == site.RoomId);
                }
            }
            Reset(placements);
            return Array.AsReadOnly(placements.ToArray());
        }
        public void Reset(IReadOnlyList<ShrinePlacement> placements)
        {
            var ids = new HashSet<int>();
            foreach (var placement in placements)
                if (placement.Id < 0 || !ids.Add(placement.Id) || placement.Site.RoomId < 0 || !Finite(placement.Site.Position))
                    throw new ArgumentException("Shrine placements require unique nonnegative ids and finite sites.");
            state.Placements.Clear(); state.Used.Clear(); state.Placements.AddRange(placements);
        }
        public bool TryActivate(Vector3 position, Vector3 velocity, InputButtons pressed, long tick, out ShrineActivatedFact fact)
        {
            fact = default;
            if (tick < 0 || !Finite(position) || !Finite(velocity) ||
                new Vector2(velocity.x, velocity.z).magnitude <= config.MinimumSpeed) return false;
            float nearest = float.PositiveInfinity;
            ShrinePlacement? selected = null;
            foreach (var placement in state.Placements)
            {
                if (state.Used.Contains(placement.Id)) continue;
                Vector3 delta = placement.Site.Position - position;
                if (Math.Abs(delta.y) > config.VerticalTolerance) continue;
                float distance = new Vector2(delta.x, delta.z).magnitude;
                float radius = (pressed & InputButtons.Interact) != 0 ? config.InteractRadius : config.ContactRadius;
                if (distance <= radius && distance < nearest) { nearest = distance; selected = placement; }
            }
            if (!selected.HasValue) return false;
            var value = selected.Value;
            state.Used.Add(value.Id);
            fact = new ShrineActivatedFact(value.Id, value.Kind, value.Site.RoomId, value.Site.Position, tick);
            return true;
        }
        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
