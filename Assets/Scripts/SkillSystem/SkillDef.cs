using UnityEngine;
using Sirenix.OdinInspector;

public enum SkillTargetMode : byte
{
    None,
    Unit,
    Point,
    Direction,
    PointOrUnit,
}

public enum SkillRangePolicy : byte
{
    MustInRange,
    AutoApproach,
    ClampToRange,
}

[CreateAssetMenu(fileName = "SkillDef", menuName = "SkillSystem/Skill Def")]
public sealed class SkillDef : ScriptableObject
{
    [TitleGroup("基础"), LabelText("技能ID")]
    public int Id;

    [TitleGroup("基础"), LabelText("技能名")]
    public string SkillName;

    [TitleGroup("基础"), LabelText("描述"), TextArea]
    public string Description;

    [TitleGroup("基础"), LabelText("是否被动")]
    public bool IsPassive = false;

    [TitleGroup("被动"), LabelText("被动模式"), ShowIf(nameof(IsPassive))]
    public SkillPassiveMode PassiveMode = SkillPassiveMode.None;

    [TitleGroup("被动"), LabelText("触发规则"), ShowIf("@IsPassive && PassiveMode == SkillPassiveMode.Triggered")]
    [ListDrawerSettings(Expanded = true)]
    public SkillPassiveTriggerRule[] PassiveTriggers;

    [TitleGroup("Runtime 生命周期"), LabelText("Runtime 创建回调")]
    [ListDrawerSettings(Expanded = true)]
    public SkillRuntimeCallbackNode[] OnCreate;

    [TitleGroup("Runtime 生命周期"), LabelText("Runtime 销毁回调")]
    [ListDrawerSettings(Expanded = true)]
    public SkillRuntimeCallbackNode[] OnExit;

    [TitleGroup("目标"), LabelText("目标模式")]
    public SkillTargetMode TargetMode;

    [TitleGroup("目标"), LabelText("射程策略")]
    public SkillRangePolicy RangePolicy = SkillRangePolicy.MustInRange;

    [TitleGroup("目标"), LabelText("施法距离")]
    public float CastRange = 0f;

    [TitleGroup("执行"), LabelText("执行通道")]
    public SkillExecutionLane ExecutionLane = SkillExecutionLane.Main;

    [TitleGroup("执行"), LabelText("默认动作锁"), InlineProperty, HideLabel]
    public SkillActionLockProfile DefaultActionLock = new SkillActionLockProfile();

    [TitleGroup("基础条件"), LabelText("检查控制阻断")]
    public bool CheckControlBlocked = true;

    [TitleGroup("基础条件"), LabelText("检查冷却")]
    public bool CheckCooldown = true;

    [TitleGroup("基础条件"), LabelText("检查资源")]
    public bool CheckManaCost = true;

    [TitleGroup("基础条件"), LabelText("启动时自动扣蓝")]
    public bool AutoPayManaOnStart = true;

    [TitleGroup("消耗"), LabelText("冷却")]
    public float Cooldown = 0f;

    [TitleGroup("消耗"), LabelText("蓝耗")]
    public int ManaCost = 0;

    [TitleGroup("消耗"), LabelText("正常结束自动进入冷却")]
    public bool AutoStartCooldownOnFinish = true;

    [TitleGroup("多段重施法"), LabelText("启用多段重施法")]
    public bool UseRepeatCast = false;

    [TitleGroup("多段重施法"), LabelText("续接窗口"), ShowIf(nameof(UseRepeatCast))]
    public float RepeatCastWindow = 0f;

    [TitleGroup("多段重施法"), LabelText("续接超时进入冷却"), ShowIf(nameof(UseRepeatCast))]
    public bool StartCooldownOnRepeatTimeout = true;

    [TitleGroup("表现"), LabelText("指示器")]
    public SkillIndicatorBase Indicator;

    [TitleGroup("步骤"), LabelText("技能步骤")]
    [ListDrawerSettings(Expanded = true)]
    public SkillStepDef[] Steps;

    [TitleGroup("窗口"), LabelText("技能窗口")]
    [ListDrawerSettings(Expanded = true)]
    public SkillWindowDef[] Windows;
}
