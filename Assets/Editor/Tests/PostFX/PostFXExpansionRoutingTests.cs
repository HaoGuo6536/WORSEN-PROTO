// ============================================================================
// PostFXExpansionRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises accepted Blinder facts and configured rim restoration through routing.
//   Tests retain pure presentation state without enabling a render pipeline.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · PostFX.
// KEY RESPONSIBILITIES:
//   - Verify one paired blindness receiver and receiver-owned Mirror Skin scaling.
//   - Verify default-off rim and configured strength after capture reset.
// DEPENDENCIES:
//   Core, Run, PostFX, Environment, Orchestrator, NUnit and transient Unity objects.
// USAGE NOTES:
//   Native Edit Mode fixture; reflection injects state, not substitute manager behavior.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;
using Worsen.Presentation.PostFX;
using Worsen.Presentation.Environment;
using Worsen.Orchestrator;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.PostFX
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PostFXExpansionRoutingTests
    {
        [TestCase(false)] [TestCase(true)]
        public void BlinderUsesOneRawDurationAndCaptureRestoresApprovedRim(bool rimEnabled)
        {
            var owned = new List<GameObject>();
            var config = ScriptableObject.CreateInstance<PostFXDriverConfig>();
            var environmentConfig = ScriptableObject.CreateInstance<EnvironmentDriverConfig>();
            var run = Make<RunSessionManager>(owned); var post = Make<PostFXManager>(owned);
            var driver = post.gameObject.AddComponent<PostFXDriver>();
            var volumeType = Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime", true);
            Set(driver, "_volume", post.gameObject.AddComponent(volumeType));
            driver.Initialize(config); var state = Read<PostFXDriverState>(driver, "_state");
            Set(post, "_driver", driver); Set(post, "_initialized", true);
            var environment = Make<EnvironmentManager>(owned); var environmentDriver = environment.GetComponent<EnvironmentDriver>();
            Set(environment, "_driver", environmentDriver); Set(environmentDriver, "_config", environmentConfig);
            Set(environmentConfig, "_hunterRimEnabled", rimEnabled); Set(environmentConfig, "_hunterRimStrength", .08f);
            var route = Make<PostFXOrchestrator>(owned);
            try
            {
                route.Configure(run, post, environment: environment); Call(route, "OnEnable"); Call(route, "OnEnable");
                Assert.That(Read<Delegate>(run, "BlinderHitPublished").GetInvocationList().Length, Is.EqualTo(1));
                post.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(new EffectId("mirror-skin"), EffectKind.Upgrade, 1) }));
                Read<Delegate>(run, "BlinderHitPublished").DynamicInvoke(new BlinderHitFact(new EntityId(7), new EntityId(1), 1, 1, 6f, false));
                Assert.That(state.BlindnessRemaining, Is.EqualTo(3f));
                Assert.That(state.HunterRim, Is.EqualTo(rimEnabled ? .08f : 0f));
                post.ResetEffects(); Assert.That(state.HunterRim, Is.Zero);
                Read<Delegate>(run, "CaptureStarted").DynamicInvoke(default(RunCaptureMetadata));
                Assert.That(state.HunterRim, Is.EqualTo(rimEnabled ? .08f : 0f));
                Assert.That(Read<Delegate>(run, "BlinderHitPublished").GetInvocationList().Length, Is.EqualTo(1));
                Call(route, "OnDisable"); Assert.That(Read<Delegate>(run, "BlinderHitPublished"), Is.Null);
            }
            finally
            {
                Call(route, "OnDisable");
                driver.Teardown();
                for (int i = owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(owned[i]);
                Object.DestroyImmediate(config); Object.DestroyImmediate(environmentConfig);
            }
        }
        private static T Make<T>(List<GameObject> owned) where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); owned.Add(go); return go.AddComponent<T>(); }
        private static T Read<T>(object target, string field) => (T)Worsen.Tests.Run.RunFactRelayTestUtility.Read(target, field);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
