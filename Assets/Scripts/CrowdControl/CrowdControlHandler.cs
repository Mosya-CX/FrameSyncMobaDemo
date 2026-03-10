using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public sealed class CrowdControlRuntime
{
    public readonly CrowdControlData Data;
    public fp RemainingTime;

    public CrowdControlRuntime(CrowdControlData data, fp duration)
    {
        Data = data;
        RemainingTime = duration;
    }
}

public class CrowdControlHandler : UnitBaseHandler
{
    public readonly List<CrowdControlRuntime> ActiveControls = new();
    public ControlConstraintSnapshot CurrentSnapshot { get; private set; } = ControlConstraintSnapshot.Default;

    public void AddControl(CrowdControlData data, fp duration)
    {
        if (data == null)
            return;

        var runtime = new CrowdControlRuntime(data, duration);
        ActiveControls.Add(runtime);
        data.OnTakeEffect?.Apply(null);
        RebuildSnapshot();
    }

    public void RemoveControl(CrowdControlData data)
    {
        if (data == null)
            return;

        for (int i = ActiveControls.Count - 1; i >= 0; i--)
        {
            if (ActiveControls[i].Data.Id == data.Id)
            {
                data.OnWearOff?.Apply(null);
                ActiveControls.RemoveAt(i);
            }
        }

        RebuildSnapshot();
    }

    public override void Tick(fp deltaTime)
    {
        bool changed = false;

        for (int i = ActiveControls.Count - 1; i >= 0; i--)
        {
            ActiveControls[i].RemainingTime -= deltaTime;

            if (ActiveControls[i].RemainingTime <= 0)
            {
                ActiveControls[i].Data.OnWearOff?.Apply(null);
                ActiveControls.RemoveAt(i);
                changed = true;
            }
            else
            {
                ActiveControls[i].Data.OnTick?.Apply(null);
            }
        }

        if (changed)
            RebuildSnapshot();
    }

    public bool HasControl(ControlType type)
    {
        for (int i = 0; i < ActiveControls.Count; i++)
        {
            if ((ActiveControls[i].Data.Type & type) != 0)
                return true;
        }
        return false;
    }

    public void Clean()
    {
        ActiveControls.Clear();
        CurrentSnapshot = ControlConstraintSnapshot.Default;
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

            var mul = (fp)data.MoveSpeedMultiplier;
            if (mul < snapshot.MoveSpeedMultiplier)
                snapshot.MoveSpeedMultiplier = mul;
        }

        CurrentSnapshot = snapshot;
    }

    protected override void OnDamageDealt(in DamageInfo info) { }
    protected override void OnDamageTaken(in DamageInfo info) { }
    protected override void OnKill(in DamageInfo info) { }
    protected override void OnDeath(in DamageInfo info) => Clean();

    public override object CaptureState() => null;
    public override void RestoreState(object state) { }
}