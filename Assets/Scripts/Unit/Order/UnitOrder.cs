using Unity.Mathematics.FixedPoint;

public abstract class UnitOrder
{
    public UnitCore Owner { get; }
    public bool IsFinished { get; protected set; }
    public bool IsCancelled { get; protected set; }

    protected UnitOrder(UnitCore owner)
    {
        Owner = owner;
    }

    public virtual void OnEnter() { }
    public virtual void Tick(fp dt) { }
    public virtual void OnExit() { }

    public virtual bool CanBeInterruptedBy(UnitOrder newOrder) => true;

    public virtual void Cancel()
    {
        IsCancelled = true;
    }
}