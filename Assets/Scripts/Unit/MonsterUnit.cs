using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class MonsterUnit : UnitCore
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

    [SerializeField, ReadOnly]
    private MonsterCamp camp;
    [SerializeField, ReadOnly]
    private fp3 originPosition;
    [SerializeField, ReadOnly]
    private fp2 originRotation;

    public void SetBelongTo(MonsterCamp camp, fp3 position, fp2 rotation)
    {
        this.camp = camp;
        originPosition = position;
        originRotation = rotation;
        LogicPosition = originPosition;
        LogicRotation = originRotation;
    }

    private void OnBattleEnter()
    {
        // 寻找最近的敌方英雄
        currentTarget = FindNearestHero();
        if (currentTarget != null)
        {
            pathFinder.SetTarget(currentTarget);
            ChangeActionState(UnitActionState.Track);
        }
        else
        {
            ChangeActionState(UnitActionState.Idle);
        }
    }

    private void OnBattleExit()
    {
        currentTarget = null;
        currentDestination = originPosition;
        pathFinder.SetDestination(currentDestination.Value);
        ChangeActionState(UnitActionState.Move);
    }

    private UnitCore FindNearestHero()
    {
        UnitCore nearest = null;
        fp minDist = fp.max_value;

        
        return nearest;
    }

    protected override void OnTrackEnter()
    {
        base.OnTrackEnter();
        if (currentTarget != null)
            pathFinder.SetTarget(currentTarget);
    }

    protected override void OnMoveEnter()
    {
        base.OnMoveEnter();
        if (currentDestination.HasValue)
            pathFinder.SetDestination(currentDestination.Value);
    }
}
