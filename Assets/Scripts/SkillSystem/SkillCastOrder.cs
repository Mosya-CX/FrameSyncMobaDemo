using Unity.Mathematics.FixedPoint;

public sealed class SkillCastOrder : UnitOrder
{
    private readonly HeroUnit hero;
    private readonly SkillCastRequest request;

    public SkillCastRequest Request => request;

    public SkillCastOrder(HeroUnit owner, in SkillCastRequest request) : base(owner)
    {
        hero = owner;
        this.request = request;
    }

    public override void OnEnter()
    {
        if (hero == null || hero.IsDead)
        {
            IsCancelled = true;
            return;
        }

        if (!TryRefreshMovementIntent())
            IsCancelled = true;
    }

    public override void Tick(fp dt)
    {
        if (hero == null || hero.IsDead)
        {
            IsCancelled = true;
            return;
        }

        var controller = hero.GetComponent<SkillExecutionController>();
        if (controller == null)
        {
            IsCancelled = true;
            return;
        }

        if (!controller.TryPlanCast(request, out var plan))
        {
            IsCancelled = true;
            return;
        }

        if (!plan.ResolvedCast.NeedApproach)
        {
            controller.TryStartCast(request);
            IsFinished = true;
            return;
        }

        if (!TryRefreshMovementIntent())
            IsCancelled = true;
    }

    private bool TryRefreshMovementIntent()
    {
        var controller = hero.GetComponent<SkillExecutionController>();
        if (controller == null || !controller.TryPlanCast(request, out var plan))
            return false;

        if (!plan.ResolvedCast.NeedApproach)
            return true;

        if (plan.ResolvedCast.TargetUnit != null)
        {
            hero.SetTargetByOrder(plan.ResolvedCast.TargetUnit);
            return true;
        }

        if (plan.ResolvedCast.ApproachPoint.HasValue)
        {
            hero.SetDestinationByOrder(plan.ResolvedCast.ApproachPoint.Value);
            return true;
        }

        return false;
    }
}
