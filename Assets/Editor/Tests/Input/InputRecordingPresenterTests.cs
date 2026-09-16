// ============================================================================
// InputRecordingPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies recording byte fidelity and failure handling. It exercises every probe field and refuses files whose schema, extent or completion marker cannot support replay.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Input.
// KEY RESPONSIBILITIES:
//   - Round-trip exact Core values and reject corrupt/incomplete files.
// DEPENDENCIES:
//   - NUnit, Core data, InputRecordingPresenter and InputReplayPresenter.
// USAGE NOTES:
//   - Editor-only pure memory tests; source/config provenance is retained exactly.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Input;

namespace Worsen.Tests.Input
{
    public sealed class InputRecordingPresenterTests
    {
        private InputReplayDriverState Capture()
        {
            var state = new InputReplayDriverState(); var replay = new InputReplayPresenter();
            replay.BeginRecording(state, new RunCaptureMetadata("test,\"record", 123, 0.125f, "rev", "config", "Player>Floor>Director", 0));
            var input = new InputFrame(new Vector2(0.33333334f, -0.2f), new Vector2(1.125f, -180f),
                InputButtons.Sprint | InputButtons.LookBack, InputButtons.Jump, InputButtons.Jump);
            var probe = new MovementProbe(true, Vector3.up, true, 1.125f, Vector3.left, 45.25f, 27, true,
                1.75f, 2.875f, new Vector3(1.125f, 2.375f, -3.75f), true);
            var resolution = new MovementResolution(new Vector3(1.25f, 7.125f, -3.5f),
                new Vector3(-2.25f, 0.125f, 8.75f), true, false, new Vector3(1.25f, 8.725f, -3.5f));
            replay.Record(state, new InputProbeRecord(1, 1, input, probe, 0.125f, resolution));
            replay.FinishRecording(state, 1, true);
            return state;
        }
        [Test]
        public void RoundTripPreservesProvenanceAndEveryInputProbeField()
        {
            var state = Capture(); var format = new InputRecordingPresenter();
            Assert.That(format.TryDecode(format.Encode(state), out var metadata, out var records, out string error), Is.True, error);
            Assert.That(metadata.SessionId, Is.EqualTo(state.Metadata.SessionId));
            Assert.That(metadata.Seed, Is.EqualTo(123)); Assert.That(metadata.FixedDeltaTime, Is.EqualTo(0.125f));
            Assert.That(metadata.SourceRevision, Is.EqualTo("rev")); Assert.That(metadata.ConfigSnapshotHash, Is.EqualTo("config"));
            Assert.That(metadata.RandomConsumptionOrder, Is.EqualTo("Player>Floor>Director"));
            var expected = state.Recorded[0]; var actual = records[0];
            Assert.That(actual.Tick, Is.EqualTo(expected.Tick)); Assert.That(actual.DeltaTime, Is.EqualTo(expected.DeltaTime));
            Assert.That(actual.Input, Is.EqualTo(expected.Input)); Assert.That(actual.Probe, Is.EqualTo(expected.Probe));
            Assert.That(actual.Probe.StandingBlocked, Is.True);
            Assert.That(actual.Resolution, Is.EqualTo(expected.Resolution));
            Assert.That(actual.Resolution.Present, Is.True);
        }
        [Test]
        public void TruncatedTrailingWrongSchemaAndIncompleteFilesAreRejected()
        {
            var state = Capture(); var format = new InputRecordingPresenter(); byte[] bytes = format.Encode(state);
            Array.Resize(ref bytes, bytes.Length - 1);
            Assert.That(format.TryDecode(bytes, out _, out _, out _), Is.False);
            bytes = format.Encode(state); Array.Resize(ref bytes, bytes.Length + 1);
            Assert.That(format.TryDecode(bytes, out _, out _, out _), Is.False);
            bytes = format.Encode(state); bytes[4] = 99;
            Assert.That(format.TryDecode(bytes, out _, out _, out _), Is.False);
            state.CaptureComplete = false;
            Assert.That(format.TryDecode(format.Encode(state), out _, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("incomplete"));
        }
    }
}
