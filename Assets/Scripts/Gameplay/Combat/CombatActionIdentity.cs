using System;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Stable source-local action identity used by keyed deterministic
    /// mechanics. It deliberately excludes UnitUid and PrefabId (D-050).
    /// </summary>
    public readonly struct OriginActionId :
        IEquatable<OriginActionId>,
        IComparable<OriginActionId>
    {
        public readonly GameplayParticipantId SourceParticipantId;
        public readonly CombatSourceType SourceType;
        public readonly int SourceId;
        public readonly int OriginLogicTick;
        public readonly int SourceLocalSequence;

        public OriginActionId(
            GameplayParticipantId sourceParticipantId,
            CombatSourceType sourceType,
            int sourceId,
            int originLogicTick,
            int sourceLocalSequence)
        {
            SourceParticipantId = sourceParticipantId;
            SourceType = sourceType;
            SourceId = sourceId;
            OriginLogicTick = originLogicTick;
            SourceLocalSequence = sourceLocalSequence;
        }

        public bool IsValid =>
            SourceParticipantId.IsValid &&
            SourceType >= CombatSourceType.Attack &&
            SourceType <= CombatSourceType.System &&
            SourceId > 0 &&
            OriginLogicTick >= 0 &&
            SourceLocalSequence >= 0;

        public ulong StableHash64()
        {
            ulong sourceKey =
                ((ulong)(byte)SourceType << 32) |
                unchecked((uint)SourceId);
            return DeterministicHash64.Compute(
                SourceParticipantId.StableHash64(),
                sourceKey,
                unchecked((uint)OriginLogicTick),
                unchecked((uint)SourceLocalSequence),
                0x4F524947494E4143UL);
        }

        public int CompareTo(OriginActionId other)
        {
            int comparison = SourceParticipantId.CompareTo(
                other.SourceParticipantId);
            if (comparison != 0) return comparison;
            comparison = SourceType.CompareTo(other.SourceType);
            if (comparison != 0) return comparison;
            comparison = SourceId.CompareTo(other.SourceId);
            if (comparison != 0) return comparison;
            comparison = OriginLogicTick.CompareTo(other.OriginLogicTick);
            if (comparison != 0) return comparison;
            return SourceLocalSequence.CompareTo(other.SourceLocalSequence);
        }

        public bool Equals(OriginActionId other) =>
            SourceParticipantId == other.SourceParticipantId &&
            SourceType == other.SourceType &&
            SourceId == other.SourceId &&
            OriginLogicTick == other.OriginLogicTick &&
            SourceLocalSequence == other.SourceLocalSequence;

        public override bool Equals(object obj) =>
            obj is OriginActionId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceParticipantId.GetHashCode();
                hash = (hash * 397) ^ (int)SourceType;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ OriginLogicTick;
                return (hash * 397) ^ SourceLocalSequence;
            }
        }

        public static bool operator ==(
            OriginActionId left,
            OriginActionId right) => left.Equals(right);

        public static bool operator !=(
            OriginActionId left,
            OriginActionId right) => !left.Equals(right);

        public static readonly OriginActionId Invalid = default;
    }

    public static class CombatActionIdentityFactory
    {
        public static OriginActionId CreateFromSource(
            UnitWorld world,
            UnitUid sourceUnitUid,
            CombatSourceType sourceType,
            int sourceId,
            int originLogicTick,
            in GameplayParticipantId affectedParticipantId,
            int stableScope)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            if (!world.TryGetUnit(sourceUnitUid, out Unit source))
                throw new DeterministicSimulationException(
                    $"Action identity references missing source Unit {sourceUnitUid}.");
            return new OriginActionId(
                source.GameplayParticipantId,
                sourceType,
                sourceId,
                originLogicTick,
                CombatFairnessKey.ParticipantLocalSequence(
                    affectedParticipantId,
                    stableScope));
        }
    }

    public static class CombatFairnessKey
    {
        private const ulong CritDomain = 0x434F4D4241544352UL;
        private const ulong ProjectileTieDomain = 0x50524F4A54494531UL;

        public static bool RollCrit(
            uint initialMatchSeed,
            in OriginActionId actionId,
            in GameplayParticipantId targetParticipantId,
            int effectOrdinal,
            fp probability)
        {
            if (effectOrdinal < 0)
                throw new DeterministicSimulationException(
                    "Combat EffectOrdinal cannot be negative.");
            if (probability <= fp.zero) return false;
            if (probability >= fp.one) return true;
            if (!actionId.IsValid)
                throw new DeterministicSimulationException(
                    "Probabilistic Crit requires a valid OriginActionId.");
            if (!targetParticipantId.IsValid)
                throw new DeterministicSimulationException(
                    "Probabilistic Crit target has no GameplayParticipantId.");

            ulong score = DeterministicHash64.Compute(
                initialMatchSeed,
                actionId.StableHash64(),
                targetParticipantId.StableHash64(),
                unchecked((uint)effectOrdinal),
                CritDomain);
            fp roll = fp.FromRaw(unchecked((uint)(score >> 32)));
            return roll < probability;
        }

        public static ulong ProjectileTieScore(
            uint initialMatchSeed,
            in OriginActionId actionId,
            in GameplayParticipantId targetParticipantId)
        {
            if (!actionId.IsValid)
                throw new DeterministicSimulationException(
                    "Projectile target arbitration requires a valid OriginActionId.");
            if (!targetParticipantId.IsValid)
                throw new DeterministicSimulationException(
                    "Projectile target has no GameplayParticipantId.");
            return DeterministicHash64.Compute(
                initialMatchSeed,
                actionId.StableHash64(),
                targetParticipantId.StableHash64(),
                ProjectileTieDomain,
                0x44495354414E4345UL);
        }

        public static int ComposeEffectOrdinal(
            int stableScope,
            int localOrdinal)
        {
            if (stableScope < 0)
                throw new ArgumentOutOfRangeException(nameof(stableScope));
            if (localOrdinal < 0)
                throw new ArgumentOutOfRangeException(nameof(localOrdinal));
            return unchecked((int)(DeterministicHash64.Compute(
                unchecked((uint)stableScope),
                unchecked((uint)localOrdinal),
                0x4546464543544F52UL,
                0x44494E414C303031UL,
                0x000000007FFFFFFFUL) & 0x7FFFFFFFUL));
        }

        public static int ComposeChildEffectOrdinal(
            int parentEffectOrdinal,
            int stableScope,
            int localOrdinal)
        {
            if (parentEffectOrdinal < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(parentEffectOrdinal));
            if (stableScope < 0)
                throw new ArgumentOutOfRangeException(nameof(stableScope));
            if (localOrdinal < 0)
                throw new ArgumentOutOfRangeException(nameof(localOrdinal));
            return unchecked((int)(DeterministicHash64.Compute(
                unchecked((uint)parentEffectOrdinal),
                unchecked((uint)stableScope),
                unchecked((uint)localOrdinal),
                0x4348494C44454646UL,
                0x000000007FFFFFFFUL) & 0x7FFFFFFFUL));
        }

        public static int ParticipantLocalSequence(
            in GameplayParticipantId participantId,
            int stableScope)
        {
            if (!participantId.IsValid)
                throw new DeterministicSimulationException(
                    "Action-local sequence requires a valid participant identity.");
            return unchecked((int)(DeterministicHash64.Compute(
                participantId.StableHash64(),
                unchecked((uint)stableScope),
                0x414354494F4E5345UL,
                0x5155454E43453031UL,
                0x000000007FFFFFFFUL) & 0x7FFFFFFFUL));
        }
    }
}
