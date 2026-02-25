using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class BuffHandler : UnitBaseHandler
{
    private readonly List<BuffInfo> buffs = new();
    private readonly Dictionary<int, BuffInfo> buffIndex = new();

    private Dictionary<string, object> sharedBlackBoard = new();

    public override void Tick(fp deltaTime)
    {
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            var buff = buffs[i];

            if (!buff.buffData.isForever)
            {
                buff.durationTimer -= deltaTime;
            }

            if (buff.buffData.TickTime > 0)
            {
                buff.tickTimer += deltaTime;
                if (buff.tickTimer >= (fp)buff.buffData.TickTime)
                {
                    buff.tickTimer = 0;

                    var ctx = new BuffCallbackContext { Buff = buff, Handler = this };
                    buff.buffData.OnTick?.Apply(ctx);
                }
            }

            if (buff.IsExpired)
            {
                RemoveBuff(buff.buffData.Id);
            }
        }
    }

    public void AddBuff(BuffData data, UnitCore source)
    {
        if (buffIndex.TryGetValue(data.Id, out var existing))
        {
            HandleUpdate(existing, data);
            return;
        }

        var info = new BuffInfo(data, source, core);
        buffs.Add(info);
        buffIndex[data.Id] = info;

        var createCtx = new BuffCallbackContext { Buff = info, Handler = this };
        data.OnCreate?.Apply(createCtx);
    }

    private void HandleUpdate(BuffInfo existing, BuffData data)
    {
        int oldStack = existing.curStack;

        switch (data.UpdateTimeMode)
        {
            case BuffUpdateTimeEnum.Replace:
                existing.durationTimer = (fp)data.Duration;
                existing.curStack = 1;
                break;
            case BuffUpdateTimeEnum.Keep:
                break;
            case BuffUpdateTimeEnum.Add:
                if (data.isStackable)
                {
                    existing.curStack = Mathf.Min(existing.curStack + 1, data.MaxStack);
                }
                existing.durationTimer = (fp)data.Duration;
                break;
        }

        if (existing.curStack != oldStack)
        {
            existing.UndoEffects();  // 撤销旧效果
            if (data.OnCreate != null)
            {
                var ctx = new BuffCallbackContext { Buff = existing, Handler = this };
                data.OnCreate.Apply(ctx);  // 重新应用
            }
        }
    }

    private void RemoveBuff(int buffId)
    {
        if (!buffIndex.TryGetValue(buffId, out var buff))
            return;

        // 触发移除前回调
        var removeCtx = new BuffCallbackContext { Buff = buff, Handler = this };
        buff.buffData.OnRemove?.Apply(removeCtx);

        // 统一撤销所有效果
        buff.UndoEffects();

        buffs.Remove(buff);
        buffIndex.Remove(buffId);
    }

    public void AddStatModifier(BuffInfo buff, UnitStatType type, StatModifierType modType, fp value)
    {
        var handle = ModifierHandleGenerator.Create();
        var modifier = new StatModifier(handle, modType, value);
        core.Stats.AddModifier(type, modifier);

        buff.AddModifierHandle(type, handle);  // 调用 BuffInfo 的公开方法记录句柄
    }

    #region 事件分发

    public void DispatchOnHit(Dictionary<string, object> eventData)
    {
        foreach (var buff in buffs)
        {
            var ctx = new BuffCallbackContext { Buff = buff, Handler = this, EventData = eventData };
            buff.buffData.OnHit?.Apply(ctx);
        }
    }

    public void DispatchOnHurt(Dictionary<string, object> eventData)
    {
        foreach (var buff in buffs)
        {
            var ctx = new BuffCallbackContext { Buff = buff, Handler = this, EventData = eventData };
            buff.buffData.OnHurt?.Apply(ctx);
        }
    }

    public void DispatchOnKill(Dictionary<string, object> eventData)
    {
        foreach (var buff in buffs)
        {
            var ctx = new BuffCallbackContext { Buff = buff, Handler = this, EventData = eventData };
            buff.buffData.OnKill?.Apply(ctx);
        }
    }

    public void DispatchOnDeath(Dictionary<string, object> eventData)
    {
        foreach (var buff in buffs)
        {
            var ctx = new BuffCallbackContext { Buff = buff, Handler = this, EventData = eventData };
            buff.buffData.OnDeath?.Apply(ctx);
        }
    }

    #endregion

    #region 快照和恢复
    public override object CaptureHandlerState()
    {
        throw new System.NotImplementedException();
    }

    public override void RestoreHandlerState(object state)
    {
        throw new System.NotImplementedException();
    }
    #endregion
}
