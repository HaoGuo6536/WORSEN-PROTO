// ============================================================================
// HUDOrchestratorHealthTests.cs
// ============================================================================
// PURPOSE:
//   Verifies initial and subsequent health through Run's public snapshot boundary.
//   The HUD uses real Driver/Presenter state without creating a UI document.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · HUD.
// KEY RESPONSIBILITIES:
//   - Seed late HUD subscriptions, retain current/maximum and pair teardown.
// DEPENDENCIES:
//   Player, Run, HUD, HUDOrchestrator, Unity objects, reflection and NUnit.
// USAGE NOTES:
//   Coordinator-run Edit Mode; synthetic Player state avoids spawning a motor.
//   Managers and Drivers teardown explicitly before owner destruction.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Session.Run;
using Worsen.Presentation.HUD;
using Worsen.Orchestrator;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDOrchestratorHealthTests
    {
        [Test] public void LateSubscriptionSeedsHealthAndUpdatesWithoutDuplicateHandlers()
        {
            var owner = new GameObject("Health route test"); owner.SetActive(false);
            var config = ScriptableObject.CreateInstance<HUDDriverConfig>();
            var run = owner.AddComponent<RunSessionManager>(); var player = owner.AddComponent<PlayerManager>();
            var hud = owner.AddComponent<HUDManager>(); var driver = owner.AddComponent<HUDDriver>();
            var route = owner.AddComponent<HUDOrchestrator>();
            try
            {
                Assert.That(RunSessionManager.Instance, Is.Null);
                var health = new PlayerBehaviorState { Id = new EntityId(1), Health = 42f, MaxHealth = 125f };
                Set(player, "_state", health);
                ((List<PlayerManager>)Read(run, "players")).Add(player);
                var state = new HUDDriverState();
                Set(driver, "_state", state); Set(driver, "_presenter", new HUDPresenter()); Set(driver, "_config", config);
                Set(hud, "_driver", driver); Set(hud, "_initialized", true);
                Set(route, "_run", run); Set(route, "_hud", hud);
                Call(route, "OnEnable"); Call(route, "OnEnable");
                Assert.That(state.HealthText, Is.EqualTo("42 / 125"));
                Assert.That(((Delegate)Read(run, "HealthChanged")).GetInvocationList(), Has.Length.EqualTo(1));
                health.Health = 20f; run.PublishHealthSnapshot();
                Assert.That(state.HealthText, Is.EqualTo("20 / 125"));
                hud.SetModalOpen(true); Assert.That(state.HealthKnown, Is.True); Assert.That(state.HealthText, Is.EqualTo("20 / 125"));
                Call(route, "OnDisable"); Assert.That(Read(run, "HealthChanged"), Is.Null);
                health.Health = 100f; run.PublishHealthSnapshot(); Assert.That(state.HealthText, Is.EqualTo("20 / 125"));
                ((List<PlayerManager>)Read(run, "players")).Clear(); run.PublishHealthSnapshot();
            }
            finally
            {
                Call(route, "OnDisable"); driver.Teardown(); player.Teardown();
                Object.DestroyImmediate(owner); Object.DestroyImmediate(config);
            }
        }
        private static object Read(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
