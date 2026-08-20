using System;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig
{
    /// <summary>
    /// Defines how a positive, real-time authoring duration is mapped onto the
    /// fixed Tick lattice selected for a match.
    /// </summary>
    public enum DurationRoundingPolicy : byte
    {
        Ceil = 0,
        Nearest = 1,
        Floor = 2,
    }

    /// <summary>
    /// Stable authoring representation for Gameplay time. Unity serializes an
    /// integer millisecond value; the Editor drawer presents that exact integer
    /// with an explicit ms suffix.
    /// Runtime systems consume only the integer Tick value produced by Bake.
    /// </summary>
    [Serializable]
    public struct DurationAuthoring : IEquatable<DurationAuthoring>
    {
        [SerializeField]
        private int milliseconds;

        [SerializeField]
        private DurationRoundingPolicy roundingPolicy;

        [SerializeField, HideInInspector]
        private bool authored;

        public int Milliseconds => milliseconds;
        public DurationRoundingPolicy RoundingPolicy => roundingPolicy;
        public bool IsAuthored => authored;
        public bool IsNonnegative => milliseconds >= 0;
        public bool IsPositive => milliseconds > 0;

        public DurationAuthoring(
            int milliseconds,
            DurationRoundingPolicy roundingPolicy =
                DurationRoundingPolicy.Ceil)
        {
            if (milliseconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(milliseconds));
            DeterministicTimeConversion.ValidatePolicy(
                roundingPolicy);
            this.milliseconds = milliseconds;
            this.roundingPolicy = roundingPolicy;
            authored = true;
        }

        public int BakeTicks(int tickRate)
        {
            return DeterministicTimeConversion.MillisecondsToTicks(
                milliseconds,
                tickRate,
                roundingPolicy);
        }

        public static DurationAuthoring FromSeconds(
            decimal seconds,
            DurationRoundingPolicy roundingPolicy =
                DurationRoundingPolicy.Ceil)
        {
            if (seconds < decimal.Zero)
                throw new ArgumentOutOfRangeException(
                    nameof(seconds));
            decimal exactMilliseconds =
                seconds *
                DeterministicTimeConversion.MillisecondsPerSecond;
            int value = checked((int)decimal.Round(
                exactMilliseconds,
                0,
                MidpointRounding.AwayFromZero));
            return new DurationAuthoring(value, roundingPolicy);
        }

        /// <summary>
        /// Converts a legacy authoring Tick count using its declared source
        /// TickRate. Flooring the millisecond representation and baking it with
        /// Ceil preserves the original positive Tick count exactly.
        /// </summary>
        public static DurationAuthoring FromLegacyTicks(
            int ticks,
            int legacyTickRate = 30,
            DurationRoundingPolicy roundingPolicy =
                DurationRoundingPolicy.Ceil)
        {
            if (ticks < 0)
                throw new ArgumentOutOfRangeException(nameof(ticks));
            if (legacyTickRate <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(legacyTickRate));
            int value = checked((int)(
                (long)ticks *
                DeterministicTimeConversion.MillisecondsPerSecond /
                legacyTickRate));
            return new DurationAuthoring(value, roundingPolicy);
        }

        public bool Equals(DurationAuthoring other)
        {
            return milliseconds == other.milliseconds &&
                   roundingPolicy == other.roundingPolicy;
        }

        public override bool Equals(object obj)
        {
            return obj is DurationAuthoring other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (milliseconds.GetHashCode() * 397) ^
                       (int)roundingPolicy;
            }
        }
    }

    /// <summary>
    /// Pure checked integer conversion shared by every authoring Bake path.
    /// It is deliberately independent of Unity frame time and floating point.
    /// </summary>
    public static class DeterministicTimeConversion
    {
        public const int MillisecondsPerSecond = 1_000;

        public static int MillisecondsToTicks(
            int milliseconds,
            int tickRate,
            DurationRoundingPolicy roundingPolicy)
        {
            if (milliseconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(milliseconds));
            if (tickRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(tickRate));
            ValidatePolicy(roundingPolicy);

            long numerator = checked((long)milliseconds * tickRate);
            long ticks;
            switch (roundingPolicy)
            {
                case DurationRoundingPolicy.Ceil:
                    ticks = numerator == 0
                        ? 0
                        : checked(
                            numerator +
                            MillisecondsPerSecond - 1L) /
                          MillisecondsPerSecond;
                    break;
                case DurationRoundingPolicy.Nearest:
                    ticks = checked(
                        numerator +
                        MillisecondsPerSecond / 2L) /
                        MillisecondsPerSecond;
                    break;
                case DurationRoundingPolicy.Floor:
                    ticks = numerator / MillisecondsPerSecond;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(roundingPolicy));
            }
            return checked((int)ticks);
        }

        public static void ValidateSupportedTickRate(int tickRate)
        {
            if (tickRate < 10 ||
                tickRate > 120 ||
                tickRate % 5 != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tickRate),
                    "TickRate must be within [10, 120] and divisible by 5.");
            }
        }

        public static int SecondsToTicks(
            float seconds,
            int tickRate,
            DurationRoundingPolicy roundingPolicy =
                DurationRoundingPolicy.Ceil)
        {
            if (float.IsNaN(seconds) ||
                float.IsInfinity(seconds) ||
                seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            return DurationAuthoring.FromSeconds(
                    (decimal)seconds,
                    roundingPolicy)
                .BakeTicks(tickRate);
        }

        public static int Legacy30HzTicksToTicks(
            int legacyTicks,
            int tickRate,
            DurationRoundingPolicy roundingPolicy =
                DurationRoundingPolicy.Ceil)
        {
            return DurationAuthoring.FromLegacyTicks(
                    legacyTicks,
                    30,
                    roundingPolicy)
                .BakeTicks(tickRate);
        }

        internal static void ValidatePolicy(
            DurationRoundingPolicy roundingPolicy)
        {
            if (roundingPolicy < DurationRoundingPolicy.Ceil ||
                roundingPolicy > DurationRoundingPolicy.Floor)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roundingPolicy));
            }
        }
    }
}
