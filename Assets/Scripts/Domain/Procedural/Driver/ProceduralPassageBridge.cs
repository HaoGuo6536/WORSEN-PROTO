// ============================================================================
// ProceduralPassageBridge.cs
// ============================================================================
// PURPOSE:
//   Applies temporary Passage crossings without mixing their lifetime into floor
//   construction. Rebuilds only the owning floor's navigation allocation when an
//   aperture opens or a tile loses its physical support, then reports collapse facts.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by ProceduralDriver · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Replace sealed wall pieces, create tile colliders and apply pure falling poses.
//   - Rebuild the injected navigation sources and fail closed if support cannot update.
//   - Report committed tile collapse facts to the owning Driver.
// DEPENDENCIES:
//   - Own Presenter/DriverState and UnityEngine.AI; no other gameplay systems.
// USAGE NOTES:
//   Scene-owned. The owner injects its allocation state and samples delta time; this
//   sub-driver never ticks independently. The owner releases the root/navigation on
//   teardown, including every object created here. No global navigation is cleared.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Worsen.Domain.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralPassageBridge : MonoBehaviour
    {
        private ProceduralDriverState _state;
        private readonly ProceduralPassagePresenter _presenter = new ProceduralPassagePresenter();
        public event Action<int, int, int, Vector3> TileCollapsed;
        public void Configure(ProceduralDriverState state) => _state = state;

        public bool Open(ProceduralLayout layout, int siteIndex, ProceduralConfig config, out ProceduralPassagePlan plan)
        {
            plan = null;
            if (_state == null || !_state.Ready || siteIndex < 0 || siteIndex >= layout.ShrineSites.Count) return false;
            var site = layout.ShrineSites[siteIndex];
            int pocket = layout.Modules.FirstOrDefault(m => m.RoomId == site.DestinationPocketRoomId).PocketId;
            if (!site.GapEdge || pocket == 0 || _state.Passages.Any(p =>
                layout.Modules.Single(m => m.RoomId == p.Plan.PocketRoomId).PocketId == pocket)) return false;
            var blocks = _state.FragmentPlans.SelectMany(pair => pair.Value.Where((b, i) =>
                _state.Fragments[pair.Key][i] != null && _state.Fragments[pair.Key][i].activeSelf)).ToArray();
            try { plan = _presenter.Build(layout, siteIndex, config, _state.Config, blocks); }
            catch (ArgumentException) { return false; }
            var sources = new List<NavMeshBuildSource>(_state.NavigationSources);
            foreach (var wall in plan.Walls)
            {
                if (wall.Original.HasCollision) sources.Remove(Source(wall.Original));
                sources.AddRange(wall.Pieces.Where(b => b.HasCollision).Select(Source));
            }
            sources.AddRange(plan.Tiles.Select(Source));
            if (!ReplaceNavigation(sources, plan)) { plan = null; return false; }
            foreach (var wall in plan.Walls)
            {
                int index = _state.FragmentPlans[wall.Original.RoomId].IndexOf(wall.Original);
                var item = _state.Fragments[wall.Original.RoomId][index];
                var material = item.GetComponent<Renderer>().sharedMaterial;
                item.SetActive(false);
                foreach (var piece in wall.Pieces)
                {
                    _state.Fragments[piece.RoomId].Add(CreateBox(piece, material, "Passage aperture"));
                    _state.FragmentPlans[piece.RoomId].Add(piece); _state.BlockCount++;
                }
            }
            var passage = new ProceduralPassageDriverState { Plan = plan };
            foreach (var tile in plan.Tiles)
                passage.Tiles.Add(CreateBox(tile, _state.PassageMaterial, "Passage " + siteIndex + " tile " + passage.Tiles.Count));
            _state.Passages.Add(passage); _state.LinedPocketAnchors.AddRange(plan.LinedAnchors);
            Physics.SyncTransforms();
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (_state == null || !_state.Ready || _state.Passages.Count == 0) return;
            var facts = new List<(ProceduralPassagePlan plan, int index)>();
            foreach (var passage in _state.Passages)
            {
                foreach (int index in _presenter.Advance(passage, deltaTime, _state.Config))
                {
                    passage.Tiles[index].GetComponent<Collider>().enabled = false;
                    _state.NavigationSources.Remove(Source(passage.Plan.Tiles[index]));
                    facts.Add((passage.Plan, index));
                }
                for (int i = 0; i < passage.CollapsedCount; i++)
                {
                    var item = passage.Tiles[i];
                    item.transform.position = passage.Plan.Tiles[i].Center + _presenter.FallOffset(passage.Elapsed, i, _state.Config);
                    if (passage.Elapsed >= _state.Config.PassageFirstTileDelay + (double)i * _state.Config.PassageTileInterval + _state.Config.PassageFallDuration)
                        item.SetActive(false);
                }
            }
            if (facts.Count > 0 && !ReplaceNavigation(new List<NavMeshBuildSource>(_state.NavigationSources), null))
            {
                // Never retain a navigable bridge whose physical support is gone.
                if (_state.NavigationInstance.valid) _state.NavigationInstance.Remove();
                _state.Ready = false;
                throw new InvalidOperationException("Passage collapse navigation rebuild failed.");
            }
            Physics.SyncTransforms();
            foreach (var fact in facts)
                TileCollapsed?.Invoke(fact.plan.SiteIndex, fact.plan.PocketRoomId, fact.index, fact.plan.TilePositions[fact.index]);
        }

        private GameObject CreateBox(ProceduralBlock block, Material material, string label)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = label; item.layer = _state.Config.GeometryLayer;
            item.transform.SetParent(_state.Root.transform, false);
            item.transform.SetPositionAndRotation(block.Center, block.Rotation); item.transform.localScale = block.Size;
            var renderer = item.GetComponent<Renderer>(); renderer.sharedMaterial = material; renderer.enabled = block.HasRenderer;
            if (!block.HasCollision) { var collider = item.GetComponent<Collider>(); collider.enabled = false; Release(collider); }
            return item;
        }

        private bool ReplaceNavigation(List<NavMeshBuildSource> sources, ProceduralPassagePlan passage)
        {
            var data = NavMeshBuilder.BuildNavMeshData(_state.NavigationSettings, sources, _state.NavigationBounds, Vector3.zero, Quaternion.identity);
            if (data == null) return false;
            _state.NavigationInstance.Remove();
            var instance = NavMesh.AddNavMeshData(data);
            var filter = new NavMeshQueryFilter { agentTypeID = _state.Config.NavMeshAgentTypeId, areaMask = 1 };
            var path = new NavMeshPath();
            bool valid = instance.valid && (passage == null ||
                (NavMesh.SamplePosition(passage.Start, out var start, _state.Config.NavSampleRadius, filter) &&
                 NavMesh.SamplePosition(passage.End, out var end, _state.Config.NavSampleRadius, filter) &&
                 NavMesh.CalculatePath(start.position, end.position, filter, path) && path.status == NavMeshPathStatus.PathComplete));
            if (!valid)
            {
                if (instance.valid) instance.Remove(); Release(data);
                _state.NavigationInstance = NavMesh.AddNavMeshData(_state.NavigationData);
                return false;
            }
            Release(_state.NavigationData); _state.NavigationData = data; _state.NavigationInstance = instance;
            _state.NavigationSources.Clear(); _state.NavigationSources.AddRange(sources);
            return true;
        }
        private static NavMeshBuildSource Source(ProceduralBlock block) => new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(block.Center, block.Rotation, Vector3.one),
            size = block.Size, area = new ProceduralNavigationPresenter().Area(block)
        };
        private static void Release(UnityEngine.Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
