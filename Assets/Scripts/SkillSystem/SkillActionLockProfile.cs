using System;
using UnityEngine;
using Sirenix.OdinInspector;

[Serializable]
public sealed class SkillActionLockProfile
{
    [LabelText("启用动作锁")]
    public bool Enabled = true;

    [LabelText("占用动作通道")]
    public ActionChannelMask OccupiedChannels = ActionChannelMask.Move | ActionChannelMask.Attack | ActionChannelMask.Track | ActionChannelMask.Rotate;

    [LabelText("额外阻断通道")]
    public ActionChannelMask BlockedChannels = ActionChannelMask.None;

    public static SkillActionLockProfile DefaultFor(SkillExecutionLane lane)
    {
        var profile = new SkillActionLockProfile();

        switch (lane)
        {
            case SkillExecutionLane.Main:
                profile.OccupiedChannels = ActionChannelMask.Cast;
                break;
            case SkillExecutionLane.Mobility:
                profile.OccupiedChannels = ActionChannelMask.Cast | ActionChannelMask.Dash;
                break;
            case SkillExecutionLane.Overlay:
            case SkillExecutionLane.Passive:
                profile.OccupiedChannels = ActionChannelMask.None;
                break;
        }

        return profile;
    }
}
