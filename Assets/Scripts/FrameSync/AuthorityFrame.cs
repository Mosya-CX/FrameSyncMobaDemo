using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync
{
    [Flags]
    public enum AuthorityFrameFlags : byte
    {
        None = 0,
        MatchEndCandidate = 1 << 0,
    }

    /// <summary>
    /// Final authoritative input and deterministic output proof for one Tick.
    /// </summary>
    public readonly struct AuthorityFrame
    {
        public readonly int Tick;
        public readonly uint FrameSequence;
        public readonly uint FinalCommandRevision;
        public readonly AuthorityFrameFlags FrameFlags;
        public readonly uint SharedGameplayChecksum;
        private readonly byte[] canonicalCommandBytes;

        public byte[] CanonicalCommandBytes =>
            canonicalCommandBytes == null
                ? Array.Empty<byte>()
                : (byte[])canonicalCommandBytes.Clone();

        internal byte[] CanonicalCommandBytesUnsafe =>
            canonicalCommandBytes ?? Array.Empty<byte>();

        public AuthorityFrame(
            int tick,
            uint frameSequence,
            uint finalCommandRevision,
            byte[] canonicalCommandBytes,
            AuthorityFrameFlags frameFlags,
            uint sharedGameplayChecksum)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            Tick = tick;
            FrameSequence = frameSequence;
            FinalCommandRevision = finalCommandRevision;
            this.canonicalCommandBytes = canonicalCommandBytes == null
                ? throw new ArgumentNullException(nameof(canonicalCommandBytes))
                : (byte[])canonicalCommandBytes.Clone();
            FrameFlags = frameFlags;
            SharedGameplayChecksum = sharedGameplayChecksum;
        }

        public static AuthorityFrame Create(
            int tick,
            uint frameSequence,
            uint finalCommandRevision,
            IReadOnlyList<GameplayCommand> commands,
            AuthorityFrameFlags frameFlags,
            uint sharedGameplayChecksum)
        {
            return new AuthorityFrame(
                tick,
                frameSequence,
                finalCommandRevision,
                CanonicalCommandCodec.Encode(commands),
                frameFlags,
                sharedGameplayChecksum);
        }

        public GameplayCommand[] DecodeCommands() =>
            CanonicalCommandCodec.Decode(canonicalCommandBytes, Tick);
    }

    internal static class CanonicalCommandCodec
    {
        public static byte[] Encode(IReadOnlyList<GameplayCommand> commands)
        {
            var collector = new CommandCollector();
            collector.BeginTick(0);
            if (commands != null)
            {
                for (int i = 0; i < commands.Count; i++)
                    collector.Collect(commands[i]);
            }

            var writer = new CanonicalByteWriter(new byte[1024 * 1024]);
            collector.WriteCanonicalBytes(writer);
            ArraySegment<byte> segment = writer.GetWrittenSegment();
            var result = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, result, 0, segment.Count);
            return result;
        }

        public static GameplayCommand[] Decode(byte[] bytes, int expectedTick)
        {
            return DecodeCore(bytes, expectedTick, true);
        }

        public static GameplayCommand[] DecodeBundle(byte[] bytes)
        {
            return DecodeCore(bytes, 0, false);
        }

        private static GameplayCommand[] DecodeCore(
            byte[] bytes,
            int expectedTick,
            bool requireExpectedTick)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            var reader = new CanonicalReader(bytes);
            int count = reader.ReadInt32();
            if (count < 0 || count > 65536)
                throw new DeterministicSimulationException("Invalid canonical Command count.");

            var commands = new GameplayCommand[count];
            for (int i = 0; i < count; i++)
            {
                uint commandSeq = reader.ReadUInt32();
                ulong clientId = reader.ReadUInt64();
                int playerSlot = reader.ReadInt32();
                UnitUid controlledUnitUid = reader.ReadUnitUid();
                int targetTick = reader.ReadInt32();
                GameplayCommandKind kind = (GameplayCommandKind)reader.ReadByte();
                int buildLocalTick = reader.ReadInt32();
                int payloadByteLength = reader.ReadInt32();
                uint schemaVersion = reader.ReadUInt32();
                if (requireExpectedTick && targetTick != expectedTick)
                    throw new DeterministicSimulationException(
                        $"AuthorityFrame {expectedTick} contains Command for Tick {targetTick}.");

                var header = new CommandHeader(
                    commandSeq, clientId, playerSlot, controlledUnitUid,
                    targetTick, kind, buildLocalTick, payloadByteLength, schemaVersion);
                GameplayCommand command;
                switch (kind)
                {
                    case GameplayCommandKind.Move:
                        command = GameplayCommand.CreateMove(
                            header, new fp2(reader.ReadFp(), reader.ReadFp()));
                        break;
                    case GameplayCommandKind.Attack:
                        command = GameplayCommand.CreateAttack(header, reader.ReadUnitUid());
                        break;
                    case GameplayCommandKind.CastAbility:
                        byte slot = reader.ReadByte();
                        AbilitySignalVerb verb = (AbilitySignalVerb)reader.ReadByte();
                        command = GameplayCommand.CreateCastAbility(
                            header, slot, verb, reader.ReadAim());
                        break;
                    case GameplayCommandKind.CancelAbility:
                        command = GameplayCommand.CreateCancelAbility(
                            header, reader.ReadByte(), (AbilityCancelReason)reader.ReadByte());
                        break;
                    case GameplayCommandKind.AllocateAbilitySkillPoint:
                        command =
                            GameplayCommand.CreateAllocateAbilitySkillPoint(
                                header,
                                reader.ReadByte());
                        break;
                    case GameplayCommandKind.EquipmentShop:
                        EquipmentShopCommandOperationType operation =
                            (EquipmentShopCommandOperationType)reader.ReadByte();
                        if (operation ==
                            EquipmentShopCommandOperationType.Purchase)
                            command =
                                GameplayCommand.CreateEquipmentPurchase(
                                    header,
                                    reader.ReadInt32());
                        else if (operation ==
                                  EquipmentShopCommandOperationType.Sell)
                            command =
                                GameplayCommand.CreateEquipmentSell(
                                    header,
                                    reader.ReadByte());
                        else if (operation ==
                                  EquipmentShopCommandOperationType.Undo)
                            command =
                                GameplayCommand.CreateEquipmentUndo(
                                    header);
                        else
                            throw new DeterministicSimulationException(
                                $"Unsupported EquipmentShop operation {operation}.");
                        break;
                    case GameplayCommandKind.SwapEquipmentSlot:
                        command =
                            GameplayCommand.CreateSwapEquipmentSlot(
                                header,
                                reader.ReadByte(),
                                reader.ReadByte());
                        break;
                    case GameplayCommandKind.UseItem:
                        command = GameplayCommand.CreateUseItem(
                            header,
                            reader.ReadByte(),
                            reader.ReadAim());
                        break;
                    default:
                        throw new DeterministicSimulationException(
                            $"Unsupported authoritative Command kind {kind}.");
                }

                if (command.Header.PayloadByteLength != payloadByteLength)
                    throw new DeterministicSimulationException(
                        $"Command {commandSeq} has non-canonical payload length.");
                commands[i] = command;
            }

            reader.RequireEnd();
            byte[] encodedAgain = Encode(commands);
            if (!ByteArrayEquals(bytes, encodedAgain))
                throw new DeterministicSimulationException(
                    "AuthorityFrame Command bytes are not in canonical order or encoding.");
            return commands;
        }

        public static bool ByteArrayEquals(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i]) return false;
            return true;
        }

        private sealed class CanonicalReader
        {
            private readonly byte[] bytes;
            private int offset;

            public CanonicalReader(byte[] bytes) => this.bytes = bytes;
            public byte ReadByte() { Require(1); return bytes[offset++]; }
            public int ReadInt32() => unchecked((int)ReadUInt32());
            public uint ReadUInt32()
            {
                Require(4);
                uint value = (uint)(bytes[offset] | bytes[offset + 1] << 8 |
                    bytes[offset + 2] << 16 | bytes[offset + 3] << 24);
                offset += 4;
                return value;
            }
            public ulong ReadUInt64()
            {
                Require(8);
                ulong value = 0;
                for (int i = 0; i < 8; i++) value |= (ulong)bytes[offset + i] << (8 * i);
                offset += 8;
                return value;
            }
            public long ReadInt64() => unchecked((long)ReadUInt64());
            public fp ReadFp() => fp.FromRaw(ReadInt64());
            public UnitUid ReadUnitUid() => new UnitUid(ReadInt32(), ReadInt32(), ReadByte());
            public AimSnapshot ReadAim()
            {
                AimKind kind = (AimKind)ReadByte();
                UnitUid target = ReadUnitUid();
                fp2 point = new fp2(ReadFp(), ReadFp());
                fp2 direction = new fp2(ReadFp(), ReadFp());
                switch (kind)
                {
                    case AimKind.None: return AimSnapshot.None;
                    case AimKind.Self: return AimSnapshot.Self;
                    case AimKind.Point: return AimSnapshot.ForPoint(point);
                    case AimKind.Unit: return AimSnapshot.ForUnit(target);
                    case AimKind.Direction: return AimSnapshot.ForDirection(direction);
                    default:
                        throw new DeterministicSimulationException(
                            $"Unsupported AimKind {kind} in authoritative Command.");
                }
            }
            public void RequireEnd()
            {
                if (offset != bytes.Length)
                    throw new DeterministicSimulationException("Canonical Command bytes contain trailing data.");
            }
            private void Require(int count)
            {
                if (count < 0 || offset > bytes.Length - count)
                    throw new DeterministicSimulationException("Canonical Command bytes are truncated.");
            }
        }
    }
}
