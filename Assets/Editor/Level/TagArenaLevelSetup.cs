// ============================================================================
// TagArenaLevelSetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds the authored traversal cluster from primitives and stable marker ids.
//   Scene assembly calls this local builder so geometry, registration and baked
//   navigation can be recreated without hand-wiring a shared scene.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Level deterministic content builder.
// KEY RESPONSIBILITIES:
//   - Build the three height classes, actor-specific routes, affordances and navigation.
//   - Author both landing endpoints only for the bidirectional clutter-route vaults.
//   - Bring the rising line to deck height at its west edge for capsule clearance.
// DEPENDENCIES:
//   - Core records, Domain Level components, UnityEditor and installed AI Navigation.
// USAGE NOTES:
//   Coordinator invokes under the exclusive Unity lease. Build changes only its
//   named generated child and Level-owned materials; it never saves scenes or
//   changes project settings. BuildNavigation is an explicit separate operation.
//   Root must have identity transform; HunterRouteGate must already be provisioned.
//   Existing material and navigation asset GUIDs survive repeat builds.
// ============================================================================

using System;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using Worsen.Domain.Level;

namespace Worsen.Editor.Level
{
    public static class TagArenaLevelSetup
    {
        public const string GeneratedRootName = "Generated Tag Arena Level";
        public const string NavigationAssetPath = "Assets/Greybox/TagArena/TagArenaNavMesh.asset";
        private const string MaterialPath = "Assets/Greybox/TagArena/Materials/";
        public static Vector3 SpawnPosition => new Vector3(-20f, 0f, 0f);

        public static LevelManager Build(Transform parent)
        {
            RequireEditor();
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (parent.position != Vector3.zero || parent.rotation != Quaternion.identity || parent.lossyScale != Vector3.one)
                throw new InvalidOperationException("The arena assembly parent must have an identity transform.");
            var gateLayer = LayerMask.NameToLayer("HunterRouteGate");
            if (gateLayer < 0) throw new InvalidOperationException("Provision the HunterRouteGate layer before building Level.");
            var existing = parent.Find(GeneratedRootName);
            if (existing != null)
            {
                if (existing.GetComponent<LevelManager>() == null)
                    throw new InvalidOperationException("An unrelated object uses the generated Level root name.");
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
            var root = new GameObject(GeneratedRootName);
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var driver = root.AddComponent<LevelDriver>();
            var registry = root.AddComponent<LevelMarkerRegistry>();
            var manager = root.AddComponent<LevelManager>();
            var floor = Material("Floor", new Color(0.23f, 0.26f, 0.29f));
            var wall = Material("Walls", new Color(0.48f, 0.51f, 0.54f));
            var vault = Material("Vault", new Color(0.8f, 0.28f, 0.08f));
            var lit = Material("VaultEdge", new Color(1f, 0.72f, 0.1f), true);
            var rebound = Material("ReboundStripe", new Color(0.1f, 0.65f, 0.95f), true);
            var gate = Material("HunterGate", new Color(0.85f, 0.05f, 0.13f), true);

            BuildRooms(root.transform, floor, wall);
            BuildMidAffordances(root.transform, wall, vault, lit, rebound);
            BuildVerticalLine(root.transform, floor, wall, lit);
            BuildPlayerRoute(root.transform, floor, wall, vault, lit);
            BuildHunterRoute(root.transform, floor, wall, gate, gateLayer);
            BuildGraphMarkers(root.transform);
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
            var surface = level.GetComponent<NavMeshSurface>();
            if (surface == null) surface = level.gameObject.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            foreach (var oldLink in level.GetComponentsInChildren<NavMeshLink>(true))
                UnityEngine.Object.DestroyImmediate(oldLink);
            surface.BuildNavMesh();
            var baked = surface.navMeshData;
            if (baked == null) throw new InvalidOperationException("The arena produced no navigation data.");
            EnsureFolder("Assets/Greybox/TagArena");
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
            foreach (var marker in level.GetComponentsInChildren<LevelMarker>(true))
            {
                if (marker.MarkerKind != LevelMarkerKind.HunterLink) continue;
                var record = marker.Capture();
                var link = marker.gameObject.AddComponent<NavMeshLink>();
                link.agentTypeID = 0;
                link.startPoint = Vector3.zero;
                link.endPoint = marker.transform.InverseTransformPoint(marker.Target);
                link.width = record.Size.x;
                link.bidirectional = record.Bidirectional;
                link.area = 0;
                link.autoUpdate = false;
            }
            AssetDatabase.SaveAssetIfDirty(saved);
            return surface;
        }

        private static void BuildRooms(Transform root, Material floor, Material wall)
        {
            Box(root, "Low Connector Floor", new Vector3(-18f, -0.25f, 0f), new Vector3(12f, 0.5f, 8f), floor);
            Box(root, "Low Connector Ceiling", new Vector3(-18f, 3.25f, 0f), new Vector3(12f, 0.5f, 8f), wall);
            Box(root, "Low West Wall", new Vector3(-24f, 1.5f, 0f), new Vector3(0.4f, 3f, 8f), wall);
            foreach (var z in new[] { -4f, 4f })
            {
                Box(root, "Low Wall Long", new Vector3(-15.5f, 1.5f, z), new Vector3(7f, 3f, 0.4f), wall);
                Box(root, "Low Wall End", new Vector3(-23.5f, 1.5f, z), new Vector3(1f, 3f, 0.4f), wall);
            }
            Box(root, "Mid Braided Floor", new Vector3(0f, -0.25f, 0f), new Vector3(24f, 0.5f, 20f), floor);
            Box(root, "Mid Ceiling", new Vector3(0f, 6.25f, 0f), new Vector3(24f, 0.5f, 20f), wall);
            foreach (var z in new[] { -10f, 10f })
                Box(root, "Mid Outer Wall", new Vector3(0f, 3f, z), new Vector3(24f, 6f, 0.4f), wall);
            foreach (var z in new[] { -7f, 7f })
                Box(root, "Mid Connector Wall", new Vector3(-12f, 3f, z), new Vector3(0.4f, 6f, 6f), wall);
            foreach (var z in new[] { -8f, 0f, 8f })
                Box(root, "Mid Atrium Braided Divider", new Vector3(12f, 3f, z), new Vector3(0.4f, 6f, 4f), wall);
            Box(root, "Tall Atrium Floor", new Vector3(21f, -0.25f, 0f), new Vector3(18f, 0.5f, 20f), floor);
            Box(root, "Tall Atrium Ceiling", new Vector3(21f, 10.25f, 0f), new Vector3(18f, 0.5f, 20f), wall);
            Box(root, "Atrium East Wall", new Vector3(30f, 5f, 0f), new Vector3(0.4f, 10f, 20f), wall);
            foreach (var z in new[] { -10f, 10f })
            {
                Box(root, "Atrium Outer Wall", new Vector3(18.5f, 5f, z), new Vector3(13f, 10f, 0.4f), wall);
                Box(root, "Atrium Wall End", new Vector3(29.5f, 5f, z), new Vector3(1f, 10f, 0.4f), wall);
            }
        }

        private static void BuildMidAffordances(Transform root, Material wall, Material vault, Material lit, Material rebound)
        {
            var pillar = Box(root, "Mid Micro-loop Sight Break", new Vector3(-5f, 2f, 3f), new Vector3(3f, 4f, 6f), wall);
            Marker(pillar, 202, LevelMarkerKind.ReboundSurface, 2, target: new Vector3(-8f, 0f, 3f));
            Box(root, "Rebound Stripe West", new Vector3(-6.52f, 1.4f, 3f), new Vector3(0.04f, 0.35f, 6f), rebound, false);
            Box(root, "Rebound Stripe East", new Vector3(-3.48f, 1.4f, 3f), new Vector3(0.04f, 0.35f, 6f), rebound, false);
            Mark(root, "Mid LOS Break", 203, LevelMarkerKind.LosBreak, 2, new Vector3(-5f, 0f, 3f));
            var hurdle = Box(root, "Mid Waist Vault", new Vector3(-4f, 0.5f, -6f), new Vector3(3f, 1f, 0.8f), vault);
            Marker(hurdle, 201, LevelMarkerKind.VaultSurface, 2, target: new Vector3(-4f, 0f, -4.5f));
            Box(root, "Mid Vault Lit Top", new Vector3(-4f, 1.02f, -6f), new Vector3(3f, 0.04f, 0.8f), lit, false);
        }

        private static void BuildVerticalLine(Transform root, Material floor, Material wall, Material lit)
        {
            // Reach the deck's west edge at x=23; a longer ramp climbs into its underside.
            var ramp = Box(root, "Atrium Rising Line", new Vector3(18f, 1.75f, -6f),
                new Vector3(Mathf.Sqrt(116f), 0.5f, 3f), floor);
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(4f, 10f) * Mathf.Rad2Deg);
            Box(root, "Atrium Upper Deck", new Vector3(26f, 3.75f, 0f), new Vector3(6f, 0.5f, 15f), floor);
            Box(root, "Atrium Upper Bridge To Mid", new Vector3(17f, 3.75f, 4f), new Vector3(14f, 0.5f, 3f), floor);
            Box(root, "Upper Bridge Rail North", new Vector3(17f, 4.5f, 5.5f), new Vector3(14f, 1f, 0.2f), wall);
            Box(root, "Upper Bridge Rail South", new Vector3(17f, 4.5f, 2.5f), new Vector3(14f, 1f, 0.2f), wall);
            var lip = Box(root, "One-way Drop Lit Lip", new Vector3(10f, 4.08f, 4f), new Vector3(0.3f, 0.16f, 3f), lit);
            Marker(lip, 106, LevelMarkerKind.OneWayDrop, 3, 2, false, TraversalAccess.Player, new Vector3(7.5f, 0f, 4f));
        }

        private static void BuildPlayerRoute(Transform root, Material floor, Material wall, Material vault, Material lit)
        {
            var route = Group(root, "Player-only Clutter Route");
            route.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            Box(route.transform, "Player Route Floor", new Vector3(3f, -0.25f, 15f), new Vector3(52f, 0.5f, 4f), floor);
            Box(route.transform, "Player Route West Join", new Vector3(-21f, -0.25f, 8.5f), new Vector3(4f, 0.5f, 9f), floor);
            Box(route.transform, "Player Route East Join", new Vector3(27f, -0.25f, 11.5f), new Vector3(4f, 0.5f, 3f), floor);
            RouteWalls(route.transform, 15f, wall);
            Box(route.transform, "Player West Join Wall", new Vector3(-23f, 1.5f, 8.5f), new Vector3(0.3f, 3f, 9f), wall);
            Box(route.transform, "Player East Join Wall", new Vector3(29f, 1.5f, 11.5f), new Vector3(0.3f, 3f, 3f), wall);
            var index = 0;
            foreach (var x in new[] { -10f, 4f, 18f })
            {
                var hurdle = Box(route.transform, "Player Waist Vault " + index, new Vector3(x, 0.5f, 15f), new Vector3(0.8f, 1f, 4f), vault);
                Marker(hurdle, 210 + index, LevelMarkerKind.VaultSurface, 2, access: TraversalAccess.Player,
                    target: new Vector3(x + 1.6f, 0f, 15f), oppositeTarget: new Vector3(x - 1.6f, 0f, 15f));
                Box(route.transform, "Vault Lit Top " + index, new Vector3(x, 1.02f, 15f), new Vector3(0.8f, 0.04f, 4f), lit, false);
                index++;
            }
            index = 0;
            foreach (var x in new[] { -3f, 11f })
            {
                var slide = Box(route.transform, "Slide Gate " + index, new Vector3(x, 2.05f, 15f), new Vector3(1f, 2f, 4f), wall);
                Marker(slide, 220 + index, LevelMarkerKind.SlideGate, 2, access: TraversalAccess.Player);
                Box(route.transform, "Slide Gate Bottom Edge " + index, new Vector3(x, 1.08f, 15f), new Vector3(1.04f, 0.06f, 4f), lit, false);
                index++;
            }
        }

        private static void BuildHunterRoute(Transform root, Material floor, Material wall, Material gate, int gateLayer)
        {
            var route = Group(root, "Hunter-only Straight Corridor");
            Box(route.transform, "Hunter Straight Floor", new Vector3(3f, -0.25f, -15f), new Vector3(52f, 0.5f, 4f), floor);
            Box(route.transform, "Hunter West Join", new Vector3(-21f, -0.25f, -8.5f), new Vector3(4f, 0.5f, 9f), floor);
            Box(route.transform, "Hunter East Join", new Vector3(27f, -0.25f, -11.5f), new Vector3(4f, 0.5f, 3f), floor);
            RouteWalls(route.transform, -15f, wall);
            Box(route.transform, "Hunter West Join Wall", new Vector3(-23f, 1.5f, -8.5f), new Vector3(0.3f, 3f, 9f), wall);
            Box(route.transform, "Hunter East Join Wall", new Vector3(29f, 1.5f, -11.5f), new Vector3(0.3f, 3f, 3f), wall);
            HunterGate(route.transform, "Hunter West Gate", new Vector3(-21f, 0f, -6f), 107, 1, gate, gateLayer);
            HunterGate(route.transform, "Hunter East Gate", new Vector3(27f, 0f, -11.5f), 108, 3, gate, gateLayer);
        }

        private static void RouteWalls(Transform root, float z, Material wall)
        {
            Box(root, "Route Outer Wall", new Vector3(3f, 1.5f, z + Mathf.Sign(z) * 2f), new Vector3(52f, 3f, 0.3f), wall);
            Box(root, "Route Inner Wall", new Vector3(3f, 1.5f, z - Mathf.Sign(z) * 2f), new Vector3(44f, 3f, 0.3f), wall);
            Box(root, "Route West End", new Vector3(-23f, 1.5f, z), new Vector3(0.3f, 3f, 4f), wall);
            Box(root, "Route East End", new Vector3(29f, 1.5f, z), new Vector3(0.3f, 3f, 4f), wall);
        }

        private static void HunterGate(Transform root, string name, Vector3 position, int markerId, int roomId,
            Material material, int layer)
        {
            var gate = Box(root, name, position + Vector3.up * 1.5f, new Vector3(4f, 3f, 0.25f), material);
            gate.layer = layer;
            gate.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            var cut = Group(root, name + " Navigation Cut");
            cut.transform.position = position;
            var volume = cut.AddComponent<NavMeshModifierVolume>();
            volume.center = Vector3.zero;
            volume.size = new Vector3(4f, 2f, 0.6f);
            volume.area = 1;
            var link = Mark(root, name + " HunterLink", markerId, LevelMarkerKind.HunterLink, roomId,
                position + Vector3.forward, roomId, true, TraversalAccess.Hunter, position - Vector3.forward);
            SetVector(link, "_size", new Vector3(2f, 1f, 1f));
        }

        private static void BuildGraphMarkers(Transform root)
        {
            var low = Mark(root, "Room 1 Low Connector", 1, LevelMarkerKind.Room, 1, new Vector3(-18f, 1.5f, 0f));
            SetVector(low, "_size", new Vector3(12f, 3f, 8f));
            var mid = Mark(root, "Room 2 Mid Braided", 2, LevelMarkerKind.Room, 2, new Vector3(0f, 3f, 0f));
            SetVector(mid, "_size", new Vector3(24f, 6f, 20f));
            var tall = Mark(root, "Room 3 Tall Atrium", 3, LevelMarkerKind.Room, 3, new Vector3(21f, 5f, 0f));
            SetVector(tall, "_size", new Vector3(18f, 10f, 20f));
            Mark(root, "Shared Connector", 101, LevelMarkerKind.RoomLink, 1, new Vector3(-12f, 0f, 0f), 2);
            Mark(root, "Lower Braid", 102, LevelMarkerKind.RoomLink, 2, new Vector3(12f, 0f, -4f), 3);
            Mark(root, "Upper Braid", 103, LevelMarkerKind.RoomLink, 2, new Vector3(12f, 0f, 4f), 3);
            Mark(root, "Hunter Corridor Route", 104, LevelMarkerKind.RoomLink, 1, new Vector3(3f, 0f, -15f), 3, true, TraversalAccess.Hunter);
            Mark(root, "Player Clutter Route", 105, LevelMarkerKind.RoomLink, 1, new Vector3(3f, 0f, 15f), 3, true, TraversalAccess.Player);
            Anchor(root, 301, 1, CakeAnchorType.Flow, new Vector3(-16f, 0.1f, 0f));
            Anchor(root, 302, 2, CakeAnchorType.Precision, new Vector3(-3f, 0.1f, 15f));
            Anchor(root, 303, 1, CakeAnchorType.Detour, new Vector3(-18f, 0.1f, 15f));
            Anchor(root, 304, 2, CakeAnchorType.Risk, new Vector3(8f, 0.1f, -6f));
            Anchor(root, 305, 3, CakeAnchorType.Vertical, new Vector3(26f, 4.1f, 4f));
            Mark(root, "Exit Marker", 401, LevelMarkerKind.ExitMarker, 3, new Vector3(28f, 0f, 0f));
        }

        private static void Anchor(Transform root, int id, int room, CakeAnchorType type, Vector3 position)
        {
            var marker = Mark(root, type + " Cake Anchor", id, LevelMarkerKind.CakeAnchor, room, position);
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("_anchorType").enumValueIndex = (int)type;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static LevelMarker Mark(Transform root, string name, int id, LevelMarkerKind kind, int room,
            Vector3 position, int targetRoom = 0, bool bidirectional = true,
            TraversalAccess access = TraversalAccess.All, Vector3 target = default)
        {
            var marker = Group(root, name);
            marker.transform.position = position;
            return Marker(marker, id, kind, room, targetRoom, bidirectional, access, target);
        }

        private static LevelMarker Marker(GameObject targetObject, int id, LevelMarkerKind kind, int room,
            int targetRoom = 0, bool bidirectional = true, TraversalAccess access = TraversalAccess.All,
            Vector3 target = default, Vector3? oppositeTarget = null)
        {
            var marker = targetObject.AddComponent<LevelMarker>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("_id").intValue = id;
            serialized.FindProperty("_kind").enumValueIndex = (int)kind;
            serialized.FindProperty("_roomId").intValue = room;
            serialized.FindProperty("_targetRoomId").intValue = targetRoom;
            serialized.FindProperty("_bidirectional").boolValue = bidirectional;
            serialized.FindProperty("_access").intValue = (int)access;
            serialized.FindProperty("_size").vector3Value = targetObject.transform.lossyScale;
            serialized.FindProperty("_targetPosition").vector3Value = target;
            serialized.FindProperty("_hasEndpointPair").boolValue = oppositeTarget.HasValue;
            serialized.FindProperty("_oppositeTargetPosition").vector3Value = oppositeTarget.GetValueOrDefault();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }

        private static GameObject Group(Transform parent, string name)
        {
            var result = new GameObject(name);
            result.transform.SetParent(parent, false);
            return result;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size,
            Material material, bool collision = true)
        {
            var result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.position = position;
            result.transform.localScale = size;
            result.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(result.GetComponent<Collider>());
            return result;
        }

        private static Material Material(string name, Color color, bool emissive = false)
        {
            EnsureFolder(MaterialPath.TrimEnd('/'));
            var path = MaterialPath + name + ".mat";
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

        private static void SetVector(UnityEngine.Object target, string field, Vector3 value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
                throw new InvalidOperationException("Stop Play Mode before generating Level content.");
        }
    }
}
