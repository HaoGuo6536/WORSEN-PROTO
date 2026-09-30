// ============================================================================
// ExpeditionThemeConsumerTests.cs
// ============================================================================
// PURPOSE:
//   Checks committed puzzle inputs and floor-scoped theme subscription pairing.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Expedition.
// KEY RESPONSIBILITIES:
//   - Reject duplicate/stale/foreign movement and validate identified vault outcomes.
//   - Check run-seed wiring and paired Procedural/Run event subscriptions.
// DEPENDENCIES:
//   NUnit, Core, Expedition, Run, Progression, Floor and Procedural test seams.
// USAGE NOTES:
//   Edit Mode; inactive temporary Managers avoid scene assembly. Source checks
//   supplement runtime rules; the missing Core vault identity remains an owner gate.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Procedural;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Expedition
{
    public sealed class ExpeditionThemeConsumerTests
    {
        private static ExpeditionSessionController Ready(out ExpeditionSessionBehaviorState state)
        {
            state = new ExpeditionSessionBehaviorState(); var c = new ExpeditionSessionController(state);
            c.Bind(SceneKey.HorrorRun);
            c.Queue(new ProgressionGenerationRequest(1, 17, 1, false, new ProgressionEffects(1f, 1f, 1f, 1f, 100f, 100f, 0)));
            c.Begin(1); c.RecordPlayer(new EntityId(1)); c.Ready(); return c;
        }
        private static PlayerMovementSample Movement(int id, long tick) => new PlayerMovementSample(new EntityId(id), tick,
            Vector3.one, Vector3.right, Vector3.up, 0f, Vector2.zero, false, MovementState.Ground, 0f);
        [Test]
        public void OnlyMatchingCommittedMovementTicksOnceAndReleaseClearsIt()
        {
            var c = Ready(out _);
            c.ObservePuzzleMovement(Movement(2, 1)); Assert.That(c.TryTickPuzzles(.02f, 1, out _), Is.False);
            c.ObservePuzzleMovement(Movement(1, 1)); Assert.That(c.TryTickPuzzles(float.NaN, 1, out _), Is.False);
            Assert.That(c.TryTickPuzzles(.02f, 2, out _), Is.False);
            Assert.That(c.TryTickPuzzles(.02f, 1, out var sample), Is.True);
            Assert.That(sample.Position, Is.EqualTo(Vector3.one));
            c.ObservePuzzleMovement(Movement(1, 1)); Assert.That(c.TryTickPuzzles(.02f, 1, out _), Is.False);
            c.ObservePuzzleMovement(Movement(1, 3)); c.ObservePuzzleMovement(Movement(1, 2));
            Assert.That(c.TryTickPuzzles(.05f, 3, out sample), Is.True); Assert.That(sample.Tick, Is.EqualTo(3));
            c.ObservePuzzleMovement(Movement(1, 4)); c.ReleaseActors(); Assert.That(c.TryTickPuzzles(.02f, 4, out _), Is.False);
        }
        [TestCase(true)] [TestCase(false)]
        public void IdentifiedVaultOutcomesAreAcceptedOnce(bool succeeded)
        {
            var c = Ready(out _);
            var fact = new PlayerTraversalFact(new EntityId(1), 1, TraversalKind.Vault, succeeded, Vector3.forward, .3f);
            Assert.That(c.AcceptPuzzleVault(fact, 0), Is.False);
            Assert.That(c.AcceptPuzzleVault(new PlayerTraversalFact(new EntityId(2), 1, TraversalKind.Vault, succeeded, Vector3.forward, .3f), 90), Is.False);
            Assert.That(c.AcceptPuzzleVault(new PlayerTraversalFact(new EntityId(1), 1, TraversalKind.Jump, succeeded, Vector3.forward, .3f), 90), Is.False);
            Assert.That(c.AcceptPuzzleVault(fact, 90), Is.True); Assert.That(c.AcceptPuzzleVault(fact, 90), Is.False);
            c.ReleaseActors(); Assert.That(c.AcceptPuzzleVault(fact, 90), Is.False);
        }
        [Test]
        public void ThemeAndInputSubscriptionsPairWithoutDuplicateEnable()
        {
            var owned = new List<GameObject>();
            try
            {
                var manager = Component<ExpeditionSessionManager>(owned); var run = Component<RunSessionManager>(owned);
                var procedural = Component<ProceduralManager>(owned); var floor = Component<FloorManager>(owned);
                var progression = Component<ProgressionSessionManager>(owned); var c = Ready(out var state);
                Set(manager, "_controller", c); Set(manager, "_state", state); Set(manager, "_run", run);
                Set(manager, "_procedural", procedural); Set(manager, "_floor", floor); Set(manager, "_progression", progression);
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    Call(manager, "OnEnable"); Call(manager, "OnEnable");
                    foreach (string name in new[] { "ThemePublished", "RoomThemePublished", "ThresholdFreezePublished", "OptionalPuzzleRewardPublished", "PuzzleSolved" })
                        Assert.That(Count(procedural, name), Is.EqualTo(1), name);
                    foreach (string name in new[] { "PlayerMovementPublished", "PlayerTraversalPublished", "TickAdvanced" }) Assert.That(Count(run, name), Is.EqualTo(1), name);
                    Call(manager, "Unsubscribe");
                    foreach (string name in new[] { "ThemePublished", "RoomThemePublished", "ThresholdFreezePublished", "OptionalPuzzleRewardPublished", "PuzzleSolved" }) Assert.That(Count(procedural, name), Is.Zero, name);
                    foreach (string name in new[] { "PlayerMovementPublished", "PlayerTraversalPublished", "TickAdvanced" }) Assert.That(Count(run, name), Is.Zero, name);
                }
                manager.ClearScene();
            }
            finally { for (int i = owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(owned[i]); }
        }
        [Test]
        public void ManagerRoutesDeltaExplicitSurfaceAndRunSeedNotFloorSeed()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs"));
            StringAssert.Contains("themeSeed: _progression.Snapshot.Seed", source);
            StringAssert.Contains("_procedural?.TickPuzzles(movement, dt)", source);
            StringAssert.Contains("_procedural?.CompletePuzzleVault(surfaceId, fact.Succeeded)", source);
        }
        private static T Component<T>(List<GameObject> owned) where T : Component
        { var root = new GameObject(typeof(T).Name); root.SetActive(false); owned.Add(root); return root.AddComponent<T>(); }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static int Count(object target, string name) => (Field(target, name).GetValue(target) as Delegate)?.GetInvocationList().Length ?? 0;
        private static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
