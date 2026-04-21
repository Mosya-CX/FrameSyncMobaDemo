using UnityEngine;

public abstract class BuffBaseModule : ScriptableObject
{
    public abstract void Apply(BuffCallbackContext context);
}
