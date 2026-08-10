using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>Stable module ids (CC v6.2 3.8).</summary>
    public static class CrowdControlModuleId
    {
        public const ushort BlockActions = 1;
        public const ushort MaxMoveSlow = 2;
        public const ushort MaxAttackSpeedSlow = 3;
        public const ushort MinVisionScale = 4;
        public const ushort BasicAttackMiss = 5;
        public const ushort ForcedBehavior = 6;
        public const ushort ForcedMoveOnAdd = 7;
        public const ushort RemoveOnSignal = 8;
        public const ushort AddControlOnNaturalExpire = 9;
        public const ushort Count = 10;
    }

    public enum ControlModuleHook : byte
    {
        OnAdd = 0,
        Collect = 1,
        OnSignal = 2,
        OnRemove = 3,
    }

    /// <summary>
    /// Compact baked module operation (CC v6.2 3.4). Different modules
    /// interpret only the fields they need.
    /// </summary>
    [System.Serializable]
    public struct ControlModuleOp
    {
        public int ExecutorIndex;
        public ControlModuleHook Hook;
        public int StaticData;
        public fp StaticFp0;
        public fp StaticFp1;
        public int ParamOffset0;
        public int ParamOffset1;
        public int ParamOffset2;
        public int ParamOffset3;
    }

    public enum ControlModuleCommandKind : byte
    {
        RemoveSelf = 0,
        AddControl = 1,
        StartForcedMove = 2,
        ReplaceForcedMove = 3,
        StopForcedMove = 4,
    }

    /// <summary>
    /// Deferred command produced by a module callback (CC v6.2 3.10). Flushed
    /// by the Handler after the current traversal; never mutates instances
    /// during a loop.
    /// </summary>
    public readonly struct ControlModuleCommand
    {
        public readonly ControlModuleCommandKind Kind;
        public readonly CrowdControlId ControlId;
        public readonly CrowdControlHandle Handle;
        public readonly ResolvedForcedMove ForcedMove;
        public readonly ControlRemoveReason RemoveReason;
        public readonly int AddDurationTicks;

        public ControlModuleCommand(
            ControlModuleCommandKind kind,
            CrowdControlId controlId = default,
            CrowdControlHandle handle = default,
            in ResolvedForcedMove forcedMove = default,
            ControlRemoveReason removeReason =
                ControlRemoveReason.Explicit,
            int addDurationTicks = 0)
        {
            Kind = kind;
            ControlId = controlId;
            Handle = handle;
            ForcedMove = forcedMove;
            RemoveReason = removeReason;
            AddDurationTicks = addDurationTicks;
        }

        public static ControlModuleCommand RemoveSelf(
            ControlRemoveReason reason =
                ControlRemoveReason.Explicit,
            CrowdControlHandle handle = default) =>
            new ControlModuleCommand(
                ControlModuleCommandKind.RemoveSelf,
                handle: handle,
                removeReason: reason);

        public static ControlModuleCommand AddControl(
            CrowdControlId controlId,
            int durationTicks) =>
            new ControlModuleCommand(
                ControlModuleCommandKind.AddControl,
                controlId: controlId,
                addDurationTicks: durationTicks);

        public static ControlModuleCommand StartForcedMove(
            in ResolvedForcedMove forcedMove) =>
            new ControlModuleCommand(
                ControlModuleCommandKind.StartForcedMove,
                forcedMove: forcedMove);

        public static ControlModuleCommand ReplaceForcedMove(
            in ResolvedForcedMove forcedMove) =>
            new ControlModuleCommand(
                ControlModuleCommandKind.ReplaceForcedMove,
                forcedMove: forcedMove);

        public static ControlModuleCommand StopForcedMove(
            CrowdControlHandle handle) =>
            new ControlModuleCommand(
                ControlModuleCommandKind.StopForcedMove,
                handle: handle);
    }

    public delegate void ControlOnAddFn(
        Unit owner,
        in CrowdControlInstance instance,
        in ControlModuleOp op,
        List<ControlModuleCommand> commands);

    public delegate void ControlCollectFn(
        Unit owner,
        in CrowdControlInstance instance,
        in ControlModuleOp op,
        ref ControlAccumulator accumulator);

    public delegate void ControlSignalFn(
        Unit owner,
        in CrowdControlInstance instance,
        in ControlModuleOp op,
        CrowdControlSignalType signal,
        List<ControlModuleCommand> commands);

    public delegate void ControlOnRemoveFn(
        Unit owner,
        in CrowdControlInstance instance,
        in ControlModuleOp op,
        ControlRemoveReason reason,
        List<ControlModuleCommand> commands);

    public readonly struct CrowdControlModuleExecutor
    {
        public readonly ControlOnAddFn OnAdd;
        public readonly ControlCollectFn Collect;
        public readonly ControlSignalFn OnSignal;
        public readonly ControlOnRemoveFn OnRemove;

        public CrowdControlModuleExecutor(
            ControlOnAddFn onAdd,
            ControlCollectFn collect,
            ControlSignalFn onSignal,
            ControlOnRemoveFn onRemove)
        {
            OnAdd = onAdd;
            Collect = collect;
            OnSignal = onSignal;
            OnRemove = onRemove;
        }
    }

    /// <summary>
    /// Global read-only module executor table (CC v6.2 3.5). Registration is
    /// one-time static construction; no reflection, no per-instance objects.
    /// </summary>
    public static class CrowdControlModuleExecutors
    {
        private static readonly CrowdControlModuleExecutor[] Table =
            new CrowdControlModuleExecutor[
                CrowdControlModuleId.Count];

        static CrowdControlModuleExecutors()
        {
            Table[CrowdControlModuleId.BlockActions] =
                new CrowdControlModuleExecutor(
                    null,
                    BlockActionsCollect,
                    null,
                    null);
            Table[CrowdControlModuleId.MaxMoveSlow] =
                new CrowdControlModuleExecutor(
                    null,
                    MaxMoveSlowCollect,
                    null,
                    null);
            Table[CrowdControlModuleId.MaxAttackSpeedSlow] =
                new CrowdControlModuleExecutor(
                    null,
                    MaxAttackSpeedSlowCollect,
                    null,
                    null);
            Table[CrowdControlModuleId.MinVisionScale] =
                new CrowdControlModuleExecutor(
                    null,
                    MinVisionScaleCollect,
                    null,
                    null);
            Table[CrowdControlModuleId.BasicAttackMiss] =
                new CrowdControlModuleExecutor(
                    null,
                    BasicAttackMissCollect,
                    null,
                    null);
            Table[CrowdControlModuleId.ForcedBehavior] =
                new CrowdControlModuleExecutor(
                    null,
                    ForcedBehaviorCollect,
                    null,
                    null);
            Table[CrowdControlModuleId.ForcedMoveOnAdd] =
                new CrowdControlModuleExecutor(
                    ForcedMoveOnAdd,
                    null,
                    null,
                    null);
            Table[CrowdControlModuleId.RemoveOnSignal] =
                new CrowdControlModuleExecutor(
                    null,
                    null,
                    RemoveOnSignal,
                    null);
            Table[CrowdControlModuleId.AddControlOnNaturalExpire] =
                new CrowdControlModuleExecutor(
                    null,
                    null,
                    null,
                    AddControlOnNaturalExpire);
        }

        public static CrowdControlModuleExecutor Get(
            int executorIndex)
        {
            if (executorIndex < 0 ||
                executorIndex >= Table.Length)
            {
                throw new DeterministicSimulationException(
                    $"Crowd-control module executor index {executorIndex} is out of range.");
            }
            return Table[executorIndex];
        }

        private static void BlockActionsCollect(
            Unit owner,
            in CrowdControlInstance instance,
            in ControlModuleOp op,
            ref ControlAccumulator accumulator)
        {
            accumulator.BlockedActions |=
                (UnitActionBlockMask)op.StaticData;
        }

        private static void MaxMoveSlowCollect(
            Unit owner,
            in CrowdControlInstance instance,
            in ControlModuleOp op,
            ref ControlAccumulator accumulator)
        {
            fp value =
                instance.Params.ReadFp(op.ParamOffset0);
            if (value > accumulator.MoveSlowRatio)
            {
                accumulator.MoveSlowRatio = value;
            }
        }

        private static void MaxAttackSpeedSlowCollect(
            Unit owner,
            in CrowdControlInstance instance,
            in ControlModuleOp op,
            ref ControlAccumulator accumulator)
        {
            fp value =
                instance.Params.ReadFp(op.ParamOffset0);
            if (value >
                accumulator.AttackSpeedSlowRatio)
            {
                accumulator.AttackSpeedSlowRatio =
                    value;
            }
        }

        private static void MinVisionScaleCollect(
            Unit owner,
            in CrowdControlInstance instance,
            in ControlModuleOp op,
            ref ControlAccumulator accumulator)
        {
            fp value =
                instance.Params.ReadFp(op.ParamOffset0);
            if (!accumulator.HasVisionScale ||
                value < accumulator.VisionScale)
            {
                accumulator.VisionScale = value;
                accumulator.HasVisionScale = true;
            }
        }

        private static void BasicAttackMissCollect(
            Unit owner,
            in CrowdControlInstance instance,
            in ControlModuleOp op,
            ref ControlAccumulator accumulator)
        {
            // Effect contract: AttackHandler consults
            // CrowdControlHandler.IsBasicAttackMissed (derived from this
            // module) at hit time. The module only aggregates the flag.
            accumulator.BasicAttackMiss = true;
        }

        private static void ForcedBehaviorCollect(
            Unit owner,
            in CrowdControlInstance instance,
            in ControlModuleOp op,
            ref ControlAccumulator accumulator)
        {
            int behaviorIdValue =
                instance.Params.ReadInt(op.ParamOffset0);
            if (behaviorIdValue <
                    (int)CrowdControlBehaviorKind.AttackTarget ||
                behaviorIdValue >
                    (int)CrowdControlBehaviorKind.FleeDirection)
            {
                throw new DeterministicSimulationException(
                    "ForcedBehavior BehaviorId is outside the framework behavior-kind range.");
            }
            short priority =
                instance.Params.ReadShort(op.ParamOffset1);
            UnitUid targetUnit =
                op.ParamOffset2 >= 0
                    ? instance.Params.ReadUnitUid(
                        op.ParamOffset2)
                    : default;
            fp2 direction =
                op.ParamOffset3 >= 0
                    ? instance.Params.ReadFp2(
                        op.ParamOffset3)
                    : fp2.zero;

            CrowdControlBehaviorOverride candidate =
                new CrowdControlBehaviorOverride(
                    instance.InstanceId,
                    instance.StartTick,
                    priority,
                    (CrowdControlBehaviorKind)behaviorIdValue,
                    targetUnit,
                    direction);

            CrowdControlBehaviorOverride current =
                accumulator.BehaviorCandidate;
            if (!current.IsValid ||
                candidate.Priority > current.Priority ||
                (candidate.Priority == current.Priority &&
                 (candidate.StartTick > current.StartTick ||
                  (candidate.StartTick == current.StartTick &&
                   candidate.InstanceId > current.InstanceId))))
            {
                accumulator.BehaviorCandidate =
                    candidate;
            }
        }

        private static void ForcedMoveOnAdd(
            Unit owner,
            in CrowdControlInstance instance,
            in ControlModuleOp op,
            List<ControlModuleCommand> commands)
        {
            fp2 direction =
                instance.Params.ReadFp2(op.ParamOffset0);
            fp distance =
                instance.Params.ReadFp(op.ParamOffset1);
            int moveTicks =
                instance.Params.ReadInt(op.ParamOffset2);
            if (moveTicks <= 0 ||
                distance <= fp.zero)
            {
                throw new DeterministicSimulationException(
                    "ForcedMoveOnAdd instance has invalid trajectory parameters.");
            }

            fp2 start =
                owner.PhysicsEntity.Transform2D.Position;
            if (!Physics.PhysicsGeometry2D.TryCreateFacing(
                    direction,
                    out fp2 facing,
                    out _))
            {
                throw new DeterministicSimulationException(
                    "ForcedMoveOnAdd direction is zero.");
            }
            fp2 target =
                start + facing * distance;

            ResolvedForcedMove resolved =
                new ResolvedForcedMove(
                    instance.MakeHandle(owner.UnitUid),
                    op.StaticData,
                    moveTicks,
                    facing,
                    target,
                    (ForceMoveWallPolicy)(int)op.StaticFp0);

            commands.Add(
                ControlModuleCommand.StartForcedMove(
                    resolved));
        }

        private static void RemoveOnSignal(
            Unit owner,
            in CrowdControlInstance instance,
            in ControlModuleOp op,
            CrowdControlSignalType signal,
            List<ControlModuleCommand> commands)
        {
            if (((int)op.StaticData &
                 (1 << (int)signal)) != 0)
            {
                commands.Add(
                    ControlModuleCommand.RemoveSelf(
                        ControlRemoveReason.Explicit,
                        instance.MakeHandle(owner.UnitUid)));
            }
        }

        private static void AddControlOnNaturalExpire(
            Unit owner,
            in CrowdControlInstance instance,
            in ControlModuleOp op,
            ControlRemoveReason reason,
            List<ControlModuleCommand> commands)
        {
            if (reason == ControlRemoveReason.NaturalExpire &&
                op.StaticData > 0)
            {
                if (op.ParamOffset0 < 0)
                {
                    throw new DeterministicSimulationException(
                        "AddControlOnNaturalExpire requires a bound duration parameter.");
                }
                commands.Add(
                    ControlModuleCommand.AddControl(
                        new CrowdControlId(
                            op.StaticData),
                        instance.Params.ReadInt(
                            op.ParamOffset0)));
            }
        }
    }

    /// <summary>
    /// Internal collector for one RebuildOutputs pass (CC v6.2 3.7).
    /// </summary>
    public struct ControlAccumulator
    {
        public UnitActionBlockMask BlockedActions;
        public CrowdControlTagMask ActiveTags;
        public fp MoveSlowRatio;
        public fp AttackSpeedSlowRatio;
        public fp VisionScale;
        public bool HasVisionScale;
        public bool BasicAttackMiss;
        public CrowdControlBehaviorOverride BehaviorCandidate;

        public void Clear()
        {
            BlockedActions = UnitActionBlockMask.None;
            ActiveTags = CrowdControlTagMask.None;
            MoveSlowRatio = fp.zero;
            AttackSpeedSlowRatio = fp.zero;
            VisionScale = fp.zero;
            HasVisionScale = false;
            BasicAttackMiss = false;
            BehaviorCandidate =
                CrowdControlBehaviorOverride.None;
        }

        public CrowdControlStateView ToStateView() =>
            new CrowdControlStateView(
                BlockedActions,
                ActiveTags,
                MoveSlowRatio,
                AttackSpeedSlowRatio);
    }
}
