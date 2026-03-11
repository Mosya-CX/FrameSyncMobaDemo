using UnityEngine;
using Unity.Mathematics.FixedPoint;

public class HeroInputHandler : MonoBehaviour
{
    [HideInInspector] public HeroUnit owner;

    private LocalCastSessionController castSessionController;
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
        castSessionController = new LocalCastSessionController(owner, indicatorPresenter);
    }

    private void Update()
    {
        if (!enabled || owner == null)
            return;

        indicatorPresenter?.Update(Time.deltaTime);

        if (castSessionController?.CurrentSession != null && LocalController.Local != null)
            castSessionController.UpdatePreviewInput(LocalController.Local.BuildCurrentInputInfo(), Time.deltaTime);
    }

    public void HandleRightMouseInput(in InputInfo info)
    {
        if (owner.DashMotor.IsInputLocked_Move() && owner.DashMotor.IsInputLocked_Attack())
            return;

        if (owner.CrowdControlHandler.CurrentSnapshot.BlockMoveInput &&
            owner.CrowdControlHandler.CurrentSnapshot.BlockAttackInput)
            return;

        castSessionController.Cancel();

        if (info.selectedUnit != null && info.selectedUnit.TeamID != owner.TeamID &&
            !info.selectedUnit.IsDead && !owner.CrowdControlHandler.CurrentSnapshot.BlockAttackInput)
        {
            SendAttackCommand(info.selectedUnit.UnitID);
            return;
        }

        if (info.mousePosition.HasValue && !owner.CrowdControlHandler.CurrentSnapshot.BlockMoveInput)
            SendMoveCommand(info.mousePosition.Value);
    }

    public void HandlePressAbilityButton(in int abilityId, in InputInfo inputInfo)
    {
        if (owner.DashMotor.IsInputLocked_Cast())
            return;

        if (owner.CrowdControlHandler.CurrentSnapshot.BlockCastInput)
            return;

        if (!owner.AbilityHandler.TryGetRuntime(abilityId, out var runtime))
            return;

        switch (runtime.Data.LocalInteractionType)
        {
            case LocalCastInteractionType.Instant:
                if (runtime.CanCommit(new AbilityTriggerContext
                {
                    TargetPosition = inputInfo.mousePosition,
                    TargetUID = inputInfo.selectedUnit != null ? inputInfo.selectedUnit.UnitID : null,
                }))
                {
                    SendAbilityCommand(abilityId, new AbilityTriggerContext
                    {
                        TargetPosition = inputInfo.mousePosition,
                        TargetUID = inputInfo.selectedUnit != null ? inputInfo.selectedUnit.UnitID : null,
                    }, true);
                }
                break;

            case LocalCastInteractionType.PressOrRelease:
            case LocalCastInteractionType.HoldAndRelease:
                castSessionController.TryBeginPreview(abilityId, inputInfo);
                break;
        }
    }

    public void HandleReleaseAbilityButton(int abilityId, in InputInfo inputInfo)
    {
        if (castSessionController.CurrentSession == null)
            return;

        if (castSessionController.CurrentSession.AbilityId != abilityId)
            return;

        castSessionController.UpdatePreviewInput(inputInfo, 0f);

        if (castSessionController.TryConfirm(out var command))
            FrameSyncCoreSystem.Instance.AddPendingCommand(command);
    }

    public void CancelCurrentIndicator()
    {
        castSessionController?.Cancel();
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