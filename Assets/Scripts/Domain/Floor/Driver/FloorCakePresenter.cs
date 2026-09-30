// ============================================================================
// FloorCakePresenter.cs
// ============================================================================
// PURPOSE:
//   Computes reproducible candle brightness and the placeholder trap tick waveform.
//   Explicit elapsed time keeps the sweep animated without reading an engine clock.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Return bounded flicker and mono samples without touching lights or audio objects.
// DEPENDENCIES:
//   System math only; no other system or engine calls.
// USAGE NOTES:
//   Stateless. The Driver supplies finite nonnegative time and configured tuning.
//   Tick timing is a tell and has no random jitter.
// ============================================================================
using System;

namespace Worsen.Domain.Floor
{
    public sealed class FloorCakePresenter
    {
        public float Flicker(float elapsed, float rate, float depth)
            => 1f + depth * (float)(0.65d * Math.Sin(elapsed * rate * 2d * Math.PI) +
                0.35d * Math.Sin(elapsed * rate * 2d * Math.PI * 1.7d));

        public float[] TickSamples(int sampleRate, float duration, float frequency)
        {
            var samples = new float[Math.Max(1, (int)(sampleRate * duration))];
            for (int i = 0; i < samples.Length; i++)
            {
                double phase = (double)i / samples.Length;
                samples[i] = (float)(Math.Sin(i * frequency * 2d * Math.PI / sampleRate) * Math.Sin(phase * Math.PI) * Math.Exp(-phase * 8d));
            }
            return samples;
        }
    }
}
