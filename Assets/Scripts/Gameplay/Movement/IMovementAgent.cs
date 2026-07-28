using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Movement agent interface.
    /// TickUpdate signature follows Pathfinding Design v13.1 section 1.4:
    /// reads SimulationTickContext.Current.DeltaTick internally.
    /// </summary>
    public interface IMovementAgent
    {
        void ApplyMoveInput(in MoveIntent intent);
        void TickUpdate();
        void ForceSetPosition(fp2 position);
        ref readonly MovementSnapshot Snapshot { get; }
        fp2 Position { get; }
        fp2 Facing { get; }
        fp2 Velocity { get; }
        fp MoveSpeed { get; }
        bool IsMoving { get; }
        fp2 TargetDirection { get; }
    }
}
