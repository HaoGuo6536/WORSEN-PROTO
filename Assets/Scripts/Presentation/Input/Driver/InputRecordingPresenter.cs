// ============================================================================
// InputRecordingPresenter.cs
// ============================================================================
// PURPOSE:
//   Encodes recordings as versioned binary data with exact floating-point values. It rejects truncated, unsupported or incomplete captures so playback cannot quietly substitute missing probes.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Input.
// KEY RESPONSIBILITIES:
//   - Serialize provenance and complete input/probe payloads; validate format boundaries.
// DEPENDENCIES:
//   - Core recording values and System memory streams only.
// USAGE NOTES:
//   - No file operations. Binary schema 1 includes StandingBlocked; float values are written without decimal rounding.
// ============================================================================
using System;
using System.IO;
using System.Text;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Input
{
    public sealed class InputRecordingPresenter
    {
        private const int Magic = 0x57525031;
        private const int MaximumRecords = 10000000;

        public byte[] Encode(InputReplayDriverState state)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(1);
                var m = state.Metadata;
                writer.Write(m.SessionId ?? ""); writer.Write(m.Seed); writer.Write(m.FixedDeltaTime);
                writer.Write(m.SourceRevision ?? ""); writer.Write(m.ConfigSnapshotHash ?? "");
                writer.Write(m.RandomConsumptionOrder ?? ""); writer.Write(m.StartTick);
                writer.Write(state.EndTick); writer.Write(state.CaptureComplete);
                writer.Write(state.Recorded.Count);
                foreach (var record in state.Recorded)
                    WriteRecord(writer, record);
                writer.Flush();
                return memory.ToArray();
            }
        }

        public bool TryDecode(byte[] bytes, out RunCaptureMetadata metadata,
            out InputProbeRecord[] records, out string error)
        {
            metadata = default; records = new InputProbeRecord[0]; error = "";
            try
            {
                if (bytes == null) throw new InvalidDataException("Recording data is absent.");
                using (var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8))
                {
                    if (reader.ReadInt32() != Magic || reader.ReadInt32() != 1)
                        throw new InvalidDataException("Unsupported recording format/schema.");
                    metadata = new RunCaptureMetadata(reader.ReadString(), reader.ReadInt32(), reader.ReadSingle(),
                        reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadInt64());
                    long endTick = reader.ReadInt64();
                    if (!reader.ReadBoolean()) throw new InvalidDataException("Capture is incomplete; retain it as evidence but do not play it back.");
                    int count = reader.ReadInt32();
                    if (count < 1 || count > MaximumRecords || count > bytes.Length / 100)
                        throw new InvalidDataException("Invalid recording length.");
                    records = new InputProbeRecord[count];
                    for (int i = 0; i < count; i++) records[i] = ReadRecord(reader);
                    if (reader.BaseStream.Position != reader.BaseStream.Length || records[count - 1].Tick != endTick)
                        throw new InvalidDataException("Recording has trailing data or an inconsistent end tick.");
                    var check = new InputReplayDriverState();
                    if (!new InputReplayPresenter().StartPlayback(check, metadata, records, true))
                        throw new InvalidDataException(check.Error);
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException ||
                exception is ArgumentException || exception is OverflowException)
            {
                error = exception.Message;
                metadata = default;
                records = new InputProbeRecord[0];
                return false;
            }
        }

        private static void WriteRecord(BinaryWriter writer, InputProbeRecord record)
        {
            writer.Write(record.SchemaVersion); writer.Write(record.Tick); writer.Write(record.DeltaTime);
            var f = record.Input;
            writer.Write(f.Move.x); writer.Write(f.Move.y); writer.Write(f.LookDelta.x); writer.Write(f.LookDelta.y);
            writer.Write((int)f.Held); writer.Write((int)f.Pressed); writer.Write((int)f.Released);
            var p = record.Probe;
            writer.Write(p.Grounded); WriteVector(writer, p.GroundNormal);
            writer.Write(p.WallDetected); writer.Write(p.WallDistance); WriteVector(writer, p.WallNormal);
            writer.Write(p.WallAngleDegrees); writer.Write(p.WallId); writer.Write(p.VaultCandidate);
            writer.Write(p.VaultHeight); writer.Write(p.VaultClearance); WriteVector(writer, p.VaultTarget);
            writer.Write(p.StandingBlocked);
            var resolution = record.Resolution;
            writer.Write(resolution.Present);
            WriteVector(writer, resolution.Position); WriteVector(writer, resolution.Velocity);
            writer.Write(resolution.Grounded); writer.Write(resolution.Ceiling); WriteVector(writer, resolution.EyePosition);
        }

        private static InputProbeRecord ReadRecord(BinaryReader reader)
        {
            int schema = reader.ReadInt32(); long tick = reader.ReadInt64(); float dt = reader.ReadSingle();
            var frame = new InputFrame(new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                new Vector2(reader.ReadSingle(), reader.ReadSingle()), (InputButtons)reader.ReadInt32(),
                (InputButtons)reader.ReadInt32(), (InputButtons)reader.ReadInt32());
            var probe = new MovementProbe(reader.ReadBoolean(), ReadVector(reader), reader.ReadBoolean(),
                reader.ReadSingle(), ReadVector(reader), reader.ReadSingle(), reader.ReadInt32(), reader.ReadBoolean(),
                reader.ReadSingle(), reader.ReadSingle(), ReadVector(reader), reader.ReadBoolean());
            bool present = reader.ReadBoolean();
            var position = ReadVector(reader); var velocity = ReadVector(reader);
            bool grounded = reader.ReadBoolean(); bool ceiling = reader.ReadBoolean(); var eye = ReadVector(reader);
            var resolution = present ? new MovementResolution(position, velocity, grounded, ceiling, eye) : default;
            return new InputProbeRecord(schema, tick, frame, probe, dt, resolution);
        }

        private static void WriteVector(BinaryWriter writer, Vector3 v)
        {
            writer.Write(v.x); writer.Write(v.y); writer.Write(v.z);
        }
        private static Vector3 ReadVector(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
}
