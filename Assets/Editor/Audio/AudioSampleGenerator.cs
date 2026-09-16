// ============================================================================
// AudioSampleGenerator.cs
// ============================================================================
//
// PURPOSE:
//   Creates original audible prototype sounds without an external sound library.
//   A deterministic waveform recipe makes every missing sample reproducible and
//   keeps placeholder content explicitly identified for later listening and tuning.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Audio.
//
// KEY RESPONSIBILITIES:
//   - Write missing mono PCM samples for cues and seamless proximity layers.
//   - Preserve existing sample identities and replacements on repeated setup.
//   - Import only the generated files before config wiring resolves their clips.
//
// DEPENDENCIES:
//   - UnityEditor imports audio assets; System math and seeded randomness synthesize samples.
//
// USAGE NOTES:
//   - Editor-only; caller must hold the repository Unity lease before invocation.
//   - These are original synthesized placeholders, not validated distance-band feedback.
//   - Existing files are never overwritten by the setup path.
//
// ============================================================================

using System;
using System.IO;
using UnityEditor;

namespace Worsen.Editor.Audio
{
    public static class AudioSampleGenerator
    {
        public const string AudioFolder = "Assets/Resources/Audio/Presentation/Audio";
        private const int SampleRate = 22050;

        [MenuItem("Worsen/Audio/Create Prototype Samples")]
        public static void EnsureSamples()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before generating Audio assets.");
            EnsureFolder();
            WriteMissing("Presence", 0, 0.8);
            WriteMissing("Detection", 1, 0.5);
            WriteMissing("Chase", 2, 1.2);
            WriteMissing("Lose", 3, 0.8);
            WriteMissing("Death", 4, 1.3);
            WriteMissing("Footstep", 5, 0.16);
            WriteMissing("ExitOpen", 6, 1.2);
            WriteMissing("RoomTelegraph", 7, 6.0);
            WriteMissing("BreathLoop", 8, 2.0);
            WriteMissing("HunterLoop", 9, 2.0);
        }

        private static void EnsureFolder()
        {
            string folder = "Assets";
            foreach (string part in new[] { "Resources", "Audio", "Presentation", "Audio" })
            {
                string next = folder + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(folder, part);
                folder = next;
            }
        }

        private static void WriteMissing(string name, int kind, double seconds)
        {
            string path = AudioFolder + "/" + name + ".wav";
            if (File.Exists(path)) return;
            WriteWave(path, kind, seconds);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void WriteWave(string path, int kind, double seconds)
        {
            int samples = (int)(seconds * SampleRate);
            var random = new Random(17041 + kind);
            double filteredNoise = 0.0;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                writer.Write(36 + samples * 2);
                writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                writer.Write(samples * 2);
                for (int i = 0; i < samples; i++)
                {
                    double t = i / (double)SampleRate;
                    double phase = t / seconds;
                    double noise = random.NextDouble() * 2.0 - 1.0;
                    filteredNoise = filteredNoise * 0.82 + noise * 0.18;
                    double sample = Waveform(kind, t, phase, filteredNoise, noise);
                    double edge = Math.Min(1.0, Math.Min(t / 0.008, (seconds - t) / 0.015));
                    writer.Write((short)(Math.Max(-1.0, Math.Min(1.0, sample * edge * 0.7)) * short.MaxValue));
                }
            }
        }

        private static double Waveform(int kind, double t, double phase, double lowNoise, double noise)
        {
            const double tau = Math.PI * 2.0;
            switch (kind)
            {
                case 0: return (Math.Sin(tau * 85.0 * t) * 0.5 + lowNoise * 0.5) * Math.Sin(Math.PI * phase);
                case 1: return (Math.Sin(tau * (420.0 * t + 300.0 * t * t)) * 0.65 + noise * 0.08) * (1.0 - phase);
                case 2:
                    double chasePulse = Math.Pow(Math.Max(0.0, Math.Sin(tau * 5.0 * t)), 2.0);
                    return (Math.Sin(tau * 185.0 * t) * 0.5 + Math.Sin(tau * 277.5 * t) * 0.25 + lowNoise * 0.2) * chasePulse * (1.0 - phase * 0.5);
                case 3: return Math.Sin(tau * (520.0 * t - 130.0 * t * t)) * Math.Exp(-t * 3.0) * 0.7;
                case 4: return (Math.Sin(tau * (140.0 * t - 35.0 * t * t)) * 0.65 + lowNoise * 0.5) * Math.Exp(-t * 2.5);
                case 5: return (Math.Sin(tau * 110.0 * t) * 0.65 + lowNoise * 0.7) * Math.Exp(-t * 26.0);
                case 6:
                    int note = Math.Min(2, (int)(t / 0.4));
                    double frequency = note == 0 ? 440.0 : note == 1 ? 554.37 : 659.25;
                    return Math.Sin(tau * frequency * t) * Math.Exp(-(t % 0.4) * 6.0) * 0.65;
                case 7:
                    double beat = t % 1.0;
                    return (Math.Sin(tau * 220.0 * t) * 0.4 + Math.Sin(tau * 330.0 * t) * 0.25) * Math.Exp(-beat * 10.0);
                case 8:
                    double breath = Math.Pow(Math.Sin(Math.PI * phase), 2.0);
                    return (noise * 0.2 + lowNoise * 1.6) * breath;
                default:
                    double pulse = 0.25 + 0.75 * Math.Pow(Math.Sin(tau * t), 2.0);
                    return (Math.Sin(tau * 62.0 * t) * 0.55 + Math.Sin(tau * 93.0 * t) * 0.2) * pulse;
            }
        }
    }
}
