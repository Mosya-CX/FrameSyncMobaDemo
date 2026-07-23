using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public interface IMovementAgent
    {
        void ApplyMoveInput(in MoveIntent intent);
        void TickUpdate(fp deltaTime);
        void ForceSetPosition(fp2 position);
        ref readonly MovementSnapshot Snapshot { get; }
    }
}
