using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public enum HealSourceKind : byte
{
    Ability,
    Buff,
    Equipment,
    Passive,
}

public sealed class HealRequest
{
    public UnitUID SourceUid;
    public UnitUID TargetUid;
    public HealSourceKind SourceKind;
    public fp BaseHeal;
    public HashSet<string> Tags = new();
    public object Extra;
}

public sealed class HealContext
{
    public UnitCore Source;
    public UnitCore Target;
    public HealSourceKind SourceKind;

    public fp BaseHeal;
    public fp BonusHeal;
    public fp HealMultiplier = 1;

    public HashSet<string> Tags = new();
    public object Extra;
}

public readonly struct HealResult
{
    public readonly UnitCore Source;
    public readonly UnitCore Target;
    public readonly fp FinalHeal;
    public readonly IReadOnlyCollection<string> Tags;

    public HealResult(UnitCore source, UnitCore target, fp finalHeal, IReadOnlyCollection<string> tags)
    {
        Source = source;
        Target = target;
        FinalHeal = finalHeal;
        Tags = tags;
    }
}