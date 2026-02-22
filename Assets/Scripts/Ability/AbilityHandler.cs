using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class AbilityHandler : MonoBehaviour
{
    private UnitCore core;
    public UnitCore Core => core;

    private readonly Dictionary<int, AbilityInfo> abilities = new();

    private void Awake()
    {
        core ??= GetComponent<UnitCore>();
    }

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

    public void Tick(fp deltaTime)
    {
        foreach (var ability in abilities.Values)
            ability.Tick(deltaTime);
    }
}

public struct AbilityCastContext
{
    public UnitUID Caster;
    public UnitUID? TargetUnit;
    public Vector3 TargetPosition;
}
