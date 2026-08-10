using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Stable global identifier of a CrowdControlDefinition (CC v6.2 2.2).
    /// </summary>
    [Serializable]
    public readonly struct CrowdControlId : IEquatable<CrowdControlId>, IComparable<CrowdControlId>
    {
        public readonly int Value;
        public CrowdControlId(int value) { Value = value; }
        public bool IsValid => Value > 0;
        public bool Equals(CrowdControlId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CrowdControlId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(CrowdControlId other) => Value.CompareTo(other.Value);
        public static bool operator ==(CrowdControlId a, CrowdControlId b) => a.Equals(b);
        public static bool operator !=(CrowdControlId a, CrowdControlId b) => !a.Equals(b);
        public override string ToString() => Value.ToString();
    }

    public enum CrowdControlIntensity : byte
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    public enum CrowdControlDurationRule : byte
    {
        DefaultTenacity = 0,
        IgnoreTenacity = 1,
    }

    /// <summary>
    /// Lightweight logical tag bits (CC v6.2 5.2). One ulong covers the
    /// suggested tag set; bit order is stable and authored at bake time.
    /// </summary>
    public readonly struct CrowdControlTagMask : IEquatable<CrowdControlTagMask>
    {
        public readonly ulong Bits;
        public CrowdControlTagMask(ulong bits) { Bits = bits; }

        public bool HasAny(in CrowdControlTagMask other) =>
            (Bits & other.Bits) != 0UL;
        public bool HasAll(in CrowdControlTagMask other) =>
            (Bits & other.Bits) == other.Bits;
        public bool HasNone(in CrowdControlTagMask other) =>
            (Bits & other.Bits) == 0UL;

        public static CrowdControlTagMask Union(
            in CrowdControlTagMask left,
            in CrowdControlTagMask right) =>
            new CrowdControlTagMask(left.Bits | right.Bits);

        public static CrowdControlTagMask None => default;

        public bool Equals(CrowdControlTagMask other) => Bits == other.Bits;
        public override bool Equals(object obj) => obj is CrowdControlTagMask other && Equals(other);
        public override int GetHashCode() => Bits.GetHashCode();
        public static bool operator ==(CrowdControlTagMask a, CrowdControlTagMask b) => a.Equals(b);
        public static bool operator !=(CrowdControlTagMask a, CrowdControlTagMask b) => !a.Equals(b);
    }

    /// <summary>
    /// All/Any/None tag query shared by immunity, cleanse and state queries
    /// (CC v6.2 5.4).
    /// </summary>
    public readonly struct CrowdControlTagQuery : IEquatable<CrowdControlTagQuery>
    {
        public readonly CrowdControlTagMask All;
        public readonly CrowdControlTagMask Any;
        public readonly CrowdControlTagMask None;

        public CrowdControlTagQuery(
            in CrowdControlTagMask all,
            in CrowdControlTagMask any,
            in CrowdControlTagMask none)
        {
            All = all;
            Any = any;
            None = none;
        }

        public bool IsEmpty =>
            All.Bits == 0UL &&
            Any.Bits == 0UL &&
            None.Bits == 0UL;

        public bool Match(in CrowdControlTagMask tags)
        {
            bool matchesAll = tags.HasAll(All);
            bool matchesAny = Any.Bits == 0UL ||
                tags.HasAny(Any);
            bool matchesNone = tags.HasNone(None);
            return matchesAll && matchesAny && matchesNone;
        }

        public bool Equals(CrowdControlTagQuery other) =>
            All == other.All && Any == other.Any && None == other.None;
        public override bool Equals(object obj) =>
            obj is CrowdControlTagQuery other && Equals(other);
        public override int GetHashCode() =>
            All.GetHashCode() ^ (Any.GetHashCode() << 1) ^ (None.GetHashCode() << 2);
    }

    /// <summary>
    /// One-shot cleanse specification (CC v6.2 5.4 / 6.10). MaxRemoveCount 0
    /// means unlimited.
    /// </summary>
    public readonly struct CrowdControlCleanseSpec
    {
        public readonly CrowdControlTagQuery Query;
        public readonly int MaxRemoveCount;

        public CrowdControlCleanseSpec(
            in CrowdControlTagQuery query,
            int maxRemoveCount)
        {
            Query = query;
            MaxRemoveCount = maxRemoveCount;
        }
    }

    /// <summary>
    /// Which unit actions are prohibited (CC v6.2 6.9). Whether an active
    /// runtime is interrupted is decided by the unit framework.
    /// </summary>
    [Flags]
    public enum UnitActionBlockMask : ulong
    {
        None = 0,
        VoluntaryMove = 1UL << 0,
        Turn = 1UL << 1,
        VoluntaryAttack = 1UL << 2,
        AbilityCast = 1UL << 3,
        Mobility = 1UL << 4,
        EquipmentActive = 1UL << 5,
        SummonerSpell = 1UL << 6,
        ControlMove = 1UL << 7,
        ControlAttack = 1UL << 8,
    }

    /// <summary>
    /// Lightweight aggregated output (CC v6.2 7.1). Value type, no allocation.
    /// </summary>
    public readonly struct CrowdControlStateView
    {
        public readonly UnitActionBlockMask BlockedActions;
        public readonly CrowdControlTagMask ActiveTags;
        public readonly fp MoveSlowRatio;
        public readonly fp AttackSpeedSlowRatio;

        public CrowdControlStateView(
            UnitActionBlockMask blockedActions,
            in CrowdControlTagMask activeTags,
            fp moveSlowRatio,
            fp attackSpeedSlowRatio)
        {
            BlockedActions = blockedActions;
            ActiveTags = activeTags;
            MoveSlowRatio = moveSlowRatio;
            AttackSpeedSlowRatio = attackSpeedSlowRatio;
        }

        public static readonly CrowdControlStateView Empty =
            new CrowdControlStateView(
                UnitActionBlockMask.None,
                CrowdControlTagMask.None,
                fp.zero,
                fp.zero);
    }

    /// <summary>
    /// Winner of ForcedBehavior candidates (CC v6.2 7.6). Produced by the
    /// Collect pass and consumed by BehaviorPlanner.
    /// </summary>
    public enum CrowdControlBehaviorKind : byte
    {
        AttackTarget = 1,
        MoveToTarget = 2,
        FleeDirection = 3,
    }

    public readonly struct CrowdControlBehaviorOverride
    {
        public readonly int InstanceId;
        public readonly int StartTick;
        public readonly short Priority;
        public readonly CrowdControlBehaviorKind Kind;
        public readonly UnitUid TargetUnitUid;
        public readonly fp2 Direction;

        public CrowdControlBehaviorOverride(
            int instanceId,
            int startTick,
            short priority,
            CrowdControlBehaviorKind kind,
            UnitUid targetUnitUid,
            in fp2 direction)
        {
            InstanceId = instanceId;
            StartTick = startTick;
            Priority = priority;
            Kind = kind;
            TargetUnitUid = targetUnitUid;
            Direction = direction;
        }

        public bool IsValid => InstanceId > 0;
        public static readonly CrowdControlBehaviorOverride None = default;
    }

    public enum CrowdControlSignalType : byte
    {
        ActualDamageTaken = 0,
        OwnerActionStarted = 1,
        ForcedMoveFinished = 2,
        Count = 3,
    }

    [Flags]
    public enum CrowdControlSignalMask : ushort
    {
        None = 0,
        ActualDamageTaken = 1 << 0,
        OwnerActionStarted = 1 << 1,
        ForcedMoveFinished = 1 << 2,
    }

    public readonly struct CrowdControlHandle : IEquatable<CrowdControlHandle>
    {
        public readonly UnitUid TargetUnitUid;
        public readonly int InstanceId;
        public CrowdControlHandle(UnitUid targetUnitUid, int instanceId)
        {
            TargetUnitUid = targetUnitUid;
            InstanceId = instanceId;
        }
        public bool IsValid => TargetUnitUid.IsValid() && InstanceId > 0;
        public bool Equals(CrowdControlHandle other) =>
            TargetUnitUid == other.TargetUnitUid && InstanceId == other.InstanceId;
        public override bool Equals(object obj) => obj is CrowdControlHandle other && Equals(other);
        public override int GetHashCode() => TargetUnitUid.GetHashCode() ^ InstanceId;
        public static bool operator ==(CrowdControlHandle a, CrowdControlHandle b) => a.Equals(b);
        public static bool operator !=(CrowdControlHandle a, CrowdControlHandle b) => !a.Equals(b);
    }

    public readonly struct CrowdControlImmunityHandle
    {
        public readonly UnitUid TargetUnitUid;
        public readonly int ImmunityId;
        public CrowdControlImmunityHandle(UnitUid targetUnitUid, int immunityId)
        {
            TargetUnitUid = targetUnitUid;
            ImmunityId = immunityId;
        }
        public bool IsValid => TargetUnitUid.IsValid() && ImmunityId > 0;
    }

    public readonly struct CrowdControlUnstoppableHandle
    {
        public readonly UnitUid TargetUnitUid;
        public readonly int UnstoppableId;
        public CrowdControlUnstoppableHandle(UnitUid targetUnitUid, int unstoppableId)
        {
            TargetUnitUid = targetUnitUid;
            UnstoppableId = unstoppableId;
        }
        public bool IsValid => TargetUnitUid.IsValid() && UnstoppableId > 0;
    }

    /// <summary>
    /// Immunity gate applied before instance creation (CC v6.2 6.4).
    /// BlockCount 0 means unlimited (-1 stored); positive counts consume
    /// per interception.
    /// </summary>
    public readonly struct CrowdControlImmunitySpec
    {
        public readonly CrowdControlTagQuery Query;
        public readonly int DurationTicks;
        public readonly int BlockCount;
        public readonly short Priority;

        public CrowdControlImmunitySpec(
            in CrowdControlTagQuery query,
            int durationTicks,
            int blockCount,
            short priority)
        {
            Query = query;
            DurationTicks = durationTicks;
            BlockCount = blockCount;
            Priority = priority;
        }
    }

    public readonly struct CrowdControlUnstoppableSpec
    {
        public readonly int DurationTicks;
        public CrowdControlUnstoppableSpec(int durationTicks)
        {
            DurationTicks = durationTicks;
        }
    }

    /// <summary>
    /// One immunity rule instance (CC v6.2 6.4). RemainingBlocks -1 means
    /// unlimited.
    /// </summary>
    public struct CrowdControlImmunity
    {
        public int ImmunityId;
        public CrowdControlTagQuery Query;
        public int ExpireTick;
        public int RemainingBlocks;
        public short Priority;
    }

    public struct CrowdControlUnstoppable
    {
        public int UnstoppableId;
        public int ExpireTick;
    }

    public enum CrowdControlAddStatus : byte
    {
        Added = 0,
        BlockedByImmunity = 1,
        RejectedByUnstoppable = 2,
        RejectedByHigherPriority = 3,
        InvalidDefinition = 4,
        InvalidParams = 5,
        InvalidDuration = 6,
        OwnerRejected = 7,
    }

    public readonly struct CrowdControlAddResult
    {
        public readonly CrowdControlAddStatus Status;
        public readonly CrowdControlHandle Handle;
        public readonly int BlockingImmunityId;

        public CrowdControlAddResult(
            CrowdControlAddStatus status,
            CrowdControlHandle handle,
            int blockingImmunityId = 0)
        {
            Status = status;
            Handle = handle;
            BlockingImmunityId = blockingImmunityId;
        }

        public bool Added => Status == CrowdControlAddStatus.Added;
    }

    public enum ControlRemoveReason : byte
    {
        Explicit = 0,
        NaturalExpire = 1,
        Cleanse = 2,
        Replaced = 3,
        SuppressedByUnstoppable = 4,
        Death = 5,
        Respawn = 6,
        Despawn = 7,
        OwnerEnded = 8,
    }

    /// <summary>
    /// Authoritative rollback snapshot (CC v6.2 10.4). Signals are included;
    /// state/behaviorOverride/dirty are reconstructed.
    /// </summary>
    public struct CrowdControlHandlerSnapshot
    {
        public List<CrowdControlInstance> Instances;
        public List<CrowdControlImmunity> Immunities;
        public List<CrowdControlUnstoppable> Unstoppables;
        public int NextInstanceId;
        public int NextImmunityId;
        public int NextUnstoppableId;
        public CrowdControlHandle ActiveForcedMoveHandle;
        public CrowdControlSignalMask PendingSignals;
        public int[] SignalEffectiveTicks;
        public static readonly CrowdControlHandlerSnapshot Default = default;
    }
}
