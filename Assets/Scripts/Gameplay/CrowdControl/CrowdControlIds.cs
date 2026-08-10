namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Standard framework control ids (CC v6.2 9.x combos). These are
    /// framework examples/acceptance fixtures, not production content; the
    /// catalog asset defines the actual baked definitions.
    /// </summary>
    public static class CrowdControlIds
    {
        public static readonly CrowdControlId Stun =
            new CrowdControlId(101);
        public static readonly CrowdControlId Root =
            new CrowdControlId(102);
        public static readonly CrowdControlId Slow =
            new CrowdControlId(103);
        public static readonly CrowdControlId Silence =
            new CrowdControlId(104);
        public static readonly CrowdControlId Disarm =
            new CrowdControlId(105);
        public static readonly CrowdControlId KnockBack =
            new CrowdControlId(106);
        public static readonly CrowdControlId Suppression =
            new CrowdControlId(107);
        public static readonly CrowdControlId Sleep =
            new CrowdControlId(108);
        public static readonly CrowdControlId Drowsy =
            new CrowdControlId(109);
        public static readonly CrowdControlId Taunt =
            new CrowdControlId(110);
        public static readonly CrowdControlId Charm =
            new CrowdControlId(111);
        public static readonly CrowdControlId Fear =
            new CrowdControlId(112);
    }
}
