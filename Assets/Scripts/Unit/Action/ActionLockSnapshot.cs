public struct ActionLockSnapshot
{
    public ActionChannelMask OccupiedChannels;
    public ActionChannelMask BlockedChannels;

    public static ActionLockSnapshot Default => new ActionLockSnapshot
    {
        OccupiedChannels = ActionChannelMask.None,
        BlockedChannels = ActionChannelMask.None,
    };

    public bool IsBlocked(ActionChannelMask channel)
    {
        return (BlockedChannels & channel) != 0;
    }
}