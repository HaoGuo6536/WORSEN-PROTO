// ============================================================================
// AudioPass3SetupTests.cs
// ============================================================================
// PURPOSE:
//   Checks the reviewed bank overlay without rewriting assets in test setup.
//   Saved-asset assertions require coordinator assignment and native Unity APIs.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Verify whole-file path selection, missing-content silence and bounded defaults.
//   - Reject partial resolution and prove saved bindings match the reviewed tables.
// DEPENDENCIES:
//   Audio editor setup, Core, Audio definitions, NUnit and UnityEditor.
// USAGE NOTES:
//   Pure cases inject a loader; native case reads assets after both setup menus.
// ============================================================================
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Editor.Audio;
using Worsen.Presentation.Audio;
namespace Worsen.Tests.Audio
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class AudioPass3SetupTests
    {
        [Test] public void ManifestHasAllRequestedBanksAndNoLegacyGrowlFallback()
        {
            var rows = HunterRosterAudioSetup.Parse(File.ReadAllText(HorrorAudioSetup.Pass3SelectionPath));
            foreach (var cue in new[] { CueId.CakeCollect, CueId.GoldenCakeCollect, CueId.Death, CueId.PlayerHit,
                CueId.GrabWarning, CueId.GrabStart, CueId.GrabHit, CueId.GrabEscape, CueId.RoomTear, CueId.MistAdvance, CueId.RoomConsumed })
                Assert.That(rows.Count(row => row.Bank == cue), Is.EqualTo(1), cue.ToString());
            foreach (var cue in new[] { CueId.Presence, CueId.Detection, CueId.Chase, CueId.EnemyWindup, CueId.EnemyRecovery, CueId.EnemyLost, CueId.EnemyScream })
            {
                var row = rows.Single(r => r.Bank == cue);
                Assert.That(row.Paths, Is.Empty); Assert.That(row.Gain, Is.Zero);
            }
            foreach (var row in rows.Where(r => r.Bank == CueId.GrabWarning || r.Bank == CueId.GrabStart || r.Bank == CueId.GrabHit ||
                r.Bank == CueId.GrabEscape || r.Bank == CueId.RoomTear || r.Bank == CueId.MistAdvance || r.Bank == CueId.RoomConsumed))
            {
                Assert.That(row.Paths.Length, Is.EqualTo(1)); Assert.That(row.Gain, Is.InRange(.01f, .14f));
            }
            foreach (var cue in new[] { CueId.CakeCollect, CueId.GoldenCakeCollect })
            {
                var row = rows.Single(r => r.Bank == cue);
                Assert.That(row.Paths, Is.Empty); Assert.That(row.Gain, Is.GreaterThan(0f), "Missing, not a false claim of approved silence.");
            }
        }
        [Test] public void OverlayDoesNotMutateInputAndRetiresLoopsWithoutLoadingMissingContent()
        {
            var input = new[] { new AudioSoundDefinition { Cue = CueId.MistAdvance, Gain = .6f, Loop = true, Ambience = true, Spatial = true } };
            var output = HorrorAudioSetup.ReviewBanks(input, "| cue:mist | MistAdvance | 0.12 | missing | - | fixture |\n", _ => throw new Exception("Must not load missing clips"));
            Assert.That(input[0].Loop, Is.True); Assert.That(input[0].Gain, Is.EqualTo(.6f));
            var bank = output[0]; Assert.That(bank.Clips, Is.Empty); Assert.That(bank.Gain, Is.EqualTo(.12f));
            Assert.That(bank.Spatial, Is.True); Assert.That(bank.Loop || bank.Ambience, Is.False);
            Assert.That(bank.GainVariation, Is.Zero); Assert.That(bank.PitchMinimum, Is.EqualTo(1)); Assert.That(bank.PitchMaximum, Is.EqualTo(1));
            Assert.That(bank.Cooldown, Is.Zero); Assert.That(bank.Priority, Is.EqualTo(55)); Assert.That(bank.MaxConcurrent, Is.EqualTo(1));
            Assert.That(bank.MinimumDistance, Is.EqualTo(1.8f)); Assert.That(bank.MaximumDistance, Is.EqualTo(16f));
        }
        [Test] public void RequiredMissingClipFailsBeforeChangingCallerData()
        {
            var input = new[] { new AudioSoundDefinition { Cue = CueId.Death, Gain = .6f } };
            string text = "| clip:a | Assets/External/Test/a.wav | 1 | 0 | 0 | fixture |\n| cue:death | Death | 0.18 | a | - | fixture |\n";
            var error = Assert.Throws<FileNotFoundException>(() => HorrorAudioSetup.ReviewBanks(input, text, _ => null));
            Assert.That(error.FileName, Is.EqualTo("Assets/External/Test/a.wav")); Assert.That(input[0].Gain, Is.EqualTo(.6f));
        }
        [Test] public void DuplicateOrAbsentBankFailsClosed()
        {
            string text = "| cue:a | Death | 0 | silence | - | fixture |\n";
            Assert.Throws<FormatException>(() => HorrorAudioSetup.ReviewBanks(Array.Empty<AudioSoundDefinition>(), text, _ => null));
            Assert.Throws<FormatException>(() => HorrorAudioSetup.ReviewBanks(new[] { new AudioSoundDefinition { Cue = CueId.Death } },
                text + text.Replace("cue:a", "cue:b"), _ => null));
        }
        [Test] public void AssignedProductionAssetsMatchBothReviewedTables()
        {
            var saved = AssetDatabase.LoadAssetAtPath<AudioSoundscapeDriverConfig>(HorrorAudioSetup.ConfigPath);
            Assert.That(saved, Is.Not.Null); Assert.That(saved.HeartbeatClip, Is.Null);
            var catalogue = new AudioCueCataloguePresenter();
            foreach (var row in HunterRosterAudioSetup.Parse(File.ReadAllText(HorrorAudioSetup.Pass3SelectionPath)))
            {
                if (!catalogue.TryGet(row.Bank, out _)) continue; // Setup deliberately prunes removed cue ids.
                var bank = saved.Sounds.Single(sound => sound.Cue == row.Bank);
                Assert.That(bank.Clips.Select(AssetDatabase.GetAssetPath), Is.EqualTo(row.Paths), row.Id);
                Assert.That(bank.Gain, Is.EqualTo(row.Gain), row.Id); Assert.That(bank.Loop, Is.False, row.Id);
            }
            foreach (var row in HunterRosterAudioSetup.Parse(File.ReadAllText(HunterRosterAudioSetup.SelectionPath)))
            {
                var binding = saved.RosterBindings.Single(b => b.Id == row.Id);
                var paths = binding.Clip == null ? Array.Empty<string>() : new[] { binding.Clip }.Concat(binding.Alternates ?? Array.Empty<AudioClip>()).Select(AssetDatabase.GetAssetPath).ToArray();
                Assert.That(paths, Is.EqualTo(row.Paths), row.Id); Assert.That(binding.Gain, Is.EqualTo(row.Gain), row.Id);
            }
        }
    }
}
