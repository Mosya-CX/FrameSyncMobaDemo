using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Deterministic movement command for frame-synchronized replay and authority
    /// comparison. Contains the complete canonical data needed to reproduce a
    /// movement Tick on any endpoint (Physics v13.1 section 5.1).
    /// </summary>
    public readonly struct MoveCommand
    {
        /// <summary>
        /// Target unit identity.
        /// </summary>
        public readonly UnitUid UnitUid;

        /// <summary>
        /// The movement intent for this Tick.
        /// </summary>
        public readonly MoveIntent Intent;

        /// <summary>
        /// Authoritative logic Tick when this command was created.
        /// </summary>
        public readonly int Tick;

        public MoveCommand(UnitUid unitUid, MoveIntent intent, int tick)
        {
            UnitUid = unitUid;
            Intent = intent;
            Tick = tick;
        }

        public static readonly MoveCommand None = default;
    }
}
