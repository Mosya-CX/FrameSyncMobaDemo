using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public class EquipmentPassiveData
{
    public int Id;
    public string Name;
    public string Description;

    [Title("基础")]
    public float Cooldown;
    public int MaxStack = 0;
    public int MaxCharge = 0;

    [Title("触发")]
    public EquipmentTriggerType TriggerType;

    [Title("条件")]
    public EquipmentConditionBase[] Conditions;

    [Title("效果")]
    public EquipmentPassiveEffectBase Effect;
}