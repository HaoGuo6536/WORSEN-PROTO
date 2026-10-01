// ============================================================================
// SetupSequenceTests.cs
// ============================================================================
// PURPOSE:
//   Proves aggregate setup has stable ordering and cannot report a partial run as
//   success. Injected actions make failure paths testable without starting Unity.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Editor setup sequencing.
// KEY RESPONSIBILITIES:
//   - Verify the successful prefix, first failure and unattempted suffix.
//   - Reject invalid manifests before any mutation and retain report snapshots.
// DEPENDENCIES:
//   - Common SetupSequence and NUnit only.
// USAGE NOTES:
//   Pure tests; no Unity editor calls or files are needed.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Worsen.Editor.Common;

namespace Worsen.Tests.Editor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class SetupSequenceTests
    {
        [Test]
        public void RunsExactlyInDeclaredOrder()
        {
            var seen = new List<string>();
            var report = SetupSequence.Run(new[] { new SetupStep("z", "first dependency", () => seen.Add("z")),
                new SetupStep("a", "consumer", () => seen.Add("a")) });
            Assert.That(seen, Is.EqualTo(new[] { "z", "a" }));
            Assert.That(report.Succeeded, Is.True);
            Assert.That(report.Steps.Select(step => step.Status), Is.All.EqualTo(SetupStepStatus.Succeeded));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void StopsAtFirstFailureAndReportsUnattemptedSuffix(int failure)
        {
            var seen = new List<int>();
            var manifest = Enumerable.Range(0, 3).Select(index => new SetupStep(index.ToString(), "dependency order", () =>
            { seen.Add(index); if (index == failure) throw new InvalidOperationException("intentional failure"); })).ToArray();
            var report = SetupSequence.Run(manifest);
            Assert.That(report.Succeeded, Is.False);
            Assert.That(seen, Is.EqualTo(Enumerable.Range(0, failure + 1)));
            Assert.That(report.Steps.Count, Is.EqualTo(3));
            for (int index = 0; index < 3; index++)
                Assert.That(report.Steps[index].Status, Is.EqualTo(index < failure ? SetupStepStatus.Succeeded :
                    index == failure ? SetupStepStatus.Failed : SetupStepStatus.NotRun));
            Assert.That(report.Steps[failure].Error, Does.Contain("intentional failure"));
            Assert.That(report.Steps[failure].Error, Does.Contain(nameof(InvalidOperationException)));
        }

        [Test]
        public void InvalidLaterStepPreventsEarlierMutation()
        {
            bool ran = false;
            Assert.Throws<ArgumentException>(() => SetupSequence.Run(new[] {
                new SetupStep("first", "valid", () => ran = true), new SetupStep("second", "invalid", null) }));
            Assert.That(ran, Is.False);
        }

        [Test]
        public void EmptyAndDuplicateManifestsFailClosed()
        {
            Assert.Throws<ArgumentException>(() => SetupSequence.Run(Array.Empty<SetupStep>()));
            var step = new SetupStep("duplicate", "reason", () => { });
            Assert.Throws<ArgumentException>(() => SetupSequence.Run(new[] { step, step }));
        }

        [Test]
        public void ReportOwnsItsSnapshot()
        {
            var step = new SetupStep("one", "reason", () => { });
            var results = new[] { new SetupStepResult(step, SetupStepStatus.Succeeded, "") };
            var report = new SetupReport(results);
            results[0] = new SetupStepResult(step, SetupStepStatus.Failed, "changed");
            Assert.That(report.Succeeded, Is.True);
        }
    }
}
