// ============================================================================
// PostFXGraceTests.cs
// ============================================================================
// PURPOSE:
//   Tests grace desaturation and catalogue blindness independently of the renderer.
//   The real Run relay subscription path is exercised with owned state injection.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · PostFX.
// KEY RESPONSIBILITIES:
//   - Verify easing, stale-end rejection, independent blindness and paired subscriptions.
// DEPENDENCIES:
//   - Core, PostFX, Run, PostFXOrchestrator, NUnit and reflection.
// USAGE NOTES:
//   Edit Mode; no volume, singleton creation or live scene mutation.
// ============================================================================
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Presentation.PostFX;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.PostFX
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PostFXGraceTests
    {
        private PostFXDriverConfig _config;
        private PostFXDriverState _state;
        private PostFXPresenter _presenter;
        [SetUp] public void SetUp()
        { _config = ScriptableObject.CreateInstance<PostFXDriverConfig>(); _state = new PostFXDriverState(); _presenter = new PostFXPresenter(); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_config);
        private GraceWindowFact Grace(long tick) => new GraceWindowFact(new EntityId(1), tick, tick + 60, HitSeverity.Heavy);

        [Test] public void GraceEasesInAndOutWithoutClearingStrongerIntrusion()
        {
            _presenter.SetGrace(_state, Grace(1), true);
            _presenter.Tick(_state, _config, _config.GraceEaseInSeconds / 2f);
            Assert.That(_state.Saturation, Is.EqualTo(-17.5f).Within(.0001f));
            _presenter.Tick(_state, _config, _config.GraceEaseInSeconds / 2f);
            Assert.That(_state.Saturation, Is.EqualTo(-35f));
            _presenter.SetGrace(_state, Grace(1), false);
            _presenter.Tick(_state, _config, _config.GraceEaseOutSeconds / 2f);
            Assert.That(_state.Saturation, Is.EqualTo(-17.5f).Within(.0001f));
            _presenter.Tick(_state, _config, _config.GraceEaseOutSeconds / 2f);
            Assert.That(_state.Saturation, Is.Zero);
            _presenter.SetGrace(_state, Grace(100), true);
            _presenter.SetGrace(_state, Grace(1), false);
            Assert.That(_state.GraceActive, Is.True);
            _presenter.PlayIntrusion(_state, 2f); _presenter.Tick(_state, _config, .2f);
            Assert.That(_state.Saturation, Is.EqualTo(-70f));
        }

        [Test] public void ExactBlindnessIdIsNeutralByDefaultAndDoesNotEraseTimedTrap()
        {
            _presenter.Tick(_state, _config, 0f); Assert.That(_state.Blackout, Is.Zero);
            _state.ActiveEffects = Effects("Blinded");
            _presenter.Tick(_state, _config, 0f); Assert.That(_state.Blackout, Is.Zero);
            _state.ActiveEffects = Effects("blinded");
            _presenter.Tick(_state, _config, 10f);
            Assert.That(_state.Blackout, Is.EqualTo(_config.BlindnessDarkness));
            Assert.That(_state.Saturation, Is.Zero);
            Assert.That(_state.Grain, Is.EqualTo(_config.BaselineGrain));
            _presenter.SetBlindness(_state, 2f); _state.ActiveEffects = default(ActiveEffects);
            _presenter.Tick(_state, _config, 1f); Assert.That(_state.Blackout, Is.GreaterThan(0f));
            _presenter.Tick(_state, _config, 1f); Assert.That(_state.Blackout, Is.Zero);
        }

        [Test] public void RunRelaysStartEndAndPairOnReconfigureDisable()
        {
            Assert.That(RunSessionManager.Instance, Is.Null);
            var owner = new GameObject("Grace relay test"); owner.SetActive(false);
            try
            {
                var run = owner.AddComponent<RunSessionManager>();
                var manager = owner.AddComponent<PostFXManager>(); var driver = owner.AddComponent<PostFXDriver>();
                var route = owner.AddComponent<PostFXOrchestrator>();
                Set(manager, "_initialized", true); Set(manager, "_driver", driver);
                Set(driver, "_state", _state); Set(driver, "_presenter", _presenter);
                route.Configure(run, manager);
                Invoke(route, "OnEnable");
                Assert.That(Subscribers(run, "OnGraceStarted"), Is.EqualTo(1));
                Publish(run, "OnGraceStarted", Grace(1)); Assert.That(_state.GraceActive, Is.True);
                Publish(run, "OnGraceEnded", Grace(1)); Assert.That(_state.GraceActive, Is.False);
                route.Configure(run, manager); Invoke(route, "OnEnable");
                Assert.That(Subscribers(run, "OnGraceStarted"), Is.EqualTo(1));
                Invoke(route, "OnDisable");
                Assert.That(Subscribers(run, "OnGraceStarted") + Subscribers(run, "OnGraceEnded"), Is.Zero);
                route.OnActiveEffectsChanged(Effects("blinded"));
                Assert.That(_presenter.HasBlindness(_state, _config), Is.True);
                Set(driver, "_state", null);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        private static ActiveEffects Effects(string id) => new ActiveEffects(new[] { new ActiveEffect(new EffectId(id), EffectKind.Curse, 1) });
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static int Subscribers(object target, string name) => (Field(target, name).GetValue(target) as Delegate)?.GetInvocationList().Length ?? 0;
        private static void Publish(object target, string name, object value) => (Field(target, name).GetValue(target) as Delegate)?.DynamicInvoke(value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
