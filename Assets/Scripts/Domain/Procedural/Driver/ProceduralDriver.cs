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
        public int OwnedBlockCount => _state.BlockCount;
        public bool IsReady => _state.Ready;
        public IReadOnlyList<LevelMarkerRecord> TraversalMarkers => _state.TraversalMarkers ?? Array.Empty<LevelMarkerRecord>();

        public void Build(ProceduralLayout layout, ProceduralConfig config, ProceduralDriverConfig driverConfig)
        {
            Teardown();
            if (transform.lossyScale != Vector3.one) throw new InvalidOperationException("Procedural owner requires unit world scale.");
            var blocks = _presenter.Build(layout, config, driverConfig);
            try
            {
                _state.Root = new GameObject("Generated Closed Rooms - Round " + layout.RoundIndex);
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
            _state.BlockCount++;
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
