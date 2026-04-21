using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public class EquipmentHandler : UnitBaseHandler
{
    [Serializable]
    public struct EquipmentHandlerSnapshot
    {
        public EquipmentItemRuntimeSnapshot[] Slots;
        public EquipmentEffectGroupRuntimeSnapshot[] Groups;
    }

    public ushort EquipmentLimitCount = 6;
    public EquipmentItemRuntime[] EquippedItems;

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

        return TryAddEquipmentToSlot(data, slot);
    }

    private bool TryAddEquipmentToSlot(EquipmentData data, int slot)
    {
        if (data == null || slot < 0 || slot >= EquippedItems.Length || EquippedItems[slot] != null)
            return false;

        var itemRuntime = new EquipmentItemRuntime(data, this);

        ApplyStats(itemRuntime);

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

        for (int i = 0; i < itemRuntime.StatModifierHandles.Count; i++)
            owner.Stats.RemoveModifierFromAllStats(itemRuntime.StatModifierHandles[i]);

        itemRuntime.StatModifierHandles.Clear();

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

    private void ClearAllInternal()
    {
        for (int i = 0; i < EquippedItems.Length; i++)
        {
            if (EquippedItems[i] != null)
                TryRemoveEquipment(i);
        }

        effectGroupTable.Clear();
    }

    public override object CaptureState()
    {
        var slotSnaps = new EquipmentItemRuntimeSnapshot[EquippedItems.Length];
        for (int i = 0; i < EquippedItems.Length; i++)
        {
            var item = EquippedItems[i];
            slotSnaps[i] = new EquipmentItemRuntimeSnapshot
            {
                Occupied = item != null,
                EquipmentId = item != null && item.Data != null ? item.Data.Id : 0,
            };
        }

        var groups = new List<EquipmentEffectGroupRuntimeSnapshot>(effectGroupTable.Count);
        foreach (var kv in effectGroupTable)
            groups.Add(kv.Value.CaptureSnapshot());

        return new EquipmentHandlerSnapshot
        {
            Slots = slotSnaps,
            Groups = groups.ToArray(),
        };
    }

    public override void RestoreState(object state)
    {
        ClearAllInternal();

        if (state is not EquipmentHandlerSnapshot snap)
            return;

        if (snap.Slots != null)
        {
            for (int i = 0; i < snap.Slots.Length && i < EquippedItems.Length; i++)
            {
                if (!snap.Slots[i].Occupied)
                    continue;

                if (GameManager.Instance.GlobalDatabase.EquipmentDatabase.TryGetValue(snap.Slots[i].EquipmentId, out var data))
                    TryAddEquipmentToSlot(data, i);
            }
        }

        if (snap.Groups != null)
        {
            for (int i = 0; i < snap.Groups.Length; i++)
            {
                var groupSnap = snap.Groups[i];
                if (!GameManager.Instance.GlobalDatabase.EquipmentDatabase.TryGetValue(groupSnap.EquipmentId, out var data))
                    continue;

                if (effectGroupTable.TryGetValue(data, out var runtime))
                    runtime.RestoreSnapshot(groupSnap);
            }
        }
    }
}