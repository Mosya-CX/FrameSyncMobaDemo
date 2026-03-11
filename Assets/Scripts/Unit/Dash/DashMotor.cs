using Unity.Mathematics.FixedPoint;

public sealed class DashMotor
{
    private readonly HeroUnit owner;

    private bool isDashing;
    private fp elapsed;
    private fp duration;
    private fp3 start;
    private fp3 end;
    private DashStateLock stateLock;

    public bool IsDashing => isDashing;
    public DashStateLock StateLock => stateLock;

    public DashMotor(HeroUnit owner)
    {
        this.owner = owner;
        stateLock = DashStateLock.Default;
    }

    public void StartDash(in DashSpec spec, fp3? targetPosition, UnitCore targetUnit = null)
    {
        if (owner.CrowdControlHandler.CurrentSnapshot.IsChannelBlocked(ActionChannelMask.Dash))
            return;

        fp3 from = owner.LogicPosition;
        fp3 to = from;

        switch (spec.TrajectoryType)
        {
            case DashTrajectoryType.ToPoint:
                if (targetPosition.HasValue)
                    to = targetPosition.Value;
                break;

            case DashTrajectoryType.ToTarget:
                if (targetUnit != null)
                    to = targetUnit.LogicPosition;
                else if (targetPosition.HasValue)
                    to = targetPosition.Value;
                break;

            case DashTrajectoryType.Linear:
                if (targetPosition.HasValue)
                {
                    var dir = targetPosition.Value - from;
                    if (fpmath.lengthsq(dir) > 0)
                    {
                        dir = fpmath.normalize(dir);
                        to = from + dir * spec.Distance;
                    }
                }
                break;
        }

        StartDash(from, to, spec.Duration);
    }

    public void StartDash(fp3 from, fp3 to, fp duration)
    {
        if (owner.CrowdControlHandler.CurrentSnapshot.IsChannelBlocked(ActionChannelMask.Dash))
            return;

        if (duration <= 0)
        {
            owner.LogicPosition = to;
            isDashing = false;
            return;
        }

        start = from;
        end = to;
        this.duration = duration;
        elapsed = 0;
        stateLock = DashStateLock.Default;
        isDashing = true;
    }

    public void Tick(fp dt)
    {
        if (!isDashing)
            return;

        var control = owner.CrowdControlHandler.CurrentSnapshot;
        if (control.ForceInterruptDash || control.IsChannelBlocked(ActionChannelMask.Dash))
        {
            Cancel();
            return;
        }

        elapsed += dt;
        if (elapsed >= duration)
        {
            owner.LogicPosition = end;
            isDashing = false;
            return;
        }

        fp t = elapsed / duration;
        owner.LogicPosition = fpmath.lerp(start, end, t);
    }

    public ActionLockSnapshot BuildActionLockSnapshot()
    {
        if (!isDashing)
            return ActionLockSnapshot.Default;

        return new ActionLockSnapshot
        {
            OccupiedChannels = stateLock.OccupiedChannels,
            BlockedChannels = stateLock.BlockedChannels,
        };
    }

    public bool IsInputLocked_Move() => isDashing && stateLock.LockMoveInput;
    public bool IsInputLocked_Attack() => isDashing && stateLock.LockAttackInput;
    public bool IsInputLocked_Cast() => isDashing && stateLock.LockCastInput;

    public void Cancel()
    {
        isDashing = false;
    }

    #region Snapshot
    [System.Serializable]
    public struct DashMotorSnapshot
    {
        public bool IsDashing;
        public fp Elapsed;
        public fp Duration;
        public fp3 Start;
        public fp3 End;

        public bool LockMoveInput;
        public bool LockAttackInput;
        public bool LockCastInput;
        public ActionChannelMask OccupiedChannels;
        public ActionChannelMask BlockedChannels;
    }

    public object CaptureState()
    {
        return new DashMotorSnapshot
        {
            IsDashing = isDashing,
            Elapsed = elapsed,
            Duration = duration,
            Start = start,
            End = end,

            LockMoveInput = stateLock.LockMoveInput,
            LockAttackInput = stateLock.LockAttackInput,
            LockCastInput = stateLock.LockCastInput,
            OccupiedChannels = stateLock.OccupiedChannels,
            BlockedChannels = stateLock.BlockedChannels,
        };
    }

    public void RestoreState(object state)
    {
        var snap = (DashMotorSnapshot)state;

        isDashing = snap.IsDashing;
        elapsed = snap.Elapsed;
        duration = snap.Duration;
        start = snap.Start;
        end = snap.End;

        stateLock = new DashStateLock
        {
            LockMoveInput = snap.LockMoveInput,
            LockAttackInput = snap.LockAttackInput,
            LockCastInput = snap.LockCastInput,
            OccupiedChannels = snap.OccupiedChannels,
            BlockedChannels = snap.BlockedChannels,
        };
    }
    #endregion
}