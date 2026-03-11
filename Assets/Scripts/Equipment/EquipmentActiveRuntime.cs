using Unity.Mathematics.FixedPoint;

public sealed class EquipmentActiveRuntime
{
    public readonly EquipmentActiveData Data;
    public readonly EquipmentEffectGroupRuntime GroupRuntime;
    public readonly EquipmentContext Context = new();

    public fp CooldownRemaining;

    public UnitCore Owner => GroupRuntime.Handler.Owner;
    public int ItemCount => GroupRuntime.ItemCount;

    public EquipmentActiveRuntime(EquipmentActiveData data, EquipmentEffectGroupRuntime groupRuntime)
    {
        Data = data;
        GroupRuntime = groupRuntime;
    }

    public void Tick(fp dt, uint currentTick)
    {
        if (CooldownRemaining > 0)
        {
            CooldownRemaining -= dt;
            if (CooldownRemaining < 0)
                CooldownRemaining = 0;
        }
    }

    public bool CanUse(EquipmentUseContext useContext)
    {
        if (CooldownRemaining > 0)
            return false;

        if (Data.Conditions != null)
        {
            for (int i = 0; i < Data.Conditions.Length; i++)
            {
                if (!Data.Conditions[i].CanUseActive(this, useContext))
                    return false;
            }
        }

        return true;
    }

    public bool TryUse(EquipmentUseContext useContext)
    {
        if (!CanUse(useContext))
            return false;

        Context.Set("UseContext", useContext);

        bool success = Data.Effect.TryApply(this, useContext);

        Context.Remove("UseContext");

        if (!success)
            return false;

        CooldownRemaining = (fp)Data.Cooldown;
        return true;
    }
}