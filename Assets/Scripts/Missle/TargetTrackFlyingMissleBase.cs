using Unity.Mathematics.FixedPoint;
using System;

public abstract class TargetTrackFlyingMissleBase : FlyingMissleBase
{
    protected UnitUID targetUid;
    protected bool canApply;
    protected bool isApplied;

    protected UnitCore Owner => UnitManager.Instance.Spawns.TryGetValue(ownerUid, out var o) ? o : null;
    protected UnitCore Target => UnitManager.Instance.Spawns.TryGetValue(targetUid, out var t) ? t : null;

    public override void OnSpawn(IMissleInitialData initialData)
    {
        shouldRecycleNow = false;
        canApply = false;
        isApplied = false;

        if (initialData is not TargetTrackMissleInitialData data)
        {
            shouldRecycleNow = true;
            return;
        }

        ownerUid = data.OwnerUid;
        targetUid = data.TargetUid;

        var owner = Owner;
        if (owner != null)
            logicPosition = owner.LogicPosition;

        var target = Target;
        if (target != null)
            direction = fpmath.normalize(target.LogicPosition - logicPosition);
    }

    public override void OnDespawn()
    {
    }

    protected override fp3 UpdateDirection(fp dt, uint currentTick)
    {
        if (Target == null || Target.IsDead)
        {
            shouldRecycleNow = true;
            return fp3.zero;
        }

        return fpmath.normalize(Target.LogicPosition - logicPosition);
    }

    protected override bool CanApply() => canApply;

    protected override bool IsRecycle() => isApplied || shouldRecycleNow;

    protected override void OnMissleApply()
    {
        isApplied = true;
    }

    public override void OnMissleTrigger(UnitCore target)
    {
        if (Target != null && target == Target)
            canApply = true;
    }

    #region Snapshot

    [Serializable]
    public class TargetTrackFlyingMissleSnapshot : FlyingMissleSnapshot
    {
        public UnitUID TargetUid;
        public bool CanApply;
        public bool IsApplied;
    }

    public override object CaptureState()
    {
        return new TargetTrackFlyingMissleSnapshot
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
            TargetUid = targetUid,
            CanApply = canApply,
            IsApplied = isApplied,
        };
    }

    public override void RestoreState(object state)
    {
        if (state is not TargetTrackFlyingMissleSnapshot snapshot)
            return;

        base.RestoreState(snapshot);
        targetUid = snapshot.TargetUid;
        canApply = snapshot.CanApply;
        isApplied = snapshot.IsApplied;
    }

    #endregion
}

public struct TargetTrackMissleInitialData : IMissleInitialData
{
    public UnitUID OwnerUid { get; }
    public UnitUID TargetUid { get; }

    public TargetTrackMissleInitialData(UnitCore source, UnitCore target)
    {
        OwnerUid = source.UnitID;
        TargetUid = target.UnitID;
    }
}