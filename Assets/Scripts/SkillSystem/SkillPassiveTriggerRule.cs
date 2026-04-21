using System;
using UnityEngine;
using Sirenix.OdinInspector;

[Serializable]
public sealed class SkillPassiveTriggerRule
{
    [LabelText("触发事件")]
    public SkillPassiveTriggerEvent EventType;

    [LabelText("指定技能ID（可选）")]
    public int RequiredSkillId = 0;

    [LabelText("指定标签（可选）")]
    public string RequiredTag;

    [LabelText("周期触发间隔"), ShowIf("@EventType == SkillPassiveTriggerEvent.Periodic")]
    public float PeriodSeconds = 0f;
}
