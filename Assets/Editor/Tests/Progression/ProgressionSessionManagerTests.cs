// ============================================================================
// ProgressionSessionManagerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that the real persistent Session publishes each floor request once.
//   The fixture enters Play Mode for supported Unity persistence and observes
//   canonical initialization, synchronous menu feedback and restart identity.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Progression.
// KEY RESPONSIBILITIES:
//   - Verify Manager events and state retention across repeated initialization.
//   - Confirm shops, purchases and restart through public integration methods.
// DEPENDENCIES:
//   - Core contracts, Session Progression, NUnit and Unity Test Framework.
// USAGE NOTES:
//   Requires the exclusive Unity lease. Uses a temporary test scene and objects;
//   it does not generate geometry, edit assets or validate navigation.
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Session.Progression;

namespace Worsen.Tests.Progression
{
    public sealed class ProgressionSessionManagerTests
    {
        [UnityTest]
        public IEnumerator CanonicalManagerPublishesGenerationOnceAndRetainsUntilExplicitRestart()
        {
            yield return new EnterPlayMode();
            Assert.That(ProgressionSessionManager.Instance, Is.Null);
            var owner = new GameObject("Progression Session test");
            var duplicateOwner = new GameObject("Duplicate Progression Session test");
            var config = ScriptableObject.CreateInstance<ProgressionConfig>();
            var snapshots = new List<ProgressionSnapshot>();
            var requests = new List<ProgressionGenerationRequest>();
            ProgressionSessionManager manager = null;
            try
            {
                manager = owner.AddComponent<ProgressionSessionManager>().Initialize(config, 412);
                manager.SnapshotChanged += snapshots.Add;
                manager.GenerationRequested += requests.Add;
                manager.StartRun(412);
                Assert.That(manager.Snapshot.Phase, Is.EqualTo(ProgressionPhase.ChooseThreat));
                var duplicate = duplicateOwner.AddComponent<ProgressionSessionManager>().Initialize(config, 999);
                Assert.That(duplicate, Is.SameAs(manager));
                Assert.That(manager.Snapshot.Seed, Is.EqualTo(412));
                for (int round = 1; round <= 3; round++)
                {
                    Assert.That(manager.ChooseThreat("watcher", manager.Snapshot.Revision), Is.True);
                    int revision = manager.Snapshot.Revision;
                    Assert.That(manager.ChooseCurse("restless", revision), Is.True);
                    Assert.That(manager.ChooseCurse("restless", revision), Is.False);
                    Assert.That(requests.Count, Is.EqualTo(round));
                    int generation = requests[requests.Count - 1].GenerationId;
                    Assert.That(manager.ConfirmFloorReady(generation), Is.True);
                    for (int anchor = 0; anchor < 3; anchor++) Assert.That(manager.RecordGoldenCollected(generation, anchor), Is.True);
                    if (round == 1) Assert.That(manager.RecordHealth(generation, 60f), Is.True);
                    Assert.That(manager.CompleteFloor(generation), Is.True);
                    Assert.That(manager.CompleteFloor(generation), Is.False);
                }
                Assert.That(requests.Count, Is.EqualTo(4));
                Assert.That(requests[3].IsShop, Is.True);
                Assert.That(requests[3].Effects.Health, Is.EqualTo(60f));
                Assert.That(requests[3].Effects.ActiveThreatBudget, Is.Zero);
                Assert.That(manager.ConfirmFloorReady(requests[3].GenerationId), Is.True);
                int beforePurchase = snapshots.Count;
                Assert.That(manager.Purchase("medkit", manager.Snapshot.Revision), Is.True);
                Assert.That(snapshots.Count, Is.EqualTo(beforePurchase + 1));
                Assert.That(manager.Snapshot.Wallet, Is.EqualTo(6));
                Assert.That(manager.Snapshot.Health, Is.EqualTo(95f));
                Assert.That(manager.ContinueShop(manager.Snapshot.Revision), Is.True);
                Assert.That(manager.Snapshot.Round, Is.EqualTo(5));
                Assert.That(manager.RestartRun(manager.Snapshot.Revision), Is.False, "An active expedition cannot be restarted by a stale results button.");
                manager.ChooseThreat("watcher", manager.Snapshot.Revision);
                manager.ChooseCurse("restless", manager.Snapshot.Revision);
                int lastGeneration = requests[4].GenerationId;
                manager.ConfirmFloorReady(lastGeneration);
                manager.EndRun(lastGeneration);
                int endRevision = manager.Snapshot.Revision;
                Assert.That(manager.RestartRun(endRevision), Is.True);
                Assert.That(manager.RestartRun(endRevision), Is.False);
                Assert.That(manager.Snapshot.Round, Is.EqualTo(1));
                Assert.That(manager.Snapshot.Wallet, Is.Zero);
                Assert.That(manager.Snapshot.Health, Is.EqualTo(100f));
                manager.ChooseThreat("watcher", manager.Snapshot.Revision);
                manager.ChooseCurse("restless", manager.Snapshot.Revision);
                Assert.That(requests[5].GenerationId, Is.GreaterThan(lastGeneration));
                Assert.That(requests[5].Seed, Is.EqualTo(requests[0].Seed));
                Assert.That(manager.ConfirmFloorReady(lastGeneration), Is.False);
            }
            finally
            {
                if (manager != null)
                {
                    manager.SnapshotChanged -= snapshots.Add;
                    manager.GenerationRequested -= requests.Add;
                }
                Object.DestroyImmediate(duplicateOwner);
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(config);
            }
            Assert.That(ProgressionSessionManager.Instance, Is.Null);
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
