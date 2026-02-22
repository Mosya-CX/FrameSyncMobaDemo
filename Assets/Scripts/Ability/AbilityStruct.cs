using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public class AbilityInfo
{
    public AbilityData Data { get; }
    public AbilityLevelData LevelData => Data.Levels[level - 1];

    public SkillState State { get; private set; } = SkillState.Idle;

    public int CurrentPhaseIndex { get; private set; }
    public SkillPhase CurrentPhase => Data.Phases[CurrentPhaseIndex];

    public SkillContext Context { get; } = new();

    private AbilityHandler handler;

    private int level = 1;
    private fp stateTimer;
    private fp cooldownRemaining;

    private AbilityCastContext castContext;

    private List<ISkillIndicatorRuntime> activeIndicators = new();

    public AbilityInfo(AbilityData data, AbilityHandler handler)
    {
        Data = data;
        this.handler = handler;
    }

    // ===== 外部接口 =====

    public void OnPress(AbilityCastContext context)
    {
        if (!CanStart()) return;

        castContext = context;

        switch (Data.TriggerMode)
        {
            case SkillTriggerMode.PressCast:
                BeginPreCast();
                break;

            case SkillTriggerMode.PressReleaseCast:
                EnterAiming();
                break;

            case SkillTriggerMode.PressCastAndCharge:
                BeginChanneling();
                break;
        }
    }

    public void OnRelease(AbilityCastContext context)
    {
        castContext = context;

        if (State == SkillState.Aiming)
        {
            BeginPreCast();
        }
        else if (State == SkillState.Channeling &&
                 Data.TriggerMode == SkillTriggerMode.PressCastAndCharge)
        {
            EnterCasting();
        }
    }

    public void OnCancel()
    {
        Interrupt();
    }

    public void Tick(fp deltaTime)
    {
        if (cooldownRemaining > 0)
            cooldownRemaining -= deltaTime;

        switch (State)
        {
            case SkillState.PreCast:
            case SkillState.Aiming:
            case SkillState.Channeling:
                UpdateIndicators();
                break;
            case SkillState.Recover:
                UpdateTimedState(deltaTime);
                break;

            case SkillState.Casting:
                UpdateCasting(deltaTime);
                break;
        }
    }

    // ===== 状态切换 =====

    private void EnterAiming()
    {
        State = SkillState.Aiming;
        CreateIndicators();
    }

    private void BeginPreCast()
    {
        State = SkillState.PreCast;
        stateTimer = CurrentPhase.PreCastTime;

        foreach (var m in CurrentPhase.Modules)
            m.OnPhaseEnter(this, handler);

        DestroyIndicators();
    }

    private void BeginChanneling()
    {
        State = SkillState.Channeling;
        stateTimer = CurrentPhase.ChannelTime;

        CreateIndicators();
    }

    private void EnterCasting()
    {
        State = SkillState.Casting;
        stateTimer = 0;

        foreach (var m in CurrentPhase.Modules)
            m.OnPhaseEnter(this, handler);

        DestroyIndicators();
    }

    private void EnterRecover()
    {
        State = SkillState.Recover;
        stateTimer = CurrentPhase.RecoverTime;
    }

    private void EnterCooldown()
    {
        State = SkillState.Cooldown;
        cooldownRemaining = LevelData.Cooldown;
        CurrentPhaseIndex = 0;

        DestroyIndicators();
    }

    private void UpdateTimedState(fp deltaTime)
    {
        stateTimer -= deltaTime;

        if (stateTimer <= 0)
        {
            if (State == SkillState.PreCast)
                EnterCasting();
            else if (State == SkillState.Channeling)
                EnterCasting();
            else if (State == SkillState.Recover)
                EnterCooldown();
        }
    }

    private void UpdateCasting(fp deltaTime)
    {
        foreach (var m in CurrentPhase.Modules)
            m.OnPhaseUpdate(this, handler, deltaTime);

        foreach (var m in CurrentPhase.Modules)
            m.OnPhaseExit(this, handler);

        AdvancePhaseOrFinish();
    }

    private void AdvancePhaseOrFinish()
    {
        CurrentPhaseIndex++;

        if (CurrentPhaseIndex >= Data.Phases.Count)
        {
            EnterRecover();
        }
        else
        {
            BeginPreCast();
        }
    }

    private void Interrupt()
    {
        if (State == SkillState.Idle || State == SkillState.Cooldown)
            return;

        if (Data.RefundOnInterrupt)
        {
            var refund = LevelData.ManaCost * Data.RefundPercent;
            handler.Core.Stats.ModifyMana(refund);
        }

        DestroyIndicators();

        State = SkillState.Interrupted;
        Context.Clear();
        CurrentPhaseIndex = 0;
    }

    private bool CanStart()
    {
        if (State != SkillState.Idle) return false;
        if (cooldownRemaining > 0) return false;
        if (handler.Core.CurrentMana < LevelData.ManaCost) return false;

        handler.Core.Stats.ModifyMana(-LevelData.ManaCost);
        return true;
    }

    private void CreateIndicators()
    {
        var modules = CurrentPhase.IndicatorModules;

        if (modules == null) return;

        foreach (var m in modules)
        {
            var runtime = m.CreateRuntime();
            runtime.OnCreate(this);
            activeIndicators.Add(runtime);
        }
    }

    private void UpdateIndicators()
    {
        foreach (var r in activeIndicators)
            r.OnUpdate(this);
    }

    private void DestroyIndicators()
    {
        foreach (var r in activeIndicators)
            r.OnDestroy();

        activeIndicators.Clear();
    }
}

public enum SkillState
{
    Idle,
    Aiming,        // 仅指示器阶段
    PreCast,
    Channeling,
    Casting,
    Recover,
    Cooldown,
    Interrupted
}

public enum SkillTriggerMode
{
    PressCast,             // 按下立即释放
    PressReleaseCast,      // 按下瞄准，松开释放
    PressCastAndCharge     // 按下开始蓄力，松开释放
}

public enum SkillIndicatorType
{
    None,
    Circle,
    Line,
    Sector,
    Target
}

public interface ISkillIndicatorRuntime
{
    void OnCreate(AbilityInfo info);
    void OnUpdate(AbilityInfo info);
    void OnDestroy();
}

public readonly struct SkillContextKey<T>
{
    public readonly string Name;
    public SkillContextKey(string name) => Name = name;
}

public class SkillContext
{
    private readonly Dictionary<string, object> data = new();

    public void Set<T>(SkillContextKey<T> key, T value)
    {
        data[key.Name] = value;
    }

    public T Get<T>(SkillContextKey<T> key)
    {
        return (T)data[key.Name];
    }

    public bool TryGet<T>(SkillContextKey<T> key, out T value)
    {
        if (data.TryGetValue(key.Name, out var obj) && obj is T t)
        {
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    public void Clear() => data.Clear();
}