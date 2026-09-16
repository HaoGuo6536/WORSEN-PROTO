// ============================================================================
// HunterRouteIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Checks actual Hunter and Player capsules against TagArena's two authored flat
//   Hunter gate cuts, using the saved bake, geometry and navigation links.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Chase route integration.
// KEY RESPONSIBILITIES:
//   - Verify both directions of each real flat cut have a short complete link path.
//   - Check width-sized portal edges and keep the full capsule inside the gate span.
//   - Require actual Hunter motor crossing and Player motor collision at that gate.
//   - Remove an owned active link mid-crossing and require invalidation and recovery.
//   - Remove a pending link while attached to its entry bank and require a real detour.
//   - Isolate temporary actors/navigation and preserve existing scenes and assets.
// DEPENDENCIES:
//   - Core, Level, Hunter, Player, TagArenaSceneRoot and TagArenaLevelSetup.
//   - UnityEditor scene/serialized inspection; Unity physics and NavMesh queries.
// USAGE NOTES:
//   Coordinator runs under the exclusive Unity lease. Edit Mode only: a temporary
//   translated copy of the saved generated Level supplies its original colliders,
//   NavMeshData and NavMeshLink components beyond all existing physics/navigation.
//   Temporary Driver-only actors receive direct motor commands at 1/60 second with
//   unchanged wired default configs/profile values. No authored scene object, config, marker
//   or navigation asset is edited/saved. The framework's existing untitled scene
//   hosts one owned root in the default physics world. Cleanup removes only owned
//   objects and preserves its original roots; that scene is never saved or closed.
//   Setup failures log their original exception before cleanup and label state checks.
//   Bounded read-only motor state snapshots diagnose path progress without changing it.
//   This tests motor/path compatibility, not normal Session scheduling, factories,
//   sensing, chase statistics, route preference, human play or general off-mesh jumps.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Editor.Level;
using Worsen.Orchestrator;

namespace Worsen.Tests.Chase
{
    public sealed class HunterRouteIntegrationTests
    {
        private const string ScenePath = "Assets/Scenes/TagArena.unity";
        private const float Step = 1f / 60f;

        [TestCase(107, false)]
        [TestCase(107, true)]
        [TestCase(108, false)]
        [TestCase(108, true)]
        public void AuthoredFlatHunterLinkPassesHunterAndStopsPlayer(int markerId, bool reverse)
        {
            using (var scope = new RouteScope(markerId))
            {
                Vector3 linkStart = reverse ? scope.LinkEnd : scope.LinkStart;
                Vector3 linkEnd = reverse ? scope.LinkStart : scope.LinkEnd;
                Vector3 direction = (linkEnd - linkStart).normalized;
                Vector3 start = linkStart - direction + Vector3.up * 0.02f;
                Vector3 goal = linkEnd + direction + Vector3.up * 0.02f;
                Assert.That(Mathf.Abs(direction.y), Is.LessThan(0.0001f), "This fixture covers flat authored links only.");
                scope.AssertCutAndCompleteLinkPath(start, goal, linkStart, linkEnd);
                scope.AssertHunterCrosses(start, goal, direction);
                scope.AssertPlayerStops(start, direction);
                scope.AssertAssetsUnchanged();
                TestContext.WriteLine("Authored gate " + markerId + (reverse ? " reverse" : " forward") +
                    ": translated saved geometry/bake, direct 60 Hz motor commands; Hunter crossed and Player stopped. " +
                    "No Session/chase/human acceptance is inferred.");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovingActiveWestLinkRejectsFarBankSnapUntilLinkReturns(bool reverse)
        {
            using (var scope = new RouteScope(107))
            {
                Vector3 entry = reverse ? scope.LinkEnd : scope.LinkStart;
                Vector3 exit = reverse ? scope.LinkStart : scope.LinkEnd;
                Vector3 direction = (exit - entry).normalized;
                Vector3 start = entry - direction + Vector3.up * 0.02f;
                Vector3 goal = exit + direction + Vector3.up * 0.02f;
                scope.AssertCutAndCompleteLinkPath(start, goal, entry, exit);
                scope.AssertActiveLinkInvalidatesAndRecovers(start, goal, entry, exit, direction);
                scope.AssertAssetsUnchanged();
            }
        }

        [Test]
        public void RemovingEastLinkWhileStillOnEntryBankUsesAvailableDetour()
        {
            using (var scope = new RouteScope(108))
            {
                Vector3 direction = (scope.LinkEnd - scope.LinkStart).normalized;
                Vector3 start = scope.LinkStart - direction + Vector3.up * 0.02f;
                Vector3 goal = scope.LinkEnd + direction + Vector3.up * 0.02f;
                scope.AssertCutAndCompleteLinkPath(start, goal, scope.LinkStart, scope.LinkEnd);
                scope.AssertNearBankLinkRemovalUsesDetour(start, goal, direction);
                scope.AssertAssetsUnchanged();
            }
        }

        private sealed class RouteScope : IDisposable
        {
            private readonly Scene[] previousScenes;
            private readonly bool[] previousDirty, previousLoaded;
            private readonly Scene previousActive;
            private readonly GameObject[] previousFrameworkRoots;
            private Scene sourceScene;
            private bool openedSource;
            private GameObject temporaryRoot, copy;
            private readonly List<GameObject> actors = new List<GameObject>();
            private readonly Dictionary<UnityEngine.Object, string> snapshots = new Dictionary<UnityEngine.Object, string>();
            private readonly Dictionary<string, Hash128> assetHashes = new Dictionary<string, Hash128>();
            private HunterProfile hunterProfile;
            private PlayerProfile playerProfile;
            private HunterMotorDriverConfig hunterConfig;
            private PlayerMoverDriverConfig playerConfig;
            private BoxCollider gate;
            private Behaviour ownedLink;
            private Vector3 offset, portalAcross, gateAcross;
            private float portalWidth, maximumGateLateral;
            public Vector3 LinkStart { get; private set; }
            public Vector3 LinkEnd { get; private set; }

            public RouteScope(int markerId)
            {
                Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False,
                    "Edit Mode admission: isPlaying=" + EditorApplication.isPlaying +
                    "; isPlayingOrWillChangePlaymode=" + EditorApplication.isPlayingOrWillChangePlaymode);
                previousScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
                previousDirty = previousScenes.Select(scene => scene.isDirty).ToArray();
                previousLoaded = previousScenes.Select(scene => scene.isLoaded).ToArray();
                previousActive = SceneManager.GetActiveScene();
                Assert.That(previousActive.IsValid() && previousActive.isLoaded, Is.True,
                    "Framework scene must be valid and loaded: " + SceneDetails(previousActive));
                Assert.That(previousActive.path, Is.Empty, "Run inside the Test Framework's untitled scene.");
                Assert.That(previousActive.GetPhysicsScene(), Is.EqualTo(Physics.defaultPhysicsScene),
                    "Direct Physics queries must see the owned actors and saved geometry copy.");
                previousFrameworkRoots = previousActive.GetRootGameObjects();
                try
                {
                    sourceScene = SceneManager.GetSceneByPath(ScenePath);
                    if (sourceScene.IsValid() && sourceScene.isLoaded)
                        Assert.That(sourceScene.isDirty, Is.False,
                            "Source TagArena must have no unsaved edits: " + SceneDetails(sourceScene));
                    else
                    {
                        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Build TagArena first.");
                        sourceScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                        openedSource = true;
                    }
                    var root = sourceScene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<TagArenaSceneRoot>(true)).Single();
                    var rootFields = new SerializedObject(root);
                    var level = Reference<LevelManager>(rootFields, "_level");
                    Assert.That(level.gameObject.scene, Is.EqualTo(sourceScene));
                    Assert.That(level.transform.position, Is.EqualTo(Vector3.zero));
                    Assert.That(level.transform.rotation, Is.EqualTo(Quaternion.identity));
                    Assert.That(level.transform.lossyScale, Is.EqualTo(Vector3.one));
                    hunterProfile = Reference<HunterProfile>(rootFields, "_hunterProfile");
                    playerProfile = Reference<PlayerProfile>(rootFields, "_playerProfile");
                    hunterConfig = Reference<HunterMotorDriverConfig>(new SerializedObject(hunterProfile.Prefab.GetComponent<HunterDriver>()), "_config");
                    playerConfig = Reference<PlayerMoverDriverConfig>(new SerializedObject(playerProfile.Prefab.GetComponent<PlayerDriver>()), "_config");
                    Assert.That(hunterProfile.PatrolSpeed, Is.EqualTo(3f));
                    Assert.That(hunterProfile.Acceleration, Is.EqualTo(20f));
                    Assert.That(hunterProfile.TurnRate, Is.EqualTo(240f));
                    Assert.That(hunterConfig.Radius, Is.EqualTo(0.4f));
                    Assert.That(hunterConfig.Height, Is.EqualTo(1.8f));
                    Assert.That(playerProfile.SprintSpeed, Is.EqualTo(8f));
                    Assert.That(playerConfig.Height, Is.EqualTo(1.8f));
                    Remember(hunterProfile); Remember(playerProfile); Remember(hunterConfig); Remember(playerConfig);
                    RememberAsset(ScenePath);
                    var surface = ComponentNamed(level.gameObject, "Unity.AI.Navigation.NavMeshSurface");
                    var data = Reference<NavMeshData>(new SerializedObject(surface), "m_NavMeshData");
                    Assert.That(AssetDatabase.GetAssetPath(data), Is.EqualTo(TagArenaLevelSetup.NavigationAssetPath));
                    RememberAsset(TagArenaLevelSetup.NavigationAssetPath);
                    var sourceMarker = level.GetComponentsInChildren<LevelMarker>(true).Single(item => item.SurfaceId == markerId);
                    LevelMarkerRecord record = sourceMarker.Capture();
                    Assert.That(record.Kind, Is.EqualTo(LevelMarkerKind.HunterLink));
                    Assert.That(record.Access, Is.EqualTo(TraversalAccess.Hunter));
                    Assert.That(record.Bidirectional, Is.True,
                        "Source marker must be bidirectional: " + ComponentDetails(sourceMarker) +
                        "; markerId=" + markerId + "; bidirectional=" + record.Bidirectional);
                    var sourceLink = ComponentNamed(sourceMarker.gameObject, "Unity.AI.Navigation.NavMeshLink");
                    var sourceFields = new SerializedObject(sourceLink);
                    Assert.That(Property(sourceFields, "m_Activated").boolValue, Is.True,
                        "Source NavMeshLink m_Activated must be true: " + ComponentDetails(sourceLink));
                    Assert.That(Property(sourceFields, "m_Bidirectional").boolValue, Is.True,
                        "Source NavMeshLink m_Bidirectional must be true: " + ComponentDetails(sourceLink));
                    Assert.That(Property(sourceFields, "m_Width").floatValue, Is.EqualTo(2f));
                    Assert.That(Property(sourceFields, "m_AgentTypeID").intValue, Is.Zero);
                    Vector3 sourceStart = sourceLink.transform.TransformPoint(Property(sourceFields, "m_StartPoint").vector3Value);
                    Vector3 sourceEnd = sourceLink.transform.TransformPoint(Property(sourceFields, "m_EndPoint").vector3Value);
                    Assert.That(sourceStart, Is.EqualTo(sourceMarker.transform.position));
                    Assert.That(sourceEnd, Is.EqualTo(sourceMarker.Target));
                    Assert.That(Vector3.Distance(sourceStart, sourceEnd), Is.EqualTo(2f).Within(0.001f));
                    string gateName = markerId == 107 ? "Hunter West Gate" : "Hunter East Gate";
                    var sourceGate = sourceMarker.transform.parent.Find(gateName).GetComponent<BoxCollider>();
                    Assert.That(sourceGate.enabled && !sourceGate.isTrigger, Is.True,
                        "Source gate must be enabled and solid: " + ComponentDetails(sourceGate));
                    int gateLayer = LayerMask.NameToLayer("HunterRouteGate");
                    Assert.That(gateLayer, Is.GreaterThanOrEqualTo(0));
                    Assert.That(sourceGate.gameObject.layer, Is.EqualTo(gateLayer));
                    Assert.That(playerConfig.CollisionMask & (1 << gateLayer), Is.Not.Zero);
                    Assert.That(sourceGate.bounds.size, Is.EqualTo(new Vector3(4f, 3f, 0.25f)));

                    float maxX = NavMesh.CalculateTriangulation().vertices.Select(point => point.x).DefaultIfEmpty(0f).Max();
                    foreach (Collider collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                        maxX = Mathf.Max(maxX, collider.bounds.max.x);
                    offset = new Vector3(Mathf.Ceil((maxX + 4096f) / 1024f) * 1024f, 0f, 0f);
                    // Reuse the framework scene: do not create, save or close a scene
                    // to work around its intentionally untitled bootstrap scene.
                    if (SceneManager.GetActiveScene() != previousActive)
                        SceneManager.SetActiveScene(previousActive);
                    Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousActive),
                        "Framework scene must be active before creating owned objects: requested=" + SceneDetails(previousActive) +
                        "; actual=" + SceneDetails(SceneManager.GetActiveScene()));
                    temporaryRoot = new GameObject("Temporary Hunter route scope " + markerId);
                    Assert.That(temporaryRoot.scene, Is.EqualTo(previousActive));
                    copy = UnityEngine.Object.Instantiate(level.gameObject, offset, Quaternion.identity);
                    copy.name = "Temporary saved TagArena route copy";
                    copy.SetActive(false);
                    copy.transform.SetParent(temporaryRoot.transform, true);
                    copy.SetActive(true);
                    Physics.SyncTransforms();
                    var copiedSurface = ComponentNamed(copy, "Unity.AI.Navigation.NavMeshSurface");
                    Assert.That(((Behaviour)copiedSurface).isActiveAndEnabled, Is.True,
                        "Cloned NavMeshSurface must be active and enabled: " + ComponentDetails(copiedSurface) +
                        "; source=" + ComponentDetails(surface));
                    Assert.That(Reference<NavMeshData>(new SerializedObject(copiedSurface), "m_NavMeshData"), Is.SameAs(data));
                    var copiedMarkers = copy.GetComponentsInChildren<LevelMarker>(true)
                        .Where(item => item.MarkerKind == LevelMarkerKind.HunterLink).ToArray();
                    Assert.That(copiedMarkers.Length, Is.EqualTo(2));
                    var marker = copiedMarkers.Single(item => item.SurfaceId == markerId);
                    var link = ComponentNamed(marker.gameObject, "Unity.AI.Navigation.NavMeshLink");
                    ownedLink = (Behaviour)link;
                    Assert.That(((Behaviour)link).isActiveAndEnabled, Is.True,
                        "Cloned NavMeshLink must be active and enabled: " + ComponentDetails(link) +
                        "; source=" + ComponentDetails(sourceLink));
                    var linkFields = new SerializedObject(link);
                    Assert.That(Property(linkFields, "m_Activated").boolValue, Is.True,
                        "Cloned NavMeshLink m_Activated must be true: " + ComponentDetails(link));
                    LinkStart = link.transform.TransformPoint(Property(linkFields, "m_StartPoint").vector3Value);
                    LinkEnd = link.transform.TransformPoint(Property(linkFields, "m_EndPoint").vector3Value);
                    portalWidth = Property(linkFields, "m_Width").floatValue;
                    Assert.That(portalWidth, Is.EqualTo(Property(sourceFields, "m_Width").floatValue));
                    // NavMeshLink endpoints are the middles of width-sized edge segments,
                    // perpendicular to start -> end in the component's local XZ plane.
                    portalAcross = Vector3.Cross(link.transform.up, LinkEnd - LinkStart).normalized;
                    Assert.That(Vector3.Distance(LinkStart, sourceStart + offset), Is.LessThan(0.001f));
                    Assert.That(Vector3.Distance(LinkEnd, sourceEnd + offset), Is.LessThan(0.001f));
                    gate = marker.transform.parent.Find(gateName).GetComponent<BoxCollider>();
                    Assert.That(gate.enabled && !gate.isTrigger, Is.True,
                        "Cloned gate must be enabled and solid: " + ComponentDetails(gate));
                    Assert.That(gate.gameObject.layer, Is.EqualTo(gateLayer));
                    Assert.That(Vector3.Distance(gate.bounds.center, sourceGate.bounds.center + offset), Is.LessThan(0.001f));
                    Assert.That(gate.bounds.size, Is.EqualTo(sourceGate.bounds.size));
                    gateAcross = gate.transform.right;
                    Assert.That(Mathf.Abs(Vector3.Dot(gateAcross, portalAcross)), Is.GreaterThan(0.9999f),
                        "The portal width axis must align with the physical gate width axis.");
                    float gateHalfWidth = gate.size.x * Mathf.Abs(gate.transform.lossyScale.x) * 0.5f;
                    maximumGateLateral = gateHalfWidth - hunterConfig.Radius;
                    Assert.That(maximumGateLateral, Is.GreaterThan(0f));
                    Assert.That(GateLateral(LinkStart) + portalWidth * 0.5f + hunterConfig.Radius, Is.LessThan(gateHalfWidth),
                        "The complete start portal and Hunter capsule must fit inside the physical gate width.");
                    Assert.That(GateLateral(LinkEnd) + portalWidth * 0.5f + hunterConfig.Radius, Is.LessThan(gateHalfWidth),
                        "The complete end portal and Hunter capsule must fit inside the physical gate width.");
                    TestContext.WriteLine("Saved gate " + markerId + " " + sourceStart + " -> " + sourceEnd +
                        "; NavMesh GUID=" + AssetDatabase.AssetPathToGUID(TagArenaLevelSetup.NavigationAssetPath) + "; translation=" + offset +
                        "; portal width=" + portalWidth + "; gate half-width=" + gateHalfWidth +
                        "; capsule radius=" + hunterConfig.Radius + "; full-capsule center lateral bound=" + maximumGateLateral);
                }
                catch (Exception setupError)
                {
                    TestContext.WriteLine("RouteScope setup failed for marker " + markerId +
                        " before cleanup. Original exception:\n" + setupError);
                    try { Dispose(); }
                    catch (Exception cleanupError)
                    {
                        TestContext.WriteLine("RouteScope cleanup also failed:\n" + cleanupError);
                        throw new AggregateException("Route setup and cleanup both failed; original details are retained.",
                            setupError, cleanupError);
                    }
                    throw;
                }
            }

            private static string SceneDetails(Scene scene) => "handle=" + scene.handle + "; name='" + scene.name + "'; path='" + scene.path +
                "'; valid=" + scene.IsValid() + "; loaded=" + scene.isLoaded + "; dirty=" + scene.isDirty;

            private static string ComponentDetails(Component component)
            {
                GameObject owner = component.gameObject;
                string state = component.GetType().Name + " '" + owner.name + "'; activeSelf=" + owner.activeSelf +
                    "; activeInHierarchy=" + owner.activeInHierarchy + "; scene={" + SceneDetails(owner.scene) + "}";
                if (component is Behaviour behaviour)
                    state += "; enabled=" + behaviour.enabled + "; isActiveAndEnabled=" + behaviour.isActiveAndEnabled;
                if (component is Collider collider)
                    state += "; enabled=" + collider.enabled + "; isTrigger=" + collider.isTrigger;
                return state;
            }

            public void AssertCutAndCompleteLinkPath(Vector3 start, Vector3 goal, Vector3 linkStart, Vector3 linkEnd)
            {
                Assert.That(NavMesh.SamplePosition(start, out NavMeshHit from, hunterConfig.PathSampleRadius, NavMesh.AllAreas), Is.True);
                Assert.That(NavMesh.SamplePosition(goal, out NavMeshHit to, hunterConfig.PathSampleRadius, NavMesh.AllAreas), Is.True);
                Assert.That(Vector3.Distance(from.position, start), Is.LessThan(0.2f));
                Assert.That(Vector3.Distance(to.position, goal), Is.LessThan(0.2f));
                Assert.That(NavMesh.Raycast(from.position, to.position, out NavMeshHit cut, NavMesh.AllAreas), Is.True,
                    "The actual baked cut must interrupt the ordinary straight navigation ray.");
                Assert.That(Mathf.Abs(cut.position.z - gate.bounds.center.z), Is.LessThan(1.5f), "The ray hit a different navigation obstacle.");
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path), Is.True);
                TestContext.WriteLine("Initial path before endpoint assertions: status=" + path.status +
                    "; length=" + Length(path.corners) + "; sampled start=" + (from.position - offset).ToString("F4") +
                    "; sampled goal=" + (to.position - offset).ToString("F4") + "; cut=" + (cut.position - offset).ToString("F4") +
                    "; link start=" + (linkStart - offset).ToString("F4") + "; link end=" + (linkEnd - offset).ToString("F4") +
                    "; nearest start corner=" + path.corners.Select(point => Vector3.Distance(point, linkStart)).DefaultIfEmpty(float.PositiveInfinity).Min() +
                    "; nearest end corner=" + path.corners.Select(point => Vector3.Distance(point, linkEnd)).DefaultIfEmpty(float.PositiveInfinity).Min() +
                    "; corners=" + string.Join(" -> ", path.corners.Select(point => (point - offset).ToString("F4"))));
                for (int i = 0; i + 1 < path.corners.Length; i++)
                {
                    Vector3 portalEntry = path.corners[i], portalExit = path.corners[i + 1];
                    if (PortalDistance(portalEntry, linkStart) > 0.01f || PortalDistance(portalExit, linkEnd) > 0.01f) continue;
                    WriteNativePathQuery("Initial committed portal span forward (strict samples)", portalEntry, portalExit, 0.01f);
                    WriteNativePathQuery("Initial committed portal span reverse (strict samples)", portalExit, portalEntry, 0.01f);
                    WriteNativePathQuery("Initial committed portal span forward (default samples)", portalEntry, portalExit, hunterConfig.PathSampleRadius);
                    WriteNativePathQuery("Initial committed portal span reverse (default samples)", portalExit, portalEntry, hunterConfig.PathSampleRadius);
                }
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(Length(path.corners), Is.LessThan(5.5f), "A route around the arena cannot stand in for crossing this link.");
                Assert.That(path.corners.Any(point => PortalDistance(point, linkStart) <= 0.01f), Is.True,
                    "The complete path must include a corner on the authored start portal edge (within 0.01 m).");
                Assert.That(path.corners.Any(point => PortalDistance(point, linkEnd) <= 0.01f), Is.True,
                    "The complete path must include a corner on the authored end portal edge (within 0.01 m).");
                Vector3 direction = (goal - start).normalized;
                Assert.That(Physics.Raycast(start + Vector3.up * 1.5f, direction, out RaycastHit hit,
                    Vector3.Distance(start, goal), ~0, QueryTriggerInteraction.Ignore), Is.True);
                Assert.That(hit.collider, Is.SameAs(gate), "The gate's actual collider must remain in the path.");
                TestContext.WriteLine("Initial path length=" + Length(path.corners) + "; corners=" +
                    string.Join(" -> ", path.corners.Select(point => (point - offset).ToString())));
            }

            public void AssertHunterCrosses(Vector3 start, Vector3 goal, Vector3 direction)
            {
                GameObject actor = Actor("Temporary Hunter motor", start, direction);
                try
                {
                    var capsule = actor.GetComponent<CapsuleCollider>();
                    capsule.radius = hunterConfig.Radius; capsule.height = hunterConfig.Height;
                    capsule.center = Vector3.up * (hunterConfig.Height * 0.5f);
                    var driver = actor.AddComponent<HunterDriver>();
                    Wire(driver, hunterConfig); driver.Initialize(); Physics.SyncTransforms();
                    float maximumAdvance = 0f, maxLateral = 0f;
                    bool overlappedGate = false;
                    int firstGoalTick = 0, firstOverlapTick = 0, firstClearedTick = 0, firstDriftTick = 0;
                    for (int tick = 1; tick <= 180; tick++)
                    {
                        driver.Move(goal, hunterProfile.PatrolSpeed, hunterProfile.Acceleration, hunterProfile.TurnRate,
                            Step, false, false, direction, hunterProfile.LungeSpeed, hunterProfile.LungeDistance);
                        float goalDistance = Vector3.Distance(driver.Position, goal);
                        float lateral = GateLateral(driver.Position);
                        if (tick == 1 || tick % 30 == 0 || !driver.PathAvailable || Mathf.Abs(driver.Position.y) >= 0.08f)
                            TestContext.WriteLine("Hunter motor tick=" + tick + "; position=" + (driver.Position - offset).ToString("F4") +
                                "; velocity=" + driver.Velocity.ToString("F4") + "; forward=" + driver.Forward.ToString("F4") +
                                "; goal distance=" + goalDistance + "; lateral=" + lateral + "; path available=" + driver.PathAvailable +
                                "; " + DriverStateDetails(driver));
                        if (firstGoalTick == 0 && goalDistance < 0.2f)
                        {
                            firstGoalTick = tick;
                            TestContext.WriteLine("First Hunter goal entry tick=" + tick + "; position=" +
                                (driver.Position - offset).ToString("F4") + "; lateral=" + lateral + "; " + DriverStateDetails(driver));
                        }
                        if (firstClearedTick == 0 && Vector3.Dot(driver.Position - gate.bounds.center, direction) >
                            gate.bounds.extents.z + hunterConfig.Radius)
                        {
                            firstClearedTick = tick;
                            TestContext.WriteLine("First full Hunter capsule beyond far gate face tick=" + tick +
                                "; position=" + (driver.Position - offset).ToString("F4") + "; lateral=" + lateral);
                        }
                        if (firstDriftTick == 0 && lateral > 0.1f)
                        {
                            firstDriftTick = tick;
                            TestContext.WriteLine("First lateral > 0.1 m at motor tick " + tick + "; " + DriverStateDetails(driver));
                            WriteFreshPathDiagnostic("First lateral > 0.1 m at motor tick " + tick, driver.Position, goal);
                        }
                        Assert.That(driver.PathAvailable, Is.True, "Actual Hunter path unavailable at motor tick " + tick);
                        maximumAdvance = Mathf.Max(maximumAdvance, Vector3.Dot(driver.Position - start, direction));
                        maxLateral = Mathf.Max(maxLateral, GateLateral(driver.Position));
                        Assert.That(Mathf.Abs(driver.Position.y), Is.LessThan(0.08f), "Hunter left the continuous physical floor.");
                        overlappedGate |= Physics.ComputePenetration(capsule, actor.transform.position, actor.transform.rotation,
                            gate, gate.transform.position, gate.transform.rotation, out _, out float depth) && depth > 0.01f;
                        if (firstOverlapTick == 0 && overlappedGate)
                        {
                            firstOverlapTick = tick;
                            TestContext.WriteLine("First actual gate overlap at motor tick " + tick + "; " + DriverStateDetails(driver));
                            WriteFreshPathDiagnostic("First actual gate overlap at motor tick " + tick, driver.Position, goal);
                        }
                    }
                    TestContext.WriteLine("Hunter before aggregate assertions: max advance=" + maximumAdvance +
                        "; max lateral=" + maxLateral + "; final=" + (driver.Position - offset).ToString("F4") +
                        "; final goal distance=" + Vector3.Distance(driver.Position, goal) + "; first goal tick=" + firstGoalTick +
                        "; first overlap tick=" + firstOverlapTick + "; first full crossing tick=" + firstClearedTick +
                        "; first lateral > 0.1 tick=" + firstDriftTick + "; actual gate overlap=" + overlappedGate);
                    Assert.That(overlappedGate, Is.True, "Hunter never physically entered the still-enabled gate collider's span.");
                    Assert.That(maxLateral, Is.LessThan(maximumGateLateral),
                        "The entire Hunter capsule must stay inside the gate width; travel around either gate end is excluded.");
                    Assert.That(Vector3.Distance(driver.Position, goal), Is.LessThan(0.2f));
                    Assert.That(Vector3.Dot(driver.Position - gate.bounds.center, direction),
                        Is.GreaterThan(gate.bounds.extents.z + hunterConfig.Radius));
                    TestContext.WriteLine("Hunter 180 direct motor ticks: advance=" + maximumAdvance +
                        "; final=" + (driver.Position - offset) + "; actual gate overlap=" + overlappedGate);
                }
                finally { UnityEngine.Object.DestroyImmediate(actor); Physics.SyncTransforms(); }
            }

            public void AssertActiveLinkInvalidatesAndRecovers(Vector3 start, Vector3 goal, Vector3 entry, Vector3 exit, Vector3 direction)
            {
                GameObject actor = Actor("Temporary Hunter link invalidation motor", start, direction);
                bool originalLinkEnabled = ownedLink.enabled;
                try
                {
                    var capsule = actor.GetComponent<CapsuleCollider>();
                    capsule.radius = hunterConfig.Radius; capsule.height = hunterConfig.Height;
                    capsule.center = Vector3.up * (hunterConfig.Height * 0.5f);
                    var driver = actor.AddComponent<HunterDriver>();
                    Wire(driver, hunterConfig); driver.Initialize(); Physics.SyncTransforms();
                    bool reachedRemovalPose = false;
                    NavMeshHit farBank = default;
                    float fullCrossing = gate.bounds.extents.z + hunterConfig.Radius;
                    for (int tick = 1; tick <= 180; tick++)
                    {
                        MoveHunter(driver, goal, direction);
                        HunterDriverState state = NativeDriverState(driver);
                        float pastMidpoint = Vector3.Dot(driver.Position - gate.bounds.center, direction);
                        bool sampled = NavMesh.SamplePosition(driver.Position, out farBank, hunterConfig.PathSampleRadius, NavMesh.AllAreas);
                        Vector3 sampleDelta = farBank.position - driver.Position; sampleDelta.y = 0f;
                        float possibleCoast = hunterProfile.PatrolSpeed * (Mathf.Max(0f, state.PathCooldown) + Step);
                        if (pastMidpoint < hunterProfile.PatrolSpeed * Step || pastMidpoint >= fullCrossing || !sampled ||
                            Vector3.Dot(sampleDelta, direction) <= hunterConfig.CornerTolerance + hunterConfig.SkinWidth ||
                            Vector3.Dot(farBank.position - gate.bounds.center, direction) <= fullCrossing ||
                            fullCrossing - pastMidpoint <= possibleCoast) continue;
                        reachedRemovalPose = true;
                        TestContext.WriteLine("Removal pose reached by normal motor tick=" + tick + "; past midpoint=" + pastMidpoint +
                            "; full crossing=" + fullCrossing + "; pending-refresh coast bound=" + possibleCoast +
                            "; far-bank sample=" + (farBank.position - offset).ToString("F4") +
                            "; planar sample displacement=" + sampleDelta.ToString("F4") + "; " + DriverStateDetails(driver));
                        Assert.That(driver.PathAvailable, Is.True);
                        Assert.That(Mathf.Abs(driver.Position.y), Is.LessThan(0.08f), "Removal pose must remain floor-supported.");
                        Assert.That(Physics.Raycast(driver.Position + Vector3.up * 0.1f, Vector3.down, out RaycastHit support,
                            0.2f, ~0, QueryTriggerInteraction.Ignore), Is.True, "Removal pose has no actual floor below it.");
                        Assert.That(Vector3.Dot(support.normal, Vector3.up), Is.GreaterThan(0.99f));
                        Assert.That(Physics.ComputePenetration(capsule, actor.transform.position, actor.transform.rotation,
                            gate, gate.transform.position, gate.transform.rotation, out _, out float depth) && depth > 0.01f,
                            Is.True, "Removal must occur during actual overlap with the enabled gate collider.");
                        Assert.That(state.Steering.CornerIndex, Is.GreaterThan(0));
                        Assert.That(state.Steering.CornerIndex, Is.LessThan(state.Steering.Corners.Length));
                        Vector3 activeEntry = state.Steering.Corners[state.Steering.CornerIndex - 1];
                        Vector3 activeExit = state.Steering.Corners[state.Steering.CornerIndex];
                        Assert.That(PortalDistance(activeEntry, entry), Is.LessThanOrEqualTo(0.01f));
                        Assert.That(PortalDistance(activeExit, exit), Is.LessThanOrEqualTo(0.01f));
                        WriteNativePathQuery("Before removal: accepted source to goal", state.Steering.Corners[0], goal, hunterConfig.PathSampleRadius);
                        WriteNativePathQuery("Before removal: committed entry to exit (strict samples)", activeEntry, activeExit, 0.01f);
                        WriteNativePathQuery("Before removal: committed exit to entry (strict samples)", activeExit, activeEntry, 0.01f);
                        WriteNativePathQuery("Before removal: committed entry to exit (default samples)", activeEntry, activeExit, hunterConfig.PathSampleRadius);
                        WriteRetainedPathContext(driver);
                        break;
                    }
                    Assert.That(reachedRemovalPose, Is.True,
                        "The same initialized Hunter must reach a far-bank-snap pose past midpoint by 180 normal motor ticks; it may not be relocated.");
                    HunterDriverState removalState = NativeDriverState(driver);
                    Vector3 acceptedSource = removalState.Steering.Corners[0];
                    Vector3 committedEntry = removalState.Steering.Corners[removalState.Steering.CornerIndex - 1];
                    Vector3 committedExit = removalState.Steering.Corners[removalState.Steering.CornerIndex];
                    ownedLink.enabled = false;
                    Physics.SyncTransforms();
                    Assert.That(ownedLink.isActiveAndEnabled, Is.False, "Only the owned cloned NavMeshLink is disabled.");
                    Assert.That(gate.enabled && !gate.isTrigger, Is.True, "The authored physical gate must stay enabled and solid.");
                    Assert.That(NavMesh.SamplePosition(driver.Position, out farBank, hunterConfig.PathSampleRadius, NavMesh.AllAreas), Is.True);
                    Assert.That(Vector3.Dot(farBank.position - driver.Position, direction),
                        Is.GreaterThan(hunterConfig.CornerTolerance + hunterConfig.SkinWidth));
                    Assert.That(NavMesh.SamplePosition(goal, out NavMeshHit sampledGoal, hunterConfig.PathSampleRadius, NavMesh.AllAreas), Is.True);
                    var misleadingPath = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(farBank.position, sampledGoal.position, NavMesh.AllAreas, misleadingPath), Is.True);
                    Assert.That(misleadingPath.status, Is.EqualTo(NavMeshPathStatus.PathComplete),
                        "The regression requires a complete far-bank path despite the link being removed.");
                    TestContext.WriteLine("Removed link still has a complete path from the far-bank sample: sample=" +
                        (farBank.position - offset).ToString("F4") + "; corners=" +
                        string.Join(" -> ", misleadingPath.corners.Select(point => (point - offset).ToString("F4"))));
                    WriteNativePathQuery("After removal: accepted source to goal", acceptedSource, goal, hunterConfig.PathSampleRadius);
                    WriteNativePathQuery("After removal: committed entry to exit", committedEntry, committedExit, hunterConfig.PathSampleRadius);
                    int refreshes = 0;
                    int refreshTickBound = (Mathf.CeilToInt(hunterConfig.PathRepathSeconds / Step) + 2) * 4;
                    float previousCooldown = removalState.PathCooldown;
                    for (int tick = 1; tick <= refreshTickBound && refreshes < 3; tick++)
                    {
                        MoveHunter(driver, goal, direction);
                        HunterDriverState state = NativeDriverState(driver);
                        if (state.PathCooldown > previousCooldown + Step * 0.5f)
                        {
                            refreshes++;
                            TestContext.WriteLine("Removed-link refresh=" + refreshes + "; motor tick=" + tick +
                                "; past midpoint=" + Vector3.Dot(driver.Position - gate.bounds.center, direction) + "; " + DriverStateDetails(driver));
                            WriteRetainedPathContext(driver);
                        }
                        previousCooldown = state.PathCooldown;
                        Assert.That(Vector3.Dot(driver.Position - gate.bounds.center, direction), Is.LessThan(fullCrossing),
                            "Hunter completed a physical crossing of the removed link.");
                        Assert.That(Vector3.Distance(driver.Position, goal), Is.GreaterThan(0.2f));
                        Assert.That(GateLateral(driver.Position), Is.LessThan(maximumGateLateral));
                        if (refreshes > 0)
                        {
                            Assert.That(driver.PathAvailable, Is.False,
                                "A far-bank sample must not revive an invalidated active crossing, including later refreshes.");
                            Assert.That(state.Steering.Corners, Is.Empty, "Invalidated crossing must clear the movement path.");
                        }
                    }
                    Assert.That(refreshes, Is.EqualTo(3), "Observe three actual repaths after removal, not just elapsed motor ticks.");
                    ownedLink.enabled = originalLinkEnabled;
                    Physics.SyncTransforms();
                    Assert.That(ownedLink.isActiveAndEnabled, Is.True);
                    bool recoveredPath = false;
                    for (int tick = 1; tick <= 180; tick++)
                    {
                        MoveHunter(driver, goal, direction);
                        if (!recoveredPath && driver.PathAvailable)
                        {
                            recoveredPath = true;
                            TestContext.WriteLine("Restored link recovered on motor tick=" + tick + "; " + DriverStateDetails(driver));
                        }
                        Assert.That(GateLateral(driver.Position), Is.LessThan(maximumGateLateral));
                        Assert.That(Mathf.Abs(driver.Position.y), Is.LessThan(0.08f));
                    }
                    Assert.That(recoveredPath && driver.PathAvailable, Is.True, "Restoring only the owned link must recover the same initialized Driver.");
                    Assert.That(Vector3.Distance(driver.Position, goal), Is.LessThan(0.2f));
                    Assert.That(Vector3.Dot(driver.Position - gate.bounds.center, direction), Is.GreaterThan(fullCrossing));
                    TestContext.WriteLine("Native link removal/recovery complete: three invalidated repaths, same Driver, actual restored crossing and arrival; " +
                        "final=" + (driver.Position - offset).ToString("F4"));
                }
                finally
                {
                    if (ownedLink != null) ownedLink.enabled = originalLinkEnabled;
                    UnityEngine.Object.DestroyImmediate(actor);
                    Physics.SyncTransforms();
                }
            }

            public void AssertNearBankLinkRemovalUsesDetour(Vector3 start, Vector3 goal, Vector3 direction)
            {
                GameObject actor = Actor("Temporary Hunter near-bank invalidation motor", start, direction);
                bool originalLinkEnabled = ownedLink.enabled;
                try
                {
                    var capsule = actor.GetComponent<CapsuleCollider>();
                    capsule.radius = hunterConfig.Radius; capsule.height = hunterConfig.Height;
                    capsule.center = Vector3.up * (hunterConfig.Height * 0.5f);
                    var driver = actor.AddComponent<HunterDriver>();
                    Wire(driver, hunterConfig); driver.Initialize(); Physics.SyncTransforms();
                    bool reachedAttachedContext = false;
                    for (int tick = 1; tick <= 180; tick++)
                    {
                        MoveHunter(driver, goal, direction);
                        HunterDriverState state = NativeDriverState(driver);
                        if (!state.RepathActive || Vector3.Dot(driver.Position - gate.bounds.center, direction) >= 0f ||
                            !IsActuallyAttached(driver.Position, out NavMeshHit sample) ||
                            !IsActuallyAttached(state.RepathEntry, out NavMeshHit entrySample) ||
                            NavMesh.Raycast(sample.position, entrySample.position, out _, NavMesh.AllAreas)) continue;
                        reachedAttachedContext = true;
                        Assert.That(driver.PathAvailable, Is.True, "The link must still be valid when its attached context is observed.");
                        Vector3 segment = state.RepathExit - state.RepathEntry; segment.y = 0f;
                        Vector3 segmentOffset = driver.Position - state.RepathEntry; segmentOffset.y = 0f;
                        Assert.That(Vector3.Dot(segmentOffset, segment), Is.GreaterThan(0f));
                        Assert.That(Vector3.Dot(segmentOffset, segment), Is.LessThan(segment.sqrMagnitude));
                        TestContext.WriteLine("Near-bank active context reached at normal motor tick=" + tick +
                            "; actual=" + (driver.Position - offset).ToString("F4") + "; sample=" + (sample.position - offset).ToString("F4") +
                            "; retained entry=" + (state.RepathEntry - offset).ToString("F4") +
                            "; retained exit=" + (state.RepathExit - offset).ToString("F4") + "; " + DriverStateDetails(driver));
                        break;
                    }
                    Assert.That(reachedAttachedContext, Is.True,
                        "Normal East forward motion must acquire active crossing context while still on its connected entry bank.");
                    Vector3 heldPosition = driver.Position;
                    int holdTicks = 0;
                    int maximumHoldTicks = Mathf.CeilToInt(hunterConfig.PathRepathSeconds / Step) + 2;
                    while (NativeDriverState(driver).PathCooldown > 0f && holdTicks < maximumHoldTicks)
                    {
                        driver.Move(goal, hunterProfile.PatrolSpeed, hunterProfile.Acceleration, hunterProfile.TurnRate,
                            Step, true, false, direction, hunterProfile.LungeSpeed, hunterProfile.LungeDistance);
                        holdTicks++;
                    }
                    HunterDriverState heldState = NativeDriverState(driver);
                    Assert.That(heldState.PathCooldown, Is.LessThanOrEqualTo(0f), "The next public moving command must query immediately.");
                    Assert.That(heldState.RepathActive, Is.True, "Public recovery must preserve the pending crossing context.");
                    Vector3 heldDelta = driver.Position - heldPosition; heldDelta.y = 0f;
                    Assert.That(heldDelta.magnitude, Is.LessThan(0.001f), "Public stopped movement must hold the bank position.");
                    Assert.That(IsActuallyAttached(driver.Position, out NavMeshHit bank), Is.True);
                    Assert.That(IsActuallyAttached(heldState.RepathEntry, out NavMeshHit retainedEntry), Is.True);
                    Assert.That(NavMesh.Raycast(bank.position, retainedEntry.position, out _, NavMesh.AllAreas), Is.False,
                        "The held body must remain connected to the original entry bank.");
                    ownedLink.enabled = false;
                    Physics.SyncTransforms();
                    Assert.That(ownedLink.isActiveAndEnabled, Is.False);
                    Assert.That(gate.enabled && !gate.isTrigger, Is.True);
                    Assert.That(NavMesh.SamplePosition(goal, out NavMeshHit target, hunterConfig.PathSampleRadius, NavMesh.AllAreas), Is.True);
                    var alternative = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(bank.position, target.position, NavMesh.AllAreas, alternative), Is.True);
                    Assert.That(alternative.status, Is.EqualTo(NavMeshPathStatus.PathComplete),
                        "An attached body needs a currently complete alternate route for this regression.");
                    Assert.That(NavMesh.Raycast(bank.position, target.position, out _, NavMesh.AllAreas), Is.True,
                        "The straight route must remain blocked by the baked cut.");
                    Assert.That(ContainsPortalCrossing(alternative.corners), Is.False, "The fresh route must omit the disabled portal pair.");
                    Vector3 detourCorner = alternative.corners.First(point =>
                    {
                        Vector3 delta = point - bank.position; delta.y = 0f;
                        return delta.magnitude > hunterConfig.CornerTolerance;
                    });
                    float initialCornerDistance = Vector3.Distance(driver.Position, detourCorner);
                    float closestCornerDistance = initialCornerDistance, maximumDisplacement = 0f;
                    TestContext.WriteLine("Near-bank link removed after " + holdTicks + " public stopped ticks: cooldown=" + heldState.PathCooldown +
                        "; alternate length=" + Length(alternative.corners) + "; alternate corners=" +
                        string.Join(" -> ", alternative.corners.Select(point => (point - offset).ToString("F4"))));
                    int movementTicks = Mathf.CeilToInt((180f / hunterProfile.TurnRate + hunterProfile.PatrolSpeed / hunterProfile.Acceleration +
                        2f * hunterConfig.Radius / hunterProfile.PatrolSpeed) / Step);
                    float previousMidpointSide = Vector3.Dot(driver.Position - gate.bounds.center, direction);
                    int observedMovementTicks = 0;
                    for (int tick = 1; tick <= movementTicks; tick++)
                    {
                        MoveHunter(driver, goal, direction);
                        observedMovementTicks = tick;
                        HunterDriverState state = NativeDriverState(driver);
                        if (tick == 1)
                        {
                            TestContext.WriteLine("First due near-bank repath: active=" + state.RepathActive + "; " + DriverStateDetails(driver));
                            Assert.That(state.RepathActive, Is.False, "Failed crossing validation on the connected entry bank must release pending context.");
                            Assert.That(ContainsPortalCrossing(state.Steering.Corners), Is.False);
                        }
                        Assert.That(driver.PathAvailable, Is.True, "The attached Hunter must use the valid detour instead of remaining invalidated.");
                        closestCornerDistance = Mathf.Min(closestCornerDistance, Vector3.Distance(driver.Position, detourCorner));
                        Vector3 displacement = driver.Position - heldPosition; displacement.y = 0f;
                        maximumDisplacement = Mathf.Max(maximumDisplacement, displacement.magnitude);
                        float midpointSide = Vector3.Dot(driver.Position - gate.bounds.center, direction);
                        if (previousMidpointSide <= 0f && midpointSide > 0f)
                            Assert.That(GateLateral(driver.Position), Is.GreaterThanOrEqualTo(maximumGateLateral + 2f * hunterConfig.Radius),
                                "A detour must cross the gate plane outside the full physical gate-plus-capsule width.");
                        previousMidpointSide = midpointSide;
                        if (maximumDisplacement > 2f * hunterConfig.Radius && initialCornerDistance - closestCornerDistance >
                            Mathf.Min(hunterConfig.Radius, initialCornerDistance * 0.5f)) break;
                    }
                    Assert.That(maximumDisplacement, Is.GreaterThan(2f * hunterConfig.Radius), "The Hunter must actually move at least one capsule diameter.");
                    Assert.That(initialCornerDistance - closestCornerDistance, Is.GreaterThan(Mathf.Min(hunterConfig.Radius, initialCornerDistance * 0.5f)),
                        "Actual movement must make progress toward the freshly observed detour corner.");
                    TestContext.WriteLine("Near-bank detour movement observed in " + observedMovementTicks + " normal ticks, bound=" + movementTicks +
                        " (half-turn + acceleration + capsule travel budget): " +
                        "displacement=" + maximumDisplacement + "; detour-corner progress=" + (initialCornerDistance - closestCornerDistance));
                }
                finally
                {
                    if (ownedLink != null) ownedLink.enabled = originalLinkEnabled;
                    UnityEngine.Object.DestroyImmediate(actor);
                    Physics.SyncTransforms();
                }
            }

            private bool IsActuallyAttached(Vector3 position, out NavMeshHit sample)
            {
                if (!NavMesh.SamplePosition(position, out sample, hunterConfig.PathSampleRadius, NavMesh.AllAreas)) return false;
                Vector3 delta = sample.position - position;
                return delta.x * delta.x + delta.z * delta.z <= 0.0001f &&
                    Mathf.Abs(delta.y) <= hunterConfig.GroundProbeDistance + hunterConfig.SkinWidth;
            }

            private bool ContainsPortalCrossing(Vector3[] corners)
            {
                for (int i = 0; i + 1 < corners.Length; i++)
                    if ((PortalDistance(corners[i], LinkStart) <= 0.01f && PortalDistance(corners[i + 1], LinkEnd) <= 0.01f) ||
                        (PortalDistance(corners[i], LinkEnd) <= 0.01f && PortalDistance(corners[i + 1], LinkStart) <= 0.01f)) return true;
                return false;
            }

            private void MoveHunter(HunterDriver driver, Vector3 goal, Vector3 direction) => driver.Move(goal,
                hunterProfile.PatrolSpeed, hunterProfile.Acceleration, hunterProfile.TurnRate, Step, false, false,
                direction, hunterProfile.LungeSpeed, hunterProfile.LungeDistance);

            private static HunterDriverState NativeDriverState(HunterDriver driver)
            {
                var field = typeof(HunterDriver).GetField("_state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var state = field?.GetValue(driver) as HunterDriverState;
                Assert.That(state, Is.Not.Null, "Read-only native state observation requires an initialized HunterDriver.");
                return state;
            }

            private void WriteRetainedPathContext(HunterDriver driver)
            {
                HunterDriverState state = NativeDriverState(driver);
                foreach (string name in new[] { "RepathCorners", "RepathCornerIndex", "RepathTarget", "RepathAnchor", "HasRepathContext" })
                {
                    var field = typeof(HunterDriverState).GetField(name);
                    object value = field?.GetValue(state);
                    string formatted = value is Vector3[] corners
                        ? string.Join(" -> ", corners.Select(point => (point - offset).ToString("F4")))
                        : value is Vector3 vectorValue ? (vectorValue - offset).ToString("F4") : value?.ToString() ?? "not present";
                    TestContext.WriteLine("Read-only optional continuation context " + name + "=" + formatted);
                }
            }

            private void WriteNativePathQuery(string label, Vector3 start, Vector3 goal, float radius)
            {
                bool foundStart = NavMesh.SamplePosition(start, out NavMeshHit from, radius, NavMesh.AllAreas);
                bool foundGoal = NavMesh.SamplePosition(goal, out NavMeshHit to, radius, NavMesh.AllAreas);
                var path = new NavMeshPath();
                bool foundPath = foundStart && foundGoal && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path);
                var directPath = new NavMeshPath();
                bool foundDirectPath = NavMesh.CalculatePath(start, goal, NavMesh.AllAreas, directPath);
                TestContext.WriteLine(label + ": external read-only query; sample radius=" + radius +
                    "; requested start=" + (start - offset).ToString("F4") + "; requested goal=" + (goal - offset).ToString("F4") +
                    "; sampled start=" + foundStart + ":" + (from.position - offset).ToString("F4") +
                    "; sampled goal=" + foundGoal + ":" + (to.position - offset).ToString("F4") +
                    "; calculated=" + foundPath + "; status=" + path.status + "; length=" + Length(path.corners) +
                    "; corners=" + string.Join(" -> ", path.corners.Select(point => (point - offset).ToString("F4"))) +
                    "; direct requested-point CalculatePath=" + foundDirectPath + "; direct status=" + directPath.status +
                    "; direct corners=" + string.Join(" -> ", directPath.corners.Select(point => (point - offset).ToString("F4"))));
            }

            private float PortalDistance(Vector3 point, Vector3 edgeCenter)
            {
                float alongEdge = Mathf.Clamp(Vector3.Dot(point - edgeCenter, portalAcross), -portalWidth * 0.5f, portalWidth * 0.5f);
                return Vector3.Distance(point, edgeCenter + portalAcross * alongEdge);
            }

            private float GateLateral(Vector3 position) => Mathf.Abs(Vector3.Dot(position - gate.bounds.center, gateAcross));

            private string DriverStateDetails(HunterDriver driver)
            {
                var field = typeof(HunterDriver).GetField("_state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var state = field?.GetValue(driver) as HunterDriverState;
                if (state == null) return "Driver state diagnostic unavailable";
                return "actual Driver state: cooldown=" + state.PathCooldown + "; corner index=" + state.Steering.CornerIndex +
                    "; position=" + (state.Steering.Position - offset).ToString("F4") + "; forward=" + state.Steering.Forward.ToString("F4") +
                    "; steering corners=" + string.Join(" -> ", state.Steering.Corners.Select(point => (point - offset).ToString("F4"))) +
                    "; NavMeshPath corners=" + string.Join(" -> ", state.Path.corners.Select(point => (point - offset).ToString("F4")));
            }

            private void WriteFreshPathDiagnostic(string label, Vector3 position, Vector3 goal)
            {
                bool sampledStart = NavMesh.SamplePosition(position, out NavMeshHit from, hunterConfig.PathSampleRadius, NavMesh.AllAreas);
                bool sampledGoal = NavMesh.SamplePosition(goal, out NavMeshHit to, hunterConfig.PathSampleRadius, NavMesh.AllAreas);
                var path = new NavMeshPath();
                bool complete = sampledStart && sampledGoal && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path);
                TestContext.WriteLine(label + ": independent read-only query, not Driver internal path; position=" +
                    (position - offset).ToString("F4") + "; sampled start=" + sampledStart + ":" + (from.position - offset).ToString("F4") +
                    "; start displacement=" + (from.position - position).ToString("F4") + "; sampled goal=" + sampledGoal +
                    "; CalculatePath=" + complete + "; status=" + path.status + "; corners=" +
                    string.Join(" -> ", path.corners.Select(point => (point - offset).ToString("F4"))));
            }

            public void AssertPlayerStops(Vector3 start, Vector3 direction)
            {
                GameObject actor = Actor("Temporary Player motor", start, direction);
                try
                {
                    var driver = actor.AddComponent<PlayerDriver>();
                    Wire(driver, playerConfig); driver.Initialize(); Physics.SyncTransforms();
                    var capsule = actor.GetComponent<CapsuleCollider>();
                    Vector3 nearFace = gate.bounds.center - direction * gate.bounds.extents.z;
                    float maximumAllowed = Vector3.Dot(nearFace - start, direction) - playerConfig.Radius + playerConfig.SkinWidth + 0.01f;
                    float maxAdvance = 0f, maxPenetration = 0f;
                    PlayerMoveResult last = default;
                    for (int tick = 1; tick <= 120; tick++)
                    {
                        Vector3 request = direction * playerProfile.SprintSpeed + Vector3.down * (playerProfile.Gravity * Step);
                        last = driver.Move(request * Step, request, false, actor.transform.eulerAngles.y, Step);
                        Physics.SyncTransforms();
                        maxAdvance = Mathf.Max(maxAdvance, Vector3.Dot(last.Position - start, direction));
                        if (Physics.ComputePenetration(capsule, actor.transform.position, actor.transform.rotation,
                            gate, gate.transform.position, gate.transform.rotation, out _, out float depth))
                            maxPenetration = Mathf.Max(maxPenetration, depth);
                        Assert.That(maxAdvance, Is.LessThanOrEqualTo(maximumAllowed), "Player capsule crossed the Hunter-only gate.");
                        Assert.That(capsule.height, Is.EqualTo(playerConfig.Height));
                    }
                    Assert.That(maxAdvance, Is.GreaterThan(0.75f), "Player never reached the gate.");
                    Assert.That(maxPenetration, Is.LessThan(0.01f), "Player overlapped the gate instead of resolving collision.");
                    Assert.That(Vector3.Dot(last.Velocity, direction), Is.LessThan(0.05f), "Player motor was not stopped by collision.");
                    Assert.That(last.Grounded, Is.True);
                    TestContext.WriteLine("Player 120 direct motor ticks: advance=" + maxAdvance +
                        "; stop bound=" + maximumAllowed + "; penetration=" + maxPenetration + "; final=" + (last.Position - offset));
                }
                finally { UnityEngine.Object.DestroyImmediate(actor); Physics.SyncTransforms(); }
            }

            private GameObject Actor(string name, Vector3 position, Vector3 direction)
            {
                var actor = new GameObject(name); actors.Add(actor);
                actor.transform.SetParent(temporaryRoot.transform, true);
                Assert.That(actor.scene, Is.EqualTo(previousActive));
                actor.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
                actor.AddComponent<CapsuleCollider>(); actor.AddComponent<Rigidbody>();
                return actor;
            }
            private void Remember(UnityEngine.Object value)
            { Assert.That(value, Is.Not.Null); snapshots.Add(value, JsonUtility.ToJson(value)); RememberAsset(AssetDatabase.GetAssetPath(value)); }
            private void RememberAsset(string path)
            { Assert.That(path, Is.Not.Empty); if (!assetHashes.ContainsKey(path)) assetHashes.Add(path, AssetDatabase.GetAssetDependencyHash(path)); }
            public void AssertAssetsUnchanged()
            {
                foreach (var value in snapshots) Assert.That(JsonUtility.ToJson(value.Key), Is.EqualTo(value.Value), value.Key.name + " changed.");
                foreach (var value in assetHashes) Assert.That(AssetDatabase.GetAssetDependencyHash(value.Key), Is.EqualTo(value.Value), value.Key + " changed.");
                Assert.That(sourceScene.isDirty, Is.False, "The authored scene must remain unmodified.");
            }
            public void Dispose()
            {
                foreach (GameObject actor in actors) if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
                actors.Clear();
                if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
                if (temporaryRoot != null) UnityEngine.Object.DestroyImmediate(temporaryRoot);
                if (previousActive.IsValid() && previousActive.isLoaded && SceneManager.GetActiveScene() != previousActive)
                    SceneManager.SetActiveScene(previousActive);
                if (openedSource && sourceScene.IsValid() && sourceScene.isLoaded) EditorSceneManager.CloseScene(sourceScene, true);
                Physics.SyncTransforms();
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousActive),
                    "Cleanup must restore the active framework scene: requested=" + SceneDetails(previousActive) +
                    "; actual=" + SceneDetails(SceneManager.GetActiveScene()));
                for (int i = 0; i < previousScenes.Length; i++)
                {
                    Assert.That(previousScenes[i].isLoaded, Is.EqualTo(previousLoaded[i]), "A pre-existing scene load state changed.");
                    if (previousScenes[i] != previousActive)
                        Assert.That(previousScenes[i].isDirty, Is.EqualTo(previousDirty[i]), "A pre-existing authored scene dirty state changed.");
                }
                CollectionAssert.AreEquivalent(previousFrameworkRoots, previousActive.GetRootGameObjects(),
                    "Cleanup must preserve every original framework root and leave no owned roots behind.");
            }
        }

        private static Component ComponentNamed(GameObject owner, string fullName) => owner.GetComponents<Component>()
            .Single(item => item != null && item.GetType().FullName == fullName);
        private static SerializedProperty Property(SerializedObject owner, string name)
        { var value = owner.FindProperty(name); Assert.That(value, Is.Not.Null, "Missing serialized field " + name); return value; }
        private static T Reference<T>(SerializedObject owner, string name) where T : UnityEngine.Object
        { var value = Property(owner, name).objectReferenceValue as T; Assert.That(value, Is.Not.Null, name); return value; }
        private static void Wire(Component driver, ScriptableObject config)
        { var fields = new SerializedObject(driver); Property(fields, "_config").objectReferenceValue = config; fields.ApplyModifiedPropertiesWithoutUndo(); }
        private static float Length(Vector3[] corners)
        { float length = 0f; for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]); return length; }
    }
}
