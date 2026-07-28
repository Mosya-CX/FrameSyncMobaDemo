using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct CrowdControlConstraint
    {
        public int InstanceId;
        public CrowdControlType Type;
        public int StartLogicTick;
        public int RemainingTicks;
        public byte Priority;
        public UnitUid SourceUnitUid;
        public bool IsForcedMove;
        public int ForcedMoveConfigId;
        public fp2 ForcedMoveDeltaPerTick;
        public ForceMoveWallPolicy ForcedMoveWallPolicy;
        public bool IsActive => RemainingTicks > 0;
        public static readonly CrowdControlConstraint None = default;
    }

    public readonly struct CrowdControlHandle : IEquatable<CrowdControlHandle>
    {
        public readonly UnitUid TargetUnitUid;
        public readonly int InstanceId;
        public CrowdControlHandle(UnitUid targetUnitUid, int instanceId) { TargetUnitUid = targetUnitUid; InstanceId = instanceId; }
        public bool IsValid => TargetUnitUid.IsValid() && InstanceId > 0;
        public bool Equals(CrowdControlHandle other) => TargetUnitUid == other.TargetUnitUid && InstanceId == other.InstanceId;
        public override bool Equals(object obj) => obj is CrowdControlHandle other && Equals(other);
        public override int GetHashCode() => TargetUnitUid.GetHashCode() ^ InstanceId;
        public static bool operator ==(CrowdControlHandle a, CrowdControlHandle b) => a.Equals(b);
        public static bool operator !=(CrowdControlHandle a, CrowdControlHandle b) => !a.Equals(b);
    }

    public readonly struct CrowdControlImmunityHandle
    {
        public readonly UnitUid TargetUnitUid;
        public readonly int ImmunityId;
        public CrowdControlImmunityHandle(UnitUid targetUnitUid, int immunityId) { TargetUnitUid = targetUnitUid; ImmunityId = immunityId; }
        public bool IsValid => TargetUnitUid.IsValid() && ImmunityId > 0;
    }

    public readonly struct CrowdControlUnstoppableHandle
    {
        public readonly UnitUid TargetUnitUid;
        public readonly int UnstoppableId;
        public CrowdControlUnstoppableHandle(UnitUid targetUnitUid, int unstoppableId) { TargetUnitUid = targetUnitUid; UnstoppableId = unstoppableId; }
        public bool IsValid => TargetUnitUid.IsValid() && UnstoppableId > 0;
    }

    public readonly struct CrowdControlImmunitySpec
    {
        public readonly int DurationTicks;
        public CrowdControlImmunitySpec(int durationTicks) => DurationTicks = durationTicks;
    }

    public readonly struct CrowdControlUnstoppableSpec
    {
        public readonly int DurationTicks;
        public CrowdControlUnstoppableSpec(int durationTicks) => DurationTicks = durationTicks;
    }

    public enum CrowdControlAddStatus : byte
    {
        Added, BlockedByImmunity, RejectedByUnstoppable,
        RejectedByHigherPriority, InvalidParams, InvalidDuration,
    }

    public readonly struct CrowdControlAddResult
    {
        public readonly CrowdControlAddStatus Status;
        public readonly CrowdControlHandle Handle;
        public CrowdControlAddResult(CrowdControlAddStatus status, CrowdControlHandle handle) { Status = status; Handle = handle; }
        public bool Added => Status == CrowdControlAddStatus.Added;
    }

    public enum ControlRemoveReason : byte
    {
        Manual, NaturalExpire, Cleanse, Death, Respawn, SuppressedByUnstoppable,
    }

    public struct CrowdControlImmunitySnapshot { public int ImmunityId; public int RemainingTicks; }
    public struct CrowdControlUnstoppableSnapshot { public int UnstoppableId; public int RemainingTicks; }

    public struct CrowdControlHandlerSnapshot
    {
        public System.Collections.Generic.List<CrowdControlConstraint> Instances;
        public System.Collections.Generic.List<CrowdControlImmunitySnapshot> Immunities;
        public System.Collections.Generic.List<CrowdControlUnstoppableSnapshot> Unstoppables;
        public int NextInstanceId;
        public int NextImmunityId;
        public int NextUnstoppableId;
        public CrowdControlHandle ActiveForcedMoveHandle;
        public static readonly CrowdControlHandlerSnapshot Default = default;
    }
}
