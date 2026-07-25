using FrameSyncMoba.LuaBridge;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Type-forward: Bootstrap uses LuaBridge's UiSnapshotDto as the
    /// canonical UI snapshot type. This file exists to avoid breaking
    /// existing references. All new code should reference
    /// FrameSyncMoba.LuaBridge.UiSnapshotDto directly.
    /// </summary>
    public static class UiSnapshotDtoAlias
    {
        public static readonly UiSnapshotDto Empty = UiSnapshotDto.Empty;
    }
}
