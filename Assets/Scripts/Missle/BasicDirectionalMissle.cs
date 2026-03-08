using Sirenix.OdinInspector;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public abstract class BasicDirectionalMissle : ProjectileMissle
{
    [SerializeField, LabelText("触发模式")]
    private ApplyMode applyMode = ApplyMode.Single;

    [SerializeField, LabelText("作用对象")]
    private AffectTargetType affectType;

    protected DirectionalMisslePath path;
    protected int nextPathPoint;
    
    protected bool shouldRecycle;

    protected UnitUID ownerUid;
    protected UnitCore owner => UnitManager.Instance.Spawns.ContainsKey(ownerUid) ? UnitManager.Instance.Spawns[ownerUid] : null;

    protected Queue<UnitUID> affectables = new();

    public override void OnDespawn()
    {
        
    }

    public override void OnSpawn(IMissleInitialData initialData)
    {
        if (initialData == null || !(initialData is DirectionalMissleInitialData data))
        {
            MissleManager.Instance.Recycle(this);
            return;
        }

        ownerUid = data.ownerUid;
        path = BakePath(data);

        if (path == null || path.pathPoints.Count == 0)
        {
            MissleManager.Instance.Recycle(this);
            return;
        }

        logicPosition = path.pathPoints[0];
    }

    protected abstract DirectionalMisslePath BakePath(in DirectionalMissleInitialData data);

    protected override bool CanApply() => affectables.Count > 0;

    protected override void OnMissleApply()
    {
        if (shouldRecycle)
            return;

        if (affectables.Count == 0)
            return;

        UnitCore target = null;
        switch (applyMode)
        {
            case ApplyMode.Single:
                while (!UnitManager.Instance.Spawns.TryGetValue(affectables.Dequeue(), out target));
                Apply(target);
                shouldRecycle = true;
                break;
            case ApplyMode.Multiple:
                while (affectables.Count > 0)
                    if (UnitManager.Instance.Spawns.TryGetValue(affectables.Dequeue(), out target))
                        Apply(target);
                break;
        }
        affectables.Clear();
    }

    protected abstract void Apply(UnitCore target);

    protected override bool IsRecycle() => shouldRecycle;

    protected override fp3 UpdateDirection(fp dt)
    {
        if (shouldRecycle) return fp3.zero;

        if (path.IsReachPoint(nextPathPoint, logicPosition))
        {
            nextPathPoint++;
            if (path.pathPoints.Count <= nextPathPoint)
            {
                shouldRecycle = true;
                return fp3.zero;
            }
            return fpmath.normalize(path.pathPoints[nextPathPoint] - logicPosition);
        }

        return direction;
    }

    protected class DirectionalMisslePath
    {
        public List<fp3> pathPoints = new();

        public bool IsReachPoint(int index, fp3 currentPosition)
        {
            return fpmath.distance(pathPoints[index], currentPosition) < 0.05m;
        }
    }

    public override void OnMissleTrigger(UnitCore target)
    {
        if (target.CompareTag("Hero"))
        {
            if (target.TeamID == owner.TeamID)
            {
                if (affectType.HasFlag(AffectTargetType.FriendlyHero))
                    affectables.Enqueue(target.UnitID);
            }
            else
            {
                if (affectType.HasFlag(AffectTargetType.EnemyHero))
                    affectables.Enqueue(target.UnitID);
            }
        }
        else if (target.CompareTag("Mob"))
        {
            if (target.TeamID == owner.TeamID)
            {
                if (affectType.HasFlag(AffectTargetType.FriendlyMob))
                    affectables.Enqueue(target.UnitID);
            }
            else
            {
                if (affectType.HasFlag(AffectTargetType.EnemyMob))
                    affectables.Enqueue(target.UnitID);
            }
        }
        else if (target.CompareTag("Monster"))
        {
            if (affectType.HasFlag(AffectTargetType.Monster))
                affectables.Enqueue(target.UnitID);
        }
    }

    [System.Flags]
    protected enum AffectTargetType
    {
        None,
        FriendlyHero,
        FriendlyMob,
        EnemyHero,
        EnemyMob,
        Monster,
        All,
    }

    protected enum ApplyMode
    {
        Single,
        Multiple,
    }

    #region 快照和回滚
    public override object CaptureState()
    {
        var state = base.CaptureState();
        if (state is MissleSnapshot snapshot)
        {
            snapshot.stateSnapshotDict.Add(nameof(path), new List<fp3>(path.pathPoints));
            snapshot.stateSnapshotDict.Add(nameof(nextPathPoint), nextPathPoint);
            snapshot.stateSnapshotDict.Add(nameof(shouldRecycle), shouldRecycle);
            snapshot.stateSnapshotDict.Add(nameof(ownerUid), ownerUid);
            snapshot.stateSnapshotDict.Add(nameof(affectables), new List<UnitUID>(affectables));
        }
        return state;
    }

    public override void RestoreState(object state)
    {
        base.RestoreState(state);
        if (state is MissleSnapshot snapshot)
        {
            if (snapshot.stateSnapshotDict.TryGetValue(nameof(path), out var pathObj))
                path.pathPoints = (List<fp3>)pathObj;
            if (snapshot.stateSnapshotDict.TryGetValue(nameof(nextPathPoint), out var nextPathPointObj))
                nextPathPoint = (int)nextPathPointObj;
            if (snapshot.stateSnapshotDict.TryGetValue(nameof(shouldRecycle), out var shouldRecycleObj))
                shouldRecycle = (bool)shouldRecycleObj;
            if (snapshot.stateSnapshotDict.TryGetValue(nameof(ownerUid), out var ownerUidObj))
                ownerUid = (UnitUID)ownerUidObj;
            if (snapshot.stateSnapshotDict.TryGetValue(nameof(affectables), out var affectablesObj))
            {
                affectables.Clear();
                var affectableList = (List<UnitUID>)affectablesObj;
                for (int i = 0; i < affectableList.Count; i++)
                    affectables.Enqueue(affectableList[i]);
            }    
        }
    }
    #endregion
}

public struct DirectionalMissleInitialData : IMissleInitialData
{
    public readonly UnitUID ownerUid;
    public readonly fp3 keyPoint;
}
