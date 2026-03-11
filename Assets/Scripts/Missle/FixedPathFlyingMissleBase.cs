using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public abstract class FixedPathFlyingMissleBase : FlyingMissleBase
{
    [SerializeField, LabelText("命中模式")]
    protected ApplyMode applyMode = ApplyMode.Single;

    [SerializeField, LabelText("命中对象")]
    protected AffectTargetType affectType = AffectTargetType.EnemyHero | AffectTargetType.EnemyMob;

    protected readonly Queue<UnitUID> affectables = new();
    protected PathData path = new();
    protected int nextPathPoint;

    protected UnitCore Owner => UnitManager.Instance.Spawns.TryGetValue(ownerUid, out var o) ? o : null;

    public override void OnSpawn(IMissleInitialData initialData)
    {
        shouldRecycleNow = false;
        nextPathPoint = 0;
        affectables.Clear();
        path = new PathData();

        if (initialData is not DirectionalMissleInitialData data)
        {
            shouldRecycleNow = true;
            return;
        }

        ownerUid = data.OwnerUid;
        path = BakePath(data);

        if (path == null || path.PathPoints.Count == 0)
        {
            shouldRecycleNow = true;
            return;
        }

        logicPosition = path.PathPoints[0];

        if (path.PathPoints.Count > 1)
            direction = fpmath.normalize(path.PathPoints[1] - logicPosition);
    }

    public override void OnDespawn()
    {
        affectables.Clear();
    }

    protected abstract PathData BakePath(in DirectionalMissleInitialData data);
    protected abstract void Apply(UnitCore target);

    protected override fp3 UpdateDirection(fp dt, uint currentTick)
    {
        if (shouldRecycleNow)
            return fp3.zero;

        if (path == null || path.PathPoints.Count == 0)
        {
            shouldRecycleNow = true;
            return fp3.zero;
        }

        if (path.IsReachPoint(nextPathPoint, logicPosition))
        {
            nextPathPoint++;
            if (nextPathPoint >= path.PathPoints.Count)
            {
                shouldRecycleNow = true;
                return fp3.zero;
            }

            return fpmath.normalize(path.PathPoints[nextPathPoint] - logicPosition);
        }

        return direction;
    }

    protected override bool CanApply() => affectables.Count > 0;
    protected override bool IsRecycle() => shouldRecycleNow;

    protected override void OnMissleApply()
    {
        if (affectables.Count == 0)
            return;

        switch (applyMode)
        {
            case ApplyMode.Single:
                while (affectables.Count > 0)
                {
                    var uid = affectables.Dequeue();
                    if (UnitManager.Instance.Spawns.TryGetValue(uid, out var target))
                    {
                        Apply(target);
                        shouldRecycleNow = true;
                        break;
                    }
                }
                break;

            case ApplyMode.Multiple:
                while (affectables.Count > 0)
                {
                    var uid = affectables.Dequeue();
                    if (UnitManager.Instance.Spawns.TryGetValue(uid, out var target))
                        Apply(target);
                }
                break;
        }
    }

    public override void OnMissleTrigger(UnitCore target)
    {
        var owner = Owner;
        if (owner == null || target == null || target.IsDead)
            return;

        if (IsAllowedTarget(owner, target))
            affectables.Enqueue(target.UnitID);
    }

    protected virtual bool IsAllowedTarget(UnitCore owner, UnitCore target)
    {
        bool sameTeam = target.TeamID == owner.TeamID;

        if (target is HeroUnit)
            return sameTeam ? affectType.HasFlag(AffectTargetType.FriendlyHero) : affectType.HasFlag(AffectTargetType.EnemyHero);

        if (target is MinionUnit)
            return sameTeam ? affectType.HasFlag(AffectTargetType.FriendlyMob) : affectType.HasFlag(AffectTargetType.EnemyMob);

        if (target is MonsterUnit)
            return affectType.HasFlag(AffectTargetType.Monster);

        return false;
    }

    [Serializable]
    public class PathData
    {
        public List<fp3> PathPoints = new();

        public bool IsReachPoint(int index, fp3 currentPosition)
        {
            if (index < 0 || index >= PathPoints.Count)
                return false;

            return fpmath.distance(PathPoints[index], currentPosition) < 0.05m;
        }
    }

    [Flags]
    public enum AffectTargetType
    {
        None = 0,
        FriendlyHero = 1 << 0,
        FriendlyMob = 1 << 1,
        EnemyHero = 1 << 2,
        EnemyMob = 1 << 3,
        Monster = 1 << 4,
        All = ~0,
    }

    public enum ApplyMode
    {
        Single,
        Multiple,
    }

    #region Snapshot

    [Serializable]
    public class FixedPathFlyingMissleSnapshot : FlyingMissleSnapshot
    {
        public List<fp3> PathPoints = new();
        public int NextPathPoint;
        public List<UnitUID> Affectables = new();
    }

    public override object CaptureState()
    {
        var snapshot = new FixedPathFlyingMissleSnapshot
        {
            PrefabId = prefabId,
            InstanceUid = instanceUid,
            OwnerUid = ownerUid,
            Position = logicPosition,
            Rotation = logicRotation,
            Size = logicSize,
            ShouldRecycleNow = shouldRecycleNow,
            FlySpeed = flySpeed,
            Direction = direction,
            NextPathPoint = nextPathPoint,
        };

        if (path != null)
            snapshot.PathPoints.AddRange(path.PathPoints);

        snapshot.Affectables.AddRange(affectables);

        return snapshot;
    }

    public override void RestoreState(object state)
    {
        if (state is not FixedPathFlyingMissleSnapshot snapshot)
            return;

        base.RestoreState(snapshot);

        path = new PathData();
        path.PathPoints.AddRange(snapshot.PathPoints);

        nextPathPoint = snapshot.NextPathPoint;

        affectables.Clear();
        for (int i = 0; i < snapshot.Affectables.Count; i++)
            affectables.Enqueue(snapshot.Affectables[i]);
    }

    #endregion
}

public struct DirectionalMissleInitialData : IMissleInitialData
{
    public UnitUID OwnerUid { get; }
    public fp3 KeyPoint { get; }

    public DirectionalMissleInitialData(UnitCore owner, fp3 keyPoint)
    {
        OwnerUid = owner.UnitID;
        KeyPoint = keyPoint;
    }
}