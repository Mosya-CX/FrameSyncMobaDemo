using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public sealed class AbilityRuntime
{
    public readonly AbilityData Data;
    public readonly AbilityHandler Handler;

    public ushort Level { get; private set; }
    public fp CooldownRemaining;
    public fp CooldownMultiplier = 1;

    public AbilityContext Blackboard = new();

    public AbilityLevelData CurrentLevelData => Level > 0 ? Data.Levels[Level - 1] : null;
    public fp CurrentCooldownDuration => CurrentLevelData != null ? ((fp)CurrentLevelData.Cooldown) * CooldownMultiplier : 0;

    public AbilityRuntime(AbilityData data, AbilityHandler handler)
    {
        Data = data;
        Handler = handler;
    }

    public void Tick(fp dt)
    {
        if (CooldownRemaining > 0)
        {
            CooldownRemaining -= dt;
            if (CooldownRemaining < 0)
                CooldownRemaining = 0;
        }
    }

    public void LevelUp()
    {
        if (Level < Data.Levels.Length)
            Level++;
    }

    public bool CanStartPreview()
    {
        if (Level == 0)
            return false;

        if (CooldownRemaining > 0)
            return false;

        var conditions = Data.TriggerConditions;
        if (conditions == null)
            return true;

        for (int i = 0; i < conditions.Length; i++)
        {
            if (!conditions[i].CanStartPreview(this))
                return false;
        }

        return true;
    }

    public bool CanCommit(in AbilityTriggerContext context)
    {
        if (Level == 0)
            return false;

        if (CooldownRemaining > 0)
            return false;

        var conditions = Data.TriggerConditions;
        if (conditions == null)
            return true;

        for (int i = 0; i < conditions.Length; i++)
        {
            if (!conditions[i].CanCommit(this, context))
                return false;
        }

        return true;
    }

    public void PayCost()
    {
        var conditions = Data.TriggerConditions;
        if (conditions == null)
            return;

        for (int i = 0; i < conditions.Length; i++)
            conditions[i].PayAbilityCost(this);
    }

    public void ReturnCost()
    {
        var conditions = Data.TriggerConditions;
        if (conditions == null)
            return;

        for (int i = 0; i < conditions.Length; i++)
            conditions[i].CancelReturn(this);
    }

    public void EnterCooldown()
    {
        CooldownRemaining = CurrentCooldownDuration;
    }
}

public readonly struct AbilityContextKey<T>
{
    public readonly string Name;
    public AbilityContextKey(string name) => Name = name;
}

public sealed class AbilityContext
{
    private readonly Dictionary<string, object> data = new();

    public AbilityContextKey<T> Set<T>(string keyName, T value)
    {
        var key = new AbilityContextKey<T>(keyName);
        data[key.Name] = value;
        return key;
    }

    public void Set<T>(AbilityContextKey<T> key, T value)
    {
        data[key.Name] = value;
    }

    public T Get<T>(AbilityContextKey<T> key)
    {
        return (T)data[key.Name];
    }

    public bool TryGet<T>(AbilityContextKey<T> key, out T value)
    {
        if (data.TryGetValue(key.Name, out var obj) && obj is T t)
        {
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    public void Remove(string keyName) => data.Remove(keyName);
    public void Clear() => data.Clear();
}