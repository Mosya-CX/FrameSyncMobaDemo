using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "BuffAddStatModifierBySkillLevelModule", menuName = "SkillSystem/Buff/Add Stat Modifier By Skill Level")]
public sealed class BuffAddStatModifierBySkillLevelModule : BuffBaseModule
{
    [LabelText("技能ID")]
    public int SkillId;

    [LabelText("属性")]
    public UnitStatType StatType = UnitStatType.AttackDamage;

    [LabelText("修正类型")]
    public StatModifierType ModifierType = StatModifierType.PercentAdd;

    [LabelText("每级数值")]
    public float[] ValuesBySkillLevel;

    public override void Apply(BuffCallbackContext context)
    {
        if (context?.Handler == null || context.Buff?.target == null)
            return;

        var skillBook = context.Buff.target.GetComponent<SkillBook>();
        if (skillBook == null || !skillBook.TryGetRuntime(SkillId, out var runtime))
            return;

        int index = ValuesBySkillLevel != null && ValuesBySkillLevel.Length > 0
            ? Mathf.Clamp(runtime.Level, 1, ValuesBySkillLevel.Length) - 1
            : 0;

        fp value = ValuesBySkillLevel != null && ValuesBySkillLevel.Length > 0
            ? (fp)ValuesBySkillLevel[index]
            : fp.zero;

        context.Handler.AddStatModifier(context.Buff, StatType, ModifierType, value);
    }
}
