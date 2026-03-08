using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public class CrowdControlHandler : UnitBaseHandler
{
    public readonly OrderedList<CrowdControlRuntime> affectedContorols = new((c1, c2) => c1.data.Priority.CompareTo(c2.data.Priority));
    public readonly Dictionary<UnitCapability, short> capabilityLimiationReference = new Dictionary<UnitCapability, short>
    {
        {UnitCapability.Move, 0 },
        {UnitCapability.Track, 0 },
        {UnitCapability.Attack, 0 },
        {UnitCapability.Cast, 0 },
        {UnitCapability.Dash, 0 },
    };

    public void AddControl(CrowdControlData controlData, fp duration)
    {
        var runtime = new CrowdControlRuntime(controlData, this, duration);
        affectedContorols.Add(runtime);
        controlData.OnTakeEffect?.Apply(runtime);
    }

    private void AddCapabilityLimiation(UnitCapability limit)
    {
        if (limit.HasFlag(UnitCapability.Move))
            capabilityLimiationReference[UnitCapability.Move]++;
        if (limit.HasFlag(UnitCapability.Track))
            capabilityLimiationReference[UnitCapability.Track]++;
        if (limit.HasFlag(UnitCapability.Attack))
            capabilityLimiationReference[UnitCapability.Attack]++;
        if (limit.HasFlag(UnitCapability.Cast))
            capabilityLimiationReference[UnitCapability.Cast]++;
        if (limit.HasFlag(UnitCapability.Dash))
            capabilityLimiationReference[UnitCapability.Dash]++;
    }

    public void RemoveControl(CrowdControlData control)
    {
        affectedContorols.RemoveAll((c)=>
        {
            if (c.data.Id == control.Id)
            {
                c.data.OnWearOff?.Apply(c);
                return true;
            }
            return false;
        });
    }

    private void RemoveCapabilityLimiation(UnitCapability limit)
    {
        if (limit.HasFlag(UnitCapability.Move))
            capabilityLimiationReference[UnitCapability.Move]--;
        if (limit.HasFlag(UnitCapability.Track))
            capabilityLimiationReference[UnitCapability.Track]--;
        if (limit.HasFlag(UnitCapability.Attack))
            capabilityLimiationReference[UnitCapability.Attack]--;
        if (limit.HasFlag(UnitCapability.Cast))
            capabilityLimiationReference[UnitCapability.Cast]--;
        if (limit.HasFlag(UnitCapability.Dash))
            capabilityLimiationReference[UnitCapability.Dash]--;
    }

    public override void Tick(fp deltaTime)
    {
        for (int i = affectedContorols.Count - 1; i >= 0; i--)
        {
            if (affectedContorols[i].existTimer < 0)
            {
                affectedContorols[i].data.OnWearOff?.Apply(affectedContorols[i]);
                affectedContorols.RemoveAt(i);
            }
                
            affectedContorols[i].existTimer -= deltaTime;
        }
        if (affectedContorols.Count > 0)
            affectedContorols[0].data.OnTick?.Apply(affectedContorols[0]);

        UpdateCapability();
    }

    private void UpdateCapability()
    {
        foreach (var capability in capabilityLimiationReference.Keys)
        {
            if (capabilityLimiationReference[capability] <= 0)
            {
                capabilityLimiationReference[capability] = 0;
                owner.capability = owner.capability & ~capability;
            }
        }
    }

    public bool IsInControlSiffness()
    {
        for (int i = 0; i < affectedContorols.Count; i++)
            if (affectedContorols[i].data.IsSiffness)
                return true;
        return false;
    }

    public void Clean()
    {
        affectedContorols.Clear();
    }

    #region 伤害回调
    protected override void OnDamageDealt(in DamageInfo info)
    {
        throw new System.NotImplementedException();
    }

    protected override void OnDamageTaken(in DamageInfo info)
    {
        throw new System.NotImplementedException();
    }

    protected override void OnKill(in DamageInfo info)
    {
        throw new System.NotImplementedException();
    }

    protected override void OnDeath(in DamageInfo info)
    {
        Clean();
    }
    #endregion

    #region 快照和回滚
    public override object CaptureState()
    {
        return null;
    }

    public override void RestoreState(object state)
    {

    }
    #endregion
}
