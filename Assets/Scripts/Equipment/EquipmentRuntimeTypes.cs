using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public sealed class EquipmentItemRuntime
{
    public readonly EquipmentData Data;
    public readonly EquipmentHandler Handler;
    public readonly List<ModifierHandle> StatModifierHandles = new();

    /// <summary>
    /// 指向同装备类型共享的效果组
    /// </summary>
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

    /// <summary>
    /// 当前该装备在该单位身上的持有数量
    /// </summary>
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
}