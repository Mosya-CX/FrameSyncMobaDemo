using System;
using System.IO;
using System.Text;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;

namespace FrameSyncMoba.Bootstrap
{
    internal static class MatchLaunchWireCodec
    {
        private const uint BootstrapAppliedMagic = 0x41504D46;
        private const uint LaunchCommitMagic = 0x4C434D46;
        private const ushort WireVersion = 2;
        private const int MaximumBytes = 4096;

        public static byte[] WriteBootstrapApplied(
            in BootstrapAppliedConfirmation confirmation)
        {
            confirmation.ValidateOrThrow();
            BootstrapAppliedConfirmation value = confirmation;
            return Write(
                BootstrapAppliedMagic,
                writer =>
                {
                    BootstrapPayloadWireCodec.WriteString(
                        writer,
                        value.MatchId);
                    writer.Write(value.StartTick);
                });
        }

        public static BootstrapAppliedConfirmation ReadBootstrapApplied(
            byte[] bytes)
        {
            return Read(
                bytes,
                BootstrapAppliedMagic,
                reader => new BootstrapAppliedConfirmation(
                    BootstrapPayloadWireCodec.ReadString(reader),
                    reader.ReadInt32()));
        }

        public static byte[] WriteLaunchCommit(
            in MatchLaunchCommit commit)
        {
            commit.ValidateOrThrow();
            MatchLaunchCommit value = commit;
            return Write(
                LaunchCommitMagic,
                writer =>
                {
                    BootstrapPayloadWireCodec.WriteString(
                        writer,
                        value.MatchId);
                    writer.Write(value.StartTick);
                    writer.Write(
                        value.LaunchServerTimeMilliseconds);
                });
        }

        public static MatchLaunchCommit ReadLaunchCommit(
            byte[] bytes)
        {
            return Read(
                bytes,
                LaunchCommitMagic,
                reader => new MatchLaunchCommit(
                    BootstrapPayloadWireCodec.ReadString(reader),
                    reader.ReadInt32(),
                    reader.ReadInt64()));
        }

        private static byte[] Write(
            uint magic,
            Action<BinaryWriter> writePayload)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(
                stream,
                Encoding.UTF8,
                true))
            {
                writer.Write(magic);
                writer.Write(WireVersion);
                writePayload(writer);
                writer.Flush();
                if (stream.Length > MaximumBytes)
                    throw new DeterministicSimulationException(
                        "Match launch message exceeds the wire limit.");
                return stream.ToArray();
            }
        }

        private static T Read<T>(
            byte[] bytes,
            uint expectedMagic,
            Func<BinaryReader, T> readPayload)
        {
            if (bytes == null ||
                bytes.Length == 0 ||
                bytes.Length > MaximumBytes)
                throw new DeterministicSimulationException(
                    "Match launch wire payload length is invalid.");
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(
                    stream,
                    Encoding.UTF8,
                    true))
                {
                    if (reader.ReadUInt32() != expectedMagic ||
                        reader.ReadUInt16() != WireVersion)
                        throw new DeterministicSimulationException(
                            "Match launch wire header is invalid.");
                    T value = readPayload(reader);
                    if (stream.Position != stream.Length)
                        throw new DeterministicSimulationException(
                            "Match launch wire payload contains trailing bytes.");
                    return value;
                }
            }
            catch (EndOfStreamException exception)
            {
                throw new DeterministicSimulationException(
                    "Match launch wire payload is truncated.",
                    exception);
            }
        }
    }
}
