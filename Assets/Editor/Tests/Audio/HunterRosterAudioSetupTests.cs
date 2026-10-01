// ============================================================================
// HunterRosterAudioSetupTests.cs
// ============================================================================
// PURPOSE:
//   Validates the real path-only manifest without installing or copying audio.
//   Intentional silence is explicit data, not a dummy monster clip at zero volume.
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
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
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
                Assert.That(rows.Count(row => row.Id == tell), Is.EqualTo(1), "A reviewed missing/silent tell is explicit, never a borrowed vocal.");
            Assert.That(rows.Select(row => row.Id), Is.Ordered.Using<string>(StringComparer.Ordinal));
            foreach (var row in rows) Assert.That(row.Paths.All(path => path.StartsWith("Assets/External/")), Is.True);
        }
        [Test] public void FixedHeraldIdsAndQuietSnapNeverUseGenericFallback()
        {
            var rows = HunterRosterAudioSetup.Parse(File.ReadAllText(HunterRosterAudioSetup.SelectionPath));
            foreach (string id in new[] { "ms_mangled_scream_03", "sb_mangled_scream_01", "sb_mangled_scream_02", "sb_mangled_scream_03" })
                Assert.That(rows.Single(row => row.Id == id).Paths.Single(), Does.EndWith("/" + id + ".wav"));
            var snap = rows.Single(row => row.Id == "mannequin.death");
            Assert.That(snap.Paths.Single(), Does.EndWith("/SFX_Punch_Designed_Gore_01.wav"));
            Assert.That(snap.Gain, Is.EqualTo(.3f)); Assert.That(snap.Bank, Is.EqualTo(CueId.Death));
            foreach (string slot in new[] { "presence", "detection", "chase", "attack" })
                Assert.That(rows.Single(row => row.Id == "mannequin." + slot).Gain, Is.Zero);
            Assert.That(rows.Any(row => row.Id == "stare.i-see-you" || row.Id == "stare.find-me"), Is.False,
                "Unverified speech must remain an explicit content request, not an unrelated substituted line.");
        }
        [Test] public void ReviewedAlternativesRemainLocalAndFixedAttackTellsNeverRandomize()
        {
            var rows = HunterRosterAudioSetup.Parse(File.ReadAllText(HunterRosterAudioSetup.SelectionPath));
            Assert.That(rows.Single(row => row.Id == "echo.footstep").Paths.Length, Is.EqualTo(2));
            Assert.That(rows.Single(row => row.Id == "weaver.skitter").Paths.Length, Is.EqualTo(2));
            Assert.That(rows.Where(row => row.Paths.Length > 1).Select(row => row.Id), Is.EquivalentTo(new[] {
                "echo.presence", "weaver.presence", "weaver.detection", "weaver.chase", "ticking.presence",
                "ram.presence", "herald.chase", "echo.footstep", "weaver.skitter", "ram-stride" }));
            foreach (var row in rows.Where(row => row.Bank == CueId.EnemyWindup && row.Paths.Length > 0))
                Assert.That(row.Paths.Length, Is.EqualTo(1), row.Id);
            foreach (var row in rows.Where(row => !row.Id.StartsWith("ram", StringComparison.Ordinal)))
                Assert.That(row.Paths.Any(path => path.Contains("Roar_Scream") || path.Contains("Breath_Generic") || path.EndsWith("Monster Bite.wav")), Is.False, row.Id);
            foreach (string hunter in new[] { "echo", "weaver", "ticking", "ram", "skip", "mimic", "blinder", "herald", "mannequin", "stare" })
            foreach (string habit in new[] { "turn", "cake-reaction" })
            {
                var row = rows.Single(r => r.Id == hunter + "." + habit);
                Assert.That(row.Paths, Is.Empty); Assert.That(row.Gain, Is.Zero);
            }
            Assert.That(rows.Single(row => row.Id == "mannequin.long-step").Gain, Is.Zero, "No motion/hold fact exists to gate this mutation sound safely.");
        }
        [Test] public void Pass3RemovesConflictedLabelsAndCatchAliasMatchesDeath()
        {
            var rows = HunterRosterAudioSetup.Parse(File.ReadAllText(HunterRosterAudioSetup.SelectionPath));
            string[] rejected = { "tt2_goose", "jmg_crunch_rip_014", "jmg_bone-break_snap_027", "ca_pig", "Orcs_", "SFX_impactmechanical01.wav", "SFX_impactmechanical02.wav" };
            foreach (var row in rows)
            foreach (string path in row.Paths)
                Assert.That(rejected.Any(path.Contains), Is.False, row.Id + ": " + path);
            Assert.That(rows.Single(row => row.Id == "stare.catch").Paths,
                Is.EqualTo(rows.Single(row => row.Id == "stare.death").Paths));
            foreach (string id in new[] { "mimic.attack", "mimic.death", "mimic-wrong-bite", "herald.breath", "echo.detection", "echo.chase" })
            {
                var row = rows.Single(r => r.Id == id);
                Assert.That(row.Paths, Is.Empty, id); Assert.That(row.Gain, Is.GreaterThan(0f), id);
            }
        }
        [Test] public void MissingClipFailsLoudlyWithItsExactPath()
        {
            var selections = HunterRosterAudioSetup.Parse(Clip + Cue);
            var error = Assert.Throws<FileNotFoundException>(() => HunterRosterAudioSetup.Resolve(selections, _ => null));
            Assert.That(error.FileName, Is.EqualTo("Assets/External/Test/a.wav"));
        }
        [Test] public void ExplicitSilenceResolvesWithoutLoadingAnyClip()
        {
            var rows = HunterRosterAudioSetup.Parse("| cue:mimic.presence | Presence | 0 | silence | - | intentional silence |\n");
            var bindings = HunterRosterAudioSetup.Resolve(rows, _ => throw new InvalidOperationException("Silence must not load assets"));
            Assert.That(rows[0].Paths, Is.Empty); Assert.That(bindings[0].Placeholder, Is.True);
            Assert.That(bindings[0].OverrideGain, Is.True); Assert.That(bindings[0].Gain, Is.Zero);
        }
        [Test] public void MissingSelectionIsDistinctFromAuthoredSilence()
        {
            var rows = HunterRosterAudioSetup.Parse("| cue:echo.detection | Detection | 0.3 | missing | - | pending breath |\n");
            var binding = HunterRosterAudioSetup.Resolve(rows, _ => throw new InvalidOperationException("Missing must not load assets"))[0];
            Assert.That(binding.Placeholder, Is.True); Assert.That(binding.Clip, Is.Null);
            Assert.That(binding.OverrideGain, Is.True); Assert.That(binding.Gain, Is.EqualTo(.3f));
            Assert.Throws<FormatException>(() => HunterRosterAudioSetup.Parse("| cue:echo.detection | Detection | 0 | missing | - | invalid |\n"));
        }
        [TestCase("0.5", "-")][TestCase("0", "a")]
        public void SilenceRejectsNonzeroGainOrAlternates(string gain, string alternate) => Assert.Throws<FormatException>(() =>
            HunterRosterAudioSetup.Parse(Clip + "| cue:mimic.presence | Presence | " + gain + " | silence | " + alternate + " | intentional silence |\n"));
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
