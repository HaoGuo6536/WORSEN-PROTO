// ============================================================================
// ProceduralDriver.cs
// ============================================================================
// PURPOSE:
//   Builds the runtime collision shell and navigation for a generated floor.
//   The exact same box descriptions feed visible cubes, physical colliders and
//   navigation sources, so a graph cannot be admitted without usable geometry.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Create enclosed rooms and bake bounded navigation from explicit owned sources.
//   - Verify native paths to every cake, room, hunter spawn and exit before admission.
//   - Apply crack textures and bounded masonry splitting with matching colliders.
//   - Tear down only the navigation instance, materials and geometry this Driver owns.
// DEPENDENCIES:
//   - UnityEngine.AI runtime navigation API; no package assembly or Domain sibling.
// USAGE NOTES:
//   Scene-owned and commanded only by ProceduralManager. Does not change global
//   lighting/fog or remove another owner's navigation data. Missing materials use
//   dark rough generated surfaces; no authored-map fallback is silently loaded.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralDriver : MonoBehaviour
    {
        private readonly ProceduralDriverState _state = new ProceduralDriverState();
        private readonly ProceduralGeometryPresenter _presenter = new ProceduralGeometryPresenter();
        private readonly ProceduralFracturePresenter _fracture = new ProceduralFracturePresenter();
        public int OwnedBlockCount => _state.BlockCount;
        public bool IsReady => _state.Ready;
        public IReadOnlyList<LevelMarkerRecord> TraversalMarkers => _state.TraversalMarkers ?? Array.Empty<LevelMarkerRecord>();

        public void Build(ProceduralLayout layout, ProceduralConfig config, ProceduralDriverConfig driverConfig)
        {
            Teardown();
            if (transform.lossyScale != Vector3.one) throw new InvalidOperationException("Procedural owner requires unit world scale.");
            var blocks = _presenter.Build(layout, config, driverConfig);
            foreach (var room in layout.Graph.Rooms) _state.RoomBounds.Add(room.Id, room.Bounds);
            try
            {
                _state.Root = new GameObject("Generated Castle Rooms - Round " + layout.RoundIndex);
                _state.Root.SetActive(false);
                _state.Root.transform.SetParent(transform, false);
                // Layout positions are world-space; parent placement must not transform the generated map.
                _state.Root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                _state.Root.transform.localScale = Vector3.one;
                var wall = MaterialOrFallback(driverConfig.WallMaterial, driverConfig.WallColor, driverConfig);
                var floor = MaterialOrFallback(driverConfig.FloorMaterial, driverConfig.FloorColor, driverConfig);
                var ceiling = MaterialOrFallback(driverConfig.CeilingMaterial, driverConfig.CeilingColor, driverConfig);
                foreach (var block in blocks)
                    CreateBlock(block, block.Kind == ProceduralSurfaceKind.Floor ? floor :
                        block.Kind == ProceduralSurfaceKind.Ceiling ? ceiling : wall, driverConfig.GeometryLayer);
                _state.TraversalMarkers = new ProceduralRoutePresenter().DescribeMarkers(blocks);
                BuildNavigation(layout, blocks, driverConfig);
                foreach (var room in layout.Graph.Rooms) CreateSafetySlab(room, driverConfig.GeometryLayer);
                _state.Root.SetActive(true);
                Physics.SyncTransforms();
                _state.Ready = true;
            }
            catch { Teardown(); throw; }
        }

        public void Teardown()
        {
            _state.Ready = false;
            if (_state.NavigationInstance.valid) _state.NavigationInstance.Remove();
            _state.NavigationInstance = default;
            if (_state.NavigationData != null) Release(_state.NavigationData);
            _state.NavigationData = null;
            if (_state.Root != null) { _state.Root.SetActive(false); Release(_state.Root); }
            _state.Root = null;
            foreach (var material in _state.OwnedMaterials) if (material != null) Release(material);
            _state.OwnedMaterials.Clear();
            _state.BlockCount = 0;
            _state.TraversalMarkers = null;
            _state.Fragments.Clear(); _state.FragmentPlans.Clear(); _state.RoomBounds.Clear();
            _state.CrackMaterials.Clear();
            if (_state.CrackTexture != null) Release(_state.CrackTexture);
            _state.CrackTexture = null;
        }

        private void OnDestroy() => Teardown();

        private void CreateBlock(ProceduralBlock block, Material material, int layer)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = "Room " + block.RoomId + " " + block.Kind;
            item.layer = layer;
            item.transform.SetParent(_state.Root.transform, false);
            item.transform.SetPositionAndRotation(block.Center, Quaternion.identity);
            item.transform.localScale = block.Size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (block.SurfaceId != 0) item.AddComponent<ProceduralTraversalSurface>().Configure(block);
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
            var settings = NavMesh.GetSettingsByID(config.NavMeshAgentTypeId);
            if (settings.agentTypeID != config.NavMeshAgentTypeId || settings.agentRadius <= 0f || settings.agentHeight <= 0f)
                throw new InvalidOperationException("The configured navigation agent type is unavailable.");
            settings.overrideVoxelSize = true;
            settings.voxelSize = config.NavVoxelSize;
            var sources = new List<NavMeshBuildSource>(blocks.Count);
            foreach (var block in blocks)
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(block.Center, Quaternion.identity, Vector3.one),
                    size = block.Size,
                    area = block.Kind == ProceduralSurfaceKind.Floor ? 0 : 1
                });
            var bounds = _presenter.NavigationBounds(blocks, config.NavBoundsPadding);
            _state.NavigationData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (_state.NavigationData == null) throw new InvalidOperationException("Runtime navigation bake returned no data.");
            _state.NavigationData.name = "Procedural Navigation - Round " + layout.RoundIndex;
            _state.NavigationInstance = NavMesh.AddNavMeshData(_state.NavigationData);
            if (!_state.NavigationInstance.valid) throw new InvalidOperationException("Runtime navigation data could not be installed.");
            ValidateNavigation(layout, config);
            ValidateShortcutDetours(blocks, config);
        }

        private static void ValidateNavigation(ProceduralLayout layout, ProceduralDriverConfig config)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = config.NavMeshAgentTypeId, areaMask = 1 };
            if (!NavMesh.SamplePosition(layout.PlayerSpawnPosition, out var start, config.NavSampleRadius, filter))
                throw new InvalidOperationException("Generated player spawn has no walkable navigation.");
            var targets = new List<Vector3> { layout.Graph.ExitPosition };
            foreach (var anchor in layout.Graph.Anchors) targets.Add(anchor.Position);
            foreach (var position in layout.HunterSpawnPositions) targets.Add(position);
            var path = new NavMeshPath();
            foreach (var target in targets)
                if (!NavMesh.SamplePosition(target, out var end, config.NavSampleRadius, filter) ||
                    !NavMesh.CalculatePath(start.position, end.position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Generated navigation cannot reach required position " + target + ".");
        }

        private static void ValidateShortcutDetours(IReadOnlyList<ProceduralBlock> blocks, ProceduralDriverConfig config)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = config.NavMeshAgentTypeId, areaMask = 1 };
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

        private Material MaterialOrFallback(Material configured, Color color, ProceduralDriverConfig config)
        {
            if (configured != null) return configured;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Generated rooms require a compatible lit material shader.");
            var material = new Material(shader) { name = "Procedural dark surface", color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", config.SurfaceSmoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", config.SurfaceSmoothness);
            _state.OwnedMaterials.Add(material);
            return material;
        }
        private static void Release(UnityEngine.Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
