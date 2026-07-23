namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Stable broad Unit classification frozen by Unit v27.3 section 1.4.
    /// Used for wide queries such as Hero / Minion / Monster / Structure.
    /// Sub-classification within a kind is expressed by <see cref="Unit.UnitSubKindId"/>.
    /// </summary>
    public enum UnitKind : byte
    {
        Hero = 0,
        Minion = 1,
        Monster = 2,
        Structure = 3,
    }
}