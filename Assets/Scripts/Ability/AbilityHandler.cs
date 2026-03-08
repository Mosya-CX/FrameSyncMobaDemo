using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public class AbilityHandler : UnitBaseHandler
{
    public readonly Dictionary<int, AbilityInfo> abilities = new();
    public readonly HashSet<AbilityInfo> activeAbilities= new();

    protected override void Awake()
    {
        base.Awake();
        var initialAbilities = owner.definitionConfig.abilityList;
        if (initialAbilities != null && initialAbilities.Length > 0)
            for (int i = 0; i < initialAbilities.Length; i++)
                abilities.Add(initialAbilities[i].Id, new AbilityInfo(initialAbilities[i], this));
    }

    public override void Tick(fp deltaTime)
    {
        foreach (var ability in abilities.Values)
            ability.Tick(deltaTime);
    }

    public void TriggerAbility(int id, AbilityTriggerContext context)
    {
        if (owner.capability.HasFlag(UnitCapability.Cast) && abilities.TryGetValue(id, out var ability))
        {
            if (ability.CurrentLevel == 0)
                return;

            if (ability.state == AbilityState.Cooldown)
                return;
            
            foreach (var actived in activeAbilities)
                if (AbilityTagAnalyzer.CheckConflict(ability.CurrentPhase.Tags, actived.CurrentPhase.Tags))
                {
                    if (actived.state == AbilityState.Precast)
                        return;
                    if (actived.state == AbilityState.Channeling)
                        actived.ExitChanneling();
                }

            ability.Trigger(context);
        }
    }

    public void InactiveAllAbility()
    {
        foreach (var ability in abilities.Values)
            ability.StopAbility();
    }

    public void InterruptChanneling()
    {
        foreach (var ability in abilities.Values)
            ability.ExitChanneling();
    }
    public void InterrupTargetAbilityChanneling(int abilityId)
    {
        if (abilities.TryGetValue(abilityId, out var ability))
            ability.ExitChanneling();
    }

    public bool IsInAbilityPrecast()
    {
        foreach (var actived in activeAbilities)
            if (actived.state == AbilityState.Precast)
                return true;
        return false;
    }

    #region 伤害回调事件
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
        throw new System.NotImplementedException();
    }
    #endregion

    #region 快照和恢复
    public override object CaptureState()
    {
        throw new System.NotImplementedException();
    }

    public override void RestoreState(object state)
    {
        throw new System.NotImplementedException();
    }
    #endregion
}


