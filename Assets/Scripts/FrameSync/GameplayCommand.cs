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

    public enum EquipmentShopCommandOperationType : byte
    {
        Purchase = 0,
        Sell = 1,
        Undo = 2,
    }

    public readonly struct GameplayCommand
    {
        private const int MovePayloadByteLength = 16;
        private const int AttackPayloadByteLength = 9;
        private const int CastAbilityPayloadByteLength = 44;
        private const int CancelAbilityPayloadByteLength = 2;
        private const int AllocateAbilitySkillPointPayloadByteLength = 1;
        private const int EquipmentPurchasePayloadByteLength = 5;
        private const int EquipmentSellPayloadByteLength = 2;
        private const int EquipmentUndoPayloadByteLength = 1;
        private const int SwapEquipmentSlotPayloadByteLength = 2;
        private const int UseItemPayloadByteLength = 43;
        private const int DebugPayloadByteLength = 5;

        public readonly CommandHeader Header;
        public readonly fp2 MoveTargetPoint;
        public readonly UnitUid AttackTargetUid;
        public readonly byte AbilitySlot;
        public readonly AbilitySignalVerb AbilityVerb;
        public readonly AimSnapshot Aim;
        public readonly AbilityCancelReason CancelReason;
        public readonly EquipmentShopCommandOperationType ShopOperationType;
        public readonly int EquipmentId;
        public readonly byte SourceSlot;
        public readonly byte TargetSlot;

        /// <summary>Debug command operation (see DebugCommandOp).</summary>
        public byte DebugOp => AbilitySlot;

        /// <summary>Debug command value (e.g. gold amount).</summary>
        public int DebugValue => EquipmentId;

        private GameplayCommand(
            in CommandHeader header,
            fp2 moveTargetPoint,
            UnitUid attackTargetUid,
            byte abilitySlot,
            AbilitySignalVerb abilityVerb,
            AimSnapshot aim,
            AbilityCancelReason cancelReason,
            EquipmentShopCommandOperationType shopOperationType,
            int equipmentId,
            byte sourceSlot,
            byte targetSlot)
        {
            Header = header;
            MoveTargetPoint = moveTargetPoint;
            AttackTargetUid = attackTargetUid;
            AbilitySlot = abilitySlot;
            AbilityVerb = abilityVerb;
            Aim = aim;
            CancelReason = cancelReason;
            ShopOperationType = shopOperationType;
            EquipmentId = equipmentId;
            SourceSlot = sourceSlot;
            TargetSlot = targetSlot;
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
                canonicalHeader, targetPoint, default, 0, default, default, default,
                default, 0, 0, 0);
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
                canonicalHeader, default, attackTargetUid, 0, default, default, default,
                default, 0, 0, 0);
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
                canonicalHeader, default, default, slot, verb, aim, default,
                default, 0, 0, 0);
        }

        public static GameplayCommand CreateCancelAbility(
            in CommandHeader header,
            byte slot,
            AbilityCancelReason reason = AbilityCancelReason.Unspecified)
        {
            CommandHeader canonicalHeader = header.WithPayload(
                GameplayCommandKind.CancelAbility, CancelAbilityPayloadByteLength);
            return new GameplayCommand(
                canonicalHeader, default, default, slot, AbilitySignalVerb.Cancel, default, reason,
                default, 0, 0, 0);
        }

        public static GameplayCommand CreateAllocateAbilitySkillPoint(
            in CommandHeader header,
            byte slot)
        {
            CommandHeader canonicalHeader = header.WithPayload(
                GameplayCommandKind.AllocateAbilitySkillPoint,
                AllocateAbilitySkillPointPayloadByteLength);
            return new GameplayCommand(
                canonicalHeader, default, default, slot, default, default, default,
                default, 0, 0, 0);
        }

        public static GameplayCommand CreateDebugCommand(
            in CommandHeader header,
            byte op,
            int value)
        {
            CommandHeader canonicalHeader = header.WithPayload(
                GameplayCommandKind.Debug,
                DebugPayloadByteLength);
            return new GameplayCommand(
                canonicalHeader, default, default, op, default, default, default,
                default, value, 0, 0);
        }

        public static GameplayCommand CreateEquipmentPurchase(
            in CommandHeader header,
            int equipmentId)
        {
            if (equipmentId <= 0)
                throw new ArgumentOutOfRangeException(nameof(equipmentId));
            CommandHeader canonicalHeader = header.WithPayload(
                GameplayCommandKind.EquipmentShop,
                EquipmentPurchasePayloadByteLength);
            return new GameplayCommand(
                canonicalHeader, default, default, 0, default, default, default,
                EquipmentShopCommandOperationType.Purchase, equipmentId, 0, 0);
        }

        public static GameplayCommand CreateEquipmentSell(
            in CommandHeader header,
            byte sourceSlot)
        {
            CommandHeader canonicalHeader = header.WithPayload(
                GameplayCommandKind.EquipmentShop,
                EquipmentSellPayloadByteLength);
            return new GameplayCommand(
                canonicalHeader, default, default, 0, default, default, default,
                EquipmentShopCommandOperationType.Sell, 0, sourceSlot, 0);
        }

        public static GameplayCommand CreateEquipmentUndo(
            in CommandHeader header)
        {
            CommandHeader canonicalHeader = header.WithPayload(
                GameplayCommandKind.EquipmentShop,
                EquipmentUndoPayloadByteLength);
            return new GameplayCommand(
                canonicalHeader, default, default, 0, default, default, default,
                EquipmentShopCommandOperationType.Undo, 0, 0, 0);
        }

        public static GameplayCommand CreateSwapEquipmentSlot(
            in CommandHeader header,
            byte sourceSlot,
            byte targetSlot)
        {
            CommandHeader canonicalHeader = header.WithPayload(
                GameplayCommandKind.SwapEquipmentSlot,
                SwapEquipmentSlotPayloadByteLength);
            return new GameplayCommand(
                canonicalHeader, default, default, 0, default, default, default,
                default, 0, sourceSlot, targetSlot);
        }

        public static GameplayCommand CreateUseItem(
            in CommandHeader header,
            byte sourceSlot,
            AimSnapshot aim)
        {
            CommandHeader canonicalHeader = header.WithPayload(
                GameplayCommandKind.UseItem,
                UseItemPayloadByteLength);
            return new GameplayCommand(
                canonicalHeader, default, default, 0, default, aim, default,
                default, 0, sourceSlot, 0);
        }

        public static readonly GameplayCommand None = default;

        public bool IsNone => Kind == GameplayCommandKind.None;

        /// <summary>
        /// Returns a copy of this Command targeting a different Tick. Used by
        /// the server to re-target late Commands to its current Tick instead
        /// of hard-rejecting them; all other fields stay identical.
        /// </summary>
        public GameplayCommand WithTargetTick(int targetTick)
        {
            if (IsNone)
            {
                return this;
            }
            CommandHeader header = new CommandHeader(
                Header.CommandSeq,
                Header.ClientId,
                Header.PlayerSlot,
                Header.ControlledUnitUid,
                targetTick,
                Header.CommandKind,
                Header.BuildLocalTick,
                Header.PayloadByteLength,
                Header.SchemaVersion);
            switch (Kind)
            {
                case GameplayCommandKind.Move:
                    return CreateMove(header, MoveTargetPoint);
                case GameplayCommandKind.Attack:
                    return CreateAttack(header, AttackTargetUid);
                case GameplayCommandKind.CastAbility:
                    return CreateCastAbility(
                        header,
                        AbilitySlot,
                        AbilityVerb,
                        Aim);
                case GameplayCommandKind.CancelAbility:
                    return CreateCancelAbility(
                        header,
                        AbilitySlot,
                        CancelReason);
                case GameplayCommandKind.AllocateAbilitySkillPoint:
                    return CreateAllocateAbilitySkillPoint(
                        header,
                        AbilitySlot);
                case GameplayCommandKind.Debug:
                    return CreateDebugCommand(
                        header,
                        AbilitySlot,
                        EquipmentId);
                case GameplayCommandKind.EquipmentShop:
                    switch (ShopOperationType)
                    {
                        case EquipmentShopCommandOperationType.Purchase:
                            return CreateEquipmentPurchase(
                                header,
                                EquipmentId);
                        case EquipmentShopCommandOperationType.Sell:
                            return CreateEquipmentSell(
                                header,
                                SourceSlot);
                        case EquipmentShopCommandOperationType.Undo:
                            return CreateEquipmentUndo(header);
                        default:
                            throw new InvalidOperationException(
                                $"Unsupported EquipmentShop operation {ShopOperationType}.");
                    }
                case GameplayCommandKind.SwapEquipmentSlot:
                    return CreateSwapEquipmentSlot(
                        header,
                        SourceSlot,
                        TargetSlot);
                case GameplayCommandKind.UseItem:
                    return CreateUseItem(header, SourceSlot, Aim);
                default:
                    throw new InvalidOperationException(
                        $"Re-targeting is not implemented for {Kind}.");
            }
        }

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

                case GameplayCommandKind.AllocateAbilitySkillPoint:
                    writer.WriteByte(AbilitySlot);
                    break;

                case GameplayCommandKind.Debug:
                    writer.WriteByte(AbilitySlot);
                    writer.WriteInt32(EquipmentId);
                    break;

                case GameplayCommandKind.EquipmentShop:
                    writer.WriteByte((byte)ShopOperationType);
                    if (ShopOperationType ==
                        EquipmentShopCommandOperationType.Purchase)
                        writer.WriteInt32(EquipmentId);
                    else if (ShopOperationType ==
                              EquipmentShopCommandOperationType.Sell)
                        writer.WriteByte(SourceSlot);
                    else if (ShopOperationType !=
                              EquipmentShopCommandOperationType.Undo)
                        throw new InvalidOperationException(
                            $"Unsupported EquipmentShop operation {ShopOperationType}.");
                    break;

                case GameplayCommandKind.SwapEquipmentSlot:
                    writer.WriteByte(SourceSlot);
                    writer.WriteByte(TargetSlot);
                    break;

                case GameplayCommandKind.UseItem:
                    writer.WriteByte(SourceSlot);
                    WriteAim(writer, Aim);
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
