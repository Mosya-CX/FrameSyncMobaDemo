using Unity.Mathematics.FixedPoint;

public sealed class MoveOrder : UnitOrder
{
    private readonly fp3 destination;

    public MoveOrder(HeroUnit owner, fp3 destination) : base(owner)
    {
        this.destination = destination;
    }

    public override void OnEnter()
    {
        if (Owner.CrowdControlHandler.CurrentSnapshot.BlockMove)
        {
            IsCancelled = true;
            return;
        }

        ((HeroUnit)Owner).SetDestinationByOrder(destination);
    }

    public override void Tick(fp dt)
    {
        if (Owner.CrowdControlHandler.CurrentSnapshot.BlockMove)
        {
            IsCancelled = true;
            return;
        }

        if (((HeroUnit)Owner).IsReach(destination, 0.01m))
            IsFinished = true;
    }
}