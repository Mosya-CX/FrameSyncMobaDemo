using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public class EquipmentActiveData
{
    public int Id;
    public string Name;
    public string Description;

    [Title("基础")]
    public float Cooldown;

    [Title("目标")]
    public EquipmentActiveTargetMode TargetMode = EquipmentActiveTargetMode.None;
    public float CastRange = 0;

    [Title("条件")]
    public EquipmentConditionBase[] Conditions;

    [Title("效果")]
    public EquipmentActiveEffectBase Effect;
}

public enum EquipmentActiveTargetMode : byte
{
    None,
    Point,
    Unit,
    PointOrUnit,
    Direction,
}