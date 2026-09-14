// ============================================================================
// ArchitectureConformanceTests.cs
// ============================================================================
//
// PURPOSE:
//   Enforces the rules in PROJECT_ARCHITECTURE_GUIDELINES.md mechanically, so
//   that the architecture is a build gate rather than a prose aspiration. Prose
//   rules decay: a header goes stale, a MonoBehaviour appears in a Controller
//   folder, a menu item escapes the project root, and nobody notices until the
//   layering is gone. This suite walks Assets/Scripts and Assets/Editor on every
//   test run and fails loudly with a file-by-file list of every deviation.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11, §13d) · project-wide.
//   This is the fourth of the four enforcement layers described in §13: asmdefs
//   catch cross-layer references at compile time (§13a), ast-grep catches
//   structural patterns inside files (§13b), GitNexus catches relationships
//   between files (§13c), and this suite catches everything that requires
//   walking the folder tree: headers, namespaces, naming, placement, and the
//   existence of tests for pure-layer logic. It names the Core layer, the
//   Domain layer, the Session layer, the Presentation layer and the
//   Orchestrator layer only as folder vocabulary; it references no game system.
//
// KEY RESPONSIBILITIES:
//   - Require the §0 documentation header, with all five sections, on every
//     script in the versioned source trees.
//   - Require every namespace to equal Worsen.[Layer].[System] derived from the
//     file path (§12), and every class suffix to match its folder.
//   - Confine MonoBehaviours to Manager/, Driver/ and Orchestrator/ (§13d).
//   - Require Managers to declare system kind (§1b) and lifecycle tier (§8),
//     and Entity Managers to implement IEntityHandle.
//   - Require a read-only view on any BehaviorState another system reads (§2c).
//   - Require a test file for every pure-layer script with real logic (§11).
//   - Require every [MenuItem] to sit under the Worsen/ root (§10).
//   - Reject assets, editor code, test folders and dead folders inside
//     Assets/Scripts (§4a, §10, §11, §12).
//
// DEPENDENCIES:
//   - None at runtime. This suite reads files from disk and does not reference
//     any game system, so it cannot itself violate the layer graph.
//   - NUnit, and UnityEngine.Application for the project path.
//
// USAGE NOTES:
//   - THIS SUITE IS NEVER SKIPPED AND NEVER [Ignore]d to make a change pass
//     (§11). When it fails, either the code is wrong or the guidelines document
//     is wrong; fix one of them in the same commit, never let them diverge.
//   - The three project-specific values live in the constants block below: the
//     namespace/asmdef prefix, the editor menu root, and the statement
//     threshold above which a pure-layer script must ship tests. Changing them
//     here is the supported way to retune the suite.
//   - Checks are textual by design. They read source files rather than
//     reflecting over loaded assemblies, so a file that fails to compile for
//     architectural reasons still produces a useful message.
//   - Lifecycle tier: not applicable. This is an editor-only test fixture and
//     instantiates nothing.
//
// ============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Worsen.Tests.Architecture
{
    [TestFixture]
    public sealed class ArchitectureConformanceTests
    {
        // --------------------------------------------------------------------
        // Project-specific configuration (the §12 adoption values)
        // --------------------------------------------------------------------

        private const string ProjectPrefix = "Worsen";
        private const string MenuRoot = "Worsen/";
        private const int StatementThresholdForTests = 20;

        // --------------------------------------------------------------------
        // Vocabulary taken from the guidelines
        // --------------------------------------------------------------------

        private static readonly string[] LayerFolders =
            { "Core", "Domain", "Session", "Presentation", "Orchestrator" };

        private static readonly string[] SkeletonFolders =
            { "Manager", "Controller", "State", "Config", "Definitions", "Driver", "Samples" };

        private static readonly string[] HeaderSections =
            { "PURPOSE:", "ARCHITECTURAL ROLE:", "KEY RESPONSIBILITIES:", "DEPENDENCIES:", "USAGE NOTES:" };

        private static readonly string[] TaxonomyTypes =
        {
            "Manager", "Factory", "Registry", "Controller", "Utility", "BehaviorState",
            "DriverConfig", "DriverState", "Config", "Content SO", "Definitions",
            "Orchestrator", "SceneRoot", "Presenter", "Driver", "Sub-driver", "Editor tool"
        };

        private static readonly string[] SystemKinds =
            { "Service system", "Entity system", "Session system" };

        private static readonly string[] LifecycleTiers = { "Persistent", "Scene-owned" };

        /// <summary>Folder name mapped to the class-name suffixes allowed inside it (§12).</summary>
        private static readonly Dictionary<string, string[]> SuffixesByFolder = new Dictionary<string, string[]>
        {
            { "Manager", new[] { "Manager", "Factory", "Registry" } },
            { "Controller", new[] { "Controller", "Utility" } },
            { "State", new[] { "BehaviorState", "State" } },
            { "Config", new[] { "Config", "DriverConfig", "Profile", "SO", "Data" } }
        };

        // --------------------------------------------------------------------
        // Tests
        // --------------------------------------------------------------------

        [Test]
        public void EveryScriptBeginsWithTheMandatoryHeader()
        {
            var violations = new List<string>();

            foreach (var file in AllSourceFiles())
            {
                var header = HeaderOf(file);

                if (!Regex.IsMatch(header, @"^//\s*=====", RegexOptions.Multiline))
                {
                    violations.Add(Rel(file) + ": missing the '// ====' banner as its first line (§0).");
                    continue;
                }

                var missing = HeaderSections.Where(s => !header.Contains(s)).ToArray();
                if (missing.Length > 0)
                {
                    violations.Add(Rel(file) + ": header is missing " + string.Join(", ", missing) + " (§0).");
                }
            }

            AssertNoViolations(violations, "Script headers (§0)");
        }

        [Test]
        public void EveryHeaderDeclaresATaxonomyTypeAndALayer()
        {
            var violations = new List<string>();

            foreach (var file in ScriptFiles())
            {
                var role = HeaderSection(HeaderOf(file), "ARCHITECTURAL ROLE:");

                if (string.IsNullOrWhiteSpace(role))
                {
                    violations.Add(Rel(file) + ": ARCHITECTURAL ROLE section is empty (§0).");
                    continue;
                }

                if (!TaxonomyTypes.Any(role.Contains))
                {
                    violations.Add(Rel(file) + ": ARCHITECTURAL ROLE names no taxonomy type (§0, Quick Reference).");
                }

                if (!LayerFolders.Any(role.Contains))
                {
                    violations.Add(Rel(file) + ": ARCHITECTURAL ROLE names no layer (§0, §9).");
                }
            }

            AssertNoViolations(violations, "Architectural role declarations (§0, §13d)");
        }

        [Test]
        public void NamespaceMirrorsFolderPath()
        {
            var violations = new List<string>();

            foreach (var file in AllSourceFiles())
            {
                var expected = ExpectedNamespace(Rel(file));
                if (expected == null)
                {
                    violations.Add(Rel(file) + ": sits outside every declared layer folder (" +
                                   string.Join(", ", LayerFolders) + ") (§12).");
                    continue;
                }

                var match = Regex.Match(CodeOf(file), @"^\s*namespace\s+([A-Za-z0-9_.]+)", RegexOptions.Multiline);
                if (!match.Success)
                {
                    violations.Add(Rel(file) + ": declares no namespace; expected '" + expected + "' (§12).");
                }
                else if (match.Groups[1].Value != expected)
                {
                    violations.Add(Rel(file) + ": namespace is '" + match.Groups[1].Value +
                                   "', expected '" + expected + "' (§12).");
                }
            }

            AssertNoViolations(violations, "Namespace mirroring (§12)");
        }

        [Test]
        public void ClassSuffixMatchesItsFolder()
        {
            var violations = new List<string>();

            foreach (var file in ScriptFiles())
            {
                var rel = Rel(file);
                var name = Path.GetFileNameWithoutExtension(file);

                if (LayerOf(rel) == "Orchestrator")
                {
                    if (!name.EndsWith("Orchestrator") && !name.EndsWith("SceneRoot"))
                    {
                        violations.Add(rel + ": files in the Orchestrator layer are named " +
                                       "[Target]Orchestrator or [Scene]SceneRoot (§6, §6b).");
                    }

                    continue;
                }

                var folder = SkeletonFolderOf(rel);
                string[] allowed;
                if (folder == null || !SuffixesByFolder.TryGetValue(folder, out allowed))
                {
                    // Definitions/, Driver/ and Samples/ hold descriptive names by design (§5, §7e).
                    continue;
                }

                if (folder == "State" && name.StartsWith("IReadOnly") && name.EndsWith("State"))
                {
                    continue; // The read-only view declared beside its state (§2c).
                }

                if (!allowed.Any(name.EndsWith))
                {
                    violations.Add(rel + ": a script in " + folder + "/ must be named [X]" +
                                   string.Join(" / [X]", allowed) + " (§12).");
                }
            }

            AssertNoViolations(violations, "Class suffix versus folder (§12)");
        }

        [Test]
        public void MonoBehavioursLiveOnlyInManagerDriverOrOrchestrator()
        {
            var violations = new List<string>();

            foreach (var file in ScriptFiles())
            {
                if (!Regex.IsMatch(CodeOf(file), @":\s*[^;{]*\bMonoBehaviour\b"))
                {
                    continue;
                }

                var rel = Rel(file);
                var folder = SkeletonFolderOf(rel);

                if (LayerOf(rel) == "Orchestrator" || folder == "Manager" || folder == "Driver")
                {
                    continue;
                }

                violations.Add(rel + ": MonoBehaviours belong in Manager/, Driver/ or Orchestrator/ only " +
                               "(§2, §3, §7b, §7c, §13d).");
            }

            AssertNoViolations(violations, "MonoBehaviour placement (§13d)");
        }

        [Test]
        public void EveryManagerDeclaresSystemKindAndLifecycleTier()
        {
            var violations = new List<string>();

            foreach (var file in ManagerFiles())
            {
                var header = HeaderOf(file);

                if (!SystemKinds.Any(header.Contains))
                {
                    violations.Add(Rel(file) + ": header declares no system kind; say one of " +
                                   string.Join(", ", SystemKinds) + " (§1b).");
                }

                if (!LifecycleTiers.Any(header.Contains))
                {
                    violations.Add(Rel(file) + ": header declares no lifecycle tier; " +
                                   "say Persistent or Scene-owned (§8).");
                }
            }

            AssertNoViolations(violations, "Manager kind and lifecycle declarations (§1b, §8)");
        }

        [Test]
        public void EveryEntityManagerImplementsIEntityHandle()
        {
            var violations = new List<string>();

            foreach (var file in ManagerFiles())
            {
                if (!HeaderOf(file).Contains("Entity system"))
                {
                    continue;
                }

                if (!Regex.IsMatch(CodeOf(file), @"\bIEntityHandle\b"))
                {
                    violations.Add(Rel(file) + ": an Entity Manager must implement IEntityHandle (§1b).");
                }
            }

            AssertNoViolations(violations, "Entity Manager identity (§1b)");
        }

        [Test]
        public void BehaviorStatesReadByOtherSystemsExposeAReadOnlyView()
        {
            var violations = new List<string>();
            var allScripts = ScriptFiles().ToArray();

            foreach (var file in allScripts.Where(f => Path.GetFileName(f).EndsWith("BehaviorState.cs")))
            {
                var rel = Rel(file);
                var owningSystem = SystemPathOf(rel);
                var typeName = Path.GetFileNameWithoutExtension(file);
                var pattern = @"\b" + Regex.Escape(typeName) + @"\b";

                var readByForeignSystem = allScripts.Any(other =>
                    SystemPathOf(Rel(other)) != owningSystem && Regex.IsMatch(CodeOf(other), pattern));

                if (!readByForeignSystem)
                {
                    continue;
                }

                var folder = Path.GetDirectoryName(file);
                var hasView = folder != null && Directory.GetFiles(folder, "IReadOnly*State.cs").Length > 0;

                if (!hasView)
                {
                    violations.Add(rel + ": " + typeName + " is referenced outside " + owningSystem +
                                   "; declare IReadOnly[System]State beside it and hand that out instead (§2c, §3).");
                }
            }

            AssertNoViolations(violations, "Read-only state views (§2c, §3)");
        }

        [Test]
        public void PureLayerScriptsWithRealLogicShipTests()
        {
            var violations = new List<string>();
            var testFiles = SourceFiles(EditorRoot).Select(Path.GetFileName).ToArray();
            var testableSuffixes = new[] { "Controller.cs", "Presenter.cs", "Utility.cs", "SO.cs" };

            foreach (var file in ScriptFiles())
            {
                var fileName = Path.GetFileName(file);
                if (!testableSuffixes.Any(fileName.EndsWith))
                {
                    continue;
                }

                var statementCount = CodeOf(file)
                    .Split('\n')
                    .Where(l => !l.TrimStart().StartsWith("using "))
                    .Sum(l => l.Count(c => c == ';'));

                if (statementCount <= StatementThresholdForTests)
                {
                    continue;
                }

                var expected = Path.GetFileNameWithoutExtension(file) + "Tests.cs";
                if (!testFiles.Contains(expected))
                {
                    violations.Add(Rel(file) + ": has real logic but no " + expected +
                                   " under Assets/Editor/Tests/ (§11).");
                }
            }

            AssertNoViolations(violations, "Tests for pure-layer logic (§11)");
        }

        [Test]
        public void EveryMenuItemSitsUnderTheProjectRoot()
        {
            var violations = new List<string>();

            foreach (var file in SourceFiles(EditorRoot))
            {
                foreach (Match match in Regex.Matches(CodeOf(file), "\\[\\s*MenuItem\\s*\\(\\s*\"([^\"]+)\""))
                {
                    var path = match.Groups[1].Value;

                    if (path.StartsWith("CONTEXT/"))
                    {
                        continue; // Inspector context menus are addressed by Unity, not by us.
                    }

                    if (!path.StartsWith(MenuRoot))
                    {
                        violations.Add(Rel(file) + ": menu item '" + path + "' must live under '" +
                                       MenuRoot + "' (§10).");
                    }
                    else if (path.Split('/').Length < 3)
                    {
                        violations.Add(Rel(file) + ": menu item '" + path +
                                       "' is a bare item at the root; group it under a system (§10).");
                    }
                }
            }

            AssertNoViolations(violations, "Editor menu placement (§10)");
        }

        [Test]
        public void EveryLayerHasItsAssemblyDefinition()
        {
            var violations = new List<string>();

            foreach (var layer in LayerFolders)
            {
                var expected = ScriptsRoot + "/" + layer + "/" + ProjectPrefix + "." + layer + ".asmdef";
                if (!File.Exists(expected))
                {
                    violations.Add("Scripts/" + layer + "/" + ProjectPrefix + "." + layer +
                                   ".asmdef is missing; the layer graph is enforced by asmdefs (§13a).");
                }
            }

            AssertNoViolations(violations, "Layer assembly definitions (§13a)");
        }

        [Test]
        public void ScriptsTreeContainsNoAssetsEditorCodeOrTestFolders()
        {
            var violations = new List<string>();

            if (!Directory.Exists(ScriptsRoot))
            {
                Assert.Fail("Assets/Scripts does not exist.");
            }

            foreach (var asset in Directory.EnumerateFiles(ScriptsRoot, "*.asset", SearchOption.AllDirectories))
            {
                violations.Add(Rel(asset) +
                               ": ScriptableObject assets belong under Assets/Resources/ScriptableObjects/ (§4a).");
            }

            foreach (var dir in Directory.EnumerateDirectories(ScriptsRoot, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(dir);

                if (name == "Editor")
                {
                    violations.Add(Rel(dir) + ": editor code belongs in Assets/Editor/[System]/ (§10).");
                }
                else if (name == "Tests")
                {
                    violations.Add(Rel(dir) + ": unit tests belong in Assets/Editor/Tests/[System]/ (§11).");
                }
            }

            AssertNoViolations(violations, "Scripts tree contents (§4a, §10, §11)");
        }

        [Test]
        public void ScriptsTreeHasNoDeadFolders()
        {
            var violations = new List<string>();

            foreach (var dir in Directory.EnumerateDirectories(ScriptsRoot, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(dir).StartsWith("."))
                {
                    continue;
                }

                var hasSource =
                    Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).Any() ||
                    Directory.EnumerateFiles(dir, "*.asmdef", SearchOption.AllDirectories).Any();

                if (!hasSource)
                {
                    violations.Add(Rel(dir) + ": empty directories are deleted, not kept for later (§12).");
                }
            }

            AssertNoViolations(violations, "Dead folders (§12)");
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        private static string AssetsRoot
        {
            get { return Application.dataPath.Replace('\\', '/'); }
        }

        private static string ScriptsRoot
        {
            get { return AssetsRoot + "/Scripts"; }
        }

        private static string EditorRoot
        {
            get { return AssetsRoot + "/Editor"; }
        }

        private static IEnumerable<string> ScriptFiles()
        {
            return SourceFiles(ScriptsRoot);
        }

        private static IEnumerable<string> ManagerFiles()
        {
            return ScriptFiles().Where(f => Path.GetFileName(f).EndsWith("Manager.cs"));
        }

        private static IEnumerable<string> AllSourceFiles()
        {
            return SourceFiles(ScriptsRoot).Concat(SourceFiles(EditorRoot));
        }

        private static IEnumerable<string> SourceFiles(string root)
        {
            if (!Directory.Exists(root))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p);
        }

        /// <summary>Path relative to Assets/, using forward slashes.</summary>
        private static string Rel(string absolute)
        {
            var normalized = absolute.Replace('\\', '/');
            return normalized.StartsWith(AssetsRoot)
                ? normalized.Substring(AssetsRoot.Length).TrimStart('/')
                : normalized;
        }

        /// <summary>The leading comment block. Blank lines above it are tolerated.</summary>
        private static string HeaderOf(string file)
        {
            var builder = new StringBuilder();
            var started = false;

            foreach (var line in File.ReadLines(file))
            {
                var trimmed = line.TrimStart();

                if (!started)
                {
                    if (trimmed.Length == 0)
                    {
                        continue;
                    }

                    if (!trimmed.StartsWith("//"))
                    {
                        break;
                    }

                    started = true;
                }
                else if (!trimmed.StartsWith("//"))
                {
                    break;
                }

                builder.AppendLine(trimmed);
            }

            return builder.ToString();
        }

        /// <summary>The body of one named header section, up to the next section or the closing banner.</summary>
        private static string HeaderSection(string header, string sectionName)
        {
            var builder = new StringBuilder();
            var inside = false;

            foreach (var raw in header.Split('\n'))
            {
                var line = raw.TrimEnd();
                var text = line.StartsWith("//") ? line.Substring(2).Trim() : line.Trim();

                if (!inside)
                {
                    inside = text == sectionName;
                    continue;
                }

                if (HeaderSections.Contains(text) || text.StartsWith("====="))
                {
                    break;
                }

                builder.AppendLine(text);
            }

            return builder.ToString();
        }

        /// <summary>File contents with whole-line comments removed, so prose keywords cannot match.</summary>
        private static string CodeOf(string file)
        {
            return string.Join("\n", File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith("//")));
        }

        /// <summary>First path segment below Scripts/, or null for files outside the layer folders.</summary>
        private static string LayerOf(string rel)
        {
            var segments = rel.Split('/');
            return segments.Length >= 3 && segments[0] == "Scripts" && LayerFolders.Contains(segments[1])
                ? segments[1]
                : null;
        }

        /// <summary>Nearest enclosing skeleton folder (Manager/, Controller/, ...), or null.</summary>
        private static string SkeletonFolderOf(string rel)
        {
            var segments = rel.Split('/');

            for (var i = segments.Length - 2; i >= 0; i--)
            {
                if (SkeletonFolders.Contains(segments[i]))
                {
                    return segments[i];
                }
            }

            return null;
        }

        /// <summary>The system a file belongs to, as "Layer/System/Nested". Used for foreign-reference checks.</summary>
        private static string SystemPathOf(string rel)
        {
            var layer = LayerOf(rel);
            if (layer == null)
            {
                return "?";
            }

            if (layer == "Core" || layer == "Orchestrator")
            {
                return layer;
            }

            var segments = rel.Split('/');
            var parts = new List<string> { layer };

            for (var i = 2; i < segments.Length - 1; i++)
            {
                if (SkeletonFolders.Contains(segments[i]))
                {
                    break;
                }

                parts.Add(segments[i]);
            }

            return string.Join("/", parts);
        }

        /// <summary>Namespace required by §12, derived from the file path. Null when the file is misplaced.</summary>
        private static string ExpectedNamespace(string rel)
        {
            var segments = rel.Split('/');

            if (segments[0] == "Editor")
            {
                if (segments.Length >= 2 && segments[1] == "Tests")
                {
                    return segments.Length > 3
                        ? ProjectPrefix + ".Tests." + segments[2]
                        : ProjectPrefix + ".Tests";
                }

                return segments.Length > 2
                    ? ProjectPrefix + ".Editor." + segments[1]
                    : ProjectPrefix + ".Editor";
            }

            var layer = LayerOf(rel);
            if (layer == null)
            {
                return null;
            }

            if (layer == "Core" || layer == "Orchestrator")
            {
                return ProjectPrefix + "." + layer;
            }

            var system = new List<string>();
            for (var i = 2; i < segments.Length - 1; i++)
            {
                if (SkeletonFolders.Contains(segments[i]))
                {
                    break;
                }

                system.Add(segments[i]);
            }

            return system.Count == 0
                ? ProjectPrefix + "." + layer
                : ProjectPrefix + "." + layer + "." + string.Join(".", system);
        }

        private static void AssertNoViolations(ICollection<string> violations, string what)
        {
            if (violations.Count == 0)
            {
                return;
            }

            Assert.Fail(what + ": " + violations.Count +
                        " violation(s) of PROJECT_ARCHITECTURE_GUIDELINES.md\n  - " +
                        string.Join("\n  - ", violations));
        }
    }
}
