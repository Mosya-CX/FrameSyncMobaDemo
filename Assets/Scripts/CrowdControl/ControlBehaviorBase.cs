using UnityEngine;
using Unity.Mathematics.FixedPoint;

public abstract class ControlBehaviorBase : ScriptableObject
{
    public virtual void OnApply(CrowdControlRuntimeContext context) { }
    public virtual void OnTick(CrowdControlRuntimeContext context, fp deltaTime) { }
    public virtual void OnRemove(CrowdControlRuntimeContext context) { }
}