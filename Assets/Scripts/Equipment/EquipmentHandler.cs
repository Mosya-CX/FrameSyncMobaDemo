using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics.FixedPoint;

public class EquipmentHandler : UnitBaseHandler
{
    public ushort EquipmentLimitCount = 6;
    public EquipmentInfo[] EquippedItems;
    private Dictionary<EquipmentData, EquipmentEffectRuntime> EquipmentEffectTable = new();

    protected override void Awake()
    {
        base.Awake();   
        EquippedItems = new EquipmentInfo[EquipmentLimitCount];
    }

    public override void Tick(fp deltaTime)
    {
        foreach (var effectRuntime in EquipmentEffectTable.Values)
            effectRuntime.Tick(deltaTime);
    }

    public bool TryAddEquipment(EquipmentData data)
    {
        if (data == null || EquippedItems.Length >= EquipmentLimitCount)
            return false;

        var info = new EquipmentInfo(data, this);

        // 应用基础属性
        ApplyStats(info);

        if (!EquipmentEffectTable.TryGetValue(data, out var effectRuntime))
        {
            effectRuntime = new EquipmentEffectRuntime(data, this);
            effectRuntime.OnCreate();
        }
        effectRuntime.referenceCount++;
        info.EffectRuntime = effectRuntime;

        for (int i = 0; i < EquippedItems.Length; i++)
        {
            if (EquippedItems[i] == null)
            {
                EquippedItems[i] = info;
                break;
            }
        }
        
        return true;
    }

    public bool TryRemoveEquipment(int equipmentIndex)
    {
        var info = EquippedItems[equipmentIndex];
        if (info == null) return false;

        if (EquipmentEffectTable.TryGetValue(info.data, out var effectRuntime))
        {
            effectRuntime.referenceCount--;
            if (effectRuntime.referenceCount <= 0)
            {
                effectRuntime.OnRemove();
                EquipmentEffectTable.Remove(info.data);
            }
        }

        // 移除属性修饰器
        foreach (var handle in info.statModifierHandlers)
            owner.Stats.RemoveModifierFromAllStats(handle);
        info.statModifierHandlers.Clear();

        EquippedItems[equipmentIndex] = null;
        return true;
    }

    public bool TryUseActiveEffect(int equipmentIndex, fp3? targetPos = null, UnitUID? targetId = null)
    {
        var info = EquippedItems[equipmentIndex];
        if (info == null ||  info.data.ActiveEffect == null) 
            return false;

        if (!EquipmentEffectTable.TryGetValue(info.data, out var effectRuntime) || effectRuntime == null)
            return false;

        if (targetPos.HasValue)
            effectRuntime.context.Set("TargetPosition", targetPos.Value);
        if (targetId.HasValue && UnitManager.Instance.Spawns.TryGetValue(targetId.Value, out var targetUnit))
            effectRuntime.context.Set("TargetUnit", targetUnit);

        var isApplied = effectRuntime.data.ActiveEffect.Apply(effectRuntime);
        effectRuntime.context.Remove("TargetPosition");
        effectRuntime.context.Remove("TargetUnit");
        return isApplied;
    }

    private void ApplyStats(EquipmentInfo info)
    {
        if (info.data.Stats == null) return;

        foreach (var stat in info.data.Stats)
        {
            var handle = ModifierHandleGenerator.Create();
            var modifier = new StatModifier(handle, stat.Mode, stat.Value);
            owner.Stats.AddModifier(stat.Type, modifier);
            info.statModifierHandlers.Add(handle);
        }
    }

    #region 伤害回调
    protected override void OnDamageDealt(in DamageInfo info)
    {
        foreach (var effectRuntime in EquipmentEffectTable.Values)
        {
            effectRuntime.context.Set("DamageInfo", info);

            foreach (var passiveEffect in effectRuntime.data.PassiveEffects)
                if (passiveEffect.Timing == EquipmentBaseEffect.ApplyTiming.OnDamageDealt)
                    effectRuntime.ApplyEffect(passiveEffect);

            effectRuntime.context.Remove("DamageInfo");
        }
    }

    protected override void OnDamageTaken(in DamageInfo info)
    {
        foreach (var effectRuntime in EquipmentEffectTable.Values)
        {
            effectRuntime.context.Set("DamageInfo", info);

            foreach (var passiveEffect in effectRuntime.data.PassiveEffects)
                if (passiveEffect.Timing == EquipmentBaseEffect.ApplyTiming.OnDamageTaken)
                    effectRuntime.ApplyEffect(passiveEffect);

            effectRuntime.context.Remove("DamageInfo");
        }
    }

    protected override void OnKill(in DamageInfo info)
    {
        foreach (var effectRuntime in EquipmentEffectTable.Values)
        {
            effectRuntime.context.Set("DamageInfo", info);

            foreach (var passiveEffect in effectRuntime.data.PassiveEffects)
                if (passiveEffect.Timing == EquipmentBaseEffect.ApplyTiming.OnKill)
                    effectRuntime.ApplyEffect(passiveEffect);

            effectRuntime.context.Remove("DamageInfo");
        }
    }

    protected override void OnDeath(in DamageInfo info)
    {
        foreach (var effectRuntime in EquipmentEffectTable.Values)
        {
            effectRuntime.context.Set("DamageInfo", info);

            foreach (var passiveEffect in effectRuntime.data.PassiveEffects)
                if (passiveEffect.Timing == EquipmentBaseEffect.ApplyTiming.OnDeath)
                    effectRuntime.ApplyEffect(passiveEffect);

            effectRuntime.context.Remove("DamageInfo");
        }
    }
    #endregion

    #region 快照和回滚
    public override object CaptureState()
    {
        return null;
    }

    public override void RestoreState(object state)
    {

    }
    #endregion
}
