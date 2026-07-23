using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using MathematicsRandom = Unity.Mathematics.Random;

namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// The single snapshot-restorable random stream used by deterministic Gameplay.
    /// </summary>
    public sealed class DeterministicRandomService
    {
        private static readonly fp Hundred = fp.FromRaw(100L << 32);

        private MathematicsRandom random;

        public DeterministicRandomService(uint seed)
        {
            if (seed == 0u)
            {
                throw new ArgumentOutOfRangeException(nameof(seed), "A deterministic random seed must be nonzero.");
            }

            random = new MathematicsRandom(seed);
        }

        public uint NextUInt()
        {
            return random.NextUInt();
        }

        public int NextInt()
        {
            return random.NextInt();
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive),
                    "The exclusive maximum must be greater than the inclusive minimum.");
            }

            uint range = (uint)((long)maxExclusive - minInclusive);
            uint offset = NextUInt() % range;

            return (int)((long)minInclusive + offset);
        }

        public fp NextFp01()
        {
            return fp.FromRaw(NextUInt());
        }

        public fp NextFp(fp minInclusive, fp maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive),
                    "The exclusive maximum must be greater than the inclusive minimum.");
            }

            return minInclusive + ((maxExclusive - minInclusive) * NextFp01());
        }

        public bool NextBool()
        {
            return (NextUInt() & 1u) != 0u;
        }

        public bool Chance01(fp probability)
        {
            fp roll = NextFp01();

            if (probability <= fp.zero)
            {
                return false;
            }

            if (probability >= fp.one)
            {
                return true;
            }

            return roll < probability;
        }

        public bool ChancePercent(fp probabilityPercent)
        {
            return Chance01(probabilityPercent / Hundred);
        }

        public int PickIndex(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "A deterministic pick count must be positive.");
            }

            return NextInt(0, count);
        }

        public T PickOne<T>(IReadOnlyList<T> readOnlyList)
        {
            if (readOnlyList == null)
            {
                throw new ArgumentNullException(nameof(readOnlyList));
            }

            if (readOnlyList.Count == 0)
            {
                throw new ArgumentException("A deterministic pick list must not be empty.", nameof(readOnlyList));
            }

            return readOnlyList[PickIndex(readOnlyList.Count)];
        }

        public void ShuffleInPlace<T>(T[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            for (int index = values.Length - 1; index > 0; index--)
            {
                int swapIndex = NextInt(0, index + 1);
                T value = values[index];
                values[index] = values[swapIndex];
                values[swapIndex] = value;
            }
        }

        public fp2 RandomDirection2D()
        {
            return DirectionFromUnitDraw(NextFp01());
        }

        public fp2 RandomPointInsideCircle(fp radius)
        {
            ValidateRadius(radius);

            fp angleDraw = NextFp01();
            fp radialDraw = NextFp01();
            fp distance = fpmath.sqrt(radialDraw) * radius;

            return DirectionFromUnitDraw(angleDraw) * distance;
        }

        public fp2 RandomPointOnCircle(fp radius)
        {
            ValidateRadius(radius);
            return DirectionFromUnitDraw(NextFp01()) * radius;
        }

        public DeterministicRandomSnapshot Capture()
        {
            return new DeterministicRandomSnapshot(random.state);
        }

        public void Restore(DeterministicRandomSnapshot snapshot)
        {
            if (snapshot.State == 0u)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(snapshot),
                    "A deterministic random state must be nonzero.");
            }

            random.state = snapshot.State;
        }

        private static fp2 DirectionFromUnitDraw(fp unitDraw)
        {
            fp angle = unitDraw * fpmath.PI_TIMES_2;
            return new fp2(fpmath.cos(angle), fpmath.sin(angle));
        }

        private static void ValidateRadius(fp radius)
        {
            if (radius < fp.zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(radius),
                    "A deterministic random circle radius must not be negative.");
            }
        }
    }
}
