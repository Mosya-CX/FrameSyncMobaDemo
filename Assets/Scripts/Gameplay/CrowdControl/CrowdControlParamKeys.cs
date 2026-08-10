using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Stable identity of a dynamic control parameter key (CC v6.2 4.5).
    /// Values are hand-assigned constants; the editor string registry below
    /// maps authored strings to these ids and rejects duplicates/unknowns.
    /// </summary>
    [System.Serializable]
    public readonly struct CrowdControlParamKey : System.IEquatable<CrowdControlParamKey>
    {
        public readonly uint Value;
        public CrowdControlParamKey(uint value) { Value = value; }
        public bool IsValid => Value != 0;
        public bool Equals(CrowdControlParamKey other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CrowdControlParamKey other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public static bool operator ==(CrowdControlParamKey a, CrowdControlParamKey b) => a.Equals(b);
        public static bool operator !=(CrowdControlParamKey a, CrowdControlParamKey b) => !a.Equals(b);
    }

    /// <summary>
    /// Code-side stable parameter key constants (CC v6.2 4.5: constants are
    /// generated in code, never hashed at runtime).
    /// </summary>
    public static class ControlParamKeys
    {
        public static readonly CrowdControlParamKey TargetUnit =
            new CrowdControlParamKey(1);
        public static readonly CrowdControlParamKey MoveSlowRatio =
            new CrowdControlParamKey(2);
        public static readonly CrowdControlParamKey AttackSpeedSlowRatio =
            new CrowdControlParamKey(3);
        public static readonly CrowdControlParamKey Direction =
            new CrowdControlParamKey(4);
        public static readonly CrowdControlParamKey Distance =
            new CrowdControlParamKey(5);
        public static readonly CrowdControlParamKey MoveTicks =
            new CrowdControlParamKey(6);
        public static readonly CrowdControlParamKey ForcedMovePriority =
            new CrowdControlParamKey(7);
        public static readonly CrowdControlParamKey BehaviorId =
            new CrowdControlParamKey(8);
        public static readonly CrowdControlParamKey Priority =
            new CrowdControlParamKey(9);
        public static readonly CrowdControlParamKey MoveScale =
            new CrowdControlParamKey(10);
        public static readonly CrowdControlParamKey SleepDurationTicks =
            new CrowdControlParamKey(11);

        private static readonly Dictionary<string, CrowdControlParamKey> ByName =
            new Dictionary<string, CrowdControlParamKey>
            {
                { "TargetUnit", TargetUnit },
                { "MoveSlowRatio", MoveSlowRatio },
                { "AttackSpeedSlowRatio", AttackSpeedSlowRatio },
                { "Direction", Direction },
                { "Distance", Distance },
                { "MoveTicks", MoveTicks },
                { "ForcedMovePriority", ForcedMovePriority },
                { "BehaviorId", BehaviorId },
                { "Priority", Priority },
                { "MoveScale", MoveScale },
                { "SleepDurationTicks", SleepDurationTicks },
            };

        /// <summary>Editor-only lookup used by the baker. Never called from
        /// a per-tick path.</summary>
        public static bool TryGetByName(
            string name,
            out CrowdControlParamKey key)
        {
            if (name != null && ByName.TryGetValue(name, out key))
            {
                return true;
            }
            key = default;
            return false;
        }

        public static IReadOnlyCollection<string> AllNames => ByName.Keys;
    }
}
