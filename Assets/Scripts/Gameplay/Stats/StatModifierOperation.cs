namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Operation type for a long-term stat modifier (Unit v27.3 section 5.3.2).
    /// </summary>
    public enum StatModifierOperation : byte
    {
        /// <summary>Adds a fixed value on top of the level base value.</summary>
        FlatAdd = 0,

        /// <summary>Adds a ratio of the level base value.</summary>
        BaseRatioAdd = 1,

        /// <summary>Applies a final ratio after flat and base ratio sums.</summary>
        FinalRatioAdd = 2,
    }
}