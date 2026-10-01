// ============================================================================
// EnvironmentTemplateRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises procedural boundary and light socket routing without generation.
//   Renderable test meshes expose admission rather than counting empty transforms.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Environment.
// KEY RESPONSIBILITIES:
//   - Compare assembled dressing with the exact procedural boundary/socket commands.
// DEPENDENCIES:
//   Core, Procedural, Expedition, Environment, Orchestrator and NUnit.
// USAGE NOTES:
//   Coordinator-run native Edit Mode; only transient objects and layout data are used.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;
using Worsen.Session.Expedition;
using Worsen.Session.Run;
using Worsen.Session.HorrorEffects;
using Worsen.Presentation.Environment;
using Worsen.Orchestrator;
using Object = UnityEngine.Object;
namespace Worsen.Tests.CastleEnvironment
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class EnvironmentTemplateRoutingTests
    {
        [Test, Category("Native")] public void RoomsUseCurvedBoundaryAndOnlyAuthoredSockets()
        {
            var objects = new List<GameObject>(); var config = ScriptableObject.CreateInstance<EnvironmentDriverConfig>();
            var procedural = Make<ProceduralManager>(objects); var environment = Make<EnvironmentManager>(objects);
            var expedition = Make<ExpeditionSessionManager>(objects); var route = Make<EnvironmentOrchestrator>(objects);
            var driver = environment.GetComponent<EnvironmentDriver>();
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.name = "Boundary test decoration";
            prefab.transform.localScale = Vector3.one * .5f; objects.Add(prefab); prefab.SetActive(false);
            try
            {
                Set(config, "_castleFurniture", "Missing/TestFurniture");
                Set(config, "_castleWallDecoration", "Missing/TestWallDecoration");
                Set(config, "_wallDecorationPrefabs", new[] { prefab }); Set(config, "_floorPropPrefabs", new[] { prefab });
                Set(config, "_cornerColumnPrefab", prefab); Set(config, "_doorArchPrefab", prefab);
                Set(config, "_merchantDisplayPrefab", prefab);
                Set(environment, "_driver", driver); Set(driver, "_config", config);
                var layout = new ProceduralLayout();
                Property(layout, "OrganicRooms", new[] { new ProceduralOrganicRoom(7, ProceduralRoomShape.Round,
                    Array.Empty<Vector2Int>(), Vector3.zero, Vector3.forward) });
                var socket = new Vector3(1f, 2f, 3f);
                Property(layout, "Interactables", new[] { new ProceduralInteractablePlan(
                    new InteractableState(42, InteractableKind.Light, 7, socket, InteractableStateValue.Lit), Vector3.one) });
                var state = Get<ProceduralBehaviorState>(procedural, "_state"); Property(state, "Layout", layout); Property(state, "IsReady", true);
                var bounds = new Bounds(new Vector3(0f, 3f, 2f), new Vector3(12f, 6f, 16f));
                var boundary = procedural.RoomBoundary(7); Assert.That(boundary, Is.Not.Null);
                var expected = EnvironmentPresenter.BuildDressing(7, bounds, false, false, Array.Empty<Vector3>(),
                    boundary: boundary, lightSockets: procedural.RoomLightSockets(7));
                var unconstrained = EnvironmentPresenter.BuildDressing(7, bounds, false, false, Array.Empty<Vector3>(),
                    lightSockets: procedural.RoomLightSockets(7));
                Assert.That(expected.Length, Is.LessThan(unconstrained.Length), "Fixture must expose missing boundary routing.");
                route.Configure(Make<RunSessionManager>(objects), expedition, Make<HorrorEffectsManager>(objects), environment, procedural: procedural);
                Call(route, "OnEnable");
                Get<Delegate>(expedition, "RoomsReady").DynamicInvoke((object)new[] {
                    new GeneratedRoomSample(7, bounds, false, false, Array.Empty<Vector3>()) });
                var output = Get<EnvironmentDriverState>(driver, "_state");
                Assert.That(output.Flames.Select(f => f.SocketPosition), Is.EqualTo(new[] { socket }));
                var decorations = output.Rooms[7].GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == prefab.name + " Dressing").Select(t => t.position).ToArray();
                Assert.That(decorations, Is.EquivalentTo(expected.Where(s => !s.Torch).Select(s => s.Position)));
            }
            finally
            {
                Call(route, "OnDisable");
                driver.Teardown(); procedural.Teardown();
                for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
                Object.DestroyImmediate(config);
            }
        }
        private static T Make<T>(List<GameObject> owners) where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); owners.Add(go); return go.AddComponent<T>(); }
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
