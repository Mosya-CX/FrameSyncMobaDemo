using System;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Display info for a buff (design v14.2 3.3).
    /// </summary>
    [Serializable]
    public sealed class BuffDisplayInfo
    {
        public string Name;
        [TextArea]
        public string Description;
        public Sprite Icon;
    }

    /// <summary>
    /// What a reapply does to the remaining duration (design v14.2 3.4).
    /// </summary>
    public enum BuffRefreshMode : byte
    {
        NoChange = 0,
        RefreshToFull = 1,
        ExtendByAmount = 2,
    }

    [Serializable]
    public sealed class BuffLifeRuleConfig
    {
        public float DurationSeconds = 60f;
        public bool Infinite;
        public BuffRefreshMode RefreshMode =
            BuffRefreshMode.RefreshToFull;
        public float ExtendSeconds;
    }

    public enum BuffAddMode : byte
    {
        Add = 0,
        Ignore = 1,
    }

    public enum BuffReduceMode : byte
    {
        Reduce = 0,
        ClearAll = 1,
    }

    [Serializable]
    public sealed class BuffStackRuleConfig
    {
        public int MaxStacks = 1;
        public BuffAddMode AddMode = BuffAddMode.Add;
        public BuffReduceMode ReduceMode =
            BuffReduceMode.Reduce;
        public int ReduceAmount = 1;
    }

    [Serializable]
    public sealed class BuffTagSet
    {
        public byte[] TagIds;

        public bool HasTag(byte tag)
        {
            if (TagIds == null)
                return false;
            for (int i = 0; i < TagIds.Length; i++)
            {
                if (TagIds[i] == tag)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Buff source identity (design v14.2 4.3).
    /// </summary>
    public enum BuffSourceType : byte
    {
        None = 0,
        Attack = 1,
        Ability = 2,
        Item = 3,
        Talent = 4,
        Rune = 5,
        Environment = 6,
        Script = 7,
    }

    public readonly struct BuffSource :
        IEquatable<BuffSource>
    {
        public readonly UnitUid CasterUid;
        public readonly BuffSourceType SourceType;
        public readonly int SourceConfigId;

        public BuffSource(
            UnitUid casterUid,
            BuffSourceType sourceType,
            int sourceConfigId)
        {
            CasterUid = casterUid;
            SourceType = sourceType;
            SourceConfigId = sourceConfigId;
        }

        public static BuffSource Create(
            UnitUid casterUid,
            BuffSourceType sourceType,
            int sourceConfigId)
        {
            return new BuffSource(
                casterUid,
                sourceType,
                sourceConfigId);
        }

        public bool IsValid => CasterUid.IsValid();

        public bool Equals(BuffSource other)
        {
            return CasterUid == other.CasterUid &&
                SourceType == other.SourceType &&
                SourceConfigId == other.SourceConfigId;
        }

        public override bool Equals(object obj)
        {
            return obj is BuffSource other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CasterUid.GetHashCode();
                hash = (hash * 397) ^
                    (int)SourceType;
                hash = (hash * 397) ^
                    SourceConfigId;
                return hash;
            }
        }
    }

    /// <summary>
    /// Static effect module wrapper inside BuffDefinition.Effects
    /// (design v14.2 6.1).
    /// </summary>
    [Serializable]
    public sealed class BuffEffectConfig
    {
        [SerializeReference]
        public BuffEffect Effect;
    }

    /// <summary>
    /// Fixed logic-frequency seconds-to-tick conversion used at Apply and
    /// runtime initialization (design v14.2 1.5, 3.4).
    /// </summary>
    public static class BuffTickConverter
    {
        public static int TickRate = 30;

        public static int SecondsToTicks(
            float seconds)
        {
            return Mathf.Max(
                0,
                Mathf.RoundToInt(
                    seconds * TickRate));
        }

        public static float TicksToSeconds(
            int ticks)
        {
            if (TickRate <= 0)
                return 0f;
            return ticks / (float)TickRate;
        }
    }
}
