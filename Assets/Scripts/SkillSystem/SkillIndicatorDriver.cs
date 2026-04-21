using UnityEngine;

[RequireComponent(typeof(HeroUnit))]
public sealed class SkillIndicatorDriver : MonoBehaviour
{
    private HeroUnit owner;
    private SkillDef currentSkill;

    private void Awake()
    {
        owner = GetComponent<HeroUnit>();
    }

    public void Show(LocalSkillCastSession session)
    {
        if (session == null || session.Caster == null || session.Skill == null)
            return;

        Hide(session);

        currentSkill = session.Skill;
        currentSkill.Indicator?.Show(session.Caster, currentSkill);
    }

    public void UpdateFromSession(LocalSkillCastSession session)
    {
        if (session == null || session.Caster == null || session.Skill == null)
            return;

        if (currentSkill != session.Skill)
        {
            Hide(session);
            currentSkill = session.Skill;
            currentSkill.Indicator?.Show(session.Caster, currentSkill);
        }

        currentSkill.Indicator?.UpdateIndicator(
            session.Caster,
            currentSkill,
            session.HoveredUnit,
            session.HoveredPoint,
            session.AimDirection,
            session.IsValid);
    }

    public void Hide(LocalSkillCastSession session)
    {
        if (currentSkill != null && currentSkill.Indicator != null && session != null && session.Caster != null)
            currentSkill.Indicator.Hide(session.Caster, currentSkill);

        currentSkill = null;
    }

    public void HideCurrent()
    {
        if (currentSkill != null && currentSkill.Indicator != null && owner != null)
            currentSkill.Indicator.Hide(owner, currentSkill);

        currentSkill = null;
    }
}
