// ============================================================================
// ProjectShaderCompileTests.cs
// ============================================================================
// PURPOSE:
//   Fails when a project shader has compile errors. The editor only marks such a
//   shader on its asset, while a player build stops on it (batch 22: CamcorderFrame).
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests (§11) · Core integration contracts.
// KEY RESPONSIBILITIES:
//   - Check every shader under Assets/Shaders for import-time compile errors.
// DEPENDENCIES:
//   NUnit and UnityEditor ShaderUtil.
// USAGE NOTES:
//   Requires Unity Edit Mode. Vendor shaders outside Assets/Shaders are not checked.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Worsen.Tests.Core
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProjectShaderCompileTests
    {
        [Test]
        public void ProjectShadersCompileWithoutErrors()
        {
            var shaders = AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Shaders" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => (path, shader: AssetDatabase.LoadAssetAtPath<Shader>(path)))
                .Where(entry => entry.shader != null).ToArray();
            Assert.That(shaders, Is.Not.Empty, "Assets/Shaders holds the project's shaders.");
            var failures = shaders.Where(entry => ShaderUtil.ShaderHasError(entry.shader))
                .Select(entry => entry.path + ": " + string.Join(" | ", ShaderUtil.GetShaderMessages(entry.shader)
                    .Where(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    .Select(message => message.message + " (" + message.file + ":" + message.line + ")")))
                .ToArray();
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
    }
}
