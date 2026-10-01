// ============================================================================
// ProceduralFurnishedUtility.cs
// ============================================================================
// PURPOSE:
//   Admits authored interaction spaces alongside the legacy shell contract.
//   Typed sockets are useful only when their envelopes are supported and empty;
//   this utility rejects malformed or obstructed shrine, puzzle and seam data.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Validate singular sockets, cardinal axes and matching reserved envelopes.
//   - Validate optional sealed Passage destinations and transition apertures.
//   - Transform reservations for later placement and dressing consumers.
// DEPENDENCIES:
//   - Own template definitions and coordinate utility; Unity value types only.
// USAGE NOTES:
//   All dimensions come from the manifest. No geometry is repaired or rescaled.
//   Reservations may overlap each other but never collision-bearing furniture.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    public static class ProceduralFurnishedUtility
    {
        public static void Validate(ProceduralTemplateCatalogue catalogue, ProceduralRoomTemplate room)
        {
            void Require(bool valid, string reason) { if (!valid) throw new ArgumentException(room.Id + ": " + reason); }
            Require(room.FurnishingVersion >= 0 && room.FurnishingVersion <= 1, "unknown furnishing version");
            Require(room.ShrineSockets != null && room.ReservedAreas != null, "missing socket/reservation arrays");
            Require(room.ShrineSockets.Length == (room.Kind == "shrine" ? 1 : 0), "exactly one shrine socket in shrine rooms only");
            Require((room.PuzzleSockets != null) == (room.Kind == "puzzle"), "exactly one lane in puzzle rooms only");
            Require(room.Kind != "shrine" || room.Gimmick == "none", "shrine is not a gimmick");
            Require(room.Kind != "shrine" || room.PassageGap != null, "shrine requires a reserved Passage pocket");
            Require(room.Kind != "puzzle" || room.Gimmick == "puzzle", "dedicated puzzle requires puzzle gimmick");
            Require(room.ReservedAreas.All(r => r != null && !string.IsNullOrWhiteSpace(r.Id)) &&
                room.ReservedAreas.Select(r => r.Id).Distinct().Count() == room.ReservedAreas.Length, "invalid reservation identity");
            var kit = catalogue.Kit.ToDictionary(p => p.Id);
            foreach (var r in room.ReservedAreas)
            {
                Require(Positive(r.Size) && ProceduralTemplateUtility.Finite(r.Center), "invalid reservation envelope");
                var bounds = new Bounds(r.Center, r.Size);
                foreach (var corner in Corners(bounds)) Require(ProceduralTemplateUtility.Inside(room, corner), "unsupported reservation");
                foreach (var p in room.Pieces)
                {
                    var piece = kit[p.Id];
                    if (!p.Collision || !piece.Collision || piece.Kind == "floor" || piece.Kind == "ceiling" || piece.Kind == "decal") continue;
                    // Passage deliberately retains its boundary seal until activation.
                    // Only the declared approach may meet that boundary wall; furniture
                    // and walls deeper inside the room remain ordinary obstructions.
                    if (r.Id == "passage-approach" && room.PassageGap != null && piece.Kind == "wall")
                    {
                        var n = ProceduralTemplateUtility.Direction(room.PassageGap.Side);
                        var normal = new Vector3(n.x, 0f, n.y);
                        var line = ProceduralTemplateValidationUtility.Segment(p, piece);
                        if (Math.Abs(Vector3.Dot(line.a - room.PassageGap.Edge, normal)) <= piece.Size.z * .5f + .001f &&
                            Math.Abs(Vector3.Dot(line.b - room.PassageGap.Edge, normal)) <= piece.Size.z * .5f + .001f) continue;
                    }
                    double angle = p.RotY * Math.PI / 180d;
                    var size = new Vector3((float)(Math.Abs(Math.Cos(angle)) * piece.Size.x + Math.Abs(Math.Sin(angle)) * piece.Size.z),
                        piece.Size.y, (float)(Math.Abs(Math.Sin(angle)) * piece.Size.x + Math.Abs(Math.Cos(angle)) * piece.Size.z));
                    Require(!Overlaps(bounds, new Bounds(p.Position + Vector3.up * (piece.Size.y * .5f), size)), "occupied reservation: " + r.Id + "/" + p.Id);
                }
            }
            void Reserved(string id, Bounds envelope)
            {
                var r = room.ReservedAreas.SingleOrDefault(a => a.Id == id);
                Require(r != null && Corners(envelope).All(p => Contains(new Bounds(r.Center, r.Size), p)), "missing/undersized " + id);
            }
            foreach (var s in room.ShrineSockets)
            {
                Require(s != null && !string.IsNullOrWhiteSpace(s.Id) && Cardinal(s.Facing) && Positive(s.ModelEnvelope) && Positive(s.InteractionSize) &&
                    ProceduralTemplateUtility.Finite(s.Position) && ProceduralTemplateUtility.Finite(s.InteractionCenter) &&
                    s.Position.y == 0f && s.InteractionCenter.y == 0f, "invalid shrine socket");
                Reserved("shrine-model", new Bounds(s.Position + Vector3.up * (s.ModelEnvelope.y * .5f), s.ModelEnvelope));
                Reserved("shrine-interaction", new Bounds(s.InteractionCenter + Vector3.up * (s.InteractionSize.y * .5f), s.InteractionSize));
            }
            var lane = room.PuzzleSockets;
            if (lane != null)
            {
                Require(Cardinal(lane.Axis) && ProceduralTemplateUtility.Finite(lane.Origin) && lane.Origin.y == 0f &&
                    new[] { lane.LaneLength, lane.LaneWidth, lane.CageHeight, lane.PanelThickness, lane.Clearance }.All(v => v > 0f && ProceduralTemplateUtility.Finite(v)), "invalid puzzle dimensions");
                Require(lane.PanelThickness < lane.LaneWidth * .5f && lane.Steps != null && lane.Steps.Length == 3, "invalid puzzle steps");
                for (int i = 0; i < 4; i++) Require(((i == 3 ? lane.Reward : lane.Steps[i]) -
                    (lane.Origin + lane.Axis * (lane.LaneLength * (-.375f + i * .25f)))).sqrMagnitude < .000001f, "displaced puzzle step/reward");
                Require(room.GoldenCake.Length == 1 && room.GoldenCake[0] == lane.Reward, "reward socket mismatch");
                Require(lane.SupportedKinds != null && lane.SupportedKinds.Length == 4 && lane.SupportedKinds.Distinct().Count() == 4 &&
                    lane.SupportedKinds.All(k => Enum.GetNames(typeof(ProceduralPuzzleKind)).Contains(k)), "unknown puzzle kinds");
                var size = new Vector3(lane.LaneWidth + 2f * lane.Clearance, lane.CageHeight, lane.LaneLength + 2f * lane.Clearance);
                if (lane.Axis.x != 0f) size = new Vector3(size.z, size.y, size.x);
                Reserved("puzzle-envelope", new Bounds(lane.Origin + Vector3.up * (lane.CageHeight * .5f + lane.PanelThickness), size));
            }
            var gap = room.PassageGap;
            if (gap != null)
            {
                Require(room.Kind == "shrine" && gap.OptionalOnly && gap.SealedUntilActivated && Positive(new Vector3(gap.Width, gap.GapLength, 1f)) &&
                    gap.PocketTurns >= 0 && gap.PocketTurns < 4 && ProceduralTemplateUtility.Finite(gap.Edge) && ProceduralTemplateUtility.Finite(gap.Landing), "invalid Passage contract");
                var n = ProceduralTemplateUtility.Direction(gap.Side); var normal = new Vector3(n.x, 0f, n.y);
                Require(gap.Edge == ProceduralTemplateUtility.Door(new ProceduralTemplateDoor { Cell = gap.EdgeCell, Side = gap.Side }) &&
                    room.Footprint.Contains(gap.EdgeCell) && !room.Footprint.Contains(gap.EdgeCell + n) &&
                    (gap.Landing - gap.Edge - normal * gap.GapLength).sqrMagnitude < .000001f &&
                    room.ShrineSockets[0].Facing == normal && Vector3.Cross(gap.Edge - room.ShrineSockets[0].Position, normal).sqrMagnitude < .000001f,
                    "Passage is not a collinear outward boundary gap");
                var approachStart = room.ShrineSockets[0].InteractionCenter;
                var approachSize = gap.Edge - approachStart;
                Require(Vector3.Dot(approachSize, normal) > 0f, "Passage approach points inward");
                Reserved("passage-approach", new Bounds((approachStart + gap.Edge) * .5f + Vector3.up,
                    new Vector3(normal.x == 0f ? gap.Width : Math.Abs(approachSize.x), 2f,
                        normal.z == 0f ? gap.Width : Math.Abs(approachSize.z))));
                var pocket = catalogue.Templates.SingleOrDefault(t => t.Id == gap.PocketTemplateId);
                // Pocket cake lines are finalized from collision by the Passage
                // presenter, so a legacy staging golden socket is not required.
                Require(pocket != null && pocket.Kind == "room" && pocket.Gimmick == "none" && pocket.MinRound <= room.MinRound, "invalid Passage pocket template");
                var local = ProceduralTemplateUtility.Rotate(gap.Landing + normal * .1f - new Vector3(gap.PocketOffset.x * 2f, 0f, gap.PocketOffset.y * 2f), (4 - gap.PocketTurns) % 4);
                Require(ProceduralTemplateUtility.Inside(pocket, local), "Passage landing misses its pocket");
            }
            var transition = room.Transition;
            if (transition != null)
            {
                Require(room.Kind == "room" && room.Gimmick == "none" && !string.IsNullOrWhiteSpace(transition.Purpose) &&
                    transition.CompatibleThemes != null && transition.CompatibleThemes.Length > 0 &&
                    transition.CompatibleThemes.Distinct().Count() == transition.CompatibleThemes.Length &&
                    transition.CompatibleThemes.All(t => t != catalogue.Theme && new[] { "castle", "hospital", "school", "basement" }.Contains(t)), "invalid transition themes");
                Require(transition.Doors != null && transition.Doors.Length > 0 && transition.Doors.All(d => d != null) &&
                    transition.Doors.Select(d => d.Index).Distinct().Count() == transition.Doors.Length, "invalid transition doors");
                foreach (var d in transition.Doors) Require(d.Index >= 0 && d.Index < room.Doors.Length && d.ClearWidth == 3.2f &&
                    d.ClearHeight == 2.8f && d.FloorY == 0f && !string.IsNullOrWhiteSpace(d.Seam), "invalid transition aperture");
            }
        }
        public static Bounds Reservation(ProceduralTemplateRoom room, ProceduralTemplateReservation area, Vector2 origin)
        {
            var size = ProceduralTemplateUtility.Rotate(area.Size, room.Turns);
            return new Bounds(ProceduralTemplateUtility.Point(room, area.Center, origin), new Vector3(Math.Abs(size.x), size.y, Math.Abs(size.z)));
        }
        private static bool Cardinal(Vector3 v) => v == Vector3.right || v == Vector3.left || v == Vector3.forward || v == Vector3.back;
        private static bool Contains(Bounds b, Vector3 p) => p.x >= b.min.x - .00001f && p.x <= b.max.x + .00001f &&
            p.y >= b.min.y - .00001f && p.y <= b.max.y + .00001f && p.z >= b.min.z - .00001f && p.z <= b.max.z + .00001f;
        private static bool Positive(Vector3 v) => ProceduralTemplateUtility.Finite(v) && v.x > 0f && v.y > 0f && v.z > 0f;
        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                yield return b.center + Vector3.Scale(b.extents, new Vector3(x, y, z));
        }
        private static bool Overlaps(Bounds a, Bounds b) => a.min.x < b.max.x - .001f && a.max.x > b.min.x + .001f &&
            a.min.y < b.max.y - .001f && a.max.y > b.min.y + .001f && a.min.z < b.max.z - .001f && a.max.z > b.min.z + .001f;
    }
}
