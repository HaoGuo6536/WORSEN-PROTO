// ============================================================================
// ProceduralPuzzleLayoutPresenter.cs
// ============================================================================
// PURPOSE:
//   Places an optional cage lane only in existing clear, connected room space.
//   Clearance against rotated geometry, portals and required anchors is checked
//   before assembly; no puzzle ever replaces a required edge or objective.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Select a seeded challenge in an authored lane; retain reward positions, not gold grants.
//   - Build cage and vault boxes that native navigation can validate before play.
// DEPENDENCIES:
//   - Own configs/layout and Core values only; no engine calls.
// USAGE NOTES:
//   The lane occupies the center of a flat cell, leaving walking bypasses on both
//   sides. Unsupported content fails visibly instead of dropping the puzzle budget.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralPuzzleLayoutPresenter
    {
        public IReadOnlyList<ProceduralPuzzlePlan> Build(ProceduralLayout layout, ProceduralChallengeConfig c,
            IReadOnlyList<ProceduralBlock> blocks, System.Random random)
        {
            if (ReferenceEquals(c, null) || layout.RoundIndex < c.PuzzleFirstRound || layout.Modules.Any(m => m.Kind == ProceduralModuleKind.MerchantRefuge))
                return Array.Empty<ProceduralPuzzlePlan>();
            var occupied = ProceduralGimmickUtility.Rooms(layout);
            if (layout.UsesTemplates && !layout.TemplateRooms.Any(r => r.Template.Kind == "puzzle" && r.Template.Gimmick == "puzzle"))
                return Array.Empty<ProceduralPuzzlePlan>();
            int budget = ProceduralGimmickUtility.Budget(c, layout.RoundIndex);
            if (layout.GimmickBudget != int.MaxValue && occupied.Count >= budget)
                return Array.Empty<ProceduralPuzzlePlan>();
            foreach (float v in new[] { c.LaneLength, c.LaneWidth, c.CageHeight, c.PanelThickness, c.ContactHeight,
                c.VaultHeight, c.Clearance, c.NearbyRadius, c.SequenceSeconds, c.SegmentSeconds, c.MovingSpeed })
                if (!(v > 0f) || float.IsInfinity(v)) throw new ArgumentException("Invalid puzzle dimensions/timing.");
            if (c.PuzzleFirstRound < 1 || (!layout.UsesTemplates && (c.LaneLength + 2f * c.Clearance >= layout.CellSize ||
                c.LaneWidth + 2f * c.Clearance >= layout.CellSize)) || c.PanelThickness >= c.LaneWidth * 0.5f ||
                c.VaultHeight >= c.CageHeight || c.ContactHeight >= c.VaultHeight)
                throw new ArgumentException("Puzzle does not fit the cell or base traversal envelope.");
            var kind = (ProceduralPuzzleKind)random.Next(4);
            foreach (var module in layout.Modules.Where(m => m.PocketId == 0 && m.RoomId != layout.Graph.ExitRoomId &&
                (layout.GimmickBudget == int.MaxValue || !occupied.Contains(m.RoomId)) &&
                (!layout.UsesTemplates || layout.TemplateRooms.Any(r => r.RoomId == m.RoomId && r.Template.Kind == "puzzle" && r.Template.Gimmick == "puzzle"))))
            foreach (var cell in ProceduralFootprintUtility.Volumes(layout, layout.Graph.Rooms[module.RoomId - 1]))
            foreach (bool alongX in new[] { false, true })
            foreach (float offset in new[] { 0f, -(layout.CellSize - c.LaneLength) * 0.25f, (layout.CellSize - c.LaneLength) * 0.25f })
            {
                var axis = alongX ? Vector3.right : Vector3.forward;
                var origin = new Vector3(cell.Center.x, 0f, cell.Center.z) + axis * offset;
                var template = layout.TemplateRooms.FirstOrDefault(r => r.RoomId == module.RoomId);
                if (template != null)
                {
                    var lane = template.Template.PuzzleSockets;
                    if (lane == null) throw new ArgumentException("Missing authored puzzle lane.");
                    if (lane.LaneLength != c.LaneLength || lane.LaneWidth != c.LaneWidth || lane.CageHeight != c.CageHeight ||
                        lane.PanelThickness != c.PanelThickness || lane.Clearance != c.Clearance)
                        throw new ArgumentException("Authored puzzle lane disagrees with challenge dimensions.");
                    axis = ProceduralTemplateUtility.Rotate(lane.Axis, template.Turns);
                    if (alongX != (axis.x != 0f) || offset != 0f) continue;
                    origin = ProceduralTemplateUtility.Point(template, lane.Origin, layout.Origin);
                }
                var size = new Vector3(c.LaneWidth + 2f * c.Clearance, c.CageHeight, c.LaneLength + 2f * c.Clearance);
                if (alongX) size = new Vector3(size.z, size.y, size.x);
                var envelope = new Bounds(origin + Vector3.up * (c.CageHeight * 0.5f + c.PanelThickness), size);
                if (!Contains(cell.Bounds, envelope.min) || !Contains(cell.Bounds, envelope.max)) continue;
                if (blocks.Any(b => b.HasCollision && WorldBounds(b).Intersects(envelope)) ||
                    layout.Interactables.Any(p => p.State.Kind == InteractableKind.KnockableProp &&
                        new Bounds(p.State.Position, p.Size).Intersects(envelope)) ||
                    layout.Graph.Anchors.Any(a => Contains(envelope, a.Position + Vector3.up * c.Clearance)) ||
                    layout.HunterSpawnPositions.Any(p => Contains(envelope, p + Vector3.up * c.Clearance))) continue;
                int id = 900000 + module.RoomId;
                var reward = new LevelAnchor(id, module.RoomId, CakeAnchorType.Risk, origin + axis * (c.LaneLength * 0.375f));
                return Array.AsReadOnly(new[] { new ProceduralPuzzlePlan(id, module.RoomId, kind, origin, alongX, reward, axis.x < 0f || axis.z < 0f) });
            }
            // A maximum is not a quota: a narrow organic floor need not invent a cage.
            if (layout.OrganicRooms.Count != 0) return Array.Empty<ProceduralPuzzlePlan>();
            throw new InvalidOperationException("No clear optional puzzle lane; retry seed.");
        }
        public Vector3 Point(ProceduralPuzzlePlan plan, ProceduralChallengeConfig c, int index)
            => plan.Origin + (plan.AlongX ? Vector3.right : Vector3.forward) * ((plan.Reversed ? -1f : 1f) * c.LaneLength * (-0.375f + index * 0.25f));
        public int Tile(ProceduralPuzzlePlan plan, ProceduralChallengeConfig c, Vector3 position)
        {
            var d = position - plan.Origin;
            float along = plan.AlongX ? d.x : d.z, across = plan.AlongX ? d.z : d.x;
            if (plan.Reversed) along = -along;
            if (Mathf.Abs(across) > c.LaneWidth * 0.5f || Mathf.Abs(along) >= c.LaneLength * 0.5f ||
                d.y < -c.ContactHeight || d.y > c.ContactHeight) return -1;
            return Mathf.Clamp(Mathf.FloorToInt((along / c.LaneLength + 0.5f) * 4f), 0, 3);
        }
        public IReadOnlyList<ProceduralBlock> Blocks(ProceduralPuzzlePlan p, ProceduralChallengeConfig c)
        {
            var result = new List<ProceduralBlock>();
            var reward = Point(p, c, 3);
            float depth = c.LaneLength * 0.25f;
            Add(reward + new Vector3(0f, c.CageHeight, 0f), new Vector3(c.LaneWidth, c.PanelThickness, depth));
            foreach (float sign in new[] { -1f, 1f })
                Add(reward + Rotate(new Vector3(sign * c.LaneWidth * 0.5f, c.CageHeight * 0.5f, 0f)),
                    new Vector3(c.PanelThickness, c.CageHeight, depth));
            Add(reward + Rotate(new Vector3(0f, c.CageHeight * 0.5f, depth * (p.Reversed ? -.5f : .5f))), new Vector3(c.LaneWidth, c.CageHeight, c.PanelThickness));
            // Last cage block is the retracting gate; omit only it from the static bake.
            Add(reward + Rotate(new Vector3(0f, c.CageHeight * 0.5f, depth * (p.Reversed ? .5f : -.5f))), new Vector3(c.LaneWidth, c.CageHeight, c.PanelThickness));
            if (p.Kind == ProceduralPuzzleKind.TimedVaults)
                for (int i = 0; i < 3; i++)
                {
                    var center = Point(p, c, i);
                    var axis = (p.AlongX ? Vector3.right : Vector3.forward) * (p.Reversed ? -1f : 1f);
                    result.Add(new ProceduralBlock(p.RoomId, ProceduralSurfaceKind.Wall, center + Vector3.up * (c.VaultHeight * 0.5f),
                        Rotate(new Vector3(c.LaneWidth, c.VaultHeight, c.PanelThickness)), p.Id * 10 + i,
                        TraversalSurfaceKind.Vault, center - axis * (depth * 0.4f), center + axis * (depth * 0.4f)));
                }
            return result.AsReadOnly();
            Vector3 Rotate(Vector3 v) => p.AlongX ? new Vector3(v.z, v.y, v.x) : v;
            void Add(Vector3 position, Vector3 size) => result.Add(new ProceduralBlock(p.RoomId, ProceduralSurfaceKind.Wall, position, Rotate(size)));
        }
        public string Manifest(IReadOnlyList<ProceduralPuzzlePlan> plans) => string.Concat(plans.Select(p =>
            "|Puzzle:" + p.Id + "," + p.RoomId + "," + p.Kind + ",reward=unassigned,socket=" + p.Reward.Id + "," + p.AlongX + ",reversed=" + p.Reversed + "," +
            string.Join(",", new[] { p.Origin.x, p.Origin.y, p.Origin.z }.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))));
        private static bool Contains(Bounds b, Vector3 p) => p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y &&
            p.y <= b.max.y && p.z >= b.min.z && p.z <= b.max.z;
        private static Bounds WorldBounds(ProceduralBlock block)
        {
            var bounds = new Bounds(block.Center, Vector3.zero);
            for (int i = 0; i < 8; i++) bounds.Encapsulate(block.Center + block.Rotation * new Vector3(
                ((i & 1) == 0 ? -1 : 1) * block.Size.x, ((i & 2) == 0 ? -1 : 1) * block.Size.y,
                ((i & 4) == 0 ? -1 : 1) * block.Size.z) * 0.5f);
            return bounds;
        }
    }
}
