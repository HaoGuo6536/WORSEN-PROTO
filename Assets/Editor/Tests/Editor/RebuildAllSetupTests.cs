// ============================================================================
// RebuildAllSetupTests.cs
// ============================================================================
// PURPOSE:
//   Locks the production rebuild manifest without executing asset writers.
//   Setup order must not change with menu registration or the current selection.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Editor setup composition.
// KEY RESPONSIBILITIES:
//   - Require one explicit ordered manifest with dependency reasons.
//   - Keep provenance last and diagnostics outside the production rebuild.
// DEPENDENCIES:
//   - RebuildAllSetup manifest and NUnit.
// USAGE NOTES:
//   Does not invoke any step. Actual rebuild is coordinator-only, outside tests.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using Worsen.Editor.Setup;

namespace Worsen.Tests.Editor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RebuildAllSetupTests
    {
        [Test]
        public void ManifestIsExplicitCompleteAndRepeatable()
        {
            var steps = RebuildAllSetup.CreateManifest();
            string[] expected = { "Admission", "Base configs", "Player effects", "Player art and prefab", "Hunter base",
                "Chase", "Floor", "Director", "Camera", "PostFX", "Telemetry", "Audio base", "HUD", "Results",
                "Procedural content", "Shop and catalogue", "Shrine", "TagArena", "FloorLoop", "HorrorRun",
                "Hunter roster audio", "Hunter roster visuals", "Final provenance" };
            Assert.That(steps.Select(step => step.Name), Is.EqualTo(expected));
            Assert.That(RebuildAllSetup.CreateManifest().Select(step => step.Name), Is.EqualTo(expected));
            Assert.That(steps.All(step => !string.IsNullOrWhiteSpace(step.Reason) && step.Execute != null), Is.True);
            Assert.That(steps.Select(step => step.Name).Distinct().Count(), Is.EqualTo(steps.Length));
        }
        [Test]
        public void AdmissionIgnoresBuiltInPackageAndShaderObjectsButChecksProjectSettings()
        {
            Assert.That(RebuildAllSetup.HoldsUserEdits(UnityEngine.Shader.Find("Hidden/InternalErrorShader")), Is.False);
            var settings = UnityEditor.AssetDatabase.LoadMainAssetAtPath("ProjectSettings/ProjectSettings.asset");
            Assert.That(settings, Is.Not.Null);
            Assert.That(RebuildAllSetup.HoldsUserEdits(settings), Is.True);
            var builtIn = UnityEditor.AssetDatabase.GetBuiltinExtraResource<UnityEngine.Material>("Default-Material.mat");
            Assert.That(builtIn == null || !RebuildAllSetup.HoldsUserEdits(builtIn), Is.True);
        }
    }
}
