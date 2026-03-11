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

    [Title("合成关系")]
    public int[] BuildFrom;
    public int[] BuildInto;
    public bool IsFullItem;

    [Title("基础属性")]
    public EquipmentStatModifierData[] Stats;

    [Title("被动效果")]
    public EquipmentPassiveData[] Passives;

    [Title("主动效果")]
    public EquipmentActiveData Active;
}
