// ============================================================================
// ExpeditionRosterTests.cs
// ============================================================================
// PURPOSE:
//   Checks roster admission, duplicate indices and run-scoped mutation retention.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Expedition.
// KEY RESPONSIBILITIES:
//   - Preserve selected catalogue keys, bounded safe capacity and late-spawn indices.
//   - Keep accepted mutations across floor replacement but not a new run.
// DEPENDENCIES:
//   - Core and pure Expedition logic, NUnit and Unity value types.
// USAGE NOTES:
//   Injected validation represents Procedural's first-contact policy; no scene is loaded.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Expedition;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Expedition
{
    public sealed class ExpeditionRosterTests
    {
        private ExpeditionSessionBehaviorState state;
        private ExpeditionSessionController controller;
        [SetUp] public void Setup()
        { state = new ExpeditionSessionBehaviorState(); controller = new ExpeditionSessionController(state); controller.Bind(SceneKey.HorrorRun); }
        private void Begin(int generation, params string[] keys)
        {
            controller.Queue(new ProgressionGenerationRequest(generation, 17, generation, false,
                new ProgressionEffects(1, 1, 1, 1, 100, 100, keys.Length, activeThreatIds: keys)));
            controller.Begin(generation);
        }
        [Test] public void SelectionOrderDuplicatesAndLateIndicesSurviveCoreRequests()
        {
            Begin(1, "echo", "weaver", "echo", "ticking", "rusher");
            var requests = controller.HunterSpawns("fallback", Enumerable.Range(0, 5).Select(i => Vector3.right * i).ToArray());
            Assert.That(requests.Select(r => r.ArchetypeKey), Is.EqualTo(new[] { "echo", "weaver", "echo", "ticking", "rusher" }));
            Assert.That(requests.Select(r => r.DuplicateIndex), Is.EqualTo(new[] { 0, 0, 1, 0, 0 }));
            controller.RecordPlayer(new EntityId(1));
            for (int i = 0; i < requests.Count; i++) controller.RecordHunter(new EntityId(-i - 1));
            controller.Ready(); Assert.That(controller.HunterSpawn("echo", Vector3.back).DuplicateIndex, Is.EqualTo(2));
        }
        [Test] public void EveryRetainedHunterSpawnsOnEachFloorWithNothingExtrasAppended()
        {
            for (int generation = 1; generation <= 3; generation++)
            {
                Begin(generation, "echo", "weaver", "echo");
                var requests = controller.HunterSpawns("fallback", Enumerable.Range(0, 5).Select(i => Vector3.right * i).ToArray(),
                    extraHunters: new[] { "ticking", "echo" });
                Assert.That(requests.Select(r => r.ArchetypeKey), Is.EqualTo(new[] { "echo", "weaver", "echo", "ticking", "echo" }));
                Assert.That(requests.Select(r => r.DuplicateIndex), Is.EqualTo(new[] { 0, 0, 1, 0, 2 }));
                controller.RecordPlayer(new EntityId(1));
                for (int i = 0; i < requests.Count; i++) controller.RecordHunter(new EntityId(-i - 1));
                controller.Ready(); controller.ReleaseActors();
            }
        }
        [Test] public void UnsafeAndDuplicatePlacementsNeverInflateCapacityOrPermitEarlyReadiness()
        {
            Begin(1, "echo", "weaver", "ticking");
            var requests = controller.HunterSpawns("fallback", new[] { Vector3.zero, Vector3.one, Vector3.one }, p => p != Vector3.zero);
            Assert.That(requests.Count, Is.EqualTo(1)); Assert.That(requests[0].Position, Is.EqualTo(Vector3.one));
            Assert.That(state.HunterSpawnShortfall, Is.EqualTo(2)); controller.RecordPlayer(new EntityId(1));
            Assert.Throws<InvalidOperationException>(controller.Ready); controller.RecordHunter(new EntityId(-1));
            Assert.Throws<InvalidOperationException>(controller.Ready, "A retained-hunter subset cannot be admitted.");
        }
        [Test] public void AcceptedMutationReplacesItsTunablePersistsAcrossFloorsAndClearsOnNewRun()
        {
            Begin(1, "echo"); controller.RecordHunter(new EntityId(-1));
            var mutation = new HunterMutation(HunterTunable.Acceleration, 17f, "tell");
            controller.RetainMutation(new HunterMutationFact(new EntityId(-2), "echo", "tell", 0, mutation));
            Assert.That(controller.RetainedMutations("echo"), Is.Empty);
            controller.RetainMutation(new HunterMutationFact(new EntityId(-1), "echo", "tell", 0, mutation));
            controller.RetainMutation(new HunterMutationFact(new EntityId(-1), "echo", "tell", 0, mutation));
            controller.RecordPlayer(new EntityId(1)); controller.Ready(); controller.ReleaseActors(); Begin(2, "echo");
            Assert.That(controller.RetainedMutations("echo").Count, Is.EqualTo(1));
            Assert.That(controller.HunterSpawn("echo", Vector3.zero).DuplicateIndex, Is.Zero);
            controller.ResetRun(); Assert.That(controller.RetainedMutations("echo"), Is.Empty);
        }
    }
}
