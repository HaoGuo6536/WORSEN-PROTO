// ============================================================================
// HunterRosterAudioSetupTests.cs
// ============================================================================
// PURPOSE:
//   Validates the real path-only manifest without installing or copying audio.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Require all ten five-slot inventories and currently authored mutation tells.
//   - Reject missing paths, duplicate ids, unsafe paths and invalid source gains.
//   - Preserve exact Herald identities and the quiet Mannequin snap selection.
// DEPENDENCIES:
//   - Audio editor setup parser, Core cue ids, NUnit and read-only file IO.
// USAGE NOTES:
//   Parser tests are managed-only. Asset resolution/playback needs the coordinator.
// ============================================================================
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Worsen.Core;
using Worsen.Editor.Audio;
namespace Worsen.Tests.Audio
{
    public sealed class HunterRosterAudioSetupTests
    {
        private const string Clip = "| clip:a | Assets/External/Test/a.wav | 1 | -3 | -20 | fixture |\n";
        private const string Cue = "| cue:echo.presence | Presence | 0.5 | a | - | fixture |\n";
        [Test] public void RealSelectionCoversFiveSlotsForEveryHunterAndAllAuthoredMutationTells()
        {
            var rows = HunterRosterAudioSetup.Parse(File.ReadAllText(HunterRosterAudioSetup.SelectionPath));
            foreach (string hunter in new[] { "echo", "weaver", "ticking", "ram", "skip", "mimic", "blinder", "herald", "mannequin", "stare" })
            foreach (string slot in new[] { "presence", "detection", "chase", "attack", "death" })
                Assert.That(rows.Count(row => row.Id == hunter + "." + slot), Is.EqualTo(1), hunter + "." + slot);
            foreach (string tell in new[] { "echo.quickened-recording", "weaver-quickened-skitter", "blinder-quickened-approach",
                "herald-quickened-approach", "mannequin.long-step", "stare.quickened-gaze" })
                Assert.That(rows.Single(row => row.Id == tell).Paths, Is.Not.Empty);
            Assert.That(rows.Select(row => row.Id), Is.Ordered.Using<string>(StringComparer.Ordinal));
            foreach (var row in rows) Assert.That(row.Paths.All(path => path.StartsWith("Assets/External/")), Is.True);
        }
        [Test] public void FixedHeraldIdsAndQuietSnapNeverUseGenericFallback()
        {
            var rows = HunterRosterAudioSetup.Parse(File.ReadAllText(HunterRosterAudioSetup.SelectionPath));
            foreach (string id in new[] { "ms_mangled_scream_03", "sb_mangled_scream_01", "sb_mangled_scream_02", "sb_mangled_scream_03" })
                Assert.That(rows.Single(row => row.Id == id).Paths.Single(), Does.EndWith("/" + id + ".wav"));
            var snap = rows.Single(row => row.Id == "mannequin.death");
            Assert.That(snap.Paths.Single(), Does.EndWith("/jmg_bone-break_snap_027.wav"));
            Assert.That(snap.Gain, Is.EqualTo(.3f)); Assert.That(snap.Bank, Is.EqualTo(CueId.Death));
            foreach (string slot in new[] { "presence", "detection", "chase", "attack" })
                Assert.That(rows.Single(row => row.Id == "mannequin." + slot).Gain, Is.Zero);
            Assert.That(rows.Any(row => row.Id == "stare.i-see-you" || row.Id == "stare.find-me"), Is.False,
                "Unverified speech must remain an explicit content request, not an unrelated substituted line.");
        }
        [Test] public void AlternativesExistButFixedAttackTellsHaveOneSelectedClip()
        {
            var rows = HunterRosterAudioSetup.Parse(File.ReadAllText(HunterRosterAudioSetup.SelectionPath));
            Assert.That(rows.Count(row => row.Paths.Length > 1), Is.GreaterThan(10));
            foreach (var row in rows.Where(row => row.Bank == CueId.EnemyWindup)) Assert.That(row.Paths.Length, Is.EqualTo(1), row.Id);
        }
        [Test] public void MissingClipFailsLoudlyWithItsExactPath()
        {
            var selections = HunterRosterAudioSetup.Parse(Clip + Cue);
            var error = Assert.Throws<FileNotFoundException>(() => HunterRosterAudioSetup.Resolve(selections, _ => null));
            Assert.That(error.FileName, Is.EqualTo("Assets/External/Test/a.wav"));
        }
        [Test] public void DuplicateCueAndUnresolvedClipAreRejected()
        {
            Assert.Throws<FormatException>(() => HunterRosterAudioSetup.Parse(Clip + Cue + Cue));
            Assert.Throws<FormatException>(() => HunterRosterAudioSetup.Parse(Cue));
        }
        [TestCase("NaN")][TestCase("Infinity")][TestCase("-0.1")][TestCase("1.1")]
        public void InvalidGainsFailBeforeResolution(string gain) => Assert.Throws<FormatException>(() => HunterRosterAudioSetup.Parse(Clip + Cue.Replace("0.5", gain)));
        [TestCase("Assets/External/../secret.wav")][TestCase("C:/vendor/a.wav")][TestCase("Assets/Audio/a.wav")]
        public void NonVendorOrTraversalPathsAreRejected(string path) => Assert.Throws<FormatException>(() => HunterRosterAudioSetup.Parse(Clip.Replace("Assets/External/Test/a.wav", path) + Cue));
    }
}
