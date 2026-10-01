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
//   - Join rotated sockets using metre subcells without rescaling authored tiles.
//   - Publish connected graph, authored anchors and first-contact spawn capacity.
//   - Reserve isolated template pockets with explicit Passage gap sites.
//   - Record placement and fallback provenance deterministically.
// DEPENDENCIES:
//   - Own definitions, configuration and utilities; Core immutable graph contracts.
// USAGE NOTES:
//   System.Random is injected. Hall-size rooms never repeat, including in pockets.
//   The exit is a clear ordinary template with at least two connected entrances,
//   protected by graph identity; its bounding-box centre need not be floor.
//   Candidate placement attempts, not wall-clock time, consume the search budget.
//   A multi-front walking spine admits graph-Voronoi regions before choosing
//   biome-specific templates. Compatible same-biome sockets add walking loops.
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
            out ProceduralLayout layout, out string reason, int? themeSeed = null, int shrineRoomCount = 0)
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
                if (shrineRoomCount < 0 || shrineRoomCount > count - 3) throw new ArgumentException("Invalid requested shrine-room count.");
                int arms = Math.Max(2, refuge ? 0 : shrineRoomCount);
                if (arms > 4) throw new ArgumentException("Authored room topology supports at most four shrine arms.");
                int Parent(int id) => id <= 3 ? 1 : arms == 4 && id <= 7 ? (id <= 5 ? 2 : 3) : id <= arms + 1 ? 2 : id - arms;
                // Shrine destinations terminate separate arms: an authored pocket
                // must never occupy the continuation of a required walking spine.
                var shrineIds = new HashSet<int>(Enumerable.Range(count - (refuge ? 0 : shrineRoomCount) + 1, refuge ? 0 : shrineRoomCount));
                var pool = ProceduralBiomeUtility.Pool(_config.Themes, round, themeSeed ?? seed);
                // A multi-arm floor uses an ordinary junction beside the exit.
                // Contract that mandatory hub pair into one Voronoi seed location.
                bool Junction(int id) => arms > 2 && id == 2 || arms == 4 && id == 3;
                var spine = LevelGraphUtility.Build(Enumerable.Range(1, count).Where(i => !shrineIds.Contains(i) && !Junction(i)).Select(i => new LevelRoom(i, new Vector3(i * 2f, 1f, 0f), Vector3.one)).ToArray(),
                    Enumerable.Range(2, count - 1).Where(i => !shrineIds.Contains(i) && !Junction(i)).Select(i => new LevelEdge(i, Junction(Parent(i)) ? 1 : Parent(i), i, true)).ToArray(), Array.Empty<LevelAnchor>(), 1, Vector3.zero);
                var biomeRandom = new System.Random(ProceduralController.LayoutSeed(seed, round));
                var biomes = pool.Length == 0 ? Enumerable.Range(1, count).ToDictionary(i => i, i => theme) :
                    ProceduralBiomeUtility.Partition(spine, pool, biomeRandom).ToDictionary(p => p.Key, p => p.Value);
                bool CanBranch(ProceduralThemeData t) => _config.RoomCatalogue.Catalogues.Any(c => c.Theme == (t?.Id ?? "castle") &&
                    c.Templates.Any(r => (r.Kind == "room" || r.Kind == "junction") && r.Gimmick == "none" && r.MinRound <= round && r.Doors.Length >= 3 && r.SizeClass != "hall"));
                // The art contains three-way junctions, not four-way hubs. Reject
                // only Voronoi draws whose mandatory hub biome cannot branch.
                for (int draw = 0; arms > 2 && !CanBranch(biomes[1]) && pool.Length > 0 && draw < 64; draw++)
                    biomes = ProceduralBiomeUtility.Partition(spine, pool, biomeRandom).ToDictionary(p => p.Key, p => p.Value);
                if (arms > 2 && !CanBranch(biomes[1])) throw new InvalidOperationException("No branching biome supports the requested shrine arms.");
                foreach (int id in Enumerable.Range(2, count - 1).Where(Junction)) biomes[id] = biomes[1];
                foreach (int id in shrineIds.OrderBy(i => i)) biomes[id] = biomes[Parent(id)];
                bool planned = biomes.Values.Select(t => t?.Id ?? "castle").Distinct().Count() > 1 || shrineRoomCount > 0;
                string ThemeId(int id) => biomes[id]?.Id ?? "castle";
                bool Transition(int id) => id > 1 && ThemeId(id) != ThemeId(Parent(id));
                catalogue = _config.RoomCatalogue.Catalogues.FirstOrDefault(c => c.Theme == ThemeId(1));
                if (catalogue == null) throw new InvalidOperationException("Missing biome catalogue " + ThemeId(1));
                foreach (string id in biomes.Values.Select(t => t?.Id ?? "castle").Distinct())
                    ProceduralTemplateValidationUtility.Validate(_config.RoomCatalogue.Catalogues.Single(c => c.Theme == id));
                int budget = refuge || ReferenceEquals(_config.Challenges, null) ? 0 : ProceduralGimmickUtility.Budget(_config.Challenges, round);
                var eligible = _config.RoomCatalogue.Catalogues.Where(c => biomes.Values.Any(t => (t?.Id ?? "castle") == c.Theme))
                    .SelectMany(c => c.Templates).Where(t => t.MinRound <= round && (t.Gimmick == "none" || budget > 0))
                    .OrderBy(t => t.Id, StringComparer.Ordinal).ToArray();
                var starts = eligible.Where(t => catalogue.Templates.Contains(t) && ProceduralExitHubUtility.TrySelect(catalogue, t,
                    _config.TemplateExitClearance, _config.TemplateExitSpawnDistance, _config.TemplateExitCakeClearance,
                    _config.DoorHeight, out _, out _)).ToArray();
                if (starts.Length == 0) throw new InvalidOperationException("No clear multi-entrance exit template is available at this round.");
                var placed = new List<ProceduralTemplateRoom> { new ProceduralTemplateRoom
                    { RoomId = 1, Template = Pick(starts), Catalogue = catalogue, Theme = biomes[1], Turns = _random.Next(4),
                        MaximumEntrances = round >= _config.MultiFloorStartRound && _config.StoreyProbability > 0f ? arms : int.MaxValue } };
                var doors = new List<ProceduralDoorPlan>(); var occupied = new HashSet<Vector2Int>(Cells(placed[0]));
                var gaps = new HashSet<Vector2Int>(); var sites = new List<ProceduralGapSite>();
                int attempts = 0, lastProgress = 0;
                while (placed.Count < count)
                {
                    if (++attempts > _config.TemplatePlacementBudget) throw new InvalidOperationException("Template placement budget exhausted at room " + placed.Count +
                        "; placed=" + string.Join(";", placed.Select(r => r.RoomId + ":" + r.Template.Id + ":" + string.Join(",", r.OpenDoors))) + ".");
                    // A blocked branch is not a failed floor: undo its last leaf
                    // and try another authored placement within the same budget.
                    if (attempts - lastProgress >= (planned ? 32 : 128) && placed.Count > (planned ? 1 : 3))
                    {
                        // Both fronts must be rewound: removing only the newest
                        // opposite-front leaf leaves the blocked parent unchanged.
                        int keep = Math.Max(1, placed.Count - (planned ? arms : 1));
                        var removedIds = new HashSet<int>(placed.Skip(keep).Select(r => r.RoomId));
                        placed.RemoveRange(keep, placed.Count - keep);
                        occupied.Clear(); foreach (var r in placed) occupied.UnionWith(Cells(r));
                        gaps.Clear();
                        foreach (var r in placed.Where(r => r.PassagePocket != null))
                            foreach (var c in PassageReservation(r)) gaps.Add(c);
                        doors.RemoveAll(d => removedIds.Contains(d.FromRoomId) || removedIds.Contains(d.ToRoomId));
                        foreach (var r in placed)
                            r.OpenDoors = Enumerable.Range(0, r.Template.Doors.Length).Where(i => doors.Any(d =>
                                (d.FromRoomId == r.RoomId || d.ToRoomId == r.RoomId) &&
                                (d.Center - ProceduralTemplateUtility.Point(r, ProceduralTemplateUtility.Door(r.Template.Doors[i]), _config.Origin)).sqrMagnitude < .001f)).ToArray();
                        lastProgress = attempts;
                    }
                    int nextId = placed.Count + 1;
                    int requiredEntrances = 1 + Enumerable.Range(nextId + 1, count - nextId).Count(id => Parent(id) == nextId);
                    var choices = eligible.Where(t => (!planned || t.Id.StartsWith(ThemeId(nextId) + "_", StringComparison.Ordinal)) &&
                        (!planned || t.Doors.Length >= requiredEntrances) &&
                        (!Junction(nextId) || (t.Kind == "room" || t.Kind == "junction") && t.Gimmick == "none" && t.Doors.Length >= 3 && t.SizeClass != "hall") &&
                        (t.Kind == "shrine") == shrineIds.Contains(nextId) &&
                        (!Transition(nextId) || t.Transition != null && t.Transition.CompatibleThemes.Contains(ThemeId(Parent(nextId)))) &&
                        (t.Gimmick == "none" || placed.Count(p => p.Template.Gimmick != "none") < budget) &&
                        (t.Kind != "puzzle" || !placed.Any(p => p.Template.Kind == "puzzle")) &&
                        (t.SizeClass != "hall" || placed.All(p => p.Template.Id != t.Id))).ToArray();
                    // Every fourth attempt permits the complete eligible set when furnished envelopes do not fit.
                    var preferred = choices.Where(t => t.FurnishingVersion > 0).ToArray();
                    if (preferred.Length == 0) preferred = choices.Where(t => placed.Count % 2 == 1 ?
                        t.Kind == "hallway" || t.Kind == "junction" : t.Kind == "room").ToArray();
                    if (budget > 0 && !refuge && !shrineIds.Contains(nextId) && !Transition(nextId) &&
                        !placed.Any(p => p.Template.Kind == "puzzle") && nextId >= count / 2 && choices.Any(t => t.Kind == "puzzle"))
                        preferred = choices.Where(t => t.Kind == "puzzle").ToArray();
                    if (preferred.Length != 0 && attempts % 4 != 0) choices = preferred;
                    if (choices.Length == 0) throw new InvalidOperationException("No eligible template remains.");
                    if (Attach(Pick(choices), 0, 0, placed, occupied, gaps, doors, sites,
                        planned ? Parent(nextId) : 0, biomes[nextId])) lastProgress = attempts;
                }
                foreach (var source in placed.Where(r => r.PassagePocket != null).ToArray())
                {
                    var pocket = source.PassagePocket; pocket.RoomId = placed.Count + 1; pocket.PocketId = pocket.RoomId;
                    placed.Add(pocket); occupied.UnionWith(Cells(pocket)); gaps.ExceptWith(Cells(pocket));
                    sites.Add(new ProceduralGapSite(source.RoomId, pocket.PocketId,
                        ProceduralTemplateUtility.Point(source, source.Template.PassageGap.Edge, _config.Origin),
                        ProceduralTemplateUtility.Point(source, source.Template.PassageGap.Landing, _config.Origin)));
                }
                if (sites.Count == 0 && round >= _config.GapStartRound && _random.NextDouble() < _config.GapProbability && _random.NextDouble() < _config.PocketProbability)
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
                    var volumes = ProceduralTemplateUtility.Volumes(cells, _config.Origin,
                        room.RoomId == 1 ? Math.Max(room.Template.Height, 3.21f) : room.Template.Height, 1f);
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
                if (doors.Count(d => d.FromRoomId == first.RoomId || d.ToRoomId == first.RoomId) < ProceduralExitHubUtility.MinimumEntrances)
                    throw new InvalidOperationException("Exit template lacks two connected walking entrances.");
                if (!ProceduralExitHubUtility.TrySelect(catalogue, first.Template, _config.TemplateExitClearance,
                    _config.TemplateExitSpawnDistance, _config.TemplateExitCakeClearance, _config.DoorHeight, out var localSpawn, out var localExit))
                    throw new InvalidOperationException("Exit template lacks clear player and exit sockets.");
                var spawn = ProceduralTemplateUtility.Point(first, localSpawn, _config.Origin) + Vector3.up * _config.SpawnHeight;
                var exitPosition = ProceduralTemplateUtility.Point(first, localExit, _config.Origin);
                double facing = Math.Atan2(exitPosition.x - spawn.x, exitPosition.z - spawn.z) * .5d;
                var graph = LevelGraphUtility.Build(rooms, doors.Select((d, i) => new LevelEdge(1001 + i, d.FromRoomId, d.ToRoomId, true)).ToArray(), anchors, 1, exitPosition);
                var candidate = new ProceduralLayout
                {
                    Seed = seed, RoundIndex = round, Theme = biomes[1], GimmickBudget = budget, TemplateCatalogue = catalogue,
                    RoomThemes = placed.ToDictionary(r => r.RoomId, r => r.Theme ?? theme),
                    TemplateRooms = placed.AsReadOnly(), Graph = graph, CellSize = 1f, Origin = _config.Origin,
                    Cells = occupied.OrderBy(c => c.x).ThenBy(c => c.y).ToArray(), GapCells = gaps.OrderBy(c => c.x).ThenBy(c => c.y).ToArray(),
                    GapSites = sites.AsReadOnly(), PocketAnchors = optional.AsReadOnly(), Doors = doors.AsReadOnly(), Modules = modules.AsReadOnly(),
                    // Horizontal yaw is managed math, so seeded layout tests need no native engine.
                    PlayerSpawnPosition = spawn, PlayerSpawnRotation = new Quaternion(0f, (float)Math.Sin(facing), 0f, (float)Math.Cos(facing))
                };
                candidate.ExitDoorYaw = (ProceduralExitHubUtility.ApproachYaw(localSpawn, localExit) + first.Turns * 90f) % 360f;
                ProceduralTemplateStoreyUtility.Apply(candidate, _config, _random);
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
            HashSet<Vector2Int> occupied, HashSet<Vector2Int> gaps, List<ProceduralDoorPlan> doors, List<ProceduralGapSite> sites,
            int parent = 0, ProceduralThemeData theme = null)
        {
            // Establish the starting hub before extending branches. A count check at
            // the end alone would turn otherwise valid seeded floors into fallbacks.
            bool needsHubEntrance = pocket == 0 && placed[0].OpenDoors.Length < ProceduralExitHubUtility.MinimumEntrances;
            var sockets = placed.Where(r => r.PocketId == 0 && r.OpenDoors.Length < r.MaximumEntrances && (!needsHubEntrance || r.RoomId == placed[0].RoomId))
                .Where(r => parent == 0 || r.RoomId == parent)
                .SelectMany(r => Enumerable.Range(0, r.Template.Doors.Length)
                .Where(i => !r.OpenDoors.Contains(i) && (r.Template.PassageGap == null ||
                    ProceduralTemplateUtility.Door(r.Template.Doors[i]) != r.Template.PassageGap.Edge)).Select(i => (room: r, index: i))).ToArray();
            if (sockets.Length == 0) return false;
            var from = sockets[_random.Next(sockets.Length)]; int to = _random.Next(template.Doors.Length);
            if (template.PassageGap != null && ProceduralTemplateUtility.Door(template.Doors[to]) == template.PassageGap.Edge) return false;
            var targetCatalogue = _config.RoomCatalogue.Catalogues.Single(c => c.Templates.Contains(template));
            if (pocket != 0 && from.room.Catalogue.Theme != targetCatalogue.Theme) return false;
            if (from.room.Catalogue.Theme != targetCatalogue.Theme &&
                (template.Transition == null || !template.Transition.Doors.Any(d => d.Index == to))) return false;
            var fromPoint = ProceduralTemplateUtility.Point(from.room, ProceduralTemplateUtility.Door(from.room.Template.Doors[from.index]), _config.Origin);
            if (sites.Any(s => (s.Edge - fromPoint).sqrMagnitude < .01f)) return false;
            var normal = ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Direction(from.room.Template.Doors[from.index].Side), from.room.Turns);
            int turns = Enumerable.Range(0, 4).Single(t =>
                ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Direction(template.Doors[to].Side), t) == -normal);
            var world = ProceduralTemplateUtility.Point(from.room, ProceduralTemplateUtility.Door(from.room.Template.Doors[from.index]), Vector2.zero);
            var target = ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Door(template.Doors[to]), turns);
            var delta = world - target;
            if (delta.x % 1f != 0f || delta.z % 1f != 0f) return false;
            var offset = new Vector2Int((int)Math.Floor(delta.x / 2f), (int)Math.Floor(delta.z / 2f));
            var next = new ProceduralTemplateRoom { RoomId = placed.Count + 1, PocketId = pocket, Template = template, Turns = turns,
                Catalogue = targetCatalogue, Theme = theme ?? from.room.Theme,
                Offset = offset + normal * gap,
                SubcellOffset = new Vector2Int((int)delta.x, (int)delta.z) - offset * 2 };
            var cells = Cells(next).ToArray();
            if (cells.Any(c => occupied.Contains(c) || gaps.Contains(c))) return false;
            var catalogue = _config.RoomCatalogue?.Catalogues.FirstOrDefault(c => c.Templates.Contains(template));
            if (catalogue != null && (!ProceduralTemplateUtility.DoorClear(from.room.Template, from.index, from.room.Catalogue ?? catalogue) ||
                !ProceduralTemplateUtility.DoorClear(template, to, catalogue))) return false;
            if (catalogue != null && placed.Any(other => !ProceduralTemplateUtility.Compatible(next, other, catalogue))) return false;
            var reserved = new List<Vector2Int>();
            if (template.PassageGap != null)
            {
                var declaration = template.PassageGap;
                next.PassagePocket = new ProceduralTemplateRoom { Template = catalogue.Templates.Single(t => t.Id == declaration.PocketTemplateId),
                    Catalogue = catalogue, Theme = next.Theme, Turns = (turns + declaration.PocketTurns) % 4,
                    Offset = next.Offset + ProceduralTemplateUtility.Rotate(declaration.PocketOffset, turns), SubcellOffset = next.SubcellOffset };
                reserved.AddRange(PassageReservation(next));
                if (reserved.Any(c => occupied.Contains(c) || gaps.Contains(c) || cells.Contains(c)) ||
                    placed.Any(r => !ProceduralTemplateUtility.Compatible(next.PassagePocket, r, catalogue))) return false;
            }
            if (gap > 0)
            {
                // Reserve the full four-metre frame corridor in metre subcells.
                var tangent = new Vector2Int(normal.y, -normal.x);
                for (int i = 0; i < gap * 2; i++) for (int side = -2; side < 2; side++)
                {
                    var p = world + new Vector3(normal.x, 0f, normal.y) * (i + .5f) + new Vector3(tangent.x, 0f, tangent.y) * (side + .5f);
                    var c = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
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
                    if (other.Catalogue.Theme != next.Catalogue.Theme) continue;
                    if (parent != 0 && (next.OpenDoors.Length >= next.Template.Doors.Length - 1 ||
                        other.OpenDoors.Length >= other.Template.Doors.Length - 1)) continue;
                    if (other.Template.PassageGap != null && ProceduralTemplateUtility.Door(other.Template.Doors[a]) == other.Template.PassageGap.Edge ||
                        next.Template.PassageGap != null && ProceduralTemplateUtility.Door(next.Template.Doors[b]) == next.Template.PassageGap.Edge) continue;
                    if (other.OpenDoors.Contains(a) || next.OpenDoors.Contains(b)) continue;
                    if (other.OpenDoors.Length >= other.MaximumEntrances || next.OpenDoors.Length >= next.MaximumEntrances) continue;
                    if (catalogue != null && (!ProceduralTemplateUtility.DoorClear(other.Template, a, other.Catalogue ?? catalogue) ||
                        !ProceduralTemplateUtility.DoorClear(next.Template, b, catalogue))) continue;
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
            => ProceduralTemplateUtility.OccupiedCells(room);
        private static IEnumerable<Vector2Int> PassageReservation(ProceduralTemplateRoom room)
        {
            foreach (var c in Cells(room.PassagePocket)) yield return c;
            var gap = room.Template.PassageGap;
            var direction = (gap.Landing - gap.Edge).normalized;
            var side = new Vector3(direction.z, 0f, -direction.x);
            for (float along = .5f; along < gap.GapLength; along += 1f)
            for (float across = -(float)Math.Ceiling(gap.Width * .5f) + .5f; across < Math.Ceiling(gap.Width * .5f); across += 1f)
            {
                var p = ProceduralTemplateUtility.Point(room, gap.Edge + direction * along + side * across, Vector2.zero);
                yield return new Vector2Int((int)Math.Floor(p.x), (int)Math.Floor(p.z));
            }
        }
        public static string Manifest(ProceduralLayout layout)
        {
            var text = new StringBuilder("|templates-v2|organic-fallback=").Append(Uri.EscapeDataString(layout.TemplateFallbackReason));
            foreach (var room in layout.TemplateRooms)
                text.Append("|Template:").Append(room.RoomId).Append(',').Append(room.Template.Id).Append(',').Append(room.Offset.x.ToString(CultureInfo.InvariantCulture))
                    .Append(',').Append(room.Offset.y.ToString(CultureInfo.InvariantCulture)).Append(',').Append(room.Turns)
                    .Append(",subcell=").Append(room.SubcellOffset.x).Append(';').Append(room.SubcellOffset.y).Append(",open=")
                    .Append(string.Join(";", room.OpenDoors.OrderBy(i => i))).Append(",biome=").Append(room.Catalogue?.Theme ?? layout.ThemeId);
            return text.Append("|HunterCapacity:").Append(layout.ValidatedHunterSpawnCapacity).ToString();
        }
    }
}
