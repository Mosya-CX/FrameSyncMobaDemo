namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Explicit stage for timing-only cast phases. It intentionally produces
    /// no Gameplay output; CastStage.DurationTicks owns completion timing.
    /// </summary>
    public sealed class DelayStageDef : StageDef
    {
    }
}
