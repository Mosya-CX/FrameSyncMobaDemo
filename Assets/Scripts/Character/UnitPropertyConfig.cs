using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "单位基础属性配置")]
public class UnitPropertyConfig : ScriptableObject
{
    [BoxGroup("生命"), LabelText("基础生命")]
    public uint baseHealth;
    [BoxGroup("生命"), LabelText("生命成长")]
    public float healthGrowth;
    [BoxGroup("生命"), LabelText("生命回复")]
    public float baseHealthRegen;

    [BoxGroup("法力"), LabelText("基础法力")]
    public uint baseMana;
    [BoxGroup("法力"), LabelText("法力成长")]
    public float manaGrowth;
    [BoxGroup("法力"), LabelText("法力回复")]
    public float baseManaRegen;

    [BoxGroup("攻击"), LabelText("基础攻击力")]
    public uint baseAttackDamage;
    [BoxGroup("攻击"), LabelText("攻击成长")]
    public float attackGrowth;
    [BoxGroup("攻击"), LabelText("基础法强")]
    public uint baseAbilityPower;
    [BoxGroup("攻击"), LabelText("法强成长")]
    public float abilityGrowth;
    [BoxGroup("攻击"), LabelText("基础攻速")]
    public float baseAttackSpeed;
    [BoxGroup("攻击"), LabelText("攻速成长")]
    public float attackSpeedGrowth;
    [BoxGroup("攻击"), LabelText("攻击距离")]
    public float baseAttackRange;

    [BoxGroup("暴击"), LabelText("基础暴击率")]
    public float baseCritChance;
    [BoxGroup("暴击"), LabelText("基础暴击伤害")]
    public float baseCritDamage;

    [BoxGroup("防御"), LabelText("基础护甲")]
    public uint baseArmor;
    [BoxGroup("防御"), LabelText("护甲成长")]
    public float armorGrowth;
    [BoxGroup("防御"), LabelText("基础魔抗")]
    public uint baseMagicResist;
    [BoxGroup("防御"), LabelText("魔抗成长")]
    public float magicResistGrowth;

    [BoxGroup("其它"), LabelText("基础移速")]
    public float baseMoveSpeed;
}