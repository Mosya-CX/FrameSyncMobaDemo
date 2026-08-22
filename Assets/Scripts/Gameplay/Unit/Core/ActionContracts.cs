using System;

namespace FrameSyncMoba.Unit
{
    [Flags]
    public enum ActionResource : ushort
    {
        None = 0,
        MainAction = 1 << 0,
        BaseAction = 1 << 1,
        Movement = 1 << 2,
        Facing = 1 << 3,
        Attack = 1 << 4,
        Ability = 1 << 5,
    }

    public enum ActionSlot : byte { None = 0, Main = 1, Base = 2 }
    public enum ActionInterruptLevel : byte { None = 0, Ordinary = 1, Forced = 2 }
    public enum ActionRuntimePhase : byte { None = 0, Moving = 1, AttackWindup = 2, AbilityStage = 3 }
    public enum ActionSubmitOutcome : byte { Rejected = 0, Granted = 1, GrantedWithPreemption = 2 }

    public enum ActionRejectReason : byte
    {
        None = 0,
        InvalidRequest = 1,
        MissingCapability = 2,
        BlockedByControl = 3,
        BlockedByActiveCast = 4,
        ResourceConflict = 5,
        ActiveActionUninterruptible = 6,
        HandlerRejected = 7,
        InvalidAbilityStage = 8,
    }

    public readonly struct ActionStartSpec
    {
        public readonly ActionSlot Slot;
        public readonly ActionResource RequiredFreeResources;
        public readonly ActionResource OccupiedResources;
        public readonly ActionInterruptLevel InterruptLevel;
        public readonly bool Interruptible;
        public readonly bool BlocksVoluntaryMove;

        public ActionStartSpec(
            ActionSlot slot,
            ActionResource requiredFreeResources,
            ActionResource occupiedResources,
            ActionInterruptLevel interruptLevel,
            bool interruptible,
            bool blocksVoluntaryMove)
        {
            Slot = slot;
            RequiredFreeResources = requiredFreeResources;
            OccupiedResources = occupiedResources;
            InterruptLevel = interruptLevel;
            Interruptible = interruptible;
            BlocksVoluntaryMove = blocksVoluntaryMove;
        }
    }

    public readonly struct ActionSubmitResult
    {
        public readonly ActionSubmitOutcome Outcome;
        public readonly ActionRejectReason RejectReason;
        public readonly ActionStartSpec StartSpec;
        public bool IsGranted => Outcome != ActionSubmitOutcome.Rejected;

        private ActionSubmitResult(
            ActionSubmitOutcome outcome,
            ActionRejectReason rejectReason,
            in ActionStartSpec startSpec)
        {
            Outcome = outcome;
            RejectReason = rejectReason;
            StartSpec = startSpec;
        }

        public static ActionSubmitResult Reject(ActionRejectReason reason) =>
            new ActionSubmitResult(ActionSubmitOutcome.Rejected, reason, default);

        public static ActionSubmitResult Grant(in ActionStartSpec startSpec, bool preempted) =>
            new ActionSubmitResult(
                preempted ? ActionSubmitOutcome.GrantedWithPreemption : ActionSubmitOutcome.Granted,
                ActionRejectReason.None,
                startSpec);
    }

    /// <summary>Diagnostic-only value; never enters Snapshot/checksum.</summary>
    public readonly struct ActionDecisionTraceRecord
    {
        public readonly int LogicTick;
        public readonly UnitUid UnitUid;
        public readonly IntentKind IntentKind;
        public readonly ActionKind ActionKind;
        public readonly ActionSubmitOutcome Outcome;
        public readonly ActionRejectReason RejectReason;
        public readonly ActionSlot Slot;
        public readonly ActionResource RequiredResources;
        public readonly ActionResource OccupiedBefore;

        public ActionDecisionTraceRecord(
            int logicTick,
            UnitUid unitUid,
            IntentKind intentKind,
            ActionKind actionKind,
            ActionSubmitOutcome outcome,
            ActionRejectReason rejectReason,
            ActionSlot slot,
            ActionResource requiredResources,
            ActionResource occupiedBefore)
        {
            LogicTick = logicTick;
            UnitUid = unitUid;
            IntentKind = intentKind;
            ActionKind = actionKind;
            Outcome = outcome;
            RejectReason = rejectReason;
            Slot = slot;
            RequiredResources = requiredResources;
            OccupiedBefore = occupiedBefore;
        }
    }

    public struct ActionRuntimeSlotSnapshot
    {
        public bool IsOccupied;
        public ActionSlot Slot;
        public ActionKind Kind;
        public ActionRuntimePhase Phase;
        public ActionResource OccupiedResources;
        public bool Interruptible;
        public bool BlocksVoluntaryMove;
        public bool IsControlAction;
        public UnitUid TargetUnitUid;
        public byte AbilitySlot;
        public static ActionRuntimeSlotSnapshot Empty => default;
    }

    public struct ActionRuntimeSetSnapshot
    {
        public ActionRuntimeSlotSnapshot Main;
        public ActionRuntimeSlotSnapshot Base;
        public static ActionRuntimeSetSnapshot Empty => default;
    }
}
