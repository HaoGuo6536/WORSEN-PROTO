// ============================================================================
// TelemetryDriverTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that output path failures remain visible and cannot produce a successful capture claim. It also checks that interrupted captures are flushed with their incomplete marker.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Exercise a real invalid output directory and successful temporary file lifecycle.
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
        private static RunCaptureMetadata Metadata() => new RunCaptureMetadata("fixture", 1, 0.1f, "s", "c", "order", 0);
    }
}
