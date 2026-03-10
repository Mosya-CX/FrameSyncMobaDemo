using Unity.Mathematics.FixedPoint;

public sealed class AttackOrder : UnitOrder
{
    private readonly UnitUID targetUid;
    private UnitCore target;

    public AttackOrder(HeroUnit owner, UnitUID targetUid) : base(owner)
    {
        this.targetUid = targetUid;
    }

    public override void OnEnter()
    {
        if (Owner.CrowdControlHandler.CurrentSnapshot.BlockAttack)
        {
            IsCancelled = true;
            return;
        }

        if (!UnitManager.Instance.Spawns.TryGetValue(targetUid, out target) || target == null)
        {
            IsCancelled = true;
            return;
        }

        ((HeroUnit)Owner).SetTargetByOrder(target);
    }

    public override void Tick(fp dt)
    {
        if (Owner.CrowdControlHandler.CurrentSnapshot.BlockAttack)
        {
            IsCancelled = true;
            return;
        }

        if (target == null || target.CurrentActionState == UnitActionState.Dead)
        {
            IsFinished = true;
            return;
        }
    }
}