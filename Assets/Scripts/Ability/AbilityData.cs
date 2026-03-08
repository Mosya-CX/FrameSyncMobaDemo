using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "技能系统/新建技能配置")]
public class AbilityData : ScriptableObject
{
    [Title("基础")]
    public int Id;
    public string Name;
    public string Description;

    [Title("成长性")]
    public AbilityLevelData[] Levels;

    [Title("条件和返还")]
    public AbilityBaseCondition[] TriggerConditions;
    [Range(0, 1)]
    public float CancelReturnCooldownPercent;

    [Title("技能段")]
    public AbilityPhase[] Phases;

    [Title("冷却开始时机")]
    public AbilityStartCooldownTiming CooldownApplyTiming = AbilityStartCooldownTiming.OnEnterPhase;
    [ValueDropdown(nameof(GetPhaseIndexList))]
    public int StartCooldownPhase;

    private int[] GetPhaseIndexList()
    {
        if (Phases == null)
            return new int[0];

        var list = new int[Phases.Length];
        for (int i = 0; i < Phases.Length; i++)
            list[i] = i;
        return list;
    }
}

[System.Serializable]
public class AbilityLevelData
{
    public float Cooldown;
    public SerializedDictionary<string, float> Parameters;
}

[System.Serializable]
public class AbilityPhase
{
    public Sprite Icon;
    public string[] Tags;
    public bool IsPersistent;
    public float PhaseKeepDuration;

    public AbilityBaseMoudle[] OnPhaseEnter;
    public AbilityBaseMoudle[] OnPhaseTick;
    public AbilityBaseMoudle[] OnPhaseExit;
    public AbilityBaseMoudle[] OnPhaseTrigger;

    [Title("前摇")]
    public float PrecastDuration;
    public AbilityBaseMoudle[] OnPrecastEnter;
    public AbilityBaseMoudle[] OnPrecastTick;
    public AbilityBaseMoudle[] OnPrecastExit;

    [Title("引导")]
    public float ChannelingDuration;
    public AbilityBaseMoudle[] OnChannelingEnter;
    public AbilityBaseMoudle[] OnChannelingTick;
    public AbilityBaseMoudle[] OnChannelingExit;
    public AbilityBaseMoudle[] OnChannelingTimeOut;
    public bool CanTriggerChanneling;
    public AbilityBaseMoudle[] OnChannelingTrigger;
    public float ChannelingTriggerCooldown;
    public short ChannelingRecycleTriggerChance = 1;

    //[Title("后摇")]
    //public float RecoveryDuration;
}

public enum AbilityChannelingMode
{
    Default,// 默认
    Charge,// 蓄力型
    Recycle,// 可重复使用的
}

public enum AbilityStartCooldownTiming
{
    OnEnterPhase,
    OnExitPhase,
    //OnEnterPrecast,
    //OnExitPrecast,
    //OnEnterChanneling,
    //OnExitChanneling,
    //OnEnterKeep,
    //OnExitKeep,
}