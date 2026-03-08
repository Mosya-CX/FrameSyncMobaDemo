using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public class AbilityInfo
{
    public AbilityData data;
    public AbilityHandler handler;

    public AbilityContext blackBoard = new();
    public AbilityState state = AbilityState.Idle;
    public AbilityTriggerContext? context;

    public int currentPhaseIndex;
    private ushort level;
    public fp cooldownMultiplier = 1;

    private bool isAbilityActive;
    private bool isPhaseActive;

    public fp localDeltaTime;
    public fp cooldownRemaining;
    public fp precastTimer;
    public fp channelingTimer;
    public bool isPersistentKeep;
    public fp phaseKeepTimer;
    public fp triggerCooldown;
    public short channelingTriggerChance;

    #region ¿ì½Ý·ÃÎÊ
    public ushort CurrentLevel => level;
    public AbilityPhase CurrentPhase => data.Phases[currentPhaseIndex];
    public AbilityLevelData CurrentLevelData => level > 0 ? data.Levels[level-1] : null;
    public fp CurrentCooldownDuration => ((fp)CurrentLevelData.Cooldown) * cooldownMultiplier;
    #endregion

    public AbilityInfo(AbilityData data, AbilityHandler handler)
    {
        this.data = data;
        this.handler = handler;
        currentPhaseIndex = 0;
        level = 0;
    }

    public void Trigger(in AbilityTriggerContext? context = null)
    {
        if (level == 0)
            return;
        if (triggerCooldown > 0)
            return;

        switch (state)
        {
            case AbilityState.Idle:
                if (isPhaseActive)
                    TriggerPhaseKeep(context);
                else
                {
                    if (!isAbilityActive)
                    {
                        for (int i = 0; i < data.TriggerConditions.Length; i++)
                            data.TriggerConditions[i].PayAbilityCost(this);
                        isAbilityActive = true;
                    }

                    StartPhase(context);
                }
                break;
            case AbilityState.Channeling:
                TriggerChanneling(context);
                break;
        }
    }

    public void Tick(fp dt)
    {
        localDeltaTime = dt;

        if (cooldownRemaining > 0)
            cooldownRemaining -= dt;

        if (isPhaseActive)
        {
            ExecuteAbilityMoudles(CurrentPhase.OnPhaseTick);
            if (phaseKeepTimer > 0)
                phaseKeepTimer -= dt;
        }

        switch (state)
        {
            case AbilityState.Idle:
                if (isPhaseActive)
                    if (phaseKeepTimer <= 0)
                        ExitPhase();
                break;
            case AbilityState.Precast:
                ExecuteAbilityMoudles(CurrentPhase.OnPrecastTick);
                break;
            case AbilityState.Channeling:
                channelingTimer -= dt;
                ExecuteAbilityMoudles(CurrentPhase.OnChannelingTick);
                if (channelingTimer <= 0)
                {
                    ExecuteAbilityMoudles(CurrentPhase.OnChannelingTimeOut);
                    ExitChanneling();
                }
                break;
            case AbilityState.Cooldown:
                if (cooldownRemaining <= 0)
                    ExitCooldown();
                break;
        }
    }

    public void StartPhase(in AbilityTriggerContext? context = null)
    {
        if (data.StartCooldownPhase == currentPhaseIndex && data.CooldownApplyTiming == AbilityStartCooldownTiming.OnEnterPhase)
            cooldownRemaining = CurrentCooldownDuration;

        handler.activeAbilities.Add(this);
        isPhaseActive = true;

        this.context = context;
        ExecuteAbilityMoudles(CurrentPhase.OnPhaseEnter);
        this.context = null;

        isPersistentKeep = CurrentPhase.IsPersistent;
        phaseKeepTimer = (fp)CurrentPhase.PhaseKeepDuration;
        precastTimer = (fp)CurrentPhase.PrecastDuration;
        channelingTimer = (fp)CurrentPhase.ChannelingDuration;
        channelingTriggerChance = CurrentPhase.ChannelingRecycleTriggerChance;
        StartPrecast();
    }

    private void StartPrecast()
    {
        if (state == AbilityState.Precast)
            return;
        state = AbilityState.Precast;
        //if (data.StartCooldownPhase == currentPhaseIndex && data.CooldownApplyTiming == AbilityStartCooldownTiming.OnEnterPrecast)
        //   cooldownRemaining = CurrentCooldownDuration;

        ExecuteAbilityMoudles(CurrentPhase.OnPrecastEnter);
    }

    private void ExitPrecast()
    {
        if (state != AbilityState.Precast)
            return;
        //if (data.StartCooldownPhase == currentPhaseIndex && data.CooldownApplyTiming == AbilityStartCooldownTiming.OnExitPrecast)
        //    cooldownRemaining = CurrentCooldownDuration;

        ExecuteAbilityMoudles(CurrentPhase.OnPrecastExit);
    }

    public void StartChannel()
    {
        if (state == AbilityState.Channeling)
            return;
        state = AbilityState.Channeling;
        //if (data.StartCooldownPhase == currentPhaseIndex && data.CooldownApplyTiming == AbilityStartCooldownTiming.OnEnterChanneling)
        //    cooldownRemaining = CurrentCooldownDuration;
        ExecuteAbilityMoudles(CurrentPhase.OnChannelingEnter);
    }

    private void TriggerChanneling(in AbilityTriggerContext? context)
    {
        if (!CurrentPhase.CanTriggerChanneling)
            return;

        if (channelingTriggerChance > 0)
        {
            this.context = context;
            ExecuteAbilityMoudles(CurrentPhase.OnChannelingTrigger);
            this.context = null;
            triggerCooldown = (fp)CurrentPhase.ChannelingTriggerCooldown;
            channelingTriggerChance--;
        }
        if (channelingTriggerChance <= 0)
            ExitChanneling();
    }

    public void ExitChanneling()
    {
        if (state != AbilityState.Channeling)
            return;
        //if (data.StartCooldownPhase == currentPhaseIndex && data.CooldownApplyTiming == AbilityStartCooldownTiming.OnExitChanneling)
        //    cooldownRemaining = CurrentCooldownDuration;
        ExecuteAbilityMoudles(CurrentPhase.OnChannelingExit);
        state = AbilityState.Idle;
    }

    private void TriggerPhaseKeep(in AbilityTriggerContext? context)
    {
        this.context = context;
        ExecuteAbilityMoudles(CurrentPhase.OnPhaseTick);
        this.context = null;
    }


    public void ExitPhase()
    {
        if (data.StartCooldownPhase == currentPhaseIndex && data.CooldownApplyTiming == AbilityStartCooldownTiming.OnEnterPhase)
            cooldownRemaining = CurrentCooldownDuration;

        ExecuteAbilityMoudles(CurrentPhase.OnPhaseExit);

        precastTimer = 0;
        channelingTimer = 0;
        isPersistentKeep = false;
        phaseKeepTimer = 0;
        channelingTriggerChance = 0;

        handler.activeAbilities.Remove(this);
        isPhaseActive = false;

        currentPhaseIndex++;
        if (currentPhaseIndex >= data.Phases.Length)
        {
            isAbilityActive = false;
            currentPhaseIndex = 0;
            EnterCooldown();
        }
    }

    public void EnterCooldown()
    {
        if (state == AbilityState.Cooldown)
            return;
        state = AbilityState.Cooldown;
    }

    private void ExitCooldown()
    {
        if (state != AbilityState.Cooldown)
            return;
        state = AbilityState.Idle;
    }

    public void ReturnResources()
    {
        for (int i = 0; i < data.TriggerConditions.Length; i++)
            data.TriggerConditions[i].CancelReturn(this);
        cooldownRemaining *= 1 - (fp)data.CancelReturnCooldownPercent;
    }

    public void StopAbility()
    {
        phaseKeepTimer = 0;
        channelingTimer = 0;
        isAbilityActive = false;
        currentPhaseIndex = 0;
        EnterCooldown();
    }

    private void ExecuteAbilityMoudles(in AbilityBaseMoudle[] moudles)
    {
        if (moudles != null)
            for (int i = 0; i < moudles.Length; i++)
                moudles[i].Apply(this);
    }

    public void UpLevel()
    {
        if (level + 1 <= data.Levels.Length)
            level++;
    }

    public bool CanTrigger(in InputInfo inputInfo)
    {
        if (state == AbilityState.Cooldown)
            return false;

        for (int i = 0; i <= data.TriggerConditions.Length; i++)
            if (!data.TriggerConditions[i].CanTrigger(this, inputInfo))
                return false;

        return true;
    }
}

public enum AbilityState
{
    Idle,
    Precast,
    Channeling,
    Cooldown,
}

public readonly struct AbilityContextKey<T>
{
    public readonly string Name;
    public AbilityContextKey(string name) => Name = name;
}

public class AbilityContext
{
    private readonly Dictionary<string, object> data = new();

    public AbilityContextKey<T> Set<T>(string keyName, T value)
    {
        var contextKey = new AbilityContextKey<T>(keyName);
        data[contextKey.Name] = value;
        return contextKey;
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

    public T Take<T>(AbilityContextKey<T> key)
    {
        var value = data[key.Name];
        Remove(key.Name);
        return (T)value;
    }

    public bool TryTake<T>(AbilityContextKey<T> key, out T value)
    {
        if (data.TryGetValue(key.Name, out var obj) && obj is T t)
        {
            Remove(key.Name);
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    public void Remove(string keyName) => data.Remove(keyName);

    public void Clear() => data.Clear();
}

public struct AbilityTriggerContext
{
    public UnitUID? TargetUID;
    public fp3? TargetPosition;
}

public static class AbilityTagAnalyzer
{
    private static Dictionary<string, HashSet<string>> tagConflictDict = new Dictionary<string, HashSet<string>>
    {
        { "Test1", new HashSet<string> { "Test2", "Test3" } },
    };

    public static bool CheckConflict(in string[] source, in string[] target)
    {
        for (int i = 0; i < source.Length; i++)
            for (int j = 0; j < target.Length; j++)
                if (CheckConflict(source[i], target[j]))
                    return true;
        return false;
    }

    public static bool CheckConflict(in string source, in string target)
    {
        return tagConflictDict.TryGetValue(source, out var conflictSet) && conflictSet.Contains(target);
    }
}