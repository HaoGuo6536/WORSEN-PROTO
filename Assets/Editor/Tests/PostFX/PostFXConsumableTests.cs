// ============================================================================
// PostFXConsumableTests.cs
// ============================================================================
// PURPOSE:
//   Verifies targeted cleanse and revival through real paired Session subscriptions.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · PostFX.
// KEY RESPONSIBILITIES:
//   - Suppress only currently blinded identities until removed; admit fresh blindness.
//   - Clear consumption without resetting injury, proximity, grace or intrusion.
// DEPENDENCIES:
//   Core, PostFX, PostFXOrchestrator, Run, HorrorEffects, NUnit and reflection.
// USAGE NOTES:
//   Coordinator-run Edit Mode; owns and tears down transient volume resources.
// ============================================================================
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Presentation.PostFX;
using Worsen.Session.Run;
using Worsen.Session.HorrorEffects;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.PostFX
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PostFXConsumableTests
    {
        private GameObject owner;
        private PostFXManager post;
        private PostFXOrchestrator route;
        private HorrorEffectsManager effects;
        private PostFXDriverConfig config;
        private PostFXDriverState state;
        private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Publish(object target, string name, object fact) => ((Delegate)Get(target, name)).DynamicInvoke(fact);
        private static ActiveEffects View(params string[] ids) => new ActiveEffects(Array.ConvertAll(ids, id => new ActiveEffect(new EffectId(id), EffectKind.Curse, 1)));
        [SetUp] public void Setup()
        {
            owner = new GameObject("PostFX consumable routes"); owner.SetActive(false);
            var run = owner.AddComponent<RunSessionManager>(); effects = owner.AddComponent<HorrorEffectsManager>();
            post = owner.AddComponent<PostFXManager>(); var driver = owner.AddComponent<PostFXDriver>(); driver.ConfigureForSetup();
            config = ScriptableObject.CreateInstance<PostFXDriverConfig>();
            Set(config, "_blindnessEffectIds", new[] { "blinded", "new-blind" });
            Set(post, "_config", config); Set(post, "_driver", driver); post.Initialize();
            state = (PostFXDriverState)Get(driver, "_state");
            route = owner.AddComponent<PostFXOrchestrator>(); route.Configure(run, post, effects: effects);
            Call(route, "OnEnable"); Call(route, "OnEnable");
            foreach (string name in new[] { "SensesCleansed", "PlayerRevived" })
                Assert.That(((Delegate)Get(effects, name)).GetInvocationList().Length, Is.EqualTo(1));
        }
        [TearDown] public void Cleanup()
        { if (route != null) Call(route, "OnDisable"); if (post != null) post.Teardown(); Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        [Test] public void CleanseClearsTimerAndCurrentViewButNotNewBlindnessOrOtherEffects()
        {
            post.SetActiveEffects(View("blinded")); post.SetBlindness(5f);
            post.SetInjury(50f, 100f); post.SetProximity(.6f); post.PlayIntrusion(3f);
            Publish(effects, "SensesCleansed", new SensoryCleanseFact(new EntityId(1), 1));
            Assert.That(state.BlindnessRemaining, Is.Zero); Assert.That(state.Blackout, Is.Zero);
            var presenter = new PostFXPresenter(); presenter.Tick(state, config, .1f);
            Assert.That(state.Blackout, Is.Zero); Assert.That(state.Injury, Is.EqualTo(.5f)); Assert.That(state.Proximity, Is.EqualTo(.6f));
            Assert.That(state.IntrusionRemaining, Is.GreaterThan(0f));
            post.SetActiveEffects(View("blinded", "new-blind")); presenter.Tick(state, config, config.BlindnessOnsetSeconds);
            Assert.That(state.Blackout, Is.GreaterThan(0f));
            post.SetActiveEffects(View()); post.SetActiveEffects(View("blinded")); presenter.Tick(state, config, 0f);
            Assert.That(state.Blackout, Is.GreaterThan(0f), "Removal ends suppression for a later application of the same identity.");
            Call(route, "OnDisable");
            Assert.That(Get(effects, "SensesCleansed"), Is.Null); Assert.That(Get(effects, "PlayerRevived"), Is.Null);
        }
        [Test] public void RevivalRearmsConsumptionWithoutResettingUnrelatedInputs()
        {
            post.SetInjury(50f, 100f); post.SetProximity(.6f); post.PlayIntrusion(3f); post.SetBlindness(2f);
            post.SetGrace(new GraceWindowFact(new EntityId(1), 1, 61, HitSeverity.Heavy), true);
            post.PlayConsumed(.9f); Assert.That(state.Consumed, Is.True);
            Publish(effects, "PlayerRevived", new EntityId(1));
            Assert.That(state.Consumed, Is.False); Assert.That(state.ConsumptionElapsed, Is.Zero);
            Assert.That(state.Injury, Is.EqualTo(.5f)); Assert.That(state.Proximity, Is.EqualTo(.6f));
            Assert.That(state.IntrusionRemaining, Is.EqualTo(3f)); Assert.That(state.BlindnessRemaining, Is.EqualTo(2f)); Assert.That(state.GraceActive, Is.True);
            post.PlayConsumed(.9f); Assert.That(state.Consumed, Is.True);
        }
    }
}
