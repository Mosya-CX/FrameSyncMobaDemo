using Unity.Mathematics.FixedPoint;
using UnityEngine;

/// <summary>
/// 本地玩家技能指示器基类。
/// </summary>
public abstract class SkillIndicatorBase : ScriptableObject
{
    public virtual void Show(HeroUnit caster, SkillDef skill)
    {
    }

    public virtual void UpdateIndicator(
        HeroUnit caster,
        SkillDef skill,
        UnitCore hoveredUnit,
        fp3? hoveredPoint,
        fp3? aimDirection,
        bool isValid)
    {
    }

    public virtual void Hide(HeroUnit caster, SkillDef skill)
    {
    }
}
