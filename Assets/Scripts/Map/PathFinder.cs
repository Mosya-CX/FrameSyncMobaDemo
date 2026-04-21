using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

public class PathFinder : MonoBehaviour
{
    private UnitCore core;

    [SerializeField] private PathMode pathMode = PathMode.None;
    [SerializeField] private byte blueTeamId = 2;

    private List<fp3> currentPath;
    private int currentWaypointIndex;
    private fp3 targetPosition;
    private UnitCore targetUnit;

    private bool needPathUpdate;

    private void Awake()
    {
        core = GetComponent<UnitCore>();
    }

    public void SetDestination(fp3 destination)
    {
        pathMode = PathMode.AStar;
        targetPosition = destination;
        targetUnit = null;
        RequestPathUpdate();
    }

    public void SetTarget(UnitCore target)
    {
        pathMode = PathMode.AStar;
        targetUnit = target;
        targetPosition = target != null ? target.LogicPosition : fp3.zero;
        RequestPathUpdate();
    }

    public void SetFlowFieldMode()
    {
        pathMode = PathMode.FlowField;
        currentPath = null;
        targetUnit = null;
        needPathUpdate = false;
    }

    public void RequestPathUpdate()
    {
        needPathUpdate = true;
    }

    public void UpdatePath()
    {
        if (!needPathUpdate)
            return;

        needPathUpdate = false;

        if (pathMode != PathMode.AStar)
            return;

        fp3 start = core.LogicPosition;
        fp3 end = targetUnit != null ? targetUnit.LogicPosition : targetPosition;

        List<fp3> path = AStarSystem.Instance.FindPathFP(start, end);
        if (path != null && path.Count > 0)
        {
            currentPath = path;
            currentWaypointIndex = 0;
        }
        else
        {
            currentPath = null;
        }
    }

    public fp3 GetDirection()
    {
        if (pathMode == PathMode.FlowField)
        {
            bool isBlue = core.TeamID == blueTeamId;
            return FlowField.FlowFieldSystem.Instance.GetMoveDirectionFP(core.LogicPosition, isBlue);
        }

        if (pathMode == PathMode.AStar)
        {
            fp3 finalTarget = targetUnit != null ? targetUnit.LogicPosition : targetPosition;

            if (currentPath == null || currentPath.Count == 0)
            {
                fp3 diff = finalTarget - core.LogicPosition;
                return fpmath.length(diff) > fp.zero ? fpmath.normalize(diff) : fp3.zero;
            }

            fp3 waypoint = currentPath[currentWaypointIndex];
            fp3 toWaypoint = waypoint - core.LogicPosition;
            fp dist = fpmath.length(toWaypoint);

            if (dist < (fp)0.5m)
            {
                currentWaypointIndex++;

                if (currentWaypointIndex >= currentPath.Count)
                {
                    fp3 diff = finalTarget - core.LogicPosition;
                    return fpmath.length(diff) > fp.zero ? fpmath.normalize(diff) : fp3.zero;
                }

                waypoint = currentPath[currentWaypointIndex];
                toWaypoint = waypoint - core.LogicPosition;
            }

            return fpmath.lengthsq(toWaypoint) > fp.zero ? fpmath.normalize(toWaypoint) : fp3.zero;
        }

        return fp3.zero;
    }

    public void Stop()
    {
        pathMode = PathMode.None;
        currentPath = null;
        targetUnit = null;
        needPathUpdate = false;
    }

    public enum PathMode
    {
        None,
        AStar,
        FlowField
    }
}
