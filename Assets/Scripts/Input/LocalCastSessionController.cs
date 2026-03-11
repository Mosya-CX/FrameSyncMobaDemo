using UnityEngine;

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

    public bool TryBeginPreview(int abilityId, in InputInfo inputInfo)
    {
        if (!owner.AbilityHandler.TryGetRuntime(abilityId, out var runtime))
            return false;

        if (!runtime.CanStartPreview())
            return false;

        CurrentSession = new LocalCastSession
        {
            AbilityId = abilityId,
            State = LocalCastSessionState.Preview,
        };

        UpdatePreviewInput(inputInfo, 0f);
        indicatorPresenter.Show(runtime, CurrentSession);
        return true;
    }

    public void UpdatePreviewInput(in InputInfo inputInfo, float deltaTime)
    {
        if (CurrentSession == null)
            return;

        CurrentSession.Aim.TargetPosition = inputInfo.mousePosition;
        CurrentSession.Aim.SelectedUnit = inputInfo.selectedUnit;
        CurrentSession.Aim.HeldSeconds += deltaTime;
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
            QueueIfBusy = true,
            Context = new AbilityTriggerContext
            {
                TargetPosition = CurrentSession.Aim.TargetPosition,
                TargetUID = CurrentSession.Aim.SelectedUnit != null ? CurrentSession.Aim.SelectedUnit.UnitID : null,
            }
        };

        Cancel();
        return true;
    }

    public void Cancel()
    {
        indicatorPresenter.Hide();
        CurrentSession = null;
    }
}