using Unity.Mathematics.FixedPoint;

public struct LocalAimData
{
    public fp3? TargetPosition;
    public UnitUID? TargetUnitId;
}

public enum LocalCastSessionState
{
    None,
    Preview,
}

public sealed class LocalCastSession
{
    public int AbilityId;
    public LocalCastSessionState State;
    public LocalAimData Aim;
}