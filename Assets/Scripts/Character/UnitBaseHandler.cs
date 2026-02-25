using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public abstract class UnitBaseHandler : MonoBehaviour, IHandlerStateful
{
    protected UnitCore core;
    public UnitCore Core => core;

    protected virtual void Awake()
    {
        core ??= GetComponent<UnitCore>();
    }

    public abstract void Tick(fp deltaTime);

    public abstract object CaptureHandlerState();
    public abstract void RestoreHandlerState(object state);
}
