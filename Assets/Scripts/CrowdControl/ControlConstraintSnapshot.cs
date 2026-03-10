using Unity.Mathematics.FixedPoint;

public struct ControlConstraintSnapshot
{
    public bool BlockMoveInput;
    public bool BlockAttackInput;
    public bool BlockCastInput;

    public bool BlockMove;
    public bool BlockTrack;
    public bool BlockAttack;
    public bool BlockCast;
    public bool BlockDash;

    public bool ForceInterruptCast;
    public bool ForceInterruptAttack;

    public fp MoveSpeedMultiplier;

    public static ControlConstraintSnapshot Default => new ControlConstraintSnapshot
    {
        MoveSpeedMultiplier = (fp)1
    };
}