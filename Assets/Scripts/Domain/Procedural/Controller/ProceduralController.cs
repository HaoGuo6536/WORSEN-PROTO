// ============================================================================
// ProceduralController.cs
// ============================================================================
// PURPOSE:
//   Generates a reproducible connected floor of enclosed grid rooms. A guaranteed
//   four-room loop supplies an alternate chase route, seeded growth changes the
//   surrounding topology, and every room receives two generous straight cake lines.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Grow bounded room layouts, match door openings and validate reachability.
//   - Produce stable room, edge and anchor identities and a comparable manifest.
// DEPENDENCIES:
//   - Core immutable level contracts and LevelGraphUtility; no Domain siblings.
// USAGE NOTES:
//   Pure C# with injected System.Random. Construct a fresh random source with
//   LayoutSeed for each run/round pair. Generation never retries indefinitely or
//   reports an authored scene as a successful procedural fallback.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralController
    {
        private readonly ProceduralBehaviorState _state;
        private readonly ProceduralConfig _config;
        private readonly System.Random _random;

        public ProceduralController(ProceduralBehaviorState state, ProceduralConfig config, System.Random random)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public static int LayoutSeed(int runSeed, int roundIndex) => unchecked((runSeed * 397) ^ (roundIndex * 7919));

        public ProceduralLayout Generate(int runSeed, int roundIndex)
        {
            Reset();
            ValidateConfig(roundIndex);
            var cells = GrowCells(RoomCount(roundIndex));
            var rooms = cells.Select((cell, index) => new LevelRoom(index + 1,
                new Vector3(_config.Origin.x + cell.x * _config.RoomSize, _config.RoomHeight * 0.5f, _config.Origin.y + cell.y * _config.RoomSize),
                new Vector3(_config.RoomSize, _config.RoomHeight, _config.RoomSize))).ToArray();
            var doors = CreateDoors(cells);
            var edges = doors.Select((door, index) => new LevelEdge(1001 + index,
                door.FromRoomId, door.ToRoomId, true, TraversalAccess.All)).ToArray();
            var modules = rooms.Select(room => new ProceduralRoomModule(room.Id,
                (ProceduralModuleKind)((room.Id - 1) % 3), _random.Next(2) == 0)).ToArray();
            var anchors = CreateAnchors(rooms, modules);
            var preliminary = LevelGraphUtility.Build(rooms, edges, anchors, rooms[0].Id, Ground(rooms[0].Center));
            var distances = LevelGraphUtility.TopologicalDistancesFrom(preliminary, rooms[0].Id, TraversalAccess.Player);
            var ordered = rooms.OrderByDescending(room => distances[room.Id]).ThenBy(room => room.Id).ToArray();
            var exit = ordered[0];
            var graph = LevelGraphUtility.Build(rooms, edges, anchors, exit.Id, Approach(exit, modules[exit.Id - 1], 1f));
            ValidateGraph(graph);
            var layout = new ProceduralLayout
            {
                Seed = runSeed,
                RoundIndex = roundIndex,
                Graph = graph,
                Cells = Array.AsReadOnly(cells.ToArray()),
                Doors = Array.AsReadOnly(doors.ToArray()),
                Modules = Array.AsReadOnly(modules),
                PlayerSpawnPosition = Approach(rooms[0], modules[0], -1f),
                PlayerSpawnRotation = Quaternion.LookRotation(modules[0].AlongX ? Vector3.forward : Vector3.right, Vector3.up),
                HunterSpawnPositions = Array.AsReadOnly(ordered.Where(room => room.Id != rooms[0].Id)
                    .Select(room => Approach(room, modules[room.Id - 1], 1f)).ToArray())
            };
            layout.Manifest = Manifest(layout);
            _state.Layout = layout;
            return layout;
        }

        public void Admit() => _state.IsReady = _state.Layout != null;
        public void Reset() { _state.IsReady = false; _state.Layout = null; }

        private int RoomCount(int roundIndex) => (int)Math.Min(_config.MaximumRoomCount,
            _config.InitialRoomCount + (long)(roundIndex - 1) * _config.RoomsPerRound);

        private List<Vector2Int> GrowCells(int count)
        {
            int orientation = _random.Next(4);
            var cells = new List<Vector2Int>
            {
                Vector2Int.zero, Rotate(new Vector2Int(1, 0), orientation),
                Rotate(new Vector2Int(1, 1), orientation), Rotate(new Vector2Int(0, 1), orientation)
            };
            var occupied = new HashSet<Vector2Int>(cells);
            while (cells.Count < count)
            {
                var frontier = new HashSet<Vector2Int>();
                foreach (var cell in cells)
                foreach (var direction in CardinalDirections())
                {
                    var candidate = cell + direction;
                    if (!occupied.Contains(candidate)) frontier.Add(candidate);
                }
                var candidates = frontier.OrderBy(cell => cell.x).ThenBy(cell => cell.y).ToArray();
                if (candidates.Length == 0) throw new InvalidOperationException("Procedural growth exhausted its frontier.");
                var next = candidates[_random.Next(candidates.Length)];
                cells.Add(next);
                occupied.Add(next);
            }
            return cells;
        }

        private List<ProceduralDoorPlan> CreateDoors(IReadOnlyList<Vector2Int> cells)
        {
            var doors = new List<ProceduralDoorPlan>();
            for (int from = 0; from < cells.Count; from++)
            for (int to = from + 1; to < cells.Count; to++)
            {
                var delta = cells[to] - cells[from];
                if (Math.Abs(delta.x) + Math.Abs(delta.y) != 1) continue;
                float offset = (_random.Next(2) == 0 ? -1f : 1f) * _config.DoorOffset;
                bool alongX = delta.y != 0;
                var center = new Vector3(_config.Origin.x + (cells[from].x + cells[to].x) * _config.RoomSize * 0.5f,
                    0f, _config.Origin.y + (cells[from].y + cells[to].y) * _config.RoomSize * 0.5f);
                center += alongX ? Vector3.right * offset : Vector3.forward * offset;
                doors.Add(new ProceduralDoorPlan(from + 1, to + 1, center, alongX));
            }
            return doors;
        }

        private List<LevelAnchor> CreateAnchors(IReadOnlyList<LevelRoom> rooms, IReadOnlyList<ProceduralRoomModule> modules)
        {
            var anchors = new List<LevelAnchor>();
            foreach (var room in rooms)
            {
                bool alongX = modules[room.Id - 1].AlongX;
                for (int line = 0; line < 2; line++)
                for (int index = 0; index < _config.CakesPerLine; index++)
                {
                    float along = (index - (_config.CakesPerLine - 1) * 0.5f) * _config.CakeSpacing;
                    float across = (line == 0 ? -1f : 1f) * _config.CakeLineOffset;
                    var point = room.Center + (alongX ? new Vector3(along, 0f, across) : new Vector3(across, 0f, along));
                    point.y = _config.AnchorHeight;
                    anchors.Add(new LevelAnchor(10001 + anchors.Count, room.Id, CakeAnchorType.Flow, point));
                }
            }
            return anchors;
        }

        private void ValidateConfig(int roundIndex)
        {
            if (roundIndex < 1) throw new ArgumentOutOfRangeException(nameof(roundIndex));
            if (!Finite(_config.Origin.x) || !Finite(_config.Origin.y)) throw new ArgumentException("Layout origin must be finite.");
            if (_config.InitialRoomCount < 4 || _config.MaximumRoomCount < _config.InitialRoomCount ||
                _config.MaximumRoomCount > 256 || _config.RoomsPerRound < 0)
                throw new ArgumentException("Procedural room budgets must satisfy 4 <= initial <= maximum <= 256 and nonnegative growth.");
            RequirePositive(_config.RoomSize, "room size"); RequirePositive(_config.RoomHeight, "room height");
            RequirePositive(_config.DoorWidth, "door width"); RequirePositive(_config.DoorHeight, "door height");
            RequirePositive(_config.CakeSpacing, "cake spacing"); RequirePositive(_config.CakeLineOffset, "cake line offset");
            RequirePositive(_config.SpawnSideOffset, "spawn side offset");
            if (_config.SpawnSideOffset >= _config.RoomSize * 0.5f)
                throw new ArgumentException("Spawns must remain inside the room perimeter.");
            if (!Finite(_config.DoorOffset) || _config.DoorOffset < 0f ||
                _config.DoorOffset + _config.DoorWidth * 0.5f >= _config.RoomSize * 0.5f ||
                _config.DoorHeight >= _config.RoomHeight)
                throw new ArgumentException("Door openings must fit entirely inside room walls and below ceilings.");
            if (_config.CakesPerLine < 2 || _config.CakesPerLine > 64 ||
                (_config.CakesPerLine - 1) * _config.CakeSpacing >= _config.RoomSize ||
                _config.CakeLineOffset * 2f >= _config.RoomSize)
                throw new ArgumentException("Cake lines must fit inside rooms and contain between 2 and 64 anchors.");
            if (!Finite(_config.AnchorHeight) || _config.AnchorHeight < 0f || _config.AnchorHeight >= _config.RoomHeight ||
                !Finite(_config.SpawnHeight) || _config.SpawnHeight < 0f || _config.SpawnHeight >= _config.DoorHeight)
                throw new ArgumentException("Anchor and spawn heights must be inside playable rooms.");
        }

        private static void ValidateGraph(LevelGraph graph)
        {
            foreach (TraversalAccess actor in new[] { TraversalAccess.Player, TraversalAccess.Hunter })
            {
                var fromSpawn = LevelGraphUtility.TopologicalDistancesFrom(graph, 1, actor);
                var toExit = LevelGraphUtility.DistancesTo(graph, graph.ExitRoomId, actor);
                if (fromSpawn.Values.Any(distance => distance < 0) || toExit.Values.Any(distance => distance < 0))
                    throw new InvalidOperationException("Generated rooms must connect spawn, every cake and the exit for both actors.");
            }
            foreach (var anchor in graph.Anchors)
                if (!graph.Rooms.First(room => room.Id == anchor.RoomId).Bounds.Contains(anchor.Position))
                    throw new InvalidOperationException("A generated cake lies outside its room.");
        }

        private static string Manifest(ProceduralLayout layout)
        {
            var text = new StringBuilder("closed-rooms-v1|");
            text.Append(layout.Seed).Append('|').Append(layout.RoundIndex).Append('|').Append(layout.Graph.ExitRoomId);
            foreach (var room in layout.Graph.Rooms)
            { text.Append("|R:").Append(room.Id); Append(text, room.Center); Append(text, room.Size); }
            foreach (var door in layout.Doors)
            { text.Append("|D:").Append(door.FromRoomId).Append(',').Append(door.ToRoomId); Append(text, door.Center); }
            foreach (var module in layout.Modules)
                text.Append("|M:").Append(module.RoomId).Append(',').Append((int)module.Kind).Append(',').Append(module.AlongX ? 1 : 0);
            foreach (var anchor in layout.Graph.Anchors)
            { text.Append("|C:").Append(anchor.Id).Append(',').Append(anchor.RoomId); Append(text, anchor.Position); }
            return text.ToString();
        }

        private static void Append(StringBuilder text, Vector3 value)
        {
            text.Append(',').Append(value.x.ToString("R", CultureInfo.InvariantCulture));
            text.Append(',').Append(value.y.ToString("R", CultureInfo.InvariantCulture));
            text.Append(',').Append(value.z.ToString("R", CultureInfo.InvariantCulture));
        }
        private Vector3 Ground(Vector3 position) => new Vector3(position.x, _config.SpawnHeight, position.z);
        private Vector3 Approach(LevelRoom room, ProceduralRoomModule module, float sign)
            => Ground(room.Center) + (module.AlongX ? Vector3.forward : Vector3.right) * (_config.SpawnSideOffset * sign);
        private static Vector2Int Rotate(Vector2Int cell, int turns)
        {
            for (int index = 0; index < turns; index++) cell = new Vector2Int(-cell.y, cell.x);
            return cell;
        }
        private static IEnumerable<Vector2Int> CardinalDirections()
        { yield return Vector2Int.right; yield return Vector2Int.up; yield return Vector2Int.left; yield return Vector2Int.down; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void RequirePositive(float value, string name)
        { if (!Finite(value) || value <= 0f) throw new ArgumentException("Procedural " + name + " must be finite and positive."); }
    }
}
