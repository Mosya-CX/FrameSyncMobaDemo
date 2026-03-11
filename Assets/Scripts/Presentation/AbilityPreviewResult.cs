using Unity.Mathematics.FixedPoint;

public enum AbilityPreviewValidity : byte
{
    Invalid,
    Valid,
    NeedApproach,
}

public sealed class AbilityPreviewResult
{
    public AbilityPreviewValidity Validity;
    public fp3? PreviewPosition;
    public UnitCore PreviewTarget;
}