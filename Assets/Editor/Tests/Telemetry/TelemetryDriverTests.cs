// ============================================================================
// TelemetryDriverTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that output path failures remain visible and cannot produce a successful capture claim. It also checks that interrupted captures are flushed with their incomplete marker.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Exercise a real invalid output directory and successful temporary file lifecycle.
//   - Verify observation facts survive the gaps before and after a floor capture.
// DEPENDENCIES:
//   - NUnit, Unity test logging, TelemetryDriver and Editor serialization.
// USAGE NOTES:
//   - Editor-only engine/file tests require the coordinator Unity lease. Temporary outputs are scoped and removed after each fixture.
// ============================================================================
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Presentation.Telemetry;

namespace Worsen.Tests.Telemetry
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class TelemetryDriverTests
    {
        private GameObject _object;
        private TelemetryDriver _driver;
        private TelemetryDriverConfig _config;
        private string _folder;
        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "worsen-telemetry-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _object = new GameObject("Telemetry writer fixture"); _object.SetActive(false);
            _driver = _object.AddComponent<TelemetryDriver>();
            _config = ScriptableObject.CreateInstance<TelemetryDriverConfig>();
            var serialized = new SerializedObject(_driver);
            serialized.FindProperty("_config").objectReferenceValue = _config;
            serialized.ApplyModifiedPropertiesWithoutUndo(); _driver.Initialize();
        }
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_object); UnityEngine.Object.DestroyImmediate(_config);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }
        [Test]
        public void OutputCreationFailureNeverReportsSuccessfulFile()
        {
            string blockingFile = Path.Combine(_folder, "file"); File.WriteAllText(blockingFile, "fixture");
            LogAssert.Expect(LogType.Error, new Regex("^Telemetry write failed:"));
            _driver.BeginSession(Metadata(), Path.Combine(blockingFile, "child"));
            Assert.That(_driver.LastError, Is.Not.Empty); Assert.That(_driver.LastOutputPath, Is.Empty);
            Assert.That(_driver.EndSession(10, true), Is.False);
        }
        [Test]
        public void PermissionDeniedAtOutputBoundaryIsExplicitFailure()
        {
            _driver.Initialize(path => throw new UnauthorizedAccessException("fixture permission denied"));
            LogAssert.Expect(LogType.Error, new Regex("^Telemetry write failed:.*permission denied"));
            _driver.BeginSession(Metadata(), _folder);
            Assert.That(_driver.LastError, Does.Contain("permission denied"));
            Assert.That(_driver.LastOutputPath, Is.Empty);
            Assert.That(_driver.EndSession(1, true), Is.False);
        }
        [Test]
        public void InterruptedCaptureWritesRawDataAndIncompleteSummary()
        {
            _driver.BeginSession(Metadata(), _folder);
            _driver.Record(new TelemetrySample(1, new EntityId(1), 0, TelemetrySampleKind.HorizontalSpeed, 8));
            _driver.Suspend();
            Assert.That(_driver.LastError, Is.Empty);
            string text = File.ReadAllText(_driver.LastOutputPath);
            Assert.That(text, Does.Contain("HorizontalSpeed"));
            Assert.That(text, Does.Contain("\"Complete\",\"False\""));
            Assert.That(_driver.EndSession(1, true), Is.False);
        }
        [Test]
        public void ObservationJournalRetainsPreCaptureChoicesAndPostCaptureRoundEndOnce()
        {
            var observations = new TelemetryObservationPresenter();
            var before = Snapshot(ProgressionPhase.Exploring, 1, 12);
            var next = Snapshot(ProgressionPhase.ChooseThreat, 2, 6);
            var choice = TelemetryCsvPresenter.Observation(0, TelemetrySampleKind.ProgressionChoice,
                ("choice_id", "rusher"), ("choice_kind", "Threat"));
            _driver.RecordObservation(choice, _folder);
            string path = _driver.LastObservationOutputPath;
            _driver.RecordGeneration(new ProgressionGenerationRequest(1, 2147483647, 1, false, default), 0);
            _driver.BeginSession(Metadata(), _folder);
            Assert.That(_driver.EndSession(10, true), Is.True);
            _driver.RecordProgression(before, next, "EarlyBail", "", 10);
            var stall = observations.Stall(new EntityId(8), "rusher", 9, Vector3.zero, 2,
                3.25, 0.2f, 0.35f, 0.4f, "Chase", new Vector3[0], null, 2147483647);
            _driver.RecordObservation(stall);
            Assert.That(_driver.LastObservationOutputPath, Is.EqualTo(path));
            Assert.That(_driver.LastObservationError, Is.Empty);
            _driver.Suspend();
            string text = File.ReadAllText(path);
            foreach (TelemetrySampleKind kind in new[] { TelemetrySampleKind.ProgressionChoice, TelemetrySampleKind.RoundStarted,
                TelemetrySampleKind.FloorSeed, TelemetrySampleKind.RoundEnded, TelemetrySampleKind.WalletChanged, TelemetrySampleKind.HunterStall })
                Assert.That(Regex.Matches(text, "\"" + kind + "\"").Count, Is.EqualTo(1), kind.ToString());
            Assert.That(text, Does.Contain("\"-6\",\"6\",\"EarlyBail\""));
            Assert.That(text, Does.Contain("\"2147483647\""));
            Assert.That(File.ReadAllText(_driver.LastOutputPath), Does.Not.Contain("HunterStall"),
                "Observation facts must not alter the legacy measurement stream.");
        }
        [Test]
        public void ObservationOutputFailureIsVisibleAndDoesNotInventSuccessOrRetryRows()
        {
            _driver.Initialize(path => throw new UnauthorizedAccessException("fixture permission denied"));
            LogAssert.Expect(LogType.Error, new Regex("^Telemetry observation write failed:.*permission denied"));
            _driver.RecordObservation(TelemetryCsvPresenter.Observation(0, TelemetrySampleKind.FloorSeed,
                ("generation_seed", 123)), _folder);
            Assert.That(_driver.LastObservationError, Does.Contain("permission denied"));
            Assert.That(_driver.LastObservationOutputPath, Is.Empty);
            _driver.RecordObservation(TelemetryCsvPresenter.Observation(1, TelemetrySampleKind.FloorSeed,
                ("generation_seed", 124)), _folder);
            Assert.That(_driver.LastObservationOutputPath, Is.Empty);
        }
        private static ProgressionSnapshot Snapshot(ProgressionPhase phase, int round, int wallet) =>
            new ProgressionSnapshot(1, 1, round, 777, wallet, 1, 1, phase, 100, 100,
                null, null, null, default, "", false, false);
        private static RunCaptureMetadata Metadata() => new RunCaptureMetadata("fixture", 1, 0.1f, "s", "c", "order", 0);
    }
}
