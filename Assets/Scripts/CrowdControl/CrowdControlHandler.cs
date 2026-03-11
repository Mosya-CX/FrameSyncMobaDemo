using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public sealed class CrowdControlRuntime
{
    public readonly CrowdControlData Data;
    public fp RemainingTime;
    public UnitCore Source;
    public object UserData;

    public CrowdControlRuntime(CrowdControlData data, fp duration, UnitCore source = null, object userData = null)
    {
        Data = data;
        RemainingTime = duration;
        Source = source;
        UserData = userData;
    }
}

public class CrowdControlHandler : UnitBaseHandler
{
    public readonly List<CrowdControlRuntime> ActiveControls = new();
    public ControlConstraintSnapshot CurrentSnapshot { get; private set; } = ControlConstraintSnapshot.Default;

    public void AddControl(CrowdControlData data, fp duration, UnitCore source = null, object userData = null)
    {
        if (data == null)
            return;

        var runtime = new CrowdControlRuntime(data, duration, source, userData);
        ActiveControls.Add(runtime);

        var context = BuildContext(runtime);
        data.OnTakeEffect?.Apply(context);
        data.SpecialBehavior?.OnApply(context);

        RebuildSnapshot();
    }

    public void RemoveControl(CrowdControlData data)
    {
        if (data == null)
            return;

        for (int i = ActiveControls.Count - 1; i >= 0; i--)
        {
            if (ActiveControls[i].Data.Id != data.Id)
                continue;

            var context = BuildContext(ActiveControls[i]);
            data.SpecialBehavior?.OnRemove(context);
            data.OnWearOff?.Apply(context);
            ActiveControls.RemoveAt(i);
        }

        RebuildSnapshot();
    }

    public override void Tick(fp deltaTime)
    {
        bool dirty = false;

        for (int i = ActiveControls.Count - 1; i >= 0; i--)
        {
            var runtime = ActiveControls[i];
            runtime.RemainingTime -= deltaTime;

            if (runtime.RemainingTime <= 0)
            {
                var removeContext = BuildContext(runtime);
                runtime.Data.SpecialBehavior?.OnRemove(removeContext);
                runtime.Data.OnWearOff?.Apply(removeContext);
                ActiveControls.RemoveAt(i);
                dirty = true;
                continue;
            }

            var tickContext = BuildContext(runtime);
            runtime.Data.OnTick?.Apply(tickContext);
            runtime.Data.SpecialBehavior?.OnTick(tickContext, deltaTime);
        }

        if (dirty)
            RebuildSnapshot();
    }

    public bool HasControl(ControlType type)
    {
        for (int i = 0; i < ActiveControls.Count; i++)
            if ((ActiveControls[i].Data.Type & type) != 0)
                return true;

        return false;
    }

    public void Clean()
    {
        ActiveControls.Clear();
        CurrentSnapshot = ControlConstraintSnapshot.Default;
    }

    private CrowdControlRuntimeContext BuildContext(CrowdControlRuntime runtime)
    {
        return new CrowdControlRuntimeContext
        {
            Owner = owner,
            Data = runtime.Data,
            RemainingTime = runtime.RemainingTime,
            Source = runtime.Source,
            UserData = runtime.UserData,
        };
    }

    private void RebuildSnapshot()
    {
        var snapshot = ControlConstraintSnapshot.Default;

        for (int i = 0; i < ActiveControls.Count; i++)
        {
            var data = ActiveControls[i].Data;

            snapshot.BlockMoveInput |= data.BlockMoveInput;
            snapshot.BlockAttackInput |= data.BlockAttackInput;
            snapshot.BlockCastInput |= data.BlockCastInput;

            snapshot.BlockMove |= data.BlockMove;
            snapshot.BlockTrack |= data.BlockTrack;
            snapshot.BlockAttack |= data.BlockAttack;
            snapshot.BlockCast |= data.BlockCast;
            snapshot.BlockDash |= data.BlockDash;

            snapshot.ForceInterruptCast |= data.ForceInterruptCast;
            snapshot.ForceInterruptAttack |= data.ForceInterruptAttack;
            snapshot.ForceInterruptDash |= data.ForceInterruptDash;

            var moveMul = (fp)data.MoveSpeedMultiplier;
            if (moveMul < snapshot.MoveSpeedMultiplier)
                snapshot.MoveSpeedMultiplier = moveMul;

            if (data.BlockMove) snapshot.BlockedChannels |= ActionChannelMask.Move;
            if (data.BlockTrack) snapshot.BlockedChannels |= ActionChannelMask.Track;
            if (data.BlockAttack) snapshot.BlockedChannels |= ActionChannelMask.Attack;
            if (data.BlockCast) snapshot.BlockedChannels |= ActionChannelMask.Cast;
            if (data.BlockDash) snapshot.BlockedChannels |= ActionChannelMask.Dash;

            if (data.Type.HasFlag(ControlType.Root))
                snapshot.BlockedChannels |= ActionChannelMask.Move | ActionChannelMask.Track;

            if (data.Type.HasFlag(ControlType.Stun) || data.Type.HasFlag(ControlType.Suppress) || data.Type.HasFlag(ControlType.Knockup))
                snapshot.BlockedChannels |= ActionChannelMask.Move | ActionChannelMask.Track | ActionChannelMask.Attack | ActionChannelMask.Cast | ActionChannelMask.Dash | ActionChannelMask.Rotate;
        }

        CurrentSnapshot = snapshot;
    }

    public override object CaptureState() => null;
    public override void RestoreState(object state) { }
}