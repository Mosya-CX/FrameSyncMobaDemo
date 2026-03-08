using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public class CrowdControlRuntime
{
    public readonly CrowdControlHandler handler;
    public readonly CrowdControlData data;
    public readonly Dictionary<string, object> blackBoard = new();

    public fp existTimer;

    public CrowdControlRuntime(CrowdControlData data, CrowdControlHandler handler, fp existDuration)
    {
        this.data = data;
        this.handler = handler;
        existTimer = existDuration;
    }
}