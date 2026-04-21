using Unity.Mathematics.FixedPoint;
using UnityEngine;

public abstract class UnitBaseHandler : MonoBehaviour, IStateful
{
    protected UnitCore owner;
    public UnitCore Owner => owner;

    protected virtual void Awake()
    {
        owner = GetComponent<UnitCore>();
    }

    public virtual void Tick(fp deltaTime) { }

    public abstract object CaptureState();
    public abstract void RestoreState(object state);
}