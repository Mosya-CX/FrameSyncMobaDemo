using Unity.Mathematics.FixedPoint;

public sealed class AttackOrder : UnitOrder
{
    private readonly UnitUID targetUid;
    public UnitUID TargetUid => targetUid;

    private UnitCore target;

    public AttackOrder(HeroUnit owner, UnitUID targetUid) : base(owner)
    {
        this.targetUid = targetUid;
    }

    public override void OnEnter()
    {
        if (Owner is not HeroUnit hero || !hero.CanStartAttack())
        {
            IsCancelled = true;
            return;
        }

        if (!UnitManager.Instance.Spawns.TryGetValue(targetUid, out target) || target == null)
        {
            IsCancelled = true;
            return;
        }

        hero.SetTargetByOrder(target);
    }

    public override void Tick(fp dt)
    {
        if (Owner is not HeroUnit hero || !hero.CanStartAttack())
        {
            IsCancelled = true;
            return;
        }

        if (target == null || target.IsDead)
        {
            IsFinished = true;
            return;
        }
    }
}