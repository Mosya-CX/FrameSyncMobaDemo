using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class AbilityHandler : UnitBaseHandler
{
    private readonly Dictionary<int, AbilityInfo> abilities = new();

    public void AddAbility(AbilityData data)
    {
        abilities[data.AbilityId] = new AbilityInfo(data, this);
    }

    public void PressSkill(int id, AbilityCastContext context)
    {
        if (abilities.TryGetValue(id, out var ability))
            ability.OnPress(context);
    }

    public void ReleaseSkill(int id, AbilityCastContext context)
    {
        if (abilities.TryGetValue(id, out var ability))
            ability.OnRelease(context);
    }

    public void CancelSkill(int id)
    {
        if (abilities.TryGetValue(id, out var ability))
            ability.OnCancel();
    }

    public override void Tick(fp deltaTime)
    {
        foreach (var ability in abilities.Values)
            ability.Tick(deltaTime);
    }

    #region øÏ’’∫Õª÷∏¥
    public override object CaptureHandlerState()
    {
        throw new System.NotImplementedException();
    }

    public override void RestoreHandlerState(object state)
    {
        throw new System.NotImplementedException();
    }
    #endregion
}

public struct AbilityCastContext
{
    public UnitUID Caster;
    public UnitUID? TargetUnit;
    public Vector3 TargetPosition;
}
