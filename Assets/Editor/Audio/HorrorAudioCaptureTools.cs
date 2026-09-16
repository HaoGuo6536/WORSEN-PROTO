// ============================================================================
// HorrorAudioCaptureTools.cs
// ============================================================================
// PURPOSE:
//   Renders at most twenty seconds of the game's main Unity mix into an Editor-only
//   buffer, then exports float WAV and signal measurements without OS/microphone capture.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Audio · explicit manual-render capture workflow.
// KEY RESPONSIBILITIES:
//   - Render once per new game frame using a temporary fixed capture step, restoring its exact prior rational value.
//   - Always release owned AudioRenderer mode on stop, failure, Play exit or reload.
//   - Export only successful rendered samples; preserve failures without a false WAV.
// DEPENDENCIES:
//   - UnityEditor lifecycle; public Unity AudioRenderer and NativeArray; System WAV I/O.
// USAGE NOTES:
//   Coordinator invokes Start/Status/Stop on the main thread under the Unity lease.
//   AudioRenderer intentionally silences hardware while active; Stop restores output.
//   This is fixed-step manual-render game-mix evidence, not real-time device-loopback audio.
//   No runtime component is added. Fixed buffers are capped; files stay under Logs.
// ============================================================================
using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Worsen.Editor.Audio
{
    [InitializeOnLoad]
    public static class HorrorAudioCaptureTools
    {
        private static HorrorAudioCaptureBuffer _buffer;
        private static NativeArray<float> _native;
        private static float[] _scratch;
        private static bool _rendererOwned;
        private static bool _captureTimeOwned;
        private static Unity.IntegerTime.RationalTime _priorCaptureStep;
        private static int _lastFrame;
        private static double _startedWall;
        private static CaptureReport _report;
        private static string _lastResult = "{\"status\":\"idle\"}";
        static HorrorAudioCaptureTools()
        {
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        }
        [MenuItem("Worsen/Audio/Start Manual Game Mix Capture (20 seconds)")]
        private static void StartMenu() => Debug.Log(Start());
        [MenuItem("Worsen/Audio/Stop Game Mix Capture")]
        private static void StopMenu() => Debug.Log(Stop());
        public static string Start(float seconds = 20f)
        {
            if (!EditorApplication.isPlaying) return "{\"status\":\"not_in_play_mode\"}";
            if (_buffer != null) return Status();
            if (_rendererOwned || _captureTimeOwned) { ReleaseRenderer(); if (_rendererOwned || _captureTimeOwned) return _lastResult; }
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return "{\"status\":\"invalid_duration\"}";
            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                .Where(listener => listener.isActiveAndEnabled).ToArray();
            if (listeners.Length != 1) return "{\"status\":\"requires_exactly_one_active_listener\",\"count\":" + listeners.Length + "}";
            int rate = AudioSettings.outputSampleRate, channels = Channels(AudioSettings.speakerMode);
            if (rate < 8000 || rate > 192000 || channels == 0) return "{\"status\":\"unsupported_audio_configuration\"}";
            float duration = Mathf.Min(seconds, 20f);
            _report = new CaptureReport
            {
                status = "starting", failure = "", startedUtc = DateTime.UtcNow.ToString("O"), sampleRate = rate, channels = channels,
                requestedSeconds = duration, listener = listeners[0].name, scene = listeners[0].gameObject.scene.path,
                unityVersion = Application.unityVersion, speakerMode = AudioSettings.speakerMode.ToString(),
                tapLocation = "AudioRenderer main Unity output", captureMode = "offline manual-render mix at 60 simulated frames/sec; hardware silent while recording; not device capture",
                encoding = "IEEE float32 WAV; no normalization; nonfinite samples exported as zero",
                scope = "Unity main game mix only; no OS, microphone or other application capture",
                startGameFrame = Time.frameCount, lastGameFrame = Time.frameCount
            };
            try
            {
                _buffer = new HorrorAudioCaptureBuffer(Math.Max(1, (int)(rate * duration)));
                _native = new NativeArray<float>(checked(rate * channels * 2), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                _scratch = new float[_native.Length];
                if (!AudioRenderer.Start())
                {
                    _report.failure = "AudioRenderer.Start returned false: another renderer may own recording mode";
                    _buffer.Freeze("start_rejected"); DisposeBuffers(); _report.status = "failed_start";
                    return _lastResult = JsonUtility.ToJson(_report);
                }
                _rendererOwned = true;
                _priorCaptureStep = Time.captureDeltaTimeRational; _report.previousCaptureDeltaTime = Time.captureDeltaTime;
                _captureTimeOwned = true; Time.captureDeltaTime = 1f / 60f; _report.captureDeltaTime = Time.captureDeltaTime;
                _lastFrame = Time.frameCount; _startedWall = EditorApplication.timeSinceStartup;
                EditorApplication.update += CaptureFrame;
                AudioSettings.OnAudioConfigurationChanged += AudioChanged;
                return Status();
            }
            catch (Exception error)
            {
                _report.failure = "Start: " + error.Message;
                _buffer?.Freeze("start_failed"); ReleaseRenderer(); DisposeBuffers(); _report.status = "failed_start";
                return _lastResult = JsonUtility.ToJson(_report);
            }
        }
        public static string Status()
        {
            if (_buffer == null) return _lastResult;
            UpdateReport(_buffer.Snapshot()); return JsonUtility.ToJson(_report);
        }
        private static void CaptureFrame()
        {
            if (_buffer == null || !_rendererOwned) return;
            try
            {
                if (!EditorApplication.isPlaying) { _buffer.Freeze("play_ended"); ReleaseRenderer(); return; }
                if (EditorApplication.timeSinceStartup - _startedWall > _report.requestedSeconds + 5f)
                { _buffer.Freeze("wall_timeout"); ReleaseRenderer(); return; }
                int frame = Time.frameCount; if (frame == _lastFrame) return;
                _report.skippedGameFrames += Math.Max(0, frame - _lastFrame - 1);
                _lastFrame = frame; _report.lastGameFrame = frame;
                // Unity Recorder's own AudioInput multiplies this sample-FRAME count by channel count.
                int frames = AudioRenderer.GetSampleCountForCaptureFrame();
                if (frames == 0) _report.zeroSampleFrames++;
                int samples = checked(frames * _report.channels);
                if (frames < 0 || samples > _native.Length) throw new InvalidOperationException("Render block exceeds fixed two-second scratch capacity.");
                NativeArray<float> block = _native.GetSubArray(0, samples);
                _report.renderCalls++;
                if (!AudioRenderer.Render(block)) throw new InvalidOperationException("AudioRenderer.Render returned false.");
                if (samples > 0)
                { NativeArray<float>.Copy(block, _scratch, samples); _buffer.Append(_scratch, _report.channels, samples); }
                if (!_buffer.Snapshot().Active) ReleaseRenderer();
            }
            catch (Exception error)
            {
                _report.failure = "Render: " + error.Message; _buffer?.Freeze("render_failed"); ReleaseRenderer();
            }
        }
        public static string Stop()
        {
            if (_buffer == null) { ReleaseRenderer(); return _lastResult; }
            _buffer.Freeze("explicit_stop"); HorrorAudioCaptureSnapshot snapshot = _buffer.Snapshot();
            ReleaseRenderer(); UpdateReport(snapshot); _report.stoppedUtc = DateTime.UtcNow.ToString("O");
            _report.sampleRateChanged |= AudioSettings.outputSampleRate != _report.sampleRate;
            _report.completeRequestedDuration = snapshot.Frames >= Math.Max(1, (int)(_report.sampleRate * _report.requestedSeconds));
            try
            {
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/HorrorExpansion/audio/captures"));
                Directory.CreateDirectory(folder);
                string stem = "manual-game-mix-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                _report.metadataPath = Path.Combine(folder, stem + ".json");
                if (!string.IsNullOrEmpty(_report.failure) || _report.sampleRateChanged) _report.status = "failed_no_wav_export";
                else if (snapshot.Frames > 0 && snapshot.Channels > 0)
                {
                    _report.wavPath = Path.Combine(folder, stem + ".wav"); WriteWave(_report.wavPath, snapshot, _report);
                    _report.status = !_report.completeRequestedDuration ? "manual_render_incomplete_not_acceptance" :
                        (_report.nonFiniteSamples > 0 ? "manual_render_captured_nonfinite_samples" : "manual_render_captured");
                }
                else _report.status = "no_rendered_frames";
                _lastResult = JsonUtility.ToJson(_report, true); File.WriteAllText(_report.metadataPath, _lastResult); return _lastResult;
            }
            catch (Exception error)
            {
                _report.failure = "Export: " + error.Message; _report.status = "failed_export";
                return _lastResult = JsonUtility.ToJson(_report);
            }
            finally { ReleaseRenderer(); DisposeBuffers(); }
        }
        private static void UpdateReport(HorrorAudioCaptureSnapshot snapshot)
        {
            _report.frames = snapshot.Frames; _report.copiedBlocks = snapshot.Callbacks;
            _report.durationSeconds = (double)snapshot.Frames / _report.sampleRate; _report.stopReason = snapshot.Reason;
            _report.status = !string.IsNullOrEmpty(_report.failure) ? "failed_call_Stop_for_diagnostics" :
                (snapshot.Active ? "manual_render_recording" : "buffer_stopped_call_Stop_to_export");
        }
        private static void ReleaseRenderer()
        {
            EditorApplication.update -= CaptureFrame; AudioSettings.OnAudioConfigurationChanged -= AudioChanged;
            if (!_rendererOwned) { RestoreCaptureTime(); return; }
            bool stopped = false;
            try { _report.audioRendererStopReturned = AudioRenderer.Stop(); _report.hardwareOutputRestored = true; stopped = true; }
            catch (Exception error) { _report.failure += " Stop: " + error.Message; _report.hardwareOutputRestored = false; }
            finally { _rendererOwned = !stopped; RestoreCaptureTime(); }
        }
        private static void RestoreCaptureTime()
        {
            if (!_captureTimeOwned) return;
            try
            {
                Time.captureDeltaTimeRational = _priorCaptureStep;
                _report.captureTimeRestored = Time.captureDeltaTimeRational.Equals(_priorCaptureStep);
                _captureTimeOwned = !_report.captureTimeRestored;
                if (!_report.captureTimeRestored) _report.failure += " Capture timing did not restore exactly.";
            }
            catch (Exception error) { _report.failure += " Restore capture timing: " + error.Message; }
        }
        private static void DisposeBuffers()
        { if (_native.IsCreated) _native.Dispose(); _scratch = null; _buffer = null; }
        private static int Channels(AudioSpeakerMode mode)
        {
            switch (mode)
            {
                case AudioSpeakerMode.Mono: return 1;
                case AudioSpeakerMode.Stereo: case AudioSpeakerMode.Prologic: return 2;
                case AudioSpeakerMode.Quad: return 4;
                case AudioSpeakerMode.Surround: return 5;
                case AudioSpeakerMode.Mode5point1: return 6;
                case AudioSpeakerMode.Mode7point1: return 8;
                default: return 0;
            }
        }
        private static void WriteWave(string path, HorrorAudioCaptureSnapshot snapshot, CaptureReport report)
        {
            int sampleCount = checked(snapshot.Frames * snapshot.Channels), bytes = checked(sampleCount * 4);
            double sumSquares = 0; report.channelRms = new double[snapshot.Channels]; report.channelPeak = new float[snapshot.Channels];
            using (var writer = new BinaryWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(48 + bytes); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((ushort)3); writer.Write((ushort)snapshot.Channels); writer.Write(report.sampleRate);
                writer.Write(report.sampleRate * snapshot.Channels * 4); writer.Write((ushort)(snapshot.Channels * 4)); writer.Write((ushort)32);
                writer.Write(Encoding.ASCII.GetBytes("fact")); writer.Write(4); writer.Write(snapshot.Frames);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
                for (int i = 0; i < sampleCount; i++)
                {
                    float sample = snapshot.Samples[i];
                    if (float.IsNaN(sample) || float.IsInfinity(sample)) { report.nonFiniteSamples++; sample = 0f; }
                    float absolute = Math.Abs(sample); int channel = i % snapshot.Channels;
                    double square = (double)sample * sample; sumSquares += square; report.channelRms[channel] += square;
                    report.peak = Math.Max(report.peak, absolute); report.channelPeak[channel] = Math.Max(report.channelPeak[channel], absolute);
                    if (absolute >= 1f) report.samplesAtOrAboveFullScale++;
                    writer.Write(sample);
                }
            }
            report.rms = Math.Sqrt(sumSquares / sampleCount);
            for (int i = 0; i < snapshot.Channels; i++) report.channelRms[i] = Math.Sqrt(report.channelRms[i] / snapshot.Frames);
            report.sampleCount = sampleCount; report.fullScaleFraction = (double)report.samplesAtOrAboveFullScale / sampleCount;
            report.silent = report.peak == 0f;
        }
        private static void AudioChanged(bool changed)
        { if (_report != null) _report.sampleRateChanged = true; _buffer?.Freeze("audio_configuration_changed"); ReleaseRenderer(); }
        private static void PlayModeChanged(PlayModeStateChange change)
        { if (change == PlayModeStateChange.ExitingPlayMode) Stop(); }
        private static void BeforeReload() { if (_buffer != null) Stop(); else ReleaseRenderer(); }
        [Serializable]
        private sealed class CaptureReport
        {
            public string failure, speakerMode, captureMode, status, startedUtc, stoppedUtc, listener, scene, unityVersion, tapLocation, encoding, scope, stopReason, wavPath, metadataPath;
            public int startGameFrame, lastGameFrame, skippedGameFrames, renderCalls, zeroSampleFrames, sampleRate, channels, frames, copiedBlocks, sampleCount, samplesAtOrAboveFullScale, nonFiniteSamples;
            public float previousCaptureDeltaTime, captureDeltaTime, requestedSeconds, peak;
            public double durationSeconds, rms, fullScaleFraction;
            public double[] channelRms;
            public float[] channelPeak;
            public bool completeRequestedDuration, captureTimeRestored, silent, sampleRateChanged, audioRendererStopReturned, hardwareOutputRestored;
        }
    }
}
