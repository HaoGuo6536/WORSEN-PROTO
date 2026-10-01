// ============================================================================
// SceneFingerprintTests.cs
// ============================================================================
// PURPOSE:
//   Proves source stamps survive checkout text differences without hiding source
//   changes. The tests use supplied content and do not require Unity or git.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Editor provenance.
// KEY RESPONSIBILITIES:
//   - Verify LF, CRLF, trailing whitespace, ordering and path normalization.
//   - Verify runtime-only scope, meaningful changes and invalid input rejection.
// DEPENDENCIES:
//   - Common SceneFingerprint and NUnit only.
// USAGE NOTES:
//   Pure tests; no files, scenes, assets or engine state are modified.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Worsen.Editor.Common;

namespace Worsen.Tests.Editor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class SceneFingerprintTests
    {
        private static KeyValuePair<string, string> Source(string path, string content) => new KeyValuePair<string, string>(path, content);
        private static string Hash(string content) => SceneFingerprint.HashRuntimeSources(new[] { Source("Assets/Scripts/Core/A.cs", content) });

        [TestCase("class A { }\n", "class A { }\r\n")]
        [TestCase("class A { }\n", "class A { } \t\r\n")]
        [TestCase("a\nb\n", "a\rb\r")]
        public void EquivalentCheckoutTextHasIdenticalStamp(string first, string second) => Assert.That(Hash(first), Is.EqualTo(Hash(second)));

        [Test]
        public void CanonicalPathsAndOrdinalOrderArePortable()
        {
            var first = new[] { Source("Assets/Scripts/Z.cs", "z"), Source("Assets/Scripts/A.cs", "a") };
            var second = new[] { Source("Assets\\Scripts\\A.cs", "a"), Source("Assets\\Scripts\\Z.cs", "z") };
            Assert.That(SceneFingerprint.HashRuntimeSources(first), Is.EqualTo(SceneFingerprint.HashRuntimeSources(second)));
        }

        [Test]
        public void EditorTestsAndMetadataDoNotEnterRuntimeStamp()
        {
            var files = new[] { Source("Assets/Scripts/Core/A.cs", "a"), Source("Assets/Editor/Setup/X.cs", "editor"),
                Source("Assets/Editor/Tests/X.cs", "test"), Source("Assets/Scripts/Core/A.cs.meta", "metadata") };
            Assert.That(SceneFingerprint.HashRuntimeSources(files), Is.EqualTo(Hash("a")));
        }

        [TestCase("a", "b")]
        [TestCase(" a", "a")]
        [TestCase("a b", "a  b")]
        public void MeaningfulContentChangesRemainVisible(string first, string second) => Assert.That(Hash(first), Is.Not.EqualTo(Hash(second)));

        [Test]
        public void RenamingRuntimeFileChangesStamp() => Assert.That(Hash("a"), Is.Not.EqualTo(
            SceneFingerprint.HashRuntimeSources(new[] { Source("Assets/Scripts/Core/B.cs", "a") })));

        [Test]
        public void StampKeepsExistingReadableFormat() => Assert.That(Hash("a"), Does.Match("^sha256:[A-F0-9]{64}$"));

        [Test]
        public void EmptyRuntimeSelectionFailsClosed() => Assert.Throws<InvalidOperationException>(() =>
            SceneFingerprint.HashRuntimeSources(new[] { Source("Assets/Editor/X.cs", "a") }));

        [Test]
        public void DuplicateCanonicalPathFailsClosed() => Assert.Throws<ArgumentException>(() =>
            SceneFingerprint.HashRuntimeSources(new[] { Source("Assets/Scripts/A.cs", "a"), Source("Assets\\Scripts\\A.cs", "b") }));
    }
}
