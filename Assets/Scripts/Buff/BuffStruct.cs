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

    private List<(UnitStatType statType, ModifierHandle handle)> modifierHandles;// 修饰符句柄列表

    private List<Action> undoActions;// 自定义撤销动作列表

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

    /// <summary>
    /// 记录由该Buff添加的数值修饰符句柄，用于后续统一移除
    /// </summary>
    public void AddModifierHandle(UnitStatType statType, ModifierHandle handle)
    {
        modifierHandles.Add((statType, handle));
    }

    /// <summary>
    /// 注册一个自定义撤销动作
    /// </summary>
    public void RegisterUndoAction(Action undoAction)
    {
        if (undoAction != null)
            undoActions.Add(undoAction);
    }

    /// <summary>
    /// 移除所有属性修饰符，并执行所有自定义撤销动作
    /// </summary>
    public void UndoEffects()
    {
        //移除所有通过修饰符系统添加的属性修改
        foreach (var (statType, handle) in modifierHandles)
        {
            target.Stats.RemoveModifier(statType, handle);
        }
        modifierHandles.Clear();

        //执行所有自定义撤销动作（如关闭特效、还原材质等）
        foreach (var undo in undoActions)
        {
            undo?.Invoke();
        }
        undoActions.Clear();
    }

}

public class BuffCallbackContext
{
    public BuffInfo Buff { get; set; }
    public BuffHandler Handler { get; set; }
    public IReadOnlyDictionary<string, object> EventData { get; set; }

    public T GetEventData<T>(string key) =>
        EventData != null && EventData.TryGetValue(key, out var val) ? (T)val : default;
}