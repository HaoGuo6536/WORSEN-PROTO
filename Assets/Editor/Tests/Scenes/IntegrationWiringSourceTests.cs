// ============================================================================
// IntegrationWiringSourceTests.cs
// ============================================================================
// PURPOSE:
//   Guards the versioned HUD asset and scene composition contracts without Unity.
//   These source checks complement native wiring tests, never claiming a rendered HUD.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Scenes.
// KEY RESPONSIBILITIES:
//   - Keep obsolete markup absent and Expand consistent between asset and setup.
//   - Keep held-item initialization after camera initialization and teardown paired.
//   - Keep applied modal visibility, heartbeat and pass-two setup routes composed.
// DEPENDENCIES:
//   NUnit, System.IO/XML and Unity execution-order metadata; no native engine access.
// USAGE NOTES:
//   Pure source-contract checks read the checkout using the harness working directory.
// ============================================================================
using System.IO;
using System.Reflection;
using System.Xml;
using NUnit.Framework;
namespace Worsen.Tests.Scenes
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class IntegrationWiringSourceTests
    {
        [Test] public void HeartbeatEnvelopeIsSampledBeforeVolumeLateUpdate()
        {
            var route = typeof(Worsen.Orchestrator.PostFXOrchestrator).GetCustomAttribute<UnityEngine.DefaultExecutionOrder>();
            var driver = typeof(Worsen.Presentation.PostFX.PostFXDriver).GetCustomAttribute<UnityEngine.DefaultExecutionOrder>();
            Assert.That(route, Is.Not.Null);
            Assert.That(route.order, Is.LessThan(driver?.order ?? 0));
            Assert.That(File.ReadAllText("Assets/Scripts/Orchestrator/Scenes/HorrorRunSceneRoot.cs"), Does.Contain("heartbeatEnvelope: () => _audio.HeartbeatEnvelope"));
        }
        [Test] public void PassTwoSetupRestoresExistingSceneAndGeneratedHazardWithoutHandAuthoredAsset()
        {
            string setup = File.ReadAllText("Assets/Editor/Scenes/HorrorRunSceneSetup.cs");
            Assert.That(typeof(Worsen.Editor.Scenes.HorrorRunSceneSetup).GetMethod("RestoreIntegrationWiring", System.Type.EmptyTypes), Is.Not.Null);
            foreach (string seam in new[] { "FloorConfigGenerator.BuildFloorAssets();", "RestoreHeldItem(root);", "RestoreIntegrationRoutes(root);",
                "HeraldOrchestrator", "FloorConfigGenerator.HazardConfigPath", "_hazardConfig", "_heraldRoute" }) Assert.That(setup, Does.Contain(seam));
            string generator = File.ReadAllText("Assets/Editor/Floor/FloorConfigGenerator.cs");
            Assert.That(generator, Does.Contain("EnsureAsset<FloorCollapseHazardConfig>"));
            string scene = File.ReadAllText("Assets/Scripts/Orchestrator/Scenes/HorrorRunSceneRoot.cs");
            Assert.That(scene, Does.Contain("_director.HearHeraldBroadcast"));
            Assert.That(scene, Does.Contain("_horror.LaunchProjectile, _horror.HitProjectile, _horror.TickProjectiles, _horror.ClearProjectiles"));
            Assert.That(scene, Does.Contain("modalVisible: () => _progressionUI.IsModalVisible"));
        }
        [Test] public void CollapseAudioAndTrapMixerUsePairedDomainAndPresentationRoutes()
        {
            string floor = File.ReadAllText("Assets/Scripts/Domain/Floor/Manager/FloorManager.cs");
            Assert.That(floor, Does.Contain("CollapseCue += HandleCollapseCue")); Assert.That(floor, Does.Contain("CollapseCue -= HandleCollapseCue"));
            string audio = File.ReadAllText("Assets/Scripts/Orchestrator/AudioOrchestrator.cs");
            Assert.That(audio, Does.Contain("CollapseCue += OnCollapseCue")); Assert.That(audio, Does.Contain("CollapseCue -= OnCollapseCue"));
            Assert.That(audio, Does.Contain("_audio.PlayCueAt(cue, position, 1f, roomId)")); Assert.That(audio, Does.Contain("_floor.ConfigureTrapAudio(_audio.EffectsGroup)"));
        }
        [Test] public void StareDefaultNamesDeathRatherThanCatch()
        { Assert.That(File.ReadAllText("Assets/Scripts/Domain/Hunter/Archetypes/Stare/Config/StareConfig.cs"), Does.Contain("_deathId = \"stare.death\"")); }
        [Test] public void HudAssetIsValidEmptyVectorHostAndExpandMatchesSetup()
        {
            const string root = "Assets/Resources/UI/Presentation/HUD/";
            var xml = new XmlDocument(); xml.Load(root + "HUD.uxml");
            var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("ui", "UnityEngine.UIElements");
            Assert.That(xml.SelectNodes("//ui:Label", ns).Count, Is.Zero);
            Assert.That(xml.SelectNodes("//ui:VisualElement[@name='hud']", ns).Count, Is.EqualTo(1));
            Assert.That(File.ReadAllText(root + "HUD.uss"), Does.Not.Contain(".hud-essential"));
            Assert.That(File.ReadAllText(root + "HUDPanelSettings.asset"), Does.Contain("m_ScreenMatchMode: 2"));
            Assert.That(File.ReadAllText("Assets/Editor/HUD/HUDSetup.cs"), Does.Contain("panel.screenMatchMode = PanelScreenMatchMode.Expand;"));
        }
        [Test] public void HeldServiceIsExplicitlyConfiguredAndReleasedBySceneOwner()
        {
            string source = File.ReadAllText("Assets/Scripts/Orchestrator/Scenes/HorrorRunSceneRoot.cs");
            int camera = source.IndexOf("SharedSceneRoot.InitializeGeneratedServices(");
            int held = source.IndexOf("_heldItem.Initialize(_camera.OutputCamera, _heldItemConfig);");
            int route = source.IndexOf("_heldItemRoute.Configure(_progression, _run, _expedition, _heldItem, _camera);");
            Assert.That(camera, Is.GreaterThanOrEqualTo(0)); Assert.That(held, Is.GreaterThan(camera)); Assert.That(route, Is.GreaterThan(held));
            Assert.That(source, Does.Contain("_heldItemRoute.Configure(null, null, null, null, null);"));
            Assert.That(source, Does.Contain("_heldItem.Teardown();"));
            string setup = File.ReadAllText("Assets/Editor/Scenes/HorrorRunSceneSetup.cs");
            Assert.That(setup, Does.Contain("RestoreHeldItem(root);"));
            Assert.That(setup, Does.Contain("Presentation/HeldItem/HeldItemDriverConfig.asset"));
            Assert.That(setup, Does.Contain("Universal Render Pipeline/Unlit"));
        }
    }
}
