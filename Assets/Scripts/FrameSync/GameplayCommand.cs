using System;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync
{
    public readonly struct CommandHeader
    {
        public const uint CurrentSchemaVersion = 1;

        public readonly uint CommandSeq;
        public readonly ulong ClientId;
        public readonly int PlayerSlot;
        public readonly UnitUid ControlledUnitUid;
        public readonly int TargetTick;
        public readonly GameplayCommandKind CommandKind;
        public readonly int BuildLocalTick;
        public readonly int PayloadByteLength;
        public readonly uint SchemaVersion;

        public CommandHeader(
            uint commandSeq,
            ulong clientId,
            int playerSlot,
            UnitUid controlledUnitUid,
            int targetTick,
            GameplayCommandKind commandKind,
            int buildLocalTick,
            int payloadByteLength,
            uint schemaVersion = CurrentSchemaVersion)
        {
            CommandSeq = commandSeq;
            ClientId = clientId;
            PlayerSlot = playerSlot;
            ControlledUnitUid = controlledUnitUid;
            TargetTick = targetTick;
            CommandKind = commandKind;
            BuildLocalTick = buildLocalTick;
            PayloadByteLength = payloadByteLength;
            SchemaVersion = schemaVersion;
        }

        internal CommandHeader WithPayload(
            GameplayCommandKind commandKind,
            int payloadByteLength)
        {
            return new CommandHeader(
                CommandSeq,
                ClientId,
                PlayerSlot,
                ControlledUnitUid,
                TargetTick,
                commandKind,
                BuildLocalTick,
                payloadByteLength,
                SchemaVersion);
        }

        internal void WriteCanonicalBytes(CanonicalByteWriter writer)
        {
            writer.WriteUInt32(CommandSeq);
            writer.WriteUInt64(ClientId);
            writer.WriteInt32(PlayerSlot);
            WriteUnitUid(writer, ControlledUnitUid);
            writer.WriteInt32(TargetTick);
            writer.WriteByte((byte)CommandKind);
            writer.WriteInt32(BuildLocalTick);
            writer.WriteInt32(PayloadByteLength);
            writer.WriteUInt32(SchemaVersion);
        }

        internal static void WriteUnitUid(CanonicalByteWriter writer, UnitUid uid)
        {
            writer.WriteInt32(uid.SpawnLogicTick);
            writer.WriteInt32(uid.RuntimeEntityPrefabId);
            writer.WriteByte(uid.SpawnSequenceInTick);
        }
    }

    public enum AbilityCancelReason : byte
    {
        Unspecified = 0,
    }

    public readonly struct GameplayCommand
    {
        private const int MovePayloadByteLength = 16;
        private const int AttackPayloadByteLength = 9;
        private const int CastAbilityPayloadByteLength = 44;
        private const int CancelAbilityPayloadByteLength = 2;

        public readonly CommandHeader Header;
        public readonly fp2 MoveTargetPoint;
        public readonly UnitUid AttackTargetUid;
        public readonly byte AbilitySlot;
        public readonly AbilitySignalVerb AbilityVerb;
        public readonly AimSnapshot Aim;
        public readonly AbilityCancelReason CancelReason;

        private GameplayCommand(
            in CommandHeader header,
            fp2 moveTargetPoint,
            UnitUid attackTargetUid,
            byte abilitySlot,
            AbilitySignalVerb abilityVerb,
            AimSnapshot aim,
            AbilityCancelReason cancelReason)
        {
            Header = header;
            MoveTargetPoint = moveTargetPoint;
            AttackTargetUid = attackTargetUid;
            AbilitySlot = abilitySlot;
            AbilityVerb = abilityVerb;
            Aim = aim;
            CancelReason = cancelReason;
        }

        public UnitUid ControlledUnitUid => Header.ControlledUnitUid;
        public UnitUid UnitUid => Header.ControlledUnitUid;
        public int TargetTick => Header.TargetTick;
        public GameplayCommandKind Kind => Header.CommandKind;
        public uint CommandSeq => Header.CommandSeq;
        public int PlayerSlot => Header.PlayerSlot;

        public static GameplayCommand CreateMove(
            in CommandHeader header,
            fp2 targetPoint)
        {
            CommandHeader canonicalHeader = header.WithPayload(
                GameplayCommandKind.Move, MovePayloadByteLength);
            return new GameplayCommand(
                canonicalHeader, targetPoint, default, 0, default, default, default);
        }

        public static GameplayCommand CreateAttack(
            in CommandHeader header,
            UnitUid attackTargetUid)
        {
            if (!attackTargetUid.IsValid())
            {
                throw new ArgumentException(
                    "Attack command requires a valid target UnitUid.", nameof(attackTargetUid));
            }

            CommandHeader canonicalHeader = header.WithPayload(
                GameplayCommandKind.Attack, AttackPayloadByteLength);
            return new GameplayCommand(
                canonicalHeader, default, attackTargetUid, 0, default, default, default);
        }

        public static GameplayCommand CreateCastAbility(
            in CommandHeader header,
            byte slot,
            AbilitySignalVerb verb,
            AimSnapshot aim)
        {
            CommandHeader canonicalHeader = header.WithPayload(
                GameplayCommandKind.CastAbility, CastAbilityPayloadByteLength);
            return new GameplayCommand(
                canonicalHeader, default, default, slot, verb, aim, default);
        }

        public static GameplayCommand CreateCancelAbility(
            in CommandHeader header,
            byte slot,
            AbilityCancelReason reason = AbilityCancelReason.Unspecified)
        {
            CommandHeader canonicalHeader = header.WithPayload(
                GameplayCommandKind.CancelAbility, CancelAbilityPayloadByteLength);
            return new GameplayCommand(
                canonicalHeader, default, default, slot, AbilitySignalVerb.Cancel, default, reason);
        }

        public static readonly GameplayCommand None = default;

        public bool IsNone => Kind == GameplayCommandKind.None;

        public void WriteCanonicalBytes(CanonicalByteWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            Header.WriteCanonicalBytes(writer);

            switch (Kind)
            {
                case GameplayCommandKind.Move:
                    writer.WriteFp(MoveTargetPoint.x);
                    writer.WriteFp(MoveTargetPoint.y);
                    break;

                case GameplayCommandKind.Attack:
                    CommandHeader.WriteUnitUid(writer, AttackTargetUid);
                    break;

                case GameplayCommandKind.CastAbility:
                    writer.WriteByte(AbilitySlot);
                    writer.WriteByte((byte)AbilityVerb);
                    WriteAim(writer, Aim);
                    break;

                case GameplayCommandKind.CancelAbility:
                    writer.WriteByte(AbilitySlot);
                    writer.WriteByte((byte)CancelReason);
                    break;

                case GameplayCommandKind.None:
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Canonical payload writing is not implemented for {Kind}.");
            }
        }

        private static void WriteAim(CanonicalByteWriter writer, AimSnapshot aim)
        {
            writer.WriteByte((byte)aim.Kind);
            CommandHeader.WriteUnitUid(writer, aim.TargetUnitUid);
            writer.WriteFp(aim.TargetPoint.x);
            writer.WriteFp(aim.TargetPoint.y);
            writer.WriteFp(aim.Direction.x);
            writer.WriteFp(aim.Direction.y);
        }
    }
}
