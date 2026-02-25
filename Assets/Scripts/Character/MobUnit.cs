using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class MobUnit : UnitCore
{
    public override void UpdateMoveDirection()
    {
        switch (currentActionState)
        {
            case UnitActionState.Move:
                // TODO
                // 根据当前位置和附近流场价值更新方向

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

