// ============================================================================
// ShaderReferenceTests.cs
// ============================================================================
// PURPOSE:
//   Exercises fail-closed shader admission on transient configs and real drivers.
//   Repeated initialization must report a missing reference once per owner rather
//   than flooding the log or creating partial visuals with an unavailable shader.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests (§11) · Core integration contracts.
// KEY RESPONSIBILITIES:
//   - Check each converted config's missing-reference diagnostic and retry behavior.
//   - Check independent sub-driver entry points and deterministic setup bindings.
// DEPENDENCIES:
//   NUnit, UnityEditor, Unity test logs and Floor/Procedural/Environment/Horror/Camera.
// USAGE NOTES:
//   Requires Unity Edit Mode. Creates only transient configs and objects; no saves.
//   Exceptions remain mandatory on failed gameplay construction even after the first log.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Domain.Floor;
using Worsen.Domain.Procedural;
using Worsen.Presentation.Environment;
using Worsen.Presentation.Horror;
using Worsen.Presentation.Camera;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Core
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ShaderReferenceTests
    {
        private GameObject _root;
        private ScriptableObject _config;
        [SetUp] public void SetUp() => _root = new GameObject("Shader reference fixture");
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(_root); if (_config != null) Object.DestroyImmediate(_config); }

        [TestCase("_surfaceShader")] [TestCase("_mistShader")]
        public void FloorConfigMissingReferenceReportsOnceAndAbortsBeforeBuilding(string field)
        {
            var config = Create<FloorDriverConfig>(field);
            var driver = _root.AddComponent<FloorDriver>(); Set(driver, "_config", config);
            const string error = "FloorDriverConfig requires SurfaceShader and MistMaterial or MistShader. Rebuild Floor assets.";
            LogAssert.Expect(LogType.Error, error);
            for (int i = 0; i < 2; i++) Assert.That(() => driver.Initialize(null, null), Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo(error));
            Assert.That(driver.OwnedRoomCount, Is.Zero); Assert.That(_root.transform.childCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase("_surfaceShader")] [TestCase("_crackShader")]
        public void ProceduralConfigMissingReferenceReportsOnceAndAbortsBeforeBuilding(string field)
        {
            var config = Create<ProceduralDriverConfig>(field);
            var driver = _root.AddComponent<ProceduralDriver>();
            const string error = "ProceduralDriverConfig requires SurfaceShader and CrackShader. Rebuild Procedural assets.";
            LogAssert.Expect(LogType.Error, error);
            for (int i = 0; i < 2; i++) Assert.That(() => driver.Build(null, null, config), Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo(error));
            Assert.That(driver.IsReady, Is.False); Assert.That(driver.OwnedBlockCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ChallengeConfigMissingReferenceReportsOnceAndAbortsBeforeBuilding()
        {
            var config = Create<ProceduralChallengeConfig>("_tileShader");
            var driver = _root.AddComponent<ProceduralPuzzleModule>();
            const string error = "ProceduralChallengeConfig requires TileShader. Rebuild Procedural assets.";
            LogAssert.Expect(LogType.Error, error);
            for (int i = 0; i < 2; i++) Assert.That(() => driver.Configure(default, config, null, 0, null), Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo(error));
            Assert.That(_root.transform.childCount, Is.Zero); LogAssert.NoUnexpectedReceived();
        }

        [TestCase("_chalkShader")] [TestCase("_panelShader")]
        public void EnvironmentConfigMissingReferenceReportsOnceAndRejectsCommands(string field)
        {
            var config = Create<EnvironmentDriverConfig>(field);
            var driver = _root.AddComponent<EnvironmentDriver>();
            LogAssert.Expect(LogType.Error, "EnvironmentDriverConfig requires ChalkShader and PanelShader. Rebuild Environment assets.");
            for (int i = 0; i < 2; i++)
            { driver.Initialize(config); driver.Tick(1f); driver.MarkDoor(i, Vector3.zero); }
            Assert.That(driver.IsReady, Is.False); Assert.That(driver.DoorMarkCount, Is.Zero);
            Assert.That(_root.transform.childCount, Is.Zero); LogAssert.NoUnexpectedReceived();
        }

        [TestCase("_attackShader")] [TestCase("_webShader")]
        public void HorrorConfigMissingReferenceReportsOnceAndDoesNotStartAtmosphere(string field)
        {
            var config = Create<HorrorDriverConfig>(field);
            var driver = _root.AddComponent<HorrorDriver>();
            LogAssert.Expect(LogType.Error, "HorrorDriverConfig requires attack and web materials or shaders. Rebuild Horror assets.");
            for (int i = 0; i < 2; i++)
            { driver.Initialize(config); Assert.That(driver.AdvanceRunClock(1f), Is.False); }
            Assert.That(driver.IsReady, Is.False); Assert.That(_root.transform.childCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CameraConfigMissingReferenceReportsOnceAndDoesNotCreateHand()
        {
            var config = Create<CameraDriverConfig>("_handShader");
            var output = _root.AddComponent<UnityEngine.Camera>();
            var driver = _root.AddComponent<CameraHandCatchDriver>();
            LogAssert.Expect(LogType.Error, "CameraDriverConfig requires HandMaterial or HandShader. Rebuild Camera assets.");
            for (int i = 0; i < 2; i++) { driver.Initialize(config, output); driver.Apply(1f, 1f, 1f); }
            Assert.That(_root.transform.childCount, Is.Zero); LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RoomSubDriverAlsoRejectsMissingMistOnce()
        {
            var config = Create<FloorDriverConfig>("_mistShader");
            var driver = _root.AddComponent<RoomCollapseVolume>();
            const string error = "FloorDriverConfig requires MistMaterial or MistShader. Rebuild Floor assets.";
            LogAssert.Expect(LogType.Error, error);
            for (int i = 0; i < 2; i++) Assert.That(() => driver.Configure(default, config, null, null), Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo(error));
            Assert.That(_root.transform.childCount, Is.Zero); LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PanelSubDriverAlsoRejectsMissingShaderOnce()
        {
            var config = Create<EnvironmentDriverConfig>("_panelShader");
            var driver = _root.AddComponent<EnvironmentFluorescentFixture>();
            const string error = "EnvironmentDriverConfig requires PanelShader. Rebuild Environment assets.";
            LogAssert.Expect(LogType.Error, error);
            for (int i = 0; i < 2; i++) Assert.That(() => driver.Configure(Vector3.zero, 0f, Vector3.one, config), Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo(error));
            Assert.That(_root.transform.childCount, Is.Zero); LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void WebSubDriverAlsoRejectsMissingShaderOnce()
        {
            var config = Create<HorrorDriverConfig>("_webShader");
            var driver = _root.AddComponent<HorrorWebDriver>();
            LogAssert.Expect(LogType.Error, "HorrorDriverConfig requires WebMaterial or WebShader. Rebuild Horror assets.");
            for (int i = 0; i < 2; i++) { driver.Initialize(config); driver.Tick(1f); }
            Assert.That(driver.VisualCount, Is.Zero); LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void FloorEditorSetupRepairsMissingReferencesAndPreservesOverrides()
        {
            var config = ScriptableObject.CreateInstance<FloorDriverConfig>(); _config = config;
            Worsen.Editor.Floor.FloorConfigGenerator.ConfigureShaders(config);
            Assert.That(config.SurfaceShader, Is.Not.Null); Assert.That(config.MistShader, Is.Not.Null);
            var retained = config.MistShader; Set(config, "_surfaceShader", retained);
            Worsen.Editor.Floor.FloorConfigGenerator.ConfigureShaders(config);
            Assert.That(config.SurfaceShader, Is.SameAs(retained));
        }

        [Test]
        public void ProceduralEditorSetupRepairsDriverAndChallengeReferences()
        {
            var config = ScriptableObject.CreateInstance<ProceduralDriverConfig>(); _config = config;
            Worsen.Editor.Procedural.ProceduralShaderSetup.Configure(config);
            Assert.That(config.SurfaceShader, Is.Not.Null); Assert.That(config.CrackShader, Is.Not.Null);
            var challenge = ScriptableObject.CreateInstance<ProceduralChallengeConfig>();
            try
            {
                Worsen.Editor.Procedural.ProceduralShaderSetup.Configure(challenge);
                Assert.That(challenge.TileShader, Is.Not.Null);
            }
            finally { Object.DestroyImmediate(challenge); }
        }

        [Test]
        public void HorrorEditorSetupRepairsAndPreservesShaderReferences()
        {
            var config = ScriptableObject.CreateInstance<HorrorDriverConfig>(); _config = config;
            Worsen.Editor.Horror.HorrorShaderSetup.Configure(config);
            Assert.That(config.AttackShader, Is.Not.Null); Assert.That(config.WebShader, Is.Not.Null);
            var retained = config.WebShader; Set(config, "_attackShader", retained);
            Worsen.Editor.Horror.HorrorShaderSetup.Configure(config);
            Assert.That(config.AttackShader, Is.SameAs(retained));
        }

        private T Create<T>(string missingField) where T : ScriptableObject
        { var config = ShaderReferenceTestSetup.Create<T>(); _config = config; Set(config, missingField, null); return config; }
        private static void Set(Object target, string field, Object value)
        { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
