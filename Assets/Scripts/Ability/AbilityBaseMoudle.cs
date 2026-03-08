using Unity.Mathematics.FixedPoint;
using UnityEngine;

public abstract class AbilityBaseMoudle : ScriptableObject
{
    public abstract void Apply(AbilityInfo info);
}
