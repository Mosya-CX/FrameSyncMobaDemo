using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics.FixedPoint;
using Unity.Netcode;
using UnityEngine;

public enum CommandType : byte
{
    Move,
    Attack,
    TriggerAbility,
    BuyItem,
    SellItem,
    UseItem,
}

public abstract class CommandBase : INetworkSerializable
{
    public CommandId CommandId;
    public UnitUID ReceiverUnitId;
    public uint TargetTick;

    public virtual void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        ulong clientId = CommandId.ClientId;
        uint frame = CommandId.Frame;
        byte seq = CommandId.Seq;
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref frame);
        serializer.SerializeValue(ref seq);
        if (serializer.IsReader)
            CommandId = new CommandId(clientId, frame, seq);

        serializer.SerializeValue(ref TargetTick);

        int cprefab = ReceiverUnitId.PrefabId;
        uint cf = ReceiverUnitId.Frame;
        byte ct = ReceiverUnitId.TeamId;
        byte cs = ReceiverUnitId.Sequence;
        serializer.SerializeValue(ref cprefab);
        serializer.SerializeValue(ref cf);
        serializer.SerializeValue(ref ct);
        serializer.SerializeValue(ref cs);
        if (serializer.IsReader)
            ReceiverUnitId = new UnitUID(cprefab, cf, ct, cs);
    }

    public abstract CommandType GetCommandType();

    public bool IsOwner => LocalController.Local ? (LocalController.Local.ControlledUnitUID == ReceiverUnitId) : false;
}

public class MoveCommand : CommandBase
{
    public override CommandType GetCommandType() => CommandType.Move;
    public fp3 TargetPosition;

    public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
    {
        base.NetworkSerialize(serializer);

        long x = TargetPosition.x.RawValue;
        long y = TargetPosition.y.RawValue;
        long z = TargetPosition.z.RawValue;

        serializer.SerializeValue(ref x);
        serializer.SerializeValue(ref y);
        serializer.SerializeValue(ref z);

        if (serializer.IsReader)
            TargetPosition = new fp3(fp.FromRaw(x), fp.FromRaw(y), fp.FromRaw(z));
    }
}

public class AttackCommand : CommandBase
{
    public override CommandType GetCommandType() => CommandType.Attack;
    public UnitUID TargetUnitId;

    public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
    {
        base.NetworkSerialize(serializer);

        int prefab = TargetUnitId.PrefabId;
        uint frame = TargetUnitId.Frame;
        byte team = TargetUnitId.TeamId;
        byte seq = TargetUnitId.Sequence;

        serializer.SerializeValue(ref prefab);
        serializer.SerializeValue(ref frame);
        serializer.SerializeValue(ref team);
        serializer.SerializeValue(ref seq);

        if (serializer.IsReader)
            TargetUnitId = new UnitUID(prefab, frame, team, seq);
    }
}

public struct AbilityTriggerContext
{
    public UnitUID? TargetUID;
    public fp3? TargetPosition;
}

public class AbilityCommand : CommandBase
{
    public override CommandType GetCommandType() => CommandType.TriggerAbility;

    public int AbilityId;
    public bool QueueIfBusy;
    public AbilityTriggerContext Context;

    public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
    {
        base.NetworkSerialize(serializer);

        serializer.SerializeValue(ref AbilityId);
        serializer.SerializeValue(ref QueueIfBusy);

        bool hasTargetUnit = Context.TargetUID.HasValue;
        bool hasTargetPosition = Context.TargetPosition.HasValue;

        serializer.SerializeValue(ref hasTargetUnit);
        serializer.SerializeValue(ref hasTargetPosition);

        if (hasTargetUnit)
        {
            int prefab = Context.TargetUID.Value.PrefabId;
            uint frame = Context.TargetUID.Value.Frame;
            byte team = Context.TargetUID.Value.TeamId;
            byte seq = Context.TargetUID.Value.Sequence;

            serializer.SerializeValue(ref prefab);
            serializer.SerializeValue(ref frame);
            serializer.SerializeValue(ref team);
            serializer.SerializeValue(ref seq);

            if (serializer.IsReader)
                Context.TargetUID = new UnitUID(prefab, frame, team, seq);
        }
        else if (serializer.IsReader)
        {
            Context.TargetUID = null;
        }

        if (hasTargetPosition)
        {
            long x = Context.TargetPosition.Value.x.RawValue;
            long y = Context.TargetPosition.Value.y.RawValue;
            long z = Context.TargetPosition.Value.z.RawValue;

            serializer.SerializeValue(ref x);
            serializer.SerializeValue(ref y);
            serializer.SerializeValue(ref z);

            if (serializer.IsReader)
                Context.TargetPosition = new fp3(fp.FromRaw(x), fp.FromRaw(y), fp.FromRaw(z));
        }
        else if (serializer.IsReader)
        {
            Context.TargetPosition = null;
        }
    }
}

public class BuyItemCommand : CommandBase
{
    public override CommandType GetCommandType() => CommandType.BuyItem;
    public int ItemId;

    public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
    {
        base.NetworkSerialize(serializer);
        serializer.SerializeValue(ref ItemId);
    }
}

public class SellItemCommand : CommandBase
{
    public override CommandType GetCommandType() => CommandType.SellItem;
    public int SellItenIndex;

    public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
    {
        base.NetworkSerialize(serializer);
        serializer.SerializeValue(ref SellItenIndex);
    }
}

public class UseItemCommand : CommandBase
{
    public override CommandType GetCommandType() => CommandType.UseItem;

    public int UseItenDataId;
    public UnitUID? TargetUID;
    public fp3? TargetPosition;

    public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
    {
        base.NetworkSerialize(serializer);

        serializer.SerializeValue(ref UseItenDataId);

        bool hasTargetUnit = TargetUID.HasValue;
        bool hasTargetPosition = TargetPosition.HasValue;

        serializer.SerializeValue(ref hasTargetUnit);
        serializer.SerializeValue(ref hasTargetPosition);

        if (hasTargetUnit)
        {
            int prefab = TargetUID.Value.PrefabId;
            uint frame = TargetUID.Value.Frame;
            byte team = TargetUID.Value.TeamId;
            byte seq = TargetUID.Value.Sequence;

            serializer.SerializeValue(ref prefab);
            serializer.SerializeValue(ref frame);
            serializer.SerializeValue(ref team);
            serializer.SerializeValue(ref seq);

            if (serializer.IsReader)
                TargetUID = new UnitUID(prefab, frame, team, seq);
        }
        else if (serializer.IsReader)
        {
            TargetUID = null;
        }

        if (hasTargetPosition)
        {
            long x = TargetPosition.Value.x.RawValue;
            long y = TargetPosition.Value.y.RawValue;
            long z = TargetPosition.Value.z.RawValue;

            serializer.SerializeValue(ref x);
            serializer.SerializeValue(ref y);
            serializer.SerializeValue(ref z);

            if (serializer.IsReader)
                TargetPosition = new fp3(fp.FromRaw(x), fp.FromRaw(y), fp.FromRaw(z));
        }
        else if (serializer.IsReader)
        {
            TargetPosition = null;
        }
    }
}

public static class CommandSerializer
{
    public static void Serialize(FastBufferWriter writer, IList<CommandBase> commands)
    {
        writer.WriteValueSafe(commands.Count);
        foreach (var cmd in commands)
        {
            writer.WriteValueSafe((byte)cmd.GetCommandType());
            switch (cmd.GetCommandType())
            {
                case CommandType.Move:
                    writer.WriteNetworkSerializable((MoveCommand)cmd);
                    break;
                case CommandType.Attack:
                    writer.WriteNetworkSerializable((AttackCommand)cmd);
                    break;
                case CommandType.TriggerAbility:
                    writer.WriteNetworkSerializable((AbilityCommand)cmd);
                    break;
                case CommandType.BuyItem:
                    writer.WriteNetworkSerializable((BuyItemCommand)cmd);
                    break;
                case CommandType.SellItem:
                    writer.WriteNetworkSerializable((SellItemCommand)cmd);
                    break;
                case CommandType.UseItem:
                    writer.WriteNetworkSerializable((UseItemCommand)cmd);
                    break;
                default:
                    Debug.LogError($"Unknown command type: {cmd.GetCommandType()}");
                    break;
            }
        }
    }

    public static List<CommandBase> Deserialize(FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        var list = new List<CommandBase>(count);

        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out byte typeByte);
            CommandType type = (CommandType)typeByte;

            switch (type)
            {
                case CommandType.Move:
                    reader.ReadNetworkSerializable(out MoveCommand moveCmd);
                    list.Add(moveCmd);
                    break;
                case CommandType.Attack:
                    reader.ReadNetworkSerializable(out AttackCommand attackCmd);
                    list.Add(attackCmd);
                    break;
                case CommandType.TriggerAbility:
                    reader.ReadNetworkSerializable(out AbilityCommand abilityCmd);
                    list.Add(abilityCmd);
                    break;
                case CommandType.BuyItem:
                    reader.ReadNetworkSerializable(out BuyItemCommand buyItemCmd);
                    list.Add(buyItemCmd);
                    break;
                case CommandType.SellItem:
                    reader.ReadNetworkSerializable(out SellItemCommand sellItemCmd);
                    list.Add(sellItemCmd);
                    break;
                case CommandType.UseItem:
                    reader.ReadNetworkSerializable(out UseItemCommand useItemCmd);
                    list.Add(useItemCmd);
                    break;
                default:
                    Debug.LogError($"Unsupported command type: {type}");
                    break;
            }
        }

        return list;
    }

    public static List<CommandBase> Deserialize(byte[] data)
    {
        using var reader = new FastBufferReader(data, Allocator.Temp);
        return Deserialize(reader);
    }
}

public readonly struct CommandId : IEquatable<CommandId>
{
    public readonly ulong ClientId;
    public readonly uint Frame;
    public readonly byte Seq;

    public CommandId(ulong clientId, uint frame, byte seq)
    {
        ClientId = clientId;
        Frame = frame;
        Seq = seq;
    }

    public bool Equals(CommandId other) =>
        ClientId == other.ClientId && Frame == other.Frame && Seq == other.Seq;

    public override bool Equals(object obj) =>
        obj is CommandId other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(ClientId, Frame, Seq);

    public static bool operator ==(CommandId left, CommandId right) => left.Equals(right);
    public static bool operator !=(CommandId left, CommandId right) => !(left == right);

    public override string ToString() => $"{ClientId}_{Frame}_{Seq}";
}