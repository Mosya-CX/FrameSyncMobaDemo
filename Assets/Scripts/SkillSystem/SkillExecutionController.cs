using Unity.Mathematics.FixedPoint;
using UnityEngine;

[RequireComponent(typeof(SkillBook))]
public sealed class SkillExecutionController : MonoBehaviour, IStateful, IActionLockProvider
{
    private sealed class LaneRuntimeState
    {
        public SkillExecution Current;
        public SkillExecution Paused;
        public bool HasQueuedRequest;
        public SkillCastRequest QueuedRequest;
    }

    private UnitCore owner;
    private SkillBook skillBook;
    private LaneRuntimeState[] laneStates;

    public event System.Action<SkillPresentationEvent> PresentationEventEmitted;

    public UnitCore Owner => owner;
    public SkillBook SkillBook => skillBook;
    public SkillBlackboard SharedBlackboard { get; } = new SkillBlackboard();

    public SkillExecution CurrentExecution => GetLaneState(SkillExecutionLane.Main).Current;
    public SkillExecution PassiveExecution => GetLaneState(SkillExecutionLane.Passive).Current;
    public SkillExecution MobilityExecution => GetLaneState(SkillExecutionLane.Mobility).Current;
    public SkillExecution OverlayExecution => GetLaneState(SkillExecutionLane.Overlay).Current;

    private void Awake()
    {
        owner = GetComponent<UnitCore>();
        skillBook = GetComponent<SkillBook>();

        laneStates = new LaneRuntimeState[4];
        for (int i = 0; i < laneStates.Length; i++)
            laneStates[i] = new LaneRuntimeState();
    }

    public bool TryPlanCast(in SkillCastRequest request, out SkillCastPlan plan)
    {
        plan = default;

        if (owner == null || skillBook == null)
            return false;

        if (!skillBook.TryGetDef(request.SkillId, out var def) || def == null)
            return false;

        if (!skillBook.TryGetRuntime(request.SkillId, out var runtime) || runtime == null)
            return false;

        if (def.IsPassive)
            return false;

        if (!CheckBuiltInPreview(owner, def, runtime))
            return false;

        if (!SkillTargetResolver.TryResolve(owner, def, request, out var resolvedCast))
            return false;

        if (!SkillApproachResolver.ResolveRange(owner, def, ref resolvedCast))
            return false;

        if (!CheckBuiltInCommit(owner, def, runtime, resolvedCast))
            return false;

        int stepIndex = ResolveStepIndex(def, runtime);
        if (def.Steps == null || stepIndex < 0 || stepIndex >= def.Steps.Length || def.Steps[stepIndex] == null)
            return false;

        var step = def.Steps[stepIndex];
        if (!RunStepGates(owner, def, step, request, resolvedCast))
            return false;

        plan = new SkillCastPlan
        {
            Def = def,
            Runtime = runtime,
            ResolvedCast = resolvedCast,
            StepIndex = stepIndex,
        };

        return true;
    }

    public bool TryStartCast(in SkillCastRequest request)
    {
        if (!TryPlanCast(request, out var plan))
            return false;

        var lane = plan.Def.ExecutionLane == SkillExecutionLane.Passive
            ? SkillExecutionLane.Main
            : plan.Def.ExecutionLane;

        return TryStartResolved(plan, request, lane);
    }

    public void Tick(fp dt, uint currentTick)
    {
        skillBook?.Tick(dt);
        TryAutoTriggerPassives(currentTick);

        for (int i = 0; i < laneStates.Length; i++)
            TickLane((SkillExecutionLane)i, laneStates[i], dt, currentTick);
    }

    private void TryAutoTriggerPassives(uint currentTick)
    {
        if (skillBook == null || owner == null)
            return;

        foreach (var pair in skillBook.RuntimeTable)
        {
            var runtime = pair.Value;
            var def = runtime != null ? runtime.Def : null;
            if (def == null || !def.IsPassive || !runtime.IsLearned)
                continue;

            if (runtime.IsCoolingDown || def.Steps == null || def.Steps.Length == 0)
                continue;

            int stepIndex = ResolveStepIndex(def, runtime);
            if (stepIndex < 0 || stepIndex >= def.Steps.Length)
                continue;

            var step = def.Steps[stepIndex];
            if (step == null || !step.CanAutoStartPassive(owner, runtime))
                continue;

            var laneState = GetLaneState(def.ExecutionLane);
            if (laneState.Current != null && !laneState.Current.IsFinished && !laneState.Current.IsCancelled)
                continue;

            var request = new SkillCastRequest
            {
                CasterUid = owner.UnitID,
                SkillId = def.Id,
                Source = SkillRequestSource.System,
                IsPreview = false,
                SmartCast = false,
                RequestTick = currentTick,
            };

            var resolved = new SkillResolvedCast
            {
                Caster = owner
            };

            var plan = new SkillCastPlan
            {
                Def = def,
                Runtime = runtime,
                ResolvedCast = resolved,
                StepIndex = stepIndex,
            };

            StartExecution(laneState, plan, request);
        }
    }

    public ActionLockSnapshot BuildActionLockSnapshot()
    {
        var snapshot = ActionLockSnapshot.Default;

        for (int i = 0; i < laneStates.Length; i++)
        {
            var execution = laneStates[i].Current;
            if (execution == null || execution.IsCancelled || execution.IsFinished)
                continue;

            var profile = execution.GetCurrentStepActionLock();
            if (profile == null || !profile.Enabled)
                continue;

            snapshot.OccupiedChannels |= profile.OccupiedChannels;
            snapshot.BlockedChannels |= profile.BlockedChannels;
        }

        return snapshot;
    }

    private bool TryStartResolved(in SkillCastPlan plan, in SkillCastRequest request, SkillExecutionLane lane)
    {
        var laneState = GetLaneState(lane);

        if (laneState.Current != null && !laneState.Current.IsFinished && !laneState.Current.IsCancelled)
        {
            if (laneState.Current.Def != null && laneState.Current.Def.Id == plan.Def.Id)
            {
                if (laneState.Current.TryTrigger(request.RequestTick))
                    return true;
            }

            return TryHandleWindowRequest(laneState, request, plan);
        }

        StartExecution(laneState, plan, request);
        return true;
    }

    private void StartExecution(LaneRuntimeState laneState, in SkillCastPlan plan, in SkillCastRequest request)
    {
        if (plan.Def.AutoPayManaOnStart && plan.Def.CheckManaCost && plan.Def.ManaCost > 0)
            owner.Stats.ModifyMana(-(fp)plan.Def.ManaCost);

        laneState.Current = new SkillExecution(
            plan.Def,
            plan.Runtime,
            owner,
            this,
            plan.ResolvedCast,
            plan.StepIndex,
            request.RequestTick,
            request.InitialBlackboard);

        laneState.Current.Start(request.RequestTick);
    }

    private void TickLane(SkillExecutionLane lane, LaneRuntimeState laneState, fp dt, uint currentTick)
    {
        var current = laneState.Current;
        if (current == null)
            return;

        current.Tick(dt, currentTick);

        if (!current.IsFinished && !current.IsCancelled)
            return;

        laneState.Current = null;

        if (current.IsFinished && current.Runtime != null)
            ResolveRepeatAndCooldown(current);

        if (laneState.Paused != null)
        {
            laneState.Current = laneState.Paused;
            laneState.Paused = null;
            return;
        }

        if (laneState.HasQueuedRequest)
        {
            var request = laneState.QueuedRequest;
            laneState.HasQueuedRequest = false;
            TryStartCast(request);
        }
    }

    private void ResolveRepeatAndCooldown(SkillExecution finished)
    {
        var def = finished.Def;
        var runtime = finished.Runtime;
        if (def == null || runtime == null)
            return;

        int stepCount = def.Steps != null ? def.Steps.Length : 0;
        bool repeatable = def.UseRepeatCast && stepCount > 1;

        if (repeatable)
        {
            int nextStep = finished.StepIndex + 1;
            if (nextStep < stepCount)
            {
                runtime.BeginRepeatWindow(nextStep, (fp)def.RepeatCastWindow);
                return;
            }

            runtime.ClearRepeatWindow();
            if (def.AutoStartCooldownOnFinish)
                runtime.StartCooldown();
            return;
        }

        runtime.ClearRepeatWindow();
        if (def.AutoStartCooldownOnFinish)
            runtime.StartCooldown();
    }

    private bool TryHandleWindowRequest(LaneRuntimeState laneState, in SkillCastRequest request, in SkillCastPlan incomingPlan)
    {
        if (!TryFindWindow(laneState.Current.Def, request.SkillId, out var window))
            return false;

        switch (window.WindowType)
        {
            case SkillWindowType.QueueAfterCurrent:
                laneState.QueuedRequest = request;
                laneState.HasQueuedRequest = true;
                return true;

            case SkillWindowType.ReplaceCurrent:
                laneState.Current.Cancel();
                laneState.Current = null;
                laneState.Paused = null;
                StartExecution(laneState, incomingPlan, request);
                return true;

            case SkillWindowType.InsertBeforeExecute:
                if (laneState.Paused != null)
                    return false;

                laneState.Paused = laneState.Current;
                laneState.Current = null;
                StartExecution(laneState, incomingPlan, request);
                return true;
        }

        return false;
    }

    private static bool TryFindWindow(SkillDef currentDef, int incomingSkillId, out SkillWindowDef window)
    {
        window = null;

        if (currentDef?.Windows == null)
            return false;

        for (int i = 0; i < currentDef.Windows.Length; i++)
        {
            var candidate = currentDef.Windows[i];
            if (candidate == null)
                continue;

            if (candidate.IncomingSkillId == incomingSkillId)
            {
                window = candidate;
                return true;
            }
        }

        return false;
    }

    private int ResolveStepIndex(SkillDef def, SkillRuntime runtime)
    {
        if (def == null || runtime == null || def.Steps == null || def.Steps.Length == 0)
            return 0;

        if (!def.UseRepeatCast)
        {
            runtime.ClearRepeatWindow();
            return 0;
        }

        int idx = runtime.ResolveCastStepIndex();
        if (idx < 0 || idx >= def.Steps.Length)
        {
            runtime.ClearRepeatWindow();
            return 0;
        }

        return idx;
    }

    private bool CheckBuiltInPreview(UnitCore caster, SkillDef def, SkillRuntime runtime)
    {
        if (caster == null || def == null || runtime == null)
            return false;

        if (!runtime.IsLearned)
            return false;

        if (def.CheckControlBlocked)
        {
            if (caster.IsDead || !caster.CanStartCast())
                return false;
        }

        if (def.CheckCooldown && runtime.IsCoolingDown)
            return false;

        if (def.CheckManaCost && caster.CurrentMana < (fp)def.ManaCost)
            return false;

        return true;
    }

    private bool CheckBuiltInCommit(UnitCore caster, SkillDef def, SkillRuntime runtime, in SkillResolvedCast resolvedCast)
    {
        if (caster == null || def == null || runtime == null)
            return false;

        if (def.RangePolicy == SkillRangePolicy.MustInRange && resolvedCast.NeedApproach)
            return false;

        return true;
    }

    private bool RunStepGates(UnitCore caster, SkillDef def, SkillStepDef step, in SkillCastRequest request, in SkillResolvedCast resolvedCast)
    {
        var gates = step != null ? step.StepGates : null;
        if (gates == null)
            return true;

        for (int i = 0; i < gates.Length; i++)
        {
            var gate = gates[i];
            if (gate == null)
                continue;

            if (!gate.CheckStep(caster, def, step, request, resolvedCast).Passed)
                return false;
        }

        return true;
    }

    private LaneRuntimeState GetLaneState(SkillExecutionLane lane)
    {
        return laneStates[(int)lane];
    }

    public object CaptureState()
    {
        var laneSnapshots = new SkillExecutionLaneSnapshot[laneStates.Length];
        for (int i = 0; i < laneStates.Length; i++)
        {
            var lane = laneStates[i];
            laneSnapshots[i] = new SkillExecutionLaneSnapshot
            {
                Lane = (SkillExecutionLane)i,
                CurrentExecution = lane.Current != null ? lane.Current.CaptureSnapshot() : null,
                PausedExecution = lane.Paused != null ? lane.Paused.CaptureSnapshot() : null,
                HasQueuedRequest = lane.HasQueuedRequest,
                QueuedRequest = lane.HasQueuedRequest ? CaptureRequest(lane.QueuedRequest) : default,
            };
        }

        return new SkillExecutionControllerSnapshot
        {
            LaneSnapshots = laneSnapshots,
            SharedBlackboard = SharedBlackboard.CaptureSnapshot(),
        };
    }

    public void RestoreState(object state)
    {
        if (state is not SkillExecutionControllerSnapshot snap)
            return;

        SharedBlackboard.RestoreSnapshot(snap.SharedBlackboard);

        for (int i = 0; i < laneStates.Length; i++)
        {
            laneStates[i].Current = null;
            laneStates[i].Paused = null;
            laneStates[i].HasQueuedRequest = false;
            laneStates[i].QueuedRequest = default;
        }

        if (snap.LaneSnapshots == null)
            return;

        for (int i = 0; i < snap.LaneSnapshots.Length; i++)
        {
            var laneSnap = snap.LaneSnapshots[i];
            var laneState = GetLaneState(laneSnap.Lane);

            RestoreExecution(ref laneState.Current, laneSnap.CurrentExecution);
            RestoreExecution(ref laneState.Paused, laneSnap.PausedExecution);

            laneState.HasQueuedRequest = laneSnap.HasQueuedRequest;
            if (laneSnap.HasQueuedRequest)
                laneState.QueuedRequest = RestoreRequest(laneSnap.QueuedRequest);
        }
    }

    private void RestoreExecution(ref SkillExecution slot, SkillExecutionSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        if (!skillBook.TryGetDef(snapshot.SkillId, out var def) || !skillBook.TryGetRuntime(snapshot.SkillId, out var runtime))
            return;

        if (def.Steps == null || snapshot.StepIndex < 0 || snapshot.StepIndex >= def.Steps.Length)
            return;

        UnitCore targetUnit = null;
        if (snapshot.HasTargetUnit)
            UnitManager.Instance.Spawns.TryGetValue(snapshot.TargetUnitUid, out targetUnit);

        var resolved = new SkillResolvedCast
        {
            Caster = owner,
            TargetUnit = targetUnit,
            TargetPoint = snapshot.TargetPoint,
            AimDirection = snapshot.AimDirection,
        };

        slot = new SkillExecution(
            def,
            runtime,
            owner,
            this,
            resolved,
            snapshot.StepIndex,
            snapshot.CastStartTick,
            snapshot.Blackboard);

        slot.RestoreSnapshot(snapshot);
    }

    private static SkillCastRequestSnapshot CaptureRequest(in SkillCastRequest request)
    {
        return new SkillCastRequestSnapshot
        {
            CasterUid = request.CasterUid,
            SkillId = request.SkillId,
            Source = request.Source,
            IsPreview = request.IsPreview,
            SmartCast = request.SmartCast,
            HasTargetUnit = request.TargetUnitUid.HasValue,
            TargetUnitUid = request.TargetUnitUid.HasValue ? request.TargetUnitUid.Value : default,
            TargetPoint = request.TargetPoint,
            AimDirection = request.AimDirection,
            RequestTick = request.RequestTick,
            InitialBlackboard = request.InitialBlackboard,
        };
    }

    private static SkillCastRequest RestoreRequest(in SkillCastRequestSnapshot snapshot)
    {
        return new SkillCastRequest
        {
            CasterUid = snapshot.CasterUid,
            SkillId = snapshot.SkillId,
            Source = snapshot.Source,
            IsPreview = snapshot.IsPreview,
            SmartCast = snapshot.SmartCast,
            TargetUnitUid = snapshot.HasTargetUnit ? snapshot.TargetUnitUid : null,
            TargetPoint = snapshot.TargetPoint,
            AimDirection = snapshot.AimDirection,
            RequestTick = snapshot.RequestTick,
            InitialBlackboard = snapshot.InitialBlackboard,
        };
    }

    public void EmitPresentationEvent(SkillPresentationEvent evt)
    {
        PresentationEventEmitted?.Invoke(evt);
    }
}

public sealed class SkillExecutionControllerSnapshot
{
    public SkillExecutionLaneSnapshot[] LaneSnapshots;
    public SkillBlackboardSnapshot SharedBlackboard;
}

public sealed class SkillExecutionLaneSnapshot
{
    public SkillExecutionLane Lane;
    public SkillExecutionSnapshot CurrentExecution;
    public SkillExecutionSnapshot PausedExecution;
    public bool HasQueuedRequest;
    public SkillCastRequestSnapshot QueuedRequest;
}

public struct SkillCastRequestSnapshot
{
    public UnitUID CasterUid;
    public int SkillId;
    public SkillRequestSource Source;
    public bool IsPreview;
    public bool SmartCast;
    public bool HasTargetUnit;
    public UnitUID TargetUnitUid;
    public fp3? TargetPoint;
    public fp3? AimDirection;
    public uint RequestTick;
    public SkillBlackboardSnapshot InitialBlackboard;
}
