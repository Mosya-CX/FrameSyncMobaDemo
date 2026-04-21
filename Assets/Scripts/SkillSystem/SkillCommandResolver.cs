public static class SkillCommandResolver
{
    public static bool TrySubmit(UnitCore caster, in SkillCastRequest request)
    {
        if (caster == null)
            return false;

        var controller = caster.GetComponent<SkillExecutionController>();
        if (controller == null)
            return false;

        if (!controller.TryPlanCast(request, out var plan))
            return false;

        if (plan.ResolvedCast.NeedApproach && caster is HeroUnit hero && hero.OrderController != null)
        {
            hero.OrderController.Submit(new SkillCastOrder(hero, request));
            return true;
        }

        return controller.TryStartCast(request);
    }
}
