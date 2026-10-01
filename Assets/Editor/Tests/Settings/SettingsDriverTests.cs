// ============================================================================
// SettingsDriverTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the real JSON boundary through a fake file Driver without player-disk writes.
//   Malformed and future files must fall back visibly and preserve unsupported data.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Session · Settings.
// KEY RESPONSIBILITIES:
//   - Verify settings/history round trips, compatibility, logged fallback and failed saves.
// DEPENDENCIES:
//   NUnit, Unity objects/JsonUtility, Core and Settings.
// USAGE NOTES:
//   EditMode engine tests; coordinator executes in Unity. Fake IO does not replace JSON serialization.
// ============================================================================
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Settings;
namespace Worsen.Tests.Settings
{
    public sealed class FakeSettingsFileDriver : SettingsDriver
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        public readonly List<string> Warnings = new List<string>();
        public bool FailWrites;
        protected override string ReadFile(string name) => Files.TryGetValue(name, out string value) ? value : null;
        protected override void WriteFile(string name, string json)
        { if (FailWrites) throw new IOException("Injected disk failure"); Files[name] = json; }
        protected override void LogWarning(string message) => Warnings.Add(message);
    }
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class SettingsDriverTests
    {
        private GameObject _owner;
        private FakeSettingsFileDriver _driver;
        private static PlayerSettingsRecord Defaults => new PlayerSettingsRecord(1, .1f, false, 95, true, true, true, 1, 1, 1);
        [SetUp] public void SetUp() { _owner = new GameObject("Fake settings files"); _driver = _owner.AddComponent<FakeSettingsFileDriver>(); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_owner);
        [Test]
        public void RoundTripPreservesEveryPreferenceAndHistoryField()
        {
            var input = new PlayerSettingsRecord(1, .27f, true, 107, false, true, false, .8f, .6f, .4f);
            Assert.That(_driver.SaveSettings(input), Is.True);
            var output = _driver.LoadSettings(Defaults);
            Assert.That(output, Is.EqualTo(input));
            var history = new RunHistoryRecord(1, 123, 27, new[] { "Echo", "weaver" });
            Assert.That(_driver.SaveHistory(history), Is.True);
            var loaded = _driver.LoadHistory();
            Assert.That(loaded.SchemaVersion, Is.EqualTo(1)); Assert.That(loaded.LifetimeRuns, Is.EqualTo(123));
            Assert.That(loaded.BestDepth, Is.EqualTo(27)); Assert.That(loaded.UnlockedThreatIds, Is.EqualTo(history.UnlockedThreatIds));
            Assert.That(_driver.Warnings, Is.Empty);
        }
        [TestCase("{broken")]
        [TestCase("{}")]
        [TestCase("null")]
        [TestCase("{\"schemaVersion\":0}")]
        [TestCase("{\"schemaVersion\":-1}")]
        public void CorruptOrUnversionedFilesFallBackAndLog(string json)
        {
            _driver.Files["player-settings.json"] = _driver.Files["run-history.json"] = json;
            Assert.That(_driver.LoadSettings(Defaults), Is.EqualTo(Defaults));
            Assert.That(_driver.LoadHistory().BestDepth, Is.Zero);
            Assert.That(_driver.Warnings.Count, Is.EqualTo(2));
            Assert.That(_driver.Warnings[0], Does.Contain("fallback"));
            Assert.That(_driver.Files["player-settings.json"], Is.EqualTo(json));
        }
        [Test]
        public void FutureVersionsAreNotOverwrittenByDefaultFallbackSaves()
        {
            const string json = "{\"schemaVersion\":2,\"future\":42}";
            _driver.Files["player-settings.json"] = _driver.Files["run-history.json"] = json;
            Assert.That(_driver.LoadSettings(Defaults), Is.EqualTo(Defaults)); _driver.LoadHistory();
            Assert.That(_driver.SaveSettings(Defaults), Is.False);
            Assert.That(_driver.SaveHistory(new RunHistoryRecord(1, 1, 8, null)), Is.False);
            Assert.That(_driver.Files["player-settings.json"], Is.EqualTo(json));
            Assert.That(_driver.Files["run-history.json"], Is.EqualTo(json));
            Assert.That(_driver.Warnings.Count, Is.EqualTo(4));
        }
        [Test]
        public void MissingFilesAreNormalAndWriteFailureIsReportedWithoutLosingPreviousFile()
        {
            Assert.That(_driver.LoadSettings(Defaults), Is.EqualTo(Defaults));
            Assert.That(_driver.LoadHistory().LifetimeRuns, Is.Zero); Assert.That(_driver.Warnings, Is.Empty);
            _driver.SaveSettings(Defaults); string previous = _driver.Files["player-settings.json"];
            _driver.FailWrites = true;
            Assert.That(_driver.SaveSettings(Defaults), Is.False);
            Assert.That(_driver.LastError, Does.Contain("Injected disk failure"));
            Assert.That(_driver.Files["player-settings.json"], Is.EqualTo(previous));
        }
    }
}
