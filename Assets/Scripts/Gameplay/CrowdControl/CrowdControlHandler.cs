using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class CrowdControlHandler : UnitHandler, IRollback<CrowdControlHandlerSnapshot>
    {
        private readonly List<CrowdControlConstraint> instances = new List<CrowdControlConstraint>(8);
        private readonly List<CrowdControlImmunitySnapshot> immunities = new List<CrowdControlImmunitySnapshot>(4);
        private readonly List<CrowdControlUnstoppableSnapshot> unstoppables = new List<CrowdControlUnstoppableSnapshot>(2);
        private CrowdControlConstraint activeConstraint;
        private CrowdControlHandle activeForcedMoveHandle;
        private int nextInstanceId = 1;
        private int nextImmunityId = 1;
        private int nextUnstoppableId = 1;

        public override void InitializeForNewRuntime() => ClearRuntimeState();
        public CrowdControlConstraint ActiveConstraint => activeConstraint;
        public CrowdControlHandle ActiveForcedMoveHandle => activeForcedMoveHandle;
        public int Count => instances.Count;
        public bool IsImmune => immunities.Count != 0;
        public bool IsUnstoppable => unstoppables.Count != 0;
        public bool IsMovementRestricted => activeConstraint.IsActive &&
            (activeConstraint.Type == CrowdControlType.Stun || activeConstraint.Type == CrowdControlType.Root ||
             activeConstraint.Type == CrowdControlType.Knockback || activeConstraint.Type == CrowdControlType.Suppression);
        public bool IsActionRestricted => activeConstraint.IsActive &&
            (activeConstraint.Type == CrowdControlType.Stun || activeConstraint.Type == CrowdControlType.Silence ||
             activeConstraint.Type == CrowdControlType.Disarm || activeConstraint.Type == CrowdControlType.Suppression);

        public CrowdControlAddResult Add(in CrowdControlConstraint input)
        {
            if (input.Type == CrowdControlType.None)
                return new CrowdControlAddResult(CrowdControlAddStatus.InvalidParams, default);
            if (input.RemainingTicks <= 0)
                return new CrowdControlAddResult(CrowdControlAddStatus.InvalidDuration, default);
            if (input.IsForcedMove &&
                (input.ForcedMoveConfigId <= 0 ||
                 fpmath.lengthsq(
                     input.ForcedMoveDeltaPerTick) <=
                     fp.zero))
            {
                return new CrowdControlAddResult(
                    CrowdControlAddStatus.InvalidParams,
                    default);
            }
            if (IsImmune)
                return new CrowdControlAddResult(CrowdControlAddStatus.BlockedByImmunity, default);
            if (input.IsForcedMove && IsUnstoppable)
                return new CrowdControlAddResult(CrowdControlAddStatus.RejectedByUnstoppable, default);
            if (input.IsForcedMove && activeForcedMoveHandle.IsValid &&
                TryGet(activeForcedMoveHandle, out CrowdControlConstraint current) &&
                input.Priority < current.Priority)
                return new CrowdControlAddResult(CrowdControlAddStatus.RejectedByHigherPriority, default);
            if (nextInstanceId == int.MaxValue)
                throw new DeterministicSimulationException("Crowd-control instance ID exhausted.");

            bool replacesForcedMove =
                input.IsForcedMove &&
                activeForcedMoveHandle.IsValid;
            if (replacesForcedMove)
            {
                RemoveInstance(
                    activeForcedMoveHandle,
                    stopMovement: false);
            }
            CrowdControlConstraint instance = input;
            instance.InstanceId = nextInstanceId++;
            instance.StartLogicTick = SimulationTickContext.Current.Tick;
            instances.Add(instance);
            var handle = new CrowdControlHandle(Owner.UnitUid, instance.InstanceId);
            if (instance.IsForcedMove)
            {
                activeForcedMoveHandle = handle;
                ResolvedForcedMove resolved =
                    BuildResolvedForcedMove(
                        instance,
                        handle);
                if (replacesForcedMove)
                {
                    Owner.MovementHandler
                        .ReplaceForcedMove(resolved);
                }
                else
                {
                    Owner.MovementHandler
                        .StartForcedMove(resolved);
                }
            }
            RefreshState();
            return new CrowdControlAddResult(CrowdControlAddStatus.Added, handle);
        }

        public bool SubmitConstraint(CrowdControlConstraint constraint) => Add(constraint).Added;

        public bool TryGetActiveForcedMove(
            UnitUid sourceUnitUid,
            int configId,
            out CrowdControlHandle handle)
        {
            handle = activeForcedMoveHandle;
            return handle.IsValid &&
                   TryGet(
                       handle,
                       out CrowdControlConstraint instance) &&
                   instance.IsForcedMove &&
                   instance.SourceUnitUid ==
                       sourceUnitUid &&
                   instance.ForcedMoveConfigId ==
                       configId;
        }

        public bool Remove(CrowdControlHandle handle, ControlRemoveReason reason)
        {
            return RemoveInstance(
                handle,
                stopMovement: true);
        }

        public CrowdControlImmunityHandle AddImmunity(in CrowdControlImmunitySpec spec)
        {
            if (spec.DurationTicks <= 0) return default;
            if (nextImmunityId == int.MaxValue)
                throw new DeterministicSimulationException("Crowd-control immunity ID exhausted.");
            int id = nextImmunityId++;
            immunities.Add(new CrowdControlImmunitySnapshot { ImmunityId = id, RemainingTicks = spec.DurationTicks });
            RemoveAllInstances();
            return new CrowdControlImmunityHandle(Owner.UnitUid, id);
        }

        public bool RemoveImmunity(CrowdControlImmunityHandle handle)
        {
            if (handle.TargetUnitUid != Owner.UnitUid) return false;
            for (int i = 0; i < immunities.Count; i++)
                if (immunities[i].ImmunityId == handle.ImmunityId)
                { immunities.RemoveAt(i); return true; }
            return false;
        }

        public CrowdControlUnstoppableHandle AddUnstoppable(in CrowdControlUnstoppableSpec spec)
        {
            if (spec.DurationTicks <= 0) return default;
            if (nextUnstoppableId == int.MaxValue)
                throw new DeterministicSimulationException("Crowd-control unstoppable ID exhausted.");
            int id = nextUnstoppableId++;
            unstoppables.Add(new CrowdControlUnstoppableSnapshot { UnstoppableId = id, RemainingTicks = spec.DurationTicks });
            if (activeForcedMoveHandle.IsValid)
                Remove(activeForcedMoveHandle, ControlRemoveReason.SuppressedByUnstoppable);
            RefreshState();
            return new CrowdControlUnstoppableHandle(Owner.UnitUid, id);
        }

        public bool RemoveUnstoppable(CrowdControlUnstoppableHandle handle)
        {
            if (handle.TargetUnitUid != Owner.UnitUid) return false;
            for (int i = 0; i < unstoppables.Count; i++)
                if (unstoppables[i].UnstoppableId == handle.UnstoppableId)
                { unstoppables.RemoveAt(i); RefreshState(); return true; }
            return false;
        }

        public void GrantImmunity() => AddImmunity(new CrowdControlImmunitySpec(int.MaxValue));
        public void RevokeImmunity() => immunities.Clear();
        public void GrantUnstoppable(int durationTicks) => AddUnstoppable(new CrowdControlUnstoppableSpec(durationTicks));

        public void TickUpdate()
        {
            int delta = SimulationTickContext.Current.DeltaTick;
            AdvanceInstances(delta);
            AdvanceImmunities(delta);
            AdvanceUnstoppables(delta);
            RefreshState();
        }

        public void OnForcedMoveFinished(
            CrowdControlHandle sourceHandle)
        {
            if (sourceHandle !=
                activeForcedMoveHandle)
            {
                return;
            }
            RemoveInstance(
                sourceHandle,
                stopMovement: false);
        }

        public override void ClearForDeath() => ClearRuntimeState();
        public override void ClearForRespawn() => ClearRuntimeState();
        public override void ResetForPool() => ClearRuntimeState();

        public void Capture(ref CrowdControlHandlerSnapshot state)
        {
            state.Instances = new System.Collections.Generic.List<CrowdControlConstraint>(instances);
            state.Immunities = new System.Collections.Generic.List<CrowdControlImmunitySnapshot>(immunities);
            state.Unstoppables = new System.Collections.Generic.List<CrowdControlUnstoppableSnapshot>(unstoppables);
            state.NextInstanceId = nextInstanceId;
            state.NextImmunityId = nextImmunityId;
            state.NextUnstoppableId = nextUnstoppableId;
            state.ActiveForcedMoveHandle = activeForcedMoveHandle;
        }

        public void Restore(in CrowdControlHandlerSnapshot state)
        {
            instances.Clear(); immunities.Clear(); unstoppables.Clear();
            if (state.Instances != null) instances.AddRange(state.Instances);
            if (state.Immunities != null) immunities.AddRange(state.Immunities);
            if (state.Unstoppables != null) unstoppables.AddRange(state.Unstoppables);
            ValidateCanonicalIds();
            nextInstanceId = state.NextInstanceId;
            nextImmunityId = state.NextImmunityId;
            nextUnstoppableId = state.NextUnstoppableId;
            activeForcedMoveHandle = state.ActiveForcedMoveHandle;
            RefreshState();
        }

        public void Resolve(in RollbackContext context)
        {
            UnitWorld world = Owner.World;
            if (world == null) return;
            for (int i = 0; i < instances.Count; i++)
            {
                UnitUid source = instances[i].SourceUnitUid;
                if (source.IsValid() && !world.TryGetUnit(source, out _))
                    throw new DeterministicSimulationException($"Crowd control references missing source {source}.");
            }
            if (activeForcedMoveHandle.IsValid && !TryGet(activeForcedMoveHandle, out _))
                throw new DeterministicSimulationException("Active forced-move handle is invalid after restore.");
        }

        public void Rebuild(in RollbackContext context) => RefreshState();

        private bool TryGet(CrowdControlHandle handle, out CrowdControlConstraint instance)
        {
            for (int i = 0; i < instances.Count; i++)
                if (instances[i].InstanceId == handle.InstanceId)
                { instance = instances[i]; return true; }
            instance = default;
            return false;
        }

        private void RefreshState()
        {
            activeConstraint = default;
            if (IsUnstoppable) return;
            for (int i = 0; i < instances.Count; i++)
            {
                CrowdControlConstraint candidate = instances[i];
                if (!candidate.IsActive) continue;
                if (!activeConstraint.IsActive || candidate.Priority > activeConstraint.Priority ||
                    (candidate.Priority == activeConstraint.Priority && candidate.InstanceId < activeConstraint.InstanceId))
                    activeConstraint = candidate;
            }
        }

        private void AdvanceInstances(int delta)
        {
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                CrowdControlConstraint entry = instances[i];
                entry.RemainingTicks -= delta;
                if (entry.RemainingTicks <= 0)
                {
                    if (activeForcedMoveHandle.InstanceId ==
                        entry.InstanceId)
                    {
                        entry.RemainingTicks = 0;
                        instances[i] = entry;
                    }
                    else
                    {
                        instances.RemoveAt(i);
                    }
                }
                else instances[i] = entry;
            }
        }

        private void AdvanceImmunities(int delta)
        {
            for (int i = immunities.Count - 1; i >= 0; i--)
            {
                CrowdControlImmunitySnapshot entry = immunities[i];
                if (entry.RemainingTicks != int.MaxValue) entry.RemainingTicks -= delta;
                if (entry.RemainingTicks <= 0) immunities.RemoveAt(i); else immunities[i] = entry;
            }
        }

        private void AdvanceUnstoppables(int delta)
        {
            for (int i = unstoppables.Count - 1; i >= 0; i--)
            {
                CrowdControlUnstoppableSnapshot entry = unstoppables[i];
                entry.RemainingTicks -= delta;
                if (entry.RemainingTicks <= 0) unstoppables.RemoveAt(i); else unstoppables[i] = entry;
            }
        }

        private void RemoveAllInstances()
        {
            CrowdControlHandle forced =
                activeForcedMoveHandle;
            instances.Clear();
            activeForcedMoveHandle = default;
            if (forced.IsValid)
            {
                Owner?.MovementHandler
                    ?.StopForcedMove(forced);
            }
            RefreshState();
        }

        private bool RemoveInstance(
            CrowdControlHandle handle,
            bool stopMovement)
        {
            if (Owner == null ||
                handle.TargetUnitUid != Owner.UnitUid)
            {
                return false;
            }
            for (int i = 0;
                 i < instances.Count;
                 i++)
            {
                if (instances[i].InstanceId !=
                    handle.InstanceId)
                {
                    continue;
                }

                instances.RemoveAt(i);
                if (activeForcedMoveHandle == handle)
                {
                    activeForcedMoveHandle = default;
                    if (stopMovement)
                    {
                        Owner.MovementHandler
                            ?.StopForcedMove(handle);
                    }
                }
                RefreshState();
                return true;
            }
            return false;
        }

        private ResolvedForcedMove
            BuildResolvedForcedMove(
                in CrowdControlConstraint instance,
                CrowdControlHandle handle)
        {
            fp2 start =
                Owner.PhysicsEntity.Transform2D.Position;
            fp2 totalDelta =
                instance.ForcedMoveDeltaPerTick *
                instance.RemainingTicks;
            Physics.PhysicsGeometry2D.TryCreateFacing(
                instance.ForcedMoveDeltaPerTick,
                out fp2 direction,
                out _);
            return new ResolvedForcedMove(
                handle,
                instance.ForcedMoveConfigId,
                instance.RemainingTicks,
                direction,
                start + totalDelta,
                instance.ForcedMoveWallPolicy);
        }

        private void ValidateCanonicalIds()
        {
            for (int i = 0; i < instances.Count; i++)
                if (instances[i].InstanceId <= 0 || (i > 0 && instances[i - 1].InstanceId >= instances[i].InstanceId))
                    throw new DeterministicSimulationException("Crowd-control instances are not in canonical ID order.");
            for (int i = 0; i < immunities.Count; i++)
                if (immunities[i].ImmunityId <= 0 || (i > 0 && immunities[i - 1].ImmunityId >= immunities[i].ImmunityId))
                    throw new DeterministicSimulationException("Crowd-control immunities are not in canonical ID order.");
            for (int i = 0; i < unstoppables.Count; i++)
                if (unstoppables[i].UnstoppableId <= 0 || (i > 0 && unstoppables[i - 1].UnstoppableId >= unstoppables[i].UnstoppableId))
                    throw new DeterministicSimulationException("Crowd-control unstoppable entries are not in canonical ID order.");
        }

        private void ClearRuntimeState()
        {
            CrowdControlHandle forced =
                activeForcedMoveHandle;
            instances.Clear(); immunities.Clear(); unstoppables.Clear();
            activeConstraint = default; activeForcedMoveHandle = default;
            nextInstanceId = 1; nextImmunityId = 1; nextUnstoppableId = 1;
            if (forced.IsValid)
            {
                Owner?.MovementHandler
                    ?.StopForcedMove(forced);
            }
        }
    }
}
