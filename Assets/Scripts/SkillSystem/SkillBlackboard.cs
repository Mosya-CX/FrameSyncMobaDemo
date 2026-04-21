using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public enum SkillBlackboardValueType : byte
{
    Int = 0,
    Fp = 1,
    Bool = 2,
    String = 3,
    UInt = 4,
    UnitUid = 5,
    Fp3 = 6,
}

public sealed class SkillBlackboard
{
    private readonly Dictionary<string, object> values = new();

    public int Count => values.Count;

    public void Clear()
    {
        values.Clear();
    }

    public void Remove(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        values.Remove(key);
    }

    public void Set(string key, int value) => SetInternal(key, value);
    public void Set(string key, fp value) => SetInternal(key, value);
    public void Set(string key, bool value) => SetInternal(key, value);
    public void Set(string key, string value) => SetInternal(key, value);
    public void Set(string key, uint value) => SetInternal(key, value);
    public void Set(string key, UnitUID value) => SetInternal(key, value);
    public void Set(string key, fp3 value) => SetInternal(key, value);

    public bool TryGet(string key, out int value) => TryGetInternal(key, out value);
    public bool TryGet(string key, out fp value) => TryGetInternal(key, out value);
    public bool TryGet(string key, out bool value) => TryGetInternal(key, out value);
    public bool TryGet(string key, out string value) => TryGetInternal(key, out value);
    public bool TryGet(string key, out uint value) => TryGetInternal(key, out value);
    public bool TryGet(string key, out UnitUID value) => TryGetInternal(key, out value);
    public bool TryGet(string key, out fp3 value) => TryGetInternal(key, out value);

    private void SetInternal<T>(string key, T value)
    {
        if (string.IsNullOrEmpty(key))
            return;

        values[key] = value;
    }

    private bool TryGetInternal<T>(string key, out T value)
    {
        if (!string.IsNullOrEmpty(key) && values.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public SkillBlackboardSnapshot CaptureSnapshot()
    {
        var entries = new SkillBlackboardEntry[values.Count];
        int idx = 0;
        foreach (var pair in values)
            entries[idx++] = SkillBlackboardEntry.From(pair.Key, pair.Value);

        return new SkillBlackboardSnapshot { Entries = entries };
    }

    public void RestoreSnapshot(SkillBlackboardSnapshot snapshot)
    {
        values.Clear();

        if (snapshot.Entries == null)
            return;

        for (int i = 0; i < snapshot.Entries.Length; i++)
        {
            var entry = snapshot.Entries[i];
            if (string.IsNullOrEmpty(entry.Key))
                continue;

            switch (entry.ValueType)
            {
                case SkillBlackboardValueType.Int: values[entry.Key] = entry.IntValue; break;
                case SkillBlackboardValueType.Fp: values[entry.Key] = entry.FpValue; break;
                case SkillBlackboardValueType.Bool: values[entry.Key] = entry.BoolValue; break;
                case SkillBlackboardValueType.String: values[entry.Key] = entry.StringValue; break;
                case SkillBlackboardValueType.UInt: values[entry.Key] = entry.UIntValue; break;
                case SkillBlackboardValueType.UnitUid: values[entry.Key] = entry.UnitUidValue; break;
                case SkillBlackboardValueType.Fp3: values[entry.Key] = entry.Fp3Value; break;
            }
        }
    }
}

[Serializable]
public struct SkillBlackboardSnapshot
{
    public SkillBlackboardEntry[] Entries;
}

[Serializable]
public struct SkillBlackboardEntry
{
    public string Key;
    public SkillBlackboardValueType ValueType;

    public int IntValue;
    public fp FpValue;
    public bool BoolValue;
    public string StringValue;
    public uint UIntValue;
    public UnitUID UnitUidValue;
    public fp3 Fp3Value;

    public static SkillBlackboardEntry From(string key, object value)
    {
        var entry = new SkillBlackboardEntry { Key = key };

        switch (value)
        {
            case int i:
                entry.ValueType = SkillBlackboardValueType.Int;
                entry.IntValue = i;
                break;
            case fp f:
                entry.ValueType = SkillBlackboardValueType.Fp;
                entry.FpValue = f;
                break;
            case bool b:
                entry.ValueType = SkillBlackboardValueType.Bool;
                entry.BoolValue = b;
                break;
            case string s:
                entry.ValueType = SkillBlackboardValueType.String;
                entry.StringValue = s;
                break;
            case uint u:
                entry.ValueType = SkillBlackboardValueType.UInt;
                entry.UIntValue = u;
                break;
            case UnitUID uid:
                entry.ValueType = SkillBlackboardValueType.UnitUid;
                entry.UnitUidValue = uid;
                break;
            case fp3 p:
                entry.ValueType = SkillBlackboardValueType.Fp3;
                entry.Fp3Value = p;
                break;
            default:
                throw new InvalidOperationException($"Unsupported blackboard value type: {value?.GetType().FullName ?? "null"}");
        }

        return entry;
    }
}
