// ============================================================================
// HorrorWebTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Weaver visual lifetimes, hunter isolation and teardown ownership.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Horror.
// KEY RESPONSIBILITIES:
//   - Cover warning cancellation, doorway/glow deduplication and renderer cleanup.
//   - Check paired routing across reconfiguration and disable.
// DEPENDENCIES:
//   - Core, Horror, Orchestrator, Session publishers, Input, NUnit and Unity objects.
// USAGE NOTES:
//   Edit Mode with transient objects; not evidence of material appearance in builds.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Horror;
using Worsen.Presentation.Input;
using Worsen.Session.Run;
using Worsen.Session.Progression;
using Worsen.Orchestrator;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Horror
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HorrorWebTests
    {
        private static WeaverFact Fact(int hunter, WeaverFactKind kind, long tick, int serial = 0) =>
            new WeaverFact(new EntityId(hunter), kind, tick, Vector3.one, Vector3.forward, .6f, 1f, serial);
        [Test] public void WarningsCancelPerHunterAndDoorwayGlowDoesNotDuplicateTheQuad()
        {
            var presenter = new HorrorWebPresenter(); var state = new HorrorWebDriverState();
            presenter.Observe(state, Fact(4, WeaverFactKind.WetClick, 1)); presenter.Observe(state, Fact(5, WeaverFactKind.WetClick, 1));
            presenter.Tick(state, .25f); presenter.Observe(state, Fact(4, WeaverFactKind.WetClick, 1));
            Assert.That(state.Visuals[(new EntityId(4), WeaverFactKind.WetClick, 0)].Remaining, Is.EqualTo(.75f));
            presenter.Observe(state, Fact(4, WeaverFactKind.WarningCancelled, 2));
            presenter.Observe(state, Fact(4, WeaverFactKind.WetClick, 1)); Assert.That(state.Visuals.Count, Is.EqualTo(1));
            presenter.Observe(state, Fact(5, WeaverFactKind.WebLaunched, 2, 1)); Assert.That(state.Visuals, Is.Empty);
            presenter.Observe(state, Fact(4, WeaverFactKind.DoorwayWebbed, 3, 1));
            presenter.Observe(state, Fact(4, WeaverFactKind.WebGlow, 3, 1)); Assert.That(state.Visuals.Count, Is.EqualTo(1));
            presenter.Observe(state, Fact(5, WeaverFactKind.WebGlow, 3, 1)); Assert.That(state.Visuals.Count, Is.EqualTo(2));
            presenter.Tick(state, 0f); Assert.That(state.Visuals.Count, Is.EqualTo(2));
            presenter.Tick(state, 1f); Assert.That(state.Visuals, Is.Empty);
            presenter.Reset(state); Assert.That(state.Ticks, Is.Empty);
        }
        [Test] public void DriverCreatesLinesAndDoorwaysThenDestroysThemOnExpiryResetAndTeardown()
        {
            var go = new GameObject("Web test"); var config = ScriptableObject.CreateInstance<HorrorDriverConfig>();
            var driver = go.AddComponent<HorrorWebDriver>();
            try
            {
                driver.Initialize(config); driver.Observe(Fact(4, WeaverFactKind.WetClick, 1));
                Assert.That(go.GetComponentsInChildren<LineRenderer>().Length, Is.EqualTo(1));
                driver.Observe(Fact(5, WeaverFactKind.DoorwayWebbed, 1, 1)); Assert.That(driver.VisualCount, Is.EqualTo(2));
                Assert.That(go.GetComponentsInChildren<Collider>(), Is.Empty);
                driver.Tick(1f); Assert.That(driver.VisualCount, Is.Zero);
                Assert.That(go.GetComponentsInChildren<Renderer>(), Is.Empty);
                driver.Observe(Fact(4, WeaverFactKind.WebGlow, 2, 1)); driver.Reset(); Assert.That(driver.VisualCount, Is.Zero);
                driver.Observe(Fact(4, WeaverFactKind.WetClick, 3)); driver.Teardown(); Assert.That(driver.VisualCount, Is.Zero);
                Assert.That(go.transform.childCount, Is.Zero);
            }
            finally { driver.Teardown(); Object.DestroyImmediate(go); Object.DestroyImmediate(config); }
        }
        [Test] public void RoutePairsWeaverPublisherAcrossConfigureAndDisable()
        {
            var owned = new List<GameObject>(); var run = Make<RunSessionManager>(owned);
            var progression = Make<ProgressionSessionManager>(owned); var input = Make<InputManager>(owned);
            var horror = Make<HorrorManager>(owned); var route = Make<HorrorOrchestrator>(owned);
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    route.Configure(run, progression, input, horror); Invoke(route, "OnEnable");
                    var published = typeof(RunHunterFactRelayController).GetField("WeaverFactPublished", BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.That(((Delegate)published.GetValue(run.HunterFacts)).GetInvocationList().Length, Is.EqualTo(1));
                }
                Invoke(route, "OnDisable");
                Assert.That(typeof(RunHunterFactRelayController).GetField("WeaverFactPublished", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run.HunterFacts), Is.Null);
            }
            finally { Invoke(route, "OnDisable"); for (int i = owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(owned[i]); }
        }
        private static T Make<T>(List<GameObject> owned) where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); owned.Add(go); return go.AddComponent<T>(); }
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
