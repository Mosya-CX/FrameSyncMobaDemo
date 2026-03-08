using Unity.Mathematics.FixedPoint;

public abstract class TargetTrackMissle : ProjectileMissle
{
    protected UnitUID sourceUid;
    protected UnitUID targetUid;

    protected UnitCore owner => UnitManager.Instance.Spawns.ContainsKey(sourceUid) ? UnitManager.Instance.Spawns[sourceUid] : null;
    protected UnitCore target => UnitManager.Instance.Spawns.ContainsKey(targetUid) ? UnitManager.Instance.Spawns[targetUid] : null;

    protected bool canApply;
    protected bool isApplied;

    public override void OnDespawn() { }

    public override void OnSpawn(IMissleInitialData initialData)
    {
        if (initialData == null || !(initialData is TargetTrackMissleInitialData data))
        {
            MissleManager.Instance.Recycle(this);
            return;
        }
        sourceUid = data.sourceUid;
        targetUid = data.sourceUid;
    }

    protected override bool IsRecycle() => isApplied;
    protected override bool CanApply() => canApply;
    protected override fp3 UpdateDirection(fp dt) => fpmath.normalize(target.LogicPosition - logicPosition);

    protected override void OnMissleApply() => isApplied = true;

    public override void OnMissleTrigger(UnitCore target)
    {
        if (this.target == target)
            canApply = true;
    }

    #region ¿ìÕÕºÍ»Ø¹ö
    public override object CaptureState()
    {
        var state = base.CaptureState();
        if (state is MissleSnapshot snapshot)
        {
            snapshot.stateSnapshotDict.Add(nameof(sourceUid), sourceUid);
            snapshot.stateSnapshotDict.Add(nameof(targetUid), targetUid);
            snapshot.stateSnapshotDict.Add(nameof(canApply), canApply);
            snapshot.stateSnapshotDict.Add(nameof(isApplied), isApplied);
        }
        return state;
    }

    public override void RestoreState(object state)
    {
        base.RestoreState(state);
        if (state is MissleSnapshot snapshot)
        {
            if (snapshot.stateSnapshotDict.TryGetValue(nameof(sourceUid), out var sourceUidObj))
                sourceUid = (UnitUID)sourceUidObj;
            if (snapshot.stateSnapshotDict.TryGetValue(nameof(targetUid), out var targetUidObj))
                targetUid = (UnitUID)targetUidObj;
            if (snapshot.stateSnapshotDict.TryGetValue(nameof(canApply), out var canApplyObj))
                canApply = (bool)canApplyObj;
            if (snapshot.stateSnapshotDict.TryGetValue(nameof(isApplied), out var isAppliedObj))
                isApplied = (bool)isAppliedObj;
        }
    }
    #endregion
}

public struct TargetTrackMissleInitialData : IMissleInitialData
{
    public readonly UnitUID sourceUid;
    public readonly UnitUID targetUid;

    public TargetTrackMissleInitialData(UnitCore source, UnitCore target)
    {
        sourceUid = source.UnitID;
        targetUid = target.UnitID;
    }
}