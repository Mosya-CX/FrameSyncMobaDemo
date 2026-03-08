using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public abstract class ProjectileMissle : BaseMissle
{
    [SerializeField, LabelText("飞行速度")]
    protected float flySpeed = 0;
    public fp FlySpeedFP => (fp)flySpeed;

    protected fp3 direction;

    public override void UpdateTransform(fp dt)
    {
        direction = UpdateDirection(dt);
        base.UpdateTransform(dt);
    }

    protected override void UpdatePosition(fp dt)
    {
        logicPosition += direction * FlySpeedFP * dt;
    }

    protected abstract fp3 UpdateDirection(fp dt);

    protected override void UpdateRotation(fp dt)
    {
        fp angle = fpmath.atan2(direction.x, direction.z);

        // 绕 Y 轴旋转的四元数 (0, sin(θ/2), 0, cos(θ/2))
        fp halfAngle = angle / 2;
        fp sinHalf = fpmath.sin(halfAngle);
        fp cosHalf = fpmath.cos(halfAngle);

        var rot = new fp4(0, sinHalf, 0, cosHalf);
        logicRotation = new fp2(rot.y, rot.w);
    }

    #region 快照和回滚
    public override object CaptureState()
    {
        var state = base.CaptureState();
        if (state is MissleSnapshot snapshot)
        {
            snapshot.stateSnapshotDict.Add(nameof(flySpeed), flySpeed);
            snapshot.stateSnapshotDict.Add(nameof(direction), direction);
        }
        return state;
    }

    public override void RestoreState(object state)
    {
        base.RestoreState(state);
        if (state is MissleSnapshot snapshot)
        {
            if (snapshot.stateSnapshotDict.TryGetValue(nameof(flySpeed), out var speedObj))
                flySpeed = (float)speedObj;
            if (snapshot.stateSnapshotDict.TryGetValue(nameof(direction), out var directionObj))
                direction = (fp3)directionObj;
        }
    }
    #endregion
}
