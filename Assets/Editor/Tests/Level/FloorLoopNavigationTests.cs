// ============================================================================
// FloorLoopNavigationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the saved FloorLoop scene's actual Level wiring and baked paths for
//   cake cues, the exit and Hunter room targets without rebuilding any content.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Level scene integration.
// KEY RESPONSIBILITIES:
//   - Read real marker/config references and query the saved default-agent bake.
//   - Isolate navigation queries from other loaded scenes and clean up owned state.
// DEPENDENCIES:
//   - Level/Floor/Hunter contracts, FloorLoop scene root, UnityEditor and Unity AI.
//   - NavMeshSurface serialized fields are inspected without changing assemblies.
// USAGE NOTES:
//   Coordinator runs under the Unity lease. Opens FloorLoop additively only when
//   absent; an already-dirty FloorLoop is skipped rather than saved or discarded.
//   A temporary translated instance of the same saved NavMeshData prevents foreign
//   surfaces from completing a missing path. Only that instance is removed.
//   No scene, asset, configuration or GUID is modified. This proves baked paths,
//   not capsule movement, dynamic carving, warning timing or first-sweep duration.
// ============================================================================

using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Editor.Level;
using Worsen.Orchestrator;

namespace Worsen.Tests.Level
{
    public sealed class FloorLoopNavigationTests
    {
        private const string ScenePath = "Assets/Scenes/FloorLoop.unity";

        [Test]
        public void SavedFloorLoopHasCompleteAnchorAndExitPathsFromBothWiredSpawns()
        {
            using (var scope = new SceneNavigationScope())
            {
                Assert.That(scope.RequiredCakeCount, Is.EqualTo(10));
                Assert.That(scope.Graph.Anchors.Count, Is.GreaterThanOrEqualTo(14));
                Assert.That(Vector3.Distance(scope.PlayerSpawn, scope.HunterSpawn), Is.GreaterThan(14f));
                Assert.That(scope.Graph.Rooms.Count(room => room.Bounds.Contains(scope.PlayerSpawn)), Is.EqualTo(1));
                Assert.That(scope.Graph.Rooms.Count(room => room.Bounds.Contains(scope.HunterSpawn)), Is.EqualTo(1));
                foreach (var anchor in scope.Graph.Anchors)
                {
                    Assert.That(scope.Graph.Rooms.Single(room => room.Id == anchor.RoomId).Bounds.Contains(anchor.Position), Is.True,
                        "Anchor " + anchor.Id + " is outside its collapse room.");
                    scope.AssertCompletePath(scope.PlayerSpawn, anchor.Position, scope.CueSampleRadius, "Player to anchor " + anchor.Id, true);
                    scope.AssertCompletePath(scope.HunterSpawn, anchor.Position, scope.HunterSampleRadius, "Hunter to anchor " + anchor.Id, true);
                }
                Assert.That(scope.Graph.Rooms.Single(room => room.Id == scope.Graph.ExitRoomId).Bounds.Contains(scope.Graph.ExitPosition), Is.True);
                scope.AssertCompletePath(scope.PlayerSpawn, scope.Graph.ExitPosition, scope.CueSampleRadius, "Player to exit", true);
                scope.AssertCompletePath(scope.HunterSpawn, scope.Graph.ExitPosition, scope.HunterSampleRadius, "Hunter to exit", true);
                TestContext.WriteLine("Verified " + ((scope.Graph.Anchors.Count + 1) * 2) +
                    " complete baked paths: both wired spawns to every anchor and the exit.");
            }
        }

        [Test]
        public void SavedHunterProfileProjectsEveryActualBoundsBottomRoomTarget()
        {
            using (var scope = new SceneNavigationScope())
            {
                Assert.That(scope.HunterSampleRadius, Is.EqualTo(4f).Within(0.0001f),
                    "FloorLoop requires its wired Hunter motor projection radius, not the generic 2m default.");
                foreach (var room in scope.Graph.Rooms)
                {
                    // Matches HunterController.RoomCenter; do not substitute a floor-height helper.
                    var patrolTarget = new Vector3(room.Center.x, room.Bounds.min.y, room.Center.z);
                    var projected = scope.AssertCompletePath(scope.HunterSpawn, patrolTarget, scope.HunterSampleRadius,
                        "Hunter patrol/cutoff room " + room.Id, false);
                    Assert.That(room.Bounds.Contains(projected), Is.True, "Projection selected a different room.");
                    TestContext.WriteLine("Room " + room.Id + " target " + patrolTarget + " projected to " + projected);
                }
            }
        }

        private sealed class SceneNavigationScope : IDisposable
        {
            private readonly Scene[] _previousScenes;
            private readonly bool[] _previousDirty;
            private readonly bool[] _previousLoaded;
            private readonly Scene _previousActive;
            private Scene _scene;
            private bool _openedScene;
            private NavMeshDataInstance _instance;
            private Vector3 _offset;
            private readonly NavMeshQueryFilter _filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            public LevelGraph Graph { get; private set; }
            public Vector3 PlayerSpawn { get; private set; }
            public Vector3 HunterSpawn { get; private set; }
            public float CueSampleRadius { get; private set; }
            public float HunterSampleRadius { get; private set; }
            public int RequiredCakeCount { get; private set; }

            public SceneNavigationScope()
            {
                Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False, "Run this fixture in Edit Mode.");
                _previousScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
                _previousDirty = _previousScenes.Select(scene => scene.isDirty).ToArray();
                _previousLoaded = _previousScenes.Select(scene => scene.isLoaded).ToArray();
                _previousActive = SceneManager.GetActiveScene();
                try
                {
                    _scene = SceneManager.GetSceneByPath(ScenePath);
                    if (_scene.IsValid() && _scene.isLoaded && _scene.isDirty)
                        Assert.Ignore("FloorLoop has unsaved edits; the navigation fixture preserves them.");
                    if (!_scene.IsValid() || !_scene.isLoaded)
                    {
                        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Build FloorLoop before running this fixture.");
                        _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                        _openedScene = true;
                    }
                    Assert.That(_scene.isLoaded, Is.True);
                    var roots = _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FloorLoopSceneRoot>(true)).ToArray();
                    Assert.That(roots.Length, Is.EqualTo(1));
                    var sceneRoot = roots[0];
                    Assert.That(sceneRoot.isActiveAndEnabled, Is.True);
                    var rootFields = new SerializedObject(sceneRoot);
                    PlayerSpawn = Property(rootFields, "_spawnPosition").vector3Value;
                    HunterSpawn = Property(rootFields, "_hunterSpawnPosition").vector3Value;
                    var level = Reference<LevelManager>(rootFields, "_level");
                    Assert.That(level.gameObject.scene, Is.EqualTo(_scene));
                    Assert.That(level.isActiveAndEnabled, Is.True);
                    Assert.That(level.transform.position, Is.EqualTo(Vector3.zero));
                    Assert.That(level.transform.rotation, Is.EqualTo(Quaternion.identity));
                    Assert.That(level.transform.lossyScale, Is.EqualTo(Vector3.one));
                    var driver = Reference<LevelDriver>(new SerializedObject(level), "_driver");
                    Assert.That(driver, Is.SameAs(level.GetComponent<LevelDriver>()));
                    Assert.That(Reference<LevelMarkerRegistry>(new SerializedObject(level), "_registry"), Is.SameAs(level.GetComponent<LevelMarkerRegistry>()));
                    var markerArray = Property(new SerializedObject(driver), "_markers");
                    var markers = Enumerable.Range(0, markerArray.arraySize)
                        .Select(index => markerArray.GetArrayElementAtIndex(index).objectReferenceValue as LevelMarker).ToArray();
                    Assert.That(markers, Is.All.Not.Null);
                    CollectionAssert.AreEquivalent(level.GetComponentsInChildren<LevelMarker>(true), markers,
                        "Serialized registration must include every authored marker exactly once.");
                    Assert.That(markers.All(marker => marker.isActiveAndEnabled), Is.True);
                    var state = new LevelBehaviorState();
                    new LevelController(state).Rebuild(markers.Select(marker => marker.Capture()));
                    Assert.That(state.IsReady, Is.True);
                    Graph = state.Graph;

                    var floor = Reference<FloorManager>(rootFields, "_floor");
                    var floorDriver = Reference<FloorDriver>(new SerializedObject(floor), "_driver");
                    CueSampleRadius = Reference<FloorDriverConfig>(new SerializedObject(floorDriver), "_config").PathSampleRadius;
                    RequiredCakeCount = Reference<FloorConfig>(rootFields, "_floorConfig").RequiredCakeCount;
                    var hunterProfile = Reference<HunterProfile>(rootFields, "_hunterProfile");
                    Assert.That(hunterProfile.Prefab, Is.Not.Null);
                    var hunterDriver = hunterProfile.Prefab.GetComponent<HunterDriver>();
                    Assert.That(hunterDriver, Is.Not.Null);
                    HunterSampleRadius = Reference<HunterMotorDriverConfig>(new SerializedObject(hunterDriver), "_config").PathSampleRadius;

                    var surfaces = level.GetComponents<Component>().Where(component => component != null &&
                        component.GetType().FullName == "Unity.AI.Navigation.NavMeshSurface").ToArray();
                    Assert.That(surfaces.Length, Is.EqualTo(1));
                    var surfaceFields = new SerializedObject(surfaces[0]);
                    Assert.That(Property(surfaceFields, "m_AgentTypeID").intValue, Is.Zero);
                    Assert.That(Property(surfaceFields, "m_GenerateLinks").boolValue, Is.False);
                    var data = Reference<NavMeshData>(surfaceFields, "m_NavMeshData");
                    Assert.That(AssetDatabase.GetAssetPath(data), Is.EqualTo(FloorLoopLevelSetup.NavigationAssetPath));
                    var guid = AssetDatabase.AssetPathToGUID(FloorLoopLevelSetup.NavigationAssetPath);
                    Assert.That(guid, Is.Not.Empty);
                    Assert.That(AssetDatabase.LoadAssetAtPath<NavMeshData>(AssetDatabase.GUIDToAssetPath(guid)), Is.SameAs(data));
                    // Add an isolated instance beyond every existing navigation vertex.
                    // Do not remove/disable another scene's surface or move scene objects.
                    var vertices = NavMesh.CalculateTriangulation().vertices;
                    var maxX = vertices.Length == 0 ? 0f : vertices.Max(vertex => vertex.x);
                    _offset = new Vector3(Mathf.Ceil((maxX + 4096f) / 1024f) * 1024f, 0f, 0f);
                    _instance = NavMesh.AddNavMeshData(data, _offset, Quaternion.identity);
                    Assert.That(_instance.valid, Is.True);
                    TestContext.WriteLine("Saved FloorLoop navigation GUID " + guid + "; isolated query translation " + _offset);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public Vector3 AssertCompletePath(Vector3 source, Vector3 target, float radius, string context, bool nearFloorTarget)
            {
                Assert.That(NavMesh.SamplePosition(source + _offset, out var start, radius, _filter), Is.True, context + ": source projection failed.");
                Assert.That(NavMesh.SamplePosition(target + _offset, out var end, radius, _filter), Is.True, context + ": target projection failed.");
                Assert.That(Vector3.Distance(start.position, source + _offset), Is.LessThan(0.5f), context + ": spawn is not on the bake.");
                if (nearFloorTarget)
                    Assert.That(Vector3.Distance(end.position, target + _offset), Is.LessThan(0.5f), context + ": anchor/exit is not on the bake.");
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(start.position, end.position, _filter, path), Is.True, context);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), context);
                Assert.That(path.corners.Length, Is.GreaterThanOrEqualTo(2), context);
                foreach (var corner in path.corners)
                    Assert.That(Graph.Rooms.Any(room => room.Bounds.Contains(corner - _offset)), Is.True,
                        context + ": navigation corner escapes collapse coverage at " + (corner - _offset));
                return end.position - _offset;
            }

            public void Dispose()
            {
                if (_instance.valid) _instance.Remove();
                if (_previousActive.IsValid() && _previousActive.isLoaded) SceneManager.SetActiveScene(_previousActive);
                if (_openedScene && _scene.IsValid() && _scene.isLoaded)
                {
                    Assert.That(EditorSceneManager.CloseScene(_scene, true), Is.True, "Could not close the fixture-owned additive scene.");
                    _openedScene = false;
                }
                for (var index = 0; index < _previousScenes.Length; index++)
                {
                    Assert.That(_previousScenes[index].isLoaded, Is.EqualTo(_previousLoaded[index]), "A pre-existing scene's loaded state changed.");
                    Assert.That(_previousScenes[index].isDirty, Is.EqualTo(_previousDirty[index]), "A pre-existing scene's dirty state changed.");
                }
            }

            private static SerializedProperty Property(SerializedObject fields, string name)
            {
                var property = fields.FindProperty(name);
                Assert.That(property, Is.Not.Null, fields.targetObject.GetType().Name + "." + name + " is missing.");
                return property;
            }

            private static T Reference<T>(SerializedObject fields, string name) where T : UnityEngine.Object
            {
                var value = Property(fields, name).objectReferenceValue as T;
                Assert.That(value, Is.Not.Null, fields.targetObject.GetType().Name + "." + name + " is not wired.");
                return value;
            }
        }
    }
}
