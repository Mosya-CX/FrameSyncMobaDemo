using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class RVOGenerator : Singleton<RVOGenerator>
{
    private List<IDynamicObstacle> dynamicObstacles;

    public void Register(IDynamicObstacle obstacle)
    {
        dynamicObstacles.Add(obstacle);
    }

    public void Unregister(IDynamicObstacle obstacle)
    {
        dynamicObstacles.Remove(obstacle);
    }

    // TODO 
    public fp3 GetModifiedDirection(IDynamicObstacle modifiedObstacle)
    {
        return fp3.zero;
    }
}

public interface IDynamicObstacle
{
    public void RegisterRVOGenerator();
    public void UnregisterRVOGenerator();

    public fp3 ObstaclePosition { get; }

    public fp3 ObstacleDirection { get; }

    public fp3 ObstacleSpeed { get; }

    public IDynamicObstacle Ingore { get; }// 修正时忽略其影响的对象
}
