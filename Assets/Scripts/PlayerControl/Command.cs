using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics.FixedPoint;
using Unity.Netcode;
using UnityEngine;

public enum CommandType : byte
{
    Move,
    Attack,
    AbilityPress,
    AbilityRelease,
    AbilityCancel,
    PurchaseItem
}

public interface ICommand
{
    CommandType Type { get; }
    UnitUID ControlledUnitId { get; set; }
}

public struct MoveCommand : ICommand, INetworkSerializable
{
    public CommandType Type => CommandType.Move;

    private UnitUID controlledUnitId;
    public UnitUID ControlledUnitId
    {
        get => controlledUnitId;
        set => controlledUnitId = value;
    }

    public Vector3 TargetPosition;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        int prefabId = controlledUnitId.PrefabId;
        ulong frame = controlledUnitId.Frame;
        byte teamId = controlledUnitId.TeamId;
        byte sequence = controlledUnitId.Sequence;

        serializer.SerializeValue(ref prefabId);
        serializer.SerializeValue(ref frame);
        serializer.SerializeValue(ref teamId);
        serializer.SerializeValue(ref sequence);

        if (serializer.IsReader)
        {
            controlledUnitId = new UnitUID(prefabId, frame, teamId, sequence);
        }

        serializer.SerializeValue(ref TargetPosition);
    }
}

public struct AttackCommand : ICommand, INetworkSerializable
{
    public CommandType Type => CommandType.Attack;

    private UnitUID controlledUnitId;
    public UnitUID ControlledUnitId
    {
        get => controlledUnitId;
        set => controlledUnitId = value;
    }

    public UnitUID TargetUnitId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        int cprefab = controlledUnitId.PrefabId;
        ulong cf = controlledUnitId.Frame;
        byte ct = controlledUnitId.TeamId;
        byte cs = controlledUnitId.Sequence;
        serializer.SerializeValue(ref cprefab);
        serializer.SerializeValue(ref cf);
        serializer.SerializeValue(ref ct);
        serializer.SerializeValue(ref cs);
        if (serializer.IsReader)
            controlledUnitId = new UnitUID(cprefab, cf, ct, cs);

        int tprefab = TargetUnitId.PrefabId;
        ulong tf = TargetUnitId.Frame;
        byte tt = TargetUnitId.TeamId;
        byte ts = TargetUnitId.Sequence;
        serializer.SerializeValue(ref tprefab);
        serializer.SerializeValue(ref tf);
        serializer.SerializeValue(ref tt);
        serializer.SerializeValue(ref ts);
        if (serializer.IsReader)
            TargetUnitId = new UnitUID(tprefab, tf, tt, ts);
    }
}

public struct AbilityCommand : ICommand, INetworkSerializable
{
    // ===== 实际数据 =====

    public CommandType CommandType;

    private UnitUID controlledUnitUid;

    public int AbilityId;

    public bool HasTargetUnit;
    public UnitUID TargetUnit;

    public bool HasTargetPosition;
    public Vector3 TargetPosition;

    // ===== 接口实现 =====

    public CommandType Type => CommandType;

    public UnitUID ControlledUnitId
    {
        get => controlledUnitUid;
        set => controlledUnitUid = value;
    }

    public UnitUID ControlledUnitUID
    {
        get => controlledUnitUid;
        set => controlledUnitUid = value;
    }

    // ===== 序列化 =====

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        byte type = (byte)CommandType;
        serializer.SerializeValue(ref type);
        if (serializer.IsReader)
            CommandType = (CommandType)type;

        int cprefab = controlledUnitUid.PrefabId;
        ulong cf = controlledUnitUid.Frame;
        byte ct = controlledUnitUid.TeamId;
        byte cs = controlledUnitUid.Sequence;

        serializer.SerializeValue(ref cprefab);
        serializer.SerializeValue(ref cf);
        serializer.SerializeValue(ref ct);
        serializer.SerializeValue(ref cs);

        if (serializer.IsReader)
            controlledUnitUid = new UnitUID(cprefab, cf, ct, cs);

        serializer.SerializeValue(ref AbilityId);

        serializer.SerializeValue(ref HasTargetUnit);
        if (HasTargetUnit)
        {
            int tprefab = TargetUnit.PrefabId;
            ulong tf = TargetUnit.Frame;
            byte tt = TargetUnit.TeamId;
            byte ts = TargetUnit.Sequence;

            serializer.SerializeValue(ref tprefab);
            serializer.SerializeValue(ref tf);
            serializer.SerializeValue(ref tt);
            serializer.SerializeValue(ref ts);

            if (serializer.IsReader)
                TargetUnit = new UnitUID(tprefab, tf, tt, ts);
        }

        serializer.SerializeValue(ref HasTargetPosition);
        if (HasTargetPosition)
        {
            serializer.SerializeValue(ref TargetPosition);
        }
    }
}

public struct PurchaseItemCommand : ICommand, INetworkSerializable
{
    public CommandType Type => CommandType.PurchaseItem;

    private UnitUID controlledUnitId;
    public UnitUID ControlledUnitId
    {
        get => controlledUnitId;
        set => controlledUnitId = value;
    }

    public int ItemId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        // 分解 controlledUnitId
        int prefab = controlledUnitId.PrefabId;
        ulong frame = controlledUnitId.Frame;
        byte team = controlledUnitId.TeamId;
        byte seq = controlledUnitId.Sequence;
        serializer.SerializeValue(ref prefab);
        serializer.SerializeValue(ref frame);
        serializer.SerializeValue(ref team);
        serializer.SerializeValue(ref seq);
        if (serializer.IsReader)
            controlledUnitId = new UnitUID(prefab, frame, team, seq);

        serializer.SerializeValue(ref ItemId);
    }
}

public static class CommandSerializer
{
    /// <summary> 将指令列表写入 FastBufferWriter </summary>
    public static void Serialize(FastBufferWriter writer, IList<ICommand> commands)
    {
        writer.WriteValueSafe(commands.Count);
        foreach (var cmd in commands)
        {
            writer.WriteValueSafe((byte)cmd.Type);
            switch (cmd.Type)
            {
                case CommandType.Move:
                    writer.WriteNetworkSerializable((MoveCommand)cmd);
                    break;
                case CommandType.Attack:
                    writer.WriteNetworkSerializable((AttackCommand)cmd);
                    break;
                case CommandType.AbilityPress:
                case CommandType.AbilityRelease:
                case CommandType.AbilityCancel:
                    writer.WriteNetworkSerializable((AbilityCommand)cmd);
                    break;
                case CommandType.PurchaseItem:
                    writer.WriteNetworkSerializable((PurchaseItemCommand)cmd);
                    break;
                default:
                    Debug.LogError($"Unknown command type: {cmd.Type}");
                    break;
            }
        }
    }

    /// <summary> 从 FastBufferReader 读取指令列表 </summary>
    public static List<ICommand> Deserialize(FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        var list = new List<ICommand>(count);
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
                case CommandType.AbilityPress:
                case CommandType.AbilityRelease:
                case CommandType.AbilityCancel:
                    reader.ReadNetworkSerializable(out AbilityCommand abilityCmd);
                    list.Add(abilityCmd);
                    break;
                case CommandType.PurchaseItem:
                    reader.ReadNetworkSerializable(out PurchaseItemCommand purchaseCmd);
                    list.Add(purchaseCmd);
                    break;
                default:
                    Debug.LogError($"Unsupported command type: {type}");
                    break;
            }
        }
        return list;
    }

    public static List<ICommand> Deserialize(byte[] data)
    {
        using var reader = new FastBufferReader(data, Allocator.Temp);
        return Deserialize(reader);
    }
}