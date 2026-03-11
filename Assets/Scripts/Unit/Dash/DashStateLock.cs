public struct DashStateLock
{
    public bool LockMoveInput;
    public bool LockAttackInput;
    public bool LockCastInput;

    public ActionChannelMask OccupiedChannels;
    public ActionChannelMask BlockedChannels;

    public static DashStateLock Default => new DashStateLock
    {
        LockMoveInput = true,
        LockAttackInput = true,
        LockCastInput = true,
        OccupiedChannels = ActionChannelMask.Dash,
        BlockedChannels = ActionChannelMask.Move | ActionChannelMask.Track | ActionChannelMask.Attack | ActionChannelMask.Cast,
    };
}