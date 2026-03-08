// EquipmentData.cs
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEquipmentData", menuName = "装备系统/新建装备配置")]
public class EquipmentData : ScriptableObject
{
    [Title("基础信息")]
    public int Id;
    public string Name;
    public string Description;
    public Sprite Icon;
    public int Value;
    public string[] Tags;

    public int[] BuildFrom;
    public int[] BuildInto;

    public bool IsFullItem;

    public EquipmentStatModifierData[] Stats;

    // 被动效果
    public EquipmentBaseEffect[] PassiveEffects;

    // 主动效果
    public EquipmentBaseEffect ActiveEffect;
}
