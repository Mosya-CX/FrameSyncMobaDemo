using System;
using Sirenix.OdinInspector;

[Serializable]
public sealed class SkillWindowDef
{
    [LabelText("进入技能ID")]
    public int IncomingSkillId;

    [LabelText("窗口类型")]
    public SkillWindowType WindowType = SkillWindowType.None;
}
