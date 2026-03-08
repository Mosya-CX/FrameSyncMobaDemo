using Unity.Mathematics.FixedPoint;
using UnityEngine;

public abstract class EquipmentBaseEffect : ScriptableObject
{
    // 效果的基础冷却时间
    public float BaseCooldown;

    // 触发时机
    public ApplyTiming Timing;

    // 装备时调用：注册事件、初始化状态
    public abstract void OnEquip(EquipmentEffectRuntime runtimeData);

    // 卸下时调用：注销事件、清理状态
    public abstract void OnUnequip(EquipmentEffectRuntime runtimeData);

    // 每帧调用
    public abstract void OnTick(EquipmentEffectRuntime runtimeData, fp dt);

    // 效果触发
    public abstract bool Apply(EquipmentEffectRuntime runtimeData);

    public enum ApplyTiming
    {
        None,
        OnDamageDealt,
        OnDamageTaken,
        OnKill,
        OnDeath,
    }
}
