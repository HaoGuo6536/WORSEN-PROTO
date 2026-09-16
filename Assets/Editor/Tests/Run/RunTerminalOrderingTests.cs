// ============================================================================
// RunTerminalOrderingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the real Session Manager's ordering at the terminal tick boundary.
//   A queued accepted hit and exit request use actual Player damage and run capture.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Run.
// KEY RESPONSIBILITIES:
//   - Prevent a pending escape from bypassing already queued lethal contact.
//   - Close completed capture before announcing Results and input shutdown.
// DEPENDENCIES:
//   - Run Session, Player Factory, reproducible Player assets and Unity Test Framework.
// USAGE NOTES:
//   Requires the Unity lease. Contact facts are supplied at the public-system
//   boundary; separate physics tests verify the Hunter's contact producer.
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Editor.Player;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Run
{
    public sealed class RunTerminalOrderingTests
    {
        [UnityTest]
        public IEnumerator QueuedLethalHitWinsEscapeBeforeNextTickAndCaptureClosesFirst()
        {
            PlayerPrefabGenerator.EnsureAssets();
            yield return new EnterPlayMode();
            // Locals from before EnterPlayMode do not survive the test domain reload.
            var profile = AssetDatabase.LoadAssetAtPath<PlayerProfile>(PlayerPrefabGenerator.ProfilePath);
            Assert.That(profile != null, Is.True);
            Assert.That(RunSessionManager.Instance, Is.Null);
            var runObject = new GameObject("Run terminal ordering");
            var factoryObject = new GameObject("Player terminal ordering");
            try
            {
                var run = runObject.AddComponent<RunSessionManager>().Initialize(42);
                run.PrepareScene(SceneKey.TagArena);
                var factory = factoryObject.AddComponent<PlayerFactory>();
                factory.Configure(profile, run.RandomSource);
                var id = factory.Spawn(new SpawnRequest(profile.ArchetypeKey, Vector3.zero, Quaternion.identity));
                Assert.That(PlayerRegistry.TryGet(id, out var player), Is.True);
                run.BindGameplay(null, null, null);
                run.HandleSceneReady(SceneKey.TagArena);
                var order = new List<string>();
                RunSummary summary = default;
                bool captureComplete = false;
                int acceptedHits = 0;
                run.CaptureEnded += (_, complete) => { captureComplete = complete; order.Add("capture"); };
                run.RunEnded += result => { summary = result; order.Add("results"); };
                run.TelemetryPublished += sample => { if (sample.Kind == TelemetrySampleKind.AcceptedHit) acceptedHits++; };
                Invoke(run, "QueueHit", new HunterHit(new EntityId(-1), id, 50, 0, Vector3.right));
                Invoke(run, "FixedUpdate");
                Assert.That(player.ReadOnlyState.Health, Is.EqualTo(50));
                Assert.That(run.Phase, Is.EqualTo(RunPhase.FirstSweep));
                Assert.That(order, Is.Empty, "A surviving accepted hit must keep capture and gameplay running.");
                long beforeTerminal = run.Tick;
                Invoke(run, "HandleExitOpened", beforeTerminal);
                Invoke(run, "HandleExitReached", new ExitReachedFact(id, beforeTerminal));
                Invoke(run, "QueueHit", new HunterHit(new EntityId(-1), id, 50, beforeTerminal, Vector3.right));
                Invoke(run, "FixedUpdate");
                Assert.That(run.Phase, Is.EqualTo(RunPhase.Ended));
                Assert.That(summary.EndReason, Is.EqualTo(RunEndReason.Died));
                Assert.That(player.ReadOnlyState.Health, Is.Zero);
                Assert.That(run.Tick, Is.EqualTo(beforeTerminal), "Committing pending terminal facts must not advance gameplay.");
                Assert.That(acceptedHits, Is.EqualTo(2));
                Assert.That(captureComplete, Is.True);
                Assert.That(order, Is.EqualTo(new[] { "capture", "results" }));
                Invoke(run, "FixedUpdate");
                Assert.That(order.Count, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(factoryObject);
                Object.DestroyImmediate(runObject);
            }
        }
        private static void Invoke(RunSessionManager run, string name, params object[] args)
        {
            var method = typeof(RunSessionManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            method.Invoke(run, args);
        }
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
