// ============================================================================
// ProceduralTemplateController.cs
// ============================================================================
// PURPOSE:
//   Builds a floor exclusively from authored theme rooms joined at matching sockets.
//   A bounded seeded placement search preserves template identity and never stretches
//   a footprint. Failure returns a reason to the owning generator's organic fallback.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Sample weighted, round-gated templates within the shared gimmick budget.
//   - Join rotated sockets on the two-metre grid without overlapping footprints.
//   - Publish connected graph, authored anchors and first-contact spawn capacity.
//   - Reserve isolated template pockets with explicit Passage gap sites.
//   - Record placement and fallback provenance deterministically.
// DEPENDENCIES:
//   - Own definitions, configuration and utilities; Core immutable graph contracts.
// USAGE NOTES:
//   System.Random is injected. Hall-size rooms never repeat, including in pockets.
//   The exit is an ordinary medium-or-larger template, protected by graph identity.
//   Candidate placement attempts, not wall-clock time, consume the search budget.
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
    public sealed class ProceduralTemplateController
    {
        private readonly ProceduralConfig _config;
        private readonly System.Random _random;
        public ProceduralTemplateController(ProceduralConfig config, System.Random random)
        { _config = config ?? throw new ArgumentNullException(nameof(config)); _random = random ?? throw new ArgumentNullException(nameof(random)); }

        public bool TryGenerate(int seed, int round, ProceduralThemeData theme, bool refuge, int requiredHunters,
            out ProceduralLayout layout, out string reason)
        {
            layout = null;
            var catalogue = _config.RoomCatalogue?.Catalogues.FirstOrDefault(c => c != null && c.Theme == (theme?.Id ?? "castle"));
            if (catalogue == null) { reason = "missing-catalogue:" + (theme?.Id ?? "castle"); return false; }
            try
            {
                ProceduralTemplateValidationUtility.Validate(catalogue);
                if (_config.TemplatePlacementBudget < 1 || _config.TemplatePlacementBudget > 1000000)
                    throw new ArgumentException("Invalid template placement budget.");
                if (!ProceduralTemplateUtility.Finite(_config.TemplateExitClearance) || _config.TemplateExitClearance < 1.6f ||
                    !ProceduralTemplateUtility.Finite(_config.TemplateExitSpawnDistance) || _config.TemplateExitSpawnDistance < 3.2f ||
                    !ProceduralTemplateUtility.Finite(_config.TemplateExitCakeClearance) || _config.TemplateExitCakeClearance < 1f)
                    throw new ArgumentException("Invalid template exit clearances.");
                int count = (int)Math.Min(_config.MaximumRoomCount, _config.InitialRoomCount + (long)(round - 1) * _config.RoomsPerRound);
                int budget = refuge || _config.Challenges == null ? 0 : ProceduralGimmickUtility.Budget(_config.Challenges, round);
                var eligible = catalogue.Templates.Where(t => t.MinRound <= round && (t.Gimmick == "none" || budget > 0))
                    .OrderBy(t => t.Id, StringComparer.Ordinal).ToArray();
                var starts = eligible.Where(t => t.Kind == "room" && t.Gimmick == "none" && t.Footprint.Length >= 10 && t.SizeClass != "hall").ToArray();
                if (starts.Length == 0) throw new InvalidOperationException("No ordinary exit template is available at this round.");
                var placed = new List<ProceduralTemplateRoom> { new ProceduralTemplateRoom
                    { RoomId = 1, Template = Pick(starts), Turns = _random.Next(4) } };
                var doors = new List<ProceduralDoorPlan>(); var occupied = new HashSet<Vector2Int>(Cells(placed[0]));
                var gaps = new HashSet<Vector2Int>(); var sites = new List<ProceduralGapSite>();
                int attempts = 0;
                while (placed.Count < count)
                {
                    if (++attempts > _config.TemplatePlacementBudget) throw new InvalidOperationException("Template placement budget exhausted.");
                    var choices = eligible.Where(t => (t.Gimmick == "none" || placed.Count(p => p.Template.Gimmick != "none") < budget) &&
                        (t.SizeClass != "hall" || placed.All(p => p.Template.Id != t.Id))).ToArray();
                    // Alternate room/hall preference; both remain actual catalogue templates.
                    var preferred = choices.Where(t => placed.Count % 2 == 1 ? t.Kind != "room" : t.Kind == "room").ToArray();
                    if (preferred.Length != 0) choices = preferred;
                    if (choices.Length == 0) throw new InvalidOperationException("No eligible template remains.");
                    Attach(Pick(choices), 0, 0, placed, occupied, gaps, doors, sites);
                }
                if (round >= _config.GapStartRound && _random.NextDouble() < _config.GapProbability && _random.NextDouble() < _config.PocketProbability)
                {
                    int startCount = placed.Count;
                    while (placed.Count < startCount + _config.PocketRoomCount)
                    {
                        if (++attempts > _config.TemplatePlacementBudget) throw new InvalidOperationException("Template pocket placement budget exhausted.");
                        var choices = eligible.Where(t => t.Kind == "room" && t.Gimmick == "none" && t.SizeClass != "hall").ToArray();
                        if (choices.Length == 0) throw new InvalidOperationException("No pocket template available.");
                        // Each pocket has its own corridor and identity, so no hidden required edge is invented.
                        Attach(Pick(choices), placed.Count - startCount + 1, _random.Next(1, _config.MaximumGapCells + 1) * 2,
                            placed, occupied, gaps, doors, sites);
                    }
                }
                var rooms = new List<LevelRoom>(); var anchors = new List<LevelAnchor>(); var optional = new List<LevelAnchor>();
                var modules = new List<ProceduralRoomModule>(); var hunters = new List<Vector3>();
                foreach (var room in placed)
                {
                    var cells = Cells(room).OrderBy(c => c.x).ThenBy(c => c.y).ToArray();
                    var volumes = ProceduralTemplateUtility.Volumes(cells, _config.Origin, room.Template.Height);
                    var bounds = volumes[0]; foreach (var volume in volumes.Skip(1)) bounds.Encapsulate(volume);
                    rooms.Add(new LevelRoom(room.RoomId, bounds.center, bounds.size, cells: volumes, pocket: room.PocketId != 0));
                    modules.Add(new ProceduralRoomModule(room.RoomId, refuge ? ProceduralModuleKind.MerchantRefuge : room.RoomId == 1 ?
                        ProceduralModuleKind.ExitHub : ProceduralModuleKind.TorchGallery, true, cells, room.PocketId, false));
                    int index = 0;
                    foreach (var point in room.Template.Cake)
                    {
                        var p = ProceduralTemplateUtility.Point(room, point, _config.Origin) + Vector3.up * _config.AnchorHeight;
                        var anchor = new LevelAnchor(10000 + room.RoomId * 10000 + index, room.RoomId,
                            index++ % 2 == 0 ? CakeAnchorType.Flow : CakeAnchorType.Detour, p);
                        (room.PocketId == 0 ? anchors : optional).Add(anchor);
                    }
                    if (room.PocketId != 0)
                        foreach (var point in room.Template.GoldenCake)
                            optional.Add(new LevelAnchor(10000 + room.RoomId * 10000 + index++, room.RoomId, CakeAnchorType.Risk,
                                ProceduralTemplateUtility.Point(room, point, _config.Origin) + Vector3.up * _config.AnchorHeight));
                    else hunters.AddRange(room.Template.HunterSpawn.Select(p => ProceduralTemplateUtility.Point(room, p, _config.Origin) + Vector3.up * _config.SpawnHeight));
                }
                var first = placed[0];
                var spawnCandidates = first.Template.Footprint.Select(c => new Vector3(c.x * 2f + 1f, 0f, c.y * 2f + 1f))
                    .Where(p => ProceduralTemplateUtility.Inside(first.Template, p, .6f))
                    .Select(p => ProceduralTemplateUtility.Point(first, p, _config.Origin) + Vector3.up * _config.SpawnHeight)
                    .OrderByDescending(p => anchors.Where(a => a.RoomId == 1).Min(a => (a.Position - p).sqrMagnitude)).ToArray();
                if (spawnCandidates.Length == 0 || anchors.Any(a => (a.Position - spawnCandidates[0]).sqrMagnitude < 1f))
                    throw new InvalidOperationException("Exit template has no cake-clear player socket.");
                var spawn = spawnCandidates[0];
                var exitCandidates = first.Template.Footprint.SelectMany(c => Enumerable.Range(1, 2).SelectMany(x => Enumerable.Range(1, 2)
                    .Select(z => new Vector3(c.x * 2f + x, 0f, c.y * 2f + z))))
                    .Where(p => ProceduralTemplateUtility.Inside(first.Template, p, _config.TemplateExitClearance))
                    .Select(p => ProceduralTemplateUtility.Point(first, p, _config.Origin))
                    .Where(p => (p - spawn).sqrMagnitude >= _config.TemplateExitSpawnDistance * _config.TemplateExitSpawnDistance &&
                        anchors.All(a => (a.Position - p).sqrMagnitude >= _config.TemplateExitCakeClearance * _config.TemplateExitCakeClearance))
                    .OrderBy(p => (p - rooms[0].Center).sqrMagnitude).ToArray();
                if (exitCandidates.Length == 0) throw new InvalidOperationException("Exit template lacks a clear exit-door envelope separate from the player.");
                var exitPosition = exitCandidates[0];
                var graph = LevelGraphUtility.Build(rooms, doors.Select((d, i) => new LevelEdge(1001 + i, d.FromRoomId, d.ToRoomId, true)).ToArray(), anchors, 1, exitPosition);
                var candidate = new ProceduralLayout
                {
                    Seed = seed, RoundIndex = round, Theme = theme, GimmickBudget = budget, TemplateCatalogue = catalogue,
                    TemplateRooms = placed.AsReadOnly(), Graph = graph, CellSize = 2f, Origin = _config.Origin,
                    Cells = occupied.OrderBy(c => c.x).ThenBy(c => c.y).ToArray(), GapCells = gaps.OrderBy(c => c.x).ThenBy(c => c.y).ToArray(),
                    GapSites = sites.AsReadOnly(), PocketAnchors = optional.AsReadOnly(), Doors = doors.AsReadOnly(), Modules = modules.AsReadOnly(),
                    PlayerSpawnPosition = spawn, PlayerSpawnRotation = Quaternion.LookRotation(new Vector3(exitPosition.x - spawn.x, 0f, exitPosition.z - spawn.z))
                };
                candidate.HunterSpawnPositions = ProceduralSpawnUtility.Select(candidate, _config, hunters.Distinct().ToArray(), out int minimum, out string report);
                candidate.MinimumHunterSpawnRooms = minimum; candidate.SpawnValidationReport = report;
                if (candidate.ValidatedHunterSpawnCapacity < requiredHunters) throw new InvalidOperationException("Template hunter capacity below requested " + requiredHunters);
                ProceduralFootprintUtility.Validate(candidate);
                layout = candidate; reason = string.Empty; return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            { reason = error.GetType().Name + ":" + error.Message; return false; }
        }

        private bool Attach(ProceduralRoomTemplate template, int pocket, int gap, List<ProceduralTemplateRoom> placed,
            HashSet<Vector2Int> occupied, HashSet<Vector2Int> gaps, List<ProceduralDoorPlan> doors, List<ProceduralGapSite> sites)
        {
            var sockets = placed.Where(r => r.PocketId == 0).SelectMany(r => Enumerable.Range(0, r.Template.Doors.Length)
                .Where(i => !r.OpenDoors.Contains(i)).Select(i => (room: r, index: i))).ToArray();
            if (sockets.Length == 0) return false;
            var from = sockets[_random.Next(sockets.Length)]; int to = _random.Next(template.Doors.Length), turns = _random.Next(4);
            var fromPoint = ProceduralTemplateUtility.Point(from.room, ProceduralTemplateUtility.Door(from.room.Template.Doors[from.index]), _config.Origin);
            if (sites.Any(s => (s.Edge - fromPoint).sqrMagnitude < .01f)) return false;
            var normal = ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Direction(from.room.Template.Doors[from.index].Side), from.room.Turns);
            if (ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Direction(template.Doors[to].Side), turns) != -normal) return false;
            var world = ProceduralTemplateUtility.Point(from.room, ProceduralTemplateUtility.Door(from.room.Template.Doors[from.index]), Vector2.zero);
            var target = ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Door(template.Doors[to]), turns);
            var delta = world - target;
            if (delta.x % 2f != 0f || delta.z % 2f != 0f) return false;
            var next = new ProceduralTemplateRoom { RoomId = placed.Count + 1, PocketId = pocket, Template = template, Turns = turns,
                Offset = new Vector2Int((int)delta.x / 2, (int)delta.z / 2) + normal * gap };
            var cells = Cells(next).ToArray();
            if (cells.Any(c => occupied.Contains(c) || gaps.Contains(c))) return false;
            var reserved = new List<Vector2Int>();
            if (gap > 0)
            {
                // A four-metre corridor reserves both sides of the socket centerline.
                var tangent = new Vector2Int(normal.y, -normal.x);
                for (int i = 0; i < gap; i++) for (int side = -1; side <= 1; side++)
                {
                    var p = world + new Vector3(normal.x, 0f, normal.y) * (i * 2f + 1f) + new Vector3(tangent.x, 0f, tangent.y) * side;
                    var c = new Vector2Int(Mathf.FloorToInt(p.x / 2f), Mathf.FloorToInt(p.z / 2f));
                    if (occupied.Contains(c) || cells.Contains(c) || gaps.Contains(c)) return false;
                    reserved.Add(c);
                }
            }
            placed.Add(next); foreach (var c in cells) occupied.Add(c); foreach (var c in reserved) gaps.Add(c);
            from.room.OpenDoors = from.room.OpenDoors.Concat(new[] { from.index }).ToArray();
            if (gap > 0)
            {
                // Reserved sockets remain sealed until Passage is activated, unlike ordinary graph portals.
                from.room.OpenDoors = from.room.OpenDoors.Where(i => i != from.index).ToArray();
                var edge = world + new Vector3(_config.Origin.x, 0f, _config.Origin.y);
                sites.Add(new ProceduralGapSite(from.room.RoomId, pocket, edge, edge + new Vector3(normal.x, 0f, normal.y) * (gap * 2f)));
            }
            else
            {
                next.OpenDoors = new[] { to };
                doors.Add(new ProceduralDoorPlan(from.room.RoomId, next.RoomId,
                    world + new Vector3(_config.Origin.x, 0f, _config.Origin.y), normal.x == 0));
                // Coincident free sockets add walking loops without creating unauthored apertures.
                foreach (var other in placed.Where(r => r.RoomId != next.RoomId && r.PocketId == 0))
                for (int a = 0; a < other.Template.Doors.Length; a++)
                for (int b = 0; b < next.Template.Doors.Length; b++)
                {
                    if (other.OpenDoors.Contains(a) || next.OpenDoors.Contains(b)) continue;
                    var point = ProceduralTemplateUtility.Point(other, ProceduralTemplateUtility.Door(other.Template.Doors[a]), _config.Origin);
                    if (point != ProceduralTemplateUtility.Point(next, ProceduralTemplateUtility.Door(next.Template.Doors[b]), _config.Origin) ||
                        ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Direction(other.Template.Doors[a].Side), other.Turns) !=
                        -ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Direction(next.Template.Doors[b].Side), next.Turns)) continue;
                    other.OpenDoors = other.OpenDoors.Concat(new[] { a }).ToArray(); next.OpenDoors = next.OpenDoors.Concat(new[] { b }).ToArray();
                    doors.Add(new ProceduralDoorPlan(other.RoomId, next.RoomId, point,
                        ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Direction(other.Template.Doors[a].Side), other.Turns).x == 0));
                }
            }
            return true;
        }
        private ProceduralRoomTemplate Pick(IReadOnlyList<ProceduralRoomTemplate> values)
        {
            double roll = _random.NextDouble() * values.Sum(v => (double)v.Weight);
            foreach (var value in values) { roll -= value.Weight; if (roll < 0d) return value; }
            return values[values.Count - 1];
        }
        private static IEnumerable<Vector2Int> Cells(ProceduralTemplateRoom room)
            => room.Template.Footprint.Select(c => ProceduralTemplateUtility.Cell(c, room.Turns) + room.Offset);
        public static string Manifest(ProceduralLayout layout)
        {
            var text = new StringBuilder("|templates-v1|organic-fallback=").Append(Uri.EscapeDataString(layout.TemplateFallbackReason));
            foreach (var room in layout.TemplateRooms)
                text.Append("|Template:").Append(room.RoomId).Append(',').Append(room.Template.Id).Append(',').Append(room.Offset.x.ToString(CultureInfo.InvariantCulture))
                    .Append(',').Append(room.Offset.y.ToString(CultureInfo.InvariantCulture)).Append(',').Append(room.Turns).Append(",open=")
                    .Append(string.Join(";", room.OpenDoors.OrderBy(i => i)));
            return text.Append("|HunterCapacity:").Append(layout.ValidatedHunterSpawnCapacity).ToString();
        }
    }
}
