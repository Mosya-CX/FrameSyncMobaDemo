using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    internal static class BootstrapPayloadWireCodec
    {
        private const uint Magic = 0x42534D46;
        private const ushort WireVersion = 4;

        public static byte[] Write(
            in GameBootstrapPayload payload)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(
                stream,
                Encoding.UTF8,
                true))
            {
                writer.Write(Magic);
                writer.Write(WireVersion);
                WriteGameStartConfig(
                    writer,
                    payload.GameStartConfig);
                WriteVersions(
                    writer,
                    payload.Versions);
                writer.Write(
                    payload.InitialSnapshotTick);
                writer.Write(payload.StartTick);
                writer.Write(
                    payload.InitialRandomSeed);
                PlayerSlotUnitMapping[] mappings =
                    payload.PlayerSlotMappings;
                writer.Write(mappings.Length);
                for (int i = 0;
                     i < mappings.Length;
                     i++)
                {
                    writer.Write(
                        mappings[i].PlayerSlot);
                    SnapshotObjectCodec.WriteValue(
                        writer,
                        typeof(UnitUid),
                        mappings[i]
                            .ControlledUnitUid,
                        0);
                }
                SnapshotObjectCodec.WriteValue(
                    writer,
                    typeof(GameplaySnapshot),
                    payload.InitialGameplaySnapshot,
                    0);
                writer.Flush();
                if (stream.Length >
                    FrameSyncWireCodec
                        .MaximumPayloadBytes)
                    throw new DeterministicSimulationException(
                        "GameBootstrapPayload exceeds the network payload limit.");
                return stream.ToArray();
            }
        }

        public static GameBootstrapPayload Read(
            byte[] bytes)
        {
            if (bytes == null ||
                bytes.Length == 0 ||
                bytes.Length >
                FrameSyncWireCodec
                    .MaximumPayloadBytes)
                throw new DeterministicSimulationException(
                    "GameBootstrapPayload wire length is invalid.");
            try
            {
                using (var stream = new MemoryStream(
                    bytes,
                    false))
                using (var reader = new BinaryReader(
                    stream,
                    Encoding.UTF8,
                    true))
                {
                    if (reader.ReadUInt32() != Magic ||
                        reader.ReadUInt16() !=
                        WireVersion)
                        throw new DeterministicSimulationException(
                            "GameBootstrapPayload wire header is invalid.");
                    GameStartConfig config =
                        ReadGameStartConfig(reader);
                    FrameSyncVersionHandshake versions =
                        ReadVersions(reader);
                    int snapshotTick =
                        reader.ReadInt32();
                    int startTick =
                        reader.ReadInt32();
                    uint randomSeed =
                        reader.ReadUInt32();
                    int count = ReadCount(
                        reader,
                        10,
                        "player mapping");
                    var mappings =
                        new PlayerSlotUnitMapping[count];
                    for (int i = 0;
                         i < count;
                         i++)
                    {
                        int slot = reader.ReadInt32();
                        var uid = (UnitUid)
                            SnapshotObjectCodec.ReadValue(
                                reader,
                                typeof(UnitUid),
                                0);
                        mappings[i] =
                            new PlayerSlotUnitMapping(
                                slot,
                                uid);
                    }
                    var snapshot = (GameplaySnapshot)
                        SnapshotObjectCodec.ReadValue(
                            reader,
                            typeof(GameplaySnapshot),
                            0);
                    if (stream.Position !=
                        stream.Length)
                        throw new DeterministicSimulationException(
                            "GameBootstrapPayload contains trailing bytes.");
                    return new GameBootstrapPayload(
                        config,
                        versions,
                        snapshot,
                        snapshotTick,
                        startTick,
                        randomSeed,
                        mappings);
                }
            }
            catch (EndOfStreamException exception)
            {
                throw new DeterministicSimulationException(
                    "GameBootstrapPayload is truncated.",
                    exception);
            }
        }

        private static void WriteGameStartConfig(
            BinaryWriter writer,
            in GameStartConfig config)
        {
            WriteString(writer, config.MatchId);
            writer.Write(config.GameModeId);
            writer.Write(config.MapConfigId);
            writer.Write(
                config.GameStartPlayerCount);
            writer.Write(config.TeamCount);
            writer.Write(config.StartTick);
            writer.Write(
                config.InitialRandomSeed);
            writer.Write(
                config.GameplayDataVersion);
            PlayerSlotConfig[] slots =
                config.PlayerSlots;
            writer.Write(slots.Length);
            for (int i = 0;
                 i < slots.Length;
                 i++)
            {
                PlayerSlotConfig slot =
                    slots[i];
                writer.Write(slot.PlayerSlot);
                WriteString(
                    writer,
                    slot.AccountId);
                writer.Write(
                    slot.ControllerClientId);
                writer.Write(slot.TeamId.Value);
                writer.Write(
                    slot.HeroConfigId);
                writer.Write(
                    slot.SpawnPointId);
            }
        }

        private static GameStartConfig
            ReadGameStartConfig(
                BinaryReader reader)
        {
            string matchId = ReadString(reader);
            int gameModeId = reader.ReadInt32();
            int mapConfigId = reader.ReadInt32();
            int playerCount = reader.ReadInt32();
            int teamCount = reader.ReadInt32();
            int startTick = reader.ReadInt32();
            uint randomSeed =
                reader.ReadUInt32();
            uint gameplayDataVersion =
                reader.ReadUInt32();
            int count = ReadCount(
                reader,
                10,
                "player slot");
            if (count != playerCount)
                throw new DeterministicSimulationException(
                    "GameStartConfig player count disagrees with its wire slots.");
            var slots =
                new PlayerSlotConfig[count];
            for (int i = 0;
                 i < count;
                 i++)
                slots[i] =
                    new PlayerSlotConfig(
                        reader.ReadInt32(),
                        ReadString(reader),
                        reader.ReadUInt64(),
                        new TeamId(
                            reader.ReadByte()),
                        reader.ReadInt32(),
                        reader.ReadInt32());
            return new GameStartConfig(
                matchId,
                gameModeId,
                mapConfigId,
                playerCount,
                teamCount,
                slots,
                startTick,
                randomSeed,
                gameplayDataVersion);
        }

        private static void WriteVersions(
            BinaryWriter writer,
            in FrameSyncVersionHandshake versions)
        {
            writer.Write(
                versions.GameplayDataVersion);
            writer.Write(
                versions.MapDataVersion);
            writer.Write(
                versions.GlobalPrefabTableVersion);
            writer.Write(
                versions.CommandSchemaVersion);
            writer.Write(
                versions.SnapshotSchemaVersion);
        }

        private static FrameSyncVersionHandshake
            ReadVersions(BinaryReader reader) =>
            new FrameSyncVersionHandshake(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32());

        internal static void WriteString(
            BinaryWriter writer,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DeterministicSimulationException(
                    "A required network string is empty.");
            byte[] bytes =
                Encoding.UTF8.GetBytes(value);
            if (bytes.Length > 4096)
                throw new DeterministicSimulationException(
                    "A network string exceeds its limit.");
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        internal static string ReadString(
            BinaryReader reader)
        {
            int count = ReadCount(
                reader,
                4096,
                "string byte");
            byte[] bytes =
                reader.ReadBytes(count);
            if (bytes.Length != count)
                throw new EndOfStreamException(
                    "A network string is truncated.");
            string value =
                Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrWhiteSpace(value))
                throw new DeterministicSimulationException(
                    "A required network string is empty.");
            return value;
        }

        internal static int ReadCount(
            BinaryReader reader,
            int maximum,
            string label)
        {
            int count = reader.ReadInt32();
            if (count < 0 ||
                count > maximum)
                throw new DeterministicSimulationException(
                    $"{label} count is invalid.");
            return count;
        }
    }

    internal static class SnapshotObjectCodec
    {
        private const int MaximumDepth = 64;
        private const int MaximumCollectionCount =
            65536;
        private static readonly Dictionary<
            Type,
            FieldInfo[]> FieldCache =
            new Dictionary<Type, FieldInfo[]>();

        public static void WriteValue(
            BinaryWriter writer,
            Type type,
            object value,
            int depth)
        {
            RequireDepth(depth);
            Type nullableType =
                Nullable.GetUnderlyingType(type);
            if (nullableType != null)
            {
                bool hasValue = value != null;
                writer.Write(hasValue);
                if (hasValue)
                    WriteValue(
                        writer,
                        nullableType,
                        value,
                        depth + 1);
                return;
            }
            if (!type.IsValueType)
            {
                bool hasValue = value != null;
                writer.Write(hasValue);
                if (!hasValue)
                    return;
            }
            if (type == typeof(string))
            {
                BootstrapPayloadWireCodec
                    .WriteString(
                        writer,
                        (string)value);
                return;
            }
            if (type.IsEnum)
            {
                WritePrimitive(
                    writer,
                    Enum.GetUnderlyingType(type),
                    Convert.ChangeType(
                        value,
                        Enum.GetUnderlyingType(
                            type)));
                return;
            }
            if (TryWritePrimitive(
                    writer,
                    type,
                    value))
                return;
            if (type.IsArray)
            {
                Array array = (Array)value;
                if (array.Length >
                    MaximumCollectionCount)
                    throw new DeterministicSimulationException(
                        "Snapshot array exceeds its limit.");
                writer.Write(array.Length);
                Type elementType =
                    type.GetElementType();
                for (int i = 0;
                     i < array.Length;
                     i++)
                    WriteValue(
                        writer,
                        elementType,
                        array.GetValue(i),
                        depth + 1);
                return;
            }
            if (IsList(type, out Type itemType))
            {
                IList list = (IList)value;
                if (list.Count >
                    MaximumCollectionCount)
                    throw new DeterministicSimulationException(
                        "Snapshot list exceeds its limit.");
                writer.Write(list.Count);
                for (int i = 0;
                     i < list.Count;
                     i++)
                    WriteValue(
                        writer,
                        itemType,
                        list[i],
                        depth + 1);
                return;
            }
            if (typeof(UnityEngine.Object)
                .IsAssignableFrom(type))
                throw new DeterministicSimulationException(
                    $"Snapshot wire data cannot contain Unity object {type.FullName}.");

            FieldInfo[] fields =
                GetFields(type);
            writer.Write(fields.Length);
            for (int i = 0;
                 i < fields.Length;
                 i++)
            {
                BootstrapPayloadWireCodec
                    .WriteString(
                        writer,
                        fields[i].Name);
                WriteValue(
                    writer,
                    fields[i].FieldType,
                    fields[i].GetValue(value),
                    depth + 1);
            }
        }

        public static object ReadValue(
            BinaryReader reader,
            Type type,
            int depth)
        {
            RequireDepth(depth);
            Type nullableType =
                Nullable.GetUnderlyingType(type);
            if (nullableType != null)
            {
                if (!reader.ReadBoolean())
                    return Activator.CreateInstance(
                        type);
                object inner =
                    ReadValue(
                        reader,
                        nullableType,
                        depth + 1);
                return Activator.CreateInstance(
                    type,
                    inner);
            }
            if (!type.IsValueType &&
                !reader.ReadBoolean())
                return null;
            if (type == typeof(string))
                return BootstrapPayloadWireCodec
                    .ReadString(reader);
            if (type.IsEnum)
                return Enum.ToObject(
                    type,
                    ReadPrimitive(
                        reader,
                        Enum.GetUnderlyingType(type)));
            if (IsPrimitive(type))
                return ReadPrimitive(
                    reader,
                    type);
            if (type.IsArray)
            {
                int count =
                    BootstrapPayloadWireCodec
                        .ReadCount(
                            reader,
                            MaximumCollectionCount,
                            "snapshot array");
                Type elementType =
                    type.GetElementType();
                Array array =
                    Array.CreateInstance(
                        elementType,
                        count);
                for (int i = 0;
                     i < count;
                     i++)
                    array.SetValue(
                        ReadValue(
                            reader,
                            elementType,
                            depth + 1),
                        i);
                return array;
            }
            if (IsList(type, out Type itemType))
            {
                int count =
                    BootstrapPayloadWireCodec
                        .ReadCount(
                            reader,
                            MaximumCollectionCount,
                            "snapshot list");
                var list = (IList)
                    Activator.CreateInstance(type);
                for (int i = 0;
                     i < count;
                     i++)
                    list.Add(
                        ReadValue(
                            reader,
                            itemType,
                            depth + 1));
                return list;
            }
            if (typeof(UnityEngine.Object)
                .IsAssignableFrom(type))
                throw new DeterministicSimulationException(
                    $"Snapshot wire data cannot contain Unity object {type.FullName}.");

            object value =
                Activator.CreateInstance(type);
            FieldInfo[] fields =
                GetFields(type);
            int fieldCount =
                BootstrapPayloadWireCodec
                    .ReadCount(
                        reader,
                        fields.Length,
                        "snapshot field");
            if (fieldCount != fields.Length)
                throw new DeterministicSimulationException(
                    $"Snapshot field count mismatch for {type.FullName}.");
            for (int i = 0;
                 i < fields.Length;
                 i++)
            {
                string name =
                    BootstrapPayloadWireCodec
                        .ReadString(reader);
                if (!string.Equals(
                        name,
                        fields[i].Name,
                        StringComparison.Ordinal))
                    throw new DeterministicSimulationException(
                        $"Snapshot field mismatch for {type.FullName}.");
                fields[i].SetValue(
                    value,
                    ReadValue(
                        reader,
                        fields[i].FieldType,
                        depth + 1));
            }
            return value;
        }

        private static bool TryWritePrimitive(
            BinaryWriter writer,
            Type type,
            object value)
        {
            if (!IsPrimitive(type))
                return false;
            WritePrimitive(writer, type, value);
            return true;
        }

        private static bool IsPrimitive(
            Type type) =>
            type == typeof(bool) ||
            type == typeof(byte) ||
            type == typeof(sbyte) ||
            type == typeof(short) ||
            type == typeof(ushort) ||
            type == typeof(int) ||
            type == typeof(uint) ||
            type == typeof(long) ||
            type == typeof(ulong) ||
            type == typeof(char);

        private static void WritePrimitive(
            BinaryWriter writer,
            Type type,
            object value)
        {
            if (type == typeof(bool))
                writer.Write((bool)value);
            else if (type == typeof(byte))
                writer.Write((byte)value);
            else if (type == typeof(sbyte))
                writer.Write((sbyte)value);
            else if (type == typeof(short))
                writer.Write((short)value);
            else if (type == typeof(ushort))
                writer.Write((ushort)value);
            else if (type == typeof(int))
                writer.Write((int)value);
            else if (type == typeof(uint))
                writer.Write((uint)value);
            else if (type == typeof(long))
                writer.Write((long)value);
            else if (type == typeof(ulong))
                writer.Write((ulong)value);
            else if (type == typeof(char))
                writer.Write((char)value);
            else
                throw new DeterministicSimulationException(
                    $"Unsupported snapshot primitive {type.FullName}.");
        }

        private static object ReadPrimitive(
            BinaryReader reader,
            Type type)
        {
            if (type == typeof(bool))
                return reader.ReadBoolean();
            if (type == typeof(byte))
                return reader.ReadByte();
            if (type == typeof(sbyte))
                return reader.ReadSByte();
            if (type == typeof(short))
                return reader.ReadInt16();
            if (type == typeof(ushort))
                return reader.ReadUInt16();
            if (type == typeof(int))
                return reader.ReadInt32();
            if (type == typeof(uint))
                return reader.ReadUInt32();
            if (type == typeof(long))
                return reader.ReadInt64();
            if (type == typeof(ulong))
                return reader.ReadUInt64();
            if (type == typeof(char))
                return reader.ReadChar();
            throw new DeterministicSimulationException(
                $"Unsupported snapshot primitive {type.FullName}.");
        }

        private static bool IsList(
            Type type,
            out Type itemType)
        {
            if (type.IsGenericType &&
                type.GetGenericTypeDefinition() ==
                typeof(List<>))
            {
                itemType =
                    type.GetGenericArguments()[0];
                return true;
            }
            itemType = null;
            return false;
        }

        private static FieldInfo[] GetFields(
            Type type)
        {
            if (FieldCache.TryGetValue(
                    type,
                    out FieldInfo[] cached))
                return cached;
            var fields = new List<FieldInfo>();
            for (Type current = type;
                 current != null &&
                 current != typeof(object);
                 current = current.BaseType)
            {
                FieldInfo[] declared =
                    current.GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);
                for (int i = 0;
                     i < declared.Length;
                     i++)
                    if (!declared[i].IsNotSerialized)
                        fields.Add(declared[i]);
            }
            fields.Sort(
                (left, right) =>
                {
                    int declaring =
                        string.CompareOrdinal(
                            left.DeclaringType
                                ?.FullName,
                            right.DeclaringType
                                ?.FullName);
                    return declaring != 0
                        ? declaring
                        : string.CompareOrdinal(
                            left.Name,
                            right.Name);
                });
            cached = fields.ToArray();
            FieldCache.Add(type, cached);
            return cached;
        }

        private static void RequireDepth(
            int depth)
        {
            if (depth > MaximumDepth)
                throw new DeterministicSimulationException(
                    "Snapshot wire object graph exceeds its depth limit.");
        }
    }
}
