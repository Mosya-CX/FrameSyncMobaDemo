using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public class EquipmentHandler : UnitBaseHandler
{
    public ushort EquipmentLimitCount = 6;

    /// <summary>
    /// 每个格子的装备实例
    /// </summary>
    public EquipmentItemRuntime[] EquippedItems;

    /// <summary>
    /// 同种装备共享一个效果组
    /// </summary>
    private readonly Dictionary<EquipmentData, EquipmentEffectGroupRuntime> effectGroupTable = new();

    protected override void Awake()
    {
        base.Awake();
        EquippedItems = new EquipmentItemRuntime[EquipmentLimitCount];
    }

    public override void Tick(fp deltaTime)
    {
        uint currentTick = UnitManager.Instance.CurrentTick;

        foreach (var groupRuntime in effectGroupTable.Values)
            groupRuntime.Tick(deltaTime, currentTick);
    }

    public bool TryAddEquipment(EquipmentData data)
    {
        if (data == null)
            return false;

        int slot = FindEmptySlot();
        if (slot < 0)
            return false;

        var itemRuntime = new EquipmentItemRuntime(data, this);

        // 先加基础属性（每件装备都要加）
        ApplyStats(itemRuntime);

        // 再接入共享效果组
        if (!effectGroupTable.TryGetValue(data, out var groupRuntime))
        {
            groupRuntime = new EquipmentEffectGroupRuntime(data, this);
            groupRuntime.ItemCount = 1;
            groupRuntime.OnCreate();
            effectGroupTable.Add(data, groupRuntime);
        }
        else
        {
            groupRuntime.ItemCount++;
        }

        itemRuntime.EffectGroupRuntime = groupRuntime;
        EquippedItems[slot] = itemRuntime;
        return true;
    }

    public bool TryRemoveEquipment(int equipmentIndex)
    {
        if (equipmentIndex < 0 || equipmentIndex >= EquippedItems.Length)
            return false;

        var itemRuntime = EquippedItems[equipmentIndex];
        if (itemRuntime == null)
            return false;

        // 先移除这件装备自身的基础属性
        for (int i = 0; i < itemRuntime.StatModifierHandles.Count; i++)
            owner.Stats.RemoveModifierFromAllStats(itemRuntime.StatModifierHandles[i]);

        itemRuntime.StatModifierHandles.Clear();

        // 再处理共享效果组
        if (itemRuntime.EffectGroupRuntime != null)
        {
            itemRuntime.EffectGroupRuntime.ItemCount--;

            if (itemRuntime.EffectGroupRuntime.ItemCount <= 0)
            {
                itemRuntime.EffectGroupRuntime.OnRemove();
                effectGroupTable.Remove(itemRuntime.Data);
            }
        }

        EquippedItems[equipmentIndex] = null;
        return true;
    }

    public bool TryUseActiveEffect(int equipmentIndex, fp3? targetPos = null, UnitUID? targetId = null)
    {
        if (equipmentIndex < 0 || equipmentIndex >= EquippedItems.Length)
            return false;

        var itemRuntime = EquippedItems[equipmentIndex];
        if (itemRuntime == null)
            return false;

        var groupRuntime = itemRuntime.EffectGroupRuntime;
        if (groupRuntime == null || groupRuntime.ActiveRuntime == null)
            return false;

        UnitCore targetUnit = null;
        if (targetId.HasValue)
            UnitManager.Instance.Spawns.TryGetValue(targetId.Value, out targetUnit);

        var useContext = new EquipmentUseContext(targetPos, targetUnit);
        return groupRuntime.ActiveRuntime.TryUse(useContext);
    }

    private int FindEmptySlot()
    {
        for (int i = 0; i < EquippedItems.Length; i++)
        {
            if (EquippedItems[i] == null)
                return i;
        }

        return -1;
    }

    private void ApplyStats(EquipmentItemRuntime itemRuntime)
    {
        if (itemRuntime.Data.Stats == null)
            return;

        for (int i = 0; i < itemRuntime.Data.Stats.Length; i++)
        {
            var stat = itemRuntime.Data.Stats[i];
            var handle = ModifierHandleGenerator.Create();
            var modifier = new StatModifier(handle, stat.Mode, stat.Value);
            owner.Stats.AddModifier(stat.Type, modifier);
            itemRuntime.StatModifierHandles.Add(handle);
        }
    }

    public override object CaptureState()
    {
        return null;
    }

    public override void RestoreState(object state)
    {
    }
}