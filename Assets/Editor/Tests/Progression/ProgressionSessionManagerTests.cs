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
//   - Confirm shops, purchases, explicit new seeds and deterministic replay restarts.
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
                for (int round = 1; round <= 2; round++)
                {
                    Assert.That(manager.ChooseThreat(manager.Snapshot.Choices[0].Id, manager.Snapshot.Revision), Is.True);
                    int revision = manager.Snapshot.Revision;
                    string curse = manager.Snapshot.Choices[0].Id;
                    Assert.That(manager.ChooseCurse(curse, revision), Is.True);
                    Assert.That(manager.ChooseCurse(curse, revision), Is.False);
                    Assert.That(requests.Count, Is.EqualTo(round));
                    int generation = requests[requests.Count - 1].GenerationId;
                    Assert.That(manager.ConfirmFloorReady(generation), Is.True);
                    for (int anchor = 0; anchor < 3; anchor++) Assert.That(manager.RecordGoldenCollected(generation, anchor), Is.True);
                    if (round == 1) Assert.That(manager.RecordHealth(generation, 60f), Is.True);
                    Assert.That(manager.CompleteFloor(generation), Is.True);
                    Assert.That(manager.CompleteFloor(generation), Is.False);
                }
                Assert.That(requests.Count, Is.EqualTo(3));
                Assert.That(requests[2].IsShop, Is.True);
                Assert.That(requests[2].Effects.Health, Is.EqualTo(60f));
                Assert.That(requests[2].Effects.ActiveThreatBudget, Is.Zero);
                Assert.That(manager.ConfirmFloorReady(requests[2].GenerationId), Is.True);
                int beforePurchase = snapshots.Count;
                Assert.That(manager.Purchase("field-dressing", manager.Snapshot.Revision), Is.True);
                Assert.That(snapshots.Count, Is.EqualTo(beforePurchase + 1));
                Assert.That(manager.Snapshot.Wallet, Is.EqualTo(4));
                Assert.That(manager.Snapshot.Health, Is.EqualTo(95f));
                Assert.That(manager.Purchase("wax-ward", manager.Snapshot.Revision), Is.True);
                Assert.That(manager.Snapshot.Effects.WaxWardCharges, Is.EqualTo(1));
                Assert.That(manager.ContinueShop(manager.Snapshot.Revision), Is.True);
                Assert.That(manager.Snapshot.Round, Is.EqualTo(4));
                Assert.That(manager.RestartRun(manager.Snapshot.Revision), Is.False, "An active expedition cannot be restarted by a stale results button.");
                manager.ChooseThreat(manager.Snapshot.Choices[0].Id, manager.Snapshot.Revision);
                manager.ChooseCurse(manager.Snapshot.Choices[0].Id, manager.Snapshot.Revision);
                int lastGeneration = requests[3].GenerationId;
                manager.ConfirmFloorReady(lastGeneration);
                Assert.That(manager.TryConsumeWaxWard(lastGeneration - 1), Is.False);
                int beforeWard = snapshots.Count;
                Assert.That(manager.TryConsumeWaxWard(lastGeneration), Is.True);
                Assert.That(snapshots.Count, Is.EqualTo(beforeWard + 1));
                Assert.That(snapshots[snapshots.Count - 1].Effects.WaxWardCharges, Is.Zero);
                Assert.That(manager.TryConsumeWaxWard(lastGeneration), Is.False);
                Assert.That(snapshots.Count, Is.EqualTo(beforeWard + 1));
                manager.EndRun(lastGeneration);
                int endRevision = manager.Snapshot.Revision;
                Assert.That(manager.RestartRun(endRevision), Is.True);
                Assert.That(manager.RestartRun(endRevision), Is.False);
                Assert.That(manager.Snapshot.Round, Is.EqualTo(1));
                Assert.That(manager.Snapshot.Wallet, Is.Zero);
                Assert.That(manager.Snapshot.Health, Is.EqualTo(100f));
                manager.ChooseThreat(manager.Snapshot.Choices[0].Id, manager.Snapshot.Revision);
                manager.ChooseCurse(manager.Snapshot.Choices[0].Id, manager.Snapshot.Revision);
                Assert.That(requests[4].GenerationId, Is.GreaterThan(lastGeneration));
                Assert.That(requests[4].Seed, Is.EqualTo(requests[0].Seed));
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

        [UnityTest]
        public IEnumerator ExplicitRestartSeedCommitsOnceAndReplayRetainsThatSeed()
        {
            yield return new EnterPlayMode();
            Assert.That(ProgressionSessionManager.Instance, Is.Null);
            var owner = new GameObject("Explicit progression restart seed test");
            var config = ScriptableObject.CreateInstance<ProgressionConfig>();
            var requests = new List<ProgressionGenerationRequest>();
            ProgressionSessionManager manager = null;
            try
            {
                manager = owner.AddComponent<ProgressionSessionManager>().Initialize(config, 101);
                manager.GenerationRequested += requests.Add;
                manager.StartRun(101);
                Assert.That(manager.RestartRun(manager.Snapshot.Revision, 902), Is.False, "Active menus cannot restart.");
                Assert.That(manager.Snapshot.Seed, Is.EqualTo(101));
                CommitFirstGeneration(manager);
                var first = requests[0];
                Assert.That(first.Seed, Is.EqualTo(new System.Random(101).Next()));
                Assert.That(manager.ConfirmFloorReady(first.GenerationId), Is.True);
                Assert.That(manager.EndRun(first.GenerationId), Is.True);
                int endedRevision = manager.Snapshot.Revision;
                Assert.That(manager.RestartRun(endedRevision - 1, 902), Is.False);
                Assert.That(manager.Snapshot.Seed, Is.EqualTo(101));
                Assert.That(manager.RestartRun(endedRevision, 902), Is.True);
                Assert.That(manager.RestartRun(endedRevision, 903), Is.False, "A duplicate click cannot replace the chosen new seed.");
                Assert.That(manager.Snapshot.Seed, Is.EqualTo(902));
                Assert.That(manager.Snapshot.Round, Is.EqualTo(1));
                Assert.That(manager.Snapshot.Phase, Is.EqualTo(ProgressionPhase.ChooseThreat));
                Assert.That(manager.Snapshot.Wallet, Is.Zero);
                Assert.That(manager.Snapshot.Effects.ActiveThreatIds, Is.Empty);
                Assert.That(requests.Count, Is.EqualTo(1), "Do not publish a floor before its choices commit.");
                CommitFirstGeneration(manager);
                Assert.That(requests.Count, Is.EqualTo(2));
                var fresh = requests[1];
                Assert.That(fresh.Seed, Is.EqualTo(new System.Random(902).Next()));
                Assert.That(fresh.Seed, Is.Not.EqualTo(first.Seed));
                Assert.That(fresh.GenerationId, Is.GreaterThan(first.GenerationId));
                Assert.That(manager.FailGeneration(first.GenerationId, "stale"), Is.False);
                Assert.That(manager.FailGeneration(fresh.GenerationId, "exercise replay from failure"), Is.True);
                Assert.That(manager.RestartRun(manager.Snapshot.Revision), Is.True, "One-argument restart preserves explicit replay behavior.");
                Assert.That(manager.Snapshot.Seed, Is.EqualTo(902));
                CommitFirstGeneration(manager);
                Assert.That(requests.Count, Is.EqualTo(3));
                Assert.That(requests[2].Seed, Is.EqualTo(fresh.Seed));
                Assert.That(requests[2].GenerationId, Is.GreaterThan(fresh.GenerationId));
            }
            finally
            {
                if (manager != null) manager.GenerationRequested -= requests.Add;
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(config);
            }
            Assert.That(ProgressionSessionManager.Instance, Is.Null);
        }

        private static void CommitFirstGeneration(ProgressionSessionManager manager)
        {
            Assert.That(manager.ChooseThreat(manager.Snapshot.Choices[0].Id, manager.Snapshot.Revision), Is.True);
            Assert.That(manager.ChooseCurse(manager.Snapshot.Choices[0].Id, manager.Snapshot.Revision), Is.True);
            Assert.That(manager.Snapshot.Phase, Is.EqualTo(ProgressionPhase.Generating));
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
