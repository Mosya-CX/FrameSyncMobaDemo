using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "DamageIndexedBlackboardTargetsNode", menuName = "SkillSystem/Effects/Common/Damage Indexed Blackboard Targets")]
public sealed class DamageIndexedBlackboardTargetsNode : SkillEffectNode
{
    [LabelText("数量键")]
    public string CountKey = "Targets.Count";

    [LabelText("目标前缀")]
    public string TargetPrefix = "Targets";

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

    [LabelText("伤害倍率")]
    public float DamageMultiplier = 1f;

    [LabelText("额外标签")]
    public string[] AdditionalTags;

    public override void Execute(SkillExecution execution, SkillEffectContext context)
    {
        if (context.Caster == null || context.Blackboard == null)
            return;

        if (!context.Blackboard.TryGet(CountKey, out int count) || count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            if (!context.Blackboard.TryGet($"{TargetPrefix}_{i}", out UnitUID uid))
                continue;

            if (!UnitManager.Instance.Spawns.TryGetValue(uid, out var target))
                continue;

            if (target == null || target.IsDead)
                continue;

            fp physical = Evaluate(
                context.Caster, target, context.Runtime,
                PhysicalBaseBySkillLevel,
                PhysicalAttackDamageRatio,
                PhysicalAbilityPowerRatio,
                PhysicalCasterMaxHealthRatio,
                PhysicalTargetMaxHealthRatio);

            fp magical = Evaluate(
                context.Caster, target, context.Runtime,
                MagicalBaseBySkillLevel,
                MagicalAttackDamageRatio,
                MagicalAbilityPowerRatio,
                MagicalCasterMaxHealthRatio,
                MagicalTargetMaxHealthRatio);

            physical *= (fp)DamageMultiplier;
            magical *= (fp)DamageMultiplier;

            if (physical <= fp.zero && magical <= fp.zero)
                continue;

            DamageManager.Instance.CreateAbilityDamageRequest(
                context.Caster,
                target,
                physical,
                magical,
                AdditionalTags);
        }
    }

    private static fp Evaluate(
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
