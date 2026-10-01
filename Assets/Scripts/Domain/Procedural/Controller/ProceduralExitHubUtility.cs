// ============================================================================
// ProceduralExitHubUtility.cs
// ============================================================================
// PURPOSE:
//   Selects supported player and exit sockets before a template is chosen as the
//   floor's starting hub. A room's bounding-box centre can be a notch or contain
//   furniture; hub identity must not depend on that point being walkable.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Admit ordinary multi-entrance templates with separate clear exit/player sites.
//   - Keep sockets on actual floor and outside authored collision and cake envelopes.
//   - Reserve the canonical single-leaf swing and face its front toward the approach.
// DEPENDENCIES:
//   - Own template definitions and coordinate utility; Unity value types only.
// USAGE NOTES:
//   The two-entrance minimum follows the owner decision for PLAN-026; its §2
//   four-door description records the starting implementation, not a requirement.
//   Conservative rotated-piece bounds may reject a site, never remove its collision.
//   This is geometric preselection, not a substitute for native NavMesh admission.
//   Render-only decals never reserve player or exit clearance.
// ============================================================================
using System;
using System.Linq;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    public static class ProceduralExitHubUtility
    {
        public const int MinimumEntrances = 2;
        public const float PlayerClearance = .3f;
        public static Bounds DoorEnvelope(Vector3 exit, float yaw, float clearance = PlayerClearance)
        {
            var min = new Vector3(-1.48f - clearance, 0f, -1.97f - clearance);
            var max = new Vector3(1.2f + clearance, 3.21f, .2f + clearance);
            int turn = ((int)Math.Round(yaw / 90f) % 4 + 4) % 4;
            var a = ProceduralTemplateUtility.Rotate(min, turn) + exit;
            var b = ProceduralTemplateUtility.Rotate(max, turn) + exit;
            return new Bounds((a + b) * .5f, new Vector3(Math.Abs(b.x - a.x), max.y - min.y, Math.Abs(b.z - a.z)));
        }
        public static float ApproachYaw(Vector3 spawn, Vector3 exit)
        {
            var d = exit - spawn;
            return Math.Abs(d.x) > Math.Abs(d.z) ? (d.x > 0f ? 90f : 270f) : (d.z > 0f ? 0f : 180f);
        }

        public static bool TrySelect(ProceduralTemplateCatalogue catalogue, ProceduralRoomTemplate room,
            float exitClearance, float spawnDistance, float cakeClearance, float doorHeight,
            out Vector3 spawn, out Vector3 exit, Func<Vector3, Vector3, bool> admission = null)
        {
            spawn = exit = default;
            if (room.Kind != "room" || room.Gimmick != "none" ||
                room.Footprint.Length < 10 || room.Doors.Length < MinimumEntrances) return false;
            var kit = catalogue.Kit.ToDictionary(p => p.Id);
            bool Clear(Vector3 point, float radius, float height)
            {
                if (!ProceduralTemplateUtility.Inside(room, point, radius)) return false;
                foreach (var placement in room.Pieces)
                {
                    var piece = kit[placement.Id];
                    if (piece.Kind == "floor" || piece.Kind == "ceiling" || piece.Kind == "decal" || placement.Position.y >= height ||
                        placement.Position.y + piece.Size.y <= 0f) continue;
                    double angle = placement.RotY * Math.PI / 180d;
                    float x = (float)(Math.Abs(Math.Cos(angle)) * piece.Size.x + Math.Abs(Math.Sin(angle)) * piece.Size.z) * .5f;
                    float z = (float)(Math.Abs(Math.Sin(angle)) * piece.Size.x + Math.Abs(Math.Cos(angle)) * piece.Size.z) * .5f;
                    if (Math.Abs(point.x - placement.Position.x) <= x + radius &&
                        Math.Abs(point.z - placement.Position.z) <= z + radius) return false;
                }
                return true;
            }
            float CakeDistance(Vector3 point) => room.Cake.Length == 0 ? float.PositiveInfinity :
                room.Cake.Min(a => (a - point).sqrMagnitude);
            // Match the existing anchor clearance contract, not a new designer tunable.
            var players = room.Footprint.Select(c => new Vector3(c.x * 2f + 1f, 0f, c.y * 2f + 1f))
                .Where(p => Clear(p, .6f, doorHeight) && CakeDistance(p) >= 1f)
                .OrderByDescending(CakeDistance).ToArray();
            var center = new Vector3(room.Footprint.Max(c => c.x) + 1f, 0f, room.Footprint.Max(c => c.y) + 1f);
            var exits = room.Footprint.SelectMany(c => Enumerable.Range(1, 4).SelectMany(x => Enumerable.Range(1, 4)
                    .Select(z => new Vector3(c.x * 2f + x * .5f, 0f, c.y * 2f + z * .5f))))
                .Where(p => Clear(p, exitClearance, 3f) && CakeDistance(p) >= cakeClearance * cakeClearance)
                .OrderBy(p => (p - center).sqrMagnitude);
            foreach (var candidate in exits)
            foreach (var player in players)
                if ((candidate - player).sqrMagnitude >= spawnDistance * spawnDistance &&
                    SwingClear(candidate, ApproachYaw(player, candidate)) && (admission == null || admission(player, candidate)))
                { spawn = player; exit = candidate; return true; }
            return false;

            bool SwingClear(Vector3 point, float yaw)
            {
                var envelope = DoorEnvelope(point, yaw);
                float shellInset = kit.Values.Where(p => (p.Kind == "wall" || p.Kind == "window" || p.Kind == "door") &&
                    p.Id != "wall_round_tangent_r4").Select(p => p.Size.z * .5f).DefaultIfEmpty(0f).Max();
                // Test every overlapped tile, not just the corners of an L-shaped room.
                for (int x = (int)Math.Floor((envelope.min.x - shellInset) / 2f); x < Math.Ceiling((envelope.max.x + shellInset) / 2f); x++)
                for (int z = (int)Math.Floor((envelope.min.z - shellInset) / 2f); z < Math.Ceiling((envelope.max.z + shellInset) / 2f); z++)
                    if (!room.Footprint.Contains(new Vector2Int(x, z))) return false;
                foreach (var p in room.Pieces.Concat(room.Doors.SelectMany(d => d.ClosedWith ?? Array.Empty<ProceduralTemplatePiece>())))
                {
                    var piece = kit[p.Id];
                    if (piece.Kind == "decal" || piece.Kind == "ceiling" ||
                        p.Position.y + piece.Size.y <= 0f) continue;
                    double angle = p.RotY * Math.PI / 180d;
                    var size = new Vector3((float)(Math.Abs(Math.Cos(angle)) * piece.Size.x + Math.Abs(Math.Sin(angle)) * piece.Size.z),
                        piece.Size.y, (float)(Math.Abs(Math.Sin(angle)) * piece.Size.x + Math.Abs(Math.Cos(angle)) * piece.Size.z));
                    if (envelope.Intersects(new Bounds(p.Position + Vector3.up * (piece.Size.y * .5f), size))) return false;
                }
                return true;
            }
        }
    }
}
