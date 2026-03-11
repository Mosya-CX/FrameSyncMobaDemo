using Unity.Mathematics.FixedPoint;

public sealed class CrowdControlRuntimeContext
{
    public UnitCore Owner;
    public CrowdControlData Data;
    public fp RemainingTime;
    public UnitCore Source;
    public object UserData;
}