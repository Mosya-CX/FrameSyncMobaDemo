using Unity.Mathematics.FixedPoint;

public class MobUnit : UnitCore
{
    private bool isInBattle;
    public bool IsInBattle
    {
        get => isInBattle;
        set
        {
            if (isInBattle == value) return;
            isInBattle = value;
            if (isInBattle)
                OnBattleEnter();
            else
                OnBattleExit();
        }
    }

    private void Start()
    {
        // 默认使用流场
        pathFinder.SetFlowFieldMode();
    }

    private void OnBattleEnter()
    {
        // 进入战斗，查找最近单位
        currentTarget = FindNearestEnemy();
        pathFinder.SetTarget(currentTarget);
        ChangeActionState(UnitActionState.Track);
    }

    private void OnBattleExit()
    {
        currentTarget = null;
        // 回到流场模式
        pathFinder.SetFlowFieldMode();
        ChangeActionState(UnitActionState.Move);
    }

    private UnitCore FindNearestEnemy()
    {
        UnitCore nearest = null;
        // TODO
        
        return nearest;
    }

    protected override void OnTrackEnter()
    {
        base.OnTrackEnter();
        if (currentTarget != null)
            pathFinder.SetTarget(currentTarget);
    }

    protected override void OnTrackExit()
    {
        base.OnTrackExit();
        pathFinder.Stop();
        pathFinder.SetFlowFieldMode();
    }

    public override void UpdateAStarPath()
    {
        if (currentActionState == UnitActionState.Track && currentTarget != null)
        {
            base.UpdateAStarPath();
        }
    }
}

