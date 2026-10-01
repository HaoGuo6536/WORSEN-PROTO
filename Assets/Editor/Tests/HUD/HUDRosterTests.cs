// ============================================================================
// HUDRosterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies independent Ticking bearings and retained shield facts without HUD text.
//   Shield routing remains intact while numerical protection stays off the run surface.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · HUD and Run relay.
// KEY RESPONSIBILITIES:
//   - Check chase visibility, stale facts, document cleanup and paired shield routes.
// DEPENDENCIES:
//   - Core, HUD, Run, Player test state, Orchestrator, UI Toolkit and NUnit.
// USAGE NOTES:
//   Edit Mode only. Uses transient objects and explicit lifecycle calls, not scenes.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Presentation.HUD;
using Worsen.Session.Run;
using Worsen.Orchestrator;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDRosterTests
    {
        private static TickingGuidanceFact Threat(int id, bool active, long tick) => new TickingGuidanceFact(
            new GuidanceTarget(GuidanceKind.ThreatArrow, Vector3.right, Vector3.one, entityId: new EntityId(id)), active, tick);
        [Test] public void IndependentThreatsSurviveChaseButNotRemovalOrReset()
        {
            var presenter = new HUDPresenter(); var state = new HUDDriverState();
            presenter.SetGuidance(state, new[] { new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.forward, Vector3.zero) });
            presenter.SetThreat(state, Threat(4, true, 1)); presenter.SetThreat(state, Threat(5, true, 1));
            presenter.SetChaseMode(state, true); presenter.SetHeading(state, 90);
            Assert.That(state.Threats[new EntityId(4)].Visible, Is.True);
            Assert.That(state.Threats[new EntityId(4)].ArrowDegrees, Is.EqualTo(0).Within(.001));
            Assert.That(state.WorldDirection, Is.EqualTo(Vector3.forward));
            presenter.SetThreat(state, Threat(4, false, 1)); presenter.SetThreat(state, Threat(4, true, 1));
            Assert.That(state.Threats[new EntityId(4)].Visible, Is.False);
            Assert.That(state.Threats[new EntityId(5)].Visible, Is.True);
            presenter.SetThreat(state, Threat(4, true, 2)); Assert.That(state.Threats[new EntityId(4)].Visible, Is.True);
            presenter.SetShield(state, 23.5f); Assert.That(state.ShieldText, Is.EqualTo("Shield: 23.5"));
            presenter.ResetRunView(state); Assert.That(state.Threats, Is.Empty); Assert.That(state.Shield, Is.Zero);
        }
        [Test] public void VisualThreatsSurviveChaseWithoutShieldTextAndUnbindCleanly()
        {
            var go = new GameObject("Roster HUD"); var config = ScriptableObject.CreateInstance<HUDDriverConfig>();
            var visual = go.AddComponent<HUDVisualDriver>(); var root = new VisualElement();
            try
            {
                var state = new HUDDriverState(); var presenter = new HUDPresenter();
                presenter.SetThreat(state, Threat(4, true, 1)); presenter.SetChaseMode(state, true); presenter.SetShield(state, 12);
                visual.Bind(root, config); visual.Apply(state);
                Assert.That(root.Q("hud").style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(root.Q("threat-4").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("shield"), Is.Null);
                Assert.That(state.Shield, Is.EqualTo(12), "Decluttering must not erase routed shield facts.");
                presenter.SetThreat(state, Threat(4, false, 2)); visual.Apply(state);
                Assert.That(root.Q("threat-4").style.display.value, Is.EqualTo(DisplayStyle.None));
                visual.Unbind(); Assert.That(root.childCount, Is.Zero);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(config); }
        }
        [Test] public void ShieldSnapshotChangesAndRebindingUseTheRunRelay()
        {
            Assert.That(RunSessionManager.Instance, Is.Null);
            var owned = new List<GameObject>(); var config = ScriptableObject.CreateInstance<HUDDriverConfig>();
            var run = Make<RunSessionManager>(owned); var player = Make<PlayerManager>(owned);
            var playerState = new PlayerBehaviorState { Id = new EntityId(7), Shield = 40 };
            Set(player, "_state", playerState); Get<List<PlayerManager>>(run, "players").Add(player);
            var hud = Make<HUDManager>(owned); var driver = hud.gameObject.AddComponent<HUDDriver>(); var state = new HUDDriverState();
            Set(driver, "_state", state); Set(driver, "_presenter", new HUDPresenter()); Set(driver, "_config", config);
            Set(hud, "_driver", driver); Set(hud, "_initialized", true);
            var route = Make<HUDOrchestrator>(owned); Set(route, "_run", run); Set(route, "_hud", hud);
            try
            {
                Invoke(run, "OnEnable"); Invoke(run, "OnEnable"); Invoke(route, "OnEnable"); Invoke(route, "OnEnable");
                Assert.That(state.Shield, Is.EqualTo(40));
                Assert.That(Get<Delegate>(player, "OnShieldChanged").GetInvocationList().Length, Is.EqualTo(1));
                Assert.That(Get<Delegate>(run, "ShieldChanged").GetInvocationList().Length, Is.EqualTo(1));
                Get<Delegate>(player, "OnShieldChanged").DynamicInvoke(player.Id, 15f);
                Assert.That(state.ShieldText, Is.EqualTo("Shield: 15"));
                Get<Delegate>(run, "TickingGuidancePublished").DynamicInvoke(Threat(4, true, 1));
                Assert.That(state.Threats.Count, Is.EqualTo(1));
                playerState.Shield = 9; Get<Delegate>(run, "CaptureStarted").DynamicInvoke(default(RunCaptureMetadata));
                Assert.That(state.Shield, Is.EqualTo(9)); Assert.That(state.Threats, Is.Empty);
            }
            finally
            {
                Invoke(route, "OnDisable"); Invoke(run, "UnsubscribeGameplay");
                Assert.That(Get<Delegate>(player, "OnShieldChanged"), Is.Null);
                Assert.That(Get<Delegate>(run, "ShieldChanged"), Is.Null);
                Assert.That(Get<Delegate>(run, "TickingGuidancePublished"), Is.Null);
                for (int i = owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(owned[i]);
                Object.DestroyImmediate(config);
            }
        }
        private static T Make<T>(List<GameObject> owned) where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); owned.Add(go); return go.AddComponent<T>(); }
        private static T Get<T>(object target, string name) => (T)Worsen.Tests.Run.RunFactRelayTestUtility.Read(target, name);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
