// ============================================================================
// ProceduralNavFallbackPresenter.cs
// ============================================================================
// PURPOSE:
//   Rejects obstructed template gameplay sockets before spending a native bake.
//   Uses the actual generated collision commands, including sealed unused doors,
//   rather than assuming that a connected room graph proves physical clearance.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Label required navigation targets so failures identify the offending socket.
//   - Check every template cake, objectives, spawns and walking doors against collision.
// DEPENDENCIES:
//   - Own layout/block definitions and Core graph data; Unity value types only.
// USAGE NOTES:
//   Agent radius and height are supplied by the Driver, never a second tuning source.
//   Swept oriented-box envelopes are conservative. This preflight detects blocked
//   sockets/doorways; native bidirectional paths remain the final reachability gate.
//   Organic multi-storey floors retain their existing ramp-aware native validation.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralNavFallbackPresenter
    {
        public IEnumerable<(string label, Vector3 position)> RequiredPositions(ProceduralLayout layout, bool everyCake = false)
        {
            yield return ("exit room=" + layout.Graph.ExitRoomId, layout.Graph.ExitPosition);
            var cakes = everyCake || layout.CakeLines.Count == 0 ? layout.Graph.Anchors :
                layout.CakeLines.Select(line => line.Anchors[0]).ToArray();
            foreach (var anchor in cakes)
                yield return ("cake anchor=" + anchor.Id + " room=" + anchor.RoomId, anchor.Position);
            for (int i = 0; i < layout.HunterSpawnPositions.Count; i++)
                yield return ("hunter spawn=" + i, layout.HunterSpawnPositions[i]);
            foreach (var route in layout.VerticalRoutes)
            {
                yield return ("vertical start=" + route.Id, route.Points[0]);
                yield return ("vertical end=" + route.Id, route.Points.Last());
            }
        }

        public void ValidateTemplate(ProceduralLayout layout, IReadOnlyList<ProceduralBlock> blocks, float radius, float height)
        {
            if (!layout.UsesTemplates) return;
            if (!(radius > 0f) || float.IsInfinity(radius) || !(height > 0f) || float.IsInfinity(height))
                throw new ArgumentException("Template preflight requires finite positive agent dimensions.");
            var obstacles = blocks.Where(b => b.HasCollision && b.Kind != ProceduralSurfaceKind.Floor).ToArray();
            Check("player spawn", layout.PlayerSpawnPosition, layout.PlayerSpawnPosition);
            foreach (var target in RequiredPositions(layout, everyCake: true)) Check(target.label, target.position, target.position);
            foreach (var door in layout.Doors.Where(d => !d.IsOptional))
            {
                var across = door.AlongX ? Vector3.forward : Vector3.right;
                Check("door " + door.FromRoomId + "->" + door.ToRoomId,
                    door.Center - across, door.Center + across);
            }

            void Check(string label, Vector3 from, Vector3 to)
            {
                // Template sockets are ground-level, even when their marker has a small y offset.
                from.y = to.y = height * .5f;
                foreach (var block in obstacles)
                    if (Blocked(block, from, to, radius, height))
                        throw new InvalidOperationException("Template navigation preflight blocked " + label +
                            " at " + from + " by room=" + block.RoomId + " piece=" + (block.PieceId ?? "primitive") + ".");
            }
        }

        internal static bool Blocked(ProceduralBlock block, Vector3 from, Vector3 to, float radius, float height)
        {
            // Unit block rotations come from geometry commands. Conjugation and the
            // vector operator keep this calculation managed (Quaternion.Inverse is native).
            var q = block.Rotation;
            var inverse = new Quaternion(-q.x, -q.y, -q.z, q.w);
            var a = inverse * (from - block.Center); var b = inverse * (to - block.Center);
            var up = inverse * Vector3.up;
            var half = block.Size * .5f + new Vector3(radius + Math.Abs(up.x) * height * .5f,
                Math.Abs(up.y) * height * .5f, radius + Math.Abs(up.z) * height * .5f);
            float low = 0f, high = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float delta = b[axis] - a[axis];
                if (Math.Abs(delta) < .00001f)
                { if (Math.Abs(a[axis]) >= half[axis]) return false; continue; }
                float enter = (-half[axis] - a[axis]) / delta, leave = (half[axis] - a[axis]) / delta;
                low = Math.Max(low, Math.Min(enter, leave)); high = Math.Min(high, Math.Max(enter, leave));
                if (low >= high) return false;
            }
            return true;
        }
    }
}
