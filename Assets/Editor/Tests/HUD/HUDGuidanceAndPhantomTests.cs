// ============================================================================
// HUDGuidanceAndPhantomTests.cs
// ============================================================================
// PURPOSE:
//   Verifies separate guidance channels and reversible display-only phantom cakes.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · HUD.
// KEY RESPONSIBILITIES:
//   - Protect chase visibility, empty snapshots, camera bearings and phantom expiry/reset.
//   - Exercise Run-to-HUD routing and paired subscriptions without rendering.
// DEPENDENCIES:
//   Core, Run, HUD, HUDOrchestrator, NUnit and transient Unity objects.
// USAGE NOTES:
//   Edit Mode. Explicit callback invocation follows the existing routing fixtures.
// ============================================================================
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Presentation.HUD;
using Worsen.Session.Run;
using Object = UnityEngine.Object;

namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDGuidanceAndPhantomTests
    {
        private static GuidanceTarget[] Both => new[] {
            new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.forward, Vector3.forward),
            new GuidanceTarget(GuidanceKind.GoldenSense, Vector3.right, Vector3.right) };

        [Test]
        public void VisualSurfaceHasSeparateGoldenAndWhiteVisibilityOutsideChrome()
        {
            var go = new GameObject("typed arrow surface"); var visual = go.AddComponent<HUDVisualDriver>();
            var config = ScriptableObject.CreateInstance<HUDDriverConfig>(); var root = new VisualElement();
            try
            {
                visual.Bind(root, config);
                var state = new HUDDriverState(); var presenter = new HUDPresenter();
                presenter.SetGuidance(state, Both); presenter.SetChaseMode(state, true); visual.Apply(state);
                Assert.That(root.Q("direction-group").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("golden-direction-group").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("hud").style.display.value, Is.EqualTo(DisplayStyle.None));
                presenter.SetGuidance(state, Array.Empty<GuidanceTarget>()); visual.Apply(state);
                Assert.That(root.Q("direction-group").style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(root.Q("golden-direction-group").style.display.value, Is.EqualTo(DisplayStyle.None));
            }
            finally { visual.Unbind(); Object.DestroyImmediate(go); Object.DestroyImmediate(config); }
        }

        [Test]
        public void TypedChannelsRemainVisibleInChaseAndEmptySnapshotHidesBoth()
        {
            var state = new HUDDriverState(); var presenter = new HUDPresenter();
            presenter.SetGuidance(state, Both); presenter.SetChaseMode(state, true);
            presenter.SetHeading(state, 90f);
            Assert.That(state.DirectionVisible && state.GoldenSenseVisible, Is.True);
            Assert.That(state.ChromeVisible, Is.False);
            Assert.That(state.ArrowDegrees, Is.EqualTo(-90f).Within(.001f));
            Assert.That(state.GoldenSenseArrowDegrees, Is.EqualTo(0f).Within(.001f));
            presenter.SetGuidance(state, new[] { Both[1] });
            Assert.That(state.DirectionVisible, Is.False); Assert.That(state.GoldenSenseVisible, Is.True);
            presenter.SetGuidance(state, Array.Empty<GuidanceTarget>());
            Assert.That(state.DirectionVisible || state.GoldenSenseVisible, Is.False, "Blind Faith publishes an empty snapshot.");
        }

        [Test]
        public void PhantomDoesNotChangeAuthoritativeCountAndExpiresEvenDuringChase()
        {
            var state = new HUDDriverState(); var presenter = new HUDPresenter();
            Assert.That(presenter.TryShowPhantomCake(state, 1f), Is.False);
            presenter.SetCount(state, 2, 5);
            Assert.That(presenter.TryShowPhantomCake(state, 1f), Is.True);
            Assert.That(state.CountText, Is.EqualTo("Cakes: 3 / 5")); Assert.That(state.Collected, Is.EqualTo(2));
            presenter.SetCount(state, 4, 5);
            Assert.That(state.CountText, Is.EqualTo("Cakes: 5 / 5"));
            presenter.SetChaseMode(state, true); presenter.Tick(state, 1f, .5f);
            Assert.That(state.CountText, Is.EqualTo("Cakes: 4 / 5")); Assert.That(state.Collected, Is.EqualTo(4));
            presenter.ResetRunView(state);
            Assert.That(presenter.TryShowPhantomCake(state, 2f), Is.True);
            presenter.ResetRunView(state); Assert.That(state.CountText, Is.EqualTo("Cakes: 4 / 5"));
            Assert.That(presenter.TryShowPhantomCake(state, float.NaN), Is.False);
            Assert.That(presenter.TryShowPhantomCake(state, 0f), Is.False);
        }

        [Test]
        public void RoutingReplacesBothChannelsWithoutLegacyDisplayOverrideAndPairsSubscriptions()
        {
            var runObject = new GameObject("guidance run"); runObject.SetActive(false);
            var hudObject = new GameObject("guidance hud"); hudObject.SetActive(false);
            var routeObject = new GameObject("guidance route"); routeObject.SetActive(false);
            var run = runObject.AddComponent<RunSessionManager>();
            var hud = hudObject.AddComponent<HUDManager>(); var driver = hudObject.AddComponent<HUDDriver>();
            var route = routeObject.AddComponent<HUDOrchestrator>();
            var state = new HUDDriverState();
            Set(driver, "_state", state); Set(driver, "_presenter", new HUDPresenter());
            Set(hud, "_driver", driver); Set(hud, "_initialized", true);
            Set(route, "_run", run); Set(route, "_hud", hud);
            try
            {
                Call(route, "OnEnable"); Call(route, "OnEnable");
                Assert.That(((Delegate)Field(run, "GuidanceChanged").GetValue(run)).GetInvocationList().Length, Is.EqualTo(1));
                Publish(run, "GuidanceChanged", (object)Both);
                Publish(run, "FloorDisplayChanged", new FloorDisplaySnapshot(1, 4, 1, ExitState.Open, false, Vector3.back));
                Publish(run, "ChaseStarted", default(ChaseFact));
                Assert.That(state.DirectionVisible && state.GoldenSenseVisible, Is.True);
                Assert.That(state.WorldDirection, Is.EqualTo(Vector3.forward));
                Publish(run, "GuidanceChanged", (object)Array.Empty<GuidanceTarget>());
                Assert.That(state.DirectionVisible || state.GoldenSenseVisible, Is.False);
                Assert.That(hud.TryShowPhantomCake(1f), Is.False, "Disabled/unbound UI must refuse the micro-event.");
                Call(route, "OnDisable"); Assert.That(Field(run, "GuidanceChanged").GetValue(run), Is.Null);
            }
            finally { Call(route, "OnDisable"); Object.DestroyImmediate(routeObject); Object.DestroyImmediate(hudObject); Object.DestroyImmediate(runObject); }
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Publish(object target, string name, params object[] args) => (Field(target, name).GetValue(target) as Delegate)?.DynamicInvoke(args);
    }
}
