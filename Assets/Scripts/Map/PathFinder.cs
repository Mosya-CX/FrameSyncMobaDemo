using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

public class PathFinder : MonoBehaviour
{
    private UnitCore core;

    [SerializeField] private PathMode pathMode = PathMode.None;

    // A* 路径相关
    private List<fp3> currentPath;
    private int currentWaypointIndex;
    private fp3 targetPosition;
    private UnitCore targetUnit;

    // 流场相关
    private bool useFlowField;
    private bool needPathUpdate;

    private void Awake()
    {
        core = GetComponent<UnitCore>();
    }

    #region 公开方法

    /// <summary>设置目标点（Move 模式）</summary>
    public void SetDestination(fp3 destination)
    {
        pathMode = PathMode.AStar;
        targetPosition = destination;
        targetUnit = null;
        useFlowField = false;
        RequestPathUpdate();
    }

    /// <summary>设置追踪目标（Track 模式）</summary>
    public void SetTarget(UnitCore target)
    {
        pathMode = PathMode.AStar;
        targetUnit = target;
        targetPosition = target.LogicPosition;
        useFlowField = false;
        RequestPathUpdate();
    }

    /// <summary>切换到流场模式</summary>
    public void SetFlowFieldMode()
    {
        pathMode = PathMode.FlowField;
        useFlowField = true;
        currentPath = null;
    }

    /// <summary>请求重新计算路径（下一帧更新）</summary>
    public void RequestPathUpdate()
    {
        needPathUpdate = true;
    }

    /// <summary>更新路径（由 UnitManager 周期性调用）</summary>
    public void UpdatePath()
    {
        if (!needPathUpdate) return;
        needPathUpdate = false;

        if (pathMode == PathMode.AStar)
        {
            // 获取起点和终点
            fp3 start = core.LogicPosition;
            fp3 end = targetUnit != null ? targetUnit.LogicPosition : targetPosition;

            // 转换为世界坐标（AStarSystem 使用 float）
            Vector3 startWorld = new Vector3((float)start.x, (float)start.y, (float)start.z);
            Vector3 endWorld = new Vector3((float)end.x, (float)end.y, (float)end.z);

            List<Vector3> worldPath = AStarSystem.Instance.FindPathWorld(startWorld, endWorld);
            if (worldPath != null && worldPath.Count > 0)
            {
                currentPath = new List<fp3>();
                foreach (var p in worldPath)
                    currentPath.Add(new fp3((fp)p.x, (fp)p.y, (fp)p.z));
                currentWaypointIndex = 0;
            }
            else
            {
                // 无有效路径，清空路径以便直接向目标移动
                currentPath = null;
            }
        }
        // 流场模式无需路径
    }

    /// <summary>获取当前移动方向（每帧调用）</summary>
    public fp3 GetDirection()
    {
        if (pathMode == PathMode.FlowField)
        {
            // 从流场系统获取方向
            fp3 pos = core.LogicPosition;
            Vector3 worldPos = new Vector3((float)pos.x, (float)pos.y, (float)pos.z);
            bool isBlue = core.TeamID == 0; // 约定 0 为蓝队
            Vector3 dir = FlowField.FlowFieldSystem.Instance.GetMoveDirectionWorld(worldPos, isBlue);
            return new fp3((fp)dir.x, (fp)dir.y, (fp)dir.z);
        }
        else if (pathMode == PathMode.AStar)
        {
            fp3 targetPos = targetUnit != null ? targetUnit.LogicPosition : targetPosition;

            // 如果没有有效路径，直接指向最终目标
            if (currentPath == null || currentPath.Count == 0)
            {
                fp3 diff = targetPos - core.LogicPosition;
                return fpmath.length(diff) > fp.zero ? fpmath.normalize(diff) : fp3.zero;
            }

            // 获取当前路径点
            fp3 waypoint = currentPath[currentWaypointIndex];
            fp3 toWaypoint = waypoint - core.LogicPosition;
            fp dist = fpmath.length(toWaypoint);

            // 如果足够接近当前路径点，切换到下一个
            if (dist < (fp)0.5m) // 到达阈值
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= currentPath.Count)
                {
                    // 路径已走完，直接指向最终目标
                    fp3 diff = targetPos - core.LogicPosition;
                    return fpmath.length(diff) > fp.zero ? fpmath.normalize(diff) : fp3.zero;
                }
                waypoint = currentPath[currentWaypointIndex];
                toWaypoint = waypoint - core.LogicPosition;
            }

            return fpmath.normalize(toWaypoint);
        }

        return fp3.zero;
    }

    /// <summary>停止寻路，清除目标</summary>
    public void Stop()
    {
        pathMode = PathMode.None;
        currentPath = null;
        targetUnit = null;
        needPathUpdate = false;
    }

    #endregion

    public enum PathMode
    {
        None,
        AStar,
        FlowField
    }
}
