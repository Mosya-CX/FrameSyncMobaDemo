using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BuffBaseModule : ScriptableObject
{
    public abstract void Apply(BuffCallbackContext context);
}
