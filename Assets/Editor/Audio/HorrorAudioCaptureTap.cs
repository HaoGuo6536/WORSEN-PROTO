// ============================================================================
// HorrorAudioCaptureTap.cs
// ============================================================================
// PURPOSE:
//   Holds bounded interleaved samples for Editor-only main-mix rendering and export.
//   The former Editor MonoBehaviour tap was rejected by Unity and has been removed.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Audio · pure capture buffer and immutable snapshot values.
// KEY RESPONSIBILITIES:
//   - Copy complete frames without changing input; bound memory and capture duration.
//   - Freeze safely before main-thread WAV export; reject channel format changes.
// DEPENDENCIES:
//   - System array copying and synchronization only; no runtime component or Unity API.
// USAGE NOTES:
//   Owned by HorrorAudioCaptureTools. Filename retained to preserve its existing meta.
//   AudioRenderer supplies data explicitly; there is no attachable capture component.
// ============================================================================
using System;
namespace Worsen.Editor.Audio
{
    internal sealed class HorrorAudioCaptureBuffer
    {
        private readonly object _gate = new object();
        private readonly float[] _samples;
        private readonly int _frameLimit;
        private bool _active = true;
        private int _frames, _channels, _callbacks;
        private string _reason = "recording";
        internal HorrorAudioCaptureBuffer(int frameLimit)
        { _frameLimit = frameLimit; _samples = new float[checked(frameLimit * 8)]; }
        internal void Append(float[] data, int channels, int sampleCount = -1)
        {
            lock (_gate)
            {
                if (!_active) return;
                _callbacks++;
                int count = sampleCount < 0 && data != null ? data.Length : sampleCount;
                if (data == null || count < 0 || count > data.Length || channels < 1 || channels > 8 || count % channels != 0)
                { _active = false; _reason = "unsupported_channel_block"; return; }
                if (_channels == 0) _channels = channels;
                if (channels != _channels) { _active = false; _reason = "channel_format_changed"; return; }
                int frames = Math.Min(count / channels, _frameLimit - _frames);
                Array.Copy(data, 0, _samples, _frames * channels, frames * channels);
                _frames += frames;
                if (_frames == _frameLimit) { _active = false; _reason = "capacity_reached"; }
            }
        }
        internal void Freeze(string reason)
        { lock (_gate) { if (_active) { _active = false; _reason = reason; } } }
        internal HorrorAudioCaptureSnapshot Snapshot()
        {
            lock (_gate) return new HorrorAudioCaptureSnapshot
            { Samples = _samples, Frames = _frames, Channels = _channels, Callbacks = _callbacks, Active = _active, Reason = _reason };
        }
    }
    internal struct HorrorAudioCaptureSnapshot
    {
        internal float[] Samples;
        internal int Frames, Channels, Callbacks;
        internal bool Active;
        internal string Reason;
    }
}
