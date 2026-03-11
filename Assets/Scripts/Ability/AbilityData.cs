using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "技能系统/新建技能配置")]
public class AbilityData : ScriptableObject
{
    [Title("基础")]
    public int Id;
    public string Name;
    public string Description;

    [Title("成长")]
    public AbilityLevelData[] Levels;

    [Title("门禁")]
    public AbilityBaseCondition[] TriggerConditions;

    [Title("目标")]
    public AbilityTargetMode TargetMode = AbilityTargetMode.PointOrUnit;
    public float CastRange = 6f;
    public bool AllowAutoApproach = true;

    [Title("行为规则")]
    public bool Queueable = true;
    public bool ResumeSuspendedOrderIfNoBufferedCast = true;
    public bool CancelByMove = true;
    public bool CancelByAttack = true;
    public bool CancelByCast = true;
    public bool CancelByStop = true;
    public bool CancelByHardControl = true;

    [Title("执行段")]
    public CastStageData[] Stages;

    [Title("指示器")]
    public AbilityIndicatorBase Indicator;
    public LocalCastInteractionType LocalInteractionType = LocalCastInteractionType.PressOrRelease;
}

[System.Serializable]
public class AbilityLevelData
{
    public float Cooldown;
    public SerializedDictionary<string, float> Parameters;
}

public enum AbilityTargetMode
{
    None,
    Point,
    Unit,
    PointOrUnit,
    Direction,
}

public enum LocalCastInteractionType
{
    Instant,
    PressOrRelease,
    HoldAndRelease,
}

[System.Serializable]
public class CastStageData
{
    public CastStageType Type;
    public float Duration;

    [Title("阶段控制")]
    public bool AllowMoveDuringStage;
    public bool AllowRotateDuringStage = true;

    [Title("插入窗口")]
    public CastWindowRule[] CastWindows;

    [Title("进入")]
    public AbilityBaseMoudle[] OnEnter;

    [Title("持续")]
    public AbilityBaseMoudle[] OnTick;

    [Title("退出")]
    public AbilityBaseMoudle[] OnExit;
}

public enum CastStageType : byte
{
    None,
    Windup,
    Execute,
    Channel,
    Recovery,
}

public enum CastWindowType : byte
{
    QueueOnly,      // 仅排队
    ReplaceCurrent, // 直接打断替换
    InsertBeforeExecute, // 插入后返回当前施法（如剑魔QE）
}

[System.Serializable]
public class CastWindowRule
{
    public int AbilityId;
    public CastWindowType Type = CastWindowType.QueueOnly;
}