using System.Collections.Generic;
using UnityEngine;

public class BuffHandler : MonoBehaviour
{
    private UnitCore core;
    public UnitCore Core => core;

    private Dictionary<string, object> blackBoard;

    private void Awake()
    {
        core ??= GetComponent<UnitCore>();
        blackBoard = new();
    }
}
