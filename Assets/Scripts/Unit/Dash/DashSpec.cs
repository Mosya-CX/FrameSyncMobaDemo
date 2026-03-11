using Unity.Mathematics.FixedPoint;

public enum DashTrajectoryType : byte
{
    Linear,
    ToPoint,
    ToTarget,
}

public sealed class DashSpec
{
    public fp Distance;
    public fp Duration;
    public DashTrajectoryType TrajectoryType;
    public bool StopOnTargetReached = true;
}