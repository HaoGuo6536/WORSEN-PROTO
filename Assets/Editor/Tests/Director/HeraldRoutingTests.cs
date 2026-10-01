// ============================================================================
// HeraldRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the typed Herald route preserves the gameplay clue, not its sound origin.
//   Receiver-spy coverage complements the typed Director admission and fan-out tests.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Director.
// KEY RESPONSIBILITIES:
//   - Preserve typed payloads once, reject missing bindings and pair publisher rebinding.
// DEPENDENCIES:
//   Run hunter relay, HeraldOrchestrator, Core, NUnit and transient Unity objects.
// USAGE NOTES:
//   Coordinator-run Edit Mode; this fixture does not claim live AI hearing works.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;
using Worsen.Orchestrator;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Director
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HeraldRoutingTests
    {
        [Test] public void TypedBroadcastKeepsClueAndObservationAgeAndPairsSubscriptions()
        {
            var owner = new GameObject("Herald route"); owner.SetActive(false);
            var run = owner.AddComponent<RunSessionManager>(); var route = owner.AddComponent<HeraldOrchestrator>();
            var received = new List<HeraldScreamFact>();
            try
            {
                route.Configure(run, received.Add); Call(route, "OnEnable"); Call(route, "OnEnable");
                var publisher = (Delegate)typeof(RunHunterFactRelayController).GetField("HeraldScreamPublished", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run.HunterFacts);
                Assert.That(publisher.GetInvocationList(), Has.Length.EqualTo(1));
                var fact = new HeraldScreamFact(new EntityId(7), HeraldSound.ChaseOne, "call", 1f,
                    new NoiseEvent(new EntityId(7), Vector3.zero, 1f, 20, NoiseSourceKind.Scream),
                    new NoiseEvent(new EntityId(7), Vector3.one, 1f, 20, NoiseSourceKind.Scream), 9, false);
                publisher.DynamicInvoke(fact);
                Assert.That(received, Has.Count.EqualTo(1));
                Assert.That(received[0].FloorWideHint.Position, Is.EqualTo(Vector3.one));
                Assert.That(received[0].Noise.Position, Is.EqualTo(Vector3.zero));
                Assert.That(received[0].ObservedTick, Is.EqualTo(9));
                Assert.That(received[0].FloorWideHint.Origin, Is.EqualTo(NoiseOrigin.Unspecified));
                Assert.That(HunterHearingUtility.Allows(received[0].FloorWideHint), Is.False,
                    "Ordinary hearing stays closed; broadcasts use the separate typed ingress.");
                Call(route, "OnDisable"); Assert.That(Read(run), Is.Null);
                route.Configure(run, null); Call(route, "OnEnable"); Assert.That(Read(run), Is.Null);
            }
            finally { Call(route, "OnDisable"); UnityEngine.Object.DestroyImmediate(owner); }
        }
        private static object Read(RunSessionManager run) => typeof(RunHunterFactRelayController).GetField("HeraldScreamPublished", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run.HunterFacts);
        private static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
