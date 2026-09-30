// ============================================================================
// ExpeditionBailTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the completed-run boundary against real Expedition and Progression
//   managers. Wallet and revision changes prove that normal completion remains gated
//   by the current scene and resolves an admitted generation only once.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Complete only through the normal escape boundary with no bail metadata.
//   - Reject wrong-scene and repeated summaries without duplicate completion.
// DEPENDENCIES:
//   - Core contracts, Session Expedition/Progression, NUnit and Unity Test Framework.
// USAGE NOTES:
//   Coordinator-run isolated Play Mode; temporary objects only, no asset writes.
//   Reflection supplies an admitted zero-hunter assembly and invokes the summary
//   handler; procedural generation and native door detection are separate gates.
// ============================================================================
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Expedition
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class ExpeditionBailTests
    {
        [UnityTest]
        public IEnumerator SummaryCompletesNormallyExactlyOnce()
        {
            yield return new EnterPlayMode();
            Assert.That(ProgressionSessionManager.Instance, Is.Null);
            Assert.That(ExpeditionSessionManager.Instance, Is.Null);
            {
                var progressionRoot = new GameObject("Bail progression");
                var expeditionRoot = new GameObject("Bail expedition");
                var config = ScriptableObject.CreateInstance<ProgressionConfig>();
                try
                {
                    var progression = progressionRoot.AddComponent<ProgressionSessionManager>().Initialize(config, 42);
                    progression.StartRun(42);
                    Assert.That(progression.ChooseThreat(progression.Snapshot.Choices[0].Id, progression.Snapshot.Revision), Is.True);
                    Assert.That(progression.ChooseCurse(progression.Snapshot.Choices[0].Id, progression.Snapshot.Revision), Is.True);
                    int generation = progression.Snapshot.GenerationId;
                    Assert.That(progression.ConfirmFloorReady(generation), Is.True);
                    for (int anchor = 0; anchor < 8; anchor++)
                        Assert.That(progression.RecordGoldenCollected(generation, anchor), Is.True);
                    int wallet = progression.Snapshot.Wallet;
                    Assert.That(wallet, Is.GreaterThan(0));
                    var expedition = expeditionRoot.AddComponent<ExpeditionSessionManager>().Initialize();
                    typeof(ExpeditionSessionManager).GetField("_progression", BindingFlags.NonPublic | BindingFlags.Instance)
                        .SetValue(expedition, progression);
                    var assembly = (ExpeditionSessionController)typeof(ExpeditionSessionManager)
                        .GetField("_controller", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(expedition);
                    assembly.Bind(SceneKey.HorrorRun);
                    var effects = new ProgressionEffects(1f, 1f, 1f, 1f, 100f, 100f, 0);
                    Assert.That(assembly.Queue(new ProgressionGenerationRequest(generation, 42, 1, false, effects)), Is.True);
                    Assert.That(assembly.Begin(generation), Is.True);
                    assembly.RecordPlayer(new EntityId(9999));
                    assembly.Ready();
                    var handler = typeof(ExpeditionSessionManager).GetMethod("HandleRunEnded", BindingFlags.NonPublic | BindingFlags.Instance);
                    int revision = progression.Snapshot.Revision;
                    handler.Invoke(expedition, new object[] { new RunSummary(1, 0, 0, 0, 0, 0,
                        RunEndReason.Escaped, scene: SceneKey.FloorLoop) });
                    Assert.That(progression.Snapshot.Revision, Is.EqualTo(revision));
                    var summary = new RunSummary(1, 0, 0, 0, 0, 0, RunEndReason.Escaped,
                        scene: SceneKey.HorrorRun);
                    handler.Invoke(expedition, new object[] { summary });
                    Assert.That(progression.Snapshot.Wallet, Is.EqualTo(wallet));
                    Assert.That(progression.Snapshot.Revision, Is.EqualTo(revision + 1));
                    Assert.That(expedition.AssemblyPhase, Is.EqualTo(ExpeditionAssemblyPhase.Resolved));
                    handler.Invoke(expedition, new object[] { summary });
                    Assert.That(progression.Snapshot.Revision, Is.EqualTo(revision + 1));
                }
                finally
                {
                    Object.DestroyImmediate(expeditionRoot);
                    Object.DestroyImmediate(progressionRoot);
                    Object.DestroyImmediate(config);
                }
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
