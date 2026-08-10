using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Sole runtime entry of the control system (CC v6.2 1.1). Creates
    /// independent instances from global Definitions, manages time,
    /// immunity/unstoppable/cleanse, lightweight signals, unique forced move
    /// arbitration and module-driven output aggregation. No Kind branches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrowdControlHandler :
        UnitHandler,
        IRollback<CrowdControlHandlerSnapshot>
    {
        public const int InfiniteTicks = int.MaxValue;
        public const int SignalRetentionTicks = 2;
        public const int MinControlTicks = 1;
        private const int MaxFlushedCommands = 32;
        private const int InvalidTick = -1;

        private readonly List<CrowdControlInstance> instances =
            new List<CrowdControlInstance>(8);
        private readonly List<CrowdControlImmunity> immunities =
            new List<CrowdControlImmunity>(4);
        private readonly List<CrowdControlUnstoppable> unstoppables =
            new List<CrowdControlUnstoppable>(2);
        private readonly List<ControlModuleCommand> pendingCommands =
            new List<ControlModuleCommand>(4);
        private readonly List<CrowdControlHandle> batchHandles =
            new List<CrowdControlHandle>(8);
        private readonly int[] signalEffectiveTicks =
            new int[(int)CrowdControlSignalType.Count];

        private CrowdControlSignalMask pendingSignals;
        private int nextInstanceId = 1;
        private int nextImmunityId = 1;
        private int nextUnstoppableId = 1;
        private CrowdControlHandle activeForcedMoveHandle;
        private CrowdControlStateView state;
        private CrowdControlBehaviorOverride behaviorOverride;
        private bool dirty;
        private int batchDepth;
        private bool flushingCommands;
        private fp minVisionScale;
        private bool hasVisionScale;
        private bool basicAttackMiss;

        public CrowdControlStateView State => state;
        public int Count => instances.Count;
        public bool IsUnstoppable => unstoppables.Count != 0;
        public CrowdControlHandle ActiveForcedMoveHandle =>
            activeForcedMoveHandle;
        public fp CurrentMinVisionScale =>
            hasVisionScale ? minVisionScale : fp.one;
        public bool IsBasicAttackMissed => basicAttackMiss;

        public override void InitializeForNewRuntime()
        {
            if (instances.Count != 0 ||
                immunities.Count != 0 ||
                unstoppables.Count != 0)
            {
                throw new DeterministicSimulationException(
                    "CrowdControlHandler.InitializeForNewRuntime requires empty runtime state.");
            }
            ResetRuntimeState();
        }

        public override void ClearForDeath() =>
            RemoveAll(ControlRemoveReason.Death);

        public override void ClearForRespawn() =>
            RemoveAll(ControlRemoveReason.Respawn);

        public override void ResetForPool() =>
            RemoveAll(ControlRemoveReason.Despawn);

        /// <summary>
        /// Try to create a new independent control instance (CC v6.2 1.6).
        /// </summary>
        public CrowdControlAddResult Add(
            CrowdControlId controlId,
            int durationTicks,
            in CrowdControlParamWriter parameters)
        {
            int currentTick =
                SimulationTickContext.Current.Tick;
            CrowdControlDefinition definition =
                ResolveDefinition(controlId);
            if (definition == null ||
                !definition.IsValid)
            {
                return new CrowdControlAddResult(
                    CrowdControlAddStatus.InvalidDefinition,
                    default);
            }
            if (!CanAcceptControl())
            {
                return new CrowdControlAddResult(
                    CrowdControlAddStatus.OwnerRejected,
                    default);
            }
            if (!parameters.Materialize(
                    definition.ParamLayout,
                    out CrowdControlParamBlock paramBlock))
            {
                return new CrowdControlAddResult(
                    CrowdControlAddStatus.InvalidParams,
                    default);
            }

            CrowdControlTagMask tags =
                definition.Tags;
            bool isForcedMove =
                tags.HasAny(
                    new CrowdControlTagMask(
                        CrowdControlDefinition.ControlTagBits.ForcedMove));

            if (IsUnstoppable && isForcedMove)
            {
                return new CrowdControlAddResult(
                    CrowdControlAddStatus.RejectedByUnstoppable,
                    default);
            }

            if (CanBeResisted(definition))
            {
                int blockingImmunityId =
                    TryBlockByImmunity(
                        definition,
                        currentTick);
                if (blockingImmunityId != 0)
                {
                    return new CrowdControlAddResult(
                        CrowdControlAddStatus.BlockedByImmunity,
                        default,
                        blockingImmunityId);
                }
            }

            CrowdControlHandle replaced =
                default;
            if (isForcedMove &&
                activeForcedMoveHandle.IsValid)
            {
                CrowdControlInstance current =
                    FindInstance(
                        activeForcedMoveHandle);
                int newPriority =
                    ReadForcedMovePriority(
                        paramBlock,
                        definition);
                int currentPriority =
                    ReadForcedMovePriority(
                        current.Params,
                        definition);
                if (newPriority < currentPriority)
                {
                    return new CrowdControlAddResult(
                        CrowdControlAddStatus.RejectedByHigherPriority,
                        default);
                }
                replaced = activeForcedMoveHandle;
            }

            int effectiveTicks =
                durationTicks == InfiniteTicks
                    ? InfiniteTicks
                    : ApplyTenacity(
                        definition,
                        durationTicks,
                        Owner);
            if (durationTicks != InfiniteTicks &&
                effectiveTicks <= 0)
            {
                return new CrowdControlAddResult(
                    CrowdControlAddStatus.InvalidDuration,
                    default);
            }

            if (nextInstanceId == int.MaxValue)
            {
                throw new DeterministicSimulationException(
                    "Crowd-control instance ID exhausted.");
            }

            var instance = new CrowdControlInstance(
                nextInstanceId++,
                controlId,
                currentTick,
                durationTicks == InfiniteTicks
                    ? InfiniteTicks
                    : currentTick + effectiveTicks,
                paramBlock);
            instances.Add(instance);
            CrowdControlHandle handle =
                instance.MakeHandle(Owner.UnitUid);

            if (isForcedMove)
            {
                ClearSignal(
                    CrowdControlSignalType.ForcedMoveFinished);
                activeForcedMoveHandle = handle;
            }

            if (!IsUnstoppable)
            {
                RunOps(
                    definition.BakedOnAddOps,
                    instance,
                    currentTick);
            }

            FlushModuleCommands(replaced);
            if (replaced.IsValid)
            {
                Remove(
                    replaced,
                    ControlRemoveReason.Replaced);
            }

            dirty = true;
            RebuildOutputsIfDirty();
            return new CrowdControlAddResult(
                CrowdControlAddStatus.Added,
                handle);
        }

        public bool Remove(
            CrowdControlHandle handle,
            ControlRemoveReason reason)
        {
            if (Owner == null ||
                !handle.IsValid ||
                handle.TargetUnitUid != Owner.UnitUid)
            {
                return false;
            }
            int index = FindInstanceIndex(handle);
            if (index < 0)
            {
                return false;
            }
            CrowdControlInstance instance =
                instances[index];
            CrowdControlDefinition definition =
                ResolveDefinition(instance.ControlId);
            if (definition != null)
            {
                RunRemoveOps(
                    definition.BakedOnRemoveOps,
                    instance,
                    reason);
            }

            if (handle == activeForcedMoveHandle)
            {
                activeForcedMoveHandle = default;
                pendingCommands.Add(
                    ControlModuleCommand.StopForcedMove(
                        handle));
            }
            instances.RemoveAt(index);
            FlushModuleCommands(default);
            dirty = true;
            RebuildOutputsIfDirty();
            return true;
        }

        public int RemoveAll(
            ControlRemoveReason reason)
        {
            if (instances.Count == 0 &&
                immunities.Count == 0 &&
                unstoppables.Count == 0)
            {
                return 0;
            }

            batchHandles.Clear();
            for (int i = 0;
                 i < instances.Count;
                 i++)
            {
                batchHandles.Add(
                    instances[i].MakeHandle(
                        Owner.UnitUid));
            }

            BeginBatch();
            for (int i = 0;
                 i < batchHandles.Count;
                 i++)
            {
                Remove(
                    batchHandles[i],
                    reason);
            }
            immunities.Clear();
            unstoppables.Clear();
            pendingSignals = CrowdControlSignalMask.None;
            ClearAllSignalTicks();
            activeForcedMoveHandle = default;
            EndBatch();
            return batchHandles.Count;
        }

        public int Cleanse(
            in CrowdControlCleanseSpec spec)
        {
            if (spec.Query.IsEmpty)
            {
                return 0;
            }
            batchHandles.Clear();
            int count = 0;
            for (int i = 0;
                 i < instances.Count;
                 i++)
            {
                CrowdControlInstance instance =
                    instances[i];
                CrowdControlDefinition definition =
                    ResolveDefinition(
                        instance.ControlId);
                if (definition == null ||
                    !CanBeResisted(definition))
                {
                    continue;
                }
                if (!spec.Query.Match(
                        definition.Tags))
                {
                    continue;
                }
                batchHandles.Add(
                    instance.MakeHandle(
                        Owner.UnitUid));
                count++;
                if (spec.MaxRemoveCount > 0 &&
                    count >= spec.MaxRemoveCount)
                {
                    break;
                }
            }

            BeginBatch();
            for (int i = 0;
                 i < batchHandles.Count;
                 i++)
            {
                Remove(
                    batchHandles[i],
                    ControlRemoveReason.Cleanse);
            }
            EndBatch();
            return count;
        }

        public CrowdControlImmunityHandle AddImmunity(
            in CrowdControlImmunitySpec spec)
        {
            if (spec.Query.IsEmpty)
            {
                return default;
            }
            if (spec.DurationTicks <= 0)
            {
                return default;
            }
            if (nextImmunityId == int.MaxValue)
            {
                throw new DeterministicSimulationException(
                    "Crowd-control immunity ID exhausted.");
            }
            int currentTick =
                SimulationTickContext.Current.Tick;
            var immunity = new CrowdControlImmunity
            {
                ImmunityId = nextImmunityId++,
                Query = spec.Query,
                ExpireTick =
                    spec.DurationTicks == InfiniteTicks
                        ? InfiniteTicks
                        : currentTick + spec.DurationTicks,
                RemainingBlocks =
                    spec.BlockCount == 0
                        ? -1
                        : spec.BlockCount,
                Priority = spec.Priority,
            };
            InsertImmunity(immunity);
            return new CrowdControlImmunityHandle(
                Owner.UnitUid,
                immunity.ImmunityId);
        }

        public bool RemoveImmunity(
            CrowdControlImmunityHandle handle)
        {
            if (handle.TargetUnitUid != Owner.UnitUid)
            {
                return false;
            }
            for (int i = 0;
                 i < immunities.Count;
                 i++)
            {
                if (immunities[i].ImmunityId ==
                    handle.ImmunityId)
                {
                    immunities.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public CrowdControlUnstoppableHandle AddUnstoppable(
            in CrowdControlUnstoppableSpec spec)
        {
            if (spec.DurationTicks <= 0)
            {
                return default;
            }
            if (nextUnstoppableId == int.MaxValue)
            {
                throw new DeterministicSimulationException(
                    "Crowd-control unstoppable ID exhausted.");
            }
            int currentTick =
                SimulationTickContext.Current.Tick;
            bool wasUnstoppable = IsUnstoppable;
            unstoppables.Add(
                new CrowdControlUnstoppable
                {
                    UnstoppableId =
                        nextUnstoppableId++,
                    ExpireTick =
                        spec.DurationTicks == InfiniteTicks
                            ? InfiniteTicks
                            : currentTick +
                              spec.DurationTicks,
                });

            if (!wasUnstoppable)
            {
                if (activeForcedMoveHandle.IsValid)
                {
                    Remove(
                        activeForcedMoveHandle,
                        ControlRemoveReason
                            .SuppressedByUnstoppable);
                }
                dirty = true;
                RebuildOutputsIfDirty();
            }
            return new CrowdControlUnstoppableHandle(
                Owner.UnitUid,
                nextUnstoppableId - 1);
        }

        public bool RemoveUnstoppable(
            CrowdControlUnstoppableHandle handle)
        {
            if (handle.TargetUnitUid != Owner.UnitUid)
            {
                return false;
            }
            for (int i = 0;
                 i < unstoppables.Count;
                 i++)
            {
                if (unstoppables[i].UnstoppableId ==
                    handle.UnstoppableId)
                {
                    unstoppables.RemoveAt(i);
                    if (!IsUnstoppable)
                    {
                        dirty = true;
                        RebuildOutputsIfDirty();
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>Per-tick advance: broadcast signals, expire controls,
        /// immunity and unstoppable (CC v6.2 1.9).</summary>
        public void Advance()
        {
            int currentTick =
                SimulationTickContext.Current.Tick;

            CrowdControlSignalMask signalMask =
                pendingSignals;
            pendingSignals = CrowdControlSignalMask.None;

            for (int type = 0;
                 type < (int)CrowdControlSignalType.Count;
                 type++)
            {
                CrowdControlSignalMask mask =
                    (CrowdControlSignalMask)(1 << type);
                if ((signalMask & mask) == 0)
                {
                    continue;
                }
                int signalTick =
                    signalEffectiveTicks[type];
                if (currentTick - signalTick >
                    SignalRetentionTicks)
                {
                    continue;
                }
                BroadcastSignal(
                    (CrowdControlSignalType)type,
                    signalTick);
            }
            FlushModuleCommands(default);

            batchHandles.Clear();
            for (int i = 0;
                 i < instances.Count;
                 i++)
            {
                CrowdControlInstance instance =
                    instances[i];
                if (instance.ExpireTick !=
                        InfiniteTicks &&
                    instance.ExpireTick <=
                        currentTick)
                {
                    batchHandles.Add(
                        instance.MakeHandle(
                            Owner.UnitUid));
                }
            }
            BeginBatch();
            for (int i = 0;
                 i < batchHandles.Count;
                 i++)
            {
                Remove(
                    batchHandles[i],
                    ControlRemoveReason.NaturalExpire);
            }
            EndBatch();

            AdvanceImmunities(currentTick);
            AdvanceUnstoppables(currentTick);
            FlushModuleCommands(default);
            RebuildOutputsIfDirty();
        }

        public void OnDamageTaken(
            in DamageEventData evt)
        {
            if (evt.ActualDamage <= fp.zero)
            {
                return;
            }
            RaiseSignal(
                CrowdControlSignalType.ActualDamageTaken);
        }

        public void OnOwnerActionStarted()
        {
            RaiseSignal(
                CrowdControlSignalType.OwnerActionStarted);
        }

        public void OnForcedMoveFinished(
            CrowdControlHandle sourceHandle)
        {
            if (sourceHandle != activeForcedMoveHandle)
            {
                return;
            }
            RaiseSignal(
                CrowdControlSignalType.ForcedMoveFinished);
        }

        public bool HasAnyTag(
            CrowdControlTagMask tags) =>
            state.ActiveTags.HasAny(tags);

        /// <summary>Whether the aggregated state blocks one unit action
        /// (CC v6.2 6.9 / 7.7).</summary>
        public bool IsBlocked(
            UnitActionBlockMask action) =>
            (state.BlockedActions & action) != 0;

        public bool MatchesTags(
            in CrowdControlTagQuery query) =>
            query.Match(state.ActiveTags);

        public bool TryGetBehaviorOverride(
            out CrowdControlBehaviorOverride value)
        {
            if (!IsUnstoppable &&
                behaviorOverride.IsValid)
            {
                value = behaviorOverride;
                return true;
            }
            value = CrowdControlBehaviorOverride.None;
            return false;
        }

        public int GetRemainingTicks(
            CrowdControlHandle handle)
        {
            CrowdControlInstance instance =
                FindInstance(handle);
            if (instance.InstanceId == 0)
            {
                return 0;
            }
            if (instance.ExpireTick == InfiniteTicks)
            {
                return InfiniteTicks;
            }
            int remaining =
                instance.ExpireTick -
                SimulationTickContext.Current.Tick;
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>
        /// Copy current instances into a caller-provided reusable buffer
        /// (CC v6.2 1.11 FillInstances; no allocation inside the handler).
        /// </summary>
        public void FillInstances(
            List<CrowdControlInstance> buffer)
        {
            if (buffer == null)
            {
                throw new System.ArgumentNullException(
                    nameof(buffer));
            }
            buffer.Clear();
            buffer.AddRange(instances);
        }

        public void Capture(
            ref CrowdControlHandlerSnapshot state)
        {
            if (batchDepth != 0)
            {
                throw new DeterministicSimulationException(
                    "CrowdControlHandler snapshot point must not be inside a batch.");
            }
            state.Instances =
                new List<CrowdControlInstance>(
                    instances);
            state.Immunities =
                new List<CrowdControlImmunity>(
                    immunities);
            state.Unstoppables =
                new List<CrowdControlUnstoppable>(
                    unstoppables);
            state.NextInstanceId = nextInstanceId;
            state.NextImmunityId = nextImmunityId;
            state.NextUnstoppableId = nextUnstoppableId;
            state.ActiveForcedMoveHandle =
                activeForcedMoveHandle;
            state.PendingSignals = pendingSignals;
            if (state.SignalEffectiveTicks == null ||
                state.SignalEffectiveTicks.Length !=
                    signalEffectiveTicks.Length)
            {
                state.SignalEffectiveTicks =
                    new int[signalEffectiveTicks.Length];
            }
            for (int i = 0;
                 i < signalEffectiveTicks.Length;
                 i++)
            {
                state.SignalEffectiveTicks[i] =
                    signalEffectiveTicks[i];
            }
        }

        public void Restore(
            in CrowdControlHandlerSnapshot state)
        {
            instances.Clear();
            immunities.Clear();
            unstoppables.Clear();
            if (state.Instances != null)
            {
                instances.AddRange(state.Instances);
            }
            if (state.Immunities != null)
            {
                immunities.AddRange(state.Immunities);
            }
            if (state.Unstoppables != null)
            {
                unstoppables.AddRange(
                    state.Unstoppables);
            }
            ValidateCanonicalState();
            nextInstanceId = state.NextInstanceId;
            nextImmunityId = state.NextImmunityId;
            nextUnstoppableId = state.NextUnstoppableId;
            activeForcedMoveHandle =
                state.ActiveForcedMoveHandle;
            pendingSignals = state.PendingSignals;
            ClearAllSignalTicks();
            if (state.SignalEffectiveTicks != null)
            {
                int length =
                    state.SignalEffectiveTicks.Length <
                    signalEffectiveTicks.Length
                        ? state.SignalEffectiveTicks.Length
                        : signalEffectiveTicks.Length;
                for (int i = 0;
                     i < length;
                     i++)
                {
                    signalEffectiveTicks[i] =
                        state.SignalEffectiveTicks[i];
                }
            }
            dirty = true;
        }

        public void Resolve(
            in RollbackContext context)
        {
            // Instances/handles/param blocks hold only stable logical
            // identity; there is nothing to re-resolve by object reference
            // (CC v6.2 10.4).
        }

        public void Rebuild(
            in RollbackContext context)
        {
            dirty = true;
            RebuildOutputsIfDirty();
        }

        private bool CanAcceptControl()
        {
            return Owner != null &&
                Owner.LifeState == LifeState.Alive;
        }

        private CrowdControlDefinition ResolveDefinition(
            CrowdControlId controlId)
        {
            if (Owner?.World == null ||
                Owner.World.CrowdControlDefinitions ==
                    null)
            {
                return null;
            }
            return Owner.World
                .CrowdControlDefinitions.TryGet(
                    controlId,
                    out CrowdControlDefinition definition)
                ? definition
                : null;
        }

        private static bool CanBeResisted(
            CrowdControlDefinition definition) =>
            definition.Intensity !=
            CrowdControlIntensity.High;

        private int TryBlockByImmunity(
            CrowdControlDefinition definition,
            int currentTick)
        {
            for (int i = 0;
                 i < immunities.Count;
                 i++)
            {
                CrowdControlImmunity immunity =
                    immunities[i];
                if (immunity.ExpireTick !=
                        InfiniteTicks &&
                    immunity.ExpireTick <=
                        currentTick)
                {
                    continue;
                }
                if (!immunity.Query.Match(
                        definition.Tags))
                {
                    continue;
                }
                if (immunity.RemainingBlocks > 0)
                {
                    immunity.RemainingBlocks--;
                    if (immunity.RemainingBlocks == 0)
                    {
                        immunities.RemoveAt(i);
                    }
                    else
                    {
                        immunities[i] = immunity;
                    }
                }
                return immunity.ImmunityId;
            }
            return 0;
        }

        private void InsertImmunity(
            in CrowdControlImmunity immunity)
        {
            int index = 0;
            while (index < immunities.Count)
            {
                CrowdControlImmunity other =
                    immunities[index];
                if (other.Priority <
                        immunity.Priority ||
                    (other.Priority ==
                         immunity.Priority &&
                     other.ImmunityId <
                         immunity.ImmunityId))
                {
                    index++;
                }
                else
                {
                    break;
                }
            }
            immunities.Insert(index, immunity);
        }

        private static int ApplyTenacity(
            CrowdControlDefinition definition,
            int baseTicks,
            Unit owner)
        {
            if (definition.DurationRule ==
                CrowdControlDurationRule.IgnoreTenacity)
            {
                return baseTicks;
            }
            fp tenacity =
                owner?.StatHandler?.GetStat(
                    StatId.Tenacity) ?? fp.zero;
            fp reduced =
                fp.one - Clamp01(tenacity);
            fp effectiveFp =
                fpmath.ceil(
                    (fp)baseTicks * reduced);
            int effective =
                (int)effectiveFp;
            return effective < MinControlTicks
                ? MinControlTicks
                : effective;
        }

        private static fp Clamp01(fp value)
        {
            if (value < fp.zero) return fp.zero;
            if (value > fp.one) return fp.one;
            return value;
        }

        private static int ReadForcedMovePriority(
            in CrowdControlParamBlock block,
            CrowdControlDefinition definition)
        {
            if (definition.ParamLayout.TryGet(
                    ControlParamKeys.ForcedMovePriority,
                    out CrowdControlParamLayoutEntry entry))
            {
                return block.ReadShort(entry.Offset);
            }
            return 0;
        }

        private void RunOps(
            ControlModuleOp[] ops,
            in CrowdControlInstance instance,
            int currentTick)
        {
            if (ops == null)
            {
                return;
            }
            for (int i = 0;
                 i < ops.Length;
                 i++)
            {
                ControlModuleOp op = ops[i];
                CrowdControlModuleExecutor executor =
                    CrowdControlModuleExecutors.Get(
                        op.ExecutorIndex);
                executor.OnAdd?.Invoke(
                    Owner,
                    instance,
                    op,
                    pendingCommands);
            }
        }

        private void RunRemoveOps(
            ControlModuleOp[] ops,
            in CrowdControlInstance instance,
            ControlRemoveReason reason)
        {
            if (ops == null)
            {
                return;
            }
            for (int i = 0;
                 i < ops.Length;
                 i++)
            {
                ControlModuleOp op = ops[i];
                CrowdControlModuleExecutor executor =
                    CrowdControlModuleExecutors.Get(
                        op.ExecutorIndex);
                executor.OnRemove?.Invoke(
                    Owner,
                    instance,
                    op,
                    reason,
                    pendingCommands);
            }
        }

        private void BroadcastSignal(
            CrowdControlSignalType type,
            int signalTick)
        {
            for (int i = 0;
                 i < instances.Count;
                 i++)
            {
                CrowdControlInstance instance =
                    instances[i];
                if (signalTick < instance.StartTick)
                {
                    continue;
                }
                CrowdControlDefinition definition =
                    ResolveDefinition(
                        instance.ControlId);
                if (definition == null)
                {
                    throw new DeterministicSimulationException(
                        $"Crowd control instance {instance.InstanceId} references missing definition {instance.ControlId.Value}.");
                }
                CrowdControlSignalMask mask =
                    (CrowdControlSignalMask)
                    (1 << (int)type);
                if ((definition.BakedSignalMask &
                     mask) == 0)
                {
                    continue;
                }
                ControlModuleOp[] ops =
                    definition.BakedSignalOps;
                if (ops == null)
                {
                    continue;
                }
                for (int opIndex = 0;
                     opIndex < ops.Length;
                     opIndex++)
                {
                    ControlModuleOp op =
                        ops[opIndex];
                    CrowdControlModuleExecutor executor =
                        CrowdControlModuleExecutors.Get(
                            op.ExecutorIndex);
                    executor.OnSignal?.Invoke(
                        Owner,
                        instance,
                        op,
                        type,
                        pendingCommands);
                }
            }
        }

        private void FlushModuleCommands(
            CrowdControlHandle replacedForcedMove)
        {
            if (flushingCommands)
            {
                // Nested flush from a module command: the outer loop already
                // owns pendingCommands and will pick up appended commands.
                return;
            }
            flushingCommands = true;
            int processed = 0;
            int cursor = 0;
            try
            {
                while (cursor < pendingCommands.Count)
                {
                    ControlModuleCommand command =
                        pendingCommands[cursor];
                    pendingCommands[cursor] = default;
                    cursor++;
                    processed++;
                    if (processed > MaxFlushedCommands)
                    {
                        throw new DeterministicSimulationException(
                            "Crowd-control module command loop exceeded the flush limit (A->B->A recursion).");
                    }
                    ExecuteModuleCommand(
                        command,
                        replacedForcedMove);
                }
            }
            finally
            {
                pendingCommands.Clear();
                flushingCommands = false;
            }
        }

        private void ExecuteModuleCommand(
            in ControlModuleCommand command,
            CrowdControlHandle replacedForcedMove)
        {
            switch (command.Kind)
            {
                case ControlModuleCommandKind.RemoveSelf:
                    if (command.Handle.IsValid)
                    {
                        Remove(
                            command.Handle,
                            command.RemoveReason);
                    }
                    break;
                case ControlModuleCommandKind.AddControl:
                    Add(
                        command.ControlId,
                        command.AddDurationTicks,
                        default);
                    break;
                case ControlModuleCommandKind.StartForcedMove:
                    if (replacedForcedMove.IsValid)
                    {
                        Owner?.MovementHandler
                            ?.ReplaceForcedMove(
                                command.ForcedMove);
                    }
                    else
                    {
                        Owner?.MovementHandler
                            ?.StartForcedMove(
                                command.ForcedMove);
                    }
                    break;
                case ControlModuleCommandKind.ReplaceForcedMove:
                    Owner?.MovementHandler
                        ?.ReplaceForcedMove(
                            command.ForcedMove);
                    break;
                case ControlModuleCommandKind.StopForcedMove:
                    Owner?.MovementHandler
                        ?.StopForcedMove(
                            command.Handle);
                    break;
            }
        }

        private void RaiseSignal(
            CrowdControlSignalType type)
        {
            int currentTick =
                SimulationTickContext.Current.Tick;
            if (signalEffectiveTicks[(int)type] ==
                currentTick)
            {
                return;
            }
            signalEffectiveTicks[(int)type] =
                currentTick;
            pendingSignals |=
                (CrowdControlSignalMask)
                (1 << (int)type);
        }

        private void ClearSignal(
            CrowdControlSignalType type)
        {
            pendingSignals &=
                (CrowdControlSignalMask)
                ~(1 << (int)type);
            signalEffectiveTicks[(int)type] =
                InvalidTick;
        }

        private void ClearAllSignalTicks()
        {
            for (int i = 0;
                 i < signalEffectiveTicks.Length;
                 i++)
            {
                signalEffectiveTicks[i] =
                    InvalidTick;
            }
        }

        private void AdvanceImmunities(
            int currentTick)
        {
            for (int i = immunities.Count - 1;
                 i >= 0;
                 i--)
            {
                CrowdControlImmunity immunity =
                    immunities[i];
                if (immunity.ExpireTick !=
                        InfiniteTicks &&
                    immunity.ExpireTick <=
                        currentTick)
                {
                    immunities.RemoveAt(i);
                }
            }
        }

        private void AdvanceUnstoppables(
            int currentTick)
        {
            for (int i = unstoppables.Count - 1;
                 i >= 0;
                 i--)
            {
                CrowdControlUnstoppable entry =
                    unstoppables[i];
                if (entry.ExpireTick !=
                        InfiniteTicks &&
                    entry.ExpireTick <=
                        currentTick)
                {
                    unstoppables.RemoveAt(i);
                }
            }
        }

        private void RebuildOutputsIfDirty()
        {
            if (!dirty || batchDepth != 0)
            {
                return;
            }
            RebuildOutputs();
        }

        private void RebuildOutputs()
        {
            var accumulator = new ControlAccumulator();
            if (IsUnstoppable)
            {
                state = CrowdControlStateView.Empty;
                behaviorOverride =
                    CrowdControlBehaviorOverride.None;
                minVisionScale = fp.zero;
                hasVisionScale = false;
                basicAttackMiss = false;
                dirty = false;
                return;
            }

            for (int i = 0;
                 i < instances.Count;
                 i++)
            {
                CrowdControlInstance instance =
                    instances[i];
                CrowdControlDefinition definition =
                    ResolveDefinition(
                        instance.ControlId);
                if (definition == null)
                {
                    throw new DeterministicSimulationException(
                        $"Crowd control instance {instance.InstanceId} references missing definition {instance.ControlId.Value}.");
                }
                accumulator.ActiveTags =
                    CrowdControlTagMask.Union(
                        accumulator.ActiveTags,
                        definition.Tags);
                ControlModuleOp[] ops =
                    definition.BakedCollectOps;
                if (ops == null)
                {
                    continue;
                }
                for (int opIndex = 0;
                     opIndex < ops.Length;
                     opIndex++)
                {
                    ControlModuleOp op = ops[opIndex];
                    CrowdControlModuleExecutor executor =
                        CrowdControlModuleExecutors.Get(
                            op.ExecutorIndex);
                    executor.Collect?.Invoke(
                        Owner,
                        instance,
                        op,
                        ref accumulator);
                }
            }

            state = accumulator.ToStateView();
            behaviorOverride =
                accumulator.BehaviorCandidate;
            minVisionScale =
                accumulator.VisionScale;
            hasVisionScale =
                accumulator.HasVisionScale;
            basicAttackMiss =
                accumulator.BasicAttackMiss;
            dirty = false;
        }

        private CrowdControlInstance FindInstance(
            CrowdControlHandle handle)
        {
            int index = FindInstanceIndex(handle);
            return index >= 0
                ? instances[index]
                : default;
        }

        private int FindInstanceIndex(
            CrowdControlHandle handle)
        {
            for (int i = 0;
                 i < instances.Count;
                 i++)
            {
                if (instances[i].InstanceId ==
                    handle.InstanceId)
                {
                    return i;
                }
            }
            return -1;
        }

        private void ValidateCanonicalState()
        {
            for (int i = 0;
                 i < instances.Count;
                 i++)
            {
                if (instances[i].InstanceId <= 0 ||
                    (i > 0 &&
                     instances[i - 1].InstanceId >=
                         instances[i].InstanceId))
                {
                    throw new DeterministicSimulationException(
                        "Crowd-control instances are not in canonical InstanceId order.");
                }
            }
            for (int i = 0;
                 i < immunities.Count;
                 i++)
            {
                CrowdControlImmunity immunity =
                    immunities[i];
                if (immunity.ImmunityId <= 0 ||
                    immunity.Query.IsEmpty)
                {
                    throw new DeterministicSimulationException(
                        "Crowd-control immunity snapshot is invalid.");
                }
                if (i > 0)
                {
                    CrowdControlImmunity previous =
                        immunities[i - 1];
                    if (previous.Priority <
                            immunity.Priority ||
                        (previous.Priority ==
                             immunity.Priority &&
                         previous.ImmunityId >=
                             immunity.ImmunityId))
                    {
                        throw new DeterministicSimulationException(
                            "Crowd-control immunities are not in canonical Priority/Id order.");
                    }
                }
            }
            for (int i = 0;
                 i < unstoppables.Count;
                 i++)
            {
                if (unstoppables[i].UnstoppableId <= 0 ||
                    (i > 0 &&
                     unstoppables[i - 1]
                         .UnstoppableId >=
                         unstoppables[i]
                             .UnstoppableId))
                {
                    throw new DeterministicSimulationException(
                        "Crowd-control unstoppable entries are not in canonical Id order.");
                }
            }
            if (activeForcedMoveHandle.IsValid &&
                FindInstanceIndex(
                    activeForcedMoveHandle) < 0)
            {
                throw new DeterministicSimulationException(
                    "Crowd-control active forced-move handle is invalid after restore.");
            }
        }

        private void BeginBatch()
        {
            batchDepth++;
        }

        private void EndBatch()
        {
            batchDepth--;
            if (batchDepth < 0)
            {
                throw new DeterministicSimulationException(
                    "Crowd-control batch depth underflow.");
            }
            dirty = true;
            RebuildOutputsIfDirty();
        }

        private void ResetRuntimeState()
        {
            instances.Clear();
            immunities.Clear();
            unstoppables.Clear();
            pendingCommands.Clear();
            pendingSignals = CrowdControlSignalMask.None;
            ClearAllSignalTicks();
            nextInstanceId = 1;
            nextImmunityId = 1;
            nextUnstoppableId = 1;
            activeForcedMoveHandle = default;
            state = CrowdControlStateView.Empty;
            behaviorOverride =
                CrowdControlBehaviorOverride.None;
            dirty = false;
            batchDepth = 0;
            minVisionScale = fp.zero;
            hasVisionScale = false;
            basicAttackMiss = false;
        }
    }
}
