using Unity.Mathematics.FixedPoint;

public sealed class AbilityPreviewContext
{
    public HeroUnit Caster;
    public AbilityRuntime Runtime;
    public LocalCastSession Session;

    public fp3? MousePosition => Session?.Aim.TargetPosition;
    public UnitCore SelectedUnit => Session?.Aim.SelectedUnit;
}