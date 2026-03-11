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

    protected virtual void OnEnable()
    {
        if (owner == null)
            owner = GetComponent<UnitCore>();

        BindEvents();
    }

    protected virtual void OnDisable()
    {
        UnbindEvents();
    }

    protected virtual void OnDestroy()
    {
        UnbindEvents();
    }

    protected virtual void BindEvents() { }
    protected virtual void UnbindEvents() { }

    public virtual void Tick(fp deltaTime) { }

    public abstract object CaptureState();
    public abstract void RestoreState(object state);
}