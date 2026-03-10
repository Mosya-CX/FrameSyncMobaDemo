using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class HeroInputHandler : MonoBehaviour
{
    [HideInInspector] public HeroUnit owner;

    private LocalCastSession currentSession;
    private AbilityIndicatorPresenter indicatorPresenter;

    private void Awake()
    {
        enabled = false;
    }

    public void Init()
    {
        if (enabled)
            return;

        enabled = true;
        owner = GetComponent<HeroUnit>();
        indicatorPresenter = new AbilityIndicatorPresenter(owner);
    }

    private void Update()
    {
        indicatorPresenter?.Tick(Time.deltaTime);
    }

    public void HandleRightMouseInput(in InputInfo info)
    {
        if (owner.CrowdControlHandler.CurrentSnapshot.BlockMoveInput &&
            owner.CrowdControlHandler.CurrentSnapshot.BlockAttackInput)
            return;

        CancelCurrentSession();

        if (info.selectedUnit != null &&
            !owner.CrowdControlHandler.CurrentSnapshot.BlockAttackInput &&
            info.selectedUnit.TeamID != owner.TeamID &&
            info.selectedUnit.CurrentActionState != UnitActionState.Dead)
        {
            SendAttackCommand(info.selectedUnit.UnitID);
            return;
        }

        if (info.mousePosition.HasValue && !owner.CrowdControlHandler.CurrentSnapshot.BlockMoveInput)
            SendMoveCommand(info.mousePosition.Value);
    }

    public void HandlePressAbilityButton(in int abilityId, in InputInfo inputInfo)
    {
        if (owner.CrowdControlHandler.CurrentSnapshot.BlockCastInput)
            return;

        if (!owner.AbilityHandler.TryGetRuntime(abilityId, out var runtime))
            return;

        if (!runtime.CanStartPreview())
            return;

        var interaction = runtime.Data.LocalInteractionType;
        var context = BuildContext(inputInfo);

        switch (interaction)
        {
            case LocalCastInteractionType.Instant:
                SendAbilityCommand(abilityId, context, false);
                break;

            case LocalCastInteractionType.PressOrRelease:
            case LocalCastInteractionType.HoldAndRelease:
                currentSession = new LocalCastSession
                {
                    AbilityId = abilityId,
                    State = LocalCastSessionState.Preview,
                    Aim = new LocalAimData
                    {
                        TargetPosition = inputInfo.mousePosition,
                        TargetUnitId = inputInfo.selectedUnit != null ? inputInfo.selectedUnit.UnitID : null,
                    }
                };
                indicatorPresenter.Show(runtime);
                break;
        }
    }

    public void HandleReleaseAbilityButton(int abilityId, in InputInfo inputInfo)
    {
        if (currentSession == null || currentSession.AbilityId != abilityId)
            return;

        var context = BuildContext(inputInfo);
        SendAbilityCommand(abilityId, context, true);
        CancelCurrentSession();
    }

    public void CancelCurrentIndicator()
    {
        CancelCurrentSession();
    }

    private void CancelCurrentSession()
    {
        if (currentSession != null)
        {
            indicatorPresenter.Hide();
            currentSession = null;
        }
    }

    private AbilityTriggerContext BuildContext(in InputInfo inputInfo)
    {
        return new AbilityTriggerContext
        {
            TargetPosition = inputInfo.mousePosition,
            TargetUID = inputInfo.selectedUnit != null ? inputInfo.selectedUnit.UnitID : null,
        };
    }

    public void SendAttackCommand(UnitUID targetUid)
    {
        var command = new AttackCommand
        {
            ReceiverUnitId = owner.UnitID,
            TargetUnitId = targetUid,
        };
        FrameSyncCoreSystem.Instance.AddPendingCommand(command);
    }

    public void SendMoveCommand(fp3 targetPosition)
    {
        var command = new MoveCommand
        {
            ReceiverUnitId = owner.UnitID,
            TargetPosition = targetPosition,
        };
        FrameSyncCoreSystem.Instance.AddPendingCommand(command);
    }

    public void SendAbilityCommand(int abilityId, AbilityTriggerContext context, bool queueIfBusy)
    {
        var command = new AbilityCommand
        {
            ReceiverUnitId = owner.UnitID,
            AbilityId = abilityId,
            QueueIfBusy = queueIfBusy,
            Context = context,
        };
        FrameSyncCoreSystem.Instance.AddPendingCommand(command);
    }
}