using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

[Serializable]
public struct EquipmentPassiveRuntimeSnapshot
{
    public int PassiveId;
    public fp CooldownRemaining;
    public int StackCount;
    public int ChargeCount;
}

[Serializable]
public struct EquipmentActiveRuntimeSnapshot
{
    public int ActiveId;
    public fp CooldownRemaining;
}

[Serializable]
public struct EquipmentEffectGroupRuntimeSnapshot
{
    public int EquipmentId;
    public int ItemCount;
    public EquipmentPassiveRuntimeSnapshot[] Passives;
    public bool HasActive;
    public EquipmentActiveRuntimeSnapshot Active;
}

[Serializable]
public struct EquipmentItemRuntimeSnapshot
{
    public bool Occupied;
    public int EquipmentId;
}

public sealed class EquipmentItemRuntime
{
    public readonly EquipmentData Data;
    public readonly EquipmentHandler Handler;
    public readonly List<ModifierHandle> StatModifierHandles = new();

    public EquipmentEffectGroupRuntime EffectGroupRuntime;

    public EquipmentItemRuntime(EquipmentData data, EquipmentHandler handler)
    {
        Data = data;
        Handler = handler;
    }
}

public sealed class EquipmentEffectGroupRuntime
{
    public readonly EquipmentData Data;
    public readonly EquipmentHandler Handler;
    public readonly EquipmentContext Context = new();

    public readonly List<EquipmentPassiveRuntime> PassiveRuntimes = new();
    public EquipmentActiveRuntime ActiveRuntime;

    public int ItemCount;

    public EquipmentEffectGroupRuntime(EquipmentData data, EquipmentHandler handler)
    {
        Data = data;
        Handler = handler;
    }

    public void OnCreate()
    {
        if (Data.Passives != null)
        {
            for (int i = 0; i < Data.Passives.Length; i++)
            {
                var passive = Data.Passives[i];
                if (passive == null || passive.Effect == null)
                    continue;

                var runtime = new EquipmentPassiveRuntime(passive, this);
                runtime.Bind();
                PassiveRuntimes.Add(runtime);
            }
        }

        if (Data.Active != null && Data.Active.Effect != null)
            ActiveRuntime = new EquipmentActiveRuntime(Data.Active, this);
    }

    public void Tick(fp dt, uint currentTick)
    {
        for (int i = 0; i < PassiveRuntimes.Count; i++)
            PassiveRuntimes[i].Tick(dt, currentTick);

        ActiveRuntime?.Tick(dt, currentTick);
    }

    public void OnRemove()
    {
        for (int i = 0; i < PassiveRuntimes.Count; i++)
            PassiveRuntimes[i].Unbind();

        PassiveRuntimes.Clear();
        ActiveRuntime = null;
        Context.Clear();
    }

    public EquipmentEffectGroupRuntimeSnapshot CaptureSnapshot()
    {
        var passives = new EquipmentPassiveRuntimeSnapshot[PassiveRuntimes.Count];
        for (int i = 0; i < PassiveRuntimes.Count; i++)
        {
            var p = PassiveRuntimes[i];
            passives[i] = new EquipmentPassiveRuntimeSnapshot
            {
                PassiveId = p.Data != null ? p.Data.Id : 0,
                CooldownRemaining = p.CooldownRemaining,
                StackCount = p.StackCount,
                ChargeCount = p.ChargeCount,
            };
        }

        return new EquipmentEffectGroupRuntimeSnapshot
        {
            EquipmentId = Data != null ? Data.Id : 0,
            ItemCount = ItemCount,
            Passives = passives,
            HasActive = ActiveRuntime != null,
            Active = ActiveRuntime != null
                ? new EquipmentActiveRuntimeSnapshot
                {
                    ActiveId = ActiveRuntime.Data != null ? ActiveRuntime.Data.Id : 0,
                    CooldownRemaining = ActiveRuntime.CooldownRemaining,
                }
                : default
        };
    }

    public void RestoreSnapshot(EquipmentEffectGroupRuntimeSnapshot snap)
    {
        ItemCount = snap.ItemCount;

        for (int i = 0; i < snap.Passives.Length && i < PassiveRuntimes.Count; i++)
        {
            PassiveRuntimes[i].CooldownRemaining = snap.Passives[i].CooldownRemaining;
            PassiveRuntimes[i].StackCount = snap.Passives[i].StackCount;
            PassiveRuntimes[i].ChargeCount = snap.Passives[i].ChargeCount;
        }

        if (ActiveRuntime != null && snap.HasActive)
            ActiveRuntime.CooldownRemaining = snap.Active.CooldownRemaining;
    }
}