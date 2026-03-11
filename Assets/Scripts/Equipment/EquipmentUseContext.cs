using Unity.Mathematics.FixedPoint;

public readonly struct EquipmentUseContext
{
    public readonly fp3? TargetPosition;
    public readonly UnitCore TargetUnit;

    public EquipmentUseContext(fp3? targetPosition, UnitCore targetUnit)
    {
        TargetPosition = targetPosition;
        TargetUnit = targetUnit;
    }
}