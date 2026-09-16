// ============================================================================
// LevelMarkerDrawer.cs
// ============================================================================
// PURPOSE:
//   Draws stable marker identities and traversal destinations in the Scene view.
//   These authoring gizmos make missing links and mismatched room assignments
//   visible without coupling gameplay to editor-only rendering.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Level authoring gizmos.
// KEY RESPONSIBILITIES:
//   - Color rooms, anchors, links and traversal surfaces and label their stable ids.
// DEPENDENCIES:
//   - Domain LevelMarker, Core records, UnityEditor and UnityEngine gizmo APIs.
// USAGE NOTES:
//   Editor-only visualization. Gizmos do not establish runtime navigation success.
// ============================================================================

using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;

namespace Worsen.Editor.Level
{
    public static class LevelMarkerDrawer
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void Draw(LevelMarker marker, GizmoType gizmoType)
        {
            var record = marker.Capture();
            Gizmos.color = ColorFor(record.Kind);
            if (record.Kind == LevelMarkerKind.Room) Gizmos.DrawWireCube(record.Position, record.Size);
            else Gizmos.DrawWireSphere(record.Position, 0.22f);
            if (record.Kind == LevelMarkerKind.HunterLink || record.Kind == LevelMarkerKind.OneWayDrop ||
                record.Kind == LevelMarkerKind.VaultSurface)
                Gizmos.DrawLine(record.Position, marker.Target);
            if ((gizmoType & GizmoType.Selected) != 0)
                Handles.Label(record.Position + Vector3.up * 0.3f, record.Kind + " #" + record.Id + " / room " + record.RoomId);
        }

        private static Color ColorFor(LevelMarkerKind kind)
        {
            switch (kind)
            {
                case LevelMarkerKind.Room: return Color.gray;
                case LevelMarkerKind.CakeAnchor: return Color.yellow;
                case LevelMarkerKind.HunterLink: return Color.red;
                case LevelMarkerKind.ReboundSurface: return Color.cyan;
                case LevelMarkerKind.ExitMarker: return Color.green;
                case LevelMarkerKind.OneWayDrop: return Color.magenta;
                default: return new Color(1f, 0.5f, 0f);
            }
        }
    }
}

