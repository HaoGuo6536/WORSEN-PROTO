// ============================================================================
// FloorLoopLevelSetup.cs
// ============================================================================
// PURPOSE:
//   Authors a complete six-room FloorLoop with shared ordinary navigation and
//   collapse coverage for every floor, ramp, doorway and cake anchor.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Level deterministic content builder.
// KEY RESPONSIBILITIES:
//   - Build three elevation bands, braided routes and stable Level marker records.
//   - Preserve owned material, mesh and navigation asset identities on rebuild.
//   - Give the six free-standing waist vaults explicit landings on both sides.
// DEPENDENCIES:
//   - Core records, Domain Level, UnityEditor and installed AI Navigation.
// USAGE NOTES:
//   Coordinator invokes under the exclusive Unity lease with an identity parent.
//   Only the named generated child and Assets/Greybox/FloorLoop assets are owned.
//   BuildNavigation is explicit; neither operation saves a scene or project settings.
//   All ramps are ordinary 1:4 slopes. No actor-specific areas, links or exclusions.
//   Bounds include ramp bottoms; navigation probes use FloorHeight, not bounds.min.y.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using Worsen.Domain.Level;

namespace Worsen.Editor.Level
{
    public static class FloorLoopLevelSetup
    {
        public const string GeneratedRootName = "Generated Floor Loop Level";
        public const string NavigationAssetPath = "Assets/Greybox/FloorLoop/FloorLoopNavMesh.asset";
        private const string AssetRoot = "Assets/Greybox/FloorLoop";
        private const float RoomWidth = 28f;
        private const float WallTop = 16f;
        public static Vector3 SpawnPosition => new Vector3(-36f, 0.1f, -10f);
        public static Vector3 HunterSpawnPosition => new Vector3(36f, 6.1f, 14f);

        public static float FloorHeight(float worldX)
        {
            if (worldX <= -14f) return 0f;
            if (worldX < -2f) return (worldX + 14f) * 0.25f;
            if (worldX <= 14f) return 3f;
            if (worldX < 26f) return 3f + (worldX - 14f) * 0.25f;
            return 6f;
        }

        public static IReadOnlyList<LevelMarkerRecord> CreateMarkerRecords()
        {
            var records = new List<LevelMarkerRecord>();
            for (var row = 0; row < 2; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    var id = 1 + row * 3 + column;
                    var x = (column - 1) * RoomWidth;
                    var z = (row == 0 ? -1f : 1f) * RoomWidth * 0.5f;
                    var bottom = FloorHeight(x - RoomWidth * 0.5f);
                    records.Add(new LevelMarkerRecord(id, LevelMarkerKind.Room, id, 0,
                        new Vector3(x, (bottom + WallTop) * 0.5f, z),
                        new Vector3(RoomWidth, WallTop - bottom, RoomWidth)));
                    AddAnchor(records, 1000 + id * 10, id, x - 8f, z - 7f, CakeAnchorType.Flow);
                    AddAnchor(records, 1001 + id * 10, id, x + 8f, z - 7f,
                        id % 2 == 0 ? CakeAnchorType.Risk : CakeAnchorType.Precision);
                    AddAnchor(records, 1002 + id * 10, id, x + 8f, z + 8f,
                        column == 2 ? CakeAnchorType.Vertical : CakeAnchorType.Detour);
                    records.Add(new LevelMarkerRecord(200 + id, LevelMarkerKind.VaultSurface, id, 0,
                        new Vector3(x + 4f, FloorHeight(x + 4f) + 0.5f, z - 6f),
                        new Vector3(4f, 1f, 0.8f)));
                    records.Add(new LevelMarkerRecord(220 + id, LevelMarkerKind.ReboundSurface, id, 0,
                        new Vector3(x + 5f, FloorHeight(x + 5f) + 2.5f, z + 1f),
                        new Vector3(2.5f, 5f, 4f)));
                    records.Add(new LevelMarkerRecord(240 + id, LevelMarkerKind.LosBreak, id, 0,
                        new Vector3(x + 5f, FloorHeight(x + 5f), z + 1f), Vector3.one));
                    if (column == 2) continue;
                    for (var door = 0; door < 2; door++)
                        records.Add(new LevelMarkerRecord(100 + row * 10 + column * 2 + door,
                            LevelMarkerKind.RoomLink, id, id + 1,
                            new Vector3(x + 14f, FloorHeight(x + 14f), z + (door == 0 ? -7f : 7f)),
                            new Vector3(0.4f, 3f, 6f)));
                }
            }
            for (var column = 0; column < 3; column++)
            {
                var x = (column - 1) * RoomWidth + 4f;
                records.Add(new LevelMarkerRecord(120 + column, LevelMarkerKind.RoomLink,
                    column + 1, column + 4, new Vector3(x, FloorHeight(x), 0f), new Vector3(6f, 3f, 0.4f)));
            }
            records.Add(new LevelMarkerRecord(2000, LevelMarkerKind.ExitMarker, 6, 0,
                new Vector3(38f, 6.1f, 22f), Vector3.one));
            return records.AsReadOnly();
        }

        public static LevelManager Build(Transform parent)
        {
            RequireEditor();
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (parent.position != Vector3.zero || parent.rotation != Quaternion.identity || parent.lossyScale != Vector3.one)
                throw new InvalidOperationException("The FloorLoop assembly parent must have an identity transform.");
            var records = CreateMarkerRecords();
            // Validate the exact snapshot before replacing any previously authored content.
            new LevelController(new LevelBehaviorState()).Rebuild(records);
            var existing = parent.Find(GeneratedRootName);
            if (existing != null)
            {
                if (existing.GetComponent<LevelManager>() == null)
                    throw new InvalidOperationException("An unrelated object uses the generated FloorLoop root name.");
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
            var root = Group(parent, GeneratedRootName);
            root.SetActive(false);
            var driver = root.AddComponent<LevelDriver>();
            var registry = root.AddComponent<LevelMarkerRegistry>();
            var manager = root.AddComponent<LevelManager>();
            var floors = new[]
            {
                Material("LowFloor", new Color(0.25f, 0.34f, 0.40f)),
                Material("MiddleFloor", new Color(0.30f, 0.39f, 0.29f)),
                Material("UpperFloor", new Color(0.43f, 0.35f, 0.25f))
            };
            var wall = Material("Walls", new Color(0.48f, 0.51f, 0.54f));
            var vault = Material("Vault", new Color(0.95f, 0.38f, 0.10f));
            var stripe = Material("ReboundStripe", new Color(0.10f, 0.65f, 0.95f), true);
            var meshes = Enumerable.Range(0, 3).Select(CreateFloorMesh).ToArray();
            foreach (var record in records.Where(record => record.Kind == LevelMarkerKind.Room))
            {
                var floor = Group(root.transform, "Room " + record.Id + " Walkable Floor And Ramp");
                floor.transform.position = new Vector3(record.Position.x, 0f, record.Position.z);
                var column = (record.Id - 1) % 3;
                floor.AddComponent<MeshFilter>().sharedMesh = meshes[column];
                floor.AddComponent<MeshRenderer>().sharedMaterial = floors[column];
                floor.AddComponent<MeshCollider>().sharedMesh = meshes[column];
            }
            BuildWalls(root.transform, wall);
            foreach (var record in records)
            {
                GameObject item;
                if (record.Kind == LevelMarkerKind.VaultSurface || record.Kind == LevelMarkerKind.ReboundSurface)
                {
                    item = Box(root.transform, record.Kind + " " + record.Id, record.Position, record.Size,
                        record.Kind == LevelMarkerKind.VaultSurface ? vault : wall);
                    if (record.Kind == LevelMarkerKind.ReboundSurface)
                        Box(item.transform, "Rebound Stripe", record.Position + Vector3.left * (record.Size.x * 0.5f + 0.02f),
                            new Vector3(0.04f, 0.3f, record.Size.z), stripe, false);
                }
                else
                {
                    item = Group(root.transform, record.Kind + " " + record.Id);
                    item.transform.position = record.Position;
                }
                AddMarker(item, record);
            }
            Wire(driver, "_markers", root.GetComponentsInChildren<LevelMarker>(true));
            Wire(manager, "_driver", driver);
            Wire(manager, "_registry", registry);
            root.SetActive(true);
            return manager;
        }

        public static NavMeshSurface BuildNavigation(LevelManager level)
        {
            RequireEditor();
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (level.name != GeneratedRootName)
                throw new InvalidOperationException("Navigation must be built on the generated FloorLoop Level root.");
            var surface = level.GetComponent<NavMeshSurface>();
            if (surface == null) surface = level.gameObject.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            surface.BuildNavMesh();
            var baked = surface.navMeshData;
            if (baked == null) throw new InvalidOperationException("FloorLoop produced no navigation data.");
            EnsureFolder(AssetRoot);
            var saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavigationAssetPath);
            surface.RemoveData();
            if (saved == null)
            {
                AssetDatabase.CreateAsset(baked, NavigationAssetPath);
                saved = baked;
            }
            else
            {
                EditorUtility.CopySerialized(baked, saved);
                UnityEngine.Object.DestroyImmediate(baked);
                EditorUtility.SetDirty(saved);
            }
            surface.navMeshData = saved;
            surface.AddData();
            AssetDatabase.SaveAssetIfDirty(saved);
            return surface;
        }

        private static void AddAnchor(List<LevelMarkerRecord> records, int id, int room, float x, float z, CakeAnchorType type)
        {
            records.Add(new LevelMarkerRecord(id, LevelMarkerKind.CakeAnchor, room, 0,
                new Vector3(x, FloorHeight(x) + 0.1f, z), Vector3.one, anchorType: type));
        }

        private static Mesh CreateFloorMesh(int column)
        {
            var centerX = (column - 1) * RoomWidth;
            var vertices = new List<Vector3>();
            foreach (var localX in new[] { -14f, -2f, 14f })
            {
                var y = FloorHeight(centerX + localX);
                vertices.Add(new Vector3(localX, y, -14f));
                vertices.Add(new Vector3(localX, y, 14f));
            }
            var mesh = new Mesh
            {
                name = "FloorLoop Floor Band " + column,
                vertices = vertices.ToArray(),
                triangles = new[] { 0, 1, 3, 0, 3, 2, 2, 3, 5, 2, 5, 4 }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EnsureFolder(AssetRoot + "/Meshes");
            var path = AssetRoot + "/Meshes/FloorBand" + column + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                saved = mesh;
            }
            else
            {
                EditorUtility.CopySerialized(mesh, saved);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(saved);
            }
            AssetDatabase.SaveAssetIfDirty(saved);
            return saved;
        }

        private static void BuildWalls(Transform root, Material wall)
        {
            // Outer walls stay inside the tiled footprint. Boundary walls have two
            // six-metre apertures per row; row crossovers provide the ladder braids.
            foreach (var x in new[] { -41.8f, 41.8f })
                Box(root, "Outer End Wall", new Vector3(x, WallTop * 0.5f, 0f), new Vector3(0.4f, WallTop, 56f), wall);
            foreach (var z in new[] { -27.8f, 27.8f })
                Box(root, "Outer Side Wall", new Vector3(0f, WallTop * 0.5f, z), new Vector3(84f, WallTop, 0.4f), wall);
            foreach (var x in new[] { -14f, 14f })
            {
                for (var row = 0; row < 2; row++)
                {
                    var startZ = -28f + row * RoomWidth;
                    foreach (var segment in new[] { new Vector2(0f, 4f), new Vector2(10f, 18f), new Vector2(24f, 28f) })
                        Box(root, "Column Boundary Divider", new Vector3(x, WallTop * 0.5f, startZ + (segment.x + segment.y) * 0.5f),
                            new Vector3(0.4f, WallTop, segment.y - segment.x), wall);
                }
            }
            for (var column = 0; column < 3; column++)
            {
                var startX = -42f + column * RoomWidth;
                foreach (var segment in new[] { new Vector2(0f, 15f), new Vector2(21f, 28f) })
                    Box(root, "Row Boundary Divider", new Vector3(startX + (segment.x + segment.y) * 0.5f, WallTop * 0.5f, 0f),
                        new Vector3(segment.y - segment.x, WallTop, 0.4f), wall);
            }
        }

        private static void AddMarker(GameObject item, LevelMarkerRecord record)
        {
            var marker = item.AddComponent<LevelMarker>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("_id").intValue = record.Id;
            serialized.FindProperty("_kind").enumValueIndex = (int)record.Kind;
            serialized.FindProperty("_roomId").intValue = record.RoomId;
            serialized.FindProperty("_targetRoomId").intValue = record.TargetRoomId;
            serialized.FindProperty("_size").vector3Value = record.Size;
            serialized.FindProperty("_bidirectional").boolValue = record.Bidirectional;
            serialized.FindProperty("_access").intValue = (int)record.Access;
            serialized.FindProperty("_anchorType").enumValueIndex = (int)record.AnchorType;
            var target = record.Position;
            if (record.Kind == LevelMarkerKind.VaultSurface)
                target = new Vector3(record.Position.x, FloorHeight(record.Position.x), record.Position.z + 1.6f);
            else if (record.Kind == LevelMarkerKind.ReboundSurface)
                target = new Vector3(record.Position.x - 3f, FloorHeight(record.Position.x - 3f), record.Position.z);
            serialized.FindProperty("_targetPosition").vector3Value = target;
            bool hasEndpointPair = record.Kind == LevelMarkerKind.VaultSurface && record.Bidirectional &&
                record.Id >= 201 && record.Id <= 206;
            serialized.FindProperty("_hasEndpointPair").boolValue = hasEndpointPair;
            serialized.FindProperty("_oppositeTargetPosition").vector3Value = hasEndpointPair
                ? new Vector3(record.Position.x, FloorHeight(record.Position.x), record.Position.z - 1.6f)
                : Vector3.zero;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Group(Transform parent, string name)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            return item;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collision = true)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            // Decorative children may have a scaled primitive parent.
            var scale = parent.lossyScale;
            item.transform.localScale = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z);
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            return item;
        }

        private static Material Material(string name, Color color, bool emissive = false)
        {
            EnsureFolder(AssetRoot + "/Materials");
            var path = AssetRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The Universal Render Pipeline Lit shader is missing.");
            material = new Material(shader) { name = name, color = color };
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Wire(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Wire(UnityEngine.Object target, string field, LevelMarker[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }

        private static void RequireEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before generating FloorLoop content.");
        }
    }
}
