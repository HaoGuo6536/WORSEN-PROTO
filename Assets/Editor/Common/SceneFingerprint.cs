// ============================================================================
// SceneFingerprint.cs
// ============================================================================
// PURPOSE:
//   Computes one portable capture fingerprint for scene builders and play gates.
//   Runtime source is normalized so editor/test edits and checkout line endings
//   do not invalidate scenes; configuration retains its existing byte contract.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Common provenance infrastructure.
// KEY RESPONSIBILITIES:
//   - Hash runtime .cs content with canonical paths, LF and trimmed line tails.
//   - Preserve the legacy configuration fingerprint and sha256: stamp format.
//   - Expose the content calculation for pure regression tests.
// DEPENDENCIES:
//   - System IO, text encoding and SHA-256; no Unity engine calls.
// USAGE NOTES:
//   Paths supplied to HashFiles are project-relative, as in all existing callers.
//   Only Assets/Scripts/*.cs recursively is source provenance; no git process,
//   editor files, metadata, timestamps or absolute checkout paths enter the hash.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Worsen.Editor.Common
{
    public static class SceneFingerprint
    {
        public static string HashFiles(string directory, string pattern)
        {
            if (directory == "Assets/Scripts" && pattern == "*.cs")
                return HashRuntimeSources(Directory.GetFiles(directory, pattern, SearchOption.AllDirectories)
                    .Select(path => new KeyValuePair<string, string>(path, File.ReadAllText(path))));

            // Configuration compatibility is deliberately independent of source normalization.
            var text = new StringBuilder();
            foreach (string path in Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                text.Append(path.Replace('\\', '/')).Append(Environment.NewLine).Append(File.ReadAllText(path)).Append(Environment.NewLine);
            return Digest(text.ToString());
        }

        public static string HashRuntimeSources(IEnumerable<KeyValuePair<string, string>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            var entries = sources.Select(item => new KeyValuePair<string, string>(item.Key.Replace('\\', '/'), item.Value))
                .Where(item => item.Key.StartsWith("Assets/Scripts/", StringComparison.Ordinal) && item.Key.EndsWith(".cs", StringComparison.Ordinal))
                .OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
            if (entries.Length == 0) throw new InvalidOperationException("No runtime source files found for scene provenance.");
            var text = new StringBuilder();
            string previous = null;
            foreach (var item in entries)
            {
                if (item.Key == previous) throw new ArgumentException("Duplicate runtime source path: " + item.Key, nameof(sources));
                previous = item.Key;
                text.Append(item.Key).Append('\n').Append(NormalizeContent(item.Value)).Append('\n');
            }
            return Digest(text.ToString());
        }

        public static string NormalizeContent(string content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            return string.Join("\n", content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(line => line.TrimEnd()));
        }

        private static string Digest(string text)
        {
            using (var hash = SHA256.Create())
                return "sha256:" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty);
        }
    }
}
