// ============================================================================
// HunterArchetypeStateViewTests.cs
// ============================================================================
// PURPOSE:
//   Checks the read-only views of archetype state constructed by HunterManager.
//   Views stay live while exposing no mutable context, queues or collection handles.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify controller hand-outs and getter-only scalar contracts for all three views.
// DEPENDENCIES:
//   - Hunter Weaver/Blinder/Herald, NUnit and transient Unity configurations.
// USAGE NOTES:
//   Edit Mode; no entity, driver, scene or shared registry is constructed.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Weaver;
using Worsen.Domain.Hunter.Archetypes.Blinder;
using Worsen.Domain.Hunter.Archetypes.Herald;

namespace Worsen.Tests.Hunter
{
    public sealed class HunterArchetypeStateViewTests
    {
        [TestCase(typeof(IReadOnlyWeaverState))]
        [TestCase(typeof(IReadOnlyBlinderState))]
        [TestCase(typeof(IReadOnlyHeraldState))]
        public void ContractsExposeOnlyReadOnlyScalarProperties(Type view)
        {
            Assert.That(view.IsInterface, Is.True);
            Assert.That(view.GetProperties(), Is.Not.Empty);
            foreach (var property in view.GetProperties())
            {
                Assert.That(property.CanWrite, Is.False);
                Assert.That(property.PropertyType.IsPrimitive || property.PropertyType.IsEnum, Is.True);
            }
        }
        [Test]
        public void ControllerViewsObserveTheirOwnStateAndNeverAnotherSpawn()
        {
            var profile = ScriptableObject.CreateInstance<HunterProfile>();
            var weaverConfig = ScriptableObject.CreateInstance<WeaverConfig>();
            var blinderConfig = ScriptableObject.CreateInstance<BlinderConfig>();
            var heraldConfig = ScriptableObject.CreateInstance<HeraldConfig>();
            try
            {
                var weaver = new WeaverBehaviorState(); var blinder = new BlinderBehaviorState(); var herald = new HeraldBehaviorState();
                IReadOnlyWeaverState w = new WeaverController(weaver, weaverConfig, profile, new System.Random(1)).ReadOnlyState;
                IReadOnlyBlinderState b = new BlinderController(blinder, blinderConfig, profile).ReadOnlyState;
                IReadOnlyHeraldState h = new HeraldController(herald, heraldConfig, new System.Random(1)).ReadOnlyState;
                weaver.LastTick = blinder.LastTick = herald.LastTick = 7;
                weaver.Warning = blinder.Warning = herald.Warning = true;
                weaver.Fire = weaver.Hold = blinder.Fire = true; weaver.Ceiling = false;
                weaver.Action = WeaverAction.Reposition; blinder.Action = BlinderAction.Reposition;
                Assert.That(w.LastTick, Is.EqualTo(7)); Assert.That(b.LastTick, Is.EqualTo(7)); Assert.That(h.LastTick, Is.EqualTo(7));
                Assert.That(w.Warning && b.Warning && h.Warning, Is.True);
                Assert.That(w.Fire && w.Hold && b.Fire, Is.True); Assert.That(w.Ceiling, Is.False);
                Assert.That(w.Action, Is.EqualTo(WeaverAction.Reposition)); Assert.That(b.Action, Is.EqualTo(BlinderAction.Reposition));
                Assert.That(new WeaverController(new WeaverBehaviorState(), weaverConfig, profile, new System.Random(1)).ReadOnlyState.Warning, Is.False);
                Assert.That(new BlinderController(new BlinderBehaviorState(), blinderConfig, profile).ReadOnlyState.Warning, Is.False);
                Assert.That(new HeraldController(new HeraldBehaviorState(), heraldConfig, new System.Random(1)).ReadOnlyState.Warning, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile); UnityEngine.Object.DestroyImmediate(weaverConfig);
                UnityEngine.Object.DestroyImmediate(blinderConfig); UnityEngine.Object.DestroyImmediate(heraldConfig);
            }
        }
    }
}
