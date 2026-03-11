using Unity.Mathematics.FixedPoint;

public enum LocalCastSessionState : byte
{
    None,
    Preview,
}

public sealed class LocalAimData
{
    public fp3? TargetPosition;
    public UnitCore SelectedUnit;
    public float HeldSeconds;
}

public sealed class LocalCastSession
{
    public int AbilityId;
    public LocalCastSessionState State;
    public LocalAimData Aim = new();
}