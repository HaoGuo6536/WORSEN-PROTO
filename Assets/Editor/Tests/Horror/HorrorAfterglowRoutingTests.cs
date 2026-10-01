// ============================================================================
// HorrorAfterglowRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Verifies authoritative room windows route only extinguished lights into Afterglow.
//   Fixtures use Level's relightable Inactive value and valid room placements.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Horror.
// KEY RESPONSIBILITIES:
//   - Preserve room identity/duration and remove visuals on relight or removal.
//   - Exercise the typed Run event channel without inventing gameplay safety.
// DEPENDENCIES:
//   Core, Level, Run, Horror, Orchestrator, Lumen and NUnit.
// USAGE NOTES:
//   Native Edit Mode; the typed Run channel must exist and pair exactly once.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using DistantLands.Lumen;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Session.Run;
using Worsen.Presentation.Horror;
using Worsen.Orchestrator;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Horror
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HorrorAfterglowRoutingTests
    {
        [Test] public void RoomWindowUsesBrokenLightsAndRelightOrRemovalClearsThem()
        {
            var owners = new List<GameObject>();
            var config = ScriptableObject.CreateInstance<HorrorDriverConfig>();
            var profile = ScriptableObject.CreateInstance<LumenEffectProfile>();
            profile.layers.Add(new LumenLightLayer { range = 2f, intensity = .2f });
            var prefab = new GameObject("Afterglow routing fill"); prefab.SetActive(false); owners.Add(prefab);
            prefab.AddComponent<LumenEffectPlayer>().profile = profile; Set(config, "_lumenNearFillPrefab", prefab);
            var run = Make<RunSessionManager>(owners); var level = Make<LevelManager>(owners);
            var horror = Make<HorrorManager>(owners); var driver = horror.GetComponent<HorrorDriver>();
            var glow = Make<HorrorAfterglowDriver>(owners); var route = Make<HorrorOrchestrator>(owners);
            try
            {
                var broken = new InteractableState(42, InteractableKind.Light, 7, Vector3.one, InteractableStateValue.Inactive);
                var lit = new InteractableState(43, InteractableKind.Light, 7, Vector3.right, InteractableStateValue.Lit);
                var elsewhere = new InteractableState(44, InteractableKind.Light, 8, Vector3.right * 20f, InteractableStateValue.Inactive);
                var graph = new LevelGraph(new[] { new LevelRoom(7, Vector3.zero, Vector3.one * 12f),
                    new LevelRoom(8, Vector3.right * 20f, Vector3.one * 12f) }, Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 8, Vector3.right * 20f);
                level.InitializeGenerated(graph, new[] { broken, lit, elsewhere });
                horror.gameObject.SetActive(true); glow.gameObject.SetActive(true);
                glow.Initialize(config); glow.SetEffects(new ActiveEffects(new[] { new ActiveEffect(new EffectId("afterglow"), EffectKind.Upgrade, 1) }));
                Set(horror, "_driver", driver); Set(driver, "_state", new HorrorDriverState { OwnerEnabled = true }); Set(driver, "_afterglow", glow);
                Set(route, "_run", run); Set(route, "_level", level); Set(route, "_horror", horror);
                Call(route, "PairAfterglow", true);
                var receiver = Get<Delegate>(run.HunterFacts, "AfterglowWindowPublished");
                Assert.That(receiver, Is.Not.Null);
                Assert.That(receiver.GetInvocationList().Length, Is.EqualTo(1)); receiver.DynamicInvoke(7, 3f);
                var state = Get<HorrorAfterglowDriverState>(glow, "state");
                Assert.That(state.Lights.Keys, Is.EquivalentTo(new[] { 42 }));
                Assert.That(state.Lights[42].Remaining, Is.EqualTo(3f));
                glow.Tick(1f); Call(route, "OnAfterglowWindow", 7, 3f);
                Assert.That(state.Lights[42].Remaining, Is.EqualTo(2f), "Replay cannot extend the visual window.");
                Call(route, "OnLightChanged", broken, new InteractableState(42, InteractableKind.Light, 7, Vector3.one, InteractableStateValue.Lit));
                Assert.That(state.Lights, Is.Empty);
                Call(route, "OnAfterglowWindow", 7, 1f); Call(route, "OnLightChanged", broken, default(InteractableState));
                Assert.That(state.Lights, Is.Empty);
                Call(route, "PairAfterglow", false);
                Assert.That(Get<Delegate>(run.HunterFacts, "AfterglowWindowPublished"), Is.Null);
            }
            finally
            {
                Call(route, "PairAfterglow", false); Set(route, "_horror", null);
                Set(driver, "_state", null); Set(driver, "_afterglow", null); glow.Clear();
                for (int i = owners.Count - 1; i >= 0; i--) Object.DestroyImmediate(owners[i]);
                Object.DestroyImmediate(config); Object.DestroyImmediate(profile);
            }
        }
        private static T Make<T>(List<GameObject> owners) where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); owners.Add(go); return go.AddComponent<T>(); }
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
