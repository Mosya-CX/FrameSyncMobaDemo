using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "单位定义配置")]
public class UnitDefinition : ScriptableObject
{
    [BoxGroup("属性"), LabelText("基础生命")]
    public uint baseHealth;
    [BoxGroup("属性"), LabelText("生命成长")]
    public float healthGrowth;
    [BoxGroup("属性"), LabelText("生命回复")]
    public float baseHealthRegen;
    [BoxGroup("属性"), LabelText("基础法力")]
    public uint baseMana;
    [BoxGroup("属性"), LabelText("法力成长")]
    public float manaGrowth;
    [BoxGroup("属性"), LabelText("法力回复")]
    public float baseManaRegen;
    [BoxGroup("属性"), LabelText("基础攻击力")]
    public uint baseAttackDamage;
    [BoxGroup("属性"), LabelText("攻击成长")]
    public float attackGrowth;
    [BoxGroup("属性"), LabelText("基础法强")]
    public uint baseAbilityPower;
    [BoxGroup("属性"), LabelText("法强成长")]
    public float abilityGrowth;
    [BoxGroup("属性"), LabelText("基础攻速")]
    public float baseAttackSpeed;
    [BoxGroup("属性"), LabelText("攻速成长")]
    public float attackSpeedGrowth;
    [BoxGroup("属性"), LabelText("攻击距离")]
    public float baseAttackRange;
    [BoxGroup("属性"), LabelText("基础暴击率")]
    public float baseCritChance;
    [BoxGroup("属性"), LabelText("基础暴击伤害")]
    public float baseCritDamage;
    [BoxGroup("属性"), LabelText("基础护甲")]
    public uint baseArmor;
    [BoxGroup("属性"), LabelText("护甲成长")]
    public float armorGrowth;
    [BoxGroup("属性"), LabelText("基础魔抗")]
    public uint baseMagicResist;
    [BoxGroup("属性"), LabelText("魔抗成长")]
    public float magicResistGrowth;
    [BoxGroup("属性"), LabelText("基础移速")]
    public float baseMoveSpeed;

    [BoxGroup("其它"), LabelText("技能列表")]
    public AbilityData[] abilityList;
}