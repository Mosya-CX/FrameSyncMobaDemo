using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "DirectDynamicTargetDamageNode", menuName = "SkillSystem/Effects/Common/Direct Dynamic Target Damage")]
public sealed class DirectDynamicTargetDamageNode : SkillEffectNode
{
    [TitleGroup("目标"), LabelText("目标模式")]
    public SkillSimpleTargetResolveMode TargetMode = SkillSimpleTargetResolveMode.ResolvedTargetUnit;

    [TitleGroup("物理伤害"), LabelText("每级基础值")]
    public float[] PhysicalBaseBySkillLevel;

    [TitleGroup("物理伤害"), LabelText("总攻击力系数")]
    public float PhysicalAttackDamageRatio = 0f;

    [TitleGroup("物理伤害"), LabelText("法强系数")]
    public float PhysicalAbilityPowerRatio = 0f;

    [TitleGroup("物理伤害"), LabelText("施法者最大生命值系数")]
    public float PhysicalCasterMaxHealthRatio = 0f;

    [TitleGroup("物理伤害"), LabelText("目标最大生命值系数")]
    public float PhysicalTargetMaxHealthRatio = 0f;

    [TitleGroup("魔法伤害"), LabelText("每级基础值")]
    public float[] MagicalBaseBySkillLevel;

    [TitleGroup("魔法伤害"), LabelText("总攻击力系数")]
    public float MagicalAttackDamageRatio = 0f;

    [TitleGroup("魔法伤害"), LabelText("法强系数")]
    public float MagicalAbilityPowerRatio = 0f;

    [TitleGroup("魔法伤害"), LabelText("施法者最大生命值系数")]
    public float MagicalCasterMaxHealthRatio = 0f;

    [TitleGroup("魔法伤害"), LabelText("目标最大生命值系数")]
    public float MagicalTargetMaxHealthRatio = 0f;

    [TitleGroup("通用"), LabelText("额外标签")]
    public string[] AdditionalTags;

    public override void Execute(SkillExecution execution, SkillEffectContext context)
    {
        if (context.Caster == null)
            return;

        UnitCore target = TargetMode switch
        {
            SkillSimpleTargetResolveMode.Self => context.Caster,
            SkillSimpleTargetResolveMode.ResolvedTargetUnit => context.TargetUnit,
            _ => null
        };

        if (target == null || target.IsDead)
            return;

        fp physical = DirectDamageIndexedBlackboardTargetsNode_Evaluator.Evaluate(
            context.Caster, target, context.Runtime,
            PhysicalBaseBySkillLevel, PhysicalAttackDamageRatio, PhysicalAbilityPowerRatio, PhysicalCasterMaxHealthRatio, PhysicalTargetMaxHealthRatio);

        fp magical = DirectDamageIndexedBlackboardTargetsNode_Evaluator.Evaluate(
            context.Caster, target, context.Runtime,
            MagicalBaseBySkillLevel, MagicalAttackDamageRatio, MagicalAbilityPowerRatio, MagicalCasterMaxHealthRatio, MagicalTargetMaxHealthRatio);

        if (physical <= fp.zero && magical <= fp.zero)
            return;

        DamageManager.Instance.CreateAbilityDamageRequest(context.Caster, target, physical, magical, AdditionalTags);
    }
}

internal static class DirectDamageIndexedBlackboardTargetsNode_Evaluator
{
    public static fp Evaluate(
        UnitCore caster,
        UnitCore target,
        SkillRuntime runtime,
        float[] baseBySkillLevel,
        float attackDamageRatio,
        float abilityPowerRatio,
        float casterMaxHealthRatio,
        float targetMaxHealthRatio)
    {
        fp value = fp.zero;

        if (baseBySkillLevel != null && baseBySkillLevel.Length > 0)
        {
            int skillLevel = runtime != null ? Mathf.Clamp(runtime.Level, 1, baseBySkillLevel.Length) : 1;
            value += (fp)baseBySkillLevel[Mathf.Clamp(skillLevel - 1, 0, baseBySkillLevel.Length - 1)];
        }

        if (caster != null)
        {
            value += caster.Stats.Get(UnitStatType.AttackDamage) * (fp)attackDamageRatio;
            value += caster.Stats.Get(UnitStatType.AbilityPower) * (fp)abilityPowerRatio;
            value += caster.Stats.Get(UnitStatType.MaxHealth) * (fp)casterMaxHealthRatio;
        }

        if (target != null)
            value += target.Stats.Get(UnitStatType.MaxHealth) * (fp)targetMaxHealthRatio;

        return value;
    }
}
