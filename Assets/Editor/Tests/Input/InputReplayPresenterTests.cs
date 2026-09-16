// ============================================================================
// InputReplayPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies single-source playback and recording validity from plain data. It protects short taps, exact angular deltas, focus/readiness boundaries and end-of-stream behavior without claiming physical device coverage.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Input.
// KEY RESPONSIBILITIES:
//   - Exercise source isolation, exact frames, rejected records and interrupted gates.
// DEPENDENCIES:
//   - NUnit, Core values, InputReplayPresenter and InputDriverState.
// USAGE NOTES:
//   - Editor-only pure data tests. Live InputFramePresenter regression suites remain unchanged.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Input;

namespace Worsen.Tests.Input
{
    public sealed class InputReplayPresenterTests
    {
        private InputReplayPresenter _presenter;
        private InputReplayDriverState _state;
        private RunCaptureMetadata _metadata;
        [SetUp]
        public void SetUp()
        {
            _presenter = new InputReplayPresenter(); _state = new InputReplayDriverState();
            _metadata = new RunCaptureMetadata("fixture", 42, 0.1f, "source-sha", "config-sha", "Player>Floor>Director", 0);
        }
        [Test]
        public void PreservesTapMasksAndAngularValuesExactlyThenRestoresLive()
        {
            var frame = new InputFrame(new Vector2(0.7f, -0.3f), new Vector2(19.125f, -7.625f),
                InputButtons.Sprint, InputButtons.Jump, InputButtons.Jump);
            Assert.That(_presenter.StartPlayback(_state, _metadata, new[] { Record(1, frame), Record(2, default) }, true), Is.True);
            Assert.That(_state.Source, Is.EqualTo(InputSource.Playback));
            Assert.That(_presenter.TryReadPlayback(_state, true, out var first), Is.True);
            Assert.That(first.Move, Is.EqualTo(frame.Move)); Assert.That(first.LookDelta, Is.EqualTo(frame.LookDelta));
            Assert.That(first.Held, Is.EqualTo(frame.Held)); Assert.That(first.Pressed, Is.EqualTo(frame.Pressed));
            Assert.That(first.Released, Is.EqualTo(frame.Released));
            Assert.That(_presenter.TryReadPlayback(_state, true, out var second), Is.True);
            Assert.That(second.Pressed, Is.EqualTo(InputButtons.None)); Assert.That(_state.Source, Is.EqualTo(InputSource.Live));
            Assert.That(_presenter.TryReadPlayback(_state, true, out _), Is.False);
        }
        [Test]
        public void PlaybackCopiesSourceAndRefusesLiveRecordMixing()
        {
            var records = new[] { Record(1, new InputFrame(Vector2.up, default, 0, 0, 0)) };
            Assert.That(_presenter.StartPlayback(_state, _metadata, records, true), Is.True);
            records[0] = Record(1, default);
            Assert.That(_presenter.Record(_state, Record(2, default)), Is.False);
            _presenter.TryReadPlayback(_state, true, out var frame);
            Assert.That(frame.Move, Is.EqualTo(Vector2.up));
        }
        [Test]
        public void ClosedGateAbortsPlaybackWithoutLeakingNextEdge()
        {
            Assert.That(_presenter.StartPlayback(_state, _metadata, new[] { Record(1, default), Record(2, default) }, true), Is.True);
            Assert.That(_presenter.TryReadPlayback(_state, false, out _), Is.False);
            Assert.That(_state.Source, Is.EqualTo(InputSource.Live));
            Assert.That(_presenter.TryReadPlayback(_state, true, out _), Is.False);
            Assert.That(_presenter.StartPlayback(_state, _metadata, new[] { Record(1, default) }, false), Is.False);
        }
        [Test]
        public void BadSchemaGapOrTimestepRejectWholePlaybackBeforeConsumption()
        {
            Assert.That(_presenter.StartPlayback(_state, _metadata, new[] { new InputProbeRecord(9, 1, default, default, 0.1f) }, true), Is.False);
            Assert.That(_presenter.StartPlayback(_state, _metadata, new[] { Record(2, default) }, true), Is.False);
            Assert.That(_presenter.StartPlayback(_state, _metadata, new[] { new InputProbeRecord(1, 1, default, default, 0.2f) }, true), Is.False);
            Assert.That(_state.Source, Is.EqualTo(InputSource.Live));
        }
        [Test]
        public void RecordingGapIsVisibleAndCannotBecomeComplete()
        {
            _presenter.BeginRecording(_state, _metadata);
            Assert.That(_presenter.Record(_state, Record(1, default)), Is.True);
            Assert.That(_presenter.Record(_state, Record(3, default)), Is.False);
            Assert.That(_presenter.FinishRecording(_state, 1, true), Is.False);
            Assert.That(_state.Error, Is.Not.Empty);
        }
        [Test]
        public void SwitchingToPlaybackCannotRelabelOrCompleteInterruptedCapture()
        {
            _presenter.BeginRecording(_state, _metadata);
            _presenter.Record(_state, Record(1, default));
            var playbackMetadata = new RunCaptureMetadata("other", 99, 0.1f, "other-source", "other-config", "other-order", 0);
            Assert.That(_presenter.StartPlayback(_state, playbackMetadata, new[] { Record(1, default) }, true), Is.True);
            Assert.That(_state.Metadata.SessionId, Is.EqualTo("fixture"));
            Assert.That(_presenter.FinishRecording(_state, 1, true), Is.False);
        }
        [Test]
        public void RestartedCaptureClearsPreviousFramesAndPlayback()
        {
            _presenter.BeginRecording(_state, _metadata); _presenter.Record(_state, Record(1, default));
            _presenter.BeginRecording(_state, _metadata);
            Assert.That(_state.Recorded, Is.Empty); Assert.That(_state.Source, Is.EqualTo(InputSource.Live));
        }
        private static InputProbeRecord Record(long tick, InputFrame frame) => new InputProbeRecord(1, tick, frame, default, 0.1f);
    }
}
