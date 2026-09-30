// ============================================================================
// ProceduralController.cs
// ============================================================================
// PURPOSE:
//   Generates a reproducible castle floor around a four-door central exit hub.
//   Seeded growth retains an ordinary walking loop, adds optional shared-wall
//   windows and slides, and offers typed cake destinations on existing ground and
//   stair-accessible decks rather than filling rooms with straight pickup lines.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Grow bounded room layouts, match door openings and validate reachability.
//   - Produce stable room, edge and anchor identities and a comparable manifest.
//   - Sample diverse geometry-backed candidates; leave required cake selection to Floor.
//   - Keep player spawns outside cake pickup radii while preserving ordinary routes.
//   - Publish exact footprint cells and pocket flags, retaining cells at higher ceilings.
//   - Start each castle floor in its exit hub with a clear approach to the center door.
//   - Enforce reusable first-contact validation and record deterministic distance relaxation.
//   - Reserve gaps before growth; separate optional pocket anchors from required candidates.
//   - Keep the initial hub/loop single-cell; weight subsequent rooms without size fallback.
//   - Add extension-cell storeys, retain their directed manifest and reject stranded objectives.
//   - Select independent theme content and stage validated threshold-freeze candidates.
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

        public ProceduralLayout Generate(int runSeed, int roundIndex, bool merchantRefuge = false, float optionalWindowMultiplier = 1f, int? themeSeed = null)
        {
            Reset();
            ValidateConfig(roundIndex);
            if (!Finite(optionalWindowMultiplier) || optionalWindowMultiplier < 0f || optionalWindowMultiplier > 1f)
                throw new ArgumentOutOfRangeException(nameof(optionalWindowMultiplier));
            int connectedCount = RoomCount(roundIndex);
            var footprints = GrowCells(connectedCount, roundIndex, out var gaps);
            float height = _config.CastleModules ? _config.CastleHeight : _config.RoomHeight;
            var rooms = footprints.Select((cells, index) => new LevelRoom(index + 1,
                new Vector3(_config.Origin.x + (cells.Min(c => c.x) + cells.Max(c => c.x)) * _config.RoomSize * 0.5f,
                    height * 0.5f, _config.Origin.y + (cells.Min(c => c.y) + cells.Max(c => c.y)) * _config.RoomSize * 0.5f),
                new Vector3((cells.Max(c => c.x) - cells.Min(c => c.x) + 1) * _config.RoomSize, height,
                    (cells.Max(c => c.y) - cells.Min(c => c.y) + 1) * _config.RoomSize),
                cells: cells.Select(cell => new Bounds(
                    new Vector3(_config.Origin.x + cell.x * _config.RoomSize, height * 0.5f,
                        _config.Origin.y + cell.y * _config.RoomSize),
                    new Vector3(_config.RoomSize, height, _config.RoomSize))).ToArray(),
                pocket: index >= connectedCount)).ToArray();
            var doors = CreateDoors(footprints, connectedCount, optionalWindowMultiplier);
            var edges = doors.Select((door, index) => new LevelEdge(1001 + index,
                door.FromRoomId, door.ToRoomId, true, door.IsOptional ? TraversalAccess.Player : TraversalAccess.All)).ToArray();
            int familyOffset = _random.Next(5);
            var modules = rooms.Select(room => new ProceduralRoomModule(room.Id,
                !_config.CastleModules ? (ProceduralModuleKind)((room.Id - 1) % 3) :
                merchantRefuge ? ProceduralModuleKind.MerchantRefuge : room.Id == 1 ? ProceduralModuleKind.ExitHub :
                (ProceduralModuleKind)((int)ProceduralModuleKind.TorchGallery + (room.Id - 2 + familyOffset) % 5),
                _random.Next(2) == 0, Array.AsReadOnly(footprints[room.Id - 1].ToArray()),
                room.Id > connectedCount ? 1 : 0)).ToArray();
            if (_config.CastleModules)
                foreach (var module in modules)
                {
                    if (module.Kind != ProceduralModuleKind.BrokenCloister && module.Kind != ProceduralModuleKind.BrokenGallery) continue;
                    var room = rooms[module.RoomId - 1];
                    rooms[module.RoomId - 1] = new LevelRoom(room.Id,
                        new Vector3(room.Center.x, _config.HighCeilingHeight * 0.5f, room.Center.z),
                        new Vector3(room.Size.x, _config.HighCeilingHeight, room.Size.z),
                        cells: room.Cells.Select(cell => new Bounds(
                            new Vector3(cell.center.x, _config.HighCeilingHeight * 0.5f, cell.center.z),
                            new Vector3(cell.size.x, _config.HighCeilingHeight, cell.size.z))).ToArray(),
                        pocket: room.Pocket);
                }
            var allAnchors = CreateAnchors(rooms, modules, doors);
            var anchors = allAnchors.Where(a => a.RoomId <= connectedCount).ToArray();
            var preliminary = LevelGraphUtility.Build(rooms, edges, anchors, rooms[0].Id, Ground(rooms[0].Center));
            var distances = LevelGraphUtility.TopologicalDistancesFrom(preliminary, rooms[0].Id, TraversalAccess.Player);
            var ordered = rooms.Take(connectedCount).OrderByDescending(room => distances[room.Id]).ThenBy(room => room.Id).ToArray();
            var exit = _config.CastleModules ? rooms[0] : ordered[0];
            var graph = LevelGraphUtility.Build(rooms, edges, anchors, exit.Id,
                _config.CastleModules ? Ground(exit.Center) : Approach(exit, modules[exit.Id - 1], 1f));
            ValidateGraph(LevelGraphUtility.Build(rooms.Take(connectedCount).ToArray(),
                edges.Where(e => e.FromRoomId <= connectedCount && e.ToRoomId <= connectedCount).ToArray(),
                anchors, exit.Id, graph.ExitPosition));
            int spawnIndex = _config.CastleModules ? exit.Id - 1 : 0;
            var spawnRoom = rooms[spawnIndex];
            var layout = new ProceduralLayout
            {
                Seed = runSeed,
                RoundIndex = roundIndex,
                Theme = ProceduralThemeUtility.Select(_config.Themes, roundIndex, new System.Random(themeSeed ?? runSeed)),
                Graph = graph,
                Cells = Array.AsReadOnly(footprints.SelectMany(c => c).ToArray()),
                CellSize = _config.RoomSize,
                Origin = _config.Origin,
                GapCells = Array.AsReadOnly(gaps.ToArray()),
                PocketAnchors = Array.AsReadOnly(allAnchors.Where(a => a.RoomId > connectedCount).ToArray()),
                Doors = Array.AsReadOnly(doors.ToArray()),
                Modules = Array.AsReadOnly(modules),
                PlayerSpawnPosition = PlayerApproach(spawnRoom, modules[spawnIndex]),
                PlayerSpawnRotation = Quaternion.LookRotation(modules[spawnIndex].AlongX ? Vector3.forward : Vector3.right, Vector3.up),
                HunterSpawnPositions = Array.Empty<Vector3>()
            };
            ProceduralStoreyUtility.Apply(layout, _config, _random);
            layout.GapSites = GapSites(layout);
            layout.Manifest = Manifest(layout);
            _state.Layout = layout; // Retain failed candidates for the existing retry journal.
            layout.HunterSpawnPositions = ProceduralSpawnUtility.Select(layout, _config,
                ordered.Select(room => Approach(room, modules[room.Id - 1], 1f)).ToArray(),
                out int minimumRooms, out string spawnReport);
            layout.MinimumHunterSpawnRooms = minimumRooms;
            layout.SpawnValidationReport = spawnReport;
            ProceduralFreezeUtility.Apply(layout, _config);
            ProceduralFootprintUtility.Validate(layout);
            ProceduralStoreyUtility.Validate(layout, _config);
            layout.PresentationRooms = DescribeRooms(layout, spawnRoom.Id);
            layout.Manifest = Manifest(layout);
            _state.Layout = layout;
            return layout;
        }

        public void Admit() => _state.IsReady = _state.Layout != null;
        public void Reset() { _state.IsReady = false; _state.Layout = null; }

        private int RoomCount(int roundIndex) => (int)Math.Min(_config.MaximumRoomCount,
            _config.InitialRoomCount + (long)(roundIndex - 1) * _config.RoomsPerRound);

        private List<List<Vector2Int>> GrowCells(int count, int round, out List<Vector2Int> gaps)
        {
            int orientation = _random.Next(4);
            var cells = new List<Vector2Int>
            {
                Vector2Int.zero, Rotate(new Vector2Int(1, 0), orientation),
                Rotate(new Vector2Int(1, 1), orientation), Rotate(new Vector2Int(0, 1), orientation)
            };
            if (_config.CastleModules)
                cells = new List<Vector2Int> { Vector2Int.zero, Rotate(Vector2Int.right, orientation),
                    Rotate(Vector2Int.up, orientation), Rotate(Vector2Int.left, orientation),
                    Rotate(Vector2Int.down, orientation), Rotate(new Vector2Int(1, 1), orientation) };
            var footprints = cells.Select(c => new List<Vector2Int> { c }).ToList();
            gaps = new List<Vector2Int>();
            var pockets = new List<Vector2Int>();
            if (round >= _config.GapStartRound && _random.NextDouble() < _config.GapProbability)
            {
                // Missing diagonal touches two core rooms in castle mode. Its outward
                // continuation separates a collapsed wing, never a required route.
                int length = _random.Next(1, _config.MaximumGapCells + 1);
                int gapX = _config.CastleModules ? -1 : 0;
                for (int i = 1; i <= length; i++) gaps.Add(Rotate(new Vector2Int(gapX, -i), orientation));
                if (_random.NextDouble() < _config.PocketProbability)
                    for (int i = 1; i <= _config.PocketRoomCount; i++)
                        pockets.Add(Rotate(new Vector2Int(gapX, -length - i), orientation));
            }
            var reserved = new HashSet<Vector2Int>(gaps.Concat(pockets));
            foreach (var pocket in pockets)
            foreach (var direction in CardinalDirections()) reserved.Add(pocket + direction);
            var occupied = new HashSet<Vector2Int>(cells);
            while (footprints.Count < count)
            {
                var frontier = new HashSet<Vector2Int>();
                foreach (var cell in occupied)
                foreach (var direction in CardinalDirections())
                {
                    var candidate = cell + direction;
                    if (!occupied.Contains(candidate) && !reserved.Contains(candidate)) frontier.Add(candidate);
                }
                double roll = round < _config.MultiCellStartRound ? -1d : _random.NextDouble() *
                    (_config.OneCellWeight + _config.TwoCellWeight + _config.ThreeCellWeight);
                int size = roll < _config.OneCellWeight ? 1 : roll < _config.OneCellWeight + _config.TwoCellWeight ? 2 : 3;
                bool bent = size == 3 && _random.NextDouble() < _config.LShapeWeight;
                var shape = Enumerable.Range(0, size).Select(i => bent && i == 2 ? Vector2Int.up : new Vector2Int(i, 0)).ToArray();
                var candidates = new List<List<Vector2Int>>();
                foreach (var origin in frontier.OrderBy(c => c.x).ThenBy(c => c.y))
                for (int turns = 0; turns < 4; turns++)
                {
                    var next = shape.Select(c => origin + Rotate(c, turns)).ToList();
                    var envelope = new List<Vector2Int>();
                    for (int x = next.Min(c => c.x); x <= next.Max(c => c.x); x++)
                    for (int y = next.Min(c => c.y); y <= next.Max(c => c.y); y++) envelope.Add(new Vector2Int(x, y));
                    if (envelope.Any(c => occupied.Contains(c) || reserved.Contains(c))) continue;
                    candidates.Add(next);
                }
                if (candidates.Count == 0) throw new InvalidOperationException("Procedural footprint growth exhausted its frontier.");
                var chosen = candidates[_random.Next(candidates.Count)];
                footprints.Add(chosen);
                foreach (var cell in chosen) occupied.Add(cell);
                // Do not put another room inside an L notch while Core uses bounding boxes.
                for (int x = chosen.Min(c => c.x); x <= chosen.Max(c => c.x); x++)
                for (int y = chosen.Min(c => c.y); y <= chosen.Max(c => c.y); y++)
                    if (!occupied.Contains(new Vector2Int(x, y))) reserved.Add(new Vector2Int(x, y));
            }
            footprints.AddRange(pockets.Select(c => new List<Vector2Int> { c }));
            return footprints;
        }

        private List<ProceduralDoorPlan> CreateDoors(IReadOnlyList<List<Vector2Int>> cells, int connectedCount, float optionalWindowMultiplier)
        {
            var doors = new List<ProceduralDoorPlan>();
            int optionalIndex = 0;
            for (int from = 0; from < cells.Count; from++)
            for (int to = from + 1; to < cells.Count; to++)
            foreach (var a in cells[from])
            foreach (var b in cells[to])
            {
                if ((from < connectedCount) != (to < connectedCount)) continue;
                var delta = b - a;
                if (Math.Abs(delta.x) + Math.Abs(delta.y) != 1) continue;
                float offset = (_random.Next(2) == 0 ? -1f : 1f) * _config.DoorOffset;
                bool alongX = delta.y != 0;
                var center = new Vector3(_config.Origin.x + (a.x + b.x) * _config.RoomSize * 0.5f,
                    0f, _config.Origin.y + (a.y + b.y) * _config.RoomSize * 0.5f);
                center += alongX ? Vector3.right * offset : Vector3.forward * offset;
                doors.Add(new ProceduralDoorPlan(from + 1, to + 1, center, alongX));
                if (_config.CastleModules && from != 0)
                {
                    // Consume fixed random draws even when a curse suppresses a window.
                    bool sampledWindow = _random.Next(2) == 0;
                    bool window = optionalIndex == 0 || (optionalIndex > 1 && sampledWindow);
                    optionalIndex++;
                    float roll = (float)_random.NextDouble();
                    if (!window || roll < optionalWindowMultiplier)
                    {
                        var secondary = center - (alongX ? Vector3.right : Vector3.forward) * (offset * 2f);
                        doors.Add(new ProceduralDoorPlan(from + 1, to + 1, secondary, alongX,
                            window ? TraversalSurfaceKind.Vault : TraversalSurfaceKind.SlideGate));
                    }
                }
            }
            return doors;
        }

        private List<LevelAnchor> CreateAnchors(IReadOnlyList<LevelRoom> rooms,
            IReadOnlyList<ProceduralRoomModule> modules, IReadOnlyList<ProceduralDoorPlan> doors)
        {
            var anchors = new List<LevelAnchor>();
            foreach (var room in rooms)
            {
                var module = modules[room.Id - 1];
                var pool = module.Cells.SelectMany((cell, index) => CandidateSites(CellRoom(room, cell), module, doors, index))
                    .Where(anchor => Preference(anchor.Type) > 0f).ToList();
                int count = _random.Next(_config.MinimumCandidatesPerRoom, _config.MaximumCandidatesPerRoom + 1);
                if (pool.Count < count)
                    throw new InvalidOperationException("Cake preferences leave too few supported candidates in room " + room.Id + ".");
                var usedTypes = new HashSet<CakeAnchorType>();
                var usedCells = new HashSet<int>();
                for (int index = 0; index < count; index++)
                {
                    // Prefer unused types before repeats; weights are per type, not per socket.
                    int Cell(LevelAnchor a) => (a.Id - 10000 - room.Id * 100) / 1000000;
                    var available = pool.Where(a => !usedCells.Contains(Cell(a))).ToArray();
                    if (available.Length == 0) available = pool.ToArray();
                    var types = available.Select(anchor => anchor.Type).Distinct().OrderBy(type => type).ToArray();
                    if (types.All(usedTypes.Contains)) usedTypes.Clear();
                    types = types.Where(type => !usedTypes.Contains(type)).ToArray();
                    double roll = _random.NextDouble() * types.Sum(type => (double)Preference(type));
                    var selected = types[types.Length - 1];
                    foreach (var type in types)
                    {
                        roll -= Preference(type);
                        if (roll < 0d) { selected = type; break; }
                    }
                    var sites = available.Where(anchor => anchor.Type == selected).ToArray();
                    var candidate = sites[_random.Next(sites.Length)];
                    anchors.Add(candidate);
                    pool.Remove(candidate);
                    usedTypes.Add(selected);
                    usedCells.Add(Cell(candidate));
                }
            }
            return anchors;
        }

        private List<LevelAnchor> CandidateSites(LevelRoom room, ProceduralRoomModule module,
            IReadOnlyList<ProceduralDoorPlan> doors, int cellIndex)
        {
            var sites = new List<LevelAnchor>();
            var portals = doors.Where(door => (door.FromRoomId == room.Id || door.ToRoomId == room.Id) &&
                room.Bounds.Contains(door.Center)).ToArray();
            int baseId = 10000 + room.Id * 100 + cellIndex * 1000000;
            bool raised = cellIndex == 0 && (module.Kind == ProceduralModuleKind.OpenStairHall ||
                module.Kind == ProceduralModuleKind.SplitLevelLibrary || module.Kind == ProceduralModuleKind.BrokenGallery);
            var origin = new Vector3(room.Center.x, _config.AnchorHeight, room.Center.z);
            float side = _config.RoomSize * 0.5f - _config.CandidatePerimeterInset;
            var corners = new[] { new Vector3(-side, 0f, -side), new Vector3(-side, 0f, side),
                new Vector3(side, 0f, -side), new Vector3(side, 0f, side) };
            // Corner pockets are off the door axes and outside partitions, piers and stairs.
            // In flat rooms the most remote pocket is Risk, measured against every exit.
            int farthest = Enumerable.Range(0, corners.Length).OrderByDescending(index =>
                portals.Length == 0 ? 0f : portals.Min(door => (origin + corners[index] - door.Center).sqrMagnitude)).First();
            for (int index = 0; index < corners.Length; index++)
                sites.Add(new LevelAnchor(baseId + index, room.Id,
                    !raised && index == farthest ? CakeAnchorType.Risk : CakeAnchorType.Detour, origin + corners[index]));
            if (portals.Length == 0)
                sites.Add(new LevelAnchor(baseId + 11, room.Id, CakeAnchorType.Flow,
                    origin - (module.AlongX ? Vector3.forward : Vector3.right) * side));
            foreach (var door in portals.Where(door => !door.IsOptional))
            {
                var delta = door.Center - origin;
                var point = door.Center - (door.AlongX ? Vector3.forward * Math.Sign(delta.z) :
                    Vector3.right * Math.Sign(delta.x)) * _config.CandidatePerimeterInset;
                point.y = _config.AnchorHeight;
                // Cardinal socket ids do not shift when other rooms select fewer candidates.
                int socket = door.AlongX ? (delta.z > 0f ? 4 : 5) : (delta.x > 0f ? 6 : 7);
                sites.Add(new LevelAnchor(baseId + socket, room.Id, CakeAnchorType.Flow, point));
            }
            if (raised)
            {
                // Authored module sockets, matching CastlePresenter's deck (x +/-4.3,
                // z 1.2..4), stair head (-3,1.2), and library's 1.7m-wide side deck.
                AddUpper(8, CakeAnchorType.Precision, module.Kind == ProceduralModuleKind.SplitLevelLibrary ?
                    new Vector3(3.45f, 0f, -1.5f) : new Vector3(0f, 0f, 1.65f));
                AddUpper(9, CakeAnchorType.Vertical, new Vector3(-3f, 0f, 2.6f));
                AddUpper(10, CakeAnchorType.Risk, new Vector3(3.6f, 0f, 3.45f));
            }
            return sites;

            void AddUpper(int socket, CakeAnchorType type, Vector3 local)
            {
                local.y = _config.UpperDeckHeight;
                var point = origin + (module.AlongX ? local : new Vector3(local.z, local.y, local.x));
                sites.Add(new LevelAnchor(baseId + socket, room.Id, type, point));
            }
        }

        private float Preference(CakeAnchorType type)
        {
            switch (type)
            {
                case CakeAnchorType.Precision: return _config.PrecisionPreference;
                case CakeAnchorType.Detour: return _config.DetourPreference;
                case CakeAnchorType.Risk: return _config.RiskPreference;
                case CakeAnchorType.Vertical: return _config.VerticalPreference;
                default: return _config.FlowPreference;
            }
        }

        private void ValidateConfig(int roundIndex)
        {
            if (roundIndex < 1) throw new ArgumentOutOfRangeException(nameof(roundIndex));
            foreach (float weight in new[] { _config.OneCellWeight, _config.TwoCellWeight, _config.ThreeCellWeight })
                if (!Finite(weight) || weight < 0f) throw new ArgumentException("Invalid footprint weight.");
            RequirePositive(_config.OneCellWeight + _config.TwoCellWeight + _config.ThreeCellWeight, "footprint weight sum");
            foreach (float chance in new[] { _config.LShapeWeight, _config.GapProbability, _config.PocketProbability })
                if (!Finite(chance) || chance < 0f || chance > 1f) throw new ArgumentException("Invalid footprint/gap probability.");
            if (_config.MultiCellStartRound < 1 || _config.GapStartRound < 1 || _config.MaximumGapCells < 1 ||
                _config.MaximumGapCells > 8 || _config.PocketRoomCount < 1 || _config.PocketRoomCount > 3)
                throw new ArgumentException("Invalid footprint/gap round or budget.");
            if (!Finite(_config.Origin.x) || !Finite(_config.Origin.y)) throw new ArgumentException("Layout origin must be finite.");
            if (_config.CastleModules && (_config.InitialRoomCount < 6 || _config.RoomSize < 12f ||
                !Finite(_config.CastleHeight) || !Finite(_config.HighCeilingHeight) ||
                _config.HighCeilingHeight <= _config.CastleHeight ||
                _config.UpperDeckHeight < 2.2f || _config.UpperDeckHeight > 2.8f ||
                _config.CastleHeight < _config.UpperDeckHeight + 2.8f || _config.DoorOffset < 2f || _config.DoorOffset > 2.5f))
                throw new ArgumentException("Castle generation needs six rooms, 12m cells, finite higher broad-room ceilings, stair headroom and separated portals.");
            if (_config.InitialRoomCount < 4 || _config.MaximumRoomCount < _config.InitialRoomCount ||
                _config.MaximumRoomCount > 256 || _config.RoomsPerRound < 0)
                throw new ArgumentException("Procedural room budgets must satisfy 4 <= initial <= maximum <= 256 and nonnegative growth.");
            RequirePositive(_config.RoomSize, "room size"); RequirePositive(_config.RoomHeight, "room height");
            RequirePositive(_config.DoorWidth, "door width"); RequirePositive(_config.DoorHeight, "door height");
            RequirePositive(_config.CakeSpacing, "cake spacing"); RequirePositive(_config.CakeLineOffset, "cake line offset");
            RequirePositive(_config.SpawnSideOffset, "spawn side offset");
            if (_config.MinimumHunterSpawnRooms < 1 || _config.MinimumHunterSpawnRooms > 256 ||
                _config.GenerationRetries < 0 || _config.GenerationRetries > 8 ||
                !Finite(_config.OrdinaryDoorFraction) || _config.OrdinaryDoorFraction < 0f || _config.OrdinaryDoorFraction > 1f ||
                _config.KnockablePropsPerRoom < 0 || _config.KnockablePropsPerRoom > 4)
                throw new ArgumentException("Invalid spawn, retry or interactable budget.");
            RequirePositive(_config.CandidatePerimeterInset, "candidate perimeter inset");
            if (_config.MinimumCandidatesPerRoom < 1 || _config.MaximumCandidatesPerRoom > 5 ||
                _config.MaximumCandidatesPerRoom < _config.MinimumCandidatesPerRoom ||
                _config.CandidatePerimeterInset >= _config.RoomSize * 0.5f)
                throw new ArgumentException("Candidate budgets must satisfy 1 <= minimum <= maximum <= 5 with an inset inside the room.");
            foreach (CakeAnchorType type in Enum.GetValues(typeof(CakeAnchorType)))
                if (!Finite(Preference(type)) || Preference(type) < 0f)
                    throw new ArgumentException("Candidate type preferences must be finite and nonnegative.");
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

        private IReadOnlyList<GeneratedRoomSample> DescribeRooms(ProceduralLayout layout, int spawnRoomId)
        {
            var samples = new List<GeneratedRoomSample>(layout.Graph.Rooms.Count);
            foreach (var room in layout.Graph.Rooms)
            {
                var kind = layout.Modules[room.Id - 1].Kind;
                bool refuge = kind == ProceduralModuleKind.MerchantRefuge;
                // Optional means removable AFTER collection, never dispensable objective content.
                bool optional = _config.CastleModules && !refuge && room.Id != spawnRoomId &&
                    room.Id != layout.Graph.ExitRoomId && PreservesEscapeWithoutRoom(layout.Graph, room.Id);
                samples.Add(new GeneratedRoomSample(room.Id, room.Bounds, false, refuge,
                    layout.Doors.Where(door => door.FromRoomId == room.Id || door.ToRoomId == room.Id)
                        .Select(door => door.Center).ToArray(), optionalRoom: optional || room.Pocket,
                    cells: room.Cells.ToArray()));
            }
            return samples.AsReadOnly();
        }

        private static bool PreservesEscapeWithoutRoom(LevelGraph graph, int omittedRoomId)
        {
            var reached = new HashSet<int> { graph.ExitRoomId };
            var frontier = new Queue<int>(); frontier.Enqueue(graph.ExitRoomId);
            while (frontier.Count > 0)
            {
                int current = frontier.Dequeue();
                foreach (var edge in graph.Edges)
                {
                    if (edge.Access != TraversalAccess.All || edge.FromRoomId == omittedRoomId || edge.ToRoomId == omittedRoomId) continue;
                    int next = edge.FromRoomId == current ? edge.ToRoomId :
                        edge.Bidirectional && edge.ToRoomId == current ? edge.FromRoomId : 0;
                    if (next != 0 && reached.Add(next)) frontier.Enqueue(next);
                }
            }
            return reached.Count == LevelGraphUtility.DistancesTo(graph, graph.ExitRoomId, TraversalAccess.Player)
                .Count(pair => pair.Value >= 0) - 1;
        }

        private string Manifest(ProceduralLayout layout)
        {
            var text = new StringBuilder("castle-rooms-v6|");
            text.Append(layout.Seed).Append('|').Append(layout.RoundIndex).Append('|').Append(layout.Graph.ExitRoomId);
            text.Append("|FootprintPolicy:").Append(_config.MultiCellStartRound).Append(',').Append(_config.GapStartRound)
                .Append(',').Append(_config.MaximumGapCells).Append(',').Append(_config.PocketRoomCount);
            foreach (float value in new[] { _config.OneCellWeight, _config.TwoCellWeight, _config.ThreeCellWeight,
                _config.LShapeWeight, _config.GapProbability, _config.PocketProbability })
                text.Append(',').Append(value.ToString("R", CultureInfo.InvariantCulture));
            foreach (var room in layout.Graph.Rooms)
            { text.Append("|R:").Append(room.Id); Append(text, room.Center); Append(text, room.Size); }
            foreach (var door in layout.Doors)
            { text.Append("|D:").Append(door.FromRoomId).Append(',').Append(door.ToRoomId); Append(text, door.Center); text.Append(',').Append((int)door.TraversalKind).Append(',').Append(door.AlongX ? 1 : 0); }
            foreach (var edge in layout.Graph.Edges)
                text.Append("|E:").Append(edge.Id).Append(',').Append(edge.FromRoomId).Append(',').Append(edge.ToRoomId)
                    .Append(',').Append(edge.Bidirectional ? 1 : 0).Append(',').Append((int)edge.Access);
            foreach (var module in layout.Modules)
            {
                text.Append("|M:").Append(module.RoomId).Append(',').Append((int)module.Kind).Append(',').Append(module.AlongX ? 1 : 0)
                    .Append(",pocket=").Append(module.PocketId);
                foreach (var cell in module.Cells) text.Append(";cell=").Append(cell.x).Append(',').Append(cell.y);
            }
            foreach (var cell in layout.GapCells) text.Append("|Gap:").Append(cell.x).Append(',').Append(cell.y);
            foreach (var site in layout.GapSites)
            { text.Append("|Passage:").Append(site.RoomId).Append(',').Append(site.PocketId); Append(text, site.Edge); Append(text, site.Landing); }
            foreach (var anchor in layout.PocketAnchors)
            { text.Append("|Optional:").Append(anchor.Id).Append(',').Append(anchor.RoomId).Append(',').Append((int)anchor.Type); Append(text, anchor.Position); }
            foreach (var anchor in layout.Graph.Anchors)
            { text.Append("|C:").Append(anchor.Id).Append(',').Append(anchor.RoomId).Append(',').Append((int)anchor.Type); Append(text, anchor.Position); }
            text.Append("|SpawnPolicy:").Append(layout.SpawnValidationReport);
            text.Append("|Player:"); Append(text, layout.PlayerSpawnPosition);
            Append(text, layout.PlayerSpawnRotation * Vector3.forward);
            text.Append("|Exit:"); Append(text, layout.Graph.ExitPosition);
            foreach (var spawn in layout.HunterSpawnPositions) { text.Append("|H:"); Append(text, spawn); }
            text.Append(ProceduralStoreyUtility.Manifest(layout, _config));
            text.Append(ProceduralThemeUtility.Manifest(layout.Theme));
            text.Append(ProceduralFreezeUtility.Manifest(layout));
            return text.ToString();
        }

        private static void Append(StringBuilder text, Vector3 value)
        {
            text.Append(',').Append(value.x.ToString("R", CultureInfo.InvariantCulture));
            text.Append(',').Append(value.y.ToString("R", CultureInfo.InvariantCulture));
            text.Append(',').Append(value.z.ToString("R", CultureInfo.InvariantCulture));
        }
        private Vector3 Ground(Vector3 position) => new Vector3(position.x, _config.SpawnHeight, position.z);
        private Vector3 PlayerApproach(LevelRoom room, ProceduralRoomModule module)
        {
            bool elevated = module.Kind == ProceduralModuleKind.OpenStairHall ||
                module.Kind == ProceduralModuleKind.SplitLevelLibrary || module.Kind == ProceduralModuleKind.BrokenGallery;
            if (!_config.CastleModules || !elevated) return Approach(room, module, -1f);
            // Raised families place their ground cake lane at -4.5m. The old -4m
            // spawn overlapped a .55m pickup trigger plus the .3m player capsule.
            // This center aisle is clear of both stair flights and is 1.4m from cake.
            return Ground(room.Center) - (module.AlongX ? Vector3.forward : Vector3.right) * 3.1f;
        }
        private Vector3 Approach(LevelRoom room, ProceduralRoomModule module, float sign)
            => Ground(CellRoom(room, module.Cells[0]).Center) + (module.AlongX ? Vector3.forward : Vector3.right) * (_config.SpawnSideOffset * sign);
        private LevelRoom CellRoom(LevelRoom room, Vector2Int cell) => new LevelRoom(room.Id,
            new Vector3(_config.Origin.x + cell.x * _config.RoomSize, room.Center.y, _config.Origin.y + cell.y * _config.RoomSize),
            new Vector3(_config.RoomSize, room.Size.y, _config.RoomSize), pocket: room.Pocket);

        private IReadOnlyList<ProceduralGapSite> GapSites(ProceduralLayout layout)
        {
            var sites = new List<ProceduralGapSite>();
            foreach (var module in layout.Modules.Where(m => m.PocketId == 0))
            foreach (var cell in module.Cells)
            foreach (var direction in CardinalDirections())
            {
                var cursor = cell + direction;
                if (!layout.GapCells.Contains(cursor)) continue;
                while (layout.GapCells.Contains(cursor)) cursor += direction;
                var pocket = layout.Modules.FirstOrDefault(m => m.PocketId != 0 && m.Cells.Contains(cursor));
                if (pocket.RoomId == 0) continue;
                var edge = CellRoom(layout.Graph.Rooms[module.RoomId - 1], cell).Center;
                edge.y = 0f;
                edge += new Vector3(direction.x, 0f, direction.y) * (_config.RoomSize * 0.5f);
                sites.Add(new ProceduralGapSite(module.RoomId, pocket.PocketId, edge,
                    Ground(CellRoom(layout.Graph.Rooms[pocket.RoomId - 1], cursor).Center)));
            }
            return sites.AsReadOnly();
        }
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
