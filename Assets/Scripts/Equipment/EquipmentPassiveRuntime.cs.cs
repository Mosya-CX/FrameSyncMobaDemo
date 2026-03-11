using Unity.Mathematics.FixedPoint;

public sealed class EquipmentPassiveRuntime
{
    public readonly EquipmentPassiveData Data;
    public readonly EquipmentEffectGroupRuntime GroupRuntime;
    public readonly EquipmentContext Context = new();

    public fp CooldownRemaining;
    public int StackCount;
    public int ChargeCount;

    public UnitCore Owner => GroupRuntime.Handler.Owner;
    public int ItemCount => GroupRuntime.ItemCount;

    public EquipmentPassiveRuntime(EquipmentPassiveData data, EquipmentEffectGroupRuntime groupRuntime)
    {
        Data = data;
        GroupRuntime = groupRuntime;
    }

    public void Bind()
    {
        Data.Effect.OnEquip(this);
    }

    public void Unbind()
    {
        Data.Effect.OnUnequip(this);
    }

    public void Tick(fp dt, uint currentTick)
    {
        if (CooldownRemaining > 0)
        {
            CooldownRemaining -= dt;
            if (CooldownRemaining < 0)
                CooldownRemaining = 0;
        }

        Data.Effect.OnTick(this, dt, currentTick);
    }

    public bool CanTrigger()
    {
        if (CooldownRemaining > 0)
            return false;

        if (Data.Conditions != null)
        {
            for (int i = 0; i < Data.Conditions.Length; i++)
            {
                if (!Data.Conditions[i].CanTriggerPassive(this))
                    return false;
            }
        }

        return true;
    }

    public bool TryTrigger()
    {
        if (!CanTrigger())
            return false;

        if (!Data.Effect.TryApply(this))
            return false;

        CooldownRemaining = (fp)Data.Cooldown;
        return true;
    }
}