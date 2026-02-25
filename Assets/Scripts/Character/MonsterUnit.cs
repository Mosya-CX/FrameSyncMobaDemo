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
            if (isInBattle == value)
                return;

            isInBattle = value;
            if (isInBattle)
                OnBattleEnter();
            else
                OnBattleExit();
        }
    }

    [SerializeField]
    private MonsterCamp camp;

    private void OnBattleEnter()
    {
        // TODO查找最近的英雄单位

        ChangeActionState(UnitActionState.Track);
    }

    private void OnBattleExit()
    {
        currentTarget = null;
        currentDestination = camp.LogicPosition;
        ChangeActionState(UnitActionState.Move);
    }

    public override void UpdateMoveDirection()
    {
        switch (currentActionState)
        {
            case UnitActionState.Move:
                if (currentDestination.HasValue)
                {
                    // TODO
                    // 根据路径更新方向
                }
                break;
            case UnitActionState.Track:
                if (currentTarget)
                {
                    // TODO
                    // 根据路径更新方向
                }
                break;
        }
    }

    public override void UpdateAStarPath()
    {
        switch (currentActionState)
        {
            case UnitActionState.Move:
                if (currentDestination.HasValue)
                {
                    // TODO
                    // 更新路径

                }
                break;
            case UnitActionState.Track:
                if (currentTarget)
                {
                    // TODO
                    // 更新路径

                }
                break;
        }
    }
}
