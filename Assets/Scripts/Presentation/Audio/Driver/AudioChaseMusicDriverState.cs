// ============================================================================
// AudioChaseMusicDriverState.cs
// ============================================================================
// PURPOSE:
//   Stores the current adaptive music mix and one chase sequence transport.
//   Keeping transition edges and scheduling times here prevents additional hunters
//   from restarting the intro or producing multiple chase endings.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Retain mix envelopes, confirmed pursuit and DSP scheduling commands.
// DEPENDENCIES:
//   - Own Audio presentation stack only.
// USAGE NOTES:
//   Owned by AudioSoundscapeDriver; replaced on reset, disable and teardown.
// ============================================================================
namespace Worsen.Presentation.Audio
{
    public sealed class AudioChaseMusicDriverState
    {
        public bool Chasing;
        public bool StartRun;
        public bool EndRun;
        public bool StopRun;
        public float TensionGain;
        public float StressGain;
        public float DangerGain;
        public float ImpactPitch = 1f;
        public double IntroStart;
        public double LoopStart;
        public double EndingStart;
    }
}
