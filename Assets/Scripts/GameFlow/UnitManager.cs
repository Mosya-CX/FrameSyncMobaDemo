using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class UnitManager : MonoSingleton<UnitManager>, IGameFlowManaged
{
    [SerializeField, LabelText("路径更新间隔")]
    private fp astarPathUpdateInterval = 0.1m;

    private fp astarPathUpdateTimer;

    public IReadOnlyDictionary<UnitUID, UnitCore> tickableUnits => UnitSpawner.Instance.Spawns;
    public fp DeltaTime => GameFlowManager.Instance.TickIntervalFP;

    public IEnumerator Begin()
    {
        yield break;
    }

    public IEnumerator Clean()
    {
        yield break;
    }

    public IEnumerator Init()
    {
        yield break;
    }

    public void Tick(ulong currentTick)
    {
        astarPathUpdateTimer += DeltaTime;
        // 状态更新
        foreach (var unit in tickableUnits.Values)
        {
            unit.Tick(DeltaTime);
        }

        // 更新寻路方向
        foreach (var unit in tickableUnits.Values)
        {
            if (astarPathUpdateTimer > astarPathUpdateInterval)
            {
                astarPathUpdateTimer -= DeltaTime;
                unit.UpdateAStarPath();
            }
            unit.UpdateMoveDirection();

            fp modifiedDir = 0;
            unit.ApplyMove(DeltaTime, modifiedDir);
        }

        // TODO 应用RVO避障修正方向
        // 应用移动
        foreach (var unit in tickableUnits.Values)
        {
            unit.ApplyMove(DeltaTime, RVOGenerator.Instance.GetModifiedDirection(unit));
        }
    }

}
