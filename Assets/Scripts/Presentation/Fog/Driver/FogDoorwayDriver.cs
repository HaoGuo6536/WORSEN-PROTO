// ============================================================================
// FogDoorwayDriver.cs
// ============================================================================
// PURPOSE:
//   Renders a small, transparent grey sheet at each published room doorway.
//   Unlike particles or the legacy field, the sheet cannot expand into the corridor.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FogDriver · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Build private doorway meshes from pure confined geometry.
//   - Show the final doorway face only when the travelling room front has arrived.
//   - Destroy every owned mesh, material and root on floor replacement.
// DEPENDENCIES:
//   - Core room facts, own Fog presenter/config/state and Unity rendering APIs.
// USAGE NOTES:
//   Scene-owned. FogDriver supplies all commands; no Update or global settings.
//   Requires the serialized DoorwayShader. The coordinator wires it through setup.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Fog
{
    public sealed class FogDoorwayDriver : MonoBehaviour
    {
        // DriverState (§7c): passive per-sheet engine resources, owned only by this sub-driver.
        private sealed class SheetDriverState
        {
            public int Room;
            public GameObject Root;
            public Mesh Mesh;
            public Material Material;
        }
        private readonly List<SheetDriverState> _sheets = new List<SheetDriverState>();
        private bool _missingShaderReported;

        public void Build(IReadOnlyList<GeneratedRoomSample> rooms, FogDriverConfig config)
        {
            Teardown();
            gameObject.SetActive(true);
            if (rooms == null) return;
            foreach (var room in rooms)
            {
                var seen = new HashSet<Vector3>();
                foreach (var portal in room.PortalCenters ?? System.Array.Empty<Vector3>())
                {
                    if (!seen.Add(portal)) continue;
                    Vector3[] vertices = FogDoorwayPresenter.Vertices(room, portal, config.PortalWidth,
                        config.PortalHeight, config.DoorwayInset, config.PortalMatchTolerance);
                    if (vertices.Length == 0) continue;
                    if (config.DoorwayShader == null)
                    {
                        if (!_missingShaderReported)
                        {
                            _missingShaderReported = true;
                            Debug.LogError("FogDriverConfig requires DoorwayShader (Worsen/Collapse Doorway Haze). Rebuild Fog assets.", this);
                        }
                        return;
                    }
                    var sheet = new SheetDriverState { Room = room.RoomId,
                        Root = new GameObject("Doorway haze " + room.RoomId),
                        Mesh = new Mesh { name = "Owned doorway haze mesh" },
                        Material = new Material(config.DoorwayShader) { name = "Owned doorway haze material" } };
                    _sheets.Add(sheet);
                    sheet.Root.transform.SetParent(transform, false);
                    sheet.Root.transform.position = Vector3.zero;
                    sheet.Root.transform.rotation = Quaternion.identity;
                    // World geometry is converted to local so the owner may have a nonidentity transform.
                    for (int i = 0; i < vertices.Length; i++) vertices[i] = sheet.Root.transform.InverseTransformPoint(vertices[i]);
                    sheet.Mesh.vertices = vertices;
                    sheet.Mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                    sheet.Mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                    sheet.Mesh.RecalculateBounds();
                    sheet.Root.AddComponent<MeshFilter>().sharedMesh = sheet.Mesh;
                    var renderer = sheet.Root.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = sheet.Material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    sheet.Root.SetActive(false);
                }
            }
        }

        public void Apply(FogDriverState state, FogDriverConfig config)
        {
            foreach (var sheet in _sheets)
            {
                float progress = state.Rooms.TryGetValue(sheet.Room, out var room) ? room.Progress : 0f;
                sheet.Root.SetActive(state.Enabled && progress >= 1f);
                Color color = FogLookPresenter.Body(state.Look, config);
                color.a = config.DoorwayOpacity * progress;
                sheet.Material.SetColor("_HazeColor", color);
            }
        }

        public void Teardown()
        {
            foreach (var sheet in _sheets)
            {
                if (sheet.Root != null) sheet.Root.SetActive(false);
                Release(sheet.Root); Release(sheet.Mesh); Release(sheet.Material);
            }
            _sheets.Clear();
        }
        private void OnDestroy() => Teardown();
        private static void Release(Object value)
        { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
