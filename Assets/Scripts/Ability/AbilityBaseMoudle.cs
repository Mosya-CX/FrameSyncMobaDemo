using UnityEngine;

public abstract class AbilityBaseMoudle : ScriptableObject
{
    public abstract void Apply(AbilityExecutionContext context);
}