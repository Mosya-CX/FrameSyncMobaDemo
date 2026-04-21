using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class BuffHandler : UnitBaseHandler
{
    [Serializable]
    public struct BuffHandlerSnapshot
    {
        public BuffInfoSnapshot[] Buffs;
    }

    private readonly List<BuffInfo> buffs = new();
    private readonly Dictionary<int, BuffInfo> buffIndex = new();

    private Dictionary<string, object> sharedBlackBoard = new();

    public override void Tick(fp deltaTime)
    {
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            var buff = buffs[i];

            if (!buff.buffData.isForever)
                buff.durationTimer -= deltaTime;

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
                RemoveBuff(buff.buffData.Id);
        }
    }

    public void AddBuff(BuffData data, UnitCore source)
    {
        if (data == null)
            return;

        if (buffIndex.TryGetValue(data.Id, out var existing))
        {
            HandleUpdate(existing, data);
            return;
        }

        var info = new BuffInfo(data, source, owner);
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
                existing.tickTimer = fp.zero;
                existing.curStack = 1;
                break;

            case BuffUpdateTimeEnum.Keep:
                break;

            case BuffUpdateTimeEnum.Add:
                if (data.isStackable)
                    existing.curStack = Mathf.Min(existing.curStack + 1, data.MaxStack);

                existing.durationTimer = (fp)data.Duration;
                existing.tickTimer = fp.zero;
                break;
        }

        if (existing.curStack != oldStack)
        {
            existing.UndoEffects();
            var ctx = new BuffCallbackContext { Buff = existing, Handler = this };
            data.OnCreate?.Apply(ctx);
        }
    }

    private void RemoveBuff(int buffId)
    {
        if (!buffIndex.TryGetValue(buffId, out var buff))
            return;

        var removeCtx = new BuffCallbackContext { Buff = buff, Handler = this };
        buff.buffData.OnRemove?.Apply(removeCtx);

        buff.UndoEffects();

        buffs.Remove(buff);
        buffIndex.Remove(buffId);
    }

    public void AddStatModifier(BuffInfo buff, UnitStatType type, StatModifierType modType, fp value)
    {
        var handle = ModifierHandleGenerator.Create();
        var modifier = new StatModifier(handle, modType, value);
        owner.Stats.AddModifier(type, modifier);
        buff.AddModifierHandle(type, handle);
    }

    private void ClearAllBuffsInternal(bool invokeRemove)
    {
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            var buff = buffs[i];

            if (invokeRemove)
            {
                var ctx = new BuffCallbackContext { Buff = buff, Handler = this };
                buff.buffData.OnRemove?.Apply(ctx);
            }

            buff.UndoEffects();
        }

        buffs.Clear();
        buffIndex.Clear();
        sharedBlackBoard.Clear();
    }

    private static BuffBlackboardEntry[] CaptureBlackboard(Dictionary<string, object> dict)
    {
        if (dict == null || dict.Count == 0)
            return Array.Empty<BuffBlackboardEntry>();

        var result = new List<BuffBlackboardEntry>(dict.Count);

        foreach (var kv in dict)
        {
            if (kv.Value is int i)
            {
                result.Add(new BuffBlackboardEntry { Key = kv.Key, ValueType = BuffValueType.Int, IntValue = i });
            }
            else if (kv.Value is fp fpv)
            {
                result.Add(new BuffBlackboardEntry { Key = kv.Key, ValueType = BuffValueType.Fp, FpValue = fpv });
            }
            else if (kv.Value is bool b)
            {
                result.Add(new BuffBlackboardEntry { Key = kv.Key, ValueType = BuffValueType.Bool, BoolValue = b });
            }
            else if (kv.Value is string s)
            {
                result.Add(new BuffBlackboardEntry { Key = kv.Key, ValueType = BuffValueType.String, StringValue = s });
            }
        }

        return result.ToArray();
    }

    private static Dictionary<string, object> RestoreBlackboard(BuffBlackboardEntry[] entries)
    {
        var dict = new Dictionary<string, object>();
        if (entries == null)
            return dict;

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            switch (e.ValueType)
            {
                case BuffValueType.Int: dict[e.Key] = e.IntValue; break;
                case BuffValueType.Fp: dict[e.Key] = e.FpValue; break;
                case BuffValueType.Bool: dict[e.Key] = e.BoolValue; break;
                case BuffValueType.String: dict[e.Key] = e.StringValue; break;
            }
        }

        return dict;
    }

    public bool TryRemoveBuff(int buffId)
    {
        if (!buffIndex.ContainsKey(buffId)) return false;
        RemoveBuff(buffId);
        return true;
    }

    public bool TryGetBuff(int buffId, out BuffInfo info) => buffIndex.TryGetValue(buffId, out info);

    public bool TryExtendBuff(int buffId, fp extraSeconds)
    {
        if (!buffIndex.TryGetValue(buffId, out var info)) return false;
        if (info.buffData.isForever) return false;
        info.durationTimer += extraSeconds;
        return true;
    }

    public override object CaptureState()
    {
        var snaps = new BuffInfoSnapshot[buffs.Count];

        for (int i = 0; i < buffs.Count; i++)
        {
            var buff = buffs[i];
            snaps[i] = new BuffInfoSnapshot
            {
                BuffId = buff.buffData != null ? buff.buffData.Id : 0,
                SourceUid = buff.source != null ? buff.source.UnitID : default,
                DurationTimer = buff.durationTimer,
                TickTimer = buff.tickTimer,
                CurrentStack = buff.curStack,
                Blackboard = CaptureBlackboard(buff.blackBoard),
            };
        }

        return new BuffHandlerSnapshot
        {
            Buffs = snaps,
        };
    }

    public override void RestoreState(object state)
    {
        ClearAllBuffsInternal(false);

        if (state is not BuffHandlerSnapshot snap || snap.Buffs == null)
            return;

        for (int i = 0; i < snap.Buffs.Length; i++)
        {
            var item = snap.Buffs[i];
            if (!GameManager.Instance.GlobalDatabase.BuffDatabase.TryGetValue(item.BuffId, out var data))
                continue;

            UnitCore source = null;
            if (!item.SourceUid.Equals(default))
                UnitManager.Instance.Spawns.TryGetValue(item.SourceUid, out source);

            var info = new BuffInfo(data, source, owner)
            {
                durationTimer = item.DurationTimer,
                tickTimer = item.TickTimer,
                curStack = item.CurrentStack,
                blackBoard = RestoreBlackboard(item.Blackboard),
            };

            buffs.Add(info);
            buffIndex[data.Id] = info;

            info.ReapplyPersistentEffects(this);
        }
    }
}
