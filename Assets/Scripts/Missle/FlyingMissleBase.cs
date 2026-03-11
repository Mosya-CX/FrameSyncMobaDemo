using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using System;

public abstract class FlyingMissleBase : BaseMissle
{
    [SerializeField, LabelText("·ÉÐÐËÙ¶È")]
    protected float flySpeed = 10f;
    public fp FlySpeedFP => (fp)flySpeed;

    protected fp3 direction;

    public override void UpdateTransform(fp dt, uint currentTick)
    {
        direction = UpdateDirection(dt, currentTick);
        base.UpdateTransform(dt, currentTick);
    }

    protected override void UpdatePosition(fp dt, uint currentTick)
    {
        logicPosition += direction * FlySpeedFP * dt;
    }

    protected override void UpdateRotation(fp dt, uint currentTick)
    {
        if (fpmath.lengthsq(direction) <= 0)
            return;

        fp angle = fpmath.atan2(direction.x, direction.z);
        fp halfAngle = angle / 2;
        fp sinHalf = fpmath.sin(halfAngle);
        fp cosHalf = fpmath.cos(halfAngle);

        var rot = new fp4(0, sinHalf, 0, cosHalf);
        logicRotation = new fp2(rot.y, rot.w);
    }

    protected abstract fp3 UpdateDirection(fp dt, uint currentTick);

    #region Snapshot

    [Serializable]
    public class FlyingMissleSnapshot : MissleSnapshot
    {
        public float FlySpeed;
        public fp3 Direction;
    }

    public override object CaptureState()
    {
        return new FlyingMissleSnapshot
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
        };
    }

    public override void RestoreState(object state)
    {
        if (state is not FlyingMissleSnapshot snapshot)
            return;

        base.RestoreState(snapshot);
        flySpeed = snapshot.FlySpeed;
        direction = snapshot.Direction;
    }

    #endregion
}