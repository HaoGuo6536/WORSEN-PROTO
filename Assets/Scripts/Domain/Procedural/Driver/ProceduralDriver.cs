// ============================================================================
// ProceduralDriver.cs
// ============================================================================
// PURPOSE:
//   Builds the runtime collision shell and navigation for a generated floor.
//   Box roles separate stepped visuals from continuous ramp collision. Collision
//   and navigation share box poses so admission requires usable physical geometry.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Build kit visuals with primitive collision/fallback and owned collapse fragments.
//   - Bake navigation and verify objectives, spawn capacity and pocket isolation.
//   - Own interactables, puzzles and routed state changes without sibling calls.
//   - Admit shrine sites and Passage apertures, tiles and future reward counts.
//   - Release only owned geometry, materials, links and navigation on teardown.
// DEPENDENCIES:
//   - UnityEngine.AI runtime navigation API; no package assembly or Domain sibling.
// USAGE NOTES:
//   Scene-owned and commanded only by ProceduralManager. Does not change global
//   lighting/fog or remove another owner's navigation data. Missing materials use
//   dark rough generated surfaces; no authored-map fallback is silently loaded.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Procedural
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProceduralPassageBridge))]
    public sealed class ProceduralDriver : MonoBehaviour
    {
        private readonly ProceduralDriverState _state = new ProceduralDriverState();
        private readonly ProceduralGeometryPresenter _presenter = new ProceduralGeometryPresenter();
        private readonly ProceduralFracturePresenter _fracture = new ProceduralFracturePresenter();
        private ProceduralPassageBridge _passageBridge;
        public int OwnedBlockCount => _state.BlockCount;
        public bool IsReady => _state.Ready;
        public EntityId PuzzlePlayerId => _state.PuzzlePlayer;
        public event Action<int, int, int> PuzzleSolved;
        public event Action<int, int, int, Vector3> PassageTileCollapsed;
        public IReadOnlyList<LevelAnchor> LinedPocketAnchors => _state.LinedPocketAnchors.AsReadOnly();
        public IReadOnlyList<LevelMarkerRecord> TraversalMarkers => _state.TraversalMarkers ?? Array.Empty<LevelMarkerRecord>();

        private void OnEnable()
        {
            EnsurePassageBridge();
            _passageBridge.TileCollapsed += OnPassageTileCollapsed;
        }
        private void OnDisable() { if (_passageBridge != null) _passageBridge.TileCollapsed -= OnPassageTileCollapsed; }
        private void OnPassageTileCollapsed(int site, int pocket, int tile, Vector3 position)
            => PassageTileCollapsed?.Invoke(site, pocket, tile, position);
        private void EnsurePassageBridge()
        {
            if (_passageBridge == null) _passageBridge = GetComponent<ProceduralPassageBridge>();
            if (_passageBridge == null) _passageBridge = gameObject.AddComponent<ProceduralPassageBridge>();
            _passageBridge.Configure(_state);
        }

        public void Build(ProceduralLayout layout, ProceduralConfig config, ProceduralDriverConfig driverConfig,
            Func<Collider, bool> puzzleActor = null)
        {
            Teardown();
            EnsurePassageBridge();
            if (transform.lossyScale != Vector3.one) throw new InvalidOperationException("Procedural owner requires unit world scale.");
            var blocks = _presenter.Build(layout, config, driverConfig);
            ProceduralStoreyUtility.Validate(layout, config);
            new ProceduralStoreyPresenter().ValidateLandings(layout, blocks);
            var objects = new ProceduralInteractablePresenter();
            layout.Interactables = objects.Build(layout, config, driverConfig, blocks, new System.Random(layout.Seed));
            var puzzles = new ProceduralPuzzleLayoutPresenter();
            layout.Puzzles = puzzles.Build(layout, config.Challenges, blocks, new System.Random(layout.Seed));
            var themed = layout.Theme != null && !layout.Theme.InheritMaterials ? layout.Theme : null;
            var freezeDoors = layout.Interactables.ToList();
            foreach (var freeze in layout.FreezeRooms)
            {
                var door = layout.Doors[freeze.DoorIndex]; int edge = 1001 + freeze.DoorIndex;
                if (freezeDoors.Any(p => p.State.Id == 100000 + edge)) continue;
                freezeDoors.Add(new ProceduralInteractablePlan(new InteractableState(100000 + edge, InteractableKind.Door,
                    door.FromRoomId, door.Center + Vector3.up * (config.DoorHeight * 0.5f), InteractableStateValue.Open, edge),
                    door.AlongX ? new Vector3(config.DoorWidth, config.DoorHeight, driverConfig.WallThickness) :
                        new Vector3(driverConfig.WallThickness, config.DoorHeight, config.DoorWidth)));
            }
            layout.Interactables = Array.AsReadOnly(freezeDoors.OrderBy(p => p.State.Id).ToArray());
            layout.InteractableManifest = objects.Manifest(layout.Interactables) + puzzles.Manifest(layout.Puzzles);
            foreach (var room in layout.Graph.Rooms) _state.RoomBounds.Add(room.Id, room.Bounds);
            try
            {
                _state.Root = new GameObject("Generated Castle Rooms - Round " + layout.RoundIndex);
                _state.Root.SetActive(false);
                _state.Root.transform.SetParent(transform, false);
                // Layout positions are world-space; parent placement must not transform the generated map.
                _state.Root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                _state.Root.transform.localScale = Vector3.one;
                var wall = MaterialOrFallback(themed == null ? driverConfig.WallMaterial : null,
                    themed?.Wall ?? driverConfig.WallColor, driverConfig, themed?.Smoothness);
                var floor = MaterialOrFallback(themed == null ? driverConfig.FloorMaterial : null,
                    themed?.Floor ?? driverConfig.FloorColor, driverConfig, themed?.Smoothness);
                var ceiling = MaterialOrFallback(themed == null ? driverConfig.CeilingMaterial : null,
                    themed?.Ceiling ?? driverConfig.CeilingColor, driverConfig, themed?.Smoothness);
                _state.Config = driverConfig; _state.PassageMaterial = floor;
                _state.Catalogue = config.RoomCatalogue; _state.ThemeId = layout.ThemeId;
                foreach (var block in blocks)
                    CreateBlock(block, block.Kind == ProceduralSurfaceKind.Floor ? floor :
                        block.Kind == ProceduralSurfaceKind.Ceiling ? ceiling : wall, driverConfig.GeometryLayer);
                _state.TraversalMarkers = new ProceduralRoutePresenter().DescribeMarkers(blocks);
                foreach (var plan in layout.Interactables)
                {
                    if (plan.SurfaceId != 0 || plan.State.Kind == InteractableKind.Light) continue;
                    var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    item.name = "Interactable " + plan.State.Id + " " + plan.State.Kind;
                    if (themed != null && plan.State.Kind == InteractableKind.KnockableProp) item.name += " " + themed.Prop;
                    item.layer = driverConfig.GeometryLayer;
                    item.transform.SetParent(_state.Root.transform, false);
                    item.transform.position = plan.State.Position; item.transform.localScale = plan.Size;
                    item.GetComponent<Renderer>().sharedMaterial = wall;
                    if (themed != null && plan.State.Kind == InteractableKind.KnockableProp && themed.PropPrimitive != PrimitiveType.Cube)
                    {
                        // Keep the exact collision envelope; swap only the visible mesh.
                        var source = GameObject.CreatePrimitive(themed.PropPrimitive);
                        var mesh = Instantiate(source.GetComponent<MeshFilter>().sharedMesh);
                        var vertices = mesh.vertices;
                        for (int i = 0; i < vertices.Length; i++) vertices[i].y *= 0.5f;
                        mesh.vertices = vertices; mesh.RecalculateBounds();
                        item.GetComponent<MeshFilter>().sharedMesh = mesh;
                        _state.OwnedMeshes.Add(mesh); source.SetActive(false); Release(source);
                    }
                    var worldObject = item.AddComponent<ProceduralWorldObject>(); worldObject.Configure(plan.State);
                    _state.Interactables.Add(plan.State.Id, worldObject);
                }
                var navigationBlocks = blocks.ToList();
                foreach (var plan in layout.Puzzles)
                {
                    var item = new GameObject("Optional " + plan.Kind + " " + plan.Id);
                    item.transform.SetParent(_state.Root.transform, false);
                    var module = item.AddComponent<ProceduralPuzzleModule>();
                    module.Configure(plan, config.Challenges, wall, driverConfig.GeometryLayer, puzzleActor);
                    _state.Puzzles.Add(module);
                    navigationBlocks.AddRange(puzzles.Blocks(plan, config.Challenges).Where((b, index) => index != 4));
                }
                _state.TraversalMarkers = new ProceduralRoutePresenter().DescribeMarkers(navigationBlocks);
                layout.ShrineSites = new ProceduralShrineSitePresenter().Build(layout, config, navigationBlocks);
                BuildNavigation(layout, navigationBlocks, driverConfig);
                layout.FuturePassageGoldenAnchorCount = new ProceduralPassagePresenter().FutureGoldenAnchorCount(layout, config, driverConfig, navigationBlocks);
                layout.InteractableManifest += "|FuturePassageGold:" + layout.FuturePassageGoldenAnchorCount;
                layout.InteractableManifest += new ProceduralShrineSitePresenter().Manifest(layout.ShrineSites, config);
                foreach (var room in layout.Graph.Rooms)
                foreach (var cell in ProceduralFootprintUtility.Volumes(layout, room)) CreateSafetySlab(cell, driverConfig.GeometryLayer);
                _state.Root.SetActive(true);
                Physics.SyncTransforms();
                _state.Ready = true;
            }
            catch { Teardown(); throw; }
        }

        public void Teardown()
        {
            _state.Ready = false;
            _state.Catalogue = null; _state.ThemeId = null;
            _state.Passages.Clear(); _state.LinedPocketAnchors.Clear();
            _state.NavigationSources.Clear(); _state.Config = null; _state.PassageMaterial = null;
            _state.Puzzles.Clear(); _state.PuzzlePlayer = EntityId.None;
            foreach (var link in _state.NavigationLinks) if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link);
            _state.NavigationLinks.Clear();
            if (_state.NavigationInstance.valid) _state.NavigationInstance.Remove();
            _state.NavigationInstance = default;
            if (_state.NavigationData != null) Release(_state.NavigationData);
            _state.NavigationData = null;
            if (_state.Root != null) { _state.Root.SetActive(false); Release(_state.Root); }
            _state.Root = null;
            foreach (var material in _state.OwnedMaterials) if (material != null) Release(material);
            _state.OwnedMaterials.Clear();
            foreach (var mesh in _state.OwnedMeshes) if (mesh != null) Release(mesh);
            _state.OwnedMeshes.Clear();
            _state.BlockCount = 0;
            _state.TraversalMarkers = null;
            _state.Fragments.Clear(); _state.FragmentPlans.Clear(); _state.RoomBounds.Clear();
            _state.CrackMaterials.Clear();
            _state.Interactables.Clear();
            if (_state.CrackTexture != null) Release(_state.CrackTexture);
            _state.CrackTexture = null;
        }

        private void OnDestroy() => Teardown();

        public bool ActivatePassage(ProceduralLayout layout, int siteIndex, ProceduralConfig config, out ProceduralPassagePlan plan)
            => _passageBridge.Open(layout, siteIndex, config, out plan);

        // Engine time is sampled here, never in the pure collapse presenter.
        private void Update() { if (_state.Ready) TickPassages(Time.deltaTime); }
        public void TickPassages(float deltaTime)
            => _passageBridge.Tick(deltaTime);

        public void TickPuzzles(PlayerMovementSample sample, float deltaTime)
        {
            if (!_state.Ready || sample.Id == EntityId.None) return;
            _state.PuzzlePlayer = sample.Id;
            float speed = new Vector2(sample.Velocity.x, sample.Velocity.z).magnitude;
            foreach (var puzzle in _state.Puzzles)
                if (puzzle.gameObject.activeSelf && puzzle.Tick(sample.Position, speed, deltaTime))
                    PuzzleSolved?.Invoke(puzzle.Plan.Id, puzzle.Plan.RoomId, puzzle.Plan.Reward.Id);
        }
        public void CompletePuzzleVault(int surfaceId, bool succeeded)
        {
            if (!_state.Ready) return;
            foreach (var puzzle in _state.Puzzles) puzzle.CompleteVault(surfaceId, succeeded);
        }

        private void CreateBlock(ProceduralBlock block, Material material, int layer)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = "Room " + block.RoomId + " " + block.Kind;
            item.layer = layer;
            item.transform.SetParent(_state.Root.transform, false);
            item.transform.SetPositionAndRotation(block.Center, block.Rotation);
            item.transform.localScale = block.Size;
            var renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = material; renderer.enabled = block.HasRenderer;
            var prefab = block.PieceId == null ? null : _state.Catalogue?.Piece(_state.ThemeId, block.PieceId);
            if (prefab != null && block.HasRenderer)
            {
                var visual = Instantiate(prefab);
                visual.name = "Kit " + block.PieceId;
                visual.transform.SetPositionAndRotation(block.PiecePosition, block.Rotation);
                visual.transform.SetParent(item.transform, true);
                foreach (var child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                renderer.enabled = false;
            }
            if (!block.HasCollision)
            {
                var collider = item.GetComponent<Collider>(); collider.enabled = false; Release(collider);
            }
            if (block.SurfaceId != 0) item.AddComponent<ProceduralTraversalSurface>().Configure(block);
            if (block.TraversalKind == TraversalSurfaceKind.Vault)
            {
                var state = new InteractableState(200000 + block.SurfaceId, InteractableKind.Partition,
                    block.RoomId, block.Center, InteractableStateValue.Inactive);
                var worldObject = item.AddComponent<ProceduralWorldObject>(); worldObject.Configure(state);
                _state.Interactables.Add(state.Id, worldObject);
            }
            if (!_state.Fragments.TryGetValue(block.RoomId, out var fragments))
            {
                fragments = new List<GameObject>(); _state.Fragments.Add(block.RoomId, fragments);
                _state.FragmentPlans.Add(block.RoomId, new List<ProceduralBlock>());
            }
            fragments.Add(item); _state.FragmentPlans[block.RoomId].Add(block);
            _state.BlockCount++;
        }

        public void SetRoomDestruction(RoomDestructionSample sample)
        {
            if (sample.Phase == RoomPhase.Closed)
                foreach (var puzzle in _state.Puzzles.Where(p => p.Plan.RoomId == sample.RoomId)) puzzle.gameObject.SetActive(false);
            if (!_state.Ready || !_state.Fragments.TryGetValue(sample.RoomId, out var fragments)) return;
            var plans = _state.FragmentPlans[sample.RoomId];
            var bounds = _state.RoomBounds[sample.RoomId];
            for (int index = 0; index < fragments.Count; index++)
                if (fragments[index] != null)
                    fragments[index].transform.position = plans[index].Center + _fracture.Offset(plans[index], bounds, sample, index);
            if (sample.Phase != RoomPhase.Open && !_state.CrackMaterials.ContainsKey(sample.RoomId))
                AddCracks(sample.RoomId, fragments);
            if (_state.CrackMaterials.TryGetValue(sample.RoomId, out var material))
            {
                var tint = new Color(0.012f, 0.006f, 0.018f, _fracture.CrackOpacity(sample));
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
                if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            }
        }

        public void ApplyInteractableState(InteractableState state)
        {
            if (!_state.Ready || !_state.Interactables.TryGetValue(state.Id, out var item) || item == null) return;
            item.Apply(state);
            if (state.Kind == InteractableKind.Partition && state.Value == InteractableStateValue.Broken)
                _state.TraversalMarkers = Array.AsReadOnly(_state.TraversalMarkers.Where(marker => marker.Id != state.Id - 200000).ToArray());
        }

        private void CreateSafetySlab(LevelRoom room, int layer)
        {
            var slab = new GameObject("Room " + room.Id + " concealed collapse catch slab");
            slab.layer = layer; slab.transform.SetParent(_state.Root.transform, false);
            slab.transform.position = new Vector3(room.Center.x, -0.6f, room.Center.z);
            slab.AddComponent<BoxCollider>().size = new Vector3(room.Size.x, 0.3f, room.Size.z);
        }

        private void AddCracks(int roomId, IReadOnlyList<GameObject> fragments)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
            if (shader == null) return;
            if (_state.CrackTexture == null) _state.CrackTexture = CreateCrackTexture();
            var material = new Material(shader) { name = "Room " + roomId + " fracture mask", renderQueue = 3001 };
            material.mainTexture = _state.CrackTexture;
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 5f);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 10f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _state.CrackMaterials.Add(roomId, material); _state.OwnedMaterials.Add(material);
            foreach (var fragment in fragments)
            {
                if (!fragment.GetComponent<Renderer>().enabled) continue;
                var overlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
                overlay.name = "Fracture texture overlay"; overlay.transform.SetParent(fragment.transform, false);
                overlay.transform.localScale = Vector3.one * 1.0015f;
                var collider = overlay.GetComponent<Collider>(); collider.enabled = false; Release(collider);
                overlay.GetComponent<Renderer>().sharedMaterial = material;
            }
        }
        private Texture2D CreateCrackTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Seeded masonry fissures", wrapMode = TextureWrapMode.Repeat };
            var pixels = _fracture.CrackPixels(size);
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        private void BuildNavigation(ProceduralLayout layout, IReadOnlyList<ProceduralBlock> blocks, ProceduralDriverConfig config)
        {
            var navigation = new ProceduralNavigationPresenter();
            navigation.Validate(config);
            var settings = NavMesh.GetSettingsByID(config.NavMeshAgentTypeId);
            if (settings.agentTypeID != config.NavMeshAgentTypeId || settings.agentRadius <= 0f || settings.agentHeight <= 0f)
                throw new InvalidOperationException("The configured navigation agent type is unavailable.");
            settings.overrideVoxelSize = true;
            settings.voxelSize = config.NavVoxelSize;
            settings.ledgeDropHeight = 0f;
            settings.maxJumpAcrossDistance = 0f;
            var sources = new List<NavMeshBuildSource>(blocks.Count);
            foreach (var block in blocks)
            {
                if (!block.HasCollision) continue;
                if (block.Role == ProceduralBlockRole.StairRamp &&
                    Vector3.Angle(block.Rotation * Vector3.up, Vector3.up) >= settings.agentSlope)
                    throw new InvalidOperationException("Generated stair ramp must be below the navigation agent's maximum slope.");
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(block.Center, block.Rotation, Vector3.one),
                    size = block.Size,
                    area = navigation.Area(block)
                });
            }
            var bounds = _presenter.NavigationBounds(blocks, config.NavBoundsPadding);
            foreach (var plan in layout.Interactables.Where(p => p.State.Kind == InteractableKind.KnockableProp))
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(plan.State.Position, Quaternion.identity, Vector3.one), size = plan.Size, area = 1 });
            _state.NavigationSettings = settings; _state.NavigationBounds = bounds;
            _state.NavigationSources.AddRange(sources);
            _state.NavigationData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (_state.NavigationData == null) throw new InvalidOperationException("Runtime navigation bake returned no data.");
            _state.NavigationData.name = "Procedural Navigation - Round " + layout.RoundIndex;
            _state.NavigationInstance = NavMesh.AddNavMeshData(_state.NavigationData);
            if (!_state.NavigationInstance.valid) throw new InvalidOperationException("Runtime navigation data could not be installed.");
            if (config.EnablePartitionIgnoringLinks)
                foreach (var plan in navigation.Links(layout, blocks, config))
                {
                    var link = NavMesh.AddLink(new NavMeshLinkData { startPosition = plan.Start, endPosition = plan.End,
                        agentTypeID = config.NavMeshAgentTypeId, area = plan.Area, bidirectional = true, width = 0f, costModifier = -1f });
                    if (!NavMesh.IsLinkValid(link)) throw new InvalidOperationException("Partition navigation link could not be installed.");
                    _state.NavigationLinks.Add(link);
                }
            ValidateNavigation(layout, config);
            ValidateShortcutDetours(blocks, config);
        }

        private static void ValidateNavigation(ProceduralLayout layout, ProceduralDriverConfig config)
        {
            ProceduralFootprintUtility.Validate(layout);
            var filter = new NavMeshQueryFilter { agentTypeID = config.NavMeshAgentTypeId, areaMask = config.HunterAreaMask };
            if (!NavMesh.SamplePosition(layout.PlayerSpawnPosition, out var start, config.NavSampleRadius, filter))
                throw new InvalidOperationException("Generated player spawn has no walkable navigation.");
            var targets = new List<Vector3> { layout.Graph.ExitPosition };
            foreach (var anchor in layout.Graph.Anchors) targets.Add(anchor.Position);
            foreach (var position in layout.HunterSpawnPositions) targets.Add(position);
            foreach (var route in layout.VerticalRoutes)
            { targets.Add(route.Points[0]); targets.Add(route.Points.Last()); }
            var path = new NavMeshPath();
            foreach (var target in targets)
                if (!NavMesh.SamplePosition(target, out var end, config.NavSampleRadius, filter) ||
                    !NavMesh.CalculatePath(start.position, end.position, filter, path) || path.status != NavMeshPathStatus.PathComplete ||
                    !NavMesh.CalculatePath(end.position, start.position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Generated navigation cannot reach required position " + target + ".");
            foreach (var pocket in layout.Modules.Where(m => m.PocketId != 0).GroupBy(m => m.PocketId))
            {
                var anchors = layout.PocketAnchors.Where(a => pocket.Any(m => m.RoomId == a.RoomId)).ToArray();
                if (anchors.Length == 0 || !NavMesh.SamplePosition(anchors[0].Position, out var origin, config.NavSampleRadius, filter))
                    throw new InvalidOperationException("Optional pocket has no navigation.");
                if (NavMesh.CalculatePath(start.position, origin.position, filter, path) && path.status == NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Optional pocket unexpectedly connects to the main floor.");
                foreach (var anchor in anchors)
                    if (!NavMesh.SamplePosition(anchor.Position, out var end, config.NavSampleRadius, filter) ||
                        !NavMesh.CalculatePath(origin.position, end.position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                        throw new InvalidOperationException("Optional pocket anchor has no local walking route.");
            }
            // Candidate sockets are optional: keep only reachable sites inside their room.
            // A small floor may offer fewer sites; the Shrine system places what fits.
            layout.ShrineSites = Array.AsReadOnly(layout.ShrineSites.Where(site =>
                NavMesh.SamplePosition(site.Position, out var end, config.NavSampleRadius, filter) &&
                layout.Graph.Rooms.Single(room => room.Id == site.RoomId).ContainsXZ(end.position) &&
                NavMesh.CalculatePath(start.position, end.position, filter, path) && path.status == NavMeshPathStatus.PathComplete &&
                NavMesh.CalculatePath(end.position, start.position, filter, path) && path.status == NavMeshPathStatus.PathComplete).ToArray());
        }

        private static void ValidateShortcutDetours(IReadOnlyList<ProceduralBlock> blocks, ProceduralDriverConfig config)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = config.NavMeshAgentTypeId, areaMask = config.HunterAreaMask };
            foreach (var block in blocks)
            {
                if (block.TraversalKind != TraversalSurfaceKind.Vault && block.TraversalKind != TraversalSurfaceKind.SlideGate) continue;
                if (!NavMesh.SamplePosition(block.EndpointA, out var start, config.NavSampleRadius, filter) ||
                    !NavMesh.SamplePosition(block.EndpointB, out var end, config.NavSampleRadius, filter))
                    throw new InvalidOperationException("A shortcut landing has no ordinary walking bypass.");
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(start.position, end.position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Hunter cannot walk around a generated partition.");
                if (path.corners.Length < 3)
                    throw new InvalidOperationException("A player shortcut was incorrectly admitted as a straight Hunter route.");
            }
        }

        private Material MaterialOrFallback(Material configured, Color color, ProceduralDriverConfig config, float? smoothness = null)
        {
            if (configured != null) return configured;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Generated rooms require a compatible lit material shader.");
            var material = new Material(shader) { name = "Procedural dark surface", color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness ?? config.SurfaceSmoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness ?? config.SurfaceSmoothness);
            _state.OwnedMaterials.Add(material);
            return material;
        }
        private static void Release(UnityEngine.Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
