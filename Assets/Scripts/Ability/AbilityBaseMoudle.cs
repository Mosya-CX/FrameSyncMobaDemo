using Unity.Mathematics.FixedPoint;
using UnityEngine;

public abstract class AbilityBaseMoudle : ScriptableObject
{
    public abstract void OnPhaseEnter(AbilityInfo info, AbilityHandler handler);
    public abstract void OnPhaseUpdate(AbilityInfo info, AbilityHandler handler, fp deltaTime);
    public abstract void OnPhaseExit(AbilityInfo info, AbilityHandler handler);
}
