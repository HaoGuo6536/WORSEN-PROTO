// ============================================================================
// LevelController.cs
// ============================================================================
// PURPOSE:
//   Turns authored marker snapshots into one validated read-only level graph.
//   It rejects incomplete registration and delegates shared graph algorithms to
//   Core, keeping scene object access out of topology assembly.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Level.
// KEY RESPONSIBILITIES:
//   - Validate marker records and replace or invalidate the owned graph.
//   - Validate supplied runtime-generated graph data without scene marker objects.
// DEPENDENCIES:
//   - Core level contracts; no other Domain system and no upper runtime layer.
// USAGE NOTES:
//   Pure assembly from supplied snapshots; no time or randomness is needed.
// ============================================================================

using System;
using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Domain.Level
{
    public sealed class LevelController
    {
        private readonly LevelBehaviorState _state;

        public LevelController(LevelBehaviorState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void Rebuild(IEnumerable<LevelMarkerRecord> markers)
        {
            Invalidate();
            if (markers == null) throw new ArgumentNullException(nameof(markers));
            var records = new List<LevelMarkerRecord>(markers);
            var ids = new HashSet<int>();
            var roomIds = new HashSet<int>();
            var rooms = new List<LevelRoom>();
            var edges = new List<LevelEdge>();
            var anchors = new List<LevelAnchor>();
            LevelMarkerRecord exit = default;
            var exitCount = 0;
            foreach (var record in records)
            {
                if (record.Id <= 0 || !ids.Add(record.Id))
                    throw new ArgumentException("Level markers need globally unique positive ids.");
                if (!Enum.IsDefined(typeof(LevelMarkerKind), record.Kind))
                    throw new ArgumentException("Unknown level marker kind.");
                if (record.Kind != LevelMarkerKind.Room) continue;
                if (record.Id != record.RoomId)
                    throw new ArgumentException("Room marker id must equal its room id.");
                roomIds.Add(record.RoomId);
                rooms.Add(new LevelRoom(record.RoomId, record.Position, record.Size));
            }
            foreach (var record in records)
            {
                if (!roomIds.Contains(record.RoomId))
                    throw new ArgumentException("Every marker must reference an enabled room.");
                switch (record.Kind)
                {
                    case LevelMarkerKind.RoomLink:
                    case LevelMarkerKind.HunterLink:
                    case LevelMarkerKind.OneWayDrop:
                        edges.Add(new LevelEdge(record.Id, record.RoomId, record.TargetRoomId,
                            record.Kind == LevelMarkerKind.OneWayDrop ? false : record.Bidirectional, record.Access));
                        break;
                    case LevelMarkerKind.CakeAnchor:
                        anchors.Add(new LevelAnchor(record.Id, record.RoomId, record.AnchorType, record.Position));
                        break;
                    case LevelMarkerKind.ExitMarker:
                        exit = record;
                        exitCount++;
                        break;
                }
            }
            if (exitCount != 1) throw new ArgumentException("A level needs exactly one enabled exit marker.");
            _state.Graph = LevelGraphUtility.Build(rooms, edges, anchors, exit.RoomId, exit.Position);
            _state.IsReady = true;
        }

        public void LoadGenerated(LevelGraph graph)
        {
            Invalidate();
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            _state.Graph = LevelGraphUtility.Build(graph.Rooms, graph.Edges, graph.Anchors,
                graph.ExitRoomId, graph.ExitPosition);
            _state.IsReady = true;
        }

        public void Invalidate()
        {
            _state.IsReady = false;
            _state.Graph = null;
        }
    }
}
