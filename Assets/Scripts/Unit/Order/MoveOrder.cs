using Unity.Mathematics.FixedPoint;

public sealed class MoveOrder : UnitOrder
{
    private readonly fp3 destination;
    public fp3 Destination => destination;

    public MoveOrder(HeroUnit owner, fp3 destination) : base(owner)
    {
        this.destination = destination;
    }

    public override void OnEnter()
    {
        if (Owner is not HeroUnit hero || !hero.CanStartMove())
        {
            IsCancelled = true;
            return;
        }

        hero.SetDestinationByOrder(destination);
    }

    public override void Tick(fp dt)
    {
        if (Owner is not HeroUnit hero || !hero.CanStartMove())
        {
            IsCancelled = true;
            return;
        }

        if (hero.IsReach(destination, 0.01m))
            IsFinished = true;
    }
}