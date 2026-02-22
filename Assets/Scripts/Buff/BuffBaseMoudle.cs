using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BuffBaseMoudle : ScriptableObject
{
    public abstract void Apply(BuffInfo info, BuffHandler handler, Dictionary<string, object> blackBoard);
}
