using System;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    public enum GameplayParticipantDomain : byte
    {
        Invalid = 0,
        InitialSpawn = 1,
        MinionWave = 2,
        JungleCamp = 3,
        DerivedSpawn = 4,
        Explicit = 5,
    }

    /// <summary>
    /// Immutable Gameplay identity that survives technical UnitUid/PrefabId
    /// relabeling. Its fields come from stable spawn provenance, never Unity
    /// registration or object identity (D-050).
    /// </summary>
    public readonly struct GameplayParticipantId :
        IEquatable<GameplayParticipantId>,
        IComparable<GameplayParticipantId>
    {
        public readonly GameplayParticipantDomain Domain;
        public readonly int Scope;
        public readonly int Generation;
        public readonly int Ordinal;

        public GameplayParticipantId(
            GameplayParticipantDomain domain,
            int scope,
            int generation,
            int ordinal)
        {
            Domain = domain;
            Scope = scope;
            Generation = generation;
            Ordinal = ordinal;
        }

        public bool IsValid =>
            Domain > GameplayParticipantDomain.Invalid &&
            Domain <= GameplayParticipantDomain.Explicit &&
            Generation >= 0 &&
            Ordinal >= 0;

        public static GameplayParticipantId InitialSpawn(
            int stableSpawnOrder) =>
            new GameplayParticipantId(
                GameplayParticipantDomain.InitialSpawn,
                stableSpawnOrder,
                0,
                0);

        public static GameplayParticipantId MinionWave(
            TeamId team,
            ushort laneId,
            int spawnLogicTick,
            int stableEntryIndex) =>
            new GameplayParticipantId(
                GameplayParticipantDomain.MinionWave,
                (team.Value << 16) | laneId,
                spawnLogicTick,
                stableEntryIndex);

        public static GameplayParticipantId JungleCamp(
            int campId,
            int spawnLogicTick,
            int memberSlot) =>
            new GameplayParticipantId(
                GameplayParticipantDomain.JungleCamp,
                campId,
                spawnLogicTick,
                memberSlot);

        public static GameplayParticipantId DerivedSpawn(
            GameplayParticipantId parent,
            int spawnLogicTick,
            int childOrdinal)
        {
            if (!parent.IsValid)
                throw new ArgumentException(
                    "A derived participant requires a valid parent identity.",
                    nameof(parent));
            return new GameplayParticipantId(
                GameplayParticipantDomain.DerivedSpawn,
                unchecked((int)(parent.StableHash64() & 0x7FFFFFFFUL)),
                spawnLogicTick,
                childOrdinal);
        }

        public static GameplayParticipantId Explicit(
            int scope,
            int generation = 0,
            int ordinal = 0) =>
            new GameplayParticipantId(
                GameplayParticipantDomain.Explicit,
                scope,
                generation,
                ordinal);

        public ulong StableHash64() =>
            DeterministicHash64.Compute(
                (ulong)Domain,
                unchecked((uint)Scope),
                unchecked((uint)Generation),
                unchecked((uint)Ordinal),
                0x5041525449434950UL);

        public int CompareTo(GameplayParticipantId other)
        {
            int comparison = Domain.CompareTo(other.Domain);
            if (comparison != 0) return comparison;
            comparison = Scope.CompareTo(other.Scope);
            if (comparison != 0) return comparison;
            comparison = Generation.CompareTo(other.Generation);
            if (comparison != 0) return comparison;
            return Ordinal.CompareTo(other.Ordinal);
        }

        public bool Equals(GameplayParticipantId other) =>
            Domain == other.Domain &&
            Scope == other.Scope &&
            Generation == other.Generation &&
            Ordinal == other.Ordinal;

        public override bool Equals(object obj) =>
            obj is GameplayParticipantId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Domain;
                hash = (hash * 397) ^ Scope;
                hash = (hash * 397) ^ Generation;
                return (hash * 397) ^ Ordinal;
            }
        }

        public static bool operator ==(
            GameplayParticipantId left,
            GameplayParticipantId right) => left.Equals(right);

        public static bool operator !=(
            GameplayParticipantId left,
            GameplayParticipantId right) => !left.Equals(right);

        public override string ToString() =>
            $"Participant({(byte)Domain},{Scope},{Generation},{Ordinal})";

        public static readonly GameplayParticipantId Invalid = default;
    }
}
