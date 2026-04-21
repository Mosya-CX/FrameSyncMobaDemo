using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

[System.Serializable]
public class BuffInfo
{
    public BuffData buffData;
    public UnitCore source;
    public UnitCore target;

    public fp durationTimer;
    public fp tickTimer;
    public int curStack;

    public Dictionary<string, object> blackBoard;

    private List<(UnitStatType statType, ModifierHandle handle)> modifierHandles;
    private List<Action> undoActions;

    public BuffInfo(BuffData buffData, UnitCore source, UnitCore target)
    {
        this.buffData = buffData;
        this.source = source;
        this.target = target;

        durationTimer = (fp)buffData.Duration;
        tickTimer = fp.zero;
        curStack = 1;

        blackBoard = new Dictionary<string, object>();
        modifierHandles = new List<(UnitStatType, ModifierHandle)>();
        undoActions = new List<Action>();
    }

    public bool IsExpired => !buffData.isForever && durationTimer <= fp.zero;

    public void AddModifierHandle(UnitStatType statType, ModifierHandle handle)
    {
        modifierHandles.Add((statType, handle));
    }

    public void RegisterUndoAction(Action undoAction)
    {
        if (undoAction != null)
            undoActions.Add(undoAction);
    }

    public void UndoEffects()
    {
        foreach (var (statType, handle) in modifierHandles)
            target.Stats.RemoveModifier(statType, handle);

        modifierHandles.Clear();

        foreach (var undo in undoActions)
            undo?.Invoke();

        undoActions.Clear();
    }

    public void ReapplyPersistentEffects(BuffHandler handler)
    {
        if (buffData?.OnCreate == null)
            return;

        var ctx = new BuffCallbackContext
        {
            Buff = this,
            Handler = handler,
        };
        buffData.OnCreate.Apply(ctx);
    }
}

[Serializable]
public struct BuffBlackboardEntry
{
    public string Key;
    public BuffValueType ValueType;

    public int IntValue;
    public fp FpValue;
    public bool BoolValue;
    public string StringValue;
}

public enum BuffValueType : byte
{
    Int,
    Fp,
    Bool,
    String,
}

[Serializable]
public struct BuffInfoSnapshot
{
    public int BuffId;
    public UnitUID SourceUid;
    public fp DurationTimer;
    public fp TickTimer;
    public int CurrentStack;
    public BuffBlackboardEntry[] Blackboard;
}

public class BuffCallbackContext
{
    public BuffInfo Buff { get; set; }
    public BuffHandler Handler { get; set; }
    public IReadOnlyDictionary<string, object> EventData { get; set; }

    public T GetEventData<T>(string key) =>
        EventData != null && EventData.TryGetValue(key, out var val) ? (T)val : default;
}

public enum BuffUpdateTimeEnum : byte
{
    /// <summary>
    /// 重新刷新持续时间，并重置为 1 层。
    /// </summary>
    Replace = 0,

    /// <summary>
    /// 保持当前持续时间与层数不变。
    /// </summary>
    Keep = 1,

    /// <summary>
    /// 刷新持续时间；如果可叠层则增加层数。
    /// </summary>
    Add = 2,
}