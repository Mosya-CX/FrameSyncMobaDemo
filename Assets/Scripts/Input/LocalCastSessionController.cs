public sealed class LocalCastSessionController
{
    private readonly HeroUnit owner;
    private readonly AbilityIndicatorPresenter indicatorPresenter;

    public LocalCastSession CurrentSession { get; private set; }

    public LocalCastSessionController(HeroUnit owner, AbilityIndicatorPresenter indicatorPresenter)
    {
        this.owner = owner;
        this.indicatorPresenter = indicatorPresenter;
    }

    public bool TryBeginSession(int abilityId, in InputInfo input)
    {
        if (!owner.AbilityHandler.TryGetRuntime(abilityId, out var ability))
            return false;

        var intent = ability.CheckIntent(owner);
        if (!intent.Success || !intent.ShowIndicator)
            return false;

        CurrentSession = new LocalCastSession
        {
            AbilityId = abilityId,
            State = LocalCastSessionState.Preview,
            Aim = BuildAim(input),
        };

        indicatorPresenter.Show(CurrentSession);
        return true;
    }

    public void UpdateSession(in InputInfo input)
    {
        if (CurrentSession == null || CurrentSession.State != LocalCastSessionState.Preview)
            return;

        CurrentSession.Aim = BuildAim(input);
        indicatorPresenter.Update(CurrentSession);
    }

    public bool TryConfirm(out AbilityCommand command)
    {
        command = null;

        if (CurrentSession == null)
            return false;

        command = new AbilityCommand
        {
            ReceiverUnitId = owner.UnitID,
            AbilityId = CurrentSession.AbilityId,
            context = new AbilityTriggerContext
            {
                TargetPosition = CurrentSession.Aim.TargetPosition,
                TargetUID = CurrentSession.Aim.TargetUnitId,
            }
        };

        indicatorPresenter.Hide();
        CurrentSession.State = LocalCastSessionState.Confirmed;
        CurrentSession = null;
        return true;
    }

    public void Cancel()
    {
        if (CurrentSession == null)
            return;

        indicatorPresenter.Hide();
        CurrentSession.State = LocalCastSessionState.Cancelled;
        CurrentSession = null;
    }

    private LocalAimData BuildAim(in InputInfo input)
    {
        return new LocalAimData
        {
            TargetPosition = input.mousePosition,
            TargetUnitId = input.selectedUnit != null ? input.selectedUnit.UnitID : null,
        };
    }
}