// ============================================================================
// AudioChaseMusicDriverState.cs
// ============================================================================
// PURPOSE:
//   Stores the current adaptive music mix and one chase sequence transport.
//   Keeping contact memory and loss-episode draws here prevents additional hunters
//   or repeated ticks from restarting the intro or rerolling the release.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Retain mix envelopes, belief contact, loss timing and DSP scheduling commands.
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
        public bool HasContact;
        public bool LossActive;
        public bool EarlyDangerFade;
        public float ReleaseDelaySeconds;
        public float ReleaseRemaining;
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
