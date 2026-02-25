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
    PurchaseItem,
    SpawnUnit,
    DespawnUnit,
}

public interface ICommand
{
    CommandType Type { get; }
    UnitUID ControlledUnitId { get; set; }
    uint TargetTick { get; set; } 
}

public struct MoveCommand : ICommand, INetworkSerializable
{
    public CommandType Type => CommandType.Move;
    public UnitUID ControlledUnitId { get; set; }
    public uint TargetTick { get; set; }

    public Vector3 TargetPosition;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        var targetTick = TargetTick;
        serializer.SerializeValue(ref targetTick);
        if (serializer.IsReader)
            TargetTick = targetTick;

        int prefabId = ControlledUnitId.PrefabId;
        ulong frame = ControlledUnitId.Frame;
        byte teamId = ControlledUnitId.TeamId;
        byte sequence = ControlledUnitId.Sequence;
        serializer.SerializeValue(ref prefabId);
        serializer.SerializeValue(ref frame);
        serializer.SerializeValue(ref teamId);
        serializer.SerializeValue(ref sequence);
        if (serializer.IsReader)
            ControlledUnitId = new UnitUID(prefabId, frame, teamId, sequence);

        serializer.SerializeValue(ref TargetPosition);
    }
}

public struct AttackCommand : ICommand, INetworkSerializable
{
    public CommandType Type => CommandType.Attack;
    public UnitUID ControlledUnitId { get; set; }
    public uint TargetTick { get; set; }

    public UnitUID TargetUnitId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        var targetTick = TargetTick;
        serializer.SerializeValue(ref targetTick);
        if (serializer.IsReader)
            TargetTick = targetTick;

        int cprefab = ControlledUnitId.PrefabId;
        ulong cf = ControlledUnitId.Frame;
        byte ct = ControlledUnitId.TeamId;
        byte cs = ControlledUnitId.Sequence;
        serializer.SerializeValue(ref cprefab);
        serializer.SerializeValue(ref cf);
        serializer.SerializeValue(ref ct);
        serializer.SerializeValue(ref cs);
        if (serializer.IsReader)
            ControlledUnitId = new UnitUID(cprefab, cf, ct, cs);

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
    public CommandType Type => CommandType;
    public UnitUID ControlledUnitId { get; set; }
    public uint TargetTick { get; set; }

    public CommandType CommandType;  // AbilityPress / AbilityRelease / AbilityCancel
    public int AbilityId;
    public bool HasTargetUnit;
    public UnitUID TargetUnit;
    public bool HasTargetPosition;
    public Vector3 TargetPosition;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        var targetTick = TargetTick;
        serializer.SerializeValue(ref targetTick);
        if (serializer.IsReader)
            TargetTick = targetTick;

        byte type = (byte)CommandType;
        serializer.SerializeValue(ref type);
        if (serializer.IsReader)
            CommandType = (CommandType)type;

        int cprefab = ControlledUnitId.PrefabId;
        ulong cf = ControlledUnitId.Frame;
        byte ct = ControlledUnitId.TeamId;
        byte cs = ControlledUnitId.Sequence;
        serializer.SerializeValue(ref cprefab);
        serializer.SerializeValue(ref cf);
        serializer.SerializeValue(ref ct);
        serializer.SerializeValue(ref cs);
        if (serializer.IsReader)
            ControlledUnitId = new UnitUID(cprefab, cf, ct, cs);

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
            serializer.SerializeValue(ref TargetPosition);
    }
}

public struct PurchaseItemCommand : ICommand, INetworkSerializable
{
    public CommandType Type => CommandType.PurchaseItem;
    public UnitUID ControlledUnitId { get; set; }
    public uint TargetTick { get; set; }
    public int ItemId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        var targetTick = TargetTick;
        serializer.SerializeValue(ref targetTick);
        if (serializer.IsReader)
            TargetTick = targetTick;

        int prefab = ControlledUnitId.PrefabId;
        ulong frame = ControlledUnitId.Frame;
        byte team = ControlledUnitId.TeamId;
        byte seq = ControlledUnitId.Sequence;
        serializer.SerializeValue(ref prefab);
        serializer.SerializeValue(ref frame);
        serializer.SerializeValue(ref team);
        serializer.SerializeValue(ref seq);
        if (serializer.IsReader)
            ControlledUnitId = new UnitUID(prefab, frame, team, seq);
        serializer.SerializeValue(ref ItemId);
    }
}

public struct SpawnUnitCommand : ICommand, INetworkSerializable
{
    public CommandType Type => CommandType.SpawnUnit;
    public UnitUID ControlledUnitId { get; set; } 
    public uint TargetTick { get; set; }

    public int PrefabId;
    public Vector3 SpawnPosition;
    public Quaternion SpawnRotation;
    public byte TeamId;
    public int StartLevel;
    public SpawnableMode Mode;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        var targetTick = TargetTick;
        serializer.SerializeValue(ref targetTick);
        if (serializer.IsReader)
            TargetTick = targetTick;

        serializer.SerializeValue(ref PrefabId);
        serializer.SerializeValue(ref SpawnPosition);
        serializer.SerializeValue(ref SpawnRotation);
        serializer.SerializeValue(ref TeamId);
        serializer.SerializeValue(ref StartLevel);
        byte mode = (byte)Mode;
        serializer.SerializeValue(ref mode);
        if (serializer.IsReader)
            Mode = (SpawnableMode)mode;
    }
}

public struct DespawnUnitCommand : ICommand, INetworkSerializable
{
    public CommandType Type => CommandType.DespawnUnit;
    public UnitUID ControlledUnitId { get; set; } 
    public uint TargetTick { get; set; }

    public UnitUID UnitId;          // 要销毁的单位ID
    public SpawnableMode Mode;       // 销毁模式（直接销毁或回池）

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        var targetTick = TargetTick;
        serializer.SerializeValue(ref targetTick);
        if (serializer.IsReader)
            TargetTick = targetTick;

        // 序列化 UnitId 的四个字段
        int prefab = UnitId.PrefabId;
        ulong frame = UnitId.Frame;
        byte team = UnitId.TeamId;
        byte seq = UnitId.Sequence;
        serializer.SerializeValue(ref prefab);
        serializer.SerializeValue(ref frame);
        serializer.SerializeValue(ref team);
        serializer.SerializeValue(ref seq);
        if (serializer.IsReader)
            UnitId = new UnitUID(prefab, frame, team, seq);

        byte mode = (byte)Mode;
        serializer.SerializeValue(ref mode);
        if (serializer.IsReader)
            Mode = (SpawnableMode)mode;
    }
}

public static class CommandSerializer
{
    public static void Serialize(FastBufferWriter writer, IList<ICommand> commands)
    {
        writer.WriteValueSafe(commands.Count);
        foreach (var cmd in commands)
        {
            writer.WriteValueSafe((byte)cmd.Type);
            // 注意：每个指令序列化时内部会处理 TargetTick，不需要在此额外写
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
                case CommandType.SpawnUnit:
                    writer.WriteNetworkSerializable((SpawnUnitCommand)cmd);
                    break;
                default:
                    Debug.LogError($"Unknown command type: {cmd.Type}");
                    break;
            }
        }
    }

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
                case CommandType.SpawnUnit:
                    reader.ReadNetworkSerializable(out SpawnUnitCommand spawnCmd);
                    list.Add(spawnCmd);
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