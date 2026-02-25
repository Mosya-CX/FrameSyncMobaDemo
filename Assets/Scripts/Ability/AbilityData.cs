using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

[CreateAssetMenu(menuName = "技能系统/新建技能配置")]
public class AbilityData : ScriptableObject
{
    public int AbilityId;
    public string AbilityName;

    public SkillTriggerMode TriggerMode;

    public bool RefundOnInterrupt;
    public fp RefundPercent;

    public fp GlobalCooldown;

    public List<AbilityLevelData> Levels;
    public List<SkillPhase> Phases;
}

[System.Serializable]
public class AbilityLevelData
{
    public fp ManaCost;
    public fp Cooldown;
    public List<fp> Parameters;
}

[System.Serializable]
public class SkillPhase
{
    public fp PreCastTime;
    public fp ChannelTime;
    public fp RecoverTime;

    public List<AbilityBaseMoudle> Modules;
    public List<SkillIndicatorModule> IndicatorModules;
}