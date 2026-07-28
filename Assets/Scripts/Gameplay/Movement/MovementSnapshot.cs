namespace FrameSyncMoba.Unit
{
    public struct MovementSnapshot
    {
        public DashRuntime Dash;
        public ForcedMoveRuntime ForcedMove;

        public bool IsDashing => Dash.IsActive;

        public static readonly MovementSnapshot Default = new MovementSnapshot
        {
            Dash = default,
            ForcedMove = default,
        };
    }
}
