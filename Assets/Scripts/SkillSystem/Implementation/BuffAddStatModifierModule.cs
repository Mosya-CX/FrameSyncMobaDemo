using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "BuffAddStatModifierModule", menuName = "SkillSystem/Buff/Add Stat Modifier")]
public sealed class BuffAddStatModifierModule : BuffBaseModule
{
    [LabelText("属性")]
    public UnitStatType StatType = UnitStatType.AttackRange;

    [LabelText("修正类型")]
    public StatModifierType ModifierType = StatModifierType.Flat;

    [LabelText("固定值")]
    public float Value = 0f;

    public override void Apply(BuffCallbackContext context)
    {
        if (context?.Handler == null || context.Buff == null)
            return;

        context.Handler.AddStatModifier(context.Buff, StatType, ModifierType, (fp)Value);
    }
}
