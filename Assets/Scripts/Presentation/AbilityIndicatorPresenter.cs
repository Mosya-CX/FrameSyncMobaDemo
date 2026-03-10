using UnityEngine;

public sealed class AbilityIndicatorPresenter
{
    private readonly HeroUnit owner;
    private AbilityIndicatorBase currentIndicator;
    private AbilityRuntime currentRuntime;
    private float updateTimer;

    public AbilityIndicatorPresenter(HeroUnit owner)
    {
        this.owner = owner;
    }

    public void Show(AbilityRuntime runtime)
    {
        Hide();

        currentRuntime = runtime;
        currentIndicator = runtime.Data.Indicator;
        updateTimer = 0;

        if (currentIndicator != null)
            currentIndicator.OnCreate(null);
    }

    public void Tick(float dt)
    {
        if (currentIndicator == null)
            return;

        updateTimer += dt;
        currentIndicator.UpdateIndicator(null);
    }

    public void Hide()
    {
        if (currentIndicator != null)
            currentIndicator.InactiveIndicator(null);

        currentIndicator = null;
        currentRuntime = null;
        updateTimer = 0;
    }
}