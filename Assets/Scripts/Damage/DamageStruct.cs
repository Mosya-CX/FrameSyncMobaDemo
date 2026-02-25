using Unity.Mathematics.FixedPoint;

public struct DamageInfo
{
    public UnitCore source;
    public UnitCore target;

    public fp physicalDamage;
    public fp magicDamage;
    public fp trueDamage;

    public DamageType damageType;

    public object extraInfo;
}

public enum DamageType
{ 

}